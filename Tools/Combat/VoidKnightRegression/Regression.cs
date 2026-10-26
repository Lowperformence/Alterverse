using System;
using System.Reflection;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Audio;
using Alterverse.Multiplayer;
class Regression
{
 static int passed;
 static void Check(bool b,string name){if(!b)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
 static void Near(float a,float b,string name)=>Check(Math.Abs(a-b)<.025f,name+" ("+a+")");
 static void Reset(){foreach(var g in GameObject.All.ToArray())g.SetActive(false);GameObject.All.Clear();Health.Active.Clear();Clock.Reset();CoopAuthority.Active=false;CoopAuthority.LocalHasServerAuthority=true;Camera.main=null;}
 static GameObject Actor(HeroId id,int team,Vector3 position){var a=new GameObject(id.ToString());a.transform.position=position;a.AddComponent<TeamMember>().Initialize(team);a.AddComponent<Health>();a.AddComponent<HeroIdentity>().Configure(id);a.AddComponent<HeroCombatProfile>().Configure(id);a.AddComponent<HeroAbilityKit>().Configure(id);a.AddComponent<HeroLocomotion>();a.AddComponent<HeroCombatStatus>();a.AddComponent<HeroAbilityExecution>();if(id==HeroId.VoidKnight)a.AddComponent<VoidKnightState>();return a;}
 static void Tick(float dt){Clock.Advance(dt);foreach(var g in GameObject.All.ToArray())if(g.activeInHierarchy){g.GetComponent<HeroLocomotion>()?.Advance(Time.time);if(g.GetComponent<HeroAttackDelivery>() is {} delivery)GameObject.Invoke(delivery,"Update");if(g.GetComponent<VoidKnightState>() is {} s)GameObject.Invoke(s,"Update");if(g.GetComponent<HeroAbilityExecution>() is {} e)GameObject.Invoke(e,"Update");}}
 static void Run(float seconds){for(int i=0;i<(int)Math.Round(seconds/.01f);i++)Tick(.01f);}
 static AimPose Aim(GameObject a)=>new(Vector3.forward,a.transform.position+Vector3.forward*4,null,true);
 static void Unlock(GameObject a)=>((float[])typeof(HeroAbilityExecution).GetField("ready",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(a.GetComponent<HeroAbilityExecution>()))[3]=0;
 static float Hp(GameObject a)=>a.GetComponent<Health>().Current;
 static void Main()
 {
  Check((int)HeroId.VoidKnight==9&&(int)HeroId.Bolt7==10&&HeroIdentity.HeroCount==11,"stable appended roster ID");Near(HeroIdentity.AttackDamage(HeroId.VoidKnight),20,"20 basic damage");
  Check(HeroIdentity.BaseHealth(HeroId.VoidKnight)<HeroIdentity.BaseHealth(HeroId.Kaelon),"less health than tank");Check(HeroIdentity.BaseHealth(HeroId.VoidKnight)>HeroIdentity.BaseHealth(HeroId.Veyr),"more health than assassin");
  foreach(var id in new[]{HeroId.Veyr,HeroId.Timmy,HeroId.Kaelon})Check(VoidKnightBalance.BasicRange>HeroCombatProfile.StatsFor(id).AttackRange,"longer reach than "+id);
  Near(HeroCombatProfile.StatsFor(HeroId.VoidKnight).AttackInterval,1.2f,"current greatsword attack interval");Check(!HeroAttackDelivery.IsRanged(HeroId.VoidKnight),"greatsword remains melee beyond 3 metres");
  Check(AimingSettings.Current.For(HeroId.VoidKnight,2).aimType==AimType.Dash,"lunge aim type");Check(AimingSettings.Current.For(HeroId.VoidKnight,3).aimType==AimType.AreaCone,"ultimate cone aim type");
  Check(HeroCombatAudio.Cue(8,4,0)==132&&HeroCombatAudio.Cue(7,4,0)==117,"legacy audio cues unchanged");
  for(int action=0;action<5;action++)for(int phase=0;phase<3;phase++)Check(HeroCombatAudio.Cue(9,action,phase)==135+action*3+phase,"appended audio cue "+action+"/"+phase);
  Resources.AudioClipsAvailable=true;Resources.LoadedAudioPaths.Clear();var bank=HeroCombatAudio.Load();Check(bank.Length==HeroIdentity.HeroCount*15,"expanded audio bank");Check(Resources.LoadedAudioPaths.FindAll(p=>p.StartsWith("Audio/Heroes/VoidKnight/")).Count==16,"all 16 runtime recordings requested");Resources.AudioClipsAvailable=false;
  Reset();var a=Actor(HeroId.VoidKnight,0,Vector3.zero);var b=Actor(HeroId.Veyr,1,Vector3.forward*2);var state=a.GetComponent<VoidKnightState>();Check(state.BeginGuard(),"guard starts");Check(a.GetComponent<HeroCombatStatus>().Rooted&&!HeroCombatStatus.CanAct(a),"guard anchors and locks actions");Near(state.ReduceFrontalDamage(40,b),8,"front guard blocks eighty percent of damage");b.transform.position=Vector3.back*2;Near(state.ReduceFrontalDamage(40,b),40,"rear bypasses guard");b.transform.position=Vector3.right*2;Near(state.ReduceFrontalDamage(40,b),40,"flank bypasses guard");Near(state.ReduceFrontalDamage(40,null),40,"unknown source bypasses guard");Run(.7f);Check(!state.Guarding&&!a.GetComponent<HeroCombatStatus>().Rooted,"guard expires and releases root");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);state=a.GetComponent<VoidKnightState>();state.RecordBasicHit();state.RecordBasicHit();state.RecordSkillHit();Near(a.GetComponent<Health>().Shield,0,"two basics cannot trigger shield");state.RecordBasicHit();state.RecordSkillHit();Near(a.GetComponent<Health>().Shield,30,"three basics prime 30 shield");state.RecordSkillHit();Near(a.GetComponent<Health>().Shield,30,"passive consumed once");Check(state.Charges==0,"passive charges reset");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);state=a.GetComponent<VoidKnightState>();CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Check(!state.BeginGuard(),"replica cannot start gameplay guard");state.RecordBasicHit();Check(state.Charges==0,"replica cannot build passive");Check(!a.GetComponent<HeroAbilityExecution>().TryCastAimed(2,Aim(a)),"replica cannot apply lunge");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.forward*3);Unlock(a);Check(a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a)),"ultimate starts");Run(.64f);Near(Hp(b),1000,"ultimate no damage before warning");Run(.03f);Near(Hp(b),938,"ultimate 42 base plus 20 drain damage");Run(.7f);Near(Hp(b),938,"ultimate hits each enemy once");Near(HeroCombatStatus.Speed(b),.75f,"ultimate applies slow");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.forward*3);Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.2f);a.GetComponent<HeroCombatStatus>().Stun(1);Tick(.01f);Run(.6f);Near(Hp(b),1000,"stun cancels pending cleave");Check(!a.GetComponent<HeroCombatStatus>().Rooted,"interruption releases owned root");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.forward*2);var behind=Actor(HeroId.Veyr,1,Vector3.forward*3.3f);Check(a.GetComponent<HeroAbilityExecution>().TryCastAimed(2,Aim(a)),"lunge starts");Run(.15f);Near(Hp(b),1000,"lunge respects preparation");Run(.6f);Near(Hp(b),978,"lunge deals 22 once");Near(Hp(behind),1000,"first enemy stops lunge");Check(a.transform.position.z<2.1f,"lunge stops near first contact");Near(HeroCombatStatus.Speed(b),.5f,"lunge slows hit enemy by 50 percent");Near(HeroCombatStatus.Speed(behind),1f,"lunge does not slow enemy behind first contact");Near(HeroCombatStatus.Speed(a),1f,"lunge does not slow caster");Run(2.1f);Near(HeroCombatStatus.Speed(b),1f,"lunge slow expires after two seconds");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.forward*3);Check(a.GetComponent<HeroAbilityExecution>().TryCastAimed(0,Aim(a)),"sweep starts");Run(.5f);Near(Hp(b),976,"sweep deals 24");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.forward*2);Check(a.GetComponent<HeroAbilityExecution>().TryCastAimed(1,Aim(a)),"parry cast starts");a.GetComponent<Health>().TakeDamage(40,b);Near(Hp(a),992,"health boundary applies parry reduction");Run(1.1f);Near(Hp(b),986,"successful guard triggers 14 damage counter");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.forward*3);a.AddComponent<BasicAttack>();Check(a.GetComponent<BasicAttack>().TryAttackAimed(Aim(a)),"basic starts at extended range");Run(.27f);Near(Hp(b),1000,"basic has readable windup");Run(.04f);Near(Hp(b),980,"basic exact 20 at 3 metres");Check(a.GetComponent<VoidKnightState>().Charges==1,"real basic hit builds passive");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);a.AddComponent<BasicAttack>();a.GetComponent<BasicAttack>().TryAttackAimed(Aim(a));Run(.5f);Check(a.GetComponent<VoidKnightState>().Charges==0,"missed basic does not build passive");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.back*2);Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.7f);Near(Hp(b),1000,"cleave excludes targets behind actor");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);b=Actor(HeroId.Veyr,1,Vector3.forward*3);Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.2f);a.SetActive(false);Run(.7f);Near(Hp(b),1000,"disabled actor cannot land pending ultimate");
  Check(!Alterverse.Shop.HeroUnlockCatalog.Released(HeroId.VoidKnight),"Void Knight remains unreleased");
  Check(!Alterverse.Shop.HeroUnlockInventory.Owned(HeroId.VoidKnight),"unreleased hero blocked even with stored ownership");
  Check(Alterverse.Shop.HeroUnlockInventory.Purchase(HeroId.VoidKnight)==Alterverse.Shop.HeroUnlockResult.NotPurchasable,"purchase rejected before release");
  Check(Alterverse.Shop.ProfileTransaction.Recoveries==0&&Alterverse.Shop.ProfileTransaction.Commits==0,"blocked purchase touches no transaction");
  Check(Alterverse.Shop.PlayerWallet.ServerRequests==0,"blocked purchase sends no server request");
  Check(!PlayerHeroSelection.Select(HeroId.VoidKnight)&&PlayerHeroSelection.Selected!=HeroId.VoidKnight,"unreleased hero cannot deploy");
  Check(!Alterverse.Shop.HeroUnlockCatalog.Released(HeroId.Timmy),"Timmy release lock preserved");
  Check(Alterverse.Shop.HeroUnlockCatalog.Price(HeroId.Traveler)==1500&&Alterverse.Shop.HeroUnlockCatalog.Price(HeroId.Nerok)==1900&&Alterverse.Shop.HeroUnlockCatalog.Price(HeroId.Timmy)==1900,"current hero prices preserved");
  Check(Alterverse.Shop.HeroUnlockCatalog.Price(HeroId.VoidKnight)==1600,"Void Knight costs exactly 1600 shards");
  Check(Alterverse.Shop.HeroUnlockCatalog.Purchasable(HeroId.VoidKnight),"Void Knight uses premium hero purchase flow after release");
  Check(Alterverse.Shop.HeroUnlockCatalog.PurchaseSku(HeroId.VoidKnight)=="hero_void_knight"&&Alterverse.Shop.HeroUnlockCatalog.OwnedKey(HeroId.VoidKnight)=="DF_HeroOwned_VoidKnight","Void Knight SKU and persisted ownership mapping");
  Check(Alterverse.Shop.WeaponSkinCatalog.VariantCount(HeroId.VoidKnight)==2,"Void Knight has two weapon shop variants");
  for(int v=0;v<2;v++)
  {
   var item=Alterverse.Shop.WeaponSkinCatalog.For(HeroId.VoidKnight,v);
   Check(item.Hero==HeroId.VoidKnight&&item.Variant==v&&item.WeaponName=="Rift Edge Greatsword","shop returns correct greatsword variant "+v);
   Near(item.Price,500,"greatsword uses existing standard price "+v);
   int recoveries=Alterverse.Shop.ProfileTransaction.Recoveries,requests=Alterverse.Shop.PlayerWallet.ServerRequests;
   Check(Alterverse.Shop.WeaponSkinInventory.Purchase(HeroId.VoidKnight,v)==Alterverse.Shop.AvatarPurchaseResult.InvalidItem,"unreleased weapon purchase is blocked "+v);
   Check(Alterverse.Shop.ProfileTransaction.Recoveries==recoveries&&Alterverse.Shop.PlayerWallet.ServerRequests==requests,"weapon preview cannot start purchase transaction "+v);
  }
  Check(Alterverse.Shop.WeaponSkinCatalog.For(HeroId.VoidKnight,0).Name!=Alterverse.Shop.WeaponSkinCatalog.For(HeroId.VoidKnight,1).Name,"greatsword variant names are distinct");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);a.GetComponent<Health>().Current=920;b=Actor(HeroId.Veyr,1,Vector3.forward*2);var second=Actor(HeroId.Veyr,1,new Vector3(1,0,2));var ally=Actor(HeroId.Kaelon,0,Vector3.forward*2);Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.7f);
  Near(Hp(b),938,"first cleave target loses base and drain damage");Near(Hp(second),938,"second cleave target loses base and drain damage");Near(Hp(a),960,"two targets restore twenty HP each");Near(Hp(ally),1000,"cleave never drains ally");Run(.5f);Near(Hp(a),960,"cleave healing occurs once per target");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);a.GetComponent<Health>().Current=900;b=Actor(HeroId.Veyr,1,Vector3.forward*2);b.GetComponent<Health>().Current=5;Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.7f);Near(Hp(a),905,"five remaining enemy HP can restore only five");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);a.GetComponent<Health>().Current=900;b=Actor(HeroId.Veyr,1,Vector3.forward*2);b.GetComponent<Health>().Shield=100;Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.7f);Near(Hp(a),900,"shield-only damage grants no life drain");Near(b.GetComponent<Health>().Shield,38,"shield absorbs full sixty-two damage");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);a.GetComponent<Health>().Current=900;b=Actor(HeroId.Veyr,1,Vector3.forward*2);b.GetComponent<Health>().DamageGate=()=>false;Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.7f);Near(Hp(a),900,"rejected cleave damage grants no healing");Near(Hp(b),1000,"invulnerable target loses no HP");
  Reset();a=Actor(HeroId.VoidKnight,0,Vector3.zero);a.GetComponent<Health>().Current=995;b=Actor(HeroId.Veyr,1,Vector3.forward*2);Unlock(a);a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Aim(a));Run(.7f);Near(Hp(a),1000,"life drain cannot exceed caster maximum HP");
  Console.WriteLine("VOID KNIGHT: "+passed+" checks passed. Engine fixtures; native Unity and NGO not covered.");
 }
}

