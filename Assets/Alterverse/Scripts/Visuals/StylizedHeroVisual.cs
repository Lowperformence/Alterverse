using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed partial class StylizedHeroVisual:MonoBehaviour
{
private bool voidfall,weaponOverdrive;private HeroIdentity identity;private TeamMember team;private bool built;private Color accent,dark,teamEdge;private Transform root;private void Awake(){Build();}private void Start(){Build();}public void Build(){if(built)return;identity=GetComponent<HeroIdentity>();team=GetComponent<TeamMember>();if(identity==null)return;built=true;var skinPreview=GetComponent<Alterverse.Shop.SkinPreviewSelection>();voidfall=skinPreview!=null?skinPreview.Voidfall:GetComponent<HeroRespawn>()!=null&&Alterverse.Shop.UniversalSkin.Equipped;weaponOverdrive=skinPreview!=null?skinPreview.WeaponOverdrive:GetComponent<HeroRespawn>()!=null&&Alterverse.Shop.WeaponSkinInventory.Equipped(identity.Hero);accent=StylizedArtDirection.HeroAccent(identity.Hero);dark=Color.Lerp(accent,new Color(.13f,.16f,.21f),.85f);teamEdge=team!=null&&team.Team==1?StylizedArtDirection.LumenEnergy:StylizedArtDirection.ZeroEnergy;Renderer baseRenderer=GetComponent<Renderer>();if(baseRenderer!=null)baseRenderer.enabled=false;root=new GameObject("DF_VisualRoot").transform;root.SetParent(transform,false);switch(identity.Hero){case HeroId.Kaelon:BuildKaelon();break;case HeroId.Nyra:BuildNyra();break;case HeroId.Elyra:BuildElyra();break;case HeroId.Mira:BuildMira();break;case HeroId.Rook:BuildRook();break;default:BuildVeyr();break;}float legWidth=(identity.Hero==HeroId.Kaelon||identity.Hero==HeroId.Rook)?0.24f:0.17f;CreateLegs(legWidth);CompleteArmor(legWidth);CreateSignature();BuildCharacterFinish(legWidth);BindArmor();if(weaponOverdrive)ApplyWeaponOverdrive();CombineMovingPanels();CombineStaticPanels();if(voidfall)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).Build(root,skinPreview!=null);if(GetComponent<HeroVisualMotion>()==null)gameObject.AddComponent<HeroVisualMotion>();if(GetComponent<FounderHeroVisual>()==null)gameObject.AddComponent<FounderHeroVisual>();}
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
}
if(identity.Hero!=HeroId.Rook)Create("Glove"+suffix,PrimitiveType.Cube,new Vector3(side*.4f,-.2f,.13f),new Vector3(.22f,.23f,.27f),dark);
Create("Knee"+suffix,PrimitiveType.Cube,new Vector3(side*width,-.57f,.13f),new Vector3(.23f,.22f,.14f),accent);
}
}
private void CreateSignature()
{
// Team color is a restrained, consistent shoulder insignia; hero identity keeps its own palette.
Create("TeamInsignia",PrimitiveType.Cube,new Vector3(.33f,.49f,.21f),new Vector3(.19f,.07f,.05f),teamEdge);
}
private Color armorLight=>new Color(.7f,.74f,.77f);
private Color cloth=>new Color(.075f,.09f,.13f);
private void BuildVeyr()
{
Create("Torso",PrimitiveType.Capsule,new Vector3(0,.02f,0),new Vector3(.66f,.65f,.44f),dark);
Create("Head",PrimitiveType.Sphere,new Vector3(0,.78f,.06f),new Vector3(.43f,.48f,.43f),armorLight);
Panel("Mask",new[]{new Vector2(-.24f,.14f),new Vector2(.22f,.09f),new Vector2(.13f,-.22f),new Vector2(-.18f,-.13f)},.12f,new Vector3(0,.79f,.22f),cloth);
Create("Eye",PrimitiveType.Cube,new Vector3(.03f,.83f,.3f),new Vector3(.25f,.04f,.03f),teamEdge,new Vector3(0,0,-13));
Panel("Mantle",new[]{new Vector2(-.62f,.42f),new Vector2(-.12f,.56f),new Vector2(.21f,.21f),new Vector2(-.43f,.06f)},.19f,new Vector3(0,.22f,-.05f),accent);
Panel("CoatL",new[]{new Vector2(-.4f,.2f),new Vector2(-.02f,.2f),new Vector2(-.16f,-.85f),new Vector2(-.66f,-.66f)},.075f,new Vector3(0,-.08f,-.22f),cloth);
Panel("CoatR",new[]{new Vector2(.03f,.2f),new Vector2(.32f,.2f),new Vector2(.58f,-.58f),new Vector2(.23f,-.77f)},.075f,new Vector3(0,-.08f,-.22f),accent);
Create("ArmL",PrimitiveType.Capsule,new Vector3(-.38f,.05f,.02f),new Vector3(.19f,.38f,.21f),dark,new Vector3(0,0,-17));
Create("ArmR",PrimitiveType.Capsule,new Vector3(.35f,.06f,.06f),new Vector3(.19f,.4f,.22f),dark,new Vector3(0,0,18));
Panel("BladeR",new[]{new Vector2(-.09f,-.36f),new Vector2(.03f,-.46f),new Vector2(.35f,.65f),new Vector2(.13f,.4f)},.08f,new Vector3(.48f,-.04f,.32f),armorLight);
Panel("BladeL",new[]{new Vector2(-.08f,-.26f),new Vector2(.04f,-.28f),new Vector2(.23f,.4f),new Vector2(.08f,.32f)},.07f,new Vector3(-.49f,-.12f,.24f),accent);
Create("ChestSlash",PrimitiveType.Cube,new Vector3(.03f,.16f,.24f),new Vector3(.07f,.54f,.035f),teamEdge,new Vector3(0,0,-25));
}
private void BuildKaelon()
{
Create("Torso",PrimitiveType.Capsule,new Vector3(0,.02f,0),new Vector3(1.1f,.62f,.72f),dark);
Panel("Chest",new[]{new Vector2(-.45f,.3f),new Vector2(.45f,.3f),new Vector2(.3f,-.23f),new Vector2(0,-.38f),new Vector2(-.3f,-.23f)},.15f,new Vector3(0,.1f,.3f),armorLight);
Create("Head",PrimitiveType.Sphere,new Vector3(0,.83f,0),new Vector3(.47f,.47f,.47f),dark);
Create("Visor",PrimitiveType.Cube,new Vector3(0,.88f,.24f),new Vector3(.33f,.07f,.05f),teamEdge);
for(int side=-1;side<=1;side+=2)
Panel(side<0?"ShoulderL":"ShoulderR",new[]{new Vector2(-.29f,.18f),new Vector2(.23f,.29f),new Vector2(.36f,-.07f),new Vector2(-.2f,-.16f)},.55f,new Vector3(side*.65f,.45f,0),armorLight);
Panel("Shield",new[]{new Vector2(-.38f,.55f),new Vector2(.22f,.62f),new Vector2(.38f,.24f),new Vector2(.22f,-.48f),new Vector2(-.15f,-.62f),new Vector2(-.42f,-.27f)},.18f,new Vector3(-.77f,.04f,.37f),dark);
Panel("ShieldInlay",new[]{new Vector2(-.2f,.32f),new Vector2(.14f,.36f),new Vector2(.21f,-.13f),new Vector2(0,-.38f),new Vector2(-.24f,-.17f)},.025f,new Vector3(-.77f,.04f,.48f),accent);
Create("ShieldLine",PrimitiveType.Cube,new Vector3(-.78f,.1f,.51f),new Vector3(.055f,.65f,.025f),teamEdge);
Create("Core",PrimitiveType.Cube,new Vector3(0,.18f,.43f),new Vector3(.17f,.17f,.03f),teamEdge,new Vector3(0,0,45));
}
private void BuildNyra()
{
Create("Torso",PrimitiveType.Capsule,new Vector3(0,.06f,0),new Vector3(.58f,.65f,.4f),cloth);
Panel("Chest",new[]{new Vector2(-.28f,.3f),new Vector2(.28f,.22f),new Vector2(.15f,-.28f),new Vector2(-.18f,-.24f)},.1f,new Vector3(0,.12f,.18f),armorLight);
Create("Head",PrimitiveType.Sphere,new Vector3(0,.82f,0),new Vector3(.43f,.47f,.44f),dark);
Panel("Brow",new[]{new Vector2(-.28f,.11f),new Vector2(.24f,.14f),new Vector2(.31f,-.03f),new Vector2(-.23f,-.07f)},.24f,new Vector3(0,.92f,.02f),accent);
Create("Visor",PrimitiveType.Cube,new Vector3(0,.85f,.23f),new Vector3(.3f,.055f,.04f),teamEdge);
Panel("Cape",new[]{new Vector2(-.3f,.4f),new Vector2(.1f,.36f),new Vector2(.4f,-.55f),new Vector2(-.4f,-.7f)},.055f,new Vector3(-.13f,-.02f,-.23f),accent);
Create("Rifle",PrimitiveType.Cube,new Vector3(.44f,.13f,.58f),new Vector3(.22f,.24f,1.42f),dark,new Vector3(0,0,-4));
Create("Stock",PrimitiveType.Cube,new Vector3(.44f,.09f,-.12f),new Vector3(.26f,.32f,.35f),armorLight);
Create("Barrel",PrimitiveType.Cylinder,new Vector3(.44f,.13f,1.35f),new Vector3(.095f,.3f,.095f),armorLight,new Vector3(90,0,0));
Create("WeaponRail",PrimitiveType.Cube,new Vector3(.44f,.27f,.68f),new Vector3(.08f,.06f,.88f),accent);
Create("Scope",PrimitiveType.Cube,new Vector3(.44f,.38f,.5f),new Vector3(.13f,.13f,.32f),teamEdge);
}
private void BuildElyra()
{
Create("Torso",PrimitiveType.Capsule,new Vector3(0,.15f,0),new Vector3(.57f,.62f,.39f),dark);
Create("Head",PrimitiveType.Sphere,new Vector3(0,.88f,0),new Vector3(.42f,.48f,.42f),armorLight);
Create("Face",PrimitiveType.Cube,new Vector3(0,.85f,.21f),new Vector3(.24f,.17f,.05f),cloth);
Panel("Mantle",new[]{new Vector2(-.6f,.22f),new Vector2(-.23f,.42f),new Vector2(.27f,.42f),new Vector2(.6f,.19f),new Vector2(0,-.14f)},.26f,new Vector3(0,.39f,-.07f),accent);
for(int side=-1;side<=1;side+=2)
Panel(side<0?"RobeL":"RobeR",new[]{new Vector2(-.22f,.37f),new Vector2(.2f,.3f),new Vector2(.37f,-.64f),new Vector2(-.33f,-.88f)},.10f,new Vector3(side*.25f,-.04f,-.17f),side<0?cloth:dark);
Panel("OrbitL",new[]{new Vector2(0,.35f),new Vector2(.2f,0),new Vector2(0,-.26f),new Vector2(-.16f,0)},.15f,new Vector3(-.72f,.46f,.03f),armorLight);
Panel("OrbitR",new[]{new Vector2(0,.26f),new Vector2(.16f,0),new Vector2(0,-.26f),new Vector2(-.16f,0)},.14f,new Vector3(.72f,.28f,.03f),accent);
Create("ChestGem",PrimitiveType.Cube,new Vector3(0,.26f,.23f),new Vector3(.17f,.23f,.035f),teamEdge,new Vector3(0,0,45));
}
private void BuildMira()
{
Create("Torso",PrimitiveType.Capsule,new Vector3(0,.07f,0),new Vector3(.64f,.64f,.43f),armorLight);
Create("Head",PrimitiveType.Sphere,new Vector3(0,.84f,0),new Vector3(.44f,.48f,.44f),dark);
Create("Face",PrimitiveType.Cube,new Vector3(0,.82f,.22f),new Vector3(.29f,.2f,.045f),armorLight);
Create("Brow",PrimitiveType.Cube,new Vector3(0,1.04f,.06f),new Vector3(.46f,.12f,.43f),accent,new Vector3(0,0,-7));
for(int side=-1;side<=1;side+=2)
{
Panel(side<0?"WingL":"WingR",new[]{new Vector2(-.15f,-.31f),new Vector2(.12f,-.4f),new Vector2(.26f,.36f),new Vector2(.07f,.66f),new Vector2(-.18f,.34f)},.16f,new Vector3(side*.55f,.48f,-.3f),accent);
Create(side<0?"ProjectorL":"ProjectorR",PrimitiveType.Cube,new Vector3(side*.53f,.58f,-.19f),new Vector3(.07f,.55f,.03f),teamEdge);
Panel(side<0?"CoatL":"CoatR",new[]{new Vector2(-.18f,.3f),new Vector2(.18f,.3f),new Vector2(.25f,-.64f),new Vector2(-.25f,-.72f)},.06f,new Vector3(side*.24f,-.1f,-.16f),armorLight);
}
Create("Staff",PrimitiveType.Cylinder,new Vector3(.7f,.0f,.19f),new Vector3(.075f,.8f,.075f),dark,new Vector3(0,0,-8));
Panel("Emitter",new[]{new Vector2(0,.28f),new Vector2(.24f,.09f),new Vector2(.13f,-.21f),new Vector2(-.13f,-.21f),new Vector2(-.24f,.09f)},.1f,new Vector3(.82f,.85f,.2f),accent);
Create("ChestLeaf",PrimitiveType.Cube,new Vector3(0,.19f,.25f),new Vector3(.16f,.32f,.04f),teamEdge);
}
private void BuildRook()
{
Create("Torso",PrimitiveType.Capsule,new Vector3(0,.04f,0),new Vector3(1f,.59f,.73f),dark);
Panel("ChestPlate",new[]{new Vector2(-.42f,.3f),new Vector2(.4f,.19f),new Vector2(.34f,-.29f),new Vector2(-.36f,-.35f)},.13f,new Vector3(0,.12f,.32f),accent);
Create("Head",PrimitiveType.Sphere,new Vector3(-.04f,.75f,.04f),new Vector3(.5f,.48f,.5f),armorLight);
Create("Brow",PrimitiveType.Cube,new Vector3(-.04f,.84f,.28f),new Vector3(.43f,.1f,.1f),dark,new Vector3(0,0,-9));
Create("Jaw",PrimitiveType.Cube,new Vector3(-.04f,.64f,.26f),new Vector3(.4f,.18f,.17f),dark);
Create("GauntletL",PrimitiveType.Capsule,new Vector3(-.65f,-.08f,.18f),new Vector3(.42f,.37f,.51f),dark,new Vector3(0,0,-8));
Create("GauntletR",PrimitiveType.Capsule,new Vector3(.7f,-.05f,.24f),new Vector3(.76f,.49f,.86f),accent,new Vector3(0,0,12));
for(int i=0;i<3;i++)Create("Knuckle"+i,PrimitiveType.Cube,new Vector3(.48f+i*.22f,-.11f,.65f),new Vector3(.17f,.24f,.16f),armorLight);
Panel("BackPlate",new[]{new Vector2(-.53f,.31f),new Vector2(.4f,.45f),new Vector2(.55f,-.23f),new Vector2(-.4f,-.33f)},.16f,new Vector3(0,.25f,-.32f),dark);
Create("Exhaust",PrimitiveType.Cylinder,new Vector3(-.42f,.62f,-.25f),new Vector3(.15f,.37f,.15f),armorLight,new Vector3(0,0,18));
Create("Reactor",PrimitiveType.Cube,new Vector3(.15f,.2f,.4f),new Vector3(.23f,.08f,.03f),teamEdge,new Vector3(0,0,-12));
}
// Meshes are generated once per hero and shared by its parts. No primitive colliders.
private Mesh armorMesh,limbMesh,helmetMesh;
private readonly System.Collections.Generic.List<Mesh> detailMeshes=new();
private readonly System.Collections.Generic.Dictionary<(Color, bool),Material> palette=new();
private readonly System.Collections.Generic.List<Material> ownedMaterials=new System.Collections.Generic.List<Material>();
private GameObject Create(string n,PrimitiveType type,Vector3 pos,Vector3 scale,Color color,Vector3 rotation=default,Transform parent=null)
{
GameObject part=new GameObject("DF_"+n);
part.transform.SetParent(parent!=null?parent:root,false);
part.transform.localPosition=pos;part.transform.localScale=scale;part.transform.localRotation=Quaternion.Euler(rotation);
Mesh mesh;
if(n=="Torso")
{
mesh=BuildCuirass();detailMeshes.Add(mesh);
}
else if(type==PrimitiveType.Capsule||type==PrimitiveType.Cylinder)
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
renderer.sharedMaterial=MaterialFor(color, IsEnergySurface(n));
return part;
}
private static bool IsEnergySurface(string name) => name=="Eye"||name=="Visor"||name=="Core"||name=="ChestGem"||name=="ChestLeaf"||name=="Reactor"||name=="ShieldLine"||name=="TeamInsignia"||name=="ProjectorL"||name=="ProjectorR"||name=="ChestSlash";
private Material MaterialFor(Color color, bool energy=false)
{
if(palette.TryGetValue((color,energy),out Material cached))return cached;
Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
if(shader==null)return null;
Color finish=color;
// Preserve team-colored surfaces and all signature energy; only armor and fabric receive the finish.
if(voidfall&&!energy&&color!=teamEdge)
    finish=color==armorLight?new Color(.26f,.16f,.36f):color==dark||color==cloth?new Color(.027f,.018f,.045f):Color.Lerp(color,new Color(.24f,.20f,.32f),.55f);
Material material=new Material(shader){name=voidfall?"DF_VoidfallFinish":"DF_HeroPalette",color=finish};
if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",finish);
bool fabric=color==cloth;bool matte=color==dark;
if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",fabric?.08f:energy?.55f:matte?.18f:voidfall?.56f:.48f);
if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",fabric?0f:energy?.12f:matte?.08f:voidfall?.52f:.65f);
if(energy&&material.HasProperty("_EmissionColor")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.3f);}
palette.Add((color,energy),material);ownedMaterials.Add(material);return material;
}
private void Panel(string name,Vector2[] outline,float depth,Vector3 position,Color color)
{
var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
// Convex armor contours use a narrow bevel, so front planes catch light independently
// from side walls. The silhouette and animated part names remain unchanged.
var contour=(Vector2[])outline.Clone();
if(Vector3.Cross((Vector3)(contour[1]-contour[0]),(Vector3)(contour[2]-contour[0])).z<0)System.Array.Reverse(contour);
Vector2 center=Vector2.zero;foreach(Vector2 point in contour)center+=point;center/=contour.Length;
float bevel=Mathf.Min(depth*.22f,.025f);
var front=new Vector3[contour.Length];var back=new Vector3[contour.Length];
for(int i=0;i<contour.Length;i++)
{
Vector2 inset=Vector2.Lerp(contour[i],center,.09f);
front[i]=new Vector3(inset.x,inset.y,depth*.5f);
back[contour.Length-1-i]=new Vector3(inset.x,inset.y,-depth*.5f);
}
AddFace(vertices,triangles,front);AddFace(vertices,triangles,back);
for(int i=0;i<contour.Length;i++)
{
int next=(i+1)%contour.Length;
Vector3 a=new Vector3(contour[i].x,contour[i].y,depth*.5f-bevel);
Vector3 b=new Vector3(contour[next].x,contour[next].y,depth*.5f-bevel);
Vector3 ba=new Vector3(a.x,a.y,-a.z),bb=new Vector3(b.x,b.y,-b.z);
Vector3 fa=front[i],fb=front[next];
AddFace(vertices,triangles,new[]{fa,a,b,fb});
AddFace(vertices,triangles,new[]{b,a,ba,bb});
AddFace(vertices,triangles,new[]{bb,ba,new Vector3(fa.x,fa.y,-fa.z),new Vector3(fb.x,fb.y,-fb.z)});
}
Mesh mesh=new Mesh{name="DF_Contour_"+name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();detailMeshes.Add(mesh);
GameObject part=new GameObject("DF_"+name);part.transform.SetParent(root,false);part.transform.localPosition=position;
part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=MaterialFor(color);
}
private void ApplyWeaponOverdrive()
{
Alterverse.Shop.WeaponSkinItem skin=Alterverse.Shop.WeaponSkinCatalog.For(identity.Hero);
Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");if(shader==null)return;
Color glow=skin.Glow;
Material weaponMaterial=new Material(shader){name="DF_WeaponOverdrive_"+identity.Hero,color=glow};
if(weaponMaterial.HasProperty("_BaseColor"))weaponMaterial.SetColor("_BaseColor",glow);
if(weaponMaterial.HasProperty("_Metallic"))weaponMaterial.SetFloat("_Metallic",.32f);
if(weaponMaterial.HasProperty("_Smoothness"))weaponMaterial.SetFloat("_Smoothness",.72f);
if(weaponMaterial.HasProperty("_EmissionColor")){weaponMaterial.EnableKeyword("_EMISSION");weaponMaterial.SetColor("_EmissionColor",glow*3.4f);}
ownedMaterials.Add(weaponMaterial);

switch(identity.Hero)
{
case HeroId.Veyr:
    ScaleAndGlow("BladeR",1.5f,weaponMaterial);
    break;
case HeroId.Kaelon:
    ScaleAndGlow("HammerGrip",1.5f,weaponMaterial);
    ScaleAndGlow("AnchorHead",1.5f,weaponMaterial);
    break;
case HeroId.Nyra:
    GroupAndScaleWeapon("DF_WeaponOverdrivePivot",new Vector3(.44f,.13f,.72f),1.5f,weaponMaterial,
        "Rifle","Stock","Barrel","WeaponRail","Scope","RifleReceiver","RifleMagazine","MuzzleBrake","ScopeLens","BarrelRail-1","BarrelRail1");
    break;
case HeroId.Elyra:
    ScaleAndGlow("OrbitL",1.5f,weaponMaterial);
    ScaleAndGlow("OrbitR",1.5f,weaponMaterial);
    break;
case HeroId.Mira:
    GroupAndScaleWeapon("DF_WeaponOverdrivePivot",new Vector3(.72f,.05f,.20f),1.5f,weaponMaterial,
        "Staff","Emitter","EmitterRim","EmitterInset","EmitterLens","StaffGrip");
    break;
case HeroId.Rook:
    ScaleAndGlow("GauntletR",1.5f,weaponMaterial);
    break;
}
}

private void ScaleAndGlow(string visualName,float scale,Material material)
{
Transform part=FindVisual(visualName);if(part==null)return;
part.localScale*=scale;
ApplyGlow(part,material);
}

private void GroupAndScaleWeapon(string groupName,Vector3 pivot,float scale,Material material,params string[] names)
{
Transform group=new GameObject(groupName).transform;group.SetParent(root,false);group.localPosition=pivot;
for(int i=0;i<names.Length;i++)
{
Transform part=FindVisual(names[i]);if(part==null||part==group)continue;
part.SetParent(group,true);
}
group.localScale=Vector3.one*scale;
ApplyGlow(group,material);
}

private Transform FindVisual(string shortName)
{
string target="DF_"+shortName;
Transform[] all=root.GetComponentsInChildren<Transform>(true);
for(int i=0;i<all.Length;i++)if(all[i]!=null&&all[i].name==target)return all[i];
return null;
}

private static void ApplyGlow(Transform parent,Material material)
{
if(parent==null||material==null)return;
MeshRenderer[] renderers=parent.GetComponentsInChildren<MeshRenderer>(true);
for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].sharedMaterial=material;
}

