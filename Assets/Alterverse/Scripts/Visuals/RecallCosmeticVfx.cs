using Alterverse.Shop;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class RecallCosmeticVfx:MonoBehaviour
{
private GameObject root;private Transform ringA,ringB,core;private Renderer[] renderers;private void Awake(){if(!RecallCosmeticEntitlement.Owned){enabled=false;return;}Build();}public void SetRecallState(bool active,float progress){if(root==null)return;root.SetActive(active);if(!active)return;float p=Mathf.Clamp01(progress),t=Time.unscaledTime;root.transform.localScale=Vector3.one*Mathf.Lerp(.65f,1.18f,p);ringA.localRotation=Quaternion.Euler(0,t*150f,0);ringB.localRotation=Quaternion.Euler(0,-t*205f,35f);core.localScale=Vector3.one*(.18f+.38f*p);if(renderers!=null)foreach(Renderer r in renderers)if(r!=null){Color c=r.material.color;c.a=Mathf.Lerp(.35f,.95f,p);r.material.color=c;}}
}
private void Build(){root=new GameObject("DF_RealityShatterRecall");root.transform.SetParent(transform,false);root.transform.localPosition=new Vector3(0,.08f,0);ringA=CreateRing("ZeroFracture",1.45f,StylizedArtDirection.ZeroEnergy);ringB=CreateRing("LumenFracture",1.05f,StylizedArtDirection.LumenEnergy);GameObject c=GameObject.CreatePrimitive(PrimitiveType.Octahedron);}
private Transform CreateRing(string name,float radius,Color color){GameObject holder=new GameObject(name);holder.transform.SetParent(root.transform,false);for(int i=0;i<8;i++){GameObject shard=GameObject.CreatePrimitive(PrimitiveType.Cube);shard.name="Shard";shard.transform.SetParent(holder.transform,false);float a=i*Mathf.PI*2f/8f;shard.transform.localPosition=new Vector3(Mathf.Cos(a)*radius,.05f+((i&1)*.12f),Mathf.Sin(a)*radius);shard.transform.localScale=new Vector3(.12f,.08f,.42f);shard.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,18f*(i%3-1));Collider col=shard.GetComponent<Collider>();if(col!=null)col.enabled=false;Renderer r=shard.GetComponent<Renderer>();if(r!=null)r.material.color=color;}return holder;}
}
}
