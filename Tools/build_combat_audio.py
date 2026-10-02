"""Recorded-source combat renderer (ffmpeg, numpy, scipy), no runtime synthesis.
Bootstrap: python Tools/build_combat_audio.py --import-sources /tmp/audio_sources
Rebuild:   python Tools/build_combat_audio.py
Original .meta files are never overwritten. No oscillator/noise generators.
"""
from pathlib import Path
import argparse, subprocess, json, hashlib, shutil, uuid
import numpy as np
from scipy import signal
ROOT=Path(__file__).resolve().parents[1]; SR=32000
SRC=ROOT/'ArtSource/Audio/Combat'; DEST=ROOT/'Assets/Alterverse/Resources/Audio'
HEROES=['Veyr','Kaelon','Nyra','Elyra','Mira','Rook','Traveler']; ACTIONS=['Basic','Q','W','E','R']
HAS_HIT=[[True,True,True,False,True],[True,True,False,True,True],[True,True,False,True,True],[True,True,True,False,True],[True,True,False,False,True],[True,True,False,True,True],[True,True,False,True,True]]
# Different recorded takes provide variants; pitch changes are only an additional design layer.
PALETTES={
 'Veyr':dict(body=['knifeSlice','knifeSlice2','drawKnife3'],hit=['chop','impactPunch_medium_001','impactPunch_medium_003'],texture=['drawKnife1','drawKnife2','drawKnife3'],low=6800),
 'Kaelon':dict(body=['impactMetal_heavy_000','impactMetal_heavy_002','impactMetal_heavy_004'],hit=['impactPlate_heavy_000','impactPlate_heavy_002','impactPlate_heavy_004'],texture=['silence','silence','silence'],low=5800),
 'Nyra':dict(body=['rifle_T17','rifle_T27','rifle_M21'],hit=['impactSoft_heavy_000','impactSoft_heavy_002','impactSoft_heavy_004'],texture=['metalClick','metalLatch','drawKnife2'],low=8200),
 'Elyra':dict(body=['impactMining_000','impactMining_002','impactMining_004'],hit=['impactGlass_heavy_000','impactGlass_heavy_002','impactGlass_heavy_004'],texture=['silence','silence','silence'],low=5000),
 'Mira':dict(body=['impactGlass_light_000','impactGlass_light_002','impactGlass_light_004'],hit=['impactBell_heavy_000','impactBell_heavy_002','impactBell_heavy_004'],texture=['bookFlip1','bookFlip2','bookFlip3'],low=6800),
 'Rook':dict(body=['cloth1','cloth2','cloth4'],hit=['impactPunch_heavy_000','impactPunch_heavy_002','impactPunch_heavy_004'],texture=['impactSoft_heavy_001','impactSoft_heavy_003','dropLeather'],low=5200),
 'Traveler':dict(body=['impactGlass_medium_001','impactGlass_medium_003','impactGlass_medium_004'],hit=['impactWood_heavy_001','impactWood_heavy_002','impactWood_heavy_003'],texture=['bookOpen','bookClose','bookPlace2'],low=6300),
}
cache={};used={}
def decode(p):
 return np.frombuffer(subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(SR),'-f','f32le','-']),np.float32).copy()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def bootstrap(folder):
 SRC.mkdir(parents=True,exist_ok=True);provenance=[]
 names=set(n for pal in PALETTES.values() for key in ('body','hit','texture') for n in pal[key] if not n.startswith('rifle') and n!='silence')
 for n in sorted(names):
  files=list(folder.rglob(n+'.ogg'));assert files,n;p=files[0];shutil.copyfile(p,SRC/(n+'.ogg'))
  pack='rpg-audio' if '/rpg/' in str(p) else 'impact-sounds'
  provenance.append(dict(file=n+'.ogg',creator='Kenney',url='https://kenney.nl/assets/'+pack,license='CC0-1.0',original_sha256=sha(p)))
 for n,stem in [('rifle_T17','T_17P'),('rifle_T27','T_27P'),('rifle_M21','M_21P')]:
  p=next(folder.rglob(stem+'.wav'));x=decode(p)
  # Preserve the first complete rifle shot, with a 4ms pre-roll, removing long outdoor ambience.
  windows=np.array([np.sqrt(np.mean(x[i:i+160]**2)) for i in range(0,len(x),160)])
  first=int(np.where(windows>max(windows)*.3)[0][0])*160;start=max(0,first-128);take=x[start:start+int(.65*SR)]
  out=SRC/(n+'.wav');subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar',str(SR),'-ac','1','-i','-','-c:a','pcm_s16le',str(out)],input=take.tobytes(),check=True)
  provenance.append(dict(file=out.name,creator='Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney',url='https://opengameart.org/content/the-free-firearm-sound-library',license='CC0-1.0',original_file=str(p.relative_to(folder/'firearms')),original_sha256=sha(p),extract_start_seconds=start/SR,extract_duration_seconds=len(take)/SR,extracted_sha256=sha(out)))
 (SRC/'sources.json').write_text(json.dumps(provenance,indent=2)+'\n')
 shutil.copyfile(folder/'impact/License.txt',ROOT/'Docs/AudioLicenses/Kenney-Impact-CC0.txt')
 shutil.copyfile(folder/'rpg/License.txt',ROOT/'Docs/AudioLicenses/Kenney-RPG-CC0.txt')
