#!/usr/bin/env python3
"""Focused Traveler complete lightning chain contracts; controlled fixtures, not Unity rendering."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse';dotnet=a.dotnet_root.resolve()
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
arena=(src/'Scripts/Match/ArenaVariantRuntime.cs').read_text();enum=re.search(r'public enum ArenaVariant\s*\{[^}]+\}',arena)[0]
execution=(src/'Scripts/Characters/HeroAbilityExecution.cs').read_text()
assert 'private const int SightlineBreakShots=3;' in execution
assert 'HeroAttackDelivery.Windup(identity.Hero,3)+i*.18f' in execution
with tempfile.TemporaryDirectory(prefix='df-traveler-lightning-') as directory:
    d=Path(directory);bridge=d/'Arena.cs';bridge.write_text('namespace Alterverse.Match {'+enum+'}')
    out=d/'Regression.dll'
    subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-define:UNITY_EDITOR','-langversion:latest','-out:'+str(out),
        *['-r:'+str(x) for x in refs.glob('*.dll')],str(root/'Tools/SpaceBomber/NukeRegression/Fixtures.cs'),str(here/'Regression.cs'),str(bridge),
        str(src/'Scripts/Visuals/BomberNukeVfx.cs'),str(src/'Editor/BomberNukeBuilder.cs'),
        str(src/'Scripts/Visuals/TravelerLightningVfx.cs'),str(src/'Editor/TravelerLightningBuilder.cs')],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(dotnet/'dotnet'),str(out)],cwd=d,check=True)
