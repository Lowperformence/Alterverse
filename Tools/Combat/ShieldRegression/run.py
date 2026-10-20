#!/usr/bin/env python3
"""Compile and exercise production Health.cs with deterministic Unity substitutes.
This verifies shield/damage/recharge rules; it is not a Unity or multiplayer test.
"""
import argparse, json, os, re, subprocess, tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path,help='Root of a .NET 8 SDK; alternative to Mono/Roslyn.')
p.add_argument('--mono-root',type=Path)
p.add_argument('--roslyn',type=Path)
a=p.parse_args();root=Path(__file__).resolve().parents[3]
if not a.dotnet_root and not (a.mono_root and a.roslyn):
 p.error('provide --dotnet-root, or both --mono-root and --roslyn')
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
 HERO_ENUM
 public class HeroIdentity:UnityEngine.MonoBehaviour{public HeroId Hero;}
 public class VoidKnightState:UnityEngine.MonoBehaviour{public float ReduceFrontalDamage(float amount,UnityEngine.GameObject source)=>amount;}
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
   float previous=id switch {
    Alterverse.Characters.HeroId.Nerok=>90,Alterverse.Characters.HeroId.Rook=>80,
    Alterverse.Characters.HeroId.Veyr=>70,Alterverse.Characters.HeroId.Kaelon=>70,
    Alterverse.Characters.HeroId.VoidKnight=>60,Alterverse.Characters.HeroId.Nyra=>30,_=>50};
   float expected=previous+30;
   var x=Hero(id);
   Near(x.HeroBaseShieldMax,expected,"hero capacity "+id);Near(x.Shield,expected,"spawn shield "+id);
   x.TakeDamage(10);Near(x.HeroBaseShield,expected-10,"damage absorption "+id);
   Near(x.HeroBaseShieldRechargeRemaining,11.25f,"shorter delay "+id);
   Refresh(x,11.249f);Near(x.HeroBaseShield,expected-10,"no premature recharge "+id);
   Refresh(x,11.25f);Near(x.HeroBaseShield,expected,"recharge boundary "+id);
   // A later hit at the exact deadline must restore first, without relying on Update.
   UnityEngine.Time.time=20;x.TakeDamage(10);UnityEngine.Time.time=31.25f;x.TakeDamage(5);
   Near(x.HeroBaseShield,expected-5,"due shield restored before new hit "+id);
   x.TakeDamage(2000);Refresh(x,100);Near(x.Shield,0,"dead shield stays empty "+id);
   x.RestoreFull();Near(x.HeroBaseShield,expected,"respawn capacity "+id);
   // Bots share the same authoritative shield configuration as player heroes.
   x.gameObject.AddComponent<Alterverse.AI.MobaBotBrain>();x.gameObject.AddComponent<Alterverse.Combat.TeamMember>().Team=1;
   x.Configure(200);Near(x.HeroBaseShieldMax,expected,"bot capacity "+id);
   x.TakeDamage(10);Refresh(x,111.25f);Near(x.HeroBaseShield,expected,"bot recharge "+id);
   Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState=false;
   x.ApplyNetworkSnapshot(200,expected+80,false);Near(x.Shield,expected+80,"replica accepts new capacity plus ability shield "+id);
   x.ApplyNetworkSnapshot(200,10000,false);Near(x.Shield,expected+80,"replica clamps excess shield "+id);
   Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState=true;UnityEngine.Time.time=0;
  }
  var ione=Hero(Alterverse.Characters.HeroId.Elyra);ione.AddShield(20,2.25f,false);
  Near(ione.TemporaryShield,20,"Phase Fold adds a temporary 20 HP shield");Near(ione.Shield,100,"Phase Fold preserves Ione's permanent shield");
  ione.TakeDamage(7);Near(ione.TemporaryShield,13,"incoming damage consumes Phase Fold first");Near(ione.HeroBaseShield,80,"Phase Fold protects the base shield");Near(ione.Current,200,"Phase Fold protects health");
  Near(ione.HeroBaseShieldRechargeRemaining,0,"ability shield hit does not damage a full base shield");
  Refresh(ione,2.249f);Near(ione.TemporaryShield,13,"Phase Fold remains before its duration ends");
  Refresh(ione,2.25f);Near(ione.TemporaryShield,0,"Phase Fold expires on its duration boundary");Near(ione.HeroBaseShield,80,"expiry does not consume the permanent shield");
  UnityEngine.Time.time=0;
  var h=Hero(Alterverse.Characters.HeroId.Timmy);h.TakeDamage(25);Near(h.Shield,55,"Timmy absorbs damage into his 80 HP base shield");Near(h.Current,200,"shield protects health");
  UnityEngine.Time.time=5;h.TakeDamage(10);Near(h.HeroBaseShieldRechargeRemaining,11.25f,"another hit restarts recharge delay");
  Refresh(h,16.249f);Near(h.Shield,45,"no premature recharge");Refresh(h,16.25f);Near(h.Shield,80,"full recharge after 11.25 damage-free seconds");
  h.TakeDamage(100);Near(h.Shield,0,"base shield depletes");Near(h.Current,180,"only excess damage reaches health");
  UnityEngine.Time.time=20;h.TakeDamage(1);Refresh(h,31.249f);Near(h.Shield,0,"unshielded damage also restarts recharge");
  Refresh(h,31.25f);Near(h.Shield,80,"depleted shield recharges to exactly 80");
  h.AddShield(10,5,false);h.TakeDamage(5);Near(h.HeroBaseShield,80,"temporary shield is consumed before Timmy base shield");Near(h.TemporaryShield,5,"remaining temporary shield is independent");
  h.TakeDamage(2000);Check(h.IsDead,"lethal damage kills");Refresh(h,100);Near(h.Shield,0,"dead hero cannot recharge");h.RestoreFull();Near(h.Current,200,"respawn restores health");Near(h.Shield,80,"respawn restores Timmy base shield");
  h.TakeDamage(20);Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState=false;Refresh(h,111.25f);Near(h.Shield,60,"client cannot authoritatively regenerate shield");
  Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState=true;Refresh(h,111.25f);Near(h.Shield,80,"host applies due recharge");
  var neutral=new UnityEngine.GameObject().AddComponent<Alterverse.Combat.Health>();neutral.Configure(200);Near(neutral.HeroBaseShieldMax,0,"non-hero does not acquire a base shield");
  System.Console.WriteLine("PASS "+checks+" production Health shield assertions: all ten heroes/bots +30 shield, 11.25s delay, Phase Fold unchanged, absorption, deadline/reset, snapshots, death/respawn and authority. Unity/network transport not tested.");
 }
}
'''
hero_enum=re.search(r'public enum HeroId\s*\{[^}]+\}',(root/'Assets/Alterverse/Scripts/Characters/HeroIdentity.cs').read_text())
if not hero_enum:raise RuntimeError('Production HeroId enum not found')
fixture=fixture.replace('HERO_ENUM',hero_enum.group())
with tempfile.TemporaryDirectory(prefix='df-shield-regression-') as d:
 d=Path(d);(d/'Fixture.cs').write_text(engine+fixture)
 sources=[d/'Fixture.cs',root/'Assets/Alterverse/Scripts/Combat/Health.cs',root/'Assets/Alterverse/Scripts/Characters/TimmyBalance.cs',root/'Assets/Alterverse/Scripts/Characters/HeroAbilityKit.cs']
 if a.dotnet_root:
  sdk=a.dotnet_root.resolve();dotnet=sdk/'dotnet';csc=sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
  references=sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];binary=d/'Shield.dll'
  subprocess.run([str(dotnet),str(csc),'-nologo','-langversion:9','-out:'+str(binary),*['-r:'+str(f) for f in references.glob('*.dll')],*map(str,sources)],check=True)
  binary.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
  subprocess.run([str(dotnet),str(binary)],check=True)
 else:
  env=dict(os.environ,MONO_PATH=str(a.mono_root/'usr/lib/mono/4.5')+':'+str(a.mono_root/'usr/lib/mono/4.5/Facades'),MONO_CFG_DIR=str(a.mono_root/'etc'),LD_LIBRARY_PATH=str(a.mono_root/'usr/lib')+':'+str(a.mono_root/'usr/lib/x86_64-linux-gnu'))
  mono=a.mono_root/'usr/bin/mono-sgen';binary=d/'Shield.exe'
  subprocess.run([str(mono),str(a.roslyn),'-nologo','-noconfig','-langversion:9','-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.dll'),'-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.Core.dll'),'-out:'+str(binary),*map(str,sources)],env=env,check=True)
  subprocess.run([str(mono),str(binary)],env=env,check=True)

