using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class HealingZoneMarker:MonoBehaviour
{
Transform root,plus;Material energy,metal,trim;
void Start(){Build();}
void Build(){root=new GameObject("DF_SpawnHealStationProduction").transform;root.SetParent(transform,false);energy=Mat(new Color(.02f,1f,.12f),.08f,.62f,true);metal=Mat(new Color(.055f,.075f,.10f),.78f,.30f);trim=Mat(new Color(.19f,.24f,.29f),.62f,.46f);Disc("HealBase",2.15f,.025f,.025f,metal);Disc("HealDeck",1.78f,.065f,.015f,trim);GameObject halo=ProductionMeshFactory.MeshObject("DF_HealEnergyRing",root,ProductionMeshFactory.Ring(1.42f,.050f,32,8),energy);halo.transform.localPosition=new Vector3(0,.185f,0);for(int i=0;i<6;i++){float a=i*Mathf.PI/3f;Vector3 outer=new(Mathf.Cos(a)*1.82f,.145f,Mathf.Sin(a)*1.82f),inner=new(Mathf.Cos(a)*1.02f,.205f,Mathf.Sin(a)*1.02f);Tube("HealFeed",outer,inner,.026f,i%2==0?energy:trim);}plus=new GameObject("DF_SpawnHealingPlus").transform;plus.SetParent(root,false);plus.localPosition=new Vector3(0,.92f,0);TubeOn(plus,"PlusVertical",new Vector3(0,-.18f,0),new Vector3(0,.18f,0),.075f,energy);TubeOn(plus,"PlusHorizontal",new Vector3(-.18f,0,0),new Vector3(.18f,0,0),.075f,energy);}
void Update(){if(plus==null)return;float t=Time.time;float p=1f+Mathf.Sin(t*2.7f)*.040f;plus.localScale=Vector3.one*(3f*p);plus.localPosition=new Vector3(0,.92f+Mathf.Sin(t*1.8f)*.012f,0);plus.Rotate(Vector3.up,9f*Time.deltaTime,Space.Self);}
void Disc(string n,float radius,float y,float halfHeight,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Disc(radius,halfHeight,32),mat);g.transform.localPosition=new Vector3(0,y,0);}
void Tube(string n,Vector3 a,Vector3 b,float radius,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Tube(a,b,radius,8),mat);}
void TubeOn(Transform parent,string n,Vector3 a,Vector3 b,float radius,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,parent,ProductionMeshFactory.Tube(a,b,radius,10),mat);}
Material Mat(Color c,float metallic,float smooth,bool emissive=false){Shader sh=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new(sh);m.color=c;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*1.8f);}return m;}
}
}