"""Offline file/signal checks. This is NOT a Unity play-mode or perceptual listening test."""
from pathlib import Path
import json, hashlib, subprocess
import numpy as np
from scipy.signal import resample_poly, butter, sosfilt
import tree_sitter, tree_sitter_c_sharp
from build_combat_audio import ROOT, DEST, HEROES, ACTIONS, HAS_HIT, SR, decode

def main():
 parser=tree_sitter.Parser(tree_sitter.Language(tree_sitter_c_sharp.language()))
 cs=list((ROOT/'Assets/Alterverse').rglob('*.cs'))
 errors=[str(p.relative_to(ROOT)) for p in cs if parser.parse(p.read_bytes()).root_node.has_error]
 assert not errors,errors
 rows=[];hashes={};expected=[]
 for h,hero in enumerate(HEROES):
  for a,action in enumerate(ACTIONS):
   for phase in ['Cast']+(['Impact','Windup'] if HAS_HIT[h][a] else []):
    count=3 if phase!='Windup' and (a==0 or h==2 and a==4) else 1
    group=[]
    for v in range(count):
     p=DEST/'Heroes'/hero/(action+'_'+phase+('' if v==0 else '_v'+str(v+1))+'.ogg');assert p.exists(),p;expected.append(p)
     x=decode(p);digest=hashlib.sha256(x.tobytes()).hexdigest();group.append(digest)
    assert len(set(group))==count,(hero,action,phase,'identical variants')
 paths=expected+list((DEST/'SFX').glob('*.ogg'))
 for p in paths:
  x=decode(p);assert len(x)>100 and np.isfinite(x).all() and np.max(abs(x))>1e-5,p
  true_peak=float(np.max(abs(resample_poly(x,4,1))))
  assert true_peak<.13,(p,true_peak)
  meta=Path(str(p)+'.meta');assert meta.exists(),p
  guids=[line[6:] for line in meta.read_text().splitlines() if line.startswith('guid: ')];assert len(guids)==1
  assert guids[0] not in hashes,(p,'duplicate GUID');hashes[guids[0]]=str(p)
  mobile=sosfilt(butter(2,[250,6000],fs=SR,btype='bandpass',output='sos'),x)
  rows.append(dict(path=str(p.relative_to(ROOT)),seconds=len(x)/SR,peak_dbfs=float(20*np.log10(max(abs(x)))),true_peak_dbfs=float(20*np.log10(true_peak)),rms_dbfs=float(20*np.log10(np.sqrt(np.mean(x*x)))),mobile_band_energy_ratio=float(np.sum(mobile*mobile)/np.sum(x*x))))
 # Eight voices, worst-case aligned decoded peaks, max possible gain variation.
 worst_bound=8*max(10**(r['true_peak_dbfs']/20) for r in rows)*1.035
 assert worst_bound<1,('pool headroom',worst_bound)
 # Deterministic rapid-fire overlap at actual .18s volley interval, all three takes.
 volley=np.zeros(SR*3)
 for i in range(12):
  p=DEST/'Heroes/Nyra'/('R_Cast'+('' if i%3==0 else '_v'+str(i%3+1))+'.ogg');x=decode(p);start=int(i*.18*SR);volley[start:start+len(x)]+=x*.9
 assert max(abs(volley))<1
 # Original asset GUIDs and complete balance catalog remain byte-for-byte unchanged.
 result=subprocess.check_output(['git','diff','--name-only'],cwd=ROOT,text=True).splitlines()
 assert not any(p.endswith('.meta') for p in result),'existing meta changed'
 assert 'Assets/Alterverse/Scripts/Characters/HeroAbilityKit.cs' not in result
 assert 'Assets/Alterverse/Scripts/Characters/HeroIdentity.cs' not in result
 report=dict(check_type='Offline C# syntax, asset mapping and decoded signal analysis; not Unity compilation, runtime, device or listening tests',csharp_files_parsed=len(cs),hero_actions=35,hero_audio_files=len(expected),generic_audio_files=len(paths)-len(expected),decoded_float_mib=sum(r['seconds']*SR*4 for r in rows)/1048576,max_true_peak_dbfs=max(r['true_peak_dbfs'] for r in rows),eight_voice_worst_aligned_peak_dbfs=float(20*np.log10(worst_bound)),rapid_volley_peak_dbfs=float(20*np.log10(max(abs(volley)))),unity_play_mode='NOT RUN: Unity editor unavailable',phone_headphone_listening='NOT RUN',network_session='NOT RUN',assets=rows)
 (ROOT/'Docs/CombatAudioValidation.json').write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps({k:v for k,v in report.items() if k!='assets'},indent=2))
if __name__=='__main__':main()
