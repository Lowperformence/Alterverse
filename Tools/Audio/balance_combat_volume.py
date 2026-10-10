#!/usr/bin/env python3
"""Balance original combat recordings against two user-selected references.
Needs ffmpeg, ffprobe and numpy. Never run against already balanced recordings.
Use an original audit as --audit and its matching original files as --source.
Preserve source formats, duration and all existing Unity import GUIDs.
"""
import argparse, concurrent.futures, hashlib, json, math, re, subprocess
from pathlib import Path
import numpy as np

def db(value):
    return 20 * math.log10(max(float(value), 1e-15))

def inspect(path):
    stream=json.loads(subprocess.check_output(["ffprobe","-v","error","-select_streams","a:0",
        "-show_entries","stream=sample_rate,channels","-of","json",str(path)],text=True))["streams"][0]
    raw=subprocess.check_output(["ffmpeg","-v","error","-i",str(path),"-f","f32le","-ac","2","-ar","48000","-"])
    wave=np.frombuffer(raw,dtype="<f4").reshape(-1,2).astype(np.float64)
    assert len(wave)>0 and np.isfinite(wave).all(),path
    power=np.mean(wave*wave,axis=1);window=min(len(power),4800)
    integral=np.concatenate(([0.],np.cumsum(power)))
    rms=math.sqrt(float(np.max((integral[window:]-integral[:-window])/window)))
    result=subprocess.run(["ffmpeg","-hide_banner","-nostats","-i",str(path),"-af",
        "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json","-f","null","-"],capture_output=True,text=True,check=True)
    loud=json.loads(re.search(r'\{\s*"input_i"[\s\S]+?\}',result.stderr)[0])
    return {"sample_rate":int(stream["sample_rate"]),"channels":int(stream["channels"]),
        "seconds":len(wave)/48000,"max100ms_rms_dbfs":db(rms),
        "sample_peak_dbfs":db(np.max(np.abs(wave))),"true_peak_dbtp":float(loud["input_tp"]),
        "integrated_lufs":float(loud["input_i"]) if math.isfinite(float(loud["input_i"])) else None}

