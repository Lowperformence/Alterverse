using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Match
{
[RequireComponent(typeof(Health))]public sealed class DimensionPlanet:MonoBehaviour
{
public static event System.Action<int,PlanetBuffType,float> NetworkBuffGranted;
[SerializeField]private PlanetBuffType buffType;[SerializeField]private float buffDuration=60f,frontInfluence=1f;private Health health;private DimensionsFront front;private Renderer[] visuals;private Collider[] colliders;private Transform ring,atmosphere,core,beacon;private Vector3 beaconRest;private Transform[] moons,orbitRings;private bool available=true;public PlanetBuffType BuffType=>buffType;public float FrontReward=>frontInfluence;public bool Available=>available;public float RespawnRemaining=>0;
public void ApplyNetworkAvailability(bool value)
{
if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
if(available&&!value)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Explosion,transform.position,.65f);
available=value;
SetVisible(value);
}
public void Initialize(PlanetBuffType type){gameObject.layer=GameplayCollision.Structures;buffType=type;MakeCollidersNonBlocking();RefreshVisualCache();}private void MakeCollidersNonBlocking(){foreach(Collider c in GetComponentsInChildren<Collider>(true))if(c!=null)c.isTrigger=true;}
private void Awake(){MakeCollidersNonBlocking();health=GetComponent<Health>();front=FindAnyObjectByType<DimensionsFront>();ring=transform.Find("DF_PlanetRing");atmosphere=transform.Find("DF_PlanetAtmosphere");core=transform.Find("DF_PlanetCore");beacon=transform.Find("DF_PlanetGroundBeacon");if(beacon!=null)beaconRest=beacon.localScale;Transform[] all=GetComponentsInChildren<Transform>(true);System.Collections.Generic.List<Transform> orbiters=new System.Collections.Generic.List<Transform>(),bands=new System.Collections.Generic.List<Transform>();foreach(Transform t in all)if(t!=null){if(t.name=="DF_GuardMoon")orbiters.Add(t);else if(t.name=="DF_PlanetOrbitRing")bands.Add(t);}moons=orbiters.ToArray();orbitRings=bands.ToArray();RefreshVisualCache();health.DiedWithSource+=OnDestroyed;}private void Update(){if(!available||Time.timeScale<=0f)return;transform.Rotate(0,14*Time.deltaTime,0,Space.World);if(ring!=null)ring.Rotate(0,34*Time.deltaTime,0,Space.Self);if(atmosphere!=null){float pulse=1.16f+Mathf.Sin(Time.time*2.4f)*.035f;atmosphere.localScale=Vector3.one*pulse;}if(moons!=null)for(int i=0;i<moons.Length;i++)if(moons[i]!=null)moons[i].RotateAround(transform.position,Vector3.up,(18f+i*4f)*Time.deltaTime);if(orbitRings!=null)for(int i=0;i<orbitRings.Length;i++)if(orbitRings[i]!=null)orbitRings[i].Rotate(0,(i%2==0?26f:-19f)*Time.deltaTime,0,Space.Self);if(core!=null){float cp=.46f+Mathf.Sin(Time.time*3.4f)*.055f;core.localScale=Vector3.one*cp;}if(beacon!=null){float bp=1f+Mathf.Sin(Time.time*2.1f)*.08f;beacon.localScale=new Vector3(beaconRest.x*bp,beaconRest.y,beaconRest.z*bp);}}public void RefreshVisualCache(){visuals=GetComponentsInChildren<Renderer>(true);colliders=GetComponentsInChildren<Collider>(true);}
private void OnDestroyed(Health victim,GameObject source){if(!available)return;TeamMember killer=source!=null?source.GetComponentInParent<TeamMember>():null;if(killer==null||killer.Team<0||killer.Team>1){health.RestoreFull();return;}available=false;Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Explosion,transform.position,.65f);Color burst=buffType==PlanetBuffType.Guard?new Color(1f,.32f,.08f):new Color(.2f,.85f,1f);Alterverse.Visuals.PrototypeVfx.RadialBurst(transform.position,burst,3.6f);ApplyTeamBuff(killer.Team);if(Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority)NetworkBuffGranted?.Invoke(killer.Team,buffType,buffDuration);if(front==null)front=FindAnyObjectByType<DimensionsFront>();front?.Push(killer.Team,frontInfluence);MatchTelemetry.Instance?.RecordObjective(killer.gameObject,frontInfluence);SetVisible(false);}
private void ApplyTeamBuff(int team)=>ApplyBuffToTeam(team,buffType,buffDuration);
public static void ApplyNetworkBuff(int team,PlanetBuffType type,float duration)
{
if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
ApplyBuffToTeam(team,type,duration);
}
private static void ApplyBuffToTeam(int team,PlanetBuffType type,float duration){foreach(TeamMember member in FindObjectsByType<TeamMember>()){if(member.Team!=team)continue;Health h=member.GetComponent<Health>();if(h==null||h.IsDead)continue;PlanetBuffReceiver receiver=member.GetComponent<PlanetBuffReceiver>()??member.gameObject.AddComponent<PlanetBuffReceiver>();receiver.Apply(type,duration);}}
private void SetVisible(bool value){RefreshVisualCache();foreach(Renderer r in visuals)if(r!=null)r.enabled=value;foreach(Collider c in colliders)if(c!=null)c.enabled=value;}private void OnDestroy(){if(health!=null)health.DiedWithSource-=OnDestroyed;}
}
}
