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
fixture=fixture.replace('public static float BaseHealth', 'public const float HeroCollisionRadius=.4f;public static float BaseHealth')
enum=re.search(r'public enum HeroId\s*\{[^}]+\}',(src/'Characters/HeroIdentity.cs').read_text()).group()
fixture=fixture.replace('HERO_ENUM',enum).replace('public class HeroCombatStatus:UnityEngine.MonoBehaviour{public void Clear(){}}','public class HeroCombatStatus:UnityEngine.MonoBehaviour{public void Clear(){} public static bool CanAct(UnityEngine.GameObject g)=>true;}')
fixture=fixture.replace('public class RealityCore:UnityEngine.MonoBehaviour{}','public class RealityCore:UnityEngine.MonoBehaviour{public int Team;public bool IsVulnerable=true;}').replace('public class LaneMinion:UnityEngine.MonoBehaviour{}','public class LaneMinion:UnityEngine.MonoBehaviour{public int Team;} public class LaneTower:UnityEngine.MonoBehaviour{public int Team;}')
fixture=fixture.replace('DamageTakenMultiplier=1;}','DamageTakenMultiplier=1;public float CooldownMultiplier=1;}')
fixture=fixture.replace('public static bool CanMutateAuthoritativeState=true;','public static bool CanMutateAuthoritativeState=true,CanSimulateHostGameplay=true;} public static class CoopGameplayCommands{public static void RecordAuthoritativeDamage(UnityEngine.GameObject g,float n){}')
fixture=fixture.replace('public float ReduceFrontalDamage','public void RecordBasicHit(){} public float ReduceFrontalDamage').replace('public static void ConfirmedKill','public static void Play(UnityEngine.GameObject g,int s,bool i,UnityEngine.Vector3 p){} public static void ConfirmedKill')
fixture=fixture.replace('public static void ConfirmedKill', 'public static void Windup(UnityEngine.GameObject g,int s){}public static void Cast(UnityEngine.GameObject g,int s){}public static void Impact(UnityEngine.GameObject g,int s,UnityEngine.Vector3 p){}public static void ConfirmedKill')
fixture+='''
namespace Alterverse.Characters {public class HeroLocomotion:UnityEngine.MonoBehaviour {public UnityEngine.Vector3 LastMove;public UnityEngine.Vector3 FrameStartPosition=>transform.position;public UnityEngine.Vector3 DashDestination;public void FaceAction(UnityEngine.Vector3 p,float s){transform.forward=p;}public bool BeginDash(UnityEngine.Vector3 p,float s){DashDestination=p;transform.position=p;return true;}public void BeginForcedMove(UnityEngine.Vector3 p,float s){LastMove=p;}}}
namespace Alterverse.Match {public class DimensionPlanet:UnityEngine.MonoBehaviour{public bool Available=true;}}
namespace Alterverse.Visuals {public static class HeroSkillVfx {public static UnityEngine.Vector3 Warning,Flight,Detonation;public static int Detonations;
 public static void Area(UnityEngine.GameObject a,UnityEngine.Vector3 p,float r,float d,bool warning,bool heal=false,int slot=-1){if(warning)Warning=p;else{Detonation=p;Detonations++;}}
 public static void Spell(UnityEngine.GameObject a,UnityEngine.Vector3 p,int s,float d){Flight=p;}
 public static void Dash(UnityEngine.GameObject a,UnityEngine.Vector3 p,UnityEngine.Vector3 q,int s,float d){}
 public static void Impact(UnityEngine.GameObject a,Alterverse.Combat.Health h,int s){}}}
namespace Alterverse.Combat {public static class GameplayCollision{public const int SightMask=-1;}public class HeroAttackDelivery{public static bool IsRanged(Alterverse.Characters.HeroId h)=>h==Alterverse.Characters.HeroId.Bolt7||h==Alterverse.Characters.HeroId.Nyra;public static HeroAttackDelivery For(UnityEngine.GameObject g)=>new();public void Basic(Health t,float a,float r){}}}
'''
fixture+='''
namespace Alterverse.Characters {
 public class HeroMotor:UnityEngine.MonoBehaviour {public UnityEngine.Vector3 CurrentMoveDirection,LastMoveDirection;}
 public class PlayerHeroAbilities:UnityEngine.MonoBehaviour {
  public float RemainingFor(int slot)=>0;public Alterverse.Combat.AimPose LastPose;
  public Alterverse.Combat.AimSpec AimFor(int slot)=>Alterverse.Combat.AimingSettings.Current.For(GetComponent<HeroIdentity>().Hero,slot);
  public bool CastAimed(int slot,Alterverse.Combat.AimPose pose){LastPose=pose;return gameObject.GetComponent<HeroAbilityExecution>().CastForTest(slot,pose);}
 }
 public sealed partial class HeroAbilityExecution:UnityEngine.MonoBehaviour {
  private HeroAbilityKit kit;private Alterverse.Combat.Health self;private HeroLocomotion locomotion;private int life;
  private void Awake(){kit=GetComponent<HeroAbilityKit>();self=GetComponent<Alterverse.Combat.Health>();locomotion=GetComponent<HeroLocomotion>();}
  public bool CastForTest(int slot,Alterverse.Combat.AimPose pose)=>TryCastBolt7(slot,pose);
 }
}
namespace Alterverse.Combat {
 public class SoftTargeting:UnityEngine.MonoBehaviour {
  public Health AcquireAssisted(float r,UnityEngine.Vector3 d,float a)=>AimGeometry.Select(gameObject,d,r,a);
  public Health AcquireCone(float r,UnityEngine.Vector3 d,float a,bool heroes=false)=>AimGeometry.Select(gameObject,d,r,a*.5f,heroesOnly:heroes);
 }
}
'''
# Compile unchanged pure geometry helpers used by Bolt7Combat, without unrelated aiming dependencies.
geometry=(src/'Combat/AimGeometry.cs').read_text()
engine=(root/'Tools/Aiming/Regression/EngineStubs.cs').read_text().split('namespace UnityEngine.InputSystem')[0]
engine=engine.replace('SubsystemRegistration,BeforeSceneLoad','SubsystemRegistration,BeforeSceneLoad,AfterSceneLoad').replace('FindObjectsInactive i) where','FindObjectsInactive i=FindObjectsInactive.Exclude) where')
names=['Combat/Health.cs','Combat/Bolt7Combat.cs','Combat/HeroCombatRules.cs','Characters/Bolt7Balance.cs','Characters/HeroCombatProfile.cs','Characters/HeroAbilityKit.cs','Characters/TimmyBalance.cs','Characters/VoidKnightBalance.cs','Combat/AimingSettings.cs','Combat/ManualAiming.cs','Characters/HeroAbilityExecution.Bolt7.cs']
refs=next((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='bolt7-regression-') as tmp:
 tmp=Path(tmp);(tmp/'Fixture.cs').write_text(engine+fixture);(tmp/'Geometry.cs').write_text(geometry);out=tmp/'Regression.dll'
 subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],str(tmp/'Fixture.cs'),str(tmp/'Geometry.cs'),str(here/'Regression.cs'),str(here/'AreaAimRegression.cs'),*[str(src/n) for n in names]],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
 subprocess.run([str(sdk/'dotnet'),str(out)],check=True)
