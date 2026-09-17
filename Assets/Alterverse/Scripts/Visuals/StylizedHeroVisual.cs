using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class StylizedHeroVisual:MonoBehaviour
{
private HeroIdentity identity;private TeamMember team;private bool built;private Color accent,dark,teamEdge;private Transform root;private void Awake(){Build();}private void Start(){Build();}public void Build(){if(built)return;identity=GetComponent<HeroIdentity>();team=GetComponent<TeamMember>();if(identity==null)return;built=true;accent=StylizedArtDirection.HeroAccent(identity.Hero);dark=Color.Lerp(accent,new Color(.16f,.18f,.24f),.52f);teamEdge=team!=null&&team.Team==1?StylizedArtDirection.LumenEnergy:StylizedArtDirection.ZeroEnergy;Renderer baseRenderer=GetComponent<Renderer>();if(baseRenderer!=null)baseRenderer.enabled=false;root=new GameObject("DF_VisualRoot").transform;root.SetParent(transform,false);switch(identity.Hero){case HeroId.Kaelon:BuildKaelon();break;case HeroId.Nyra:BuildNyra();break;case HeroId.Elyra:BuildElyra();break;case HeroId.Mira:BuildMira();break;case HeroId.Rook:BuildRook();break;default:BuildVeyr();break;}float legWidth=(identity.Hero==HeroId.Kaelon||identity.Hero==HeroId.Rook)?0.24f:0.17f;CreateLegs(legWidth);CompleteArmor(legWidth);CreateSignature();if(GetComponent<HeroVisualMotion>()==null)gameObject.AddComponent<HeroVisualMotion>();if(GetComponent<FounderHeroVisual>()==null)gameObject.AddComponent<FounderHeroVisual>();}
private void CreateLegs(float width){Create("LegL",PrimitiveType.Capsule,new Vector3(-width,-.62f,0),new Vector3(.18f,.45f,.18f),dark);Create("LegR",PrimitiveType.Capsule,new Vector3(width,-.62f,0),new Vector3(.18f,.45f,.18f),dark);Create("BootL",PrimitiveType.Cube,new Vector3(-width,-.91f,.08f),new Vector3(.24f,.14f,.36f),accent);Create("BootR",PrimitiveType.Cube,new Vector3(width,-.91f,.08f),new Vector3(.24f,.14f,.36f),accent);}
private void CompleteArmor(float width)
{
Transform torso=root.Find("DF_Torso");
if(torso!=null){Vector3 s=torso.localScale;s.x*=1.12f;s.z*=1.08f;torso.localScale=s;}
for(int side=-1;side<=1;side+=2)
{
string suffix=side<0?"L":"R";
if(root.Find("DF_Arm"+suffix)==null&&identity.Hero!=HeroId.Rook)
{
Create("Arm"+suffix,PrimitiveType.Capsule,new Vector3(side*.38f,.12f,.05f),new Vector3(.19f,.35f,.19f),dark,new Vector3(0,0,side*10));
Create("Glove"+suffix,PrimitiveType.Cube,new Vector3(side*.4f,-.2f,.13f),new Vector3(.22f,.23f,.27f),accent);
}
Create("Knee"+suffix,PrimitiveType.Cube,new Vector3(side*width,-.57f,.13f),new Vector3(.23f,.22f,.14f),accent);
}
}
private void CreateSignature(){switch(identity.Hero){case HeroId.Veyr:Energy("NullShard",PrimitiveType.Cube,new Vector3(-.66f,.58f,-.12f),new Vector3(.08f,.52f,.08f),teamEdge,new Vector3(22,0,-28));break;case HeroId.Kaelon:Energy("AnchorCore",PrimitiveType.Sphere,new Vector3(0,.27f,.5f),Vector3.one*.16f,teamEdge);break;case HeroId.Nyra:Energy("SightNode",PrimitiveType.Sphere,new Vector3(.43f,.34f,.72f),Vector3.one*.11f,teamEdge);break;case HeroId.Elyra:Energy("FoldNode",PrimitiveType.Cube,new Vector3(0,1.38f,0),Vector3.one*.16f,teamEdge,new Vector3(35,45,15));break;case HeroId.Mira:Energy("LifeSeed",PrimitiveType.Sphere,new Vector3(0,.38f,-.42f),Vector3.one*.16f,teamEdge);break;case HeroId.Rook:Energy("ReactorScar",PrimitiveType.Cube,new Vector3(.18f,.26f,.42f),new Vector3(.24f,.07f,.04f),teamEdge,new Vector3(0,0,-14));break;}}
private void Energy(string n,PrimitiveType type,Vector3 p,Vector3 s,Color c,Vector3 r=default){GameObject g=Create(n,type,p,s,c,r);g.AddComponent<HeroEnergyDetail>();}
private void BuildVeyr(){Create("Torso",PrimitiveType.Capsule,new Vector3(0,.02f,0),new Vector3(.48f,.72f,.4f),dark);Create("ChestSlash",PrimitiveType.Cube,new Vector3(.04f,.22f,.22f),new Vector3(.08f,.62f,.06f),accent,new Vector3(0,0,-32));Create("Head",PrimitiveType.Sphere,new Vector3(0,.83f,0),new Vector3(.48f,.5f,.48f),dark);Create("Mask",PrimitiveType.Cube,new Vector3(0,.82f,.23f),new Vector3(.36f,.17f,.08f),accent);Create("Eye",PrimitiveType.Cube,new Vector3(.08f,.85f,.28f),new Vector3(.12f,.035f,.025f),teamEdge);Create("HoodL",PrimitiveType.Cube,new Vector3(-.24f,1.04f,-.02f),new Vector3(.12f,.32f,.22f),dark,new Vector3(0,0,-22));Create("HoodR",PrimitiveType.Cube,new Vector3(.24f,1.04f,-.02f),new Vector3(.12f,.32f,.22f),dark,new Vector3(0,0,22));Create("ArmL",PrimitiveType.Capsule,new Vector3(-.36f,.12f,.02f),new Vector3(.18f,.48f,.18f),dark,new Vector3(0,0,-14));Create("ArmR",PrimitiveType.Capsule,new Vector3(.36f,.12f,.02f),new Vector3(.18f,.48f,.18f),dark,new Vector3(0,0,14));Create("BladeR",PrimitiveType.Cube,new Vector3(.52f,-.02f,.32f),new Vector3(.08f,.72f,.11f),accent,new Vector3(25,0,-16));Create("BladeL",PrimitiveType.Cube,new Vector3(-.48f,.02f,.22f),new Vector3(.07f,.48f,.09f),teamEdge,new Vector3(18,0,20));}
private void BuildKaelon(){Create("Torso",PrimitiveType.Cube,new Vector3(0,.05f,0),new Vector3(.82f,.82f,.64f),dark);Create("Chest",PrimitiveType.Cube,new Vector3(0,.24f,.34f),new Vector3(.58f,.35f,.12f),accent);Create("Core",PrimitiveType.Cube,new Vector3(0,.24f,.415f),new Vector3(.18f,.12f,.035f),teamEdge,new Vector3(0,0,45));Create("Head",PrimitiveType.Cube,new Vector3(0,.88f,0),new Vector3(.52f,.48f,.5f),dark);Create("Visor",PrimitiveType.Cube,new Vector3(0,.91f,.27f),new Vector3(.42f,.11f,.06f),teamEdge);Create("Crest",PrimitiveType.Cube,new Vector3(0,1.18f,-.02f),new Vector3(.1f,.38f,.32f),accent,new Vector3(-12,0,0));Create("ShoulderL",PrimitiveType.Cube,new Vector3(-.62f,.43f,0),new Vector3(.5f,.36f,.72f),accent);Create("ShoulderR",PrimitiveType.Cube,new Vector3(.62f,.43f,0),new Vector3(.5f,.36f,.72f),accent);Create("Shield",PrimitiveType.Cube,new Vector3(-.78f,-.02f,.28f),new Vector3(.14f,.92f,.78f),teamEdge,new Vector3(0,0,-6));Create("ShieldCore",PrimitiveType.Cube,new Vector3(-.86f,.02f,.31f),new Vector3(.04f,.35f,.34f),accent,new Vector3(0,0,39));}
private void BuildNyra(){Create("Torso",PrimitiveType.Capsule,new Vector3(0,.04f,0),new Vector3(.42f,.68f,.38f),dark);Create("Chest",PrimitiveType.Cube,new Vector3(0,.2f,.2f),new Vector3(.36f,.32f,.07f),accent);Create("Head",PrimitiveType.Sphere,new Vector3(0,.82f,0),new Vector3(.45f,.48f,.45f),dark);Create("Visor",PrimitiveType.Cube,new Vector3(0,.84f,.23f),new Vector3(.36f,.1f,.06f),teamEdge);Create("Antenna",PrimitiveType.Cube,new Vector3(.22f,1.1f,0),new Vector3(.035f,.34f,.035f),accent,new Vector3(0,0,-15));Create("Rifle",PrimitiveType.Cube,new Vector3(.43f,.12f,.55f),new Vector3(.13f,.15f,1.42f),accent,new Vector3(0,4,-7));Create("Barrel",PrimitiveType.Cube,new Vector3(.43f,.12f,1.28f),new Vector3(.08f,.08f,.36f),teamEdge);Create("Scope",PrimitiveType.Cube,new Vector3(.43f,.3f,.55f),new Vector3(.13f,.12f,.32f),dark);Create("Cape",PrimitiveType.Cube,new Vector3(-.18f,.05f,-.27f),new Vector3(.58f,.68f,.06f),dark,new Vector3(8,0,8));}
private void BuildElyra(){Create("Torso",PrimitiveType.Capsule,new Vector3(0,.05f,0),new Vector3(.44f,.7f,.4f),dark);Create("ChestGem",PrimitiveType.Sphere,new Vector3(0,.25f,.23f),Vector3.one*.15f,teamEdge);Create("Head",PrimitiveType.Sphere,new Vector3(0,.84f,0),new Vector3(.46f,.5f,.46f),dark);Create("Face",PrimitiveType.Cube,new Vector3(0,.82f,.23f),new Vector3(.28f,.2f,.045f),accent);Create("CrownL",PrimitiveType.Cube,new Vector3(-.2f,1.16f,0),new Vector3(.09f,.38f,.09f),accent,new Vector3(0,0,-20));Create("CrownR",PrimitiveType.Cube,new Vector3(.2f,1.16f,0),new Vector3(.09f,.38f,.09f),teamEdge,new Vector3(0,0,20));Create("OrbitL",PrimitiveType.Sphere,new Vector3(.72f,.5f,.04f),Vector3.one*.24f,accent);Create("OrbitR",PrimitiveType.Sphere,new Vector3(-.68f,.18f,.18f),Vector3.one*.18f,teamEdge);Create("Mantle",PrimitiveType.Cube,new Vector3(0,.4f,-.25f),new Vector3(1.02f,.12f,.42f),accent);}
private void BuildMira(){Create("Torso",PrimitiveType.Capsule,new Vector3(0,.03f,0),new Vector3(.45f,.7f,.4f),dark);Create("ChestLeaf",PrimitiveType.Cube,new Vector3(0,.23f,.22f),new Vector3(.3f,.36f,.06f),accent,new Vector3(0,0,45));Create("Head",PrimitiveType.Sphere,new Vector3(0,.84f,0),new Vector3(.47f,.5f,.47f),dark);Create("Face",PrimitiveType.Cube,new Vector3(0,.82f,.235f),new Vector3(.3f,.21f,.045f),accent);Create("Crown",PrimitiveType.Sphere,new Vector3(0,1.16f,0),Vector3.one*.19f,teamEdge);Create("HornL",PrimitiveType.Cube,new Vector3(-.18f,1.2f,0),new Vector3(.06f,.32f,.06f),accent,new Vector3(0,0,-28));Create("HornR",PrimitiveType.Cube,new Vector3(.18f,1.2f,0),new Vector3(.06f,.32f,.06f),accent,new Vector3(0,0,28));Create("WingL",PrimitiveType.Cube,new Vector3(-.5f,.35f,-.26f),new Vector3(.78f,.1f,.42f),accent,new Vector3(0,-18,-18));Create("WingR",PrimitiveType.Cube,new Vector3(.5f,.35f,-.26f),new Vector3(.78f,.1f,.42f),accent,new Vector3(0,18,18));Create("Staff",PrimitiveType.Cylinder,new Vector3(.58f,.08f,.18f),new Vector3(.07f,.65f,.07f),dark,new Vector3(0,0,-8));Create("Bloom",PrimitiveType.Sphere,new Vector3(.68f,.73f,.18f),Vector3.one*.22f,accent);}
private void BuildRook(){Create("Torso",PrimitiveType.Cube,new Vector3(0,.02f,0),new Vector3(.78f,.76f,.64f),dark);Create("ChestPlate",PrimitiveType.Cube,new Vector3(0,.2f,.34f),new Vector3(.62f,.34f,.1f),accent,new Vector3(0,0,-5));Create("Head",PrimitiveType.Sphere,new Vector3(0,.82f,0),new Vector3(.55f,.48f,.55f),dark);Create("Brow",PrimitiveType.Cube,new Vector3(0,.87f,.28f),new Vector3(.44f,.1f,.1f),accent,new Vector3(0,0,-5));Create("Jaw",PrimitiveType.Cube,new Vector3(0,.7f,.28f),new Vector3(.42f,.18f,.14f),dark);Create("Mohawk",PrimitiveType.Cube,new Vector3(0,1.14f,-.02f),new Vector3(.1f,.34f,.42f),teamEdge,new Vector3(-12,0,0));Create("GauntletL",PrimitiveType.Cube,new Vector3(-.64f,-.02f,.22f),new Vector3(.5f,.56f,.66f),dark,new Vector3(0,0,-8));Create("GauntletR",PrimitiveType.Cube,new Vector3(.68f,-.02f,.26f),new Vector3(.66f,.66f,.78f),accent,new Vector3(0,0,8));Create("Knuckle",PrimitiveType.Cube,new Vector3(.72f,-.03f,.7f),new Vector3(.48f,.18f,.16f),teamEdge);Create("BackPlate",PrimitiveType.Cube,new Vector3(0,.24f,-.36f),new Vector3(.9f,.58f,.14f),teamEdge);}
// Meshes are generated once per hero and shared by its parts. No primitive colliders.
private Mesh armorMesh,limbMesh,helmetMesh;
private readonly System.Collections.Generic.List<Material> ownedMaterials=new System.Collections.Generic.List<Material>();
private GameObject Create(string n,PrimitiveType type,Vector3 pos,Vector3 scale,Color color,Vector3 rotation=default,Transform parent=null)
{
GameObject part=new GameObject("DF_"+n);
part.transform.SetParent(parent!=null?parent:root,false);
part.transform.localPosition=pos;part.transform.localScale=scale;part.transform.localRotation=Quaternion.Euler(rotation);
Mesh mesh;
if(type==PrimitiveType.Capsule||type==PrimitiveType.Cylinder)
{
if(limbMesh==null)limbMesh=BuildProfile("DF_TaperedArmor",new float[]{-1f,-.72f,.55f,1f},new float[]{.28f,.48f,.5f,.32f});
mesh=limbMesh;
}
else if(type==PrimitiveType.Sphere)
{
if(helmetMesh==null)helmetMesh=BuildProfile("DF_FacetedHelmet",new float[]{-.5f,-.28f,.27f,.5f},new float[]{.25f,.48f,.46f,.24f});
mesh=helmetMesh;
}
else
{
if(armorMesh==null)armorMesh=BuildBeveledArmor();
mesh=armorMesh;
}
part.AddComponent<MeshFilter>().sharedMesh=mesh;
MeshRenderer renderer=part.AddComponent<MeshRenderer>();
Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
if(shader!=null)
{
Material material=new Material(shader);material.color=color;
if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.28f);
if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.16f);
ownedMaterials.Add(material);renderer.sharedMaterial=material;
}
return part;
}
private static Mesh BuildProfile(string name,float[] heights,float[] radii)
{
var vertices=new System.Collections.Generic.List<Vector3>();
var triangles=new System.Collections.Generic.List<int>();
const int sides=8;
for(int ring=0;ring<heights.Length-1;ring++)
for(int i=0;i<sides;i++)
{
float a=(i+.5f)*Mathf.PI*2/sides,b=(i+1.5f)*Mathf.PI*2/sides;
Vector3 lowA=new Vector3(Mathf.Cos(a)*radii[ring],heights[ring],Mathf.Sin(a)*radii[ring]);
Vector3 lowB=new Vector3(Mathf.Cos(b)*radii[ring],heights[ring],Mathf.Sin(b)*radii[ring]);
Vector3 highA=new Vector3(Mathf.Cos(a)*radii[ring+1],heights[ring+1],Mathf.Sin(a)*radii[ring+1]);
Vector3 highB=new Vector3(Mathf.Cos(b)*radii[ring+1],heights[ring+1],Mathf.Sin(b)*radii[ring+1]);
AddFace(vertices,triangles,new[]{lowA,highA,highB,lowB});
}
for(int cap=0;cap<2;cap++)
{
int ring=cap==0?0:heights.Length-1;
var face=new Vector3[sides];
for(int i=0;i<sides;i++){float angle=((cap==0?i:sides-1-i)+.5f)*Mathf.PI*2/sides;face[i]=new Vector3(Mathf.Cos(angle)*radii[ring],heights[ring],Mathf.Sin(angle)*radii[ring]);}
AddFace(vertices,triangles,face);
}
Mesh mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
}
private static Mesh BuildBeveledArmor()
{
// True beveled box: six panels, twelve bevels and eight corner triangles.
var vertices=new System.Collections.Generic.List<Vector3>();
var triangles=new System.Collections.Generic.List<int>();
const float h=.5f,b=.38f;
for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
{
int u=(axis+1)%3,v=(axis+2)%3;var face=new Vector3[4];
int[] us={-1,1,1,-1},vs={-1,-1,1,1};
for(int i=0;i<4;i++){Vector3 p=Vector3.zero;p[axis]=sign*h;p[u]=us[i]*b;p[v]=vs[i]*b;face[i]=p;}
OutwardFace(vertices,triangles,face);
}
for(int a=0;a<3;a++)for(int d=a+1;d<3;d++)for(int sa=-1;sa<=1;sa+=2)for(int sd=-1;sd<=1;sd+=2)
{
int remaining=3-a-d;
Vector3 p=Vector3.zero;p[a]=sa*h;p[d]=sd*b;p[remaining]=-b;
Vector3 q=p;q[remaining]=b;
Vector3 r=q;r[a]=sa*b;r[d]=sd*h;
Vector3 s=r;s[remaining]=-b;
OutwardFace(vertices,triangles,new[]{p,q,r,s});
}
for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
OutwardFace(vertices,triangles,new[]{new Vector3(x*h,y*b,z*b),new Vector3(x*b,y*h,z*b),new Vector3(x*b,y*b,z*h)});
Mesh mesh=new Mesh{name="DF_BeveledArmor"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
}
private static void OutwardFace(System.Collections.Generic.List<Vector3> vertices,System.Collections.Generic.List<int> triangles,Vector3[] face)
{
Vector3 center=Vector3.zero;foreach(Vector3 point in face)center+=point;
if(Vector3.Dot(Vector3.Cross(face[1]-face[0],face[2]-face[0]),center)<0)System.Array.Reverse(face);
AddFace(vertices,triangles,face);
}
private static void AddFace(System.Collections.Generic.List<Vector3> vertices,System.Collections.Generic.List<int> triangles,Vector3[] points)
{
int first=vertices.Count;vertices.AddRange(points);
for(int i=1;i<points.Length-1;i++){triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);}
}
private void OnDestroy()
{
if(armorMesh!=null)Destroy(armorMesh);if(limbMesh!=null)Destroy(limbMesh);if(helmetMesh!=null)Destroy(helmetMesh);
foreach(Material material in ownedMaterials)if(material!=null)Destroy(material);
}
}
}
