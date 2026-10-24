#!/usr/bin/env python3
"""Selected ability native source/footprint/hold/follow contracts; substitutes do not render Unity assets."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse';dotnet=a.dotnet_root.resolve()
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
arena=(src/'Scripts/Match/ArenaVariantRuntime.cs').read_text();enum=re.search(r'public enum ArenaVariant\s*\{[^}]+\}',arena)[0]
shader=(src/'Resources/CombatVfx/SelectedAbilityParticles.shader').read_text()
assert 'input.positionWS.xz - _AttackClip.xy' in shader
assert 'clip(along); clip(1.0 - along)' in shader
assert '_AttackCone.x' in shader
with tempfile.TemporaryDirectory(prefix='df-selected-abilities-') as directory:
    d=Path(directory);bridge=d/'Arena.cs';bridge.write_text('namespace Alterverse.Match {'+enum+'}')
    fixture=d/'Fixtures.cs';engine=(root/'Tools/SpaceBomber/NukeRegression/Fixtures.cs').read_text().replace('public static Vector3 up=>','public static Vector3 forward=>new(0,0,1);public static Vector3 up=>').replace('public float MoveMultiplier=1f;','public bool Rooted;public float MoveMultiplier=1f;');fixture.write_text(engine)
    out=d/'Regression.dll'
    subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-define:UNITY_EDITOR','-langversion:latest','-out:'+str(out),
        *['-r:'+str(x) for x in refs.glob('*.dll')],str(fixture),str(here/'Regression.cs'),str(bridge),
        str(src/'Scripts/Visuals/BomberNukeVfx.cs'),str(src/'Editor/BomberNukeBuilder.cs'),
        str(src/'Scripts/Visuals/SelectedAbilityVfx.cs'),str(src/'Editor/SelectedAbilityVfxBuilder.cs')],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(dotnet/'dotnet'),str(out)],cwd=d,check=True)
