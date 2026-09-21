using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed partial class StylizedHeroVisual:MonoBehaviour
{
private bool voidfall,betaSupporterWings,plagueWings,weaponOverdrive;private HeroIdentity identity;private TeamMember team;private bool built;private Color accent,dark,teamEdge;private Transform root;private void Awake(){Build();}private void Start(){Build();}public void Build(){if(built)return;identity=GetComponent<HeroIdentity>();team=GetComponent<TeamMember>();if(identity==null)return;built=true;var skinPreview=GetComponent<Alterverse.Shop.SkinPreviewSelection>();Alterverse.Shop.UniversalHeroCosmetic cosmetic=skinPreview!=null?skinPreview.UniversalCosmetic:GetComponent<HeroRespawn>()!=null?Alterverse.Shop.UniversalCosmeticSelection.EquippedFor(identity.Hero):Alterverse.Shop.UniversalHeroCosmetic.Original;voidfall=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.Voidfall;betaSupporterWings=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.BetaSupporterWings;plagueWings=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.PlagueWings;weaponOverdrive=skinPreview!=null?skinPreview.WeaponOverdrive:GetComponent<HeroRespawn>()!=null&&Alterverse.Shop.WeaponSkinInventory.Equipped(identity.Hero);accent=StylizedArtDirection.HeroAccent(identity.Hero);dark=Color.Lerp(accent,new Color(.13f,.16f,.21f),.85f);teamEdge=team!=null&&team.Team==1?StylizedArtDirection.LumenEnergy:StylizedArtDirection.ZeroEnergy;Renderer baseRenderer=GetComponent<Renderer>();if(baseRenderer!=null)baseRenderer.enabled=false;root=new GameObject("DF_VisualRoot").transform;root.SetParent(transform,false);BuildProductionHero();BindArmor();if(weaponOverdrive)ApplyWeaponOverdrive();CombineMovingPanels();CombineStaticPanels();if(voidfall)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).Build(root,skinPreview!=null);else if(betaSupporterWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildBetaSupporter(root,skinPreview!=null,identity.Hero);else if(plagueWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildPlague(root,skinPreview!=null,identity.Hero);if(GetComponent<HeroVisualMotion>()==null)gameObject.AddComponent<HeroVisualMotion>();if(GetComponent<FounderHeroVisual>()==null)gameObject.AddComponent<FounderHeroVisual>();if(GetComponent<Health>()!=null){if(GetComponent<LunarFootprintTrail>()==null)gameObject.AddComponent<LunarFootprintTrail>();HeroVfxPool.Warm();}}
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
Mesh colored=Instantiate(mesh);colored.name=mesh.name+"_HeroColor";detailMeshes.Add(colored);
HeroMeshLibrary.Paint(colored,FinishedColor(color,IsEnergySurface(n)),IsEnergySurface(n));
part.AddComponent<MeshFilter>().sharedMesh=colored;
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
Color finish=FinishedColor(color,energy);
bool fabric=color==cloth;bool matte=color==dark;
Material material=HeroMeshLibrary.Surface(finish,energy?2:fabric||matte?0:1);
palette.Add((color,energy),material);return material;
}
private Color FinishedColor(Color color,bool energy)
{
if(!voidfall||energy||color==teamEdge)return color;
return color==armorLight?new Color(.26f,.16f,.36f):color==dark||color==cloth?new Color(.027f,.018f,.045f):Color.Lerp(color,new Color(.24f,.20f,.32f),.55f);
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
Mesh mesh=new Mesh{name="DF_Contour_"+name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();HeroMeshLibrary.Paint(mesh,FinishedColor(color,false),false);detailMeshes.Add(mesh);
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

// Keep cosmetic weapons attached to their animated hand pivots.
if(identity.Hero==HeroId.Veyr)ApplyNullEdgeOverdrive(shader,weaponMaterial);
else if(identity.Hero==HeroId.Kaelon)ApplyAnchorHammerOverdrive(shader,weaponMaterial);
else if(identity.Hero==HeroId.Rook)ApplyBreakerFistOverdrive(shader,weaponMaterial);
else if(identity.Hero==HeroId.Elyra){ScaleAndGlow("Orb",2.106f,weaponMaterial);ScaleAndGlow("OrbitL",1.872f,weaponMaterial);ScaleAndGlow("OrbitR",1.872f,weaponMaterial);}
else if(identity.Hero==HeroId.Mira)ApplyLifeweaveOverdrive(shader,weaponMaterial);
else if(identity.Hero==HeroId.Nyra)ApplyStarlineOverdrive(shader,weaponMaterial);
else ScaleAndGlow("WeaponR",1.25f,weaponMaterial);
}

private void ApplyNullEdgeOverdrive(Shader shader,Material voidMaterial)
{
Transform leftWeapon=FindVisual("WeaponL");
Transform rightWeapon=FindVisual("WeaponR");

if(leftWeapon!=null)leftWeapon.localScale*=1.11f;
if(rightWeapon!=null)rightWeapon.localScale*=1.11f;

Color blackSteel=new(.025f,.020f,.035f,1f);
Material steelMaterial=new Material(shader){name="DF_NightBlossomBlackSteel",color=blackSteel};
if(steelMaterial.HasProperty("_BaseColor"))steelMaterial.SetColor("_BaseColor",blackSteel);
if(steelMaterial.HasProperty("_Metallic"))steelMaterial.SetFloat("_Metallic",.84f);
if(steelMaterial.HasProperty("_Smoothness"))steelMaterial.SetFloat("_Smoothness",.58f);
ownedMaterials.Add(steelMaterial);

Color violetCore=new(.72f,.16f,1f,1f);
Material coreMaterial=new Material(shader){name="DF_NightBlossomVoidCore",color=violetCore};
if(coreMaterial.HasProperty("_BaseColor"))coreMaterial.SetColor("_BaseColor",violetCore);
if(coreMaterial.HasProperty("_Metallic"))coreMaterial.SetFloat("_Metallic",.10f);
if(coreMaterial.HasProperty("_Smoothness"))coreMaterial.SetFloat("_Smoothness",.90f);
if(coreMaterial.HasProperty("_EmissionColor"))
{
coreMaterial.EnableKeyword("_EMISSION");
coreMaterial.SetColor("_EmissionColor",violetCore*3.5f);
}
ownedMaterials.Add(coreMaterial);

for(int side=-1;side<=1;side+=2)
{
string suffix=side<0?"L":"R";
Transform weapon=side<0?leftWeapon:rightWeapon;
if(weapon==null)continue;

Transform blade=FindVisual("Blade"+suffix);
if(blade!=null)ApplyGlow(blade,steelMaterial);

Transform edge=FindVisual("BladeEdge"+suffix);
if(edge!=null)
{
edge.localScale*=1.11f;
ApplyGlow(edge,voidMaterial);
}

Transform hilt=FindVisual("Hilt"+suffix);
if(hilt!=null)
{
hilt.localScale=Vector3.Scale(hilt.localScale,new Vector3(1.10f,1.15f,1.10f));
ApplyGlow(hilt,steelMaterial);
}

Transform guard=FindVisual("Guard"+suffix);
if(guard!=null){guard.localScale*=1.12f;ApplyGlow(guard,steelMaterial);}

Transform pommel=FindVisual("Pommel"+suffix);
if(pommel!=null)ApplyGlow(pommel,coreMaterial);

CreateNullEdgePart("NightBlossomCore"+suffix,weapon,
new Vector3(0,-.105f,.13f),new Vector3(.080f,.080f,.045f),
new Vector3(0,0,45f),coreMaterial);

CreateNullEdgePart("NightGuardWingA"+suffix,weapon,
new Vector3(side*.12f,-.12f,.09f),new Vector3(.055f,.24f,.08f),
new Vector3(0,0,side*-34f),steelMaterial);
CreateNullEdgePart("NightGuardWingB"+suffix,weapon,
new Vector3(side*-.12f,-.12f,.09f),new Vector3(.055f,.24f,.08f),
new Vector3(0,0,side*34f),steelMaterial);
CreateNullEdgePart("NightPetal"+suffix,weapon,
new Vector3(side*.12f,-.28f,.31f),new Vector3(.032f,.16f,.022f),
new Vector3(side*14f,0,side*18f),coreMaterial);
}
}

private GameObject CreateNullEdgePart(string name,Transform parent,Vector3 localPosition,Vector3 localScale,Vector3 localRotation,Material material)
{
GameObject part=Create(name,PrimitiveType.Cube,localPosition,localScale,Color.white,localRotation,parent);
MeshRenderer renderer=part.GetComponent<MeshRenderer>();
if(renderer!=null)renderer.sharedMaterial=material;
return part;
}

private void ApplyStarlineOverdrive(Shader shader,Material energyMaterial)
{
Transform weapon=FindVisual("WeaponR");if(weapon==null)return;

// RED JUDGEMENT // EPIC: keep the elite base rifle architecture but push it into a
// longer precision silhouette with black armor, gunmetal reinforcement and red optics.
weapon.localScale*=1.18f;

Color blackSteel=new(.030f,.034f,.040f,1f);
Material blackMaterial=new Material(shader){name="DF_RedJudgementBlackSteel",color=blackSteel};
if(blackMaterial.HasProperty("_BaseColor"))blackMaterial.SetColor("_BaseColor",blackSteel);
if(blackMaterial.HasProperty("_Metallic"))blackMaterial.SetFloat("_Metallic",.88f);
if(blackMaterial.HasProperty("_Smoothness"))blackMaterial.SetFloat("_Smoothness",.42f);
ownedMaterials.Add(blackMaterial);

Color gunmetal=new(.24f,.26f,.29f,1f);
Material gunmetalMaterial=new Material(shader){name="DF_RedJudgementGunmetal",color=gunmetal};
if(gunmetalMaterial.HasProperty("_BaseColor"))gunmetalMaterial.SetColor("_BaseColor",gunmetal);
if(gunmetalMaterial.HasProperty("_Metallic"))gunmetalMaterial.SetFloat("_Metallic",.90f);
if(gunmetalMaterial.HasProperty("_Smoothness"))gunmetalMaterial.SetFloat("_Smoothness",.54f);
ownedMaterials.Add(gunmetalMaterial);

Color redCore=new(1f,.025f,.018f,1f);
Material redMaterial=new Material(shader){name="DF_RedJudgementOptic",color=redCore};
if(redMaterial.HasProperty("_BaseColor"))redMaterial.SetColor("_BaseColor",redCore);
if(redMaterial.HasProperty("_Metallic"))redMaterial.SetFloat("_Metallic",.08f);
if(redMaterial.HasProperty("_Smoothness"))redMaterial.SetFloat("_Smoothness",.96f);
if(redMaterial.HasProperty("_EmissionColor"))
{
redMaterial.EnableKeyword("_EMISSION");
redMaterial.SetColor("_EmissionColor",redCore*4.6f);
}
ownedMaterials.Add(redMaterial);

string[] blackParts={"Rifle","RifleReceiver","RifleStock","RifleMagazine","Scope","RifleForegrip","RifleSensor"};
for(int i=0;i<blackParts.Length;i++)
{
Transform part=FindVisual(blackParts[i]);
if(part!=null)ApplyGlow(part,blackMaterial);
}
string[] metalParts={"RifleBarrel","MuzzleBrake","ScopeBridge","WeaponRail"};
for(int i=0;i<metalParts.Length;i++)
{
Transform part=FindVisual(metalParts[i]);
if(part!=null)ApplyGlow(part,gunmetalMaterial);
}
Transform lens=FindVisual("ScopeLens");
if(lens!=null){lens.localScale*=1.18f;ApplyGlow(lens,redMaterial);}
Transform sensor=FindVisual("RifleSensorGlow");
if(sensor!=null){sensor.localScale*=1.22f;ApplyGlow(sensor,redMaterial);}

// Premium silhouette changes: elongated shroud, split muzzle cage, side energy rails,
// secondary rangefinder and a red judgement core. Not a recolor.
CreateRedJudgementPart("RJ_UpperShroud",weapon,new Vector3(0,.10f,.78f),new Vector3(.18f,.10f,.62f),Vector3.zero,blackMaterial);
CreateRedJudgementPart("RJ_LeftRail",weapon,new Vector3(-.135f,.03f,.55f),new Vector3(.028f,.055f,.58f),Vector3.zero,redMaterial);
CreateRedJudgementPart("RJ_RightRail",weapon,new Vector3(.135f,.03f,.55f),new Vector3(.028f,.055f,.58f),Vector3.zero,redMaterial);
CreateRedJudgementPart("RJ_MuzzleWingL",weapon,new Vector3(-.115f,.015f,1.34f),new Vector3(.070f,.16f,.22f),new Vector3(0,0,-8f),gunmetalMaterial);
CreateRedJudgementPart("RJ_MuzzleWingR",weapon,new Vector3(.115f,.015f,1.34f),new Vector3(.070f,.16f,.22f),new Vector3(0,0,8f),gunmetalMaterial);
CreateRedJudgementPart("RJ_Rangefinder",weapon,new Vector3(.145f,.25f,.24f),new Vector3(.095f,.12f,.30f),Vector3.zero,blackMaterial);
CreateRedJudgementPart("RJ_RangefinderLens",weapon,new Vector3(.145f,.25f,.405f),new Vector3(.050f,.050f,.018f),Vector3.zero,redMaterial);
CreateRedJudgementPart("RJ_Core",weapon,new Vector3(-.13f,.02f,.18f),new Vector3(.030f,.10f,.18f),Vector3.zero,redMaterial);
}

private GameObject CreateRedJudgementPart(string name,Transform parent,Vector3 localPosition,Vector3 localScale,Vector3 localRotation,Material material)
{
GameObject part=Create(name,PrimitiveType.Cube,localPosition,localScale,Color.white,localRotation,parent);
MeshRenderer renderer=part.GetComponent<MeshRenderer>();
if(renderer!=null)renderer.sharedMaterial=material;
return part;
}

private void ApplyLifeweaveOverdrive(Shader shader,Material energyMaterial)
{
Transform weapon=FindVisual("WeaponR");if(weapon==null)return;

// The previous premium staff used 1.25x. Another +20% from that state equals 1.50x.
weapon.localScale*=1.50f;

// Preserve the existing emerald/cyan premium glow on the full staff.
ApplyGlow(weapon,energyMaterial);

// Turn the existing center lens in the staff crown into a distinct glowing red gem.
Color gemColor=new Color(1f,.025f,.02f);
Material gemMaterial=new Material(shader){name="DF_LifeweaveRuby",color=gemColor};
if(gemMaterial.HasProperty("_BaseColor"))gemMaterial.SetColor("_BaseColor",gemColor);
if(gemMaterial.HasProperty("_Metallic"))gemMaterial.SetFloat("_Metallic",.12f);
if(gemMaterial.HasProperty("_Smoothness"))gemMaterial.SetFloat("_Smoothness",.96f);
if(gemMaterial.HasProperty("_EmissionColor"))
{
gemMaterial.EnableKeyword("_EMISSION");
gemMaterial.SetColor("_EmissionColor",gemColor*4.4f);
}
ownedMaterials.Add(gemMaterial);

Transform gem=FindVisual("EmitterLens");
if(gem!=null)
{
gem.localScale*=1.12f;
ApplyGlow(gem,gemMaterial);
}

// Saturn-like premium ring around the staff crown. It keeps the staff's
// emerald/cyan energy identity while framing the red center gem.
Mesh planetRingMesh=HeroMeshLibrary.Ring("DF_LifeweavePlanetRing",.34f,.022f);
Color ringColor=Alterverse.Shop.WeaponSkinCatalog.For(identity.Hero).Glow;
GameObject planetRing=Sculpt("LifeweavePlanetRing",planetRingMesh,
new Vector3(0,.80f,.06f),Vector3.one,ringColor,weapon,true);
planetRing.transform.localRotation=Quaternion.Euler(72f,0f,28f);
MeshRenderer ringRenderer=planetRing.GetComponent<MeshRenderer>();
if(ringRenderer!=null)ringRenderer.sharedMaterial=energyMaterial;
}

private void ApplyAnchorHammerOverdrive(Shader shader,Material energyMaterial)
{
Transform weapon=FindVisual("WeaponR");if(weapon==null)return;

// Premium version keeps the same industrial breacher language as Kaelon's new base hammer:
// blackened forged metal, pale impact caps and restrained amber energy channels.
weapon.localScale*=1.18f;

Color blackSteel=new Color(.045f,.050f,.055f);
Material steelMaterial=new Material(shader){name="DF_SiegeBreakerBlackSteel",color=blackSteel};
if(steelMaterial.HasProperty("_BaseColor"))steelMaterial.SetColor("_BaseColor",blackSteel);
if(steelMaterial.HasProperty("_Metallic"))steelMaterial.SetFloat("_Metallic",.90f);
if(steelMaterial.HasProperty("_Smoothness"))steelMaterial.SetFloat("_Smoothness",.38f);
ownedMaterials.Add(steelMaterial);

Color impactSteel=new Color(.67f,.65f,.58f);
Material impactMaterial=new Material(shader){name="DF_SiegeBreakerImpactSteel",color=impactSteel};
if(impactMaterial.HasProperty("_BaseColor"))impactMaterial.SetColor("_BaseColor",impactSteel);
if(impactMaterial.HasProperty("_Metallic"))impactMaterial.SetFloat("_Metallic",.82f);
if(impactMaterial.HasProperty("_Smoothness"))impactMaterial.SetFloat("_Smoothness",.46f);
ownedMaterials.Add(impactMaterial);

Color coreColor=Alterverse.Shop.WeaponSkinCatalog.For(identity.Hero).Glow;
Material coreMaterial=new Material(shader){name="DF_SiegeBreakerCore",color=coreColor};
if(coreMaterial.HasProperty("_BaseColor"))coreMaterial.SetColor("_BaseColor",coreColor);
if(coreMaterial.HasProperty("_Metallic"))coreMaterial.SetFloat("_Metallic",.16f);
if(coreMaterial.HasProperty("_Smoothness"))coreMaterial.SetFloat("_Smoothness",.92f);
if(coreMaterial.HasProperty("_EmissionColor"))
{
coreMaterial.EnableKeyword("_EMISSION");
coreMaterial.SetColor("_EmissionColor",coreColor*3.7f);
}
ownedMaterials.Add(coreMaterial);

Transform head=FindVisual("AnchorHead");if(head!=null)ApplyGlow(head,steelMaterial);
Transform face=FindVisual("HammerFace");if(face!=null)ApplyGlow(face,impactMaterial);
Transform top=FindVisual("HammerTopPlate");if(top!=null)ApplyGlow(top,impactMaterial);
Transform grip=FindVisual("HammerGrip");if(grip!=null)ApplyGlow(grip,steelMaterial);
Transform collar=FindVisual("HammerCollar");if(collar!=null)ApplyGlow(collar,impactMaterial);
Transform pommel=FindVisual("HammerLowerPommel");if(pommel!=null)ApplyGlow(pommel,impactMaterial);

for(int side=-1;side<=1;side+=2)
{
Transform cheek=FindVisual("HammerCheek"+side);if(cheek!=null)ApplyGlow(cheek,steelMaterial);
Transform channel=FindVisual("HammerEnergy"+side);if(channel!=null){channel.localScale*=1.22f;ApplyGlow(channel,coreMaterial);}
}
for(int i=0;i<5;i++)
{
Transform vent=FindVisual("HammerVent"+i);if(vent!=null)ApplyGlow(vent,steelMaterial);
}

// Shop-only premium geometry: reinforced end caps, frontal energy spine and a compact core.
// These are attached to the animated weapon pivot, so the preview and in-game attack stay identical.
CreateAnchorHammerPart("OverdriveEndCapL",weapon,new Vector3(-.52f,.54f,.02f),new Vector3(.13f,.38f,.47f),
new Vector3(0,0,-7f),impactMaterial);
CreateAnchorHammerPart("OverdriveEndCapR",weapon,new Vector3(.52f,.54f,.02f),new Vector3(.13f,.38f,.47f),
new Vector3(0,0,7f),impactMaterial);
CreateAnchorHammerPart("OverdriveEnergySpine",weapon,new Vector3(0,.54f,.344f),new Vector3(.42f,.045f,.018f),
Vector3.zero,coreMaterial);
CreateAnchorHammerPart("OverdriveCore",weapon,new Vector3(0,.54f,.366f),new Vector3(.13f,.13f,.026f),
new Vector3(0,0,45f),coreMaterial);
CreateAnchorHammerPart("OverdriveShaftBrace",weapon,new Vector3(0,.22f,.02f),new Vector3(.12f,.24f,.13f),
Vector3.zero,impactMaterial);
}

private GameObject CreateAnchorHammerPart(string name,Transform parent,Vector3 localPosition,Vector3 localScale,Vector3 localRotation,Material material)
{
GameObject part=Create(name,PrimitiveType.Cube,localPosition,localScale,Color.white,localRotation,parent);
MeshRenderer renderer=part.GetComponent<MeshRenderer>();
if(renderer!=null)renderer.sharedMaterial=material;
return part;
}

private void ApplyBreakerFistOverdrive(Shader shader,Material energyMaterial)
{
Color darkSteel=new(.045f,.050f,.058f,1f);
Material darkMaterial=new Material(shader){name="DF_ReactorBreakerDarkSteel",color=darkSteel};
if(darkMaterial.HasProperty("_BaseColor"))darkMaterial.SetColor("_BaseColor",darkSteel);
if(darkMaterial.HasProperty("_Metallic"))darkMaterial.SetFloat("_Metallic",.86f);
if(darkMaterial.HasProperty("_Smoothness"))darkMaterial.SetFloat("_Smoothness",.48f);
ownedMaterials.Add(darkMaterial);

Color armorYellow=new(.78f,.49f,.045f,1f);
Material yellowMaterial=new Material(shader){name="DF_ReactorBreakerYellowArmor",color=armorYellow};
if(yellowMaterial.HasProperty("_BaseColor"))yellowMaterial.SetColor("_BaseColor",armorYellow);
if(yellowMaterial.HasProperty("_Metallic"))yellowMaterial.SetFloat("_Metallic",.62f);
if(yellowMaterial.HasProperty("_Smoothness"))yellowMaterial.SetFloat("_Smoothness",.38f);
ownedMaterials.Add(yellowMaterial);

Color hotOrange=new(1f,.34f,.025f,1f);
Material hotMaterial=new Material(shader){name="DF_ReactorBreakerCore",color=hotOrange};
if(hotMaterial.HasProperty("_BaseColor"))hotMaterial.SetColor("_BaseColor",hotOrange);
if(hotMaterial.HasProperty("_Metallic"))hotMaterial.SetFloat("_Metallic",.12f);
if(hotMaterial.HasProperty("_Smoothness"))hotMaterial.SetFloat("_Smoothness",.92f);
if(hotMaterial.HasProperty("_EmissionColor"))
{
hotMaterial.EnableKeyword("_EMISSION");
hotMaterial.SetColor("_EmissionColor",hotOrange*4.1f);
}
ownedMaterials.Add(hotMaterial);

for(int side=-1;side<=1;side+=2)
{
string suffix=side<0?"L":"R";
Transform gauntlet=FindVisual("Gauntlet"+suffix);
if(gauntlet==null)continue;

// EPIC silhouette is larger and more armored, but not an oversized glowing block.
gauntlet.localScale*=1.24f;
ApplyGlow(gauntlet,darkMaterial);

for(int k=0;k<3;k++)
{
Transform knuckle=FindVisual("Knuckle"+suffix+k);
if(knuckle!=null)
{
knuckle.localScale=Vector3.Scale(knuckle.localScale,new Vector3(1.04f,1.08f,1.08f));
ApplyGlow(knuckle,yellowMaterial);
}
}

Transform reactor=FindVisual("GauntletReactor"+suffix);
if(reactor!=null)
{
reactor.localScale=Vector3.Scale(reactor.localScale,new Vector3(1.20f,1.18f,1.10f));
ApplyGlow(reactor,hotMaterial);
}

Transform fistPlate=FindVisual("BrakkFistPlate"+suffix);
if(fistPlate!=null)ApplyGlow(fistPlate,yellowMaterial);
Transform fistSide=FindVisual("BrakkFistSide"+suffix);
if(fistSide!=null)ApplyGlow(fistSide,darkMaterial);

// Layered impact frame inspired by the reference's reinforced power-fist construction.
CreateBreakerFistPart("ReactorBreakerBackPlate"+suffix,gauntlet,
new Vector3(0,.115f,.015f),new Vector3(.34f,.070f,.28f),
new Vector3(0,0,side*4f),yellowMaterial);
CreateBreakerFistPart("ReactorBreakerSideA"+suffix,gauntlet,
new Vector3(-.215f,.018f,.080f),new Vector3(.055f,.18f,.19f),
new Vector3(0,0,-10f),darkMaterial);
CreateBreakerFistPart("ReactorBreakerSideB"+suffix,gauntlet,
new Vector3(.215f,.018f,.080f),new Vector3(.055f,.18f,.19f),
new Vector3(0,0,10f),darkMaterial);

// Orange energy channels make the epic skin immediately readable in the shop and in combat.
CreateBreakerFistPart("ReactorBreakerRailTop"+suffix,gauntlet,
new Vector3(0,.165f,.105f),new Vector3(.22f,.025f,.13f),
Vector3.zero,hotMaterial);
CreateBreakerFistPart("ReactorBreakerRailL"+suffix,gauntlet,
new Vector3(-.145f,.045f,.165f),new Vector3(.025f,.15f,.040f),
new Vector3(0,0,-8f),hotMaterial);
CreateBreakerFistPart("ReactorBreakerRailR"+suffix,gauntlet,
new Vector3(.145f,.045f,.165f),new Vector3(.025f,.15f,.040f),
new Vector3(0,0,8f),hotMaterial);
CreateBreakerFistPart("ReactorBreakerCore"+suffix,gauntlet,
new Vector3(0,.095f,.205f),new Vector3(.10f,.085f,.035f),
new Vector3(0,0,45f),hotMaterial);
}
}

private GameObject CreateBreakerFistPart(string name,Transform parent,Vector3 localPosition,Vector3 localScale,Vector3 localRotation,Material material)
{
GameObject part=Create(name,PrimitiveType.Cube,localPosition,localScale,Color.white,localRotation,parent);
MeshRenderer renderer=part.GetComponent<MeshRenderer>();
if(renderer!=null)renderer.sharedMaterial=material;
return part;
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
var animated=new System.Collections.Generic.HashSet<string>{"DF_LegL","DF_LegR","DF_BootL","DF_BootR","DF_ArmL","DF_ArmR","DF_GauntletL","DF_GauntletR","DF_OrbitL","DF_OrbitR","DF_WingL","DF_WingR","DF_Cape","DF_BladeR","DF_WeaponR","DF_WeaponL","DF_Orb"};
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




