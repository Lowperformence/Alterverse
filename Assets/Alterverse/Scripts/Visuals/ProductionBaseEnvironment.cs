using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ProductionBaseEnvironment:MonoBehaviour
{
readonly List<Material> mats=new();Transform root;
public void Build(bool zero){root=new GameObject(zero?"DF_ZeroProductionBase":"DF_LumenProductionBase").transform;root.SetParent(transform,false);Color faction=zero?StylizedArtDirection.ZeroMid:StylizedArtDirection.LumenMid,glow=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;Material floor=Mat(Color.Lerp(StylizedArtDirection.ArenaStone,StylizedArtDirection.Ink,.34f),.52f,.34f),trim=Mat(Color.Lerp(faction,StylizedArtDirection.ArenaStone,.55f),.58f,.38f),energy=Mat(glow,.12f,.62f,true);Disc("BaseDeck",6.0f,.015f,.08f,floor);Ring("BaseOuterTrim",5.55f,.095f,.105f,trim);Ring("BaseEnergyCircuit",4.72f,.055f,.12f,energy);BuildRadials(zero,trim,energy);BuildServiceNodes(zero,trim,energy);}
void BuildRadials(bool zero,Material trim,Material energy){for(int i=0;i<8;i++){float a=(i*45+(zero?8:0))*Mathf.Deg2Rad;Vector3 a0=new(Mathf.Cos(a)*2.15f,.12f,Mathf.Sin(a)*2.15f),b0=new(Mathf.Cos(a)*4.55f,.12f,Mathf.Sin(a)*4.55f);Tube("BaseConduit",a0,b0,.035f,i%2==0?energy:trim);}}
void BuildServiceNodes(bool zero,Material trim,Material energy){for(int i=0;i<4;i++){float a=(i*90+45+(zero?11:0))*Mathf.Deg2Rad;Vector3 p=new(Mathf.Cos(a)*4.05f,.18f,Mathf.Sin(a)*4.05f);Disc("ServiceNode",.34f,.18f,.10f,trim,p);GameObject r=ProductionMeshFactory.MeshObject("DF_ServiceHalo",root,ProductionMeshFactory.Ring(.27f,.025f,20,6),energy);r.transform.localPosition=p+Vector3.up*.13f;r.transform.localRotation=Quaternion.Euler(90,0,0);}}
void Disc(string n,float radius,float y,float half,Material mat){Disc(n,radius,y,half,mat,Vector3.zero);}void Disc(string n,float radius,float y,float half,Material mat,Vector3 offset){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Disc(radius,half,36),mat);g.transform.localPosition=offset+Vector3.up*y;}
void Ring(string n,float radius,float tube,float y,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Ring(radius,tube,36,8),mat);g.transform.localPosition=Vector3.up*y;}
void Tube(string n,Vector3 a,Vector3 b,float radius,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Tube(a,b,radius,8),mat);}
Material Mat(Color c,float metallic,float smooth,bool emissive=false){Shader sh=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new(sh);mats.Add(m);m.color=c;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*1.45f);}return m;}
void OnDestroy(){foreach(Material m in mats)if(m!=null)Destroy(m);}
}
}