using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.UI;

partial class Regression
{
 static void TimmyCases()
 {
  Check((int)HeroId.Timmy==8,"Timmy appends a stable hero ID");
  Check(HeroIdentity.HeroCount==System.Enum.GetValues(typeof(HeroId)).Length,"regular roster contains every catalogued hero");
  Check(HeroIdentity.HeroCount==9,"Timmy is available without a preview switch");
  Check(Alterverse.Audio.HeroCombatAudio.CueCount==135,"production audio bank accepts the constant nine-hero roster");
  for(int h=0;h<8;h++)for(int action=0;action<5;action++)for(int phase=0;phase<3;phase++)
  {
   int expected=h==7?105+action*3+phase:phase==2?70+h*5+action:h*10+action*2+phase;
   Check(Alterverse.Audio.HeroCombatAudio.Cue(h,action,phase)==expected,"existing audio cue indices are stable");
  }
  foreach(int action in new[]{0,3})for(int phase=0;phase<3;phase++)
   Check(Alterverse.Audio.HeroCombatAudio.Cue((int)HeroId.Timmy,action,phase)==Alterverse.Audio.HeroCombatAudio.Cue((int)HeroId.Veyr,action,phase),"Timmy reuses an authored fast-cut cue");
  for(int phase=0;phase<3;phase++)
   Check(Alterverse.Audio.HeroCombatAudio.Cue((int)HeroId.Timmy,1,phase)==123+phase,"Claw Swipe has its own appended cue");
  for(int phase=0;phase<3;phase++)
   Check(Alterverse.Audio.HeroCombatAudio.Cue((int)HeroId.Timmy,2,phase)==126+phase,"Stardust has its own appended cue");
  for(int phase=0;phase<3;phase++)
   Check(Alterverse.Audio.HeroCombatAudio.Cue((int)HeroId.Timmy,4,phase)==132+phase,"Scratch has an appended cue without changing legacy indices");
  Resources.AudioClipsAvailable=true;Resources.LoadedAudioPaths.Clear();
  var bank=Alterverse.Audio.HeroCombatAudio.Load();
  Check(bank[123][0]?.name=="Audio/Heroes/Timmy/Q_Cast","Claw Swipe loads the supplied slicing recording");
  Check(bank[126][0]?.name=="Audio/Heroes/Timmy/W_Cast","Stardust loads the supplied sci-fi recording");
  Check(bank[132][0]?.name=="Audio/Heroes/Timmy/Ultimate_Selected","Scratch bank loads the supplied recording");
  Resources.AudioClipsAvailable=false;Resources.LoadedAudioPaths.Clear();
  Reset();var audioActor=Hero(HeroId.Timmy,0,Vector3.zero);
  Camera.main=new GameObject("audio camera").AddComponent<Camera>();
  Alterverse.Audio.DimensionAudio.LastCue=-1;Alterverse.Audio.HeroCombatAudio.Play(audioActor,0,false,Vector3.zero);
  Check(Alterverse.Audio.DimensionAudio.LastCue==0,"Timmy basic reaches the real audio dispatcher");
  Check(Alterverse.Multiplayer.CoopHeroStateSync.LastCombatSoundActor==audioActor,"audio relay retains Timmy's actor identity");
  Alterverse.Audio.DimensionAudio.LastCue=-1;Alterverse.Audio.HeroCombatAudio.Cast(audioActor,0);
  Check(Alterverse.Audio.DimensionAudio.LastCue==123,"Claw Swipe starts its selected recording on release");
  Alterverse.Audio.DimensionAudio.LastCue=-1;Alterverse.Audio.HeroCombatAudio.Impact(audioActor,0,Vector3.zero);Alterverse.Audio.HeroCombatAudio.Windup(audioActor,1);
  Check(Alterverse.Audio.DimensionAudio.LastCue==-1,"Claw Swipe does not layer preparation or impact recordings");
  Alterverse.Audio.DimensionAudio.LastCue=-1;Alterverse.Audio.HeroCombatAudio.Cast(audioActor,1);
  Check(Alterverse.Audio.DimensionAudio.LastCue==126,"Stardust starts its selected recording on release");
  Alterverse.Audio.DimensionAudio.LastCue=-1;Alterverse.Audio.HeroCombatAudio.Impact(audioActor,1,Vector3.zero);Alterverse.Audio.HeroCombatAudio.Windup(audioActor,2);
  Check(Alterverse.Audio.DimensionAudio.LastCue==-1,"Stardust does not layer preparation or impact recordings");
  Alterverse.Audio.DimensionAudio.LastCue=-1;Alterverse.Audio.HeroCombatAudio.Cast(audioActor,3);
  Check(Alterverse.Audio.DimensionAudio.LastCue==132,"Scratch starts the selected recording on release");
  Alterverse.Audio.DimensionAudio.LastCue=-1;Alterverse.Audio.HeroCombatAudio.Impact(audioActor,3,Vector3.zero);
  Check(Alterverse.Audio.DimensionAudio.LastCue==-1,"Scratch ticks do not layer another impact recording");
  Alterverse.Audio.HeroCombatAudio.Windup(audioActor,4);
  Check(Alterverse.Audio.DimensionAudio.LastCue==-1,"selected Scratch recording has no second preparation layer");
  Camera.main=null;
  Check(TimmyBalance.TotalPoints==100,"provisional design budget totals 100");
  Near(HeroCombatProfile.StatsFor(HeroId.Timmy).MoveSpeed,HeroCombatProfile.StatsFor(HeroId.Veyr).MoveSpeed,"Zhaev speed reference");
  Check(HeroIdentity.BaseHealth(HeroId.Timmy)<HeroIdentity.BaseHealth(HeroId.Veyr),"short-range assassin has lower health");
  Check(HeroAttackCatalog.For(HeroId.Timmy).Length==5,"five attack cards including basic");
  var aim=AimingSettings.Current.For(HeroId.Timmy,3);
  Check(aim.aimType==AimType.Dash&&aim.targetingMode==TargetingMode.Cone,"Scratch uses a targeted cone leap");
  Check(AimingSettings.Current.For(HeroId.Timmy,1).aimType==AimType.Area,"Stardust has ground placement");

  Reset();var body=new GameObject("reconfigured collider");var cc=body.AddComponent<CharacterController>();cc.center=new Vector3(0,.1f,0);
  var identity=body.AddComponent<HeroIdentity>();identity.Configure(HeroId.Timmy);Near(cc.radius,.28f,"small Timmy collision radius");Near(cc.center.y,-.2f,"smaller capsule preserves ground baseline");
  identity.Configure(HeroId.Timmy);Near(cc.center.y,-.2f,"reconfiguration does not stack offset");identity.Configure(HeroId.Veyr);Near(cc.center.y,.1f,"switching hero restores authored collider center");Near(cc.radius,.4f,"existing collider radius restored");

  Reset();var a=Hero(HeroId.Timmy,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,Vector3.forward*2);
  a.GetComponent<BasicAttack>().TryAttackAimed(Forward(a));Run(.7f);Near(Total(b),1000,"basic cannot reach outside 1.6m");
  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*1.5f);
  var other=Hero(HeroId.Kaelon,1,Angle(10,1.7f));
  Check(a.GetComponent<HeroAbilityExecution>().TryCastAimed(0,Forward(a)),"Claw Swipe cast");Run(.5f);
  Near(Total(b),1000-TimmyBalance.ClawDamage,"Claw Swipe damage");Near(Total(other),1000,"Claw Swipe hits one enemy");
  Near(HeroCombatStatus.Speed(a),TimmyBalance.PassiveSpeed,"damaging skill triggers passive speed");

  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  other=Hero(HeroId.Kaelon,1,new Vector3(1,0,4));var outside=Hero(HeroId.Kaelon,1,new Vector3(1.7f,0,4));
  Camera.main=new GameObject("Stardust audio camera").AddComponent<Camera>();Alterverse.Audio.DimensionAudio.TimmyStardustPlays=0;
  a.GetComponent<HeroAbilityExecution>().TryCastAimed(1,Forward(a));Run(.2f);
  Check(Alterverse.Audio.DimensionAudio.TimmyStardustPlays==0,"Stardust recording waits for the existing warning");
  Run(.2f);Check(Alterverse.Audio.DimensionAudio.TimmyStardustPlays==1,"multi-target Stardust starts the recording exactly once");
  Near(Total(b),1000-TimmyBalance.DustDamage,"Stardust first enemy");Near(Total(other),1000-TimmyBalance.DustDamage,"Stardust is genuinely AOE");Near(Total(outside),1000,"Stardust outside radius misses");
  Near(HeroCombatStatus.Speed(b),TimmyBalance.DustSlow,"Stardust slow");
  Run(.5f);Check(Alterverse.Audio.DimensionAudio.TimmyStardustPlays==1,"Stardust victims never restart the recording");
  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  a.GetComponent<HeroAbilityExecution>().TryCastAimed(1,Forward(a));b.transform.position+=Vector3.right*3;Run(.4f);Near(Total(b),1000,"Stardust can be dodged");

  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);var execution=a.GetComponent<HeroAbilityExecution>();
  Check(execution.TryCastAimed(2,new AimPose((-Vector3.forward),(-Vector3.forward)*3.4f)),"Jet Pounce backwards");Near(a.transform.position.z,-TimmyBalance.JetRange,"Jet range");
  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*6);other=Hero(HeroId.Kaelon,1,new Vector3(.8f,0,6));execution=a.GetComponent<HeroAbilityExecution>();
  Camera.main=new GameObject("Scratch audio camera").AddComponent<Camera>();Alterverse.Audio.DimensionAudio.TimmyUltimatePlays=0;
  Check(execution.TryCastAimed(3,Forward(a,9)),"Scratch acquires in the cast frame");Check(!execution.TryCastAimed(3,Forward(a,9)),"Scratch duplicate rejected");
  Run(.65f);Check(Alterverse.Audio.DimensionAudio.TimmyUltimatePlays==1,"Scratch plays the supplied recording at leap start");Check(b.GetComponent<HeroCombatStatus>()?.Rooted==true,"Scratch holds after physical contact");
  Check(!HeroCombatStatus.CanAct(a)&&HeroCombatStatus.CanAct(b),"channel locks Timmy but victim can counterattack");
  Check(!execution.TryCastAimed(0,Forward(a)),"channel rejects another skill");Near(Total(b),1000,"first scratch waits half a second");
  Run(1.9f);Check(b.GetComponent<HeroCombatStatus>().Rooted,"hold lasts nearly two seconds");
  Run(.2f);Check(Alterverse.Audio.DimensionAudio.TimmyUltimatePlays==1,"four Scratch ticks never restart the selected recording");Near(Total(b),1000-TimmyBalance.ScratchDamage,"Scratch applies all four ticks once");Near(Total(other),1000,"Scratch damages exactly one enemy");
  Check(!b.GetComponent<HeroCombatStatus>().Rooted&&!a.GetComponent<HeroCombatStatus>().Rooted,"normal completion releases both roots");

  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*6);execution=a.GetComponent<HeroAbilityExecution>();
  execution.TryCastAimed(3,Forward(a,9));Run(.25f);b.transform.position+=Vector3.right*3;Run(2.7f);
  Near(Total(b),1000,"Scratch leap does not home or guarantee damage");Check(b.GetComponent<HeroCombatStatus>()?.Rooted!=true,"miss does not root");Near(a.transform.position.x,0,"leap keeps captured endpoint");

  for(int interruption=0;interruption<5;interruption++)
  {
   Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*6);execution=a.GetComponent<HeroAbilityExecution>();
   execution.TryCastAimed(3,Forward(a,9));Run(.65f);Check(b.GetComponent<HeroCombatStatus>().Rooted,"interruption starts during grip");
   if(interruption==0)a.GetComponent<HeroCombatStatus>().Stun(.5f);
   else if(interruption==1)a.GetComponent<Health>().TakeDamage(2000);
   else if(interruption==2)a.SetActive(false);
   else if(interruption==3)a.GetComponent<HeroLocomotion>().BeginForcedMove(Vector3.right*3,.2f);
   else b.GetComponent<Health>().TakeDamage(2000);
   Step();Check(!b.GetComponent<HeroCombatStatus>().Rooted,"interruption releases victim "+interruption);
   if(interruption<4){Run(2);Near(Total(b),1000,"interruption stops pending ticks "+interruption);}
  }
  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*6);
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,.5f,3);wall.transform.localScale=new Vector3(2,2,.3f);
  Check(!a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Forward(a,9)),"Scratch rejects through wall");Near(a.GetComponent<HeroAbilityExecution>().Remaining(3),0,"invalid Scratch spends no cooldown");
  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,(-Vector3.forward)*4);
  Check(!a.GetComponent<HeroAbilityExecution>().TryCastAimed(3,Forward(a,9)),"Scratch cannot capture behind cone");
  var status=b.AddComponent<HeroCombatStatus>();status.ApplyNetworkSnapshot(false,0);
  Check(status.Rooted&&HeroCombatStatus.CanAct(b),"network victim root preserves attacks");Check(!b.GetComponent<HeroLocomotion>().BeginDash(Vector3.zero),"root blocks predicted escape dash");
  status.ApplyNetworkSnapshot(true,0);Check(!HeroCombatStatus.CanAct(b),"network channel blocks actions");status.ClearNetworkSnapshot();Check(!status.Rooted,"snapshot cleanup");
  status.TryRoot(a,2);status.Slow(.7f,1);status.ReleaseRoot(b);Check(status.Rooted,"wrong owner cannot release grip");status.ReleaseRoot(a);Near(status.MoveMultiplier,.7f,"root cleanup preserves unrelated slow");

  Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Angle(30,5));
  var minion=new GameObject("nearby soldier");minion.transform.position=Vector3.forward*1;minion.AddComponent<TeamMember>().Initialize(1);minion.AddComponent<Health>();minion.AddComponent<LaneMinion>().Team=1;
  var gesture=ManualAiming.For(a);Check(gesture.Begin(3,new Vector2(700,100)),"Scratch hold begins");gesture.Drag(new Vector2(700,196),1,false);
  Check(a.GetComponent<SoftTargeting>().Current==b.GetComponent<Health>(),"Scratch cone marks a hero without selecting the closer soldier");
  Resources.PreviewShaderAvailable=true;var overlay=a.AddComponent<AbilityRangePreviewOverlay>();Call(overlay,"LateUpdate");
  gesture.Cancel();Call(overlay,"LateUpdate");Near(a.GetComponent<HeroAbilityExecution>().Remaining(3),0,"Scratch cancel consumes no cooldown");
  Check(gesture.Begin(3,new Vector2(700,100))&&gesture.Release(),"Scratch tap captures the same hero");Run(2.8f);Near(Total(b),946,"Scratch tap still uses contact and timed ticks");Near(Total(minion),1000,"Scratch never hits the marked corridor's soldier");
 }
}
