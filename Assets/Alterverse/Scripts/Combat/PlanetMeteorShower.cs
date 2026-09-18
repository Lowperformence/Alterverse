using Alterverse.Characters;using Alterverse.Match;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(DimensionPlanet),typeof(Health))]public sealed class PlanetMeteorShower:MonoBehaviour
{
[SerializeField]private float radius=11f,cooldown=8.5f,telegraph=1.55f,damage=14f;[SerializeField]private int meteors=7;private Health self;private DimensionPlanet planet;private float next;
private void Awake(){self=GetComponent<Health>();planet=GetComponent<DimensionPlanet>();next=Time.time+3f;}
private void Update(){if(Time.timeScale<=0||self==null||self.IsDead||planet==null||!planet.Available||Time.time<next)return;if(!HasHero())return;next=Time.time+cooldown;for(int i=0;i<meteors;i++){Vector2 r=Random.insideUnitCircle*radius;Vector3 p=transform.position+new Vector3(r.x,0,r.y);MeteorStrike.Create(p,telegraph,damage,i*.16f);}}
private bool HasHero(){float sq=radius*radius;foreach(TeamMember m in FindObjectsByType<TeamMember>()){Health h=m!=null?m.GetComponent<Health>():null;if(h==null||h.IsDead)continue;Vector3 d=m.transform.position-transform.position;d.y=0;if(d.sqrMagnitude<=sq)return true;}return false;}
}
public sealed class MeteorStrike:MonoBehaviour
{
private float hitAt,damage;private GameObject warning,meteor;public static void Create(Vector3 p,float delay,float dmg,float stagger){GameObject g=new GameObject("DF_MeteorStrike");g.transform.position=p;MeteorStrike s=g.AddComponent<MeteorStrike>();s.damage=dmg;s.hitAt=Time.time+delay+stagger;s.warning=GameObject.CreatePrimitive(PrimitiveType.Cylinder);s.warning.name="DF_MeteorWarning";s.warning.transform.position=p+Vector3.up*.03f;s.warning.transform.localScale=new Vector3(2.1f,.02f,2.1f);Collider c=s.warning.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=s.warning.GetComponent<Renderer>();if(r!=null)r.material.color=new Color(1f,.2f,.05f,.38f);s.meteor=GameObject.CreatePrimitive(PrimitiveType.Sphere);s.meteor.name="DF_FallingMeteor";s.meteor.transform.localScale=Vector3.one*.7f;Collider mc=s.meteor.GetComponent<Collider>();if(mc!=null)mc.enabled=false;Renderer mr=s.meteor.GetComponent<Renderer>();if(mr!=null)mr.material.color=new Color(1f,.32f,.08f);}
private void Update(){if(Time.time<hitAt){if(meteor!=null){float remain=Mathf.Max(0,hitAt-Time.time);meteor.transform.position=transform.position+Vector3.up*Mathf.Lerp(.35f,9f,Mathf.Clamp01(remain/1.7f));}return;}PrototypeVfx.RadialBurst(transform.position+Vector3.up*.1f,new Color(1f,.28f,.06f),2.1f);float sq=2.1f*2.1f;foreach(TeamMember m in FindObjectsByType<TeamMember>()){Health h=m!=null?m.GetComponent<Health>():null;if(h==null||h.IsDead)continue;Vector3 d=m.transform.position-transform.position;d.y=0;if(d.sqrMagnitude<=sq)h.TakeDamage(damage,gameObject);}if(warning!=null)Destroy(warning);if(meteor!=null)Destroy(meteor);Destroy(gameObject);}
private void OnDestroy(){if(warning!=null)Destroy(warning);if(meteor!=null)Destroy(meteor);}
}
}
