#!/usr/bin/env python3
"""Exercise native Flux Nuke and meteor Fireball imports, playback and impact hooks."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse';dotnet=a.dotnet_root.resolve()
pulse=(src/'Scripts/Match/PlanetPulseAttack.cs').read_text()
meteor=(src/'Scripts/Combat/PlanetMeteorShower.cs').read_text()
spider=(src/'Scripts/Combat/NoxaraSpiderMeteorShower.cs').read_text()
hero=(src/'Scripts/Visuals/HeroSkillVfx.cs').read_text()
assert 'AttackExplosionVfx.TryPlay(AttackExplosionVfx.Kind.FluxNuke,transform.position)' in pulse
assert 'PrototypeVfx.Burst(transform.position, warning, radius * .55f, 10);' in pulse
assert 'if(pulse!=null&&pulse.gameObject.name==ownerName){pulse.Explode(false);return;}' in pulse
assert 'AttackExplosionVfx.TryMeteor(impact,new Color(1f,.63f,.23f))' in meteor
assert 'AttackExplosionVfx.TryMeteor(impact,PoisonGreenHot)' in spider
assert 'AttackExplosionVfx.TryMeteor(Ground(center),Accent(actor),HeroVfxEvents.ReplayElapsed,radius)' in hero
assert 'UltimateLightningVfx.TryPlay(hero==HeroId.Elyra?UltimateLightningVfx.Ione:UltimateLightningVfx.Traveler,' in hero
assert 'AttackExplosionVfx.Prepare(world.transform);' in (src/'Scripts/Prototype/PrototypeBootstrap.cs').read_text()
assert not any('PlanetAttackSparksVfx' in x for x in (pulse,meteor,spider,hero))
assert not (src/'Editor/PlanetAttackSparksBuilder.cs').exists()
assert not (src/'Scripts/Visuals/PlanetAttackSparksVfx.cs').exists()
assert 'AttackExplosionVfx' not in (src/'Scripts/Combat/SpaceBomberEvent.cs').read_text()
assert 'AttackExplosionVfx' not in (src/'Scripts/Visuals/BomberNukeVfx.cs').read_text()
# Numeric equivalent of the new shader: native smoke/highlights stay neutral;
# saturated fire retains brightness while receiving the attacking meteor's hue.
def recolour(colour,hue):
    peak=max(colour);saturation=(peak-min(colour))/max(peak,.0001);high=max(max(hue),.0001)
    return tuple(peak*(1-saturation)+x/high*peak*saturation for x in hue)
for colour in ((.3,.3,.3),(1,1,1),(0,0,0)):
    assert recolour(colour,(.08,1,.22))==colour
orange=(1,.34,.1);green=recolour(orange,(.08,1,.22));blue=recolour(orange,(.2,.6,1))
assert green[1]>green[0] and green[1]>green[2] and max(green)==max(orange)
assert blue[2]>blue[1]>blue[0] and max(blue)==max(orange)
shader=(src/'Resources/CombatVfx/AttackExplosionParticles.shader').read_text()
assert '_EffectTint.w > .5' in shader and 'colour.rgb = lerp' in shader
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
enum=re.search(r'public enum ArenaVariant\s*\{[^}]+\}',(src/'Scripts/Match/ArenaVariantRuntime.cs').read_text())[0]
with tempfile.TemporaryDirectory(prefix='df-attack-explosions-') as directory:
    d=Path(directory);bridge=d/'Arena.cs';bridge.write_text('namespace Alterverse.Match {'+enum+'}')
    out=d/'Regression.dll'
    command=[str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-langversion:latest',*['-r:'+str(x) for x in refs.glob('*.dll')],str(root/'Tools/SpaceBomber/NukeRegression/Fixtures.cs'),str(here/'Regression.cs'),str(bridge),str(src/'Scripts/Visuals/BomberNukeVfx.cs'),str(src/'Editor/BomberNukeBuilder.cs'),str(src/'Scripts/Visuals/AttackExplosionVfx.cs'),str(src/'Editor/AttackExplosionVfxBuilder.cs')]
    subprocess.run([*command,'-define:UNITY_EDITOR','-out:'+str(out)],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(dotnet/'dotnet'),str(out)],cwd=d,check=True)
    subprocess.run([*command,'-define:UNITY_EDITOR,UNITY_SERVER','-target:library','-out:'+str(d/'Server.dll')],check=True)
