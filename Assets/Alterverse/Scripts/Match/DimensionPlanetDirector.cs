using Alterverse.Combat;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.Match
{
public sealed class DimensionPlanetDirector:MonoBehaviour
{
private const float FirstSpawn=120f,SecondSpawn=300f,Warning=15f;private float elapsed;private bool warnedFirst,warnedSecond,spawnedFirst,spawnedSecond;public static string Announcement{get;private set;}public static float AnnouncementUntil{get;private set;}
private void Update(){elapsed+=Time.deltaTime;if(!warnedFirst&&elapsed>=FirstSpawn-Warning){warnedFirst=true;Announce("FLUX-PLANET ERSCHEINT IN 15 SEKUNDEN");}if(!spawnedFirst&&elapsed>=FirstSpawn){spawnedFirst=true;SpawnPlanet("DF_FluxPlanet",new Vector3(0,1.25f,27),PlanetBuffType.Flux,320,StylizedArtDirection.LumenEnergy);}if(!warnedSecond&&elapsed>=SecondSpawn-Warning){warnedSecond=true;Announce("GUARD-PLANET ERSCHEINT IN 15 SEKUNDEN");}if(!spawnedSecond&&elapsed>=SecondSpawn){spawnedSecond=true;SpawnPlanet("DF_GuardPlanet",new Vector3(0,1.25f,-27),PlanetBuffType.Guard,460,StylizedArtDirection.WarmAccent);}}
private static void Announce(string text){Announcement=text;AnnouncementUntil=Time.unscaledTime+4f;}
private static void SpawnPlanet(string name,Vector3 p,PlanetBuffType type,float hp,Color color){GameObject root=GameObject.CreatePrimitive(PrimitiveType.Sphere);root.name=name;root.transform.position=p;root.transform.localScale=Vector3.one*2.15f;Renderer r=root.GetComponent<Renderer>();if(r!=null)r.material.color=color;Health h=root.AddComponent<Health>();h.Configure(hp);DimensionPlanet planet=root.AddComponent<DimensionPlanet>();planet.Initialize(type);for(int i=0;i<4;i++){GameObject shard=GameObject.CreatePrimitive(PrimitiveType.Cube);shard.name="DF_PlanetShard";shard.transform.SetParent(root.transform,false);float a=i*Mathf.PI*.5f;shard.transform.localPosition=new Vector3(Mathf.Cos(a)*1.45f,(i%2)*.35f-.15f,Mathf.Sin(a)*1.45f);shard.transform.localScale=new Vector3(.18f,.65f,.18f);shard.transform.rotation=Quaternion.Euler(25,i*90,25);Collider c=shard.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer sr=shard.GetComponent<Renderer>();if(sr!=null)sr.material.color=color;}}
}
}
