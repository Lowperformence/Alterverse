using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.IO;
using Alterverse.Visuals;
using Alterverse.Audio;
using Alterverse.CameraSystem;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Alterverse.UI;
using Alterverse.Match;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
static class Regression {
 static int checks;
 static void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
 static void Call(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
 static (GameObject,MatchInsertionTransport,MatchOpening,Renderer[]) World(){
  var world=new GameObject("World");var root=new GameObject("Insertion");root.transform.SetParent(world.transform,false);
  var opening=new GameObject("Opening").AddComponent<MatchOpening>();
  var insertion=root.AddComponent<MatchInsertionTransport>();insertion.Prepare(opening);
  var heroes=new Renderer[8];
  for(int i=0;i<8;i++){
   var hero=new GameObject("Hero");hero.transform.SetParent(world.transform,false);hero.transform.position=new Vector3(i<4?-30:30,0,i%4-1.5f);
   hero.AddComponent<HeroIdentity>();hero.AddComponent<TeamMember>().Team=i/4;heroes[i]=hero.AddComponent<MeshRenderer>();
  }
  return(world,insertion,opening,heroes);
 }
 static void Phase(MatchOpening opening,int code,float elapsed=0f){
  string text=code switch{4=>"REALITY COLLIDE",3=>"3",2=>"2",1=>"1",0=>"GO",_=>""};
  typeof(MatchOpening).GetMethod("SetPhase",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(opening,new object[]{text,1f});
  typeof(MatchOpening).GetField("phaseElapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(opening,elapsed);
 }
 static void Main(string[] args){
  var cameraObject=new GameObject("Camera");UnityEngine.Camera.main=cameraObject.AddComponent<UnityEngine.Camera>();
  var camera=cameraObject.AddComponent<MobaCamera>();var target=new GameObject("Local target");camera.SetTarget(target.transform);
  Vector3 normalCamera=camera.transform.position;
  var (world,insertion,opening,heroes)=World();
  Check(camera.transform.position.y>normalCamera.y,"Arrival framing makes room for the complete aircraft");heroes[7].forceRenderingOff=true;
  int builtMeshes=Mesh.Created,builtMaterials=Material.Created;
  MatchInsertionTransport.PrepareForReveal(world);
  Check(heroes.All(h=>h.forceRenderingOff),"Squad hidden before first arena frame");
  foreach(int phase in new[]{4,3}){
   Phase(opening,phase,.5f);Call(insertion,"LateUpdate");
   Check(heroes.All(h=>h.forceRenderingOff),"Heroes remain aboard during approach/door opening");
  }
  Phase(opening,2);Call(insertion,"LateUpdate");
  Check(heroes.Take(7).All(h=>!h.forceRenderingOff)&&heroes[7].forceRenderingOff,"Belly delivery restores exact previous renderer visibility");
  Phase(opening,3);Call(insertion,"LateUpdate");
  Check(heroes.Take(7).All(h=>!h.forceRenderingOff),"Network correction cannot hide disembarked heroes again");
  Phase(opening,1,.6f);Call(insertion,"LateUpdate");
  Check(Mesh.Created==builtMeshes&&Material.Created==builtMaterials,"No geometry/material allocations during flight");
  Phase(opening,0);Call(insertion,"LateUpdate");
  Check(insertion.enabled,"Invisible final phase retains continuous camera return");
  Check(world.GetComponentsInChildren<InsertionTransportModel>(true).All(m=>!m.gameObject.activeSelf),"Both freighters leave at GO");
  Phase(opening,0,.5f);Call(insertion,"LateUpdate");
  Check(camera.transform.position.y>normalCamera.y&&camera.transform.position.y<normalCamera.y*1.45f,"Camera moves continuously back after departure");
  Phase(opening,0,1f);Call(insertion,"LateUpdate");
  Check(!insertion.enabled&&Math.Abs(camera.transform.position.y-normalCamera.y)<.001f,"Normal framing restored before play");
  Phase(opening,4);Call(insertion,"LateUpdate");
  Check(world.GetComponentsInChildren<InsertionTransportModel>(true).All(m=>!m.gameObject.activeSelf),"Respawn/later state cannot replay arrival");
  var (w2,i2,o2,h2)=World();MatchInsertionTransport.PrepareForReveal(w2);Call(i2,"OnDisable");
  Check(h2.All(r=>!r.forceRenderingOff),"Interrupted introduction restores all passengers");
  var (w3,i3,o3,h3)=World();Call(o3,"OnDisable");MatchInsertionTransport.PrepareForReveal(w3);
  Check(h3.All(r=>!r.forceRenderingOff)&&!i3.enabled,"Late join skips intro without hiding live heroes");
  var (w4,i4,o4,h4)=World();MatchInsertionTransport.PrepareForReveal(w4);
  Check(h4.All(r=>r.forceRenderingOff),"A new match owns a fresh insertion");
  Check(MatchInsertionTransport.PhaseProgress(4,99,0)==1,"Clamps bad duration and elapsed");
  Check(MatchInsertionTransport.PhaseProgress(-1,0,1)==0,"Unknown replicated phase waits for arrival");
  Check(MatchInsertionTransport.PhaseProgress(2,.5f,1)==2.5f,"Presentation follows authoritative phase progress");
  // Compile and exercise the real opening controller, including a dedicated server and remote snapshots.
  Check(typeof(MatchOpening).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic)==null,"Opening has no text/countdown rendering");
  var openingOnly=new GameObject("Clock").AddComponent<MatchOpening>();
  Check(openingOnly.OpeningActive&&Time.timeScale==0,"Awake pauses before actors can simulate");
  MainMenuController.IsVisible=true;
  var routine=(System.Collections.IEnumerator)typeof(MatchOpening).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(openingOnly,null);
  routine.MoveNext();Check(openingOnly.PhaseCode==-1,"Offline opening waits for menu handoff");
  MainMenuController.IsVisible=false;MatchEntryCurtain.AwaitingArena=true;routine.MoveNext();
  Check(openingOnly.PhaseCode==-1,"Opening cannot consume landing time while arena remains hidden");
  MatchEntryCurtain.AwaitingArena=false;routine.MoveNext();
  Check(openingOnly.PhaseCode==4,"Arena readiness starts invisible authoritative approach phase");
  Call(openingOnly,"OnDisable");
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;
  var remote=new GameObject("Remote clock").AddComponent<MatchOpening>();
  var remoteRoutine=(System.Collections.IEnumerator)typeof(MatchOpening).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(remote,null);
  remoteRoutine.MoveNext();remote.ApplyNetworkOpeningState(true,2,.25f,.72f);
  Check(remote.OpeningActive&&remote.PhaseCode==2,"Remote follows authoritative phase");
  Call(remote,"Update");Check(remote.PhaseElapsed>.25f,"Remote presentation interpolates in foreground");
  float before=remote.PhaseElapsed;MatchClock.Set(PauseReason.Background,true);Call(remote,"Update");
  Check(remote.PhaseElapsed==before,"Backgrounding freezes remote presentation");
  MatchClock.Set(PauseReason.Background,false);remote.ApplyNetworkOpeningState(false,0,0,0);
  Check(!remote.OpeningActive&&Time.timeScale==1,"Remote release comes from authoritative state");
  var alreadyStarted=new GameObject("Completed-before-Start clock").AddComponent<MatchOpening>();
  alreadyStarted.ApplyNetworkOpeningState(false,0,0,0);
  var completedRoutine=(System.Collections.IEnumerator)typeof(MatchOpening).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(alreadyStarted,null);
  Check(!completedRoutine.MoveNext()&&!alreadyStarted.OpeningActive,"Completed snapshot before Start cannot repause a late client");
  CoopAuthority.LocalHasServerAuthority=true;
  Unity.Netcode.NetworkManager.Singleton=new(){IsServer=true,IsClient=false,IsListening=true};
  MainMenuController.IsVisible=true;MatchEntryCurtain.AwaitingArena=true;
  var server=new GameObject("Server clock").AddComponent<MatchOpening>();
  var serverRoutine=(System.Collections.IEnumerator)typeof(MatchOpening).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
  serverRoutine.MoveNext();Check(server.PhaseCode==4,"Dedicated server never waits on visual loading");
  Call(server,"OnDisable");MainMenuController.IsVisible=false;MatchEntryCurtain.AwaitingArena=false;CoopAuthority.Active=false;
  Unity.Netcode.NetworkManager.Singleton=null;
  // Actual audio controller, controlled clip/source substitutes.
  var audio=new GameObject("Audio").AddComponent<InsertionTransportAudio>();audio.Prepare();
  var voices=audio.GetComponentsInChildren<AudioSource>();int loads=Resources.Loads;
  audio.Advance(.1f);Check(voices.Length==2&&voices[0].plays==1,"One cinematic mix starts one prepared engine loop");
  audio.Advance(1f);audio.Advance(1.2f);audio.Advance(2f);audio.Advance(.8f);audio.Advance(2f);
  Check(voices[1].cues.Count==2,"Bay release and hydraulics play once despite corrected phases");
  PlayerProfile.MasterVolume=.5f;PlayerProfile.SfxVolume=0;audio.Advance(2.5f);
  Check(voices.All(v=>v.volume==0),"Live SFX mute reaches both voices");
  PlayerProfile.SfxVolume=.6f;audio.Advance(2.5f);
  Check(voices[0].volume>0&&voices[0].volume<.3f,"Engine respects master and SFX gains");
  MatchClock.Set(PauseReason.Background,true);audio.Advance(2.5f);
  Check(voices.All(v=>v.paused),"Background pauses engine and mechanisms");
  MatchClock.Set(PauseReason.Background,false);audio.Advance(3f);
  Check(voices.All(v=>!v.paused)&&voices[0].plays==1&&voices[1].cues.Count==3,"Resume keeps loop position and departure plays once");
  Check(Resources.Loads==loads,"Flight does not load audio assets");
  audio.Advance(4f);Check(voices.All(v=>!v.isPlaying),"All lander audio stops at GO");
  var lateAudio=new GameObject("Late audio").AddComponent<InsertionTransportAudio>();lateAudio.Prepare();lateAudio.Advance(2.4f);
  Check(lateAudio.GetComponentsInChildren<AudioSource>()[1].cues.Count==0,"Late client does not replay old bay-release cues");
  Call(lateAudio,"OnDisable");Check(lateAudio.GetComponentsInChildren<AudioSource>().All(v=>!v.isPlaying),"Exit stops transport sound");
  PlayerProfile.MasterVolume=PlayerProfile.SfxVolume=1;
  var model=new GameObject("Preview").AddComponent<InsertionTransportModel>();model.Build(0);model.Pose(0,1);
  var parts=model.GetComponentsInChildren<MeshFilter>();int triangles=0;
  foreach(var part in parts){
   var m=part.sharedMesh;triangles+=m.triangles.Count/3;
   Check(m.triangles.All(n=>n>=0&&n<m.vertices.Count),"Mesh indices valid");
   for(int i=0;i<m.triangles.Count;i+=3){
    var a=m.vertices[m.triangles[i]];var b=m.vertices[m.triangles[i+1]];var c=m.vertices[m.triangles[i+2]];
    var ab=new System.Numerics.Vector3(b.x-a.x,b.y-a.y,b.z-a.z);var ac=new System.Numerics.Vector3(c.x-a.x,c.y-a.y,c.z-a.z);
    Check(System.Numerics.Vector3.Cross(ab,ac).LengthSquared()>1e-10,"No degenerate triangles");
   }
  }
  Check(triangles<2500,"Mobile geometry budget below 2500 triangles per ship");
  var data=parts.Select(part=>new{ name=part.gameObject.name,vertices=part.sharedMesh.vertices.Select(v=>{var p=part.transform.TransformPoint(v);return new[]{p.x,p.y,p.z};}),triangles=part.sharedMesh.triangles,color=new[]{part.GetComponent<Renderer>().sharedMaterial.color.r,part.GetComponent<Renderer>().sharedMaterial.color.g,part.GetComponent<Renderer>().sharedMaterial.color.b}});
  if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(data));
  Console.WriteLine($"PASS: {checks} insertion/geometry checks; {triangles} triangles and {parts.Length} mesh parts per ship (managed fixtures; no native rendering).");
 }
}
