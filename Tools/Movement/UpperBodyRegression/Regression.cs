using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
partial class Regression {
 static int assertions,cases;static StreamWriter csv;static string fixture;
 static readonly int[] parents={-1,0,1,2,2,4,5,2,7,8,0,10,11,0,13,14};
 class Rig {public GameObject actor;public Transform model;public HeroVisualMotion motion;public MeleeBasicAttackAnimation melee;
  public Transform[] bones=new Transform[16];public Quaternion[] rest=new Quaternion[16];public Transform[] grips=new Transform[2];}
 static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
 static void Near(float a,float b,float tolerance,string message){Check(Math.Abs(a-b)<=tolerance,message+": "+a+" != "+b);}
 static void SameQ(Quaternion a,Quaternion b,string m){Near(Quaternion.Angle(a,b),0,.0001f,m);}
 static void SameV(Vector3 a,Vector3 b,string m){Near(Vector3.Distance(a,b),0,.00001f,m);}
 static void Call(object obj,string method){obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);}
 static float PrivateFloat(object obj,string name){return (float)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
 static float F(string s){return float.Parse(s,CultureInfo.InvariantCulture);}
 static Rig Make(HeroId hero,bool native){
  Time.time=10;Time.deltaTime=1f/60f;var r=new Rig();
  var parent=new GameObject("Parent").transform;parent.localRotation=Quaternion.Euler(13,47,-9);parent.localScale=new Vector3(1.07f,.93f,1.03f);
  r.actor=new GameObject("Actor");r.actor.transform.parent=parent;r.actor.transform.localPosition=new Vector3(3,0,-2);r.actor.transform.localRotation=Quaternion.Euler(0,-31,0);
  var ident=r.actor.AddComponent<HeroIdentity>();ident.Hero=hero;r.actor.AddComponent<BasicAttack>();r.actor.AddComponent<Health>();
  r.motion=r.actor.AddComponent<HeroVisualMotion>();r.motion.PresentedRotation=r.actor.transform.rotation;r.motion.PresentedVelocity=r.motion.PresentedRotation*(Vector3.forward*6);
  r.actor.AddComponent<HeroLocomotion>();r.actor.AddComponent<AuthoredHeroPresentation>();r.melee=r.actor.AddComponent<MeleeBasicAttackAnimation>();
  var visual=new GameObject("DF_VisualRoot").transform;visual.parent=r.actor.transform;visual.localScale=new Vector3(1.22f,1.32f,1.22f);
  r.model=new GameObject("DF_AuthoredModel").transform;r.model.parent=visual;r.model.localScale=new Vector3(1.10f,1,1.10f);r.model.localRotation=Quaternion.Euler(0,19,0);
  if(native){var all=new Dictionary<string,Transform>();foreach(string row in File.ReadAllLines(fixture)){
    if(row.StartsWith("bone,")||row.Trim().Length==0)continue;var p=row.Split(',');var b=new GameObject(p[0]).transform;
    b.parent=p[1].Length==0?r.model:all[p[1]];b.localPosition=new Vector3(F(p[2]),F(p[3]),F(p[4]));b.localRotation=new Quaternion(F(p[5]),F(p[6]),F(p[7]),F(p[8]));all[p[0]]=b;}
   for(int i=0;i<16;i++)r.bones[i]=all[CombatAnimationLibrary.Bones[i]];r.grips[0]=all["WeaponGripL"];r.grips[1]=all["WeaponGripR"];
  }else{
   Vector3[] positions={new Vector3(0,1,0),new Vector3(0,.15f,0),new Vector3(0,.20f,0),new Vector3(0,.25f,0),
    new Vector3(-.19f,.20f,0),new Vector3(0,-.29f,.03f),new Vector3(0,-.26f,.05f),
    new Vector3(.19f,.20f,0),new Vector3(0,-.29f,.03f),new Vector3(0,-.26f,.05f),
    new Vector3(-.14f,0,0),new Vector3(0,-.42f,0),new Vector3(0,-.40f,.04f),new Vector3(.14f,0,0),new Vector3(0,-.42f,0),new Vector3(0,-.40f,.04f)};
   for(int i=0;i<16;i++){r.bones[i]=new GameObject(CombatAnimationLibrary.Bones[i]).transform;r.bones[i].parent=parents[i]<0?r.model:r.bones[parents[i]];r.bones[i].localPosition=positions[i];
    r.bones[i].localRotation=i==4?Quaternion.Euler(23,37,-58):i==7?Quaternion.Euler(-21,-33,61):i==5?Quaternion.Euler(-17,12,9):i==8?Quaternion.Euler(18,-14,-7):Quaternion.identity;}
   for(int g=0;g<2;g++){r.grips[g]=new GameObject("WeaponGrip"+g).transform;r.grips[g].parent=r.bones[g==0?6:9];r.grips[g].localPosition=new Vector3(.02f,-.03f,.04f);r.grips[g].localRotation=Quaternion.Euler(11,16,-19);}
  }
  for(int i=0;i<16;i++)r.rest[i]=r.bones[i].localRotation;
  Call(r.melee,"Awake");Call(r.melee,"OnEnable");r.melee.InitializeRig(r.model);return r;
 }
 static float ArmAngle(Rig r,int i){return Quaternion.Angle(r.rest[i],r.bones[i].localRotation);}
 // Reproduce AuthoredHeroPresentation's fresh Rest arm writes before its synchronous EvaluatePose.
 // Rifle socket writes are not present in the stub, so preserve its supplied socket-owned pose.
 static void EvaluateAuthoredFrame(Rig r){if(!r.motion.AbilityActionActive&&r.actor.GetComponent<HeroIdentity>().Hero!=HeroId.Nyra)
   for(int i=4;i<=9;i++)r.bones[i].localRotation=r.rest[i];r.melee.EvaluatePose();}
 static void GuardEqualsFinal(Rig r){var field=typeof(MeleeBasicAttackAnimation).GetField("bones",BindingFlags.NonPublic|BindingFlags.Instance);var bones=(Array)field.GetValue(r.melee);
  for(int i=0;i<16;i++){object bone=bones.GetValue(i);var guard=(Quaternion)bone.GetType().GetField("GuardWorld").GetValue(bone);SameQ(guard,r.bones[i].rotation,"GuardWorld must capture final gait "+i);}}
 static void ProfileMatrix(){foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))foreach(bool native in new[]{false,true})foreach(string direction in new[]{"forward","back","strafe"})foreach(float run in new[]{0f,1f}){
   Rig r=Make(hero,native);r.motion.RunBlend=run;Vector3 v=direction=="back"?-Vector3.forward:direction=="strafe"?Vector3.right:Vector3.forward;
   r.motion.PresentedVelocity=r.motion.PresentedRotation*(v*6);Quaternion grip0=r.grips[0].localRotation,grip1=r.grips[1].localRotation;
   Vector3 gripPos0=r.grips[0].localPosition,gripPos1=r.grips[1].localPosition;
   float maxL=0,maxR=0,maxEL=0,maxER=0;
   for(int sample=0;sample<16;sample++){r.motion.GaitPhase=sample*Mathf.PI*2/16;Time.time+=Time.deltaTime;
    r.actor.transform.ClearWrites();EvaluateAuthoredFrame(r);cases++;GuardEqualsFinal(r);
    Check(r.actor.transform.rotationWrites+r.actor.transform.positionWrites+r.actor.transform.scaleWrites==0,"Actor root writes");
    Check(r.model.rotationWrites+r.model.positionWrites+r.model.scaleWrites==0,"Model root writes");
    for(int i=10;i<16;i++)Check(r.bones[i].rotationWrites+r.bones[i].positionWrites+r.bones[i].scaleWrites==0,"Moving gait directly wrote leg "+i);
    SameQ(grip0,r.grips[0].localRotation,"Fitted left grip local rotation");SameQ(grip1,r.grips[1].localRotation,"Fitted right grip local rotation");
    SameV(gripPos0,r.grips[0].localPosition,"Fitted left grip position");SameV(gripPos1,r.grips[1].localPosition,"Fitted right grip position");
    if(hero==HeroId.Nyra)for(int i=4;i<=9;i++)SameQ(r.rest[i],r.bones[i].localRotation,"Rifle arm changed");
    maxL=Math.Max(maxL,ArmAngle(r,4));maxR=Math.Max(maxR,ArmAngle(r,7));maxEL=Math.Max(maxEL,ArmAngle(r,5));maxER=Math.Max(maxER,ArmAngle(r,8));
   }
   var expected=HeroUpperBodyGait.Sample(hero,0,1,run,v.z,v.x);
   Near(maxR,Math.Abs(expected.rightShoulder),.0005f,"Final right shoulder must match sampled amplitude "+hero+" "+direction);
   if(hero!=HeroId.Kaelon)Near(maxL,Math.Abs(expected.leftShoulder),.0005f,"Final left shoulder must match sampled amplitude "+hero+" "+direction);
   if(hero!=HeroId.Nyra)Check(maxR>2,"Visible right arm gait amplitude absent");
   csv.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:F6},{5:F6},{6:F6},{7:F6}",hero,native?"native_nerok_source_rest":"rotated_synthetic_rest",direction,run,maxL,maxR,maxEL,maxER));
  }}
 static void Sampler(){foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))foreach(float forward in new[]{-1f,0f,1f})for(int i=0;i<32;i++){
   float p=i*Mathf.PI*2/32;var a=HeroUpperBodyGait.Sample(hero,p,1,1,forward,forward==0?1:0);var z=HeroUpperBodyGait.Sample(hero,p,0,1,forward,1);
   Near(z.leftShoulder,0,0,"Zero-weight shoulder");Near(z.leftElbow,0,0,"Zero-weight elbow");Near(z.hipYaw,0,0,"Zero-weight torso");
   Check(a.hipYaw*a.chestYaw<=.00001f,"Chest and hip counterphase");Check(a.leftElbow>=0&&a.rightElbow>=0,"Elbow hyperextension");
   if(hero==HeroId.Nyra)Near(Math.Abs(a.leftShoulder)+Math.Abs(a.rightShoulder)+a.leftElbow+a.rightElbow,0,0,"Rifle exclusions");
   if(hero==HeroId.Kaelon)Near(Math.Abs(a.leftShoulder)+a.leftElbow,0,0,"Shield exclusions");
   var opposite=HeroUpperBodyGait.Sample(hero,p+Mathf.PI,1,1,forward,forward==0?1:0);Near(a.leftShoulder,-opposite.leftShoulder,.0001f,"Opposite stride arms");
  }}
 static void Lifecycle(){foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))foreach(bool native in new[]{false,true}){
   Rig r=Make(hero,native);r.motion.GaitPhase=0;EvaluateAuthoredFrame(r);Quaternion[] stable=new Quaternion[16];for(int i=0;i<16;i++)stable[i]=r.bones[i].localRotation;
   for(int repeat=0;repeat<720;repeat++){Time.time+=Time.deltaTime;EvaluateAuthoredFrame(r);for(int i=0;i<16;i++)SameQ(stable[i],r.bones[i].localRotation,"Cumulative drift "+hero+" "+i);}
   Time.deltaTime=0;for(int repeat=0;repeat<50;repeat++){EvaluateAuthoredFrame(r);for(int i=0;i<16;i++)SameQ(stable[i],r.bones[i].localRotation,"Paused repeat drift "+hero+" "+i);}Time.deltaTime=1f/60;
   Quaternion shieldL=r.bones[4].rotation,shieldWrist=r.bones[6].rotation;r.motion.LocomotionWeight=0;EvaluateAuthoredFrame(r);
   if(hero!=HeroId.Nyra){SameQ(r.rest[7],r.bones[7].localRotation,"Weight zero right rest");SameQ(r.rest[8],r.bones[8].localRotation,"Weight zero right elbow rest");}
   if(hero==HeroId.Kaelon){SameQ(shieldL,r.bones[4].rotation,"Shield independent of gait");SameQ(shieldWrist,r.bones[6].rotation,"Shield wrist independent of gait");}
   else if(hero!=HeroId.Nyra){SameQ(r.rest[4],r.bones[4].localRotation,"Weight zero left rest");SameQ(r.rest[5],r.bones[5].localRotation,"Weight zero left elbow rest");}
   r.motion.PlanarSpeed=0;for(int i=0;i<4;i++)r.bones[i].localRotation=r.rest[i]*Quaternion.Euler(2+i,3-i,-1);Quaternion[] body=new Quaternion[4];for(int i=0;i<4;i++)body[i]=r.bones[i].localRotation;
   EvaluateAuthoredFrame(r);for(int i=0;i<4;i++)SameQ(body[i],r.bones[i].localRotation,"Idle body was erased "+i);
   r.motion.PlanarSpeed=6;r.motion.LocomotionWeight=1;r.motion.GroundingSuspended=true;EvaluateAuthoredFrame(r);
   if(hero!=HeroId.Nyra)SameQ(r.rest[7],r.bones[7].localRotation,"Suspended grounding applied arm gait");r.motion.GroundingSuspended=false;
   r.motion.AbilityActionActive=true;for(int i=0;i<16;i++)r.bones[i].localRotation=r.rest[i]*Quaternion.Euler(i*.3f,-i*.2f,i*.1f);Quaternion[] ability=new Quaternion[16];for(int i=0;i<16;i++)ability[i]=r.bones[i].localRotation;
   r.actor.transform.ClearWrites();EvaluateAuthoredFrame(r);for(int i=0;i<16;i++){SameQ(ability[i],r.bones[i].localRotation,"Ability overwritten "+i);Check(r.bones[i].rotationWrites+r.bones[i].positionWrites==0,"Ability early return wrote bones");}
   Near(PrivateFloat(r.melee,"armGaitWeight"),0,0,"Ability did not suppress gait");
   r.motion.AbilityActionActive=false;Time.time+=.20f;EvaluateAuthoredFrame(r);for(int k=0;k<10;k++){Time.time+=Time.deltaTime;EvaluateAuthoredFrame(r);}Near(PrivateFloat(r.melee,"armGaitWeight"),1,.0001f,"Gait recovery failed");
   r.motion.ArmActionActive=true;float before=PrivateFloat(r.melee,"armGaitWeight");Time.time+=Time.deltaTime;EvaluateAuthoredFrame(r);Check(PrivateFloat(r.melee,"armGaitWeight")<before,"Arm action fade failed");
   for(int k=0;k<10;k++){Time.time+=Time.deltaTime;EvaluateAuthoredFrame(r);}Near(PrivateFloat(r.melee,"armGaitWeight"),0,.0001f,"Arm action never fades to zero");
   if(hero!=HeroId.Nyra)SameQ(r.rest[7],r.bones[7].localRotation,"Arm action did not return guard");
   r.motion.ArmActionActive=false;for(int k=0;k<10;k++){Time.time+=Time.deltaTime;EvaluateAuthoredFrame(r);}Near(PrivateFloat(r.melee,"armGaitWeight"),1,.0001f,"Arm action recovery failed");
  }}
 static void Solver(){Rig r=Make(HeroId.Nerok,true);Transform upper=r.bones[7],elbow=r.bones[8],hand=r.bones[9];float length=Vector3.Distance(upper.position,elbow.position)+Vector3.Distance(elbow.position,hand.position);
  Vector3 origin=upper.position,point=origin+r.model.TransformDirection(new Vector3(.10f,-.55f,.24f))*length;
  Quaternion wrist=hand.rotation;CombatMotion.Solve(upper,elbow,hand,point,elbow.position);SameQ(wrist,hand.rotation,"Original solver wrist preservation");
  Check(!float.IsNaN(hand.position.x)&&Vector3.Distance(hand.position,point)<.05f,"Original solver FK reach under nonuniform scale");
 }
 static void Steering(){foreach(HeroId hero in Enum.GetValues(typeof(HeroId))){var previous=HeroUpperBodyGait.Sample(hero,0,1,1,1,0);
   for(int degrees=1;degrees<=360;degrees++){float angle=degrees*Mathf.Deg2Rad;var current=HeroUpperBodyGait.Sample(hero,0,1,1,Mathf.Cos(angle),Mathf.Sin(angle));
    Check(Math.Abs(current.leftShoulder-previous.leftShoulder)<.55f,"Steering threshold snapped left arm");
    Check(Math.Abs(current.rightShoulder-previous.rightShoulder)<.55f,"Steering threshold snapped right arm");previous=current;}
  }}
 static void DirectGaitRepeat(){foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))foreach(bool native in new[]{false,true}){
   Rig r=Make(hero,native);r.melee.EvaluatePose();var initial=new Quaternion[16];for(int i=0;i<16;i++)initial[i]=r.bones[i].localRotation;
   Time.deltaTime=0;for(int repeat=0;repeat<100;repeat++){r.melee.EvaluatePose();foreach(int i in new[]{4,5,6,7,8,9}){
     if(hero==HeroId.Kaelon&&i<=6)continue;SameQ(initial[i],r.bones[i].localRotation,"Gait chains drift without Authored rebase "+hero+" "+i);}}
  }}
 static void Main(string[] args){fixture=args[0];csv=new StreamWriter(args[1]);csv.WriteLine("hero,rig,direction,run,max_left_shoulder_deviation_from_rest_deg,max_right_shoulder_deviation_from_rest_deg,max_left_elbow_deviation_from_rest_deg,max_right_elbow_deviation_from_rest_deg");
  Console.WriteLine("Isolated C# regression: unmodified HeroUpperBodyGait + full MeleeBasicAttackAnimation + original full CombatMotion.");
  Console.WriteLine("Substitute Unity Transform/math implemented with System.Numerics; clip library and presentation grips are stubs. No Unity, renderer or real attack-clip test.");
  try{ShieldGuardCases();Sampler();Steering();Console.WriteLine("PASS sampler: eight profiles, counterphase, exclusions, zero weight, forward/back/strafe and continuous 360-degree steering.");ProfileMatrix();Console.WriteLine("PASS final pipeline: "+cases+" evaluations, rotated synthetic axes and native Nerok source rest, parent rotation, imported 1.342/1.32 scale.");
   Lifecycle();Console.WriteLine("PASS lifecycle: drift, pause, zero weight, idle body, grounding suspend, ability early return, action fade/recovery, shield/rifle exceptions.");
   DirectGaitRepeat();Console.WriteLine("PASS repeated direct EvaluatePose: all gait-owned arm chains remain stable without Authored rebase; existing left shield IK excluded.");
   Solver();Console.WriteLine("PASS original CombatMotion.Solve: native Nerok source-rest reach and wrist preservation.");
   Console.WriteLine("ALL PASS: "+assertions+" assertions.");}
  catch(Exception e){Console.WriteLine("FAIL: "+e);Environment.ExitCode=1;}finally{csv.Dispose();}
 }
}
