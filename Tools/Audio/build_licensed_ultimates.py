"""Build seven licensed complete-cast cues from archived CC0 original downloads.
Requires numpy and ffmpeg. Old unverified recordings are never read by this build.
Run from any directory. Existing Unity AudioImporter GUIDs are preserved.
"""
from pathlib import Path
import hashlib,json,math,subprocess,tempfile,wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Audio/UltimateReplacements/2026-10-12'
SR=44100
# filename, cue start, level, source trim start, source trim length, playback speed.
PLANS={
 'Rook':('Brakk-Orun',2.0,[('laserLarge_001.ogg',0,.32,0,.6,1),('explosionCrunch_004.ogg',.55,1,0,1.45,1)]),
 'Mira':('Tahla-Venn',3.75399,[('magical_1.ogg',0,.22,0,2.8,1),('healing.ogg',.48,1,19.85,1.85,1)]),
 'Elyra':('Ione-Vaska',3.46943,[('spaceEngineLow_000.ogg',0,1,0,3.46943,1)]),
 'Nyra':('Sivra-Quell',6.03648,[('spaceEngineLarge_000.ogg',0,.38,0,5,1),('laserLarge_001.ogg',.60,1,0,.72,1),('laserLarge_001.ogg',.78,.68,0,.72,1),('laserLarge_001.ogg',.96,.48,0,.72,1),('laserLarge_001.ogg',1.14,.32,0,.72,1)]),
 'Kaelon':('Auren-Kest',2.712585,[('explosionCrunch_001.ogg',0,1,0,1.36,1),('lowFrequency_explosion_000.ogg',0,.7,0,2,1)]),
 'Traveler':('The-Traveler',7.24802,[('teleport.wav',0,.6,0,1.62,1),('spaceEngineLow_000.ogg',.1,.18,0,5,1),('explosionCrunch_001.ogg',1.05,1,0,1.36,1),('lowFrequency_explosion_000.ogg',1.05,.7,0,2,1)]),
 'Veyr':('Zhaev-Null',1.56735,[('sfx_06.ogg',0,1,0,2.24,1.429)])
}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def decode(p,start=0,length=None,speed=1):
 cmd=['ffmpeg','-v','error','-ss',str(start),'-i',str(p)]
 if length is not None:cmd+=['-t',str(length)]
 if speed!=1:cmd+=['-af','atempo='+str(speed)]
 b=subprocess.check_output(cmd+['-ar',str(SR),'-ac','2','-f','f32le','-'])
 return np.frombuffer(b,dtype='<f4').reshape(-1,2).copy()
def measure(p):
 raw=decode(p);assert np.isfinite(raw).all() and 0<np.max(abs(raw))<1
 r=subprocess.run(['ffmpeg','-v','info','-i',str(p),'-af','loudnorm=I=-18:TP=-1.7:LRA=11:print_format=json','-f','null','-'],capture_output=True,text=True,check=True)
 j=json.loads(r.stderr[r.stderr.rfind('{'):])
 return dict(duration=len(raw)/SR,channels=2,sampleRate=SR,peakDbfs=20*math.log10(float(np.max(abs(raw)))),truePeakDbtp=float(j['input_tp']),integratedLufs=float(j['input_i']),sha256=sha(p))
