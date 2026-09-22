using Alterverse.AI;using Alterverse.CameraSystem;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Match;using Alterverse.Multiplayer;using Alterverse.UI;using Alterverse.Visuals;using UnityEngine;
using CombatFeedbackComponent=Alterverse.Combat.CombatFeedback;using FrontHudComponent=Alterverse.Match.DimensionsFrontHUD;
namespace Alterverse.Prototype
{
public sealed class PrototypeBootstrap:MonoBehaviour
{
private static readonly Color GameplayVoidColor=new(.004f,.006f,.012f,1f);
private const int MenuShowcaseLayer=31;

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
[SerializeField]private bool buildOnStart=true;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<PrototypeBootstrap>()==null)new GameObject("AV_PrototypeBootstrap").AddComponent<PrototypeBootstrap>();}private System.Collections.IEnumerator Start(){if(!buildOnStart)yield break;yield return null;while(MainMenuController.IsVisible)yield return null;BuildPrototype();}
public System.Collections.IEnumerator RestartPrototype()
{
MatchClock.Set(PauseReason.Transition,true);
try
{
PrepareTransitionFrame();
GameObject previous=GameObject.Find("DF_MatchWorld");
if(previous!=null){previous.SetActive(false);Destroy(previous);}
yield return null;
BuildPrototype();
}
finally{MatchClock.Set(PauseReason.Transition,false);}
}
public void BuildPrototype()
{
if(GameObject.Find("AV_Player")!=null)return;

bool coop=CoopAuthority.Active&&CoopAuthority.ActivePlan?.Session!=null;
CoopMatchPlan coopPlan=coop?CoopAuthority.ActivePlan:null;
int localSlot=coop?CoopRosterBuilder.FindLocalHumanSlot(coopPlan):-1;
if(coop&&localSlot<0){Debug.LogError("DIMENSIONS FRONT CO-OP: local human slot missing.");return;}

HeroId selected=coop?coopPlan.Session.Slots[localSlot].Hero:PlayerHeroSelection.Selected;
if(coop)
{
ArenaVariantRuntime.BeginCoopMatch(coopPlan.Arena);
BotDifficultySettings.BeginMatchOverride(coopPlan.Difficulty);
PlayerHeroSelection.Select(selected);
}
else ArenaVariantRuntime.BeginMatch();

WingMatchRewardBonus.BeginMatch(selected);
var existing=new System.Collections.Generic.HashSet<GameObject>(gameObject.scene.GetRootGameObjects());
GameObject world=new GameObject("DF_MatchWorld");

BuildArena();

Vector3 playerSpawn=coop?CoopRosterBuilder.SafeSpawn(0,localSlot):new Vector3(-32,1,-4);
GameObject player=CreateLocalPlayer(selected,playerSpawn,!coop||CoopAuthority.LocalIsHost);
Health ph=player.GetComponent<Health>();

SpawnHealingZone zeroZone=GameObject.Find("AV_ZeroSpawnZone")?.GetComponent<SpawnHealingZone>();
if(zeroZone!=null){zeroZone.InitializeTeam(0);zeroZone.Initialize(ph);}
SpawnHealingZone lumenZone=GameObject.Find("AV_LumenSpawnZone")?.GetComponent<SpawnHealingZone>();
if(lumenZone!=null)lumenZone.InitializeTeam(1);

UnityEngine.Camera cam=UnityEngine.Camera.main;
if(cam==null){GameObject cameraObject=new("Main Camera");cameraObject.tag="MainCamera";cam=cameraObject.AddComponent<UnityEngine.Camera>();}
cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=GameplayVoidColor;
cam.cullingMask=~(1<<MenuShowcaseLayer);
MobaCamera mc=cam.GetComponent<MobaCamera>()??cam.gameObject.AddComponent<MobaCamera>();mc.SetTarget(player.transform);

new GameObject("AV_HUD").AddComponent<PrototypeHUD>().Initialize(ph,null,null);
new GameObject("DF_TargetIndicator").AddComponent<TargetIndicatorHUD>();
new GameObject("DF_HeroTeamMarkers").AddComponent<HeroTeamMarkers>();

if(coop)
{
if(!CoopRosterBuilder.Build(player,coopPlan))
{
Debug.LogError("DIMENSIONS FRONT CO-OP: deterministic roster build failed.");
return;
}
new GameObject("DF_CoopHeroStateSync").AddComponent<CoopHeroStateSync>();
new GameObject("DF_CoopMatchStateSync").AddComponent<CoopMatchStateSync>();
new GameObject("DF_CoopWorldEventSync").AddComponent<CoopWorldEventSync>();
new GameObject("DF_CoopWorldStateSync").AddComponent<CoopWorldStateSync>();
new GameObject("DF_CoopMatchResultSync").AddComponent<CoopMatchResultSync>();
}
else new GameObject("AV_5v5Roster").AddComponent<FiveVsFiveTestRoster>();

new GameObject("AV_DimensionsFront").AddComponent<DimensionsFront>();
new GameObject("AV_DimensionsFrontCombatBridge").AddComponent<DimensionsFrontCombatBridge>();
new GameObject("AV_DimensionsFrontVisual").AddComponent<DimensionsFrontVisual>();
new GameObject("AV_DimensionsFrontHUD").AddComponent<FrontHudComponent>();
new GameObject("DF_HotZone").AddComponent<HotZoneDirector>();
new GameObject("DF_HotZoneHUD").AddComponent<HotZoneHUD>();
new GameObject("DF_HotZoneTankHUD").AddComponent<HotZoneTankHUD>();
new GameObject("DF_OvertimePresentation").AddComponent<OvertimePresentation>();
new GameObject("DF_DimensionPlanetDirector").AddComponent<DimensionPlanetDirector>();
new GameObject("DF_PlanetHUD").AddComponent<PlanetHUD>();
new GameObject("DF_SpaceBomberDirector").AddComponent<SpaceBomberDirector>();
new GameObject("AV_MatchTelemetry").AddComponent<MatchTelemetry>();
new GameObject("AV_MatchScoreboard").AddComponent<MatchScoreboard>();
new GameObject("AV_MatchOpening").AddComponent<MatchOpening>();

foreach(GameObject created in gameObject.scene.GetRootGameObjects())
if(created!=world&&!existing.Contains(created))created.transform.SetParent(world.transform,true);
}

