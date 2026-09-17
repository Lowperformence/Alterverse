using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Shop;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class FounderHeroVisual:MonoBehaviour
{
private Health health;private HeroIdentity identity;private Transform crown;private bool wasDead;private Color zero=new Color(.48f,.12f,.95f),lumen=new Color(.05f,.9f,1f),gold=new Color(1f,.72f,.18f);
private void Start(){if(!FounderEntitlement.Owned||GetComponent<PlayerHeroAbilities>()==null){enabled=false;return;}health=GetComponent<Health>();identity=GetComponent<HeroIdentity>();if(health==null||identity==null){enabled=false;return;}BuildMark();SpawnEffect();wasDead=health.IsDead;}
private void Update(){if(health==null)return;if(!wasDead&&health.IsDead)DeathEffect();if(wasDead&&!health.IsDead)SpawnEffect();wasDead=health.IsDead;if(crown!=null){crown.Rotate(0,55*Time.deltaTime,0,Space.Self);float p=1+Mathf.Sin(Time.time*2.8f)*.08f;crown.localScale=new Vector3(p,.05f,p);}}
private void BuildMark(){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="DF_FirstFractureFounderMark";g.transform.SetParent(transform,false);g.transform.localPosition=new Vector3(0,1.55f,0);g.transform.localScale=new Vector3(.42f,.025f,.42f);Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=gold;crown=g.transform;for(int i=0;i<3;i++){GameObject shard=GameObject.CreatePrimitive(PrimitiveType.Cube);shard.name="DF_FounderShard";shard.transform.SetParent(transform,false);float a=i*120*Mathf.Deg2Rad;shard.transform.localPosition=new Vector3(Mathf.Cos(a)*.48f,1.48f,Mathf.Sin(a)*.48f);shard.transform.localScale=new Vector3(.06f,.22f,.06f);Collider sc=shard.GetComponent<Collider>();if(sc!=null)sc.enabled=false;Renderer sr=shard.GetComponent<Renderer>();if(sr!=null)sr.material.color=i==0?gold:(i==1?zero:lumen);}}
private void SpawnEffect(){Vector3 p=transform.position;PrototypeVfx.Ring(p,zero,2.8f,.5f);PrototypeVfx.Ring(p+Vector3.up*.04f,lumen,2.15f,.42f);PrototypeVfx.Burst(p,gold,1.15f,9);PrototypeVfx.Guard(p,Color.Lerp(zero,lumen,.5f),1.55f);}
private void DeathEffect(){Vector3 p=transform.position;PrototypeVfx.Shockwave(p,zero,2.7f);PrototypeVfx.Burst(p,lumen,1.35f,12);PrototypeVfx.Ring(p,gold,3.1f,.55f);}
}
}