private void BindArmor()
{
// Gloves and knee armor move with their limbs instead of floating independently.
Bind("GloveL","ArmL");Bind("GloveR","ArmR");Bind("KneeL","LegL");Bind("KneeR","LegR");
for(int i=0;i<3;i++)Bind("Knuckle"+i,"GauntletR");
}
private void Bind(string childName,string parentName)
{
Transform child=root.Find("DF_"+childName),parent=root.Find("DF_"+parentName);
if(child!=null&&parent!=null)child.SetParent(parent,true);
}
private void CombineStaticPanels()
{
var groups=new System.Collections.Generic.Dictionary<Material,System.Collections.Generic.List<CombineInstance>>();
var remove=new System.Collections.Generic.List<GameObject>();
var animated=new System.Collections.Generic.HashSet<string>{"DF_LegL","DF_LegR","DF_BootL","DF_BootR","DF_ArmL","DF_ArmR","DF_GauntletL","DF_GauntletR","DF_OrbitL","DF_OrbitR","DF_WingL","DF_WingR","DF_Cape","DF_BladeR"};
foreach(Transform part in root)
{
if(animated.Contains(part.name))continue;
MeshFilter filter=part.GetComponent<MeshFilter>();MeshRenderer renderer=part.GetComponent<MeshRenderer>();
if(filter==null||renderer==null||renderer.sharedMaterial==null)continue;
Material material=renderer.sharedMaterial;if(!groups.TryGetValue(material,out var list)){list=new System.Collections.Generic.List<CombineInstance>();groups.Add(material,list);}
list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*part.localToWorldMatrix});remove.Add(part.gameObject);
}
foreach(var pair in groups)
{
Mesh combined=new Mesh{name="DF_HeroBodyBatch"};combined.CombineMeshes(pair.Value.ToArray(),true,true);combined.RecalculateBounds();detailMeshes.Add(combined);
GameObject batch=new GameObject("DF_BodySurface");batch.transform.SetParent(root,false);batch.AddComponent<MeshFilter>().sharedMesh=combined;batch.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
}
foreach(GameObject part in remove){part.SetActive(false);Destroy(part);}
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
foreach(Mesh mesh in detailMeshes)if(mesh!=null)Destroy(mesh);
foreach(Material material in ownedMaterials)if(material!=null)Destroy(material);
}
}
}


