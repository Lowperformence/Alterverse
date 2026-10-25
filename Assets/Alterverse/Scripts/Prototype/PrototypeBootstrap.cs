using Alterverse.AI;using Alterverse.CameraSystem;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Match;using Alterverse.Multiplayer;using Alterverse.UI;using Alterverse.Visuals;using Unity.Netcode;using UnityEngine;
using CombatFeedbackComponent=Alterverse.Combat.CombatFeedback;using FrontHudComponent=Alterverse.Match.DimensionsFrontHUD;
namespace Alterverse.Prototype
{
public sealed class PrototypeBootstrap:MonoBehaviour
{
private static readonly Color GameplayVoidColor=new(.004f,.006f,.012f,1f);
private const int MenuShowcaseLayer=31;
private UnityEngine.Camera pendingArenaCamera;
private GameObject pendingArenaWorld;
private int revealArenaFrame;

/// <summary>
/// Prevents the scene camera from exposing the underlying scene/default clear color while
/// the menu is being hidden and the procedural battlefield is constructed.
/// </summary>
public static void PrepareTransitionFrame()
{
foreach(UnityEngine.Camera camera in FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Exclude))
{
if(camera==null||camera.cameraType!=CameraType.Game||camera.targetTexture!=null)continue;
camera.clearFlags=CameraClearFlags.SolidColor;
camera.backgroundColor=GameplayVoidColor;
camera.cullingMask=0;
}
}
public static void RestoreTransitionFrame()
{
foreach(UnityEngine.Camera camera in FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Exclude))
{
if(camera==null||camera.cameraType!=CameraType.Game||camera.targetTexture!=null)continue;
if(camera.cullingMask==0)camera.cullingMask=~(1<<MenuShowcaseLayer);
}
}
[SerializeField]private bool buildOnStart=true;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void AutoCreate()
{
// The bootstrap coroutine yields before building the arena. Hide the scene's
// default skybox before that first yield can expose a blue startup frame.
if(GameObject.Find("DF_MatchWorld")==null)PrepareTransitionFrame();
if(FindAnyObjectByType<PrototypeBootstrap>()==null)new GameObject("AV_PrototypeBootstrap").AddComponent<PrototypeBootstrap>();
}
private System.Collections.IEnumerator Start(){if(!buildOnStart)yield break;yield return null;while(MainMenuController.IsVisible||OnlineMatchLoadingScreen.IsVisible||MatchExit.Leaving)yield return null;BuildPrototype();}

