"""Install pinned official TexTools/Rokoko archives into an explicitly chosen scripts folder.
Rigify and Node Wrangler ship with the tested Blender 4.5.3 distribution.
Then enable node_wrangler, textools, rigify and rokoko in Blender preferences.
"""
import argparse,hashlib,io,json,urllib.request,zipfile
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--scripts-dir',type=Path,required=True)
args=p.parse_args()
manifest=json.loads(Path(__file__).with_name('blender-tools.json').read_text())
for name,entry in manifest['addons'].items():
    data=urllib.request.urlopen(entry['url'],timeout=60).read()
    assert hashlib.sha256(data).hexdigest()==entry['sha256'],'Archive checksum mismatch: '+name
    target=(args.scripts_dir/'addons'/name).resolve();target.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for item in archive.infolist():
            parts=Path(item.filename).parts
            if len(parts)<2:continue
            path=(target/Path(*parts[1:])).resolve()
            assert path.is_relative_to(target),'Invalid archive path'
            if item.is_dir():path.mkdir(parents=True,exist_ok=True)
            else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(archive.read(item))
    print('Installed official '+name+' '+entry['commit'])
