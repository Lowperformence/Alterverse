using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class DimensionTowerShell:MonoBehaviour
{
private Transform visualRoot;private Color dark,energy;private bool zero;public void Build(int team){zero=team==0;dark=zero?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark;energy=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;visualRoot=new GameObject("DF_TowerShell").transform;visualRoot.SetParent(transform,false);if(zero)BuildZero();else BuildLumen();}
private void BuildZero(){Part("BrokenSpine",PrimitiveType.Cube,new Vector3(0,.45f,0),new Vector3(.72f,1.85f,.72f),dark,new Vector3(0,17,8));Part("FractureBladeL",PrimitiveType.Cube,new Vector3(-.48f,.9f,.03f),new Vector3(.18f,1.55f,.42f),energy,new Vector3(0,-14,-28),true);Part("FractureBladeR",PrimitiveType.Cube,new Vector3(.42f,1.05f,-.06f),new Vector3(.15f,1.25f,.34f),energy,new Vector3(0,22,34),true);Part("Crown",PrimitiveType.Cube,new Vector3(0,1.45f,0),new Vector3(.82f,.18f,.82f),dark,new Vector3(0,32,12));}
private void BuildLumen(){Part("PrismSpine",PrimitiveType.Cube,new Vector3(0,.48f,0),new Vector3(.68f,1.95f,.68f),dark,new Vector3(0,45,0));for(int i=0;i<3;i++){float a=(i*120+30)*Mathf.Deg2Rad;Part("LightRail",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.46f,.78f,Mathf.Sin(a)*.46f),new Vector3(.09f,1.5f,.09f),energy,new Vector3(0,45,0),true);}Part("Halo",PrimitiveType.Cylinder,new Vector3(0,1.55f,0),new Vector3(.78f,.045f,.78f),energy,Vector3.zero,true);}
private void Part(string n,PrimitiveType type,Vector3 p,Vector3 s,Color c,Vector3 r,bool pulse=false){GameObject g=GameObject.CreatePrimitive(type);g.name="DF_"+n;g.transform.SetParent(visualRoot,false);g.transform.localPosition=p;g.transform.localScale=s;g.transform.localRotation=Quaternion.Euler(r);Collider col=g.GetComponent<Collider>();if(col!=null)col.enabled=false;Renderer renderer=g.GetComponent<Renderer>();if(renderer!=null)renderer.material.color=c;if(pulse)g.AddComponent<ArenaLightPulse>();}
}
}
