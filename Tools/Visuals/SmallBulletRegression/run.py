#!/usr/bin/env python3
"""Compile the focused native-part importer and presenter against controlled engine fixtures."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse';dotnet=a.dotnet_root.resolve()
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
arena=(src/'Scripts/Match/ArenaVariantRuntime.cs').read_text();enum=re.search(r'public enum ArenaVariant\s*\{[^}]+\}',arena)[0]
monster=(src/'Scripts/Visuals/MonsterAttackVfx.cs').read_text()
method=monster[monster.index('        private static bool SmallBulletGuard'):monster.index('        public static void PresentLaser')]
method=method.replace('private static bool SmallBulletGuard','public static bool SmallBulletGuard')
hero=(src/'Scripts/Visuals/HeroSkillVfx.cs').read_text()
assert 'if(Hero(actor)==HeroId.Nyra&&SmallBulletVfx.Launch' in hero
assert 'if(slot<0&&Hero(actor)==HeroId.Nyra&&SmallBulletVfx.ConfirmHit' in hero
assert 'if(guard&&SmallBulletGuard(actor,from,to,elapsed))return;' in monster
shader=(src/'Resources/CombatVfx/SmallBulletParticles.shader').read_text()
assert '_AttackTrail.zw * along' in shader and 'clip(_AttackClip.z * _AttackClip.z - dot(offset, offset))' in shader
with tempfile.TemporaryDirectory(prefix='df-small-bullet-') as directory:
    d=Path(directory);bridge=d/'Bridge.cs'
    bridge.write_text('using UnityEngine;using Alterverse.Audio;namespace Alterverse.Match {'+enum+'}'
        'namespace Alterverse.Visuals {public enum MonsterAttackStyle{Mech,Stone}'
        'public class MechMachineGunVisual:MonoBehaviour {public int Bursts;public void MarkBurstPresented(){Bursts++;}}'
        'public static class GuardBridge {public static MonsterAttackStyle StyleFor(GameObject actor)=>MonsterAttackStyle.Mech;'+method+'}}')
    out=d/'Regression.dll'
    subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-define:UNITY_EDITOR','-langversion:latest','-out:'+str(out),
        *['-r:'+str(x) for x in refs.glob('*.dll')],str(root/'Tools/SpaceBomber/NukeRegression/Fixtures.cs'),str(here/'Regression.cs'),str(bridge),
        str(src/'Scripts/Visuals/BomberNukeVfx.cs'),str(src/'Editor/BomberNukeBuilder.cs'),
        str(src/'Scripts/Visuals/SmallBulletVfx.cs'),str(src/'Editor/SmallBulletBuilder.cs')],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(dotnet/'dotnet'),str(out)],cwd=d,check=True)