private void LateUpdate()
{
if(pendingArenaCamera==null||Time.frameCount<revealArenaFrame)return;
if(!HotZoneTank.FinishMatchPreparation())return;
// Terrain, lighting and the panorama are built in Start; planet themes run in
// Update. Reveal only after the next frame has run those initialization steps.
if(pendingArenaWorld!=null&&pendingArenaWorld.activeInHierarchy)
{
pendingArenaCamera.cullingMask=~(1<<MenuShowcaseLayer);
OnlineMatchLoadingScreen.Complete();
}
else
{
Debug.LogError("DIMENSIONS FRONT: arena disappeared before camera reveal for "+ArenaVariantRuntime.Current);
RecoverFailedBuild();
}
pendingArenaCamera=null;
pendingArenaWorld=null;
}
public System.Collections.IEnumerator RestartPrototype()
{
MatchClock.Set(PauseReason.Transition,true);
try
{
if(!IsServerOnlyMatch())PrepareTransitionFrame();
GameObject previous=GameObject.Find("DF_MatchWorld");
if(previous!=null){previous.SetActive(false);Destroy(previous);}
yield return null;
BuildPrototype();
}
finally{MatchClock.Set(PauseReason.Transition,false);}
}
public bool BuildPrototype()
{
if(GameObject.Find("AV_Player")!=null||GameObject.Find("DF_MatchWorld")!=null)return true;

bool coop=CoopAuthority.Active&&CoopAuthority.ActivePlan?.Session!=null;
CoopMatchPlan coopPlan=coop?CoopAuthority.ActivePlan:null;
int localSlot=coop?CoopRosterBuilder.FindLocalHumanSlot(coopPlan):-1;
bool serverOnly=coop&&localSlot<0&&IsServerOnlyMatch();
if(coop&&localSlot<0&&!serverOnly){Debug.LogError("DIMENSIONS FRONT CO-OP: local human slot missing.");RecoverFailedBuild();return false;}

MatchPlayerSlot localSlotData=coop&&!serverOnly?coopPlan.Session.Slots[localSlot]:null;
int localTeam=localSlotData?.Team??0;
HeroId selected=localSlotData?.Hero??PlayerHeroSelection.Selected;
if(coop)
{
ArenaVariantRuntime.BeginCoopMatch(coopPlan.Arena);
BotDifficultySettings.BeginMatchOverride(coopPlan.Difficulty);
if(!serverOnly)PlayerHeroSelection.Select(selected);
}
else ArenaVariantRuntime.BeginMatch();

if(!serverOnly)WingMatchRewardBonus.BeginMatch(selected);
var existing=new System.Collections.Generic.HashSet<GameObject>(gameObject.scene.GetRootGameObjects());
GameObject world=new GameObject("DF_MatchWorld");
bool completed=false;
string stage="arena and base guards";
try
{
BuildArena(ref stage);

stage="local hero";
GameObject player=null;
Health ph=null;
if(!serverOnly)
{
Vector3 playerSpawn=coop?CoopRosterBuilder.SafeSpawn(localTeam,localSlot):ArenaLayout.HeroSpawn(0,0,MatchSession.TeamSize);
player=CreateLocalPlayer(selected,playerSpawn,!coop||CoopAuthority.LocalIsHost,localTeam);
ph=player.GetComponent<Health>();
}

SpawnHealingZone zeroZone=GameObject.Find("AV_ZeroSpawnZone")?.GetComponent<SpawnHealingZone>();
if(zeroZone!=null){zeroZone.InitializeTeam(0);zeroZone.Initialize(!serverOnly&&localTeam==0?ph:null);}
SpawnHealingZone lumenZone=GameObject.Find("AV_LumenSpawnZone")?.GetComponent<SpawnHealingZone>();
if(lumenZone!=null){lumenZone.InitializeTeam(1);lumenZone.Initialize(!serverOnly&&localTeam==1?ph:null);}

if(!serverOnly)
{
UnityEngine.Camera cam=UnityEngine.Camera.main;
if(cam==null){GameObject cameraObject=new("Main Camera");cameraObject.tag="MainCamera";cam=cameraObject.AddComponent<UnityEngine.Camera>();}
cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=GameplayVoidColor;
cam.cullingMask=0;
MobaCamera mc=cam.GetComponent<MobaCamera>()??cam.gameObject.AddComponent<MobaCamera>();mc.SetTarget(player.transform);

new GameObject("AV_HUD").AddComponent<PrototypeHUD>().Initialize(ph,null,null);
if(FindAnyObjectByType<InMatchSettingsMenu>()==null)new GameObject("DF_InMatchSettings").AddComponent<InMatchSettingsMenu>();
new GameObject("DF_TargetIndicator").AddComponent<TargetIndicatorHUD>();
new GameObject("DF_HeroTeamMarkers").AddComponent<HeroTeamMarkers>();
}

stage="team roster";
if(coop)
{
bool rosterBuilt=serverOnly?CoopRosterBuilder.BuildServer(coopPlan):CoopRosterBuilder.Build(player,coopPlan);
if(!rosterBuilt)
{
Debug.LogError(serverOnly
?"DIMENSIONS FRONT SERVER: authoritative roster build failed."
:"DIMENSIONS FRONT CO-OP: deterministic roster build failed.");
return false;
}
new GameObject("DF_CoopHeroStateSync").AddComponent<CoopHeroStateSync>();
new GameObject("DF_CoopMatchStateSync").AddComponent<CoopMatchStateSync>();
new GameObject("DF_CoopWorldEventSync").AddComponent<CoopWorldEventSync>();
new GameObject("DF_CoopWorldStateSync").AddComponent<CoopWorldStateSync>();
new GameObject("DF_CoopMatchResultSync").AddComponent<CoopMatchResultSync>();
}
else new GameObject("AV_4v4Roster").AddComponent<FiveVsFiveTestRoster>();

stage="match directors and HUD";
new GameObject("AV_DimensionsFront").AddComponent<DimensionsFront>();
new GameObject("AV_DimensionsFrontCombatBridge").AddComponent<DimensionsFrontCombatBridge>();
new GameObject("AV_DimensionsFrontVisual").AddComponent<DimensionsFrontVisual>();
if(!serverOnly)new GameObject("AV_DimensionsFrontHUD").AddComponent<FrontHudComponent>();
new GameObject("DF_HotZone").AddComponent<HotZoneDirector>();
if(!serverOnly)
{
new GameObject("DF_HotZoneHUD").AddComponent<HotZoneHUD>();
new GameObject("DF_HotZoneTankHUD").AddComponent<HotZoneTankHUD>();
new GameObject("DF_OvertimePresentation").AddComponent<OvertimePresentation>();
}
new GameObject("DF_DimensionPlanetDirector").AddComponent<DimensionPlanetDirector>();
if(!serverOnly)new GameObject("DF_PlanetHUD").AddComponent<PlanetHUD>();
new GameObject("DF_SpaceBomberDirector").AddComponent<SpaceBomberDirector>();
new GameObject("AV_MatchTelemetry").AddComponent<MatchTelemetry>();
new GameObject("AV_MatchScoreboard").AddComponent<MatchScoreboard>();
if(!serverOnly)new GameObject("AV_MatchOpening").AddComponent<MatchOpening>();

stage="Hot Zone monster and Magic Aura preparation";
HotZoneTank.PrepareForMatch(world.transform);
if(serverOnly)HotZoneTank.FinishMatchPreparation(true);

completed=true;
if(!serverOnly)
{
pendingArenaCamera=UnityEngine.Camera.main;
pendingArenaWorld=world;
revealArenaFrame=Time.frameCount+1;
}
return true;
}
catch(System.Exception error)
{
Debug.LogError("DIMENSIONS FRONT: "+ArenaVariantRuntime.Current+" startup failed during "+stage);
Debug.LogException(error);
return false;
}
finally
{
// Even a failed roster/arena build must own every newly-created root so it cannot
// leak actors, HUDs or scenery into a later attempt. Destroy is deferred: hide now.
foreach(GameObject created in gameObject.scene.GetRootGameObjects())
if(created!=world&&!existing.Contains(created))created.transform.SetParent(world.transform,true);
if(!completed)
{
world.SetActive(false);Destroy(world);
pendingArenaCamera=null;pendingArenaWorld=null;
if(!serverOnly)RecoverFailedBuild();
}
}
}

