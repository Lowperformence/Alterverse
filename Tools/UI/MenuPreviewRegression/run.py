"""Exercise production preview selection/cache and async backdrop with controlled engine objects.

Pixel fixture uses a smaller buffer and deterministic math substitutes. This does not
measure Unity/Android frame time, native Perlin noise or GPU upload latency.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

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
    assert 'private const int Width = 2048;' in backdrop and 'private const int Height = 1152;' in backdrop
    (folder/'Backdrop.cs').write_text(backdrop.replace('Width = 2048','Width = 64').replace('Height = 1152','Height = 36'))
    showcase=(source/'MenuHeroShowcase.cs').read_text()
    fields=showcase[showcase.index('        private const int PreviewLayer'):showcase.index('        // Older local skin scripts')]
    (folder/'Selection.cs').write_text('using UnityEngine;using Alterverse.Characters;using Alterverse.Visuals;namespace Alterverse.UI {public sealed class MenuHeroShowcase:MonoBehaviour {'+fields+
        method(showcase,'        private static int EquippedWeaponVariant(')+
        method(showcase,'        private static void SetPreviewWeaponVariant(')+
        'private Transform orbitLeft,orbitRight,wingLeft,wingRight;'+
        'private void CreateStage(){stage=new GameObject("stage");}private void UpdateOriginWorld(HeroId hero){}'+
        'public GameObject TestActor=>actor;public int TestCacheCount=>previews.Count;'+
        method(showcase,'        public void Present(')+'}}')
    output=folder/'PreviewRegression.dll';response=folder/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-nowarn:0649,0414','-langversion:9','-out:'+str(output),
        *['-r:'+str(p) for p in runtime.glob('*.dll')],str(here/'Regression.cs'),str(folder/'Backdrop.cs'),str(folder/'Selection.cs')]))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(output)],check=True)
