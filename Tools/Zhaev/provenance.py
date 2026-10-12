from pathlib import Path
import hashlib,json,datetime
R=Path(__file__).resolve().parents[2]
paths=[R/'Tools/Zhaev',R/'ArtSource/Heroes/ZhaevOriginal',R/'Documentation/ZhaevOriginal']
files=[]
for folder in paths:
 for p in sorted(folder.rglob('*')):
  if p.is_file() and p.name!='provenance.json' and '__pycache__' not in p.parts:files.append({'path':p.relative_to(R).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size})
report={'generatedAtUTC':datetime.datetime.now(datetime.timezone.utc).isoformat(),'method':'AI-assisted new mathematical geometry from recorded source','externalCharacterAssetInputs':[],'ownershipCertificate':False,'files':files,'unityAndroidVerified':False}
(R/'Documentation/ZhaevOriginal/provenance.json').write_text(json.dumps(report,indent=2)+'\n')
