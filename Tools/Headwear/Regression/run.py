"""Compile the production hat/pass code with isolated engine substitutes; never uses a player save."""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path,required=True)
a=p.parse_args()
here=Path(__file__).resolve().parent
root=here.parents[2]
sdk=sorted((a.dotnet_root/'sdk').glob('*'))[-1]
refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
source=root/'Assets/Alterverse/Scripts'
files=['Shop/UniversalSkin.cs','Shop/CosmeticCatalog.cs','Shop/CosmeticProfile.cs','Shop/HeadwearSelection.cs',
       'Shop/WingPreviewIcons.cs','Match/WingMatchRewardBonus.cs','Profile/BattlePassProgress.cs',
       'Visuals/BetaCapMesh.cs','Visuals/UfoHatMesh.cs','Visuals/HeadwearPresentation.cs','UI/CosmeticCollectionChecks.cs','UI/CosmeticCollectionCategories.cs','UI/HeadwearIconRenderer.cs',
       'Multiplayer/CoopHeroStateSync.Headwear.cs','../Editor/HeadwearFitWindow.cs','../Editor/HeadwearRegression.cs']
with tempfile.TemporaryDirectory(prefix='df-headwear-') as folder:
    tmp=Path(folder)
    panel=(source/'UI/CosmeticCollectionPanel.cs').read_text()
    grid='internal static class CollectionGridLayout'+panel.split('internal static class CollectionGridLayout',1)[1]
    (tmp/'Grid.cs').write_text('using UnityEngine;namespace Alterverse.UI { '+grid)
    out=tmp/'Regression.dll'
    command=[str(a.dotnet_root/'dotnet'),'exec',str(sdk/'Roslyn/bincore/csc.dll'),'-nologo',
             '-langversion:9','-define:UNITY_EDITOR','-target:exe','-out:'+str(out)]
    command+=['-r:'+str(f) for f in refs.glob('*.dll')]
    command+=[str(here/'EngineStubs.cs'),str(here/'EditorCompileStubs.cs'),str(here/'NetworkFixtures.cs'),str(here/'Regression.cs'),str(tmp/'Grid.cs')]+[str(source/f) for f in files]
    response=tmp/'compile.rsp';response.write_text('\n'.join(command[3:]))
    subprocess.run(command[:3]+['@'+str(response)],check=True)
    (tmp/'Regression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0',
        'framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(out)],check=True)

