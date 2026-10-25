#!/usr/bin/env python3
"""Exercise native Sparks import, additive impact hooks and pooled eight-times playback."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse';dotnet=a.dotnet_root.resolve()
pulse=(src/'Scripts/Match/PlanetPulseAttack.cs').read_text()
meteor=(src/'Scripts/Combat/PlanetMeteorShower.cs').read_text()
assert pulse.count('PlanetAttackSparksVfx.TryPlay(transform.position);')==1
assert 'PrototypeVfx.Burst(transform.position, warning, radius * .55f, 10);' in pulse
assert 'if(pulse!=null&&pulse.gameObject.name==ownerName){pulse.Explode(false);return;}' in pulse
assert meteor.count('PlanetAttackSparksVfx.TryPlay(impact);')==1
assert 'BomberExplosionVfx.Play(impact,new Color(1f,.63f,.23f),ImpactRadius);' in meteor
assert 'PlanetAttackSparksVfx.Prepare(world.transform);' in (src/'Scripts/Prototype/PrototypeBootstrap.cs').read_text()
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
enum=re.search(r'public enum ArenaVariant\s*\{[^}]+\}',(src/'Scripts/Match/ArenaVariantRuntime.cs').read_text())[0]
with tempfile.TemporaryDirectory(prefix='df-planet-sparks-') as directory:
    d=Path(directory);bridge=d/'Arena.cs';bridge.write_text('namespace Alterverse.Match {'+enum+'}')
    out=d/'Regression.dll'
    command=[str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-langversion:latest',*['-r:'+str(x) for x in refs.glob('*.dll')],str(root/'Tools/SpaceBomber/NukeRegression/Fixtures.cs'),str(here/'Regression.cs'),str(bridge),str(src/'Scripts/Visuals/BomberNukeVfx.cs'),str(src/'Editor/BomberNukeBuilder.cs'),str(src/'Scripts/Visuals/PlanetAttackSparksVfx.cs'),str(src/'Editor/PlanetAttackSparksBuilder.cs')]
    subprocess.run([*command,'-define:UNITY_EDITOR','-out:'+str(out)],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(dotnet/'dotnet'),str(out)],cwd=d,check=True)
    subprocess.run([*command,'-define:UNITY_EDITOR,UNITY_SERVER','-target:library','-out:'+str(d/'Server.dll')],check=True)
