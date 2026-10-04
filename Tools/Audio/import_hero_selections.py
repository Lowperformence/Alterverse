"""Import the user's exact 2026-10-04 MP3 selection; requires ffmpeg, standard Python only.
Usage: python3 Tools/Audio/import_hero_selections.py /path/to/selected_sfx
Existing Unity .ogg.meta files are preserved by the commit. Never guess duplicate filenames.
"""
import array, hashlib, json, math, shutil, subprocess, sys, tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path(sys.argv[1])
ART=ROOT/'ArtSource/Audio/HeroSelections';ORIGINALS=ART/'Originals'
ORIGINALS.mkdir(parents=True,exist_ok=True)
MAPPINGS=[
 ('floraphonic-swing-whoosh-metal-hit-19-234636.mp3',[('Kaelon','Q','Anchor Strike')]),
 ('floraphonic-swing-whoosh-metal-hit-14-234631.mp3',[('Kaelon','E','Gravity Anchor')]),
 ('freesound_community-funny-swish-101878(1).mp3',[('Veyr','E','Gapfold'),('Traveler','W','Rift Step'),('Elyra','E','Phase Fold')]),
 ('dragon-studio-sword-slice-393847(1).mp3',[('Veyr','W','Rift Scar')]),
 ('dragon-studio-sword-slice-2-393845.mp3',[('Veyr','Q','Null Cut')]),
 ('soundreality-sci-fi-gun-shot-312659(1).mp3',[('Nyra','E','Light Snare')]),
 ('universfield-sci-fi-blaster-shot-229313(1).mp3',[('Nyra','Q','Prism Shot')]),
 ('yodguard-short-energy-beam-shot-4-482500(1).mp3',[('Elyra','Q','Orbit Fracture')]),
 ('yodguard-short-energy-beam-shot-1-482495.mp3',[('Elyra','W','Invert Field')]),
 ('yodguard-healing-magic-2-378663(1).mp3',[('Mira','W','Growth Veil')]),
 ('yodguard-short-energy-beam-shot-4-482500.mp3',[('Mira','E','Venn Shelter')]),
 ('freesound_community-power-down-7103.mp3',[('Mira','Q','Resonant Seed')]),
 ('audiopapkin-big-robot-footstep-014-445104.mp3',[('Rook','E','Wreckstorm')]),
 ('yodguard-short-energy-beam-shot-4-482500 (1).mp3',[('Traveler','E','Chrono Snare')]),
 ('audiopapkin-big-robot-footstep-009-426481.mp3',[('Nerok','E','Breaker Wave')]),
]
report=[]
for filename,targets in MAPPINGS:
 src=SOURCE/filename
 assert src.is_file(),filename
 shutil.copyfile(src,ORIGINALS/filename)
 raw=subprocess.check_output(['ffmpeg','-v','error','-i',str(src),'-ac','1','-ar','44100','-f','f32le','pipe:1'])
 samples=array.array('f');samples.frombytes(raw)
 assert samples and all(math.isfinite(x) for x in samples),filename
 original_peak=max(abs(x) for x in samples);assert original_peak>1e-6,filename
 # Only leading digital silence is removed, not the whoosh/attack or reverb tail.
 first=next(i for i,x in enumerate(samples) if abs(x)>.0008)
 first=max(0,first-44);samples=samples[first:]
 gain=.63/original_peak
 for i in range(len(samples)):
  fade=min(1,i/88,(len(samples)-1-i)/441)
  samples[i]*=gain*max(0,fade)
 destinations=[]
 for hero,slot,ability in targets:
  out=ROOT/f'Assets/Alterverse/Resources/Audio/Heroes/{hero}/{slot}_Cast.ogg';out.parent.mkdir(parents=True,exist_ok=True)
  # Never truncate the live Unity asset before encoding and verification succeed.
  with tempfile.NamedTemporaryFile(dir=out.parent,suffix='.pending',delete=False) as f: pending=Path(f.name)
  try:
   subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar','44100','-ac','1','-i','pipe:0','-c:a','libvorbis','-q:a','5','-f','ogg',str(pending)],input=samples.tobytes(),check=True)
   assert pending.stat().st_size>100,filename
   check=array.array('f');check.frombytes(subprocess.check_output(['ffmpeg','-nostdin','-v','error','-i',str(pending),'-f','f32le','-ac','1','pipe:1']))
   assert check and all(math.isfinite(x) for x in check),filename
   peak=max(abs(x) for x in check);assert 0<peak<.95,(filename,peak)
   pending.replace(out)
  finally:
   pending.unlink(missing_ok=True)
  destinations.append(dict(hero=hero,slot=slot,ability=ability,path=out.relative_to(ROOT).as_posix(),decodedPeak=round(peak,6),sha256=hashlib.sha256(out.read_bytes()).hexdigest()))
 report.append(dict(source=filename,sourceSha256=hashlib.sha256(src.read_bytes()).hexdigest(),sourcePeak=round(original_peak,6),gain=round(gain,6),trimmedLeadingSeconds=round(first/44100,6),durationSeconds=round(len(samples)/44100,6),targets=destinations))
for row in report:
 for target in row['targets']:
  data=(ROOT/target['path']).read_bytes()
  assert len(data)>100 and hashlib.sha256(data).hexdigest()==target['sha256'],target['path']
manifest_pending=ART/'manifest.json.pending'
manifest_pending.write_text(json.dumps(report,indent=2)+'\n')
manifest_pending.replace(ART/'manifest.json')
print(json.dumps([dict(source=r['source'],seconds=r['durationSeconds'],targets=[x['hero']+' '+x['slot'] for x in r['targets']]) for r in report],indent=2))
