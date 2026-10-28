using System;
using System.Reflection;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
partial class Bolt7Regression {
 static int checks;
 static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
 static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.001f,why+" actual="+a+" expected="+b);
 static Health Hero(HeroId id,int team,Vector3 point){var g=new GameObject(id.ToString());g.transform.position=point;g.AddComponent<HeroIdentity>().Hero=id;g.AddComponent<TeamMember>().Team=team;g.AddComponent<HeroLocomotion>();var h=g.AddComponent<Health>();h.Configure(360);return h;}
 static void Set(Health h,string f,float n)=>typeof(Health).GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(h,n);
 static void Main(){
  AreaAimCases();
  Clock.Reset();
  Near(Bolt7Balance.BasicInterval,HeroCombatProfile.StatsFor(HeroId.Nyra).AttackInterval/3,"three times Sivra rate");Near(Bolt7Balance.BasicDamage,2,"existing gatling damage remains unchanged");
  Check(Bolt7Balance.BasicRange<HeroCombatProfile.StatsFor(HeroId.Nyra).AttackRange,"limited basic range");Near(Bolt7Balance.FxScale,2.5f,"native FX scale");
  Check((int)HeroId.Bolt7==10 && (int)HeroId.VoidKnight==9,"stable network hero IDs");
  var kit=HeroAbilityKit.LoadoutFor(HeroId.Bolt7);Near(kit.Q.Radius,2f,"Frag Arc preview/gameplay radius 2 metres");Near(kit.E.Radius,2f,"Impulse preview/gameplay radius 2 metres");Check(Bolt7Balance.HasAbility(2)&&kit.E.Name=="Impulse Grenade","complete five-attack kit includes a second grenade");Near(Bolt7Balance.Health,280,"reduced HP");Near(Bolt7Balance.Shield,40,"40 rechargeable base shield");Near(kit.Ultimate.Power,0,"no ultimate HP damage");Check(kit.Q.Name=="Frag Arc"&&kit.W.Name=="Piston Ram"&&kit.Ultimate.Name=="Null Field","English ability labels");
  var actor=Hero(HeroId.Bolt7,0,Vector3.zero);var enemy=Hero(HeroId.Nyra,1,new Vector3(0,0,1));var ally=Hero(HeroId.Nyra,0,new Vector3(.1f,0,1));var far=Hero(HeroId.Nyra,1,new Vector3(0,0,4));
  Set(enemy,"shield",1000000);Set(enemy,"shieldUntil",100);enemy.AddShield(1,1,false);
  float health=enemy.Current,own=actor.Shield,friendly=ally.Shield;
  Check(Bolt7Combat.Nullfield(actor.gameObject,Vector3.zero)==1,"one enemy in ultimate area");Near(enemy.Shield,0,"both pools cleared without damage cap");Near(enemy.Current,health,"ultimate preserves HP");Near(actor.Shield,own,"self protected");Near(ally.Shield,friendly,"ally protected");Near(far.Shield,60,"outside radius protected");
  enemy.AddShield(25,4,false);Near(enemy.TemporaryShield,25,"new shield activation works immediately after nullfield");Near(enemy.HeroBaseShield,0,"new ability shield is independent of empty base shield");Near(enemy.RemoveAllShields(actor.gameObject),25,"second cast removes only current shield");
  Near(enemy.HeroBaseShieldRechargeRemaining,11.25f,"normal recharge delay");Time.time=11.249f;GameObject.Invoke(enemy,"Update");Near(enemy.Shield,0,"no early recharge");Time.time=11.25f;GameObject.Invoke(enemy,"Update");Near(enemy.Shield,60,"shield recovers normally");
  CoopAuthority.CanSimulateHostGameplay=false;Check(Bolt7Combat.Nullfield(actor.gameObject,Vector3.zero)==0,"replica cannot resolve");Near(enemy.Shield,60,"replica preserves shields");CoopAuthority.CanSimulateHostGameplay=true;
  CoopAuthority.CanMutateAuthoritativeState=false;Near(enemy.RemoveAllShields(actor.gameObject),0,"health authority boundary");CoopAuthority.CanMutateAuthoritativeState=true;
  enemy.DamageGate=()=>false;Check(Bolt7Combat.Nullfield(actor.gameObject,Vector3.zero)==0,"protected target");Near(enemy.Shield,60,"damage gate respected");enemy.DamageGate=null;
  var wall=new GameObject("cover");wall.transform.position=new Vector3(0,.45f,.5f);wall.transform.localScale=new Vector3(.2f,2,.1f);wall.AddComponent<Collider>();Check(Bolt7Combat.Nullfield(actor.gameObject,Vector3.zero)==0,"cover blocks ultimate");wall.SetActive(false);
  Near(Bolt7Combat.GrenadeDamage(0),35,"grenade core");Near(Bolt7Combat.GrenadeDamage(.6f),35,"grenade core edge");Near(Bolt7Combat.GrenadeDamage(1.175f),35,"grenade middle has full damage");Near(Bolt7Combat.GrenadeDamage(2f),35,"grenade outer edge");
  enemy.RestoreFull();enemy.transform.position=new Vector3(0,0,2f);Check(Bolt7Combat.Grenade(actor.gameObject,Vector3.zero)==1,"grenade single hit at radius boundary");Near(enemy.Shield,25,"grenade edge damage resolves once");enemy.transform.position=new Vector3(0,0,2.001f);Near(Bolt7Combat.Grenade(actor.gameObject,Vector3.zero),0,"outside enlarged area");
  enemy.RestoreFull();enemy.transform.position=new Vector3(0,0,.9f);Check(Bolt7Combat.Ram(actor.gameObject,Vector3.forward)==1,"ram front contact");Near(enemy.Shield,20,"ram damage");Near(enemy.GetComponent<HeroLocomotion>().LastMove.z,1.7f,"ram knockback");enemy.RestoreFull();enemy.transform.position=new Vector3(0,0,-.9f);Near(Bolt7Combat.Ram(actor.gameObject,Vector3.forward),0,"ram rear exclusion");
  enemy.RestoreFull();enemy.transform.position=new Vector3(0,0,2.7f);
  var wide=Hero(HeroId.Nyra,1,new Vector3(1.25f,0,2.2f));
  var outside=Hero(HeroId.Nyra,1,new Vector3(2.5f,0,1));
  actor.transform.position=new Vector3(0,0,2);
  Check(Bolt7Combat.Ram(actor.gameObject,Vector3.zero,Vector3.forward)==2,"full preview cone hits multiple targets from pre-dash origin");Near(enemy.Shield,20,"far cone damage");Near(wide.Shield,20,"wide cone damage");Near(outside.Shield,60,"outside cone protected");Near(far.Shield,60,"dash must not extend cone beyond original range");
  wide.gameObject.SetActive(false);outside.gameObject.SetActive(false);actor.transform.position=Vector3.zero;
  enemy.RestoreFull();enemy.transform.position=new Vector3(0,0,1f);
  Check(Bolt7Combat.Impulse(actor.gameObject,Vector3.zero)==1,"impulse grenade area hit");Near(enemy.Shield,25,"impulse grenade damage once");Near(enemy.GetComponent<HeroLocomotion>().LastMove.z,1.8f,"outward grenade knockback");
  enemy.RestoreFull();enemy.transform.position=new Vector3(0,0,2f);Near(Bolt7Combat.Impulse(actor.gameObject,Vector3.zero),1,"impulse hits at enlarged radius boundary");Near(enemy.Shield,25,"impulse boundary damage unchanged");
  enemy.RestoreFull();enemy.transform.position=new Vector3(0,0,2.001f);Near(Bolt7Combat.Impulse(actor.gameObject,Vector3.zero),0,"impulse outside enlarged radius");
  enemy.transform.position=new Vector3(0,0,1);enemy.DamageGate=()=>false;Near(Bolt7Combat.Impulse(actor.gameObject,Vector3.zero),0,"protected enemy not hit or pushed");enemy.DamageGate=null;
  actor.TakeDamage(10000);Near(Bolt7Combat.Nullfield(actor.gameObject,Vector3.zero),0,"dead caster cannot resolve");
  Console.WriteLine("PASS "+checks+" BOLT-7 assertions: production balance, health, AOE resolution, full-area damage, cover, authority, team protection, shield recharge and ram. Unity rendering/import not tested.");
 }
}

