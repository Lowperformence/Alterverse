"""Convert user-selected ultimate recordings. Usage: python script.py INPUT_FOLDER.
Requires ffmpeg. Preserve full duration; stereo 44.1kHz Vorbis, peak -6 dBFS.
"""
from pathlib import Path
import array, hashlib, json, math, subprocess, sys, uuid
ROOT=Path(__file__).resolve().parents[2]
SOURCES={'Rook':'brakk ulti.wav','Mira':'heiler ulti.wav','Elyra':'ione ulti.wav','Nyra':'sivra ulti.wav','Kaelon':'tank ulti.wav','Traveler':'ulti traveler.wav','Veyr':'assasinen ulti.mp3'}
def decode(path):
 raw=subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-f','f32le','-ac','2','-ar','44100','-'])
 data=array.array('f');data.frombytes(raw)
 if not data or not all(math.isfinite(v) for v in data):raise ValueError('Invalid audio '+str(path))
 return data
report=[]
for hero,name in SOURCES.items():
 source=Path(sys.argv[1])/name;data=decode(source);peak=max(abs(v) for v in data)
 if peak<=0:raise ValueError('Silent source '+name)
 dest=ROOT/'Assets/Alterverse/Resources/Audio/Heroes'/hero/'Ultimate_Selected.ogg';dest.parent.mkdir(parents=True,exist_ok=True)
 gain=10**(-6/20)/peak
 subprocess.run(['ffmpeg','-v','error','-y','-i',str(source),'-af',f'volume={gain:.10f}','-ac','2','-ar','44100','-c:a','libvorbis','-q:a','5',str(dest)],check=True)
 result=decode(dest);outpeak=max(abs(v) for v in result)
 assert outpeak<1 and abs(len(result)-len(data))<2048
 meta=dest.with_suffix('.ogg.meta')
 if not meta.exists():
  guid=uuid.uuid5(uuid.NAMESPACE_URL,'Alterverse/'+str(dest.relative_to(ROOT))).hex
  meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''')
 report.append({'hero':hero,'source':name,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'duration':len(result)/88200,'peak_dbfs':20*math.log10(outpeak),'bytes':dest.stat().st_size})
print(json.dumps(report,indent=2))
(ROOT/'Tools/Audio/selected-ultimates-validation.json').write_text(json.dumps(report,indent=2)+'\n')
