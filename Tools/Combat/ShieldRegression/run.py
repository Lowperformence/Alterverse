#!/usr/bin/env python3
"""Compile and exercise production Health.cs with deterministic Unity substitutes.
This verifies shield/damage/recharge rules; it is not a Unity or multiplayer test.
"""
import argparse, os, subprocess, tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--mono-root',type=Path,required=True)
p.add_argument('--roslyn',type=Path,required=True)
a=p.parse_args();root=Path(__file__).resolve().parents[3]
engine=(root/'Tools/Aiming/Regression/EngineStubs.cs').read_text().split('namespace UnityEngine.InputSystem')[0]
engine=engine.replace('SubsystemRegistration,BeforeSceneLoad','SubsystemRegistration,BeforeSceneLoad,AfterSceneLoad')
engine=engine.replace('FindObjectsInactive i) where','FindObjectsInactive i=FindObjectsInactive.Exclude) where')
fixture=r'''
namespace UnityEngine {
 public static class ParentLookup {
  public static T GetComponentInParent<T>(this GameObject g) where T:class {
   for(var current=g;current!=null;current=current.transform.parent?.gameObject){var c=current.GetComponent<T>();if(c!=null)return c;}return null;
  }
 }
}
namespace Alterverse.Characters {
 public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy}
 public class HeroIdentity:UnityEngine.MonoBehaviour{public HeroId Hero;}
}
namespace Alterverse.Combat {
 public class HeroCombatStatus:UnityEngine.MonoBehaviour{public void Clear(){}}
 public class TeamMember:UnityEngine.MonoBehaviour{public int Team;}
 public class RealityCore:UnityEngine.MonoBehaviour{}
 public class LaneMinion:UnityEngine.MonoBehaviour{}
}
namespace Alterverse.Match {
 public class PlanetBuffReceiver:UnityEngine.MonoBehaviour{public float OutgoingDamageMultiplier=1,DamageTakenMultiplier=1;}
 public class MatchScoreboard{public static MatchScoreboard Instance;public void RecordDamage(Alterverse.Combat.Health h,UnityEngine.GameObject s){}}
 public class MatchTelemetry{public static MatchTelemetry Instance;public void RecordDamage(Alterverse.Combat.Health h,UnityEngine.GameObject s,float a){}public void RecordDeath(Alterverse.Combat.Health h,UnityEngine.GameObject s){}public void RecordHealing(Alterverse.Combat.Health h,float a){}}
 public class FractureCorePickup{public static void Spawn(UnityEngine.Vector3 p){}}
}
namespace Alterverse.AI {
 public class Difficulty{public float DamageTakenMultiplier=1;}
 public class MobaBotBrain:UnityEngine.MonoBehaviour{public Difficulty Difficulty=new Difficulty();}
}
namespace Alterverse.Multiplayer{public static class CoopAuthority{public static bool CanMutateAuthoritativeState=true;}}
namespace Alterverse.Audio {
 public enum DimensionSound{Shield,CoreHit,MinionDeath,HeroDeath,Heal}
 public static class DimensionAudio{public static void PlayAt(DimensionSound s,UnityEngine.Vector3 p,float a){}public static void Play(DimensionSound s,float a){}}
 public static class HeroCombatAudio{public static void ConfirmedKill(UnityEngine.GameObject g){}}
}
class ShieldRegression {
 static int checks;
 static void Check(bool b,string s){checks++;if(!b)throw new System.Exception(s);}
 static void Near(float a,float b,string s)=>Check(System.Math.Abs(a-b)<.001f,s+" actual="+a+" expected="+b);
 static Alterverse.Combat.Health Hero(Alterverse.Characters.HeroId id){var g=new UnityEngine.GameObject();g.AddComponent<Alterverse.Characters.HeroIdentity>().Hero=id;var h=g.AddComponent<Alterverse.Combat.Health>();h.Configure(200);return h;}
 static void Refresh(Alterverse.Combat.Health h,float t){UnityEngine.Time.time=t;UnityEngine.GameObject.Invoke(h,"Update");}
 static void Main(){
  UnityEngine.Time.time=0;
  foreach(Alterverse.Characters.HeroId id in System.Enum.GetValues(typeof(Alterverse.Characters.HeroId))){
   var x=Hero(id);float expected=id==Alterverse.Characters.HeroId.Nerok?90:id==Alterverse.Characters.HeroId.Rook?80:id==Alterverse.Characters.HeroId.Veyr||id==Alterverse.Characters.HeroId.Kaelon?70:50;
   Near(x.HeroBaseShieldMax,expected,"hero capacity "+id);Near(x.Shield,expected,"spawn shield "+id);
  }
  var h=Hero(Alterverse.Characters.HeroId.Timmy);h.TakeDamage(25);Near(h.Shield,25,"Timmy absorbs damage into his 50 HP base shield");Near(h.Current,200,"shield protects health");
  UnityEngine.Time.time=5;h.TakeDamage(10);Near(h.HeroBaseShieldRechargeRemaining,15,"another hit restarts recharge delay");
  Refresh(h,19.99f);Near(h.Shield,15,"no premature recharge");Refresh(h,20);Near(h.Shield,50,"full recharge after 15 damage-free seconds");
  h.TakeDamage(70);Near(h.Shield,0,"base shield depletes");Near(h.Current,180,"only excess damage reaches health");
  UnityEngine.Time.time=25;h.TakeDamage(1);Refresh(h,39.99f);Near(h.Shield,0,"unshielded damage also restarts recharge");
  Refresh(h,40);Near(h.Shield,50,"depleted shield recharges to exactly 50");
  h.AddShield(10,5,false);h.TakeDamage(5);Near(h.HeroBaseShield,50,"temporary shield is consumed before Timmy base shield");Near(h.TemporaryShield,5,"remaining temporary shield is independent");
  h.TakeDamage(2000);Check(h.IsDead,"lethal damage kills");Refresh(h,100);Near(h.Shield,0,"dead hero cannot recharge");h.RestoreFull();Near(h.Current,200,"respawn restores health");Near(h.Shield,50,"respawn restores Timmy base shield");
  h.TakeDamage(20);Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState=false;Refresh(h,115);Near(h.Shield,30,"client cannot authoritatively regenerate shield");
  Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState=true;Refresh(h,115);Near(h.Shield,50,"host applies due recharge");
  var neutral=new UnityEngine.GameObject().AddComponent<Alterverse.Combat.Health>();neutral.Configure(200);Near(neutral.HeroBaseShieldMax,0,"non-hero does not acquire a base shield");
  System.Console.WriteLine("PASS "+checks+" production Health shield assertions: Timmy 50 HP, unchanged other heroes, absorption, recharge delay/reset, death/respawn and authority.");
 }
}
'''
env=dict(os.environ,MONO_PATH=str(a.mono_root/'usr/lib/mono/4.5')+':'+str(a.mono_root/'usr/lib/mono/4.5/Facades'),MONO_CFG_DIR=str(a.mono_root/'etc'),LD_LIBRARY_PATH=str(a.mono_root/'usr/lib')+':'+str(a.mono_root/'usr/lib/x86_64-linux-gnu'))
mono=a.mono_root/'usr/bin/mono-sgen'
with tempfile.TemporaryDirectory(prefix='df-shield-regression-') as d:
 d=Path(d);(d/'Fixture.cs').write_text(engine+fixture);binary=d/'Shield.exe'
 subprocess.run([str(mono),str(a.roslyn),'-nologo','-noconfig','-langversion:9','-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.dll'),'-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.Core.dll'),'-out:'+str(binary),str(d/'Fixture.cs'),str(root/'Assets/Alterverse/Scripts/Combat/Health.cs'),str(root/'Assets/Alterverse/Scripts/Characters/TimmyBalance.cs')],env=env,check=True)
 subprocess.run([str(mono),str(binary)],env=env,check=True)
