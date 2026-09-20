using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Green Planet cosmetic shell for existing mech gameplay actors.
    /// Builds a compact Xeno-style creature from the same palette and mesh language
    /// as the Hot Zone Xeno Titan. This component owns visuals only.
    /// </summary>
    public sealed class GreenAlienMechVisual : MonoBehaviour
    {
        private readonly List<Mesh> meshes=new();
        private readonly List<Material> materials=new();
        private bool built;

        public static void Build(Transform host,int team,bool guardian)
        {
            if(host==null)return;
            GreenAlienMechVisual visual=host.GetComponent<GreenAlienMechVisual>();
            if(visual==null)visual=host.gameObject.AddComponent<GreenAlienMechVisual>();
            visual.BuildInternal(Mathf.Clamp(team,0,1),guardian);
        }

        private void BuildInternal(int team,bool guardian)
        {
            if(built)return;
            built=true;

            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return;

            Color bioEnergy=new(.54f,.86f,.16f,1f);
            Color armor=new(.17f,.245f,.12f,1f);
            Color hide=new(.055f,.085f,.052f,1f);
            Color joint=new(.035f,.055f,.038f,1f);
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;

            Material armorMat=MakeMaterial(shader,armor,.10f,.31f,Color.black);
            Material hideMat=MakeMaterial(shader,hide,.02f,.12f,Color.black);
            Material jointMat=MakeMaterial(shader,joint,.03f,.09f,Color.black);
            Material energyMat=MakeMaterial(shader,bioEnergy,.04f,.56f,bioEnergy*2.0f);
            Material teamMat=MakeMaterial(shader,teamEnergy,.10f,.46f,teamEnergy*1.45f);

            Mesh chest=Track(XenoTitanMeshFactory.Plate("DF_GreenMechChest",new[]{
                new Vector2(-.48f,-.52f),new Vector2(.48f,-.52f),new Vector2(.62f,.04f),
                new Vector2(.38f,.62f),new Vector2(0,.76f),new Vector2(-.38f,.62f),new Vector2(-.62f,.04f)},.14f));
            Mesh sidePlate=Track(XenoTitanMeshFactory.Plate("DF_GreenMechSide",new[]{
                new Vector2(-.34f,-.48f),new Vector2(.30f,-.44f),new Vector2(.44f,.10f),
                new Vector2(.18f,.58f),new Vector2(-.30f,.50f),new Vector2(-.46f,.02f)},.12f));
            Mesh shoulder=Track(XenoTitanMeshFactory.Plate("DF_GreenMechShoulder",new[]{
                new Vector2(-.42f,-.28f),new Vector2(.38f,-.34f),new Vector2(.50f,.16f),
                new Vector2(.18f,.48f),new Vector2(-.38f,.38f)},.14f));
            Mesh shinPlate=Track(XenoTitanMeshFactory.Plate("DF_GreenMechShin",new[]{
                new Vector2(-.28f,-.46f),new Vector2(.28f,-.46f),new Vector2(.38f,.18f),
                new Vector2(.10f,.52f),new Vector2(-.30f,.40f)},.11f));
            Mesh head=Track(XenoTitanMeshFactory.HeadShell("DF_GreenMechHead"));
            Mesh claw=Track(XenoTitanMeshFactory.ClawBlade("DF_GreenMechClaw",.58f,.13f,.10f,.12f));
            Mesh toe=Track(XenoTitanMeshFactory.ClawBlade("DF_GreenMechToe",.40f,.11f,.08f,.07f));
            Mesh spineTall=Track(XenoTitanMeshFactory.Spine("DF_GreenMechSpineTall",.70f,.19f,.22f));
            Mesh spineShort=Track(XenoTitanMeshFactory.Spine("DF_GreenMechSpineShort",.50f,.16f,.19f));
            Mesh teamMark=Track(XenoTitanMeshFactory.Plate("DF_GreenMechTeamMark",new[]{
                new Vector2(-.14f,-.10f),new Vector2(.14f,-.10f),new Vector2(.10f,.13f),
                new Vector2(0,.20f),new Vector2(-.10f,.13f)},.045f));

            Transform visualRoot=Pivot("DF_GreenAlienMechVisual",transform,new Vector3(0,.02f,0),Quaternion.identity);
            visualRoot.localScale=Vector3.one*(guardian?1.03f:.98f);

            // Organic pelvis and armored chest use the Xeno Titan's dark-hide / green-carapace language.
            Transform pelvis=Pivot("DF_GreenMechPelvis",visualRoot,new Vector3(0,.02f,-.03f),Quaternion.Euler(5,0,0));
            Part("PelvisMass",PrimitiveType.Sphere,pelvis,Vector3.zero,new Vector3(1.02f,.42f,.72f),hideMat,Quaternion.identity,false);
            MeshPart("HipCarapace",sidePlate,pelvis,new Vector3(0,.10f,.40f),new Vector3(.95f,.58f,.74f),armorMat,Quaternion.Euler(-4,0,0));

            Transform torso=Pivot("DF_GreenMechTorso",visualRoot,new Vector3(0,.72f,.02f),Quaternion.Euler(8,0,0));
            Part("TorsoMass",PrimitiveType.Sphere,torso,new Vector3(0,-.02f,-.03f),new Vector3(1.24f,.72f,.72f),hideMat,Quaternion.identity,false);
            MeshPart("ChestCenter",chest,torso,new Vector3(0,.02f,.52f),new Vector3(.90f,.86f,.92f),armorMat,Quaternion.Euler(-3,0,0));
            MeshPart("ChestLeft",sidePlate,torso,new Vector3(-.48f,.02f,.40f),new Vector3(.72f,.72f,.86f),armorMat,Quaternion.Euler(-2,-10,-5));
            MeshPart("ChestRight",sidePlate,torso,new Vector3(.48f,0,.40f),new Vector3(.76f,.70f,.86f),armorMat,Quaternion.Euler(-2,10,5));
            Part("BioCore",PrimitiveType.Sphere,torso,new Vector3(0,-.05f,.64f),new Vector3(.20f,.17f,.13f),energyMat,Quaternion.identity,true);
            MeshPart("TeamMark",teamMark,torso,new Vector3(0,.30f,.64f),new Vector3(.72f,.72f,.72f),teamMat,Quaternion.identity);

            // Narrow alien skull and jaw replace the robot sensor head.
            Transform headPivot=Pivot("DF_GreenMechHeadPivot",torso,new Vector3(0,.92f,.02f),Quaternion.Euler(-9,0,0));
            MeshPart("HeadShell",head,headPivot,new Vector3(0,0,.04f),new Vector3(.62f,.62f,.70f),armorMat,Quaternion.identity);
            MeshPart("HeadCowl",sidePlate,headPivot,new Vector3(0,.13f,-.34f),new Vector3(.62f,.40f,.82f),hideMat,Quaternion.Euler(8,0,180));
            Part("EyeL",PrimitiveType.Sphere,headPivot,new Vector3(-.17f,.02f,.54f),new Vector3(.095f,.065f,.055f),energyMat,Quaternion.identity,true);
            Part("EyeR",PrimitiveType.Sphere,headPivot,new Vector3(.17f,.02f,.54f),new Vector3(.095f,.065f,.055f),energyMat,Quaternion.identity,true);

            BuildArm(visualRoot,-1f,guardian,shoulder,sidePlate,claw,armorMat,hideMat,jointMat,teamMat);
            BuildArm(visualRoot,1f,guardian,shoulder,sidePlate,claw,armorMat,hideMat,jointMat,teamMat);
            BuildLeg(visualRoot,-1f,shinPlate,toe,armorMat,hideMat,jointMat);
            BuildLeg(visualRoot,1f,shinPlate,toe,armorMat,hideMat,jointMat);

            // Irregular dorsal spines are the strongest shared silhouette cue with the Hot Zone alien.
            Transform back=Pivot("DF_GreenMechBackSpines",torso,new Vector3(0,.20f,-.42f),Quaternion.Euler(20,0,0));
            MeshPart("SpineCenter",spineTall,back,new Vector3(.03f,.03f,-.04f),new Vector3(.86f,.96f,.86f),armorMat,Quaternion.Euler(8,4,-2));
            MeshPart("SpineL",spineShort,back,new Vector3(-.34f,-.08f,0),new Vector3(.75f,.78f,.78f),armorMat,Quaternion.Euler(13,-8,-17));
            MeshPart("SpineR",spineShort,back,new Vector3(.36f,-.12f,.02f),new Vector3(.68f,.68f,.74f),armorMat,Quaternion.Euler(12,8,14));
            if(guardian)
            {
                MeshPart("SpineGuardL",spineShort,back,new Vector3(-.58f,-.27f,.05f),new Vector3(.52f,.48f,.62f),hideMat,Quaternion.Euler(16,-11,-22));
                MeshPart("SpineGuardR",spineShort,back,new Vector3(.56f,-.30f,.06f),new Vector3(.48f,.44f,.58f),hideMat,Quaternion.Euler(15,10,20));
            }
        }

        private void BuildArm(Transform root,float side,bool guardian,Mesh shoulderMesh,Mesh forearmMesh,Mesh clawMesh,Material armor,Material hide,Material joint,Material team)
        {
            bool left=side<0f;
            Transform arm=Pivot(left?"DF_GreenMechArmL":"DF_GreenMechArmR",root,new Vector3(side*.78f,.74f,.02f),Quaternion.Euler(-5,0,side*7));
            Part(left?"ShoulderMassL":"ShoulderMassR",PrimitiveType.Sphere,arm,Vector3.zero,new Vector3(.48f,.40f,.50f),hide,Quaternion.identity,false);
            MeshPart(left?"ShoulderArmorL":"ShoulderArmorR",shoulderMesh,arm,new Vector3(side*.06f,.07f,.28f),new Vector3(guardian?.86f:.78f,guardian?.88f:.80f,.84f),armor,Quaternion.Euler(-2,side*8,side*-8));
            MeshPart(left?"ShoulderMarkL":"ShoulderMarkR",shoulderMesh,arm,new Vector3(side*.05f,.08f,.38f),new Vector3(.32f,.30f,.34f),team,Quaternion.Euler(-2,side*7,0));
            Part(left?"UpperArmL":"UpperArmR",PrimitiveType.Capsule,arm,new Vector3(0,-.43f,.02f),new Vector3(.22f,.48f,.24f),hide,Quaternion.Euler(0,0,side*3),false);
            MeshPart(left?"ForearmArmorL":"ForearmArmorR",forearmMesh,arm,new Vector3(0,-.75f,.22f),new Vector3(.62f,.52f,.66f),armor,Quaternion.Euler(-5,0,side*3));

            Transform clawRoot=Pivot(left?"DF_GreenMechClawL":"DF_GreenMechClawR",arm,new Vector3(0,-1.02f,.18f),Quaternion.Euler(-6,0,side*4));
            Part(left?"PalmL":"PalmR",PrimitiveType.Sphere,clawRoot,new Vector3(0,0,.04f),new Vector3(.40f,.24f,.48f),joint,Quaternion.identity,false);
            for(int i=-1;i<=1;i++)
                MeshPart(left?"ClawBladeL":"ClawBladeR",clawMesh,clawRoot,new Vector3(i*.14f,-.02f,.25f),new Vector3(.82f,i==0?.92f:.78f,i==0?.90f:.76f),joint,Quaternion.Euler(-7-i*2,side*i*6,side*i*3));
        }

        private void BuildLeg(Transform root,float side,Mesh shinMesh,Mesh toeMesh,Material armor,Material hide,Material joint)
        {
            bool left=side<0f;
            Transform leg=Pivot(left?"DF_GreenMechLegL":"DF_GreenMechLegR",root,new Vector3(side*.34f,-.12f,.02f),Quaternion.Euler(4,0,side*4));
            Part(left?"ThighL":"ThighR",PrimitiveType.Capsule,leg,new Vector3(0,-.27f,0),new Vector3(.30f,.42f,.32f),hide,Quaternion.identity,false);
            MeshPart(left?"ThighArmorL":"ThighArmorR",shinMesh,leg,new Vector3(0,-.25f,.27f),new Vector3(.72f,.66f,.75f),armor,Quaternion.Euler(-4,0,side*2));
            Part(left?"ShinL":"ShinR",PrimitiveType.Capsule,leg,new Vector3(0,-.62f,.06f),new Vector3(.24f,.38f,.28f),joint,Quaternion.Euler(6,0,side*2),false);
            MeshPart(left?"ShinArmorL":"ShinArmorR",shinMesh,leg,new Vector3(0,-.60f,.26f),new Vector3(.60f,.54f,.68f),armor,Quaternion.Euler(-7,0,side*2));
            Transform foot=Pivot(left?"DF_GreenMechFootL":"DF_GreenMechFootR",leg,new Vector3(0,-.92f,.22f),Quaternion.identity);
            Part(left?"FootMassL":"FootMassR",PrimitiveType.Sphere,foot,new Vector3(0,0,.12f),new Vector3(.46f,.18f,.62f),hide,Quaternion.identity,false);
            for(int i=-1;i<=1;i++)
                MeshPart(left?"ToeL":"ToeR",toeMesh,foot,new Vector3(i*.13f,-.03f,.28f),new Vector3(.72f,.68f,i==0?.86f:.72f),joint,Quaternion.Euler(-3,side*i*7,0));
        }

        private Material MakeMaterial(Shader shader,Color color,float metallic,float smoothness,Color emission)
        {
            Material material=new(shader){color=color};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(emission.maxColorComponent>0f&&material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",emission);
            }
            materials.Add(material);
            return material;
        }

        private Mesh Track(Mesh mesh){if(mesh!=null)meshes.Add(mesh);return mesh;}

        private static Transform Pivot(string name,Transform parent,Vector3 localPosition,Quaternion localRotation)
        {
            Transform pivot=new GameObject(name).transform;
            pivot.SetParent(parent,false);
            pivot.localPosition=localPosition;
            pivot.localRotation=localRotation;
            return pivot;
        }

        private static Transform Part(string name,PrimitiveType type,Transform parent,Vector3 localPosition,Vector3 localScale,Material material,Quaternion rotation,bool energy)
        {
            GameObject part=GameObject.CreatePrimitive(type);
            part.name="DF_GreenAlienMech_"+name;
            part.transform.SetParent(parent,false);
            part.transform.localPosition=localPosition;
            part.transform.localScale=localScale;
            part.transform.localRotation=rotation;
            Collider collider=part.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
            Renderer renderer=part.GetComponent<Renderer>();
            if(renderer!=null)
            {
                renderer.sharedMaterial=material;
                renderer.shadowCastingMode=energy?ShadowCastingMode.Off:ShadowCastingMode.On;
                renderer.receiveShadows=!energy;
            }
            return part.transform;
        }

        private static Transform MeshPart(string name,Mesh mesh,Transform parent,Vector3 localPosition,Vector3 localScale,Material material,Quaternion rotation)
        {
            GameObject part=new("DF_GreenAlienMech_"+name);
            part.transform.SetParent(parent,false);
            part.transform.localPosition=localPosition;
            part.transform.localScale=localScale;
            part.transform.localRotation=rotation;
            MeshFilter filter=part.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            MeshRenderer renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            return part.transform;
        }

        private void OnDestroy()
        {
            foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(Material material in materials)if(material!=null)Destroy(material);
        }
    }
}
