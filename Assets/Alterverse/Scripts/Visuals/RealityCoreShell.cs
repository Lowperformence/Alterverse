using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class RealityCoreShell:MonoBehaviour
{
Transform root;Material metal,secondary,energy;
Alterverse.Combat.Health health;Alterverse.Combat.RealityCore core;Color energyColor;float hitUntil;
void Update(){if(energy==null)return;bool vulnerable=core!=null&&core.IsVulnerable;float hit=Time.unscaledTime<hitUntil?1:0;Color color=Color.Lerp(energyColor,Color.white,hit*.82f);float pulse=vulnerable?1.55f+.22f*Mathf.Sin(Time.time*3.4f):.30f;energy.SetColor("_EmissionColor",color*pulse);}
void OnHit(float damage,float absorbed,GameObject source){if(damage>0)hitUntil=Time.unscaledTime+.18f;}
public void Build(bool zero){if(root!=null)return;EnsureSolidCollision();health=GetComponent<Alterverse.Combat.Health>();core=GetComponent<Alterverse.Combat.RealityCore>();if(health!=null)health.Damaged+=OnHit;root=new GameObject("DF_RealityCoreProduction").transform;root.SetParent(transform,false);root.localScale=Vector3.one*1.10f;Color e=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;energyColor=e;metal=Mat(Color.Lerp(StylizedArtDirection.Ink,zero?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark,.55f),.74f,.34f);secondary=Mat(Color.Lerp(Color.gray,zero?StylizedArtDirection.ZeroMid:StylizedArtDirection.LumenMid,.35f),.62f,.48f);energy=Mat(Color.Lerp(e,Color.white,.04f),.10f,.76f,true);StructureSurfaceMaterials.Apply(metal,StructureFinish.Metal);StructureSurfaceMaterials.Apply(secondary,StructureFinish.Deck);BuildBase();BuildFrame(zero);BuildChronosphere(zero);HorizontalRing("CoreHalo",1.42f,.035f,1.73f,energy);}
void EnsureSolidCollision()
{
    // The production shell reaches roughly 1.55 m from the core centre after its
    // visual scale is applied. The old bootstrap collider (radius .9 m) only
    // covered the reactor centre, so characters could walk visibly inside the shell.
    // Keep the collider on the actual structure object so the surrounding 4.75 m
    // production deck remains fully walkable.
    CapsuleCollider body=GetComponent<CapsuleCollider>()??gameObject.AddComponent<CapsuleCollider>();
    body.isTrigger=false;
    body.direction=1;
    body.radius=1.62f;
    body.height=3.40f;
    body.center=new Vector3(0,1.60f,0);
}
void BuildBase(){Disc("CoreLowerHousing",1.30f,.20f,.20f,metal);Disc("CoreUpperHousing",1.02f,.36f,.10f,secondary);Disc("CoreRecess",.70f,.425f,.025f,metal);HorizontalRing("CoreBaseEnergy",1.12f,.056f,.43f,energy);for(int i=0;i<6;i++){float a=i*Mathf.PI/3f;Vector3 from=new(Mathf.Cos(a)*.82f,.44f,Mathf.Sin(a)*.82f),to=new(Mathf.Cos(a)*1.20f,.25f,Mathf.Sin(a)*1.20f);Tube("PowerFeed",from,to,.070f,i%2==0?energy:secondary);}}
void BuildFrame(bool zero){for(int i=0;i<4;i++){float a=(i*90+45)*Mathf.Deg2Rad;Vector3 foot=new(Mathf.Cos(a)*1.18f,.48f,Mathf.Sin(a)*1.18f);Vector3 shoulder=new(Mathf.Cos(a)*.78f,1.65f,Mathf.Sin(a)*.78f);Tube("ChronoStrut",foot,shoulder,.105f,metal);Vector3 brace=new(Mathf.Cos(a)*.96f,1.18f,Mathf.Sin(a)*.96f);Tube("EnergyConduit",foot+Vector3.up*.08f,brace,.040f,energy);Node(shoulder,.15f,secondary);}}
void BuildChronosphere(bool zero){Vector3 center=new(0,1.72f,0);Node(center,.36f,energy);float[] radii={.72f,.96f,1.18f};Vector3[] rotations={new(68,0,0),new(18,34,58),new(42,72,18)};for(int i=0;i<3;i++){GameObject ring=ProductionMeshFactory.MeshObject("DF_ChronoRing_"+i,root,ProductionMeshFactory.Ring(radii[i],.052f,i==2?32:28,8),i==1?secondary:metal);ring.transform.localPosition=center;ring.transform.localRotation=Quaternion.Euler(rotations[i]);var motion=ring.AddComponent<ChronoRingMotion>();motion.Configure((i%2==0?1:-1)*(12f+i*7f));for(int j=0;j<4;j++){float a=j*Mathf.PI*.5f;Vector3 p=center+new Vector3(Mathf.Cos(a)*radii[i],0,Mathf.Sin(a)*radii[i]);Node(p,.062f,energy);}}for(int i=0;i<3;i++){float a=(i*120+(zero?18:0))*Mathf.Deg2Rad;Vector3 p=center+new Vector3(Mathf.Cos(a)*1.38f,.12f*Mathf.Sin(a*2),Mathf.Sin(a)*1.38f);Node(p,.11f,secondary);Tube("FieldArm",new Vector3(Mathf.Cos(a)*.8f,1.62f,Mathf.Sin(a)*.8f),p,.045f,secondary);}}
void HorizontalRing(string n,float radius,float width,float y,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Ring(radius,width,32,8),mat);g.transform.localPosition=new Vector3(0,y,0);}
void Disc(string n,float radius,float y,float height,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Disc(radius,height,32),mat);g.transform.localPosition=new Vector3(0,y,0);}
void Tube(string n,Vector3 a,Vector3 b,float r,Material mat){ProductionMeshFactory.MeshObject("DF_"+n,root,ProductionMeshFactory.Tube(a,b,r,8),mat);}
void Node(Vector3 p,float r,Material mat){GameObject g=ProductionMeshFactory.MeshObject("DF_CoreNode",root,ProductionMeshFactory.Ring(r*.62f,r*.28f,20,7),mat);g.transform.localPosition=p;g.transform.localRotation=Quaternion.Euler(90,0,0);}
Material Mat(Color c,float metallic,float smooth,bool emissive=false){Shader sh=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new(sh);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);m.color=c;if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(emissive){m.EnableKeyword("_EMISSION");if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",c*1.65f);}return m;}
void OnDestroy(){if(health!=null)health.Damaged-=OnHit;var owned=new System.Collections.Generic.HashSet<Mesh>();if(root!=null)foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&owned.Add(filter.sharedMesh))Destroy(filter.sharedMesh);if(metal!=null)Destroy(metal);if(secondary!=null)Destroy(secondary);if(energy!=null)Destroy(energy);}

}
}