def record(n):
 if n not in cache:
  p=next(SRC.glob(n+'.*'));x=decode(p)
  # Trim only leading silence, never the initial transient.
  nz=np.flatnonzero(abs(x)>max(abs(x))*.008);x=x[max(0,int(nz[0])-64):] if len(nz) else x
  cache[n]=x;used[n]=dict(file=str(p.relative_to(ROOT)),sha256=sha(p))
 return cache[n]
def layer(n,duration,rate=1,reverse=False):
 x=record(n).copy()
 if rate!=1:x=signal.resample_poly(x,int(1000/rate),1000)
 if reverse:x=x[::-1]
 length=int(duration*SR);x=x[:length];x=np.pad(x,(0,max(0,length-len(x))))
 rms=np.sqrt(np.mean(x*x));x/=max(rms,1e-6)
 # A gentle source compressor preserves attack detail without enormous isolated peaks.
 x=np.tanh(x*.35)
 return x
def mix_layer(out,n,gain=1,delay=0,rate=1,reverse=False):
 # Remove armor clatter and creaking layers without replacing the body/impact recordings.
 if n=="silence":return
 start=int(delay*SR)
 if start>=len(out):return
 x=layer(n,(len(out)-start)/SR,rate,reverse);out[start:]+=x*gain

def render(hero,a,phase,v):
 pal=PALETTES[hero];b=pal['body'];hit=pal['hit'];t=pal['texture'];k=(a+v)%3
 duration=(.08 if a==0 else .22 if hero=='Nyra' and a==4 else .18 if a<4 else .48) if phase=='Windup' else (.28 if a==0 else .36 if a<4 else .62)
 if phase=='Cast' and hero=='Nyra':duration=.22 if a==4 else .32
 out=np.zeros(int(duration*SR),np.float64)
 if phase=='Windup':
  mix_layer(out,t[k],.5,rate=.85 if hero in ('Kaelon','Rook') else 1,reverse=hero in ('Elyra','Traveler','Mira'))
  if hero in ('Elyra','Traveler'):mix_layer(out,b[k],.5,rate=.55 if hero=='Elyra' else .85,reverse=True)
  out*=np.linspace(.1,1,len(out))
 elif phase=='Impact':
  mix_layer(out,hit[k],1,rate=.78 if hero in ('Kaelon','Rook','Elyra') else 1)
  if hero=='Veyr':mix_layer(out,b[(k+1)%3],.22,.012)
  elif hero=='Kaelon':mix_layer(out,b[(k+1)%3],.55,.018,.65)
  elif hero=='Rook':mix_layer(out,t[k],.45,.012,.8)
  elif hero=='Nyra':mix_layer(out,'impactPunch_heavy_002',.24,.008)
  elif hero=='Mira':mix_layer(out,b[k],.55,.016,1.12)
  elif hero=='Elyra':mix_layer(out,b[(k+1)%3],.7,.014,.55)
  elif hero=='Traveler':mix_layer(out,b[k],.55,.028,1,True)
 else:
  if hero=='Nyra' and a!=2:
   mix_layer(out,b[v%3],1,rate=.94 if a==1 else 1)
   mix_layer(out,t[k],.14,.11)
  elif hero=='Nyra':mix_layer(out,'cloth2',1);mix_layer(out,t[k],.25,.03)
  elif hero=='Rook':mix_layer(out,b[k],1);mix_layer(out,t[k],.25,.025,.9)
  elif hero=='Kaelon':mix_layer(out,t[k],.7);mix_layer(out,b[k],.8,.012,.78)
  elif hero=='Elyra':
   mix_layer(out,t[k],.5,rate=.55,reverse=True);mix_layer(out,b[k],.9,.04,.62);mix_layer(out,hit[k],.25,.06,.7)
  elif hero=='Traveler':
   mix_layer(out,b[k],.8,rate=.83,reverse=True);mix_layer(out,t[k],.55,.025);mix_layer(out,hit[k],.4,.065,.75)
  elif hero=='Mira':mix_layer(out,b[k],.7,rate=1.08);mix_layer(out,t[k],.18);mix_layer(out,hit[k],.25,.025,1.16)
  else:mix_layer(out,b[k],1);mix_layer(out,t[k],.22,.02)
  # Skills have authored rhythms; R has a stronger layered body rather than a longer basic.
  if a==1 and hero!='Nyra':mix_layer(out,b[(k+1)%3],.45,.055)
  if a==2:out*=np.linspace(.5,1,len(out));mix_layer(out,t[(k+1)%3],.4,.065,.85,hero=='Traveler')
  if a==3:mix_layer(out,t[(k+2)%3],.45,.032,.7)
  if a==4 and hero!='Nyra':
   mix_layer(out,hit[(k+1)%3],.85,.07,.64 if hero!='Mira' else 1)
   mix_layer(out,b[(k+2)%3],.4,.13,.75)
 if hero in ('Elyra','Mira','Traveler') and phase!='Windup':
  dry=out.copy();delay=int((.047 if hero=='Traveler' else .023)*SR);out[delay:]+=dry[:-delay]*(.23 if hero=='Traveler' else .12)
  if hero=='Traveler':out[delay*2:]+=dry[:-delay*2]*.1
 out=signal.sosfilt(signal.butter(2,80,fs=SR,btype='highpass',output='sos'),out)
 out=signal.sosfilt(signal.butter(2,pal['low'],fs=SR,output='sos'),out)
 # Broad, moderate presence attenuation retains a rifle's crack and blade edge.
 bq,aq=signal.iirpeak(3300,.65,fs=SR);out-=signal.lfilter(bq,aq,out)*.3
 out=np.tanh(out*1.3)
 fade=int(.002*SR);tail=min(int(.045*SR),len(out)//3);out[:fade]*=np.linspace(0,1,fade);out[-tail:]*=np.linspace(1,0,tail)
 target=-29 if phase=='Windup' else -24 if a==0 else -22 if a<4 else -21
 out*=min(10**(target/20)/max(np.sqrt(np.mean(out*out)),1e-8),10**(-22/20)/max(float(max(abs(out))),1e-8))
 return out.astype(np.float32)
def render_kill():
 # Original short kill-confirmation: dry body, sharp crack, tiny glass tail.
 # Uses the project's licensed recorded sources rather than a copied game sound.
 out=np.zeros(int(.24*SR),np.float64)
 mix_layer(out,'impactPunch_heavy_002',.9,rate=1.12)
 mix_layer(out,'metalClick',.55,.004,1.35)
 mix_layer(out,'impactGlass_heavy_002',.28,.018,1.4)
 out=signal.sosfilt(signal.butter(2,[180,7600],fs=SR,btype='bandpass',output='sos'),out)
 out*=np.exp(-np.arange(len(out))/SR*13)
 out[:32]*=np.linspace(0,1,32);out[-960:]*=np.linspace(1,0,960)
 out*=10**(-22/20)/max(abs(out))
 return out.astype(np.float32)

def meta(p):
 m=Path(str(p)+'.meta')
 if m.exists():return
 guid=uuid.uuid5(uuid.NAMESPACE_URL,'dimensions-front/'+str(p.relative_to(ROOT))).hex
 m.write_text(f'fileFormatVersion: 2\nguid: {guid}\nAudioImporter:\n  externalObjects: {{}}\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 2\n    sampleRateOverride: 32000\n    compressionFormat: 1\n    quality: 0.7\n    conversionMode: 0\n  platformSettingOverrides: {{}}\n  forceToMono: 1\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
def encode(x,p):
 p.parent.mkdir(parents=True,exist_ok=True)
 subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar',str(SR),'-ac','1','-i','-','-c:a','libvorbis','-q:a','4',str(p)],input=x.astype(np.float32).tobytes(),check=True);meta(p)
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--import-sources',type=Path);args=parser.parse_args()
 if args.import_sources:bootstrap(args.import_sources)
 manifest=[]
 for h,hero in enumerate(HEROES):
  for a,action in enumerate(ACTIONS):
   for phase in ['Cast']+(['Impact','Windup'] if HAS_HIT[h][a] else []):
    count=3 if phase!='Windup' and (a==0 or h==2 and a==4) else 1
    for v in range(count):
     used.clear();x=render(hero,a,phase,v);p=DEST/'Heroes'/hero/(action+'_'+phase+('' if v==0 else '_v'+str(v+1))+'.ogg');encode(x,p)
     manifest.append(dict(hero=hero,action=action,phase=phase,variant=v+1,path=str(p.relative_to(ROOT)),sources=list(used.values()),duration=len(x)/SR))
 # Support, reactions, deaths and explosions: recorded layers replace generic metallic placeholders.
 extras={'Shield':('Kaelon',2,'Cast'),'Heal':('Mira',2,'Cast'),'HitLight':('Rook',0,'Impact'),'HitHeavy':('Rook',4,'Impact'),'HeroDeath':('Traveler',4,'Impact'),'MinionDeath':('Rook',0,'Impact'),'TowerShot':('Kaelon',1,'Cast'),'TowerImpact':('Kaelon',1,'Impact'),'MeteorImpact':('Kaelon',4,'Impact'),'CoreHit':('Traveler',4,'Impact'),'AbilityCast':('Elyra',1,'Cast'),'AbilityImpact':('Elyra',1,'Impact'),'Attack':('Veyr',0,'Cast')}
 for cue,(hero,a,phase) in extras.items():
  used.clear();x=render(hero,a,phase,0);p=DEST/'SFX'/(cue+'.ogg');encode(x,p);manifest.append(dict(cue=cue,path=str(p.relative_to(ROOT)),sources=list(used.values()),duration=len(x)/SR))
 used.clear();x=render_kill();p=DEST/'SFX/Kill.ogg';encode(x,p)
 manifest.append(dict(cue='Kill',path=str(p.relative_to(ROOT)),sources=list(used.values()),duration=len(x)/SR))
 # Preserve warning identities; enforce a pool-safe peak ceiling, after decode, for every SFX.
 for p in (DEST/'SFX').glob('*.ogg'):
  if p.stem not in extras and p.stem!='Kill':
   source=SRC/'Warnings'/p.name
   x=decode(source);x*=min(1,10**(-22/20)/max(abs(x)));encode(x,p)
   manifest.append(dict(cue=p.stem,path=str(p.relative_to(ROOT)),sources=[dict(file=str(source.relative_to(ROOT)),sha256=sha(source),provenance='Docs/ThirdPartyAudioLicenses.md')],duration=len(x)/SR))
 (ROOT/'Docs/CombatAudioAssets.json').write_text(json.dumps(manifest,indent=2)+'\n')
 (ROOT/'Docs/HeroAudioAssets.json').write_text(json.dumps([r for r in manifest if 'hero' in r],indent=2)+'\n')
 # Audition uses decoded shipped files and existing preparation/release/contact timings.
 audition=[];chapters=[];cursor=0
 basic_windup=[.09,.16,.08,.14,.12,.13,.10]
 skill_windup=[[.13,.13,0,.32],[.20,0,.45,.55],[.12,0,.12,.22],[.38,.46,0,.68],[.16,0,0,.4],[.17,0,.30,.50],[.14,0,.38,.68]]
 areas={(1,3),(1,4),(3,1),(3,2),(3,4),(4,4),(5,3),(5,4),(6,3),(6,4)}
 for h,hero in enumerate(HEROES):
  chapters.append(dict(hero=hero,start_seconds=round(cursor,3)));chunks=[]
  for sequence,a in enumerate([0,0,0,1,2,3,4]):
   v=sequence if a==0 else 0;action=ACTIONS[a];folder=DEST/'Heroes'/hero
   def clip(phase,variant=0):return decode(folder/(action+'_'+phase+('' if variant==0 else '_v'+str(variant+1))+'.ogg'))
   windup=basic_windup[h] if a==0 else skill_windup[h][a-1]
   events=[]
   if HAS_HIT[h][a]:events.append((0,clip('Windup')*.32))
   if h==2 and a==4:
    for shot in range(4):
     events.append((windup+shot*.18,clip('Cast',shot%3)*.9));events.append((windup+shot*.18+.14,clip('Impact',shot%3)*.84))
   else:
    events.append((windup,clip('Cast',v)*(.58 if a==0 else .9 if a==4 else .7)))
    if HAS_HIT[h][a]:events.append((windup+(0 if (h,a) in areas or h in (0,1,5) else .14),clip('Impact',v)*(.48 if a==0 else .84 if a==4 else .62)))
   length=max(int(at*SR)+len(x) for at,x in events);event=np.zeros(length)
   for at,x in events:event[int(at*SR):int(at*SR)+len(x)]+=x
   chunks.extend([event,np.zeros(int(.16*SR))])
  block=np.concatenate(chunks+[np.zeros(int(.55*SR))]);audition.append(block);cursor+=len(block)/SR
 x=np.concatenate(audition).astype(np.float32)
 # Audition normalization followed by the same 30% reduction as runtime PlayHero.
 x*=.7*10**(-3/20)/max(abs(x))
 p=ROOT/'Docs/Combat-Audio-Audition.ogg';encode(x,p);Path(str(p)+'.meta').unlink()
 subprocess.run(['ffmpeg','-v','error','-y','-i',str(p),'-c:a','libmp3lame','-q:a','2',str(ROOT/'Docs/Combat-Audio-Audition.mp3')],check=True)
 (ROOT/'Docs/Combat-Audio-Audition.json').write_text(json.dumps(chapters,indent=2)+'\n')
 print('Rendered',len(manifest),'combat assets;',round(cursor,2),'second audition')
if __name__=='__main__':main()
