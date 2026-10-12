"""Archive actual raw-file hashes and the original CC0 anatomy package notices."""
from pathlib import Path
import datetime, hashlib, io, json, urllib.request, zipfile
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Documentation/Provenance'
url='https://mirror.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip'
directory=OUT/'Licenses/Blender-Human-Base-Meshes';directory.mkdir(parents=True,exist_ok=True)
record={'retrievedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'anatomyBundleUrl':url,'anatomyNoticeStatus':'not retrieved','mediaFiles':[]}
try:
    request=urllib.request.Request(url,headers={'User-Agent':'DimensionsFront-provenance-audit/1.0'})
    with urllib.request.urlopen(request,timeout=90) as response:raw=response.read()
    record['anatomyBundleSha256']=hashlib.sha256(raw).hexdigest();archive=zipfile.ZipFile(io.BytesIO(raw));notices=[]
    for info in archive.infolist():
        if info.file_size<200000 and not info.is_dir() and any(w in Path(info.filename).name.lower() for w in ['readme','license','licence','copyright']):
            data=archive.read(info);name=Path(info.filename).name
            if (directory/name).exists():name=str(len(notices))+'-'+name
            (directory/name).write_bytes(data);notices.append({'archivePath':info.filename,'savedFile':(directory/name).relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(data).hexdigest()})
    record['anatomyNotices']=notices
    record['anatomyNoticeStatus']='original archive notices saved' if notices else 'archive retrieved but no matching text notice'
except Exception as error:
    record['anatomyNoticeStatus']='retrieval failed; existing README claim retained without fabricated notice'
    record['anatomyRetrievalError']=str(error)
extensions={'.png','.jpg','.jpeg','.mp4','.wav','.mp3','.ogg','.ttf','.otf','.fbx','.obj','.bytes','.blend','.gz'}
for base in ['Assets','ArtSource']:
    for path in sorted((ROOT/base).rglob('*')):
        if path.is_file() and path.suffix.lower() in extensions:
            data=path.read_bytes();record['mediaFiles'].append({'path':path.relative_to(ROOT).as_posix(),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
record['modelValidation']=json.loads((ROOT/'ArtSource/Heroes/ConceptRebuild/validation-report.json').read_text())
(OUT/'BuildEvidence.json').write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps({'actualFilesHashed':len(record['mediaFiles']),'anatomyNoticeStatus':record['anatomyNoticeStatus'],'models':len(record['modelValidation']['heroes'])}))