private static GameObject CreateLocalPlayer(HeroId selected,Vector3 spawn,bool authoritativeRespawn)
{
GameObject player=new GameObject("AV_Player");
player.transform.position=spawn;
CharacterController cc=player.AddComponent<CharacterController>();cc.height=HeroIdentity.HeroCollisionHeight;cc.radius=HeroIdentity.HeroCollisionRadius;
player.AddComponent<HeroIdentity>().Configure(selected);
HeroCombatProfile profile=player.AddComponent<HeroCombatProfile>();profile.Configure(selected);
player.AddComponent<HeroAbilityKit>().Configure(selected);
Health ph=player.AddComponent<Health>();ph.Configure(profile.Health);
player.AddComponent<TeamMember>().Initialize(0);
player.AddComponent<HeroMotor>();
if(authoritativeRespawn)player.AddComponent<HeroRespawn>();
player.AddComponent<BasicAttack>();
player.AddComponent<PlayerHeroAbilities>();
player.AddComponent<CombatFeedbackComponent>();
player.AddComponent<StylizedHeroVisual>().Build();
return player;
}
private static void BuildArena(){CreateGround("DF_ArenaFloor",new Vector3(0,-.12f,0),new Vector3(84,.2f,72),Color.gray);CreateBaseCore(new Vector3(-36,0f,0),StylizedArtDirection.ZeroEnergy,"DF_ZeroRealityCore",true,0);CreateBaseCore(new Vector3(36,0f,0),StylizedArtDirection.LumenEnergy,"DF_LumenRealityCore",false,1);CreateSpawnZone(new Vector3(-27.5f,.14f,0),StylizedArtDirection.ZeroEnergy,"AV_ZeroSpawnZone");CreateSpawnZone(new Vector3(27.5f,.14f,0),StylizedArtDirection.LumenEnergy,"AV_LumenSpawnZone");HealingGuardMech.Spawn(0,new Vector3(-27.5f,.14f,0));HealingGuardMech.Spawn(1,new Vector3(27.5f,.14f,0));CreateDefenseBunkers();CreateDefenseTrenches();new GameObject("DF_TrenchReinforcements").AddComponent<TrenchReinforcementDirector>();CreateCentralHealing();new GameObject("DF_ProductionLunarGround").AddComponent<ProductionLunarGround>();new GameObject("DF_CosmicArenaBackdrop").AddComponent<CosmicArenaBackdrop>();}
private static void CreateSpawnZone(Vector3 p,Color c,string n){GameObject g=new GameObject(n);g.transform.position=p;g.AddComponent<SpawnHealingZone>();g.AddComponent<HealingZoneMarker>();}private static void CreateDefenseBunkers(){const float x=26.25f,z=21.5f;DefenseBunker.Spawn(0,new Vector3(-x,0,z),"Left");DefenseBunker.Spawn(0,new Vector3(-x,0,-z),"Right");DefenseBunker.Spawn(1,new Vector3(x,0,-z),"Left");DefenseBunker.Spawn(1,new Vector3(x,0,z),"Right");}private static void CreateDefenseTrenches(){const float x=25.25f,z=10.75f;DefenseTrench.Spawn(0,new Vector3(-x,0,z),"Left");DefenseTrench.Spawn(0,new Vector3(-x,0,-z),"Right");DefenseTrench.Spawn(1,new Vector3(x,0,-z),"Left");DefenseTrench.Spawn(1,new Vector3(x,0,z),"Right");}private static void CreateObjectiveZone(Vector3 p,Color c,string n){CreateVisualCylinder(n,p,new Vector3(3.2f,.025f,3.2f),c);for(int i=0;i<6;i++){float a=i*60*Mathf.Deg2Rad;CreateShard(p+new Vector3(Mathf.Cos(a)*2.5f,.55f,Mathf.Sin(a)*2.5f),c,.4f,true);}}private static void CreateCentralHealing(){GameObject heal=new GameObject("DF_CentralHealingPickup");heal.transform.position=new Vector3(0,.12f,0);heal.AddComponent<CentralHealingPickup>();}private static void CreateBaseCore(Vector3 p,Color c,string n,bool zero,int team){GameObject g=new GameObject(n);g.layer=GameplayCollision.Structures;g.transform.position=p;CapsuleCollider body=g.AddComponent<CapsuleCollider>();body.radius=.9f;body.height=2.6f;body.center=Vector3.up*1.3f;Health h=g.AddComponent<Health>();h.Configure(5000,true);g.AddComponent<RealityCore>().Initialize(team);g.AddComponent<RealityCoreShell>().Build(zero);g.AddComponent<ProductionBaseEnvironment>().Build(zero);}private static void CreateGround(string n,Vector3 p,Vector3 s,Color c){GameObject g=new GameObject(n);g.layer=GameplayCollision.Ground;g.transform.position=p;Mesh mesh=GameplayCollision.CreateRoundedPrismMesh(Vector3.zero,s.x,s.y,s.z,4f,8);MeshCollider floor=g.AddComponent<MeshCollider>();floor.sharedMesh=mesh;floor.convex=true;g.AddComponent<GameplayCollisionMeshOwner>().Own(mesh);}private static void CreateShard(Vector3 p,Color c,float size,bool animated){GameObject s=GameObject.CreatePrimitive(PrimitiveType.Cube);s.name="AV_RiftShard";s.transform.position=p;s.transform.localScale=new Vector3(size*.35f,size,size*.35f);s.transform.rotation=Quaternion.Euler(20,45,25);DisableCollider(s);SetColor(s,c);if(animated)s.AddComponent<MapAmbientVisual>();}private static void CreateVisualCylinder(string n,Vector3 p,Vector3 s,Color c){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.position=p;g.transform.localScale=s;DisableCollider(g);SetColor(g,c);}private static void CreateVisualBlock(string n,Vector3 p,Vector3 s,Color c,Vector3 r){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.position=p;g.transform.localScale=s;g.transform.rotation=Quaternion.Euler(r);DisableCollider(g);SetColor(g,c);}private static void DisableCollider(GameObject g){Collider c=g.GetComponent<Collider>();if(c!=null){c.enabled=false;Destroy(c);}}private static void SetColor(GameObject g,Color c){Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=c;}
}}
