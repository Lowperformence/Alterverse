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
  var template=new GameObject("R35 template");template.AddComponent<MeshRenderer>();
  foreach(string name in new[]{"Dropship_Hatch","Dropship_R35-Engine1","Dropship_R35-Engine2"}){
   var child=new GameObject(name);child.transform.SetParent(template.transform,false);child.AddComponent<MeshRenderer>();
  }
  var zero=new Material(Shader.Find("URP"));var lumen=new Material(Shader.Find("URP"));
  Resources.Assets["InsertionTransport/R35"]=template;Resources.Assets["InsertionTransport/R35_Zero"]=zero;Resources.Assets["InsertionTransport/R35_Lumen"]=lumen;
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
  CoopAuthority.ActivePlan=new CoopMatchPlan{Session=new PlannedSession{Slots=new[]{new PlannedPlayer{PlayerId="p1"},new PlannedPlayer{PlayerId="p2"},new PlannedPlayer{PlayerId="bot",IsBot=true}}}};
  var sync=new GameObject("Ready sync").AddComponent<CoopMatchStateSync>();
  var manager=Unity.Netcode.NetworkManager.Singleton;manager.ConnectedClientsIds.AddRange(new ulong[]{1,2});
  serverRoutine.MoveNext();Check(server.PhaseCode==-1&&server.OpeningActive&&Time.timeScale==0,"Server cannot finish insertion while mobile arenas are loading");
  var ready=typeof(CoopMatchStateSync).GetMethod("OnArenaReady",BindingFlags.Instance|BindingFlags.NonPublic);
  void Ready(ulong peer,uint round,string id)=>ready.Invoke(sync,new object[]{peer,new Unity.Netcode.FastBufferReader(round,new Unity.Collections.FixedString128Bytes(id))});
  Ready(9,17,"p1");Ready(1,16,"p1");Check(!CoopMatchStateSync.OpeningReady,"Spoofed and previous-round readiness cannot start a match");
  Ready(1,17,"p1");Check(!CoopMatchStateSync.OpeningReady,"Fast player must wait for the slow player");
  Ready(2,17,"p2");Check(CoopMatchStateSync.OpeningReady,"Every authenticated human ready; bots need no loading acknowledgement");
  manager.ConnectedClientsIds.Remove(2);Check(!CoopMatchStateSync.OpeningReady,"A disconnected ready peer cannot start the match");manager.ConnectedClientsIds.Add(2);
  serverRoutine.MoveNext();Check(server.PhaseCode==4,"Dedicated server starts the full insertion after readiness, without a front end");
  Call(sync,"OnDisable");Check(!CoopMatchStateSync.OpeningReady,"New worlds cannot reuse previous readiness");CoopAuthority.ActivePlan=null;
  Call(server,"OnDisable");MainMenuController.IsVisible=false;MatchEntryCurtain.AwaitingArena=false;CoopAuthority.Active=false;
  Unity.Netcode.NetworkManager.Singleton=null;
  // Exercise the actual client-ready send and the bounded server loading failure.
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;
  CoopAuthority.ActivePlan=new CoopMatchPlan{Session=new PlannedSession{Slots=new[]{new PlannedPlayer{PlayerId="p1"},new PlannedPlayer{PlayerId="p2"}}}};
  manager=new Unity.Netcode.NetworkManager{IsListening=true,IsClient=true,LocalClientId=1};Unity.Netcode.NetworkManager.Singleton=manager;
  var front=new GameObject("Front").AddComponent<DimensionsFront>();
  var clientClock=new GameObject("Loading clock").AddComponent<MatchOpening>();
  var clientSync=new GameObject("Client ready sync").AddComponent<CoopMatchStateSync>();
  typeof(CoopMatchStateSync).GetField("opening",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(clientSync,clientClock);
  var localHero=new GameObject("Local human").AddComponent<CoopHeroSlot>();localHero.IsLocalHuman=true;localHero.PlayerId="p1";
  Call(clientSync,"Update");Check(manager.CustomMessagingManager.Sent.Count==0,"A constructing client must not advertise arena readiness");
  CoopMatchStateSync.MarkLocalArenaReady();Time.unscaledTime=1;Call(clientSync,"Update");
  var sent=manager.CustomMessagingManager.Sent.Single();
  Check(sent.Name=="DF_COOP_ARENA_READY_V1"&&(uint)sent.Data[0]==17&&sent.Data[1].ToString()=="p1","Revealed client sends its round-bound human identity");
  Call(clientSync,"OnDisable");Call(clientClock,"OnDisable");localHero.IsLocalHuman=false;
  CoopAuthority.LocalHasServerAuthority=true;DedicatedServerNetworkRuntime.Requested=true;
  manager=new Unity.Netcode.NetworkManager{IsListening=true,IsServer=true};manager.ConnectedClientsIds.AddRange(new ulong[]{1,2});Unity.Netcode.NetworkManager.Singleton=manager;
  var timeoutClock=new GameObject("Timed out opening").AddComponent<MatchOpening>();
  var timeoutSync=new GameObject("Timed out sync").AddComponent<CoopMatchStateSync>();
  typeof(CoopMatchStateSync).GetField("opening",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(timeoutSync,timeoutClock);
  RankedMatchReporter.Instance=new RankedMatchReporter{Registered=true,MatchId="loading-failure"};
  Time.unscaledTime=100;Call(timeoutSync,"Update");
  Check(manager.ConnectedClientsIds.Count==0&&EdgegapDedicatedServerLifecycle.Stopped&&RankedMatchReporter.Instance.Voids==1,"Loading timeout disconnects safely and voids registered ranked admission");
  Check(Time.timeScale==0&&!CoopMatchStateSync.OpeningReady,"Timed out loading never releases gameplay");
  Call(timeoutSync,"OnDisable");Call(timeoutClock,"OnDisable");
  DedicatedServerNetworkRuntime.Requested=false;RankedMatchReporter.Instance=null;CoopAuthority.Active=false;CoopAuthority.ActivePlan=null;Unity.Netcode.NetworkManager.Singleton=null;
  // The selected real recording replaces all four original synthetic cues.
  var audio=new GameObject("Audio").AddComponent<InsertionTransportAudio>();audio.Prepare();audio.Prepare();
  var voices=audio.GetComponentsInChildren<AudioSource>();int loads=Resources.Loads;
  audio.Advance(.1f);Check(voices.Length==1&&voices[0].plays==1&&!voices[0].loop,"Exactly one unlooped recording; Prepare is idempotent");
  Check(voices[0].clip.name.EndsWith("R35Arrival")&&voices[0].pitch==1f,"Selected recording retains its original pitch");
  audio.Advance(1f);audio.Advance(1.2f);audio.Advance(2f);audio.Advance(.8f);audio.Advance(2f);
  Check(voices[0].plays==1&&voices[0].cues.Count==0,"Network corrections do not replay the approach or add synthetic cues");
  PlayerProfile.MasterVolume=.5f;PlayerProfile.SfxVolume=0;audio.Advance(2.5f);
  Check(voices[0].volume==0,"Live SFX mute reaches the recording");
  PlayerProfile.SfxVolume=.6f;audio.Advance(2.5f);
  Check(voices[0].volume>0&&voices[0].volume<.3f,"Recording respects master and SFX gains");
  MatchClock.Set(PauseReason.Background,true);audio.Advance(2.5f);
  Check(voices[0].paused,"Background pauses the recording");
  MatchClock.Set(PauseReason.Background,false);audio.Advance(3f);
  Check(!voices[0].paused&&voices[0].plays==1&&voices[0].pitch==1f,"Resume keeps the original recording and pitch");
  Check(Resources.Loads==loads,"Flight does not load audio assets");
  audio.Advance(3.9f);Check(voices[0].volume<.02f,"Departure fades continuously before GO");
  audio.Advance(4f);Check(!voices[0].isPlaying,"Recording stops at GO");
  var lateAudio=new GameObject("Late audio").AddComponent<InsertionTransportAudio>();lateAudio.Prepare();lateAudio.Advance(2.4f);
  Check(lateAudio.GetComponentsInChildren<AudioSource>()[0].time>3f,"Late client seeks past the earlier approach");
  Call(lateAudio,"OnDisable");Check(!lateAudio.GetComponentsInChildren<AudioSource>()[0].isPlaying,"Exit stops the recording");
  PlayerProfile.MasterVolume=PlayerProfile.SfxVolume=1;
  // Exercise the real R35 adapter with a controlled imported-prefab hierarchy.
  var r35=new GameObject("R35 runtime").AddComponent<InsertionTransportModel>();r35.Build(1);
  Check(r35.transform.Find("R35 / DIMENSIONS FRONT")!=null,"Imported resource prefab replaces the old procedural model");
  Check(r35.GetComponentsInChildren<Renderer>(true).All(r=>ReferenceEquals(r.sharedMaterial,lumen)),"Lumen uses a shared separate game material");
  Transform imported=r35.transform.Find("R35 / DIMENSIONS FRONT");
  Check(Math.Abs(imported.localScale.x-1.05f)<.001f,"R35 fits the spawn at a uniform scale");
  int r35Meshes=Mesh.Created,r35Materials=Material.Created,preparedLoads=Resources.Loads,preparedObjects=GameObject.All.Count;
  r35.Build(1);r35.Pose(1f,1.5f);
  var engine=imported.Find("Dropship_R35-Engine1");var hatch=imported.Find("Dropship_Hatch");
  Vector3 nozzle=engine.localRotation*new Vector3(0f,0f,-1f);
  Check(nozzle.y<-.95f,"Hover rotates the actual engine nozzles downward");
  Check((hatch.localRotation*new Vector3(0f,0f,-1f)).y<-.7f,"Cargo hatch opens during delivery");
  r35.Pose(1f,4f);
  Check(Math.Abs((engine.localRotation*new Vector3(0f,0f,-1f)).y)<.001f,"Departure restores forward thrust");
  Check(Math.Abs((hatch.localRotation*new Vector3(0f,0f,-1f)).y)<.001f,"Hatch closes for departure");
  Check(Mesh.Created==r35Meshes&&Material.Created==r35Materials&&Resources.Loads==preparedLoads&&GameObject.All.Count==preparedObjects,"R35 poses allocate no meshes/materials/objects and load no resources");
  var zeroShip=new GameObject("Zero runtime").AddComponent<InsertionTransportModel>();zeroShip.Build(0);
  Check(zeroShip.GetComponentsInChildren<Renderer>(true).All(r=>ReferenceEquals(r.sharedMaterial,zero)),"Zero has its own shared team material");
  Check(template.GetComponent<Renderer>().sharedMaterial==null&&template.transform.localScale.x==1f,"Imported source prefab remains untouched");
  Resources.Assets.Remove("InsertionTransport/R35");
  bool missingFailed=false;try{new GameObject("Missing model").AddComponent<InsertionTransportModel>().Build(0);}catch(InvalidOperationException){missingFailed=true;}
  Check(missingFailed,"A missing R35 reports a preparation failure instead of silently restoring the old aircraft");
  Console.WriteLine($"PASS: {checks} insertion/R35/audio/opening checks (managed fixtures; no native rendering).");
 }
}
