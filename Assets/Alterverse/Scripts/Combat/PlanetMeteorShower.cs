using Alterverse.Characters;using Alterverse.Match;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(DimensionPlanet),typeof(Health))]public sealed class PlanetMeteorShower:MonoBehaviour
{
public const float MeteorDamage=14f;
public static event System.Action<string,Vector3,float,float,float> NetworkMeteorSpawned;
[SerializeField]private float radius=11f,cooldown=8.5f,telegraph=1.55f,damage=MeteorDamage;[SerializeField]private int meteors=7;private const int HeroScanBufferSize=32;private readonly Collider[] heroScanBuffer=new Collider[HeroScanBufferSize];private Health self;private DimensionPlanet planet;private float next,nextHeroScan;private bool heroNearby;
private void Awake(){self=GetComponent<Health>();planet=GetComponent<DimensionPlanet>();next=Time.time+3f;}
private void Update()
{
if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
if(Time.timeScale<=0||self==null||self.IsDead||planet==null||!planet.Available||Time.time<next)return;
if(Time.time>=nextHeroScan){nextHeroScan=Time.time+.25f;heroNearby=HasHero();}
if(!heroNearby)return;
next=Time.time+cooldown;
for(int i=0;i<meteors;i++)
{
Vector2 r=Random.insideUnitCircle*radius;
Vector3 p=transform.position+new Vector3(r.x,0,r.y);
float stagger=i*.16f;
MeteorStrike.Create(p,telegraph,damage,stagger,planet);
if(Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
    NetworkMeteorSpawned?.Invoke(gameObject.name,p,telegraph,damage,stagger);
}
}
public static void ApplyNetworkMeteor(string ownerName,Vector3 point,float delay,float damage,float stagger)
{
if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
DimensionPlanet[] planets=Object.FindObjectsByType<DimensionPlanet>(FindObjectsInactive.Exclude);
for(int i=0;i<planets.Length;i++)
{
DimensionPlanet p=planets[i];
if(p!=null&&p.gameObject.name==ownerName){MeteorStrike.Create(point,delay,damage,stagger,p);return;}
}
}
private bool HasHero(){int hits=Physics.OverlapSphereNonAlloc(transform.position,radius,heroScanBuffer);for(int i=0;i<hits;i++){Collider c=heroScanBuffer[i];if(c==null)continue;TeamMember m=c.GetComponentInParent<TeamMember>();if(m==null)continue;Health h=m.GetComponent<Health>();if(h!=null&&!h.IsDead)return true;}return false;}
}
public sealed class MeteorStrike:MonoBehaviour
{
private static readonly System.Collections.Generic.List<MeteorStrike> active=new();public static System.Collections.Generic.IReadOnlyList<MeteorStrike> Active=>active;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetRegistry()=>active.Clear();
private const float ImpactRadius=2.1f;private Material warningInk,rockMaterial;private Mesh meteorMesh;private LineRenderer warningLine,trail;
private const int HitBufferSize=32;private readonly Collider[] hitBuffer=new Collider[HitBufferSize];private float spawnAt,hitAt,damage;private GameObject warning,meteor;private Vector3 startPosition;private DimensionPlanet owner;private DimensionsFront front;
public Vector3 ImpactPosition=>startPosition;public float BlastRadius=>ImpactRadius;public float TelegraphAge=>Mathf.Max(0f,Time.time-spawnAt);public float TimeRemaining=>Mathf.Max(0f,hitAt-Time.time);public bool IsTelegraphing=>Time.time<hitAt&&owner!=null&&owner.Available;
private void OnEnable(){if(!active.Contains(this))active.Add(this);}private void OnDisable(){active.Remove(this);}
public static void Create(Vector3 p,float delay,float dmg,float stagger,DimensionPlanet sourcePlanet){GameObject g=new GameObject("DF_MeteorStrike");g.transform.position=p;MeteorStrike s=g.AddComponent<MeteorStrike>();s.damage=dmg;s.spawnAt=Time.time;s.hitAt=Time.time+delay+stagger;s.startPosition=p;s.owner=sourcePlanet;s.front=sourcePlanet!=null?sourcePlanet.GetComponentInParent<DimensionsFront>():null;if(s.front==null)s.front=FindAnyObjectByType<DimensionsFront>();Transform world=GameObject.Find("DF_MatchWorld")?.transform;if(world!=null)g.transform.SetParent(world,true);s.BuildVisuals(world);}
private void BuildVisuals(Transform world)
{
Shader ink=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");Shader rock=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
if(ink==null||rock==null)return;
warningInk=new Material(ink){name="DF_MeteorWarningInk"};rockMaterial=new Material(rock){name="DF_MeteorRock",color=new Color(.19f,.14f,.11f)};
if(rockMaterial.HasProperty("_Smoothness"))rockMaterial.SetFloat("_Smoothness",.08f);
rockMaterial.EnableKeyword("_EMISSION");rockMaterial.SetColor("_EmissionColor",new Color(.5f,.14f,.025f));
warning=new GameObject("DF_MeteorWarning");if(world!=null)warning.transform.SetParent(world,false);
warningLine=warning.AddComponent<LineRenderer>();warningLine.sharedMaterial=warningInk;warningLine.loop=true;warningLine.useWorldSpace=true;warningLine.positionCount=48;warningLine.widthMultiplier=.07f;warningLine.startColor=warningLine.endColor=new Color(1,.48f,.12f);warningLine.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;warningLine.SetPosition(i,new Vector3(startPosition.x+Mathf.Cos(angle)*ImpactRadius,.15f,startPosition.z+Mathf.Sin(angle)*ImpactRadius));}
meteorMesh=LunarFragmentGeometry.Create(13);meteor=ProductionMeshFactory.MeshObject("DF_FallingMeteor",world,meteorMesh,rockMaterial);meteor.transform.localScale=Vector3.one*.55f;meteor.transform.position=startPosition+Vector3.up*9;
trail=meteor.AddComponent<LineRenderer>();trail.sharedMaterial=warningInk;trail.useWorldSpace=true;trail.positionCount=2;trail.startWidth=.14f;trail.endWidth=.015f;trail.startColor=new Color(1,.54f,.14f);trail.endColor=new Color(.35f,.13f,.04f);trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
}

private void Update(){if(owner==null||!owner.Available){Destroy(gameObject);return;}if(front!=null&&front.HasEnded){Destroy(gameObject);return;}if(Time.time<hitAt){if(meteor!=null){float remain=Mathf.Max(0,hitAt-Time.time);float height=Mathf.Lerp(.35f,9f,Mathf.Clamp01(remain/1.7f));meteor.transform.position=new Vector3(startPosition.x,height,startPosition.z);meteor.transform.Rotate(80*Time.deltaTime,36*Time.deltaTime,52*Time.deltaTime);if(trail!=null){trail.SetPosition(0,meteor.transform.position);trail.SetPosition(1,meteor.transform.position+new Vector3(.14f,1.4f,.1f));}if(warningLine!=null)warningLine.widthMultiplier=.055f+.025f*(1-Mathf.Clamp01(remain/1.7f));}return;}Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Explosion,transform.position,.5f);Vector3 impact=new Vector3(transform.position.x,.3f,transform.position.z);BomberExplosionVfx.Play(impact,new Color(1f,.63f,.23f),ImpactRadius);PrototypeVfx.Ring(impact,new Color(1f,.28f,.08f),ImpactRadius*1.05f,.3f);int hits=Physics.OverlapSphereNonAlloc(transform.position,ImpactRadius,hitBuffer);for(int i=0;i<hits;i++){Collider c=hitBuffer[i];if(c==null)continue;Health h=c.GetComponentInParent<Health>();if(h==null||h.IsDead)continue;bool alreadyHit=false;for(int j=0;j<i;j++){Collider prior=hitBuffer[j];if(prior!=null&&prior.GetComponentInParent<Health>()==h){alreadyHit=true;break;}}if(alreadyHit)continue;if(h.GetComponent<RealityCore>()!=null||h.GetComponent<LaneTower>()!=null||h.GetComponent<DimensionPlanet>()!=null)continue;if(h.GetComponent<TeamMember>()==null&&h.GetComponent<LaneMinion>()==null)continue;h.TakeDamage(damage,gameObject);}if(warning!=null)Destroy(warning);if(meteor!=null)Destroy(meteor);Destroy(gameObject);}
private void OnDestroy(){active.Remove(this);if(warning!=null)Destroy(warning);if(meteor!=null)Destroy(meteor);if(warningInk!=null)Destroy(warningInk);if(rockMaterial!=null)Destroy(rockMaterial);if(meteorMesh!=null)Destroy(meteorMesh);}
}
}
