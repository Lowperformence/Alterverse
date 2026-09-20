using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Mars-only cosmetic shell for existing mech gameplay actors.
    /// Owns visuals only: no health, movement, combat, AI or collider behavior.
    /// </summary>
    public sealed class MarsStoneGolemMechVisual : MonoBehaviour
    {
        private readonly List<Mesh> meshes=new();
        private readonly List<Material> materials=new();
        private bool built;

        public static void Build(Transform host,int team,bool guardian)
        {
            if(host==null)return;
            MarsStoneGolemMechVisual visual=host.GetComponent<MarsStoneGolemMechVisual>();
            if(visual==null)visual=host.gameObject.AddComponent<MarsStoneGolemMechVisual>();
            visual.BuildInternal(Mathf.Clamp(team,0,1),guardian);
        }

        private void BuildInternal(int team,bool guardian)
        {
            if(built)return;
            built=true;

            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return;

            Color stone=new(.42f,.19f,.105f,1f);
            Color stoneLight=new(.58f,.29f,.14f,1f);
            Color stoneDark=new(.15f,.075f,.052f,1f);
            Color seam=new(.075f,.035f,.028f,1f);
            Color magma=new(1f,.22f,.035f,1f);
            Color magmaHot=new(1f,.48f,.055f,1f);
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;

            Material stoneMat=MakeMaterial(shader,stone,.03f,.19f,Color.black);
            Material lightStoneMat=MakeMaterial(shader,stoneLight,.025f,.16f,Color.black);
            Material darkStoneMat=MakeMaterial(shader,stoneDark,.02f,.10f,Color.black);
            Material seamMat=MakeMaterial(shader,seam,.01f,.07f,Color.black);
            Material magmaMat=MakeMaterial(shader,magma,.02f,.43f,magma*3.15f);
            Material magmaHotMat=MakeMaterial(shader,magmaHot,.02f,.50f,magmaHot*3.6f);
            Material teamMat=MakeMaterial(shader,teamEnergy,.06f,.36f,teamEnergy*1.25f);

            Mesh chestPlate=Track(XenoTitanMeshFactory.Plate("DF_MarsGolemChest",new[]{
                new Vector2(-.56f,-.50f),new Vector2(.50f,-.55f),new Vector2(.67f,-.04f),
                new Vector2(.48f,.56f),new Vector2(.10f,.74f),new Vector2(-.38f,.66f),new Vector2(-.66f,.08f)},.22f));
            Mesh shoulderPlate=Track(XenoTitanMeshFactory.Plate("DF_MarsGolemShoulder",new[]{
                new Vector2(-.43f,-.31f),new Vector2(.38f,-.36f),new Vector2(.53f,.06f),
                new Vector2(.20f,.48f),new Vector2(-.35f,.40f),new Vector2(-.51f,.03f)},.20f));
            Mesh forearmPlate=Track(XenoTitanMeshFactory.Plate("DF_MarsGolemForearm",new[]{
                new Vector2(-.32f,-.48f),new Vector2(.34f,-.45f),new Vector2(.42f,.14f),
                new Vector2(.08f,.54f),new Vector2(-.34f,.42f)},.18f));
            Mesh shinPlate=Track(XenoTitanMeshFactory.Plate("DF_MarsGolemShin",new[]{
                new Vector2(-.30f,-.48f),new Vector2(.31f,-.46f),new Vector2(.39f,.13f),
                new Vector2(.12f,.53f),new Vector2(-.31f,.41f)},.17f));
            Mesh browPlate=Track(XenoTitanMeshFactory.Plate("DF_MarsGolemBrow",new[]{
                new Vector2(-.42f,-.16f),new Vector2(.42f,-.16f),new Vector2(.31f,.24f),
                new Vector2(.04f,.37f),new Vector2(-.35f,.25f)},.22f));
            Mesh backShardTall=Track(XenoTitanMeshFactory.Spine("DF_MarsGolemBackTall",.68f,.30f,.34f));
            Mesh backShardShort=Track(XenoTitanMeshFactory.Spine("DF_MarsGolemBackShort",.48f,.24f,.28f));
            Mesh teamRune=Track(XenoTitanMeshFactory.Plate("DF_MarsGolemRune",new[]{
                new Vector2(-.13f,-.14f),new Vector2(.13f,-.14f),new Vector2(.09f,.12f),
                new Vector2(0,.22f),new Vector2(-.10f,.11f)},.045f));

            Transform visualRoot=Pivot("DF_MarsStoneGolemVisual",transform,new Vector3(0,.02f,0),Quaternion.identity);
            visualRoot.localScale=Vector3.one*(guardian?1.08f:.95f);

            // Broad rock pelvis and layered torso; guardian is deliberately wider and more defensive.
            Transform pelvis=Pivot("DF_MarsGolemPelvis",visualRoot,new Vector3(0,.02f,-.03f),Quaternion.Euler(guardian?2f:7f,0,0));
            Rock("PelvisCore",pelvis,Vector3.zero,new Vector3(guardian?1.20f:1.02f,.42f,.78f),darkStoneMat,new Vector3(3,guardian?0:5,guardian?-2:2));
            Rock("PelvisL",pelvis,new Vector3(-.42f,.08f,.05f),new Vector3(.54f,.38f,.55f),stoneMat,new Vector3(-4,-7,-9));
            Rock("PelvisR",pelvis,new Vector3(.40f,.06f,.02f),new Vector3(.52f,.36f,.53f),lightStoneMat,new Vector3(5,8,7));

            Transform torso=Pivot("DF_MarsGolemTorso",visualRoot,new Vector3(0,.77f,.01f),Quaternion.Euler(guardian?3f:10f,0,0));
            Rock("TorsoMass",torso,new Vector3(0,-.06f,-.05f),new Vector3(guardian?1.55f:1.30f,.78f,.80f),darkStoneMat,new Vector3(-2,3,1));
            MeshPart("ChestSlab",chestPlate,torso,new Vector3(0,.02f,.55f),new Vector3(guardian?1.13f:.96f,guardian?1.02f:.90f,1f),stoneMat,Quaternion.Euler(-4,0,guardian?0:-3));
            MeshPart("ChestShardL",shoulderPlate,torso,new Vector3(-.52f,.00f,.46f),new Vector3(.72f,.76f,.82f),lightStoneMat,Quaternion.Euler(-5,-12,-6));
            MeshPart("ChestShardR",shoulderPlate,torso,new Vector3(.51f,-.03f,.45f),new Vector3(.76f,.73f,.84f),stoneMat,Quaternion.Euler(-3,13,7));

            // Magma heart and split cracks create an ancient elemental core instead of machinery.
            Part("MagmaCore",PrimitiveType.Sphere,torso,new Vector3(0,-.08f,.68f),new Vector3(.20f,.18f,.14f),magmaHotMat,Quaternion.identity,true);
            Crack("ChestCrackA",torso,new Vector3(-.11f,.18f,.69f),new Vector3(.035f,.30f,.025f),magmaMat,new Vector3(0,0,-22));
            Crack("ChestCrackB",torso,new Vector3(.10f,-.23f,.68f),new Vector3(.030f,.24f,.023f),magmaMat,new Vector3(0,0,19));
            Crack("ChestCrackC",torso,new Vector3(.22f,.04f,.67f),new Vector3(.028f,.18f,.021f),magmaMat,new Vector3(0,0,57));
            MeshPart("TeamRune",teamRune,torso,new Vector3(0,.34f,.69f),new Vector3(.72f,.72f,.72f),teamMat,Quaternion.identity);

            // Small primitive stone head with a heavy brow and two magma eyes.
            Transform head=Pivot("DF_MarsGolemHead",torso,new Vector3(0,.96f,.03f),Quaternion.Euler(-7,0,0));
            Rock("HeadBlock",head,Vector3.zero,new Vector3(.68f,.48f,.60f),stoneMat,new Vector3(-4,2,2));
            MeshPart("BrowRock",browPlate,head,new Vector3(0,.10f,.35f),new Vector3(.82f,.62f,.78f),lightStoneMat,Quaternion.Euler(-5,0,0));
            Rock("JawRock",head,new Vector3(0,-.24f,.24f),new Vector3(.51f,.22f,.34f),darkStoneMat,new Vector3(-6,0,0));
            Part("EyeL",PrimitiveType.Sphere,head,new Vector3(-.17f,.02f,.46f),new Vector3(.090f,.060f,.050f),magmaHotMat,Quaternion.identity,true);
            Part("EyeR",PrimitiveType.Sphere,head,new Vector3(.17f,.02f,.46f),new Vector3(.090f,.060f,.050f),magmaHotMat,Quaternion.identity,true);

            BuildArm(visualRoot,-1f,guardian,shoulderPlate,forearmPlate,stoneMat,lightStoneMat,darkStoneMat,seamMat,magmaMat);
            BuildArm(visualRoot,1f,guardian,shoulderPlate,forearmPlate,stoneMat,lightStoneMat,darkStoneMat,seamMat,magmaMat);
            BuildLeg(visualRoot,-1f,guardian,shinPlate,stoneMat,lightStoneMat,darkStoneMat,seamMat);
            BuildLeg(visualRoot,1f,guardian,shinPlate,stoneMat,lightStoneMat,darkStoneMat,seamMat);

            // Uneven rock shards reinforce the ancient Mars silhouette without reading as antennae.
            Transform back=Pivot("DF_MarsGolemBack",torso,new Vector3(0,.25f,-.46f),Quaternion.Euler(18,0,0));
            MeshPart("BackShardCenter",backShardTall,back,new Vector3(.03f,.02f,-.03f),new Vector3(.92f,guardian?1.08f:.86f,.92f),stoneMat,Quaternion.Euler(6,4,-2));
            MeshPart("BackShardL",backShardShort,back,new Vector3(-.39f,-.10f,.01f),new Vector3(.76f,.78f,.80f),lightStoneMat,Quaternion.Euler(12,-8,-18));
            MeshPart("BackShardR",backShardShort,back,new Vector3(.41f,-.14f,.02f),new Vector3(.68f,.68f,.75f),stoneMat,Quaternion.Euler(10,9,15));
            if(guardian)
            {
                MeshPart("GuardShardL",backShardShort,back,new Vector3(-.67f,-.31f,.05f),new Vector3(.56f,.52f,.62f),darkStoneMat,Quaternion.Euler(16,-12,-25));
                MeshPart("GuardShardR",backShardShort,back,new Vector3(.64f,-.34f,.06f),new Vector3(.52f,.48f,.60f),darkStoneMat,Quaternion.Euler(15,11,22));
            }
        }

        private void BuildArm(Transform root,float side,bool guardian,Mesh shoulderMesh,Mesh forearmMesh,Material stone,Material lightStone,Material darkStone,Material seam,Material magma)
        {
            bool left=side<0f;
            float shoulderX=guardian?.91f:.79f;
            Transform arm=Pivot(left?"DF_MarsGolemArmL":"DF_MarsGolemArmR",root,new Vector3(side*shoulderX,.78f,.01f),Quaternion.Euler(-4,0,side*(guardian?5f:9f)));
            Rock(left?"ShoulderMassL":"ShoulderMassR",arm,Vector3.zero,new Vector3(guardian?.62f:.52f,guardian?.54f:.46f,.58f),darkStone,new Vector3(2,side*5,side*8));
            MeshPart(left?"ShoulderSlabL":"ShoulderSlabR",shoulderMesh,arm,new Vector3(side*.06f,.08f,.32f),new Vector3(guardian?.98f:.82f,guardian?.95f:.82f,.90f),left?lightStone:stone,Quaternion.Euler(-2,side*9,side*-7));

            Transform upper=Pivot(left?"DF_MarsGolemUpperArmL":"DF_MarsGolemUpperArmR",arm,new Vector3(0,-.48f,.01f),Quaternion.Euler(0,0,side*(guardian?1f:5f)));
            Rock(left?"UpperRockL":"UpperRockR",upper,Vector3.zero,new Vector3(.34f,.52f,.36f),stone,new Vector3(side*4,2,side*3));

            Transform fore=Pivot(left?"DF_MarsGolemForearmL":"DF_MarsGolemForearmR",arm,new Vector3(0,-.92f,.14f),Quaternion.Euler(5,0,side*3));
            Rock(left?"ForearmMassL":"ForearmMassR",fore,new Vector3(0,-.05f,.02f),new Vector3(guardian?.54f:.47f,guardian?.66f:.58f,.52f),darkStone,new Vector3(-4,side*5,side*2));
            MeshPart(left?"ForearmSlabL":"ForearmSlabR",forearmMesh,fore,new Vector3(0,-.06f,.31f),new Vector3(guardian?.88f:.76f,guardian?.83f:.72f,.82f),left?stone:lightStone,Quaternion.Euler(-6,0,side*4));
            Crack(left?"ArmCrackL":"ArmCrackR",fore,new Vector3(side*.06f,-.05f,.42f),new Vector3(.026f,.31f,.020f),magma,new Vector3(0,0,side*15));

            Transform fist=Pivot(left?"DF_MarsGolemFistL":"DF_MarsGolemFistR",fore,new Vector3(0,-.47f,.12f),Quaternion.Euler(-4,0,side*4));
            Rock(left?"FistL":"FistR",fist,Vector3.zero,new Vector3(guardian?.60f:.52f,.36f,.62f),stone,new Vector3(-3,side*5,side*4));
            Rock(left?"KnuckleL":"KnuckleR",fist,new Vector3(0,.02f,.31f),new Vector3(.48f,.22f,.24f),lightStone,new Vector3(-6,0,0));
        }

        private void BuildLeg(Transform root,float side,bool guardian,Mesh shinMesh,Material stone,Material lightStone,Material darkStone,Material seam)
        {
            bool left=side<0f;
            Transform leg=Pivot(left?"DF_MarsGolemLegL":"DF_MarsGolemLegR",root,new Vector3(side*(guardian?.42f:.35f),-.13f,.01f),Quaternion.Euler(3,0,side*3));
            Rock(left?"ThighL":"ThighR",leg,new Vector3(0,-.27f,0),new Vector3(guardian?.42f:.35f,.48f,.40f),darkStone,new Vector3(side*4,0,side*2));
            MeshPart(left?"ThighSlabL":"ThighSlabR",shinMesh,leg,new Vector3(0,-.25f,.28f),new Vector3(guardian?.82f:.70f,.70f,.76f),left?lightStone:stone,Quaternion.Euler(-4,0,side*2));
            Rock(left?"KneeL":"KneeR",leg,new Vector3(0,-.53f,.12f),new Vector3(.34f,.25f,.36f),seam,new Vector3(0,side*4,0));
            Rock(left?"ShinL":"ShinR",leg,new Vector3(0,-.72f,.05f),new Vector3(guardian?.37f:.31f,.43f,.36f),stone,new Vector3(5,side*2,side*2));
            MeshPart(left?"ShinSlabL":"ShinSlabR",shinMesh,leg,new Vector3(0,-.70f,.28f),new Vector3(.62f,.58f,.68f),darkStone,Quaternion.Euler(-7,0,side*2));

            Transform foot=Pivot(left?"DF_MarsGolemFootL":"DF_MarsGolemFootR",leg,new Vector3(0,-1.00f,.20f),Quaternion.identity);
            Rock(left?"FootL":"FootR",foot,Vector3.zero,new Vector3(guardian?.61f:.53f,.23f,.76f),darkStone,new Vector3(0,side*2,0));
            Rock(left?"ToeRockL":"ToeRockR",foot,new Vector3(0,-.01f,.34f),new Vector3(.48f,.18f,.36f),lightStone,new Vector3(-4,0,0));
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

        private static Transform Rock(string name,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation)
        {
            return Part(name,PrimitiveType.Cube,parent,position,scale,material,Quaternion.Euler(rotation),false);
        }

        private static Transform Crack(string name,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation)
        {
            return Part(name,PrimitiveType.Cube,parent,position,scale,material,Quaternion.Euler(rotation),true);
        }

        private static Transform Part(string name,PrimitiveType type,Transform parent,Vector3 localPosition,Vector3 localScale,Material material,Quaternion rotation,bool energy)
        {
            GameObject part=GameObject.CreatePrimitive(type);
            part.name="DF_MarsStoneGolem_"+name;
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
            GameObject part=new("DF_MarsStoneGolem_"+name);
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
