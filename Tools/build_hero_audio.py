"""Offline CC0 recording-layer renderer. Requires ffmpeg, numpy and the Kenney sci-fi Audio directory.
Usage: python Tools/build_hero_audio.py /path/to/Kenney/Audio
No oscillators, test tones or runtime synthesis. Rebuilds deterministic, distinct mono clips.
"""
import sys, json, hashlib, subprocess, uuid
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path(sys.argv[1])
DEST=ROOT/'Assets/Alterverse/Resources/Audio/Heroes'
SR=32000
# Each row is an authored primary recording + texture + rhythm, not a pitch preset.
# Hero: [(basic, Q, W, E, R)] with primary, texture, impact recording, contour.
RECIPES={
 'Veyr':[
 ('impactMetal_000','thrusterFire_000','impactMetal_002','cut'),
 ('impactMetal_003','spaceEngineSmall_001','impactMetal_001','double'),
 ('doorClose_000','slime_000','impactMetal_004','tear'),
 ('thrusterFire_002','spaceEngineLow_000',None,'reverse'),
 ('explosionCrunch_002','spaceEngineLarge_000','lowFrequency_explosion_001','collapse')],
 'Kaelon':[
 ('impactMetal_004','spaceEngineLow_001','explosionCrunch_000','weight'),
 ('explosionCrunch_001','impactMetal_001','impactMetal_003','slam'),
 ('forceField_001','spaceEngineLarge_001',None,'swell'),
 ('doorClose_001','spaceEngineLow_002','lowFrequency_explosion_000','anchor'),
 ('lowFrequency_explosion_001','forceField_003','explosionCrunch_003','citadel')],
 'Nyra':[
 ('impactMetal_002','thrusterFire_001','impactMetal_000','snap'),
 ('thrusterFire_003','impactMetal_003','impactMetal_001','prism'),
 ('doorOpen_000','spaceEngineSmall_002',None,'shift'),
 ('impactMetal_001','forceField_000','impactMetal_004','snare'),
 ('thrusterFire_004','spaceEngineSmall_004','explosionCrunch_001','volley')],
 'Elyra':[
 ('slime_001','spaceEngineSmall_000','impactMetal_003','pulse'),
 ('forceField_002','spaceEngineLarge_002','explosionCrunch_004','orbit'),
 ('spaceEngineLow_003','slime_000','impactMetal_002','invert'),
 ('doorOpen_001','thrusterFire_000',None,'fold'),
 ('spaceEngineLarge_004','lowFrequency_explosion_000','explosionCrunch_002','singularity')],
 'Mira':[
 ('slime_000','spaceEngineSmall_003','impactMetal_002','seed'),
 ('forceField_000','slime_001','impactMetal_000','resonate'),
 ('forceField_004','spaceEngineLow_004',None,'heal'),
 ('forceField_001','spaceEngineSmall_001',None,'shelter'),
 ('spaceEngineLarge_003','forceField_004','forceField_002','surge')],
 'Rook':[
 ('doorClose_000','impactMetal_001','explosionCrunch_001','crush'),
 ('explosionCrunch_000','thrusterFire_002','impactMetal_004','swing'),
 ('doorClose_001','forceField_003',None,'grip'),
 ('impactMetal_003','explosionCrunch_004','explosionCrunch_003','storm'),
 ('lowFrequency_explosion_000','spaceEngineLarge_001','explosionCrunch_004','overdrive')]
}
ACTIONS=['Basic','Q','W','E','R']
DURATIONS=[.16,.27,.34,.29,.52]
cache={}
def recording(name):
 if name not in cache:
  data=subprocess.check_output(['ffmpeg','-v','error','-i',str(SOURCE/(name+'.ogg')),'-ac','1','-ar',str(SR),'-f','f32le','-'])
  cache[name]=np.frombuffer(data,dtype=np.float32).copy()
 return cache[name]
