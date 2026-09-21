using Alterverse.Combat;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.Match
{
public sealed class DimensionPlanetDirector:MonoBehaviour
{
public static event System.Action<Vector3> PlanetArrived;
public static event System.Action<int> NetworkPlanetSpawned;
private const float FirstSpawn=120f,SecondSpawn=405f,ThirdSpawn=510f,Warning=15f;private static DimensionPlanetDirector active;private float elapsed;private bool warnedFirst,warnedSecond,warnedThird,spawnedFirst,spawnedSecond,spawnedThird;private DimensionsFront front;public static string Announcement{get;private set;}public static float AnnouncementUntil{get;private set;}
public static float FirstSpawnRemaining=>Mathf.Max(0,FirstSpawn-(active!=null?active.elapsed:FirstSpawn));
public static float SecondSpawnRemaining=>Mathf.Max(0,SecondSpawn-(active!=null?active.elapsed:SecondSpawn));
public static float ThirdSpawnRemaining=>Mathf.Max(0,ThirdSpawn-(active!=null?active.elapsed:ThirdSpawn));
public static string NextEventLabel
{
get
{
if(active==null)return "";
if(!active.spawnedFirst)return "FLUX PLANET";
if(!active.spawnedSecond)return "GUARD PLANET";
if(!active.spawnedThird)return "FINAL FLUX";
return "";
}
}
public static float NextEventRemaining
{
get
{
if(active==null)return 0;
if(!active.spawnedFirst)return FirstSpawnRemaining;
if(!active.spawnedSecond)return SecondSpawnRemaining;
if(!active.spawnedThird)return ThirdSpawnRemaining;
return 0;
}
}private void Awake(){active=this;elapsed=0;warnedFirst=warnedSecond=warnedThird=spawnedFirst=spawnedSecond=spawnedThird=false;Announcement=null;AnnouncementUntil=0;front=FindAnyObjectByType<DimensionsFront>();}private void OnDestroy(){if(active==this){active=null;Announcement=null;AnnouncementUntil=0;}}
private void Update(){if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;if(Time.timeScale<=0f)return;if(front==null)front=FindAnyObjectByType<DimensionsFront>();if(front!=null&&front.HasEnded)return;elapsed+=Time.deltaTime;if(!warnedFirst&&elapsed>=FirstSpawn-Warning){warnedFirst=true;Announce("FLUX PLANET SPAWNS IN 15 SECONDS — CENTER FRONT");}if(!spawnedFirst&&elapsed>=FirstSpawn){spawnedFirst=true;SpawnVariant(0,true);Announce("FLUX PLANET ACTIVE — BUFF + 1% FRONT");}if(!warnedSecond&&elapsed>=SecondSpawn-Warning){warnedSecond=true;Announce("GUARD PLANET IN 15 SECONDS — METEOR STORM • CENTER FRONT");}if(!spawnedSecond&&elapsed>=SecondSpawn){spawnedSecond=true;SpawnVariant(1,true);Announce("GUARD PLANET ACTIVE — METEOR SHOWER • CAPTURE = +1% FRONT");}if(!warnedThird&&elapsed>=ThirdSpawn-Warning){warnedThird=true;Announce("FINAL FLUX PLANET SPAWNS IN 15 SECONDS — CENTER FRONT");}if(!spawnedThird&&elapsed>=ThirdSpawn){spawnedThird=true;SpawnVariant(2,true);Announce("FINAL FLUX PLANET ACTIVE — BUFF + 1% FRONT");}}
public void ApplyNetworkSpawn(int variant)
{
if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
if(variant==0)spawnedFirst=true;else if(variant==1)spawnedSecond=true;else if(variant==2)spawnedThird=true;else return;
SpawnVariant(variant,false);
Announce(variant==0?"FLUX PLANET ACTIVE — BUFF + 1% FRONT":
    variant==1?"GUARD PLANET ACTIVE — METEOR SHOWER • CAPTURE = +1% FRONT":
    "FINAL FLUX PLANET ACTIVE — BUFF + 1% FRONT");
}
private static void SpawnVariant(int variant,bool announceNetwork)
{
string name;Vector3 p;PlanetBuffType type;float hp;Color color;
switch(variant)
{
case 0:name="DF_FluxPlanet";p=new Vector3(0,1.25f,3.2f);type=PlanetBuffType.Flux;hp=320;color=StylizedArtDirection.LumenEnergy;break;
case 1:name="DF_GuardPlanet";p=new Vector3(0,1.25f,-3.2f);type=PlanetBuffType.Guard;hp=900;color=StylizedArtDirection.WarmAccent;break;
case 2:name="DF_FinalFluxPlanet";p=new Vector3(0,1.25f,10f);type=PlanetBuffType.Flux;hp=460;color=StylizedArtDirection.LumenEnergy;break;
default:return;
}
if(GameObject.Find(name)!=null)return;
SpawnPlanet(name,p,type,hp,color,variant);
if(announceNetwork&&Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
    NetworkPlanetSpawned?.Invoke(variant);
}
private static void Announce(string text){Announcement=text;AnnouncementUntil=Time.unscaledTime+4f;}private static void SpawnPlanet(string name,Vector3 p,PlanetBuffType type,float hp,Color color,int variant){GameObject root=GameObject.CreatePrimitive(PrimitiveType.Sphere);root.name=name;GameObject world=GameObject.Find("DF_MatchWorld");if(world!=null)root.transform.SetParent(world.transform,true);root.transform.position=p;root.transform.localScale=Vector3.one*(variant==2?1.82f:1.65f);root.AddComponent<PlanetVisualPresentation>().Build(color,variant);Health h=root.AddComponent<Health>();h.Configure(hp);DimensionPlanet planet=root.AddComponent<DimensionPlanet>();planet.Initialize(type);PlanetPulseAttack pulse=root.AddComponent<PlanetPulseAttack>();if(type==PlanetBuffType.Guard){pulse.enabled=false;root.AddComponent<PlanetMeteorShower>();}PlanetArrived?.Invoke(p);}
}
}
