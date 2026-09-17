using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class RealityCoreShell:MonoBehaviour
{
private Transform root;private Color dark,energy;public void Build(bool zero){dark=zero?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark;energy=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;root=new GameObject("DF_RealityCoreShell").transform;root.SetParent(transform,false);if(zero)BuildZero();else BuildLumen();}
private void BuildZero(){for(int i=0;i<5;i++){float a=(i*72+14)*Mathf.Deg2Rad;float radius=1.15f+(i%2)*.2f;Part("BrokenCoreRib",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*radius,.45f+((i%2)*.18f),Mathf.Sin(a)*radius),new Vector3(.18f,1.65f,.34f),i%2==0?dark:energy,new Vector3(18+i*5,-i*31,24-i*7),i%2!=0);}Part("VoidSeed",PrimitiveType.Cube,new Vector3(0,.52f,0),new Vector3(.58f,.58f,.58f),energy,new Vector3(25,45,18),true);}
private void BuildLumen(){for(int i=0;i<4;i++){float a=(i*90+45)*Mathf.Deg2Rad;Part("PrismPillar",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*1.05f,.55f,Mathf.Sin(a)*1.05f),new Vector3(.22f,1.8f,.22f),dark,new Vector3(0,45,0));Part("PrismRail",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.82f,.72f,Mathf.Sin(a)*.82f),new Vector3(.08f,1.25f,.08f),energy,new Vector3(0,45,0),true);}Part("LightSeed",PrimitiveType.Sphere,new Vector3(0,.7f,0),Vector3.one*.62f,energy,Vector3.zero,true);Part("CrownRing",PrimitiveType.Cylinder,new Vector3(0,1.55f,0),new Vector3(1.15f,.035f,1.15f),energy,Vector3.zero,true);}
private void Part(string n,PrimitiveType type,Vector3 p,Vector3 s,Color c,Vector3 r,bool pulse=false){GameObject g=GameObject.CreatePrimitive(type);g.name="DF_"+n;g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=s;g.transform.localRotation=Quaternion.Euler(r);Collider col=g.GetComponent<Collider>();if(col!=null)col.enabled=false;Renderer renderer=g.GetComponent<Renderer>();if(renderer!=null)renderer.material.color=c;if(pulse)g.AddComponent<ArenaLightPulse>();}
}
}
