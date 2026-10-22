"""Compile actual shop/selection/reward code with a small isolated engine boundary.
No Unity runtime, billing service or player save is used. Requires a .NET 8 SDK.
"""
import argparse,json,subprocess,tempfile
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path,required=True)
args=p.parse_args()
here=Path(__file__).resolve().parent
root=here.parents[2]
sdk=sorted((args.dotnet_root/'sdk').glob('*'))[-1]
compiler=sdk/'Roslyn/bincore/csc.dll'
refs=sorted((args.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
host=args.dotnet_root/'dotnet'
source=root/'Assets/Alterverse/Scripts'
files=[source/a for a in ['Shop/UniversalSkin.cs','Shop/CosmeticCatalog.cs','Shop/CosmeticProfile.cs','Shop/HeadwearSelection.cs','Shop/WingPreviewIcons.cs','Match/WingMatchRewardBonus.cs','UI/CosmeticCollectionChecks.cs']]
with tempfile.TemporaryDirectory(prefix='df-premium-wings-') as folder:
    tmp=Path(folder)
    # Compile the actual grid functions used by the existing editor collection suite.
    panel=(source/'UI/CosmeticCollectionPanel.cs').read_text()
    grid='internal static class CollectionGridLayout'+panel.split('internal static class CollectionGridLayout',1)[1]
    (tmp/'Grid.cs').write_text('using UnityEngine;namespace Alterverse.UI { '+grid)
    out=tmp/'Regression.dll'
    command=[str(host),'exec',str(compiler),'-nologo','-define:UNITY_EDITOR','-target:exe','-out:'+str(out)]
    command+=['-r:'+str(f) for f in refs.glob('*.dll')]
    command+=[str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(tmp/'Grid.cs')]+list(map(str,files))
    subprocess.run(command,check=True)
    (tmp/'Regression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
    subprocess.run([str(host),str(out)],check=True)