def layer(name,seconds,offset,reverse=False):
 x=recording(name);n=int(seconds*SR);start=min(int(offset*SR),max(0,len(x)-n));x=x[start:start+n].copy()
 if reverse:x=x[::-1].copy()
 if len(x)<n:x=np.pad(x,(0,n-len(x)))
 rms=np.sqrt(np.mean(x*x));x/=max(rms,1e-5)
 fade=min(int(.015*SR),n//4);tail=min(int(.075*SR),n//3)
 x[:fade]*=np.linspace(0,1,fade);x[-tail:]*=np.linspace(1,0,tail)
 return x
def meta(path,folder=False):
 p=Path(str(path)+'.meta')
 if p.exists():return
 uid=uuid.uuid5(uuid.NAMESPACE_URL,'dimensions-front/'+str(path.relative_to(ROOT))).hex
 if folder:p.write_text(f'fileFormatVersion: 2\nguid: {uid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
 else:p.write_text(f'fileFormatVersion: 2\nguid: {uid}\nAudioImporter:\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 32000\n    compressionFormat: 1\n    quality: 0.7\n    conversionMode: 0\n  forceToMono: 1\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n')
DEST.mkdir(parents=True,exist_ok=True);meta(DEST,True)
manifest=[]
for h,(hero,recipes) in enumerate(RECIPES.items()):
 directory=DEST/hero;directory.mkdir(exist_ok=True);meta(directory,True)
 for a,(body,texture,hit,contour) in enumerate(recipes):
  for phase in ['Cast','Impact'] if hit else ['Cast']:
   duration=DURATIONS[a] if phase=='Cast' else (.12 if a==0 else .22 if a<4 else .32)
   primary=body if phase=='Cast' else hit
   secondary=texture if phase=='Cast' else body
   # Different source combinations and explicitly varied envelopes/rhythms create each signature.
   x=layer(primary,duration,0,contour in ('reverse','invert','fold') and phase=='Cast')
   y=layer(secondary,duration,.045*(a+1),contour in ('tear','collapse','singularity') and phase=='Cast')
   envelope=np.linspace(.2,1,len(x)) if contour in ('swell','heal','surge','citadel') else np.exp(-np.linspace(0,2.2+a*.1,len(x)))
   mix=x*.72+y*envelope*.28
   if contour in ('double','anchor','prism','snare','orbit','volley','storm','overdrive'):
    delay=int((.028+a*.013)*SR);mix[delay:]+=x[:-delay]*(.18 if phase=='Cast' else .1)
   if contour in ('weight','slam','crush','swing','collapse','singularity','overdrive'):
    mix+=layer('lowFrequency_explosion_001',duration,.1)*.16
   cutoff=[2600,2200,2900,2400,2600,2100][h]
   filters=f'highpass=f=75,equalizer=f=2800:t=q:w=0.7:g=-6,lowpass=f={cutoff}:p=2'
   raw=subprocess.check_output(['ffmpeg','-v','error','-f','f32le','-ar',str(SR),'-ac','1','-i','-','-af',filters,'-f','f32le','-'],input=mix.astype(np.float32).tobytes())
   out=np.frombuffer(raw,dtype=np.float32).copy();rms=np.sqrt(np.mean(out*out));peak=np.max(abs(out));assert rms>1e-7
   rms_target=-29 if a==0 else -26 if a<4 else -25
   out*=min(10**(rms_target/20)/rms,10**(-7/20)/peak)
   path=directory/(ACTIONS[a]+'_'+phase+'.ogg')
   subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar',str(SR),'-ac','1','-i','-','-c:a','libvorbis','-q:a','3',str(path)],input=out.tobytes(),check=True);meta(path)
   sources={primary,secondary}
   if contour in ('weight','slam','crush','swing','collapse','singularity','overdrive'):sources.add('lowFrequency_explosion_001')
   manifest.append({'hero':hero,'action':ACTIONS[a],'phase':phase,'path':str(path.relative_to(ROOT)),'character':contour,'duration':duration,'cutoff_hz':cutoff,'rms_target_dbfs':rms_target,'sources':[{'file':n+'.ogg','sha256':hashlib.sha256((SOURCE/(n+'.ogg')).read_bytes()).hexdigest()} for n in sorted(sources)]})
(ROOT/'Docs/HeroAudioAssets.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(f'{len(manifest)} layered recording assets, {sum((ROOT/m["path"]).stat().st_size for m in manifest)} bytes')