def encode(source,destination,adjustment,ceiling,original):
    # Catch reconstructed peaks at 192 kHz; disable the limiter's default makeup.
    # Compensation, padding and exact trimming preserve the complete original cue.
    filters=(f"aresample=192000,volume={adjustment:.9f}dB,"
        f"alimiter=limit={10**(ceiling/20):.9f}:attack=2:release=35:level=false:latency=true,"
        f"aresample={original['sample_rate']},apad,atrim=duration={original['seconds']:.9f}")
    codec=(["-c:a","libvorbis","-q:a","5"] if source.suffix==".ogg" else
        ["-c:a","libmp3lame","-b:a","256k"] if source.suffix==".mp3" else ["-c:a","pcm_s16le"])
    subprocess.run(["ffmpeg","-v","error","-y","-i",str(source),"-af",filters,
        "-ac",str(original["channels"]),"-ar",str(original["sample_rate"]),*codec,str(destination)],check=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source",type=Path,required=True)
    parser.add_argument("--audit",type=Path,required=True)
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    if args.source.resolve()==args.output.resolve():raise ValueError("Do not overwrite source recordings")
    audit=json.loads(args.audit.read_text());active=[r for r in audit["rows"] if r["active"]]
    basic=next(r for r in active if r["hero"]=="Nerok" and r["action"]==0)
    paradox=next(r for r in active if r["hero"]=="Traveler" and r["action"]==4 and r["phase"]==0)
    basic_target=basic["output_max100ms_rms_dbfs"]+db(.7)
    basic_peak=basic["output_true_peak_dbtp"]+db(.7)
    ability_target=paradox["output_integrated_lufs"]
    ability_peak=paradox["output_true_peak_dbtp"]
    basic_adjustments={hero:basic_target-max(r["output_max100ms_rms_dbfs"] for r in active
        if r["hero"]==hero and r["action"]==0) for hero in audit["loadouts"]}

    def process(row):
        source=args.source/row["path"];original=inspect(source)
        gain=row["gain"]*(.7 if row["hero"]=="Nerok" and row["action"]==0 else 1)
        gain_db=db(gain)
        # Keep the two references and quiet support layers unchanged.
        changed=(row["action"]==0 and row["hero"]!="Nerok") or (
            row["action"]>0 and row["phase"]==0 and row["path"]!=paradox["path"])
        # Existing silent preparation placeholders must stay silent.
        changed=changed and original["sample_peak_dbfs"]>-100
        result={**row,"changed":changed,"new_gain":gain,"before":original}
        if not changed:
            after=original
        else:
            destination=args.output/row["path"];destination.parent.mkdir(parents=True,exist_ok=True)
            if row["action"]==0:
                metric="max100ms_rms_dbfs";adjustment=basic_adjustments[row["hero"]]
                target=row["output_max100ms_rms_dbfs"]+adjustment;peak_limit=basic_peak
            else:
                metric="integrated_lufs";target=ability_target
                adjustment=target-(original[metric]+gain_db);peak_limit=ability_peak
            ceiling=min(-1.5,peak_limit-gain_db)
            # Leave extra allowance before lossy encoding/resampling.
            ceiling-=.15
            for attempt in range(8):
                encode(source,destination,adjustment,ceiling,original)
                after=inspect(destination);actual=after[metric]+gain_db
                peak_error=max(after["true_peak_dbtp"]+1.5,after["true_peak_dbtp"]+gain_db-peak_limit)
                if peak_error>.05:ceiling-=peak_error+.10
                elif abs(actual-target)>.30:adjustment+=max(-3.,min(3.,target-actual))
                else:break
            assert abs(actual-target)<.65,(row["path"],actual,target)
            assert after["true_peak_dbtp"]<=-1.4,(row["path"],after)
            assert after["true_peak_dbtp"]+gain_db<=peak_limit+.15,(row["path"],after)
            assert abs(after["seconds"]-original["seconds"])<.03,(row["path"],after["seconds"],original["seconds"])
            assert (after["channels"],after["sample_rate"])==(original["channels"],original["sample_rate"]),row["path"]
            result.update({"metric":metric,"target":target,"applied_gain_db":adjustment,
                "limiter_source_ceiling_dbtp":ceiling,"sha256":hashlib.sha256(destination.read_bytes()).hexdigest()})
        result["after"]=after
        result["output_max100ms_rms_dbfs"]=after["max100ms_rms_dbfs"]+gain_db
        result["output_true_peak_dbtp"]=after["true_peak_dbtp"]+gain_db
        result["output_integrated_lufs"]=None if after["integrated_lufs"] is None else after["integrated_lufs"]+gain_db
        return result

    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        rows=list(pool.map(process,active))
    for hero in audit["loadouts"]:
        level=max(r["output_max100ms_rms_dbfs"] for r in rows if r["hero"]==hero and r["action"]==0)
        assert abs(level-basic_target)<.65,(hero,level,basic_target)
    casts=[r for r in rows if r["action"]>0 and r["phase"]==0]
    assert len(casts)==32
    assert all(abs(r["output_integrated_lufs"]-ability_target)<.65 for r in casts)
    report={"source_commit":audit["commit"],"basic_reference":basic["path"],"nerok_basic_reduction":.30,
        "basic_reference_max100ms_rms_dbfs":basic_target,"basic_reference_true_peak_dbtp":basic_peak,
        "ability_reference":paradox["path"],"ability_reference_output_lufs":ability_target,
        "ability_reference_true_peak_dbtp":ability_peak,
        "scope":"Offline file loudness and code gain estimates; not Unity import, complete live mix or listening verification",
        "reference_files_changed":False,"support_layers_changed":False,"rows":rows}
    path=args.output/"Docs/CombatVolumeBalance-2026-10-10.json";path.parent.mkdir(parents=True,exist_ok=True)
    def finite(value):
        if isinstance(value,float) and not math.isfinite(value):return None
        if isinstance(value,dict):return {k:finite(v) for k,v in value.items()}
        if isinstance(value,list):return [finite(v) for v in value]
        return value
    path.write_text(json.dumps(finite(report),indent=2,allow_nan=False)+"\n")
    print(json.dumps({k:v for k,v in report.items() if k!="rows"},indent=2))
    print(f"Verified {len(rows)} active cues; {sum(r['changed'] for r in rows)} audio assets changed.")

if __name__=="__main__":
    main()

