"""Exercise production preview selection/cache and immediate imported hero artwork.

Tests read the actual committed PNGs and use controlled engine objects. This does
not measure Unity/Android disk, texture decompression or GPU latency.
"""
import argparse
import json
import re
from pathlib import Path
import subprocess
import tempfile
import hashlib
from PIL import Image

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2]
source=root/'Assets/Alterverse/Scripts/UI'

def method(text,declaration):
    start=text.index(declaration);end=text.index('{',start)+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]

sdk=sorted((args.dotnet_root/'sdk').iterdir())[-1]
runtime=sorted((args.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-preview-') as folder:
    folder=Path(folder)
    backdrop=(source/'HeroOriginBackdrop.cs').read_text()
    assert not any(code in backdrop for code in ['Task.Run','BuildPixels(','SetPixels32(','PerlinNoise('])
    (folder/'Backdrop.cs').write_text(backdrop)
    showcase=(source/'MenuHeroShowcase.cs').read_text()
    fields=showcase[showcase.index('        private const int PreviewLayer'):showcase.index('        // Older local skin scripts')]
    (folder/'Selection.cs').write_text('using UnityEngine;using Alterverse.Characters;using Alterverse.Visuals;namespace Alterverse.UI {public sealed class MenuHeroShowcase:MonoBehaviour {'+fields+
        method(showcase,'        private static int EquippedWeaponVariant(')+
        method(showcase,'        private static void SetPreviewWeaponVariant(')+
        'private Transform orbitLeft,orbitRight,wingLeft,wingRight;'+
        'private void CreateStage(){stage=new GameObject("stage");originWorld=new SpriteRenderer();}'+
        'public GameObject TestActor=>actor;public int TestCacheCount=>previews.Count;public bool TestNeedsFit=>needsFit;'+
        'public Texture Image=>texture;public Texture BackgroundImage=>originWorld?.sprite?.texture;'+
        'public void TestPoll()=>UpdateOriginWorld(selected);public void TestDispose()=>originBackdrop?.Dispose();'+
        method(showcase,'        private void UpdateOriginWorld(')+
        method(showcase,'        public void DrawPreview(')+
        method(showcase,'        public void Present(')+'}}')
    # Use the production roster so a newly added hero cannot silently reuse another world's style.
    identity=(root/'Assets/Alterverse/Scripts/Characters/HeroIdentity.cs').read_text()
    hero_enum=re.search(r'public enum HeroId\s*\{[^}]+\}',identity).group()
    fixture=(here/'Regression.cs').read_text().replace('/* HERO_ENUM */',hero_enum)
    (folder/'Regression.cs').write_text(fixture)
    output=folder/'PreviewRegression.dll';response=folder/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-nowarn:0649,0414','-langversion:9','-out:'+str(output),
        *['-r:'+str(p) for p in runtime.glob('*.dll')],str(folder/'Regression.cs'),str(folder/'Backdrop.cs'),str(folder/'Selection.cs')]))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    assets=root/'Assets/Alterverse/Resources/UI/HeroOriginBackdrops'
    heroes=[h.strip().split('=')[0].strip() for h in hero_enum.split('{')[1].split('}')[0].split(',')]
    hashes=set();guids=set()
    for hero in heroes:
        png=assets/(hero+'.png')
        with Image.open(png) as image:
            image.load()
            assert image.size==(1024,576) and image.mode=='RGB'
            rgb=image.tobytes()
        digest=hashlib.sha256(rgb).hexdigest()
        assert digest not in hashes,hero+' duplicates another hero backdrop'
        hashes.add(digest)
        (folder/(hero+'.rgb')).write_bytes(rgb)
        meta=png.with_suffix('.png.meta').read_text()
        assert all(setting in meta for setting in ['enableMipMap: 0','isReadable: 0','npotScale: 0','maxTextureSize: 1024','alphaSource: 0'])
        guid=re.search(r'^guid: (\w+)',meta,re.M).group(1)
        assert guid not in guids
        guids.add(guid)
    assert len(list(assets.glob('*.png')))==len(heroes)
    print(f'{len(heroes)} imported PNGs verified: complete, distinct, correct resolution and Unity import settings.',flush=True)
    subprocess.run([str(args.dotnet_root/'dotnet'),str(output),str(folder)],check=True)


