using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class HealingZoneMarker:MonoBehaviour
{
Transform root,plus;Material energy,metal,trim;
void Start(){Build();}
void Build(){root=new GameObject("DF_SpawnHealStationProduction").transform;root.SetParent(transform,false);energy=Mat(new Color(.02f,1f,.12f),.08f,.62f,true);metal=Mat(new Color(.055f,.075f,.10f),.78f,.30f);trim=Mat(new Color(.19f,.24f,.29f),.62f,.46f);Disc("HealBase",2.15f,.13f,.16f,metal);Disc("HealDeck",1.78f,.29f,.11f,trim);GameObject halo=ProductionMeshFactory.MeshObject("DF_HealEnergyRing",root,ProductionMeshFactory.Ring(1.42f,.065f,32,8),energy);halo.transform.localPosition=new Vector3(0,.39f,0);for(int i=0;i<6;i++){float a=i*Mathf.PI/3f;Vector3 outer=new(Mathf.Cos(a)*1.82f,.34f,Mathf.Sin(a)*1.82f),inner=new(Mathf.Cos(a)*1.02f,.43f,Mathf.Sin(a)*1.02f);Tube("HealFeed",outer,inner,.035f,i%2==0?energy:trim);}plus=new GameObject("DF_SpawnHealingPlus").transform;plus.SetParent(root,false);plus.localPosition=new Vector3(0,1.78f,0);TubeOn(plus,"PlusVertical",new Vector3(0,-.52f,0),new Vector3(0,.52f,0),.15f,energy);TubeOn(plus,"PlusHorizontal",new Vector3(-.52f,0,0),new Vector3(.52f,0,0),.15f,energy);}
void Update(){if(plus==null)return;float t=Time.time;float p=1f+Mathf.Sin(t*2.7f)*.055f;plus.localScale=Vector3.one*p;plus.localPosition=new Vector3(0,1.78f+Mathf.Sin(t*1.8f)*.07f,0);plus.Rotate(Vector3.up,12f*Time.deltaTime,Space.Self);}
void Disc(string n,float radius,float y,float halfHeight,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Disc(radius,halfHeight,32),mat);g.transform.localPosition=new Vector3(0,y,0);}
void Tube(string n,Vector3 a,Vector3 b,float radius,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Tube(a,b,radius,8),mat);}
void TubeOn(Transform parent,string n,Vector3 a,Vector3 b,float radius,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,parent,ProductionMeshFactory.Tube(a,b,radius,10),mat);}
Material Mat(Color c,float metallic,float smooth,bool emissive=false){Shader sh=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new(sh);m.color=c;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*1.8f);}return m;}
}
}