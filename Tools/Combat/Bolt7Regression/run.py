"""Compile BOLT-7 production combat with the shared deterministic shield fixtures.
Run with --dotnet-root pointing to an installed .NET 8 SDK. Not a Unity play test.
"""
import argparse,ast,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);args=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts';sdk=args.dotnet_root.resolve()
shared=ast.parse((here.parent/'ShieldRegression/run.py').read_text())
fixture=next(ast.literal_eval(n.value) for n in shared.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='fixture' for t in n.targets))
fixture=fixture.split('class ShieldRegression')[0]
enum=re.search(r'public enum HeroId\s*\{[^}]+\}',(src/'Characters/HeroIdentity.cs').read_text()).group()
fixture=fixture.replace('HERO_ENUM',enum).replace('public class HeroCombatStatus:UnityEngine.MonoBehaviour{public void Clear(){}}','public class HeroCombatStatus:UnityEngine.MonoBehaviour{public void Clear(){} public static bool CanAct(UnityEngine.GameObject g)=>true;}')
fixture=fixture.replace('public class RealityCore:UnityEngine.MonoBehaviour{}','public class RealityCore:UnityEngine.MonoBehaviour{public int Team;public bool IsVulnerable=true;}').replace('public class LaneMinion:UnityEngine.MonoBehaviour{}','public class LaneMinion:UnityEngine.MonoBehaviour{public int Team;} public class LaneTower:UnityEngine.MonoBehaviour{public int Team;}')
fixture=fixture.replace('DamageTakenMultiplier=1;}','DamageTakenMultiplier=1;public float CooldownMultiplier=1;}')
fixture=fixture.replace('public static bool CanMutateAuthoritativeState=true;','public static bool CanMutateAuthoritativeState=true,CanSimulateHostGameplay=true;} public static class CoopGameplayCommands{public static void RecordAuthoritativeDamage(UnityEngine.GameObject g,float n){}')
fixture=fixture.replace('public float ReduceFrontalDamage','public void RecordBasicHit(){} public float ReduceFrontalDamage').replace('public static void ConfirmedKill','public static void Play(UnityEngine.GameObject g,int s,bool i,UnityEngine.Vector3 p){} public static void ConfirmedKill')
fixture+='''
namespace Alterverse.Characters {public class HeroLocomotion:UnityEngine.MonoBehaviour {public UnityEngine.Vector3 LastMove;public void BeginForcedMove(UnityEngine.Vector3 p,float s){LastMove=p;}}}
namespace Alterverse.Match {public class DimensionPlanet:UnityEngine.MonoBehaviour{public bool Available=true;}}
namespace Alterverse.Visuals {public static class HeroSkillVfx {public static void Impact(UnityEngine.GameObject a,Alterverse.Combat.Health h,int s){}}}
namespace Alterverse.Combat {public static class GameplayCollision{public const int SightMask=-1;}public class HeroAttackDelivery{public static HeroAttackDelivery For(UnityEngine.GameObject g)=>new();public void Basic(Health t,float a,float r){}}}
'''
# Compile unchanged pure geometry helpers used by Bolt7Combat, without unrelated aiming dependencies.
geometry=(src/'Combat/AimGeometry.cs').read_text().split('// The visible ground cone')[0]+'}}'
engine=(root/'Tools/Aiming/Regression/EngineStubs.cs').read_text().split('namespace UnityEngine.InputSystem')[0]
engine=engine.replace('SubsystemRegistration,BeforeSceneLoad','SubsystemRegistration,BeforeSceneLoad,AfterSceneLoad').replace('FindObjectsInactive i) where','FindObjectsInactive i=FindObjectsInactive.Exclude) where')
names=['Combat/Health.cs','Combat/Bolt7Combat.cs','Combat/HeroCombatRules.cs','Characters/Bolt7Balance.cs','Characters/HeroCombatProfile.cs','Characters/HeroAbilityKit.cs','Characters/TimmyBalance.cs','Characters/VoidKnightBalance.cs']
refs=next((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='bolt7-regression-') as tmp:
 tmp=Path(tmp);(tmp/'Fixture.cs').write_text(engine+fixture);(tmp/'Geometry.cs').write_text(geometry);out=tmp/'Regression.dll'
 subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],str(tmp/'Fixture.cs'),str(tmp/'Geometry.cs'),str(here/'Regression.cs'),*[str(src/n) for n in names]],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
 subprocess.run([str(sdk/'dotnet'),str(out)],check=True)