private static void RecoverFailedBuild()
{
// Use the same session/network cleanup as a normal exit. Offline recovery finishes
// synchronously; network recovery leaves through its owner before showing the menu.
RestoreTransitionFrame();
_ = MatchExit.ReturnToMenuAsync("Could not load "+ArenaVariantRuntime.Current+". Please try again or choose another map.");
}

private static bool IsServerOnlyMatch()
{
NetworkManager manager=NetworkManager.Singleton;
return manager!=null&&manager.IsListening&&manager.IsServer&&!manager.IsClient&&CoopAuthority.LocalHasServerAuthority;
}

private static GameObject CreateLocalPlayer(HeroId selected,Vector3 spawn,bool authoritativeRespawn,int team)
{
GameObject player=new GameObject("AV_Player");
player.transform.position=spawn;
CharacterController cc=player.AddComponent<CharacterController>();cc.height=HeroIdentity.HeroCollisionHeight;cc.radius=HeroIdentity.HeroCollisionRadius;
player.AddComponent<HeroIdentity>().Configure(selected);
HeroCombatProfile profile=player.AddComponent<HeroCombatProfile>();profile.Configure(selected);
player.AddComponent<HeroAbilityKit>().Configure(selected);
Health ph=player.AddComponent<Health>();ph.Configure(profile.Health);
player.AddComponent<TeamMember>().Initialize(Mathf.Clamp(team,0,1));
player.AddComponent<HeroMotor>();
if(authoritativeRespawn)player.AddComponent<HeroRespawn>();
player.AddComponent<BasicAttack>();
player.AddComponent<PlayerHeroAbilities>();
player.AddComponent<AbilityRangePreviewOverlay>();
player.AddComponent<CombatFeedbackComponent>();
player.AddComponent<StylizedHeroVisual>().Build();
return player;
}
private static void BuildArena(ref string stage)
{
stage="arena floor";
CreateGround("DF_ArenaFloor",new Vector3(0,-.12f,0),new Vector3(ArenaLayout.HalfLength*2f,.2f,ArenaLayout.HalfBreadth*2f),Color.gray);
stage="base cores and healing zones";
CreateBaseCore(new Vector3(-ArenaLayout.CoreX,0f,0),StylizedArtDirection.ZeroEnergy,"DF_ZeroRealityCore",true,0);
CreateBaseCore(new Vector3(ArenaLayout.CoreX,0f,0),StylizedArtDirection.LumenEnergy,"DF_LumenRealityCore",false,1);
CreateSpawnZone(ArenaLayout.HealingPosition(0),StylizedArtDirection.ZeroEnergy,"AV_ZeroSpawnZone");
CreateSpawnZone(ArenaLayout.HealingPosition(1),StylizedArtDirection.LumenEnergy,"AV_LumenSpawnZone");
stage="base guardian visuals";
HealingGuardMech.Spawn(0,ArenaLayout.HealingPosition(0));
HealingGuardMech.Spawn(1,ArenaLayout.HealingPosition(1));
stage="bunkers, trenches and healing pickup";
CreateDefenseBunkers();CreateDefenseTrenches();
new GameObject("DF_TrenchReinforcements").AddComponent<TrenchReinforcementDirector>();
CreateCentralHealing();
stage="ground, backdrop and obstacles";
if(ArenaVariantRuntime.IsGJ504b)new GameObject("DF_GJ504bArenaLandscape").AddComponent<GJ504bArenaLandscape>();
else if(!ArenaVariantRuntime.IsDiamondCore)new GameObject("DF_ProductionLunarGround").AddComponent<ProductionLunarGround>();
new GameObject("DF_CosmicArenaBackdrop").AddComponent<CosmicArenaBackdrop>();
if(FrontierPlanetLayout.Active)FrontierPlanetLayout.BuildObstacles(GameObject.Find("DF_MatchWorld").transform);
}
private static void CreateSpawnZone(Vector3 p,Color c,string n){GameObject g=new GameObject(n);g.transform.position=p;g.AddComponent<SpawnHealingZone>();g.AddComponent<HealingZoneMarker>();}private static void CreateDefenseBunkers()
{
for(int team=0;team<2;team++)for(int flank=0;flank<2;flank++)
DefenseBunker.Spawn(team,ArenaLayout.DefensePosition(team,true,flank),flank==0?"Left":"Right");
}
private static void CreateDefenseTrenches()
{
for(int team=0;team<2;team++)for(int flank=0;flank<2;flank++)
DefenseTrench.Spawn(team,ArenaLayout.DefensePosition(team,false,flank),flank==0?"Left":"Right");
}
private static void CreateCentralHealing(){GameObject heal=new GameObject("DF_CentralHealingPickup");heal.transform.position=new Vector3(0,.12f,0);heal.AddComponent<CentralHealingPickup>();}private static void CreateBaseCore(Vector3 p,Color c,string n,bool zero,int team){GameObject g=new GameObject(n);g.layer=GameplayCollision.Structures;g.transform.position=p;CapsuleCollider body=g.AddComponent<CapsuleCollider>();body.radius=.9f;body.height=2.6f;body.center=Vector3.up*1.3f;Health h=g.AddComponent<Health>();h.Configure(5000,true);g.AddComponent<RealityCore>().Initialize(team);g.AddComponent<RealityCoreShell>().Build(zero);g.AddComponent<ProductionBaseEnvironment>().Build(zero);}private static void CreateGround(string n,Vector3 p,Vector3 s,Color c){GameObject g=new GameObject(n);g.layer=GameplayCollision.Ground;g.transform.position=p;Mesh mesh=GameplayCollision.CreateRoundedPrismMesh(Vector3.zero,s.x,s.y,s.z,4f,8);MeshCollider floor=g.AddComponent<MeshCollider>();floor.sharedMesh=mesh;floor.convex=true;g.AddComponent<GameplayCollisionMeshOwner>().Own(mesh);}
}}


