"""Bake the hero origin artwork once, outside the game, using the production palettes.

Requires .NET 8 SDK and Pillow. Imported 1024x576 PNGs keep hero selection free of
procedural pixel work. The offline gradient noise is deterministic, independent
of Unity's native Perlin implementation; rerun when changing world palettes.
"""
import argparse
from io import BytesIO
import json
from pathlib import Path
import re
import subprocess
import tempfile
import uuid
from PIL import Image

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
here=Path(__file__).resolve().parent
root=here.parents[2]
output=root/'Assets/Alterverse/Resources/UI/HeroOriginBackdrops'
output.mkdir(parents=True,exist_ok=True)
sdk=sorted((args.dotnet_root/'sdk').iterdir())[-1]
runtime=sorted((args.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
identity=(root/'Assets/Alterverse/Scripts/Characters/HeroIdentity.cs').read_text()
hero_enum=re.search(r'public enum HeroId\s*\{[^}]+\}',identity).group()
with tempfile.TemporaryDirectory(prefix='df-origin-bake-') as scratch:
    scratch=Path(scratch)
    shim=scratch/'EngineShim.cs'
    shim.write_text((here/'EngineShim.cs').read_text().replace('/* HERO_ENUM */',hero_enum))
    binary=scratch/'Bake.dll'
    response=scratch/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-optimize+','-langversion:9','-out:'+str(binary),
        *['-r:'+str(p) for p in runtime.glob('*.dll')],str(shim),str(here/'Bake.cs'),
        str(root/'Assets/Alterverse/Scripts/UI/HeroOriginBackdrop.cs')]))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    binary.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(binary),str(scratch)],check=True)
    for raw in scratch.glob('*.rgba'):
        image=Image.frombytes('RGBA',(1024,576),raw.read_bytes()).transpose(Image.Transpose.FLIP_TOP_BOTTOM).convert('RGB')
        png=output/(raw.stem+'.png')
        encoded=BytesIO()
        image.save(encoded,format='PNG',optimize=True)
        data=encoded.getvalue()
        with Image.open(BytesIO(data)) as check:
            check.load()
        png.write_bytes(data)
        assert png.read_bytes()==data
        meta=png.with_suffix('.png.meta')
        if not meta.exists():
            guid=uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/hero-origin/'+raw.stem).hex
            meta.write_text('''fileFormatVersion: 2
guid: '''+guid+'''
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  npotScale: 0
  textureType: 0
  sRGBTexture: 1
  alphaSource: 0
  alphaIsTransparency: 0
  textureCompression: 1
  compressionQuality: 50
  userData:
  assetBundleName:
  assetBundleVariant:
''')
for folder in [output.parent,output]:
    meta=folder.with_suffix('.meta')
    if not meta.exists():
        guid=uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/'+str(folder.relative_to(root))).hex
        meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
print('Wrote',len(list(output.glob('*.png'))),'hero backgrounds,',sum(p.stat().st_size for p in output.glob('*.png')),'PNG bytes.')
