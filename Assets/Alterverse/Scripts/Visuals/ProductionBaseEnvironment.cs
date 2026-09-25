using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ProductionBaseEnvironment:MonoBehaviour
{
readonly List<Material> mats=new();Transform root;
public void Build(bool zero){root=new GameObject(zero?"DF_ZeroProductionBase":"DF_LumenProductionBase").transform;root.SetParent(transform,false);Color faction=zero?StylizedArtDirection.ZeroMid:StylizedArtDirection.LumenMid,glow=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;Material floor=Mat(Color.Lerp(StylizedArtDirection.ArenaStone,StylizedArtDirection.Ink,.34f),.52f,.43f),trim=Mat(Color.Lerp(faction,StylizedArtDirection.ArenaStone,.55f),.64f,.53f),energy=Mat(glow,.12f,.72f,true);Disc("BaseDeck",4.75f,.015f,.08f,floor);Ring("BaseOuterTrim",4.42f,.085f,.105f,trim);Ring("BaseEnergyCircuit",3.72f,.05f,.12f,energy);Ring("BaseInnerArmor",2.52f,.12f,.15f,trim);Ring("BaseReactorTrace",2.30f,.035f,.17f,energy);BuildRadials(zero,trim,energy);BuildServiceNodes(zero,trim,energy);}
void BuildRadials(bool zero,Material trim,Material energy){for(int i=0;i<8;i++){float a=(i*45+(zero?8:0))*Mathf.Deg2Rad;Vector3 a0=new(Mathf.Cos(a)*1.75f,.12f,Mathf.Sin(a)*1.75f),b0=new(Mathf.Cos(a)*3.62f,.12f,Mathf.Sin(a)*3.62f);Tube("BaseConduit",a0,b0,.035f,i%2==0?energy:trim);}}
void BuildServiceNodes(bool zero,Material trim,Material energy){for(int i=0;i<4;i++){float a=(i*90+45+(zero?11:0))*Mathf.Deg2Rad;Vector3 p=new(Mathf.Cos(a)*3.32f,.18f,Mathf.Sin(a)*3.32f);Disc("ServiceNode",.34f,.18f,.10f,trim,p);GameObject r=ProductionMeshFactory.MeshObject("DF_ServiceHalo",root,ProductionMeshFactory.Ring(.27f,.025f,20,6),energy);r.transform.localPosition=p+Vector3.up*.13f;r.transform.localRotation=Quaternion.Euler(90,0,0);}}
void Disc(string n,float radius,float y,float half,Material mat){Disc(n,radius,y,half,mat,Vector3.zero);}void Disc(string n,float radius,float y,float half,Material mat,Vector3 offset){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Disc(radius,half,36),mat);g.transform.localPosition=offset+Vector3.up*y;}
void Ring(string n,float radius,float tube,float y,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Ring(radius,tube,36,8),mat);g.transform.localPosition=Vector3.up*y;}
void Tube(string n,Vector3 a,Vector3 b,float radius,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Tube(a,b,radius,8),mat);}
Material Mat(Color c,float metallic,float smooth,bool emissive=false){Shader sh=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new(sh);mats.Add(m);m.color=c;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*1.45f);}return m;}
void OnDestroy(){var owned=new HashSet<Mesh>();if(root!=null)foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&owned.Add(filter.sharedMesh))Destroy(filter.sharedMesh);foreach(Material m in mats)if(m!=null)Destroy(m);}
}
}
