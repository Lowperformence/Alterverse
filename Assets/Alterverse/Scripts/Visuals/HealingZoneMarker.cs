using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class HealingZoneMarker:MonoBehaviour
{
private Transform root,plus;private Material energyMat,stoneMat;
private void Start(){Build();}
private void Build(){root=new GameObject("DF_SpawnHealStation").transform;root.SetParent(transform,false);energyMat=MakeMaterial(new Color(.08f,1f,.30f),true);stoneMat=MakeMaterial(new Color(.16f,.21f,.29f),false);const int segments=10;for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;Transform seg=Part(root,"SpawnPedestal",new Vector3(Mathf.Cos(a)*2.35f,.22f,Mathf.Sin(a)*2.35f),new Vector3(1.45f,.42f,.78f),stoneMat);seg.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg+90f,0);Part(seg,"SpawnEnergyStrip",new Vector3(0,.04f,-.42f),new Vector3(.82f,.10f,.06f),energyMat);}Part(root,"SpawnCore",new Vector3(0,.14f,0),new Vector3(3.25f,.24f,3.25f),stoneMat);Part(root,"SpawnCoreGlow",new Vector3(0,.27f,0),new Vector3(2.25f,.035f,2.25f),energyMat);plus=new GameObject("DF_SpawnHealingPlus").transform;plus.SetParent(root,false);plus.localPosition=new Vector3(0,2.35f,0);Part(plus,"PlusVertical",Vector3.zero,new Vector3(.58f,2.25f,.48f),energyMat);Part(plus,"PlusHorizontal",Vector3.zero,new Vector3(2.25f,.58f,.48f),energyMat);}
private void Update(){if(plus==null)return;float p=1f+Mathf.Sin(Time.time*3.1f)*.09f;plus.localScale=Vector3.one*p;plus.localPosition=new Vector3(0,2.35f+Mathf.Sin(Time.time*2.1f)*.14f,0);}
private Transform Part(Transform parent,string n,Vector3 p,Vector3 scale,Material mat){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_"+n;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.sharedMaterial=mat;return g.transform;}
private Material MakeMaterial(Color color,bool emissive){Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");Material m=new Material(shader);m.color=color;if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2.7f);}return m;}
}
}