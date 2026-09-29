using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ProductionBaseEnvironment:MonoBehaviour
{
readonly List<Material> mats=new();Transform root;
public void Build(bool zero)
{
    if(root!=null)return;
    root=new GameObject(zero?"DF_ZeroProductionBase":"DF_LumenProductionBase").transform;
    root.SetParent(transform,false);
    Color faction=zero?StylizedArtDirection.ZeroMid:StylizedArtDirection.LumenMid;
    Color glow=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
    Material floor=Mat(Color.Lerp(StylizedArtDirection.ArenaStone,StylizedArtDirection.Ink,.34f),.52f,.43f);
    Material trim=Mat(Color.Lerp(faction,StylizedArtDirection.ArenaStone,.55f),.64f,.53f);
    Material dark=Mat(Color.Lerp(StylizedArtDirection.Ink,faction,.16f),.78f,.36f);
    Material energy=Mat(glow,.12f,.72f,true);
    Disc("BaseDeck",4.75f,.015f,.08f,floor);
    Ring("BaseOuterTrim",4.42f,.085f,.105f,trim);
    Ring("BaseEnergyCircuit",3.72f,.05f,.12f,energy);
    Ring("BaseInnerArmor",2.52f,.12f,.15f,trim);
    Ring("BaseReactorTrace",2.30f,.035f,.17f,energy);
    BuildRadials(zero,trim,energy);
    BuildDeckPanels(zero,dark,trim,energy);
    BuildServiceNodes(zero,dark,trim,energy);
}
void BuildRadials(bool zero,Material trim,Material energy)
{
    for(int i=0;i<8;i++)
    {
        float a=(i*45+(zero?8:0))*Mathf.Deg2Rad;
        Vector3 direction=new(Mathf.Cos(a),0,Mathf.Sin(a));
        Tube("BaseConduit",direction*1.75f+Vector3.up*.12f,
            direction*3.62f+Vector3.up*.12f,.035f,i%2==0?energy:trim);
        // Paired armor rails frame each feed without blocking the walkable deck.
        Vector3 tangent=new(-direction.z,0,direction.x);
        for(int side=-1;side<=1;side+=2)
            Tube("ConduitGuard",direction*2.67f+tangent*(side*.15f)+Vector3.up*.135f,
                direction*3.45f+tangent*(side*.15f)+Vector3.up*.135f,.024f,trim);
    }
}
void BuildDeckPanels(bool zero,Material dark,Material trim,Material energy)
{
    for(int i=0;i<12;i++)
    {
        float angle=(i*30+(zero?15:0))*Mathf.Deg2Rad;
        Vector3 radial=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
        Vector3 tangent=new(-radial.z,0,radial.x);
        Vector3 center=radial*3.03f;
        Disc("InsetDeckPlate",.36f,.14f,.022f,dark,center);
        Tube("PlateSeam",center-tangent*.28f+Vector3.up*.164f,
            center+tangent*.28f+Vector3.up*.164f,.013f,trim);
        Disc("StatusStud",.055f,.176f,.018f,i%3==0?energy:trim,center+radial*.25f);
    }
    // Segmented perimeter plates read as a built station from the overhead camera.
    for(int i=0;i<16;i++)
    {
        float angle=(i*22.5f+(zero?8:0))*Mathf.Deg2Rad;
        Vector3 radial=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
        Vector3 tangent=new(-radial.z,0,radial.x);
        Vector3 center=radial*4.37f;
        Tube("PerimeterPanel",center-tangent*.28f+Vector3.up*.20f,
            center+tangent*.28f+Vector3.up*.20f,.045f,i%4==0?energy:trim);
        Disc("PanelFastener",.065f,.22f,.025f,dark,center);
    }
}
void BuildServiceNodes(bool zero,Material dark,Material trim,Material energy)
{
    for(int i=0;i<4;i++)
    {
        float a=(i*90+45+(zero?11:0))*Mathf.Deg2Rad;
        Vector3 radial=new(Mathf.Cos(a),0,Mathf.Sin(a));
        Vector3 tangent=new(-radial.z,0,radial.x);
        Vector3 p=radial*3.32f+Vector3.up*.18f;
        Disc("ServiceNode",.34f,.18f,.10f,trim,p-Vector3.up*.18f);
        Disc("ServiceCore",.19f,.30f,.035f,dark,p-Vector3.up*.18f);
        GameObject halo=ProductionMeshFactory.MeshObject("DF_ServiceHalo",root,
            ProductionMeshFactory.Ring(.27f,.025f,20,6),energy);
        halo.transform.localPosition=p+Vector3.up*.13f;
        halo.transform.localRotation=Quaternion.Euler(90,0,0);
        for(int side=-1;side<=1;side+=2)
        {
            Vector3 socket=p+tangent*(side*.26f);
            Tube("ServiceBrace",socket+Vector3.up*.02f,socket+Vector3.up*.34f,.025f,trim);
            Disc("ServiceIndicator",.055f,.43f,.02f,energy,socket-Vector3.up*.18f);
        }
        Tube("ServiceFeed",p-radial*.49f+Vector3.up*.12f,
            p-radial*.20f+Vector3.up*.12f,.028f,energy);
    }
}
void Disc(string n,float radius,float y,float half,Material mat){Disc(n,radius,y,half,mat,Vector3.zero);}void Disc(string n,float radius,float y,float half,Material mat,Vector3 offset){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Disc(radius,half,36),mat);g.transform.localPosition=offset+Vector3.up*y;}
void Ring(string n,float radius,float tube,float y,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Ring(radius,tube,36,8),mat);g.transform.localPosition=Vector3.up*y;}
void Tube(string n,Vector3 a,Vector3 b,float radius,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Tube(a,b,radius,8),mat);}
Material Mat(Color c,float metallic,float smooth,bool emissive=false){Shader sh=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new(sh);mats.Add(m);m.color=c;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*1.45f);}return m;}
void OnDestroy(){var owned=new HashSet<Mesh>();if(root!=null)foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&owned.Add(filter.sharedMesh))Destroy(filter.sharedMesh);foreach(Material m in mats)if(m!=null)Destroy(m);}
}
}
