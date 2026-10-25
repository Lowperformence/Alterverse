using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed partial class StylizedHeroVisual:MonoBehaviour
{
private bool voidfall,betaSupporterWings,plagueWings,deluxeWings,angelWings,rgbRiftWings;private HeroIdentity identity;private TeamMember team;private bool built;private Color accent,dark,teamEdge;private Transform root;private void Awake(){Build();}private void Start(){Build();}public void Build(){if(built)return;identity=GetComponent<HeroIdentity>();team=GetComponent<TeamMember>();if(identity==null)return;built=true;if(identity.Hero==HeroId.Bolt7){var body=GetComponent<Renderer>();if(body!=null)body.enabled=false;Bolt7Visual.Create(gameObject);return;}var skinPreview=GetComponent<Alterverse.Shop.SkinPreviewSelection>();Alterverse.Shop.UniversalHeroCosmetic cosmetic=skinPreview!=null?skinPreview.UniversalCosmetic:LocalHeroContext.IsLocal(gameObject)?Alterverse.Shop.UniversalCosmeticSelection.EquippedFor(identity.Hero):Alterverse.Shop.UniversalHeroCosmetic.Original;voidfall=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.Voidfall;betaSupporterWings=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.BetaSupporterWings;plagueWings=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.PlagueWings;deluxeWings=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.DeluxeWings;angelWings=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.AngelWings;rgbRiftWings=cosmetic==Alterverse.Shop.UniversalHeroCosmetic.RgbRiftWings;accent=StylizedArtDirection.HeroAccent(identity.Hero);dark=Color.Lerp(accent,new Color(.13f,.16f,.21f),.85f);teamEdge=team!=null&&team.Team==1?StylizedArtDirection.LumenEnergy:StylizedArtDirection.ZeroEnergy;Renderer baseRenderer=GetComponent<Renderer>();if(baseRenderer!=null)baseRenderer.enabled=false;if(AuthoredHeroPresentation.TryCreate(gameObject,identity.Hero,out root)){FinishAuthoredHero(skinPreview!=null);return;}root=new GameObject("DF_VisualRoot").transform;root.SetParent(transform,false);root.localScale=new Vector3(1.22f,1.32f,1.22f);root.localPosition=new Vector3(0,.32f,0);BuildProductionHero();BuildModernHeroFinish();ScaleDefaultWeapon("WeaponL");ScaleDefaultWeapon("WeaponR");ScaleDefaultWeapon("GauntletL");ScaleDefaultWeapon("GauntletR");BindArmor();HeroBootPresentation.ApplyProcedural(root,identity.Hero);CombineMovingPanels();CombineStaticPanels();if(voidfall)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).Build(root,skinPreview!=null);else if(betaSupporterWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildBetaSupporter(root,skinPreview!=null,identity.Hero);else if(plagueWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildPlague(root,skinPreview!=null,identity.Hero);else if(deluxeWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildDeluxe(root,skinPreview!=null,identity.Hero);
else if(angelWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildAngel(root,skinPreview!=null,identity.Hero);
else if(rgbRiftWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildRgbRift(root,skinPreview!=null,identity.Hero);if(GetComponent<HeroVisualMotion>()==null)gameObject.AddComponent<HeroVisualMotion>();HeadwearPresentation.Configure(gameObject,root,identity.Hero,GetComponent<Alterverse.Shop.SkinPreviewSelection>());if(GetComponent<FounderHeroVisual>()==null)gameObject.AddComponent<FounderHeroVisual>();if(GetComponent<Health>()!=null){if(GetComponent<LunarFootprintTrail>()==null)gameObject.AddComponent<LunarFootprintTrail>();if(GetComponent<HeroAttackReadabilityPresentation>()==null)gameObject.AddComponent<HeroAttackReadabilityPresentation>();HeroVfxPool.Warm();}}
private void FinishAuthoredHero(bool preview)
{
if(voidfall)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).Build(root,preview);
else if(betaSupporterWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildBetaSupporter(root,preview,identity.Hero);
else if(plagueWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildPlague(root,preview,identity.Hero);
else if(deluxeWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildDeluxe(root,preview,identity.Hero);
else if(angelWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildAngel(root,preview,identity.Hero);
else if(rgbRiftWings)(GetComponent<VoidWingPresentation>()??gameObject.AddComponent<VoidWingPresentation>()).BuildRgbRift(root,preview,identity.Hero);
if(GetComponent<HeroVisualMotion>()==null)gameObject.AddComponent<HeroVisualMotion>();HeadwearPresentation.Configure(gameObject,root,identity.Hero,GetComponent<Alterverse.Shop.SkinPreviewSelection>());
if(GetComponent<FounderHeroVisual>()==null)gameObject.AddComponent<FounderHeroVisual>();
if(GetComponent<Health>()!=null){if(GetComponent<LunarFootprintTrail>()==null)gameObject.AddComponent<LunarFootprintTrail>();if(GetComponent<HeroAttackReadabilityPresentation>()==null)gameObject.AddComponent<HeroAttackReadabilityPresentation>();HeroVfxPool.Warm();}
}
private void ScaleDefaultWeapon(string name){Transform weapon=root.Find("DF_"+name);if(weapon!=null)weapon.localScale*=1.12f;}
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
if(helmetMesh==null)helmetMesh=BuildProfile("DF_FacetedHelmet",new float[]{-.5f,-.42f,-.24f,0f,.24f,.42f,.5f},new float[]{.015f,.29f,.445f,.48f,.445f,.29f,.015f});
mesh=helmetMesh;
}
else
{
if(armorMesh==null)armorMesh=HeroSculptGeometry.RoundedBox();
mesh=armorMesh;
}
Mesh colored=Instantiate(mesh);colored.name=mesh.name+"_HeroColor";detailMeshes.Add(colored);
HeroMeshLibrary.Paint(colored,FinishedColor(color,IsEnergySurface(n)),IsEnergySurface(n),HeroMeshLibrary.FinishFor(n,color));
part.AddComponent<MeshFilter>().sharedMesh=colored;
MeshRenderer renderer=part.AddComponent<MeshRenderer>();
renderer.sharedMaterial=MaterialFor(color, IsEnergySurface(n));
return part;
}
private static bool IsEnergySurface(string name) => name=="Eye"||name=="Visor"||name=="Core"||name=="ChestGem"||name=="ChestLeaf"||name=="Reactor"||name=="ShieldLine"||name=="TeamInsignia"||name=="ProjectorL"||name=="ProjectorR"||name=="ChestSlash"||name=="HammerPowerSpine"||name=="HammerStrikeLens"||name=="MuzzleCrown";
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
// Wing cosmetics only add their own geometry and materials; preserve the hero palette.
return color;
}
private void Panel(string name,Vector2[] outline,float depth,Vector3 position,Color color)
{
Mesh mesh=HeroSculptGeometry.ArmorPanel(name,outline,depth);
HeroMeshLibrary.Paint(mesh,FinishedColor(color,false),false,HeroMeshLibrary.FinishFor(name,color));detailMeshes.Add(mesh);
GameObject part=new GameObject("DF_"+name);part.transform.SetParent(root,false);part.transform.localPosition=position;
part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=MaterialFor(color);
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
internal static bool IsFlowingFabric(string name)
{
    return name=="DF_Cape"||name=="DF_CoatTailR"||
        name=="DF_VeyrBackMantle"||name=="DF_TahlaBackDrape"||
        name.StartsWith("DF_TahlaGoldenVeil")||name.StartsWith("DF_TahlaVeilEdge")||
        name=="DF_TravelerMantleBack"||name=="DF_TravelerMantleL"||name=="DF_TravelerMantleR"||
        name=="DF_TravelerMantleSpine"||name.StartsWith("DF_TravelerMantleSeam")||
        name.StartsWith("DF_TravelerMantleFold")||name.StartsWith("DF_TravelerFront")||
        name=="DF_TahlaRobeFront"||name=="DF_TahlaRobeBack"||
        name=="DF_TahlaRobeSideL"||name=="DF_TahlaRobeSideR";
}
private void CombineStaticPanels()
{
var groups=new System.Collections.Generic.Dictionary<Material,System.Collections.Generic.List<CombineInstance>>();
var remove=new System.Collections.Generic.List<GameObject>();
var animated=new System.Collections.Generic.HashSet<string>{"DF_LegL","DF_LegR","DF_BootL","DF_BootR","DF_ArmL","DF_ArmR","DF_GauntletL","DF_GauntletR","DF_OrbitL","DF_OrbitR","DF_WingL","DF_WingR","DF_Cape","DF_BladeR","DF_WeaponR","DF_WeaponL","DF_Orb"};
foreach(Transform part in root)
{
if(animated.Contains(part.name)||IsFlowingFabric(part.name)||IsArticulatedBodyPart(part))continue;
MeshFilter filter=part.GetComponent<MeshFilter>();MeshRenderer renderer=part.GetComponent<MeshRenderer>();
if(filter==null||renderer==null||renderer.sharedMaterial==null)continue;
Material material=renderer.sharedMaterial;if(!groups.TryGetValue(material,out var list)){list=new System.Collections.Generic.List<CombineInstance>();groups.Add(material,list);}
list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*part.localToWorldMatrix});remove.Add(part.gameObject);
}
foreach(var pair in groups)
{
Mesh combined=new Mesh{name="DF_HeroBodyBatch"};int vertexCount=0;foreach(var instance in pair.Value)vertexCount+=instance.mesh.vertexCount;if(vertexCount>65535)combined.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;combined.CombineMeshes(pair.Value.ToArray(),true,true);combined.RecalculateBounds();detailMeshes.Add(combined);
GameObject batch=new GameObject("DF_BodySurface");batch.transform.SetParent(root,false);batch.AddComponent<MeshFilter>().sharedMesh=combined;batch.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
}
foreach(GameObject part in remove){part.SetActive(false);Destroy(part);}
}
private static bool IsArticulatedBodyPart(Transform part)
{
if(part==null)return false;
string n=part.name.ToLowerInvariant();
if(n.Contains("torso")||n.Contains("pelvis")||n.Contains("head")||n.Contains("neck")||
n.Contains("breastplate")||n.Contains("abdomen")||n.Contains("gorget")||n.Contains("belt")||
n.Contains("buckle")||n.Contains("tasset")||n.Contains("hipguard")||n.Contains("clavicle")||
n.Contains("chest")||n.Contains("visor")||n.Contains("brow")||n.Contains("face")||
n.Contains("hair")||n.Contains("eye")||n.Contains("jaw")||n.Contains("mask"))return true;
if(n.Contains("cape")||n.Contains("cloak")||n.Contains("mantle")||n.Contains("robe")||
n.Contains("tabard")||n.Contains("veil")||n.Contains("drape")||n.Contains("stole")||
n.Contains("coat")||n.Contains("orbit")||n.Contains("halo")||n.Contains("wing")||
n.Contains("projector")||n.Contains("weapon")||n.Contains("staff")||n.Contains("rifle")||
n.Contains("muzzle")||n.Contains("emitter")||n.Contains("blade")||n.Contains("gauntlet"))return false;
Vector3 p=part.localPosition;
bool torsoZone=Mathf.Abs(p.x)<=.32f&&p.y>=-.08f&&p.y<=.58f;
bool pelvisZone=Mathf.Abs(p.x)<=.40f&&p.y>=-.43f&&p.y<-.15f;
bool headZone=Mathf.Abs(p.x)<=.32f&&p.y>.69f;
return torsoZone||pelvisZone||headZone;
}
private static Mesh BuildProfile(string name,float[] heights,float[] radii)
{
var vertices=new System.Collections.Generic.List<Vector3>();
var normals=new System.Collections.Generic.List<Vector3>();
var triangles=new System.Collections.Generic.List<int>();
const int sides=16;
Vector3 SurfaceNormal(float angle,int ring)
{
int previous=Mathf.Max(0,ring-1),next=Mathf.Min(heights.Length-1,ring+1);
float rise=Mathf.Max(.001f,heights[next]-heights[previous]);
float slope=(radii[next]-radii[previous])/rise;
return new Vector3(Mathf.Cos(angle),-slope,Mathf.Sin(angle)).normalized;
}
for(int ring=0;ring<heights.Length-1;ring++)
for(int i=0;i<sides;i++)
{
float a=(i+.5f)*Mathf.PI*2/sides,b=(i+1.5f)*Mathf.PI*2/sides;
Vector3 lowA=new Vector3(Mathf.Cos(a)*radii[ring],heights[ring],Mathf.Sin(a)*radii[ring]);
Vector3 lowB=new Vector3(Mathf.Cos(b)*radii[ring],heights[ring],Mathf.Sin(b)*radii[ring]);
Vector3 highA=new Vector3(Mathf.Cos(a)*radii[ring+1],heights[ring+1],Mathf.Sin(a)*radii[ring+1]);
Vector3 highB=new Vector3(Mathf.Cos(b)*radii[ring+1],heights[ring+1],Mathf.Sin(b)*radii[ring+1]);
int first=vertices.Count;
vertices.Add(lowA);vertices.Add(highA);vertices.Add(highB);vertices.Add(lowB);
normals.Add(SurfaceNormal(a,ring));normals.Add(SurfaceNormal(a,ring+1));
normals.Add(SurfaceNormal(b,ring+1));normals.Add(SurfaceNormal(b,ring));
triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
}
for(int cap=0;cap<2;cap++)
{
int ring=cap==0?0:heights.Length-1;
var face=new Vector3[sides];
for(int i=0;i<sides;i++)
{
int index=cap==0?i:sides-1-i;
float angle=(index+.5f)*Mathf.PI*2/sides;
face[i]=new Vector3(Mathf.Cos(angle)*radii[ring],heights[ring],Mathf.Sin(angle)*radii[ring]);
}
int first=vertices.Count;vertices.AddRange(face);
Vector3 capNormal=cap==0?Vector3.down:Vector3.up;
for(int i=0;i<sides;i++)normals.Add(capNormal);
for(int i=1;i<sides-1;i++)
{
triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);
}
}
Mesh mesh=new Mesh{name=name};
mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
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