def main():
 sources=json.loads((ART/'sources.json').read_text());known={r['file']:r for r in sources}
 for r in sources:assert sha(ART/'Sources'/r['file'])==r['sha256'],r['file']
 (ART/'Previews').mkdir(exist_ok=True);report=[];playlist=[]
 with tempfile.TemporaryDirectory() as temp:
  for hero,(name,duration,events) in PLANS.items():
   n=round(duration*SR);mix=np.zeros((n,2));steps=[]
   for file,start,level,trim,length,speed in events:
    x=decode(ART/'Sources'/file,trim,length,speed);x/=max(float(abs(x).max()),1e-8)
    fade=min(round(.008*SR),len(x)//2);x[:fade]*=np.linspace(0,1,fade)[:,None];x[-fade:]*=np.linspace(1,0,fade)[:,None]
    off=round(start*SR);count=min(len(x),n-off);mix[off:off+count]+=level*x[:count]
    steps.append(dict(source=file,start=start,level=level,trimStart=trim,trimLength=length,atempo=speed))
   dry=mix.copy()
   for delay,gain in [(.12,.16),(.28,.11),(.52,.06),(.83,.025)]:
    shift=round(delay*SR);mix[shift:]+=dry[:-shift]*gain
   # Smooth release and entry avoid sample discontinuities.
   attack=round(.004*SR);release=min(round(.22*SR),n//3)
   mix[:attack]*=np.linspace(0,1,attack)[:,None];mix[-release:]*=np.linspace(1,0,release)[:,None]
   mix*=.6/max(float(abs(mix).max()),1e-8)
   wav=Path(temp)/'mix.wav'
   with wave.open(str(wav),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(np.round(mix*32767).astype('<i2').tobytes())
   gain=.756 if hero=='Traveler' else .63
   target=-22.87956408997587-20*math.log10(gain)
   ogg=ROOT/'Assets/Alterverse/Resources/Audio/Heroes'/hero/'Ultimate_Selected.ogg';ogg.parent.mkdir(parents=True,exist_ok=True)
   normalized=Path(temp)/'normalized.wav'
   subprocess.run(['ffmpeg','-v','error','-y','-i',str(wav),'-af',f'loudnorm=I={target}:TP=-2:LRA=11','-ar',str(SR),'-c:a','pcm_f32le',str(normalized)],check=True)
   adjustment=0.;ceiling=.68
   for attempt in range(8):
    filters=f'volume={adjustment}dB,aresample=176400,alimiter=limit={ceiling}:level=false:latency=true,aresample=44100'
    subprocess.run(['ffmpeg','-v','error','-y','-i',str(normalized),'-af',filters,'-c:a','libvorbis','-q:a','5',str(ogg)],check=True)
    m=measure(ogg);error=target-m['integratedLufs']
    if abs(error)<.35 and m['truePeakDbtp']<=-1.4:break
    if m['truePeakDbtp']>-1.4:ceiling*=10**((-1.7-m['truePeakDbtp'])/20)
    else:adjustment+=error
   # Vorbis overshoot is checked after encoding; no clipping and existing mix ceiling.
   assert m['truePeakDbtp']<=-1.4,m
   assert abs(m['duration']-duration)<.025,m
   assert abs(m['integratedLufs']+20*math.log10(gain)+22.87956408997587)<.65,m
   preview=ART/'Previews'/f'{name}.mp3'
   subprocess.run(['ffmpeg','-v','error','-y','-i',str(ogg),'-c:a','libmp3lame','-q:a','3','-metadata',f'title={name} - Ultimate CC0 replacement','-metadata','artist=DIMENSIONS FRONT; original creators in sources.json',str(preview)],check=True)
   audio=decode(ogg);playlist+=[audio,np.zeros((SR,2))]
   report.append(dict(hero=hero,name=name,path=str(ogg.relative_to(ROOT)),preview=str(preview.relative_to(ROOT)),sources=sorted(set(e[0] for e in events)),processing=steps,runtimeGain=gain,outputLufs=m['integratedLufs']+20*math.log10(gain),**m))
  allwav=Path(temp)/'all.wav';audio=np.concatenate(playlist)
  with wave.open(str(allwav),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(np.round(audio*32767).astype('<i2').tobytes())
  subprocess.run(['ffmpeg','-v','error','-y','-i',str(allwav),'-c:a','libmp3lame','-q:a','3',str(ART/'Ultimate-Hoerproben.mp3')],check=True)
 out=dict(date='2026-10-12',status='offline-validated replacement revision; Unity/Android and subjective listening not run',oldAudioUsedAsSample=False,sourceManifest='ArtSource/Audio/UltimateReplacements/2026-10-12/sources.json',generatorSha256=sha(Path(__file__)),ffmpegVersion=subprocess.check_output(['ffmpeg','-version'],text=True).splitlines()[0],clips=report)
 (ART/'validation.json').write_text(json.dumps(out,indent=2)+'\n')
 (ROOT/'Tools/Audio/selected-ultimates-validation.json').write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps([dict(hero=r['hero'],duration=r['duration'],outputLufs=r['outputLufs'],truePeak=r['truePeakDbtp']) for r in report],indent=2))
if __name__=='__main__':main()
