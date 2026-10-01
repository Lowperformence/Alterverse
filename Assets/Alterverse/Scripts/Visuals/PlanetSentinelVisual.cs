using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Dedicated exoplanet guard rigs and six-legged sandstone Hot Zone beast.</summary>
    public sealed class PlanetSentinelVisual : MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        readonly List<Transform> legs=new();
        readonly List<Quaternion> rest=new();
        Transform root,head,leftArm,rightArm;
        Mesh rock,plate,gem;
        Material armor,pale,dark,energy,team;
        Vector3 previous;
        float phase,blend,attackUntil;
        bool beast,magentaBeast;
        public Transform Muzzle { get; private set; }
        public Color EnergyColor { get; private set; }

        public static PlanetSentinelVisual Build(Transform host,int assignedTeam,bool monster)
        {
            var visual=host.gameObject.AddComponent<PlanetSentinelVisual>();
            visual.BuildInternal(assignedTeam,monster);return visual;
        }

        void BuildInternal(int assignedTeam,bool monster)
        {
            beast=monster;
            magentaBeast=monster&&ArenaVariantRuntime.IsGJ504b;
            bool crystal=ArenaVariantRuntime.IsDiamondCore;
            EnergyColor=crystal?new Color(.30f,.86f,1f):magentaBeast?new Color(1f,.08f,.72f):new Color(1f,.48f,.76f);
            armor=Mat(crystal?new Color(.30f,.58f,.72f):magentaBeast?new Color(.47f,.025f,.24f):new Color(.43f,.09f,.29f),crystal?.70f:magentaBeast?.38f:.10f);
            pale=Mat(crystal?new Color(.73f,.93f,1f):magentaBeast?new Color(.96f,.22f,.57f):new Color(.68f,.23f,.44f),crystal?.88f:magentaBeast?.48f:.16f);
            dark=Mat(crystal?new Color(.025f,.055f,.095f):magentaBeast?new Color(.095f,.008f,.065f):new Color(.12f,.027f,.085f),.20f);
            energy=Mat(EnergyColor,.65f,true);
            team=Mat(assignedTeam==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy,.50f,true);
            rock=Own(LunarFragmentGeometry.Create(127));
            plate=Own(XenoTitanMeshFactory.Plate("DF_ExoplanetArmorPlate",new[]{
                new Vector2(-.5f,-.4f),new Vector2(.5f,-.4f),new Vector2(.65f,.1f),
                new Vector2(.3f,.55f),new Vector2(-.3f,.55f),new Vector2(-.65f,.1f)},.18f));
            if(crystal)gem=Own(DiamondCoreCrystalVisuals.BuildGuardianGemMesh());
            root=Pivot(monster?"DF_GJ504bSandstoneBeast":"DF_ExoplanetMechGuard",transform,Vector3.zero);
            if(monster)BuildBeast();else BuildGuard(crystal);
            previous=transform.position;
        }

        void BuildGuard(bool crystal)
        {
            // GJ: layered sandstone shield plates. PSR: narrow prism chassis and crystal crown.
            Mesh shell=crystal?gem:rock;
            Part("Pelvis",shell,root,new Vector3(0,.1f,0),new Vector3(.9f,.38f,.65f),dark);
            Part("Chest",shell,root,new Vector3(0,.92f,0),new Vector3(crystal?1.15f:1.55f,.92f,.72f),armor);
            Part("ChestArmor",plate,root,new Vector3(0,.95f,.50f),new Vector3(.85f,.75f,1),pale);
            Part("PowerHeart",crystal?gem:plate,root,new Vector3(0,.88f,.64f),new Vector3(.23f,.38f,.23f),energy);
            Part("TeamBadge",plate,root,new Vector3(0,1.23f,.64f),new Vector3(.15f,.12f,.12f),team);
            head=Pivot("SensorHead",root,new Vector3(0,1.72f,0));
            Part("Helmet",shell,head,Vector3.zero,new Vector3(.65f,.48f,.50f),pale);
            Part("Sensor",plate,head,new Vector3(0,0,.35f),new Vector3(.30f,.065f,.1f),energy);
            Muzzle=Pivot("GuardMuzzle",head,new Vector3(0,0,.43f));
            for(int side=-1;side<=1;side+=2)
            {
                Transform leg=Pivot("Leg"+side,root,new Vector3(side*.43f,-.10f,0));
                AddLeg(leg);
                Part("Thigh",shell,leg,new Vector3(0,-.3f,0),new Vector3(.36f,.54f,.35f),dark);
                Part("Shin",shell,leg,new Vector3(0,-.83f,.05f),new Vector3(.42f,.61f,.40f),armor);
                Part("Foot",rock,leg,new Vector3(0,-1.3f,.22f),new Vector3(.53f,.25f,.72f),dark);
                Transform arm=Pivot("Arm"+side,root,new Vector3(side*.94f,1.01f,0));
                if(side<0)leftArm=arm;else rightArm=arm;
                Part("Shoulder",shell,arm,Vector3.zero,new Vector3(.64f,.50f,.64f),pale);
                Part("ArmJoint",rock,arm,new Vector3(0,-.43f,0),new Vector3(.25f,.49f,.27f),dark);
                Part("Forearm",shell,arm,new Vector3(0,-.85f,.17f),new Vector3(.49f,.61f,.52f),armor);
                Part("Hand",rock,arm,new Vector3(0,-1.18f,.23f),new Vector3(.42f,.30f,.43f),dark);
                if(crystal)
                    Part("ShoulderPrism",gem,arm,new Vector3(side*.12f,.44f,0),new Vector3(.24f,.70f,.24f),energy);
                else
                    Part("SandstoneShield",plate,arm,new Vector3(side*.08f,-.62f,.46f),new Vector3(.64f,.98f,1),pale);
            }
            if(crystal)
                for(int i=-1;i<=1;i++)Part("CrownPrism"+i,gem,head,new Vector3(i*.23f,.35f,0),new Vector3(.13f,.48f-i*i*.12f,.13f),energy);
            else
                for(int i=0;i<3;i++)Part("BackSandstoneFin"+i,plate,root,new Vector3(0,.8f+i*.26f,-.44f),new Vector3(.65f-i*.10f,.2f,1),armor);
        }

        void BuildBeast()
        {
            Part("LowSandstoneCarapace",rock,root,new Vector3(0,.05f,0),new Vector3(1.9f,.80f,2.3f),dark);
            for(int i=0;i<5;i++)
                Part("DorsalSandstonePlate"+i,plate,root,new Vector3(0,.35f,-.85f+i*.40f),new Vector3(1.1f,.44f,1.6f),i%2==0?armor:pale,Quaternion.Euler(76,0,0));
            head=Pivot("BurrowerHead",root,new Vector3(0,.10f,1.35f));
            Part("HeadShield",rock,head,Vector3.zero,new Vector3(.96f,.57f,.75f),armor);
            for(int side=-1;side<=1;side+=2)
            {
                Part("Eye"+side,plate,head,new Vector3(side*.25f,.16f,.43f),new Vector3(.12f,.07f,.08f),energy);
                Part("Jaw"+side,plate,head,new Vector3(side*.31f,-.18f,.54f),new Vector3(.23f,.48f,.8f),pale,Quaternion.Euler(-64,side*22,0));
                for(int i=0;i<3;i++)
                {
                    Transform leg=Pivot("BurrowerLeg"+side+"_"+i,root,new Vector3(side*.80f,0,.70f-i*.72f));
                    AddLeg(leg);
                    Part("Upper",plate,leg,new Vector3(side*.47f,-.05f,0),new Vector3(.65f,.20f,1),armor,Quaternion.Euler(0,0,side*-12));
                    Part("Lower",rock,leg,new Vector3(side*.89f,-.45f,.05f),new Vector3(.23f,.85f,.28f),pale,Quaternion.Euler(0,0,side*23));
                }
                Transform claw=Pivot("SiegeClaw"+side,root,new Vector3(side*.92f,.20f,1.04f));
                if(side<0)leftArm=claw;else rightArm=claw;
                Part("ClawArmor",rock,claw,new Vector3(side*.35f,-.05f,.36f),new Vector3(.71f,.40f,.96f),pale);
                Part("ClawTip",plate,claw,new Vector3(side*.33f,-.06f,.91f),new Vector3(.32f,.61f,.6f),armor,Quaternion.Euler(90,side*12,0));
            }
            Part("TeamRune",plate,root,new Vector3(0,.65f,.3f),new Vector3(.26f,.24f,.12f),team,Quaternion.Euler(90,0,0));
            if(magentaBeast)
            {
                // Bright geode ridges give the Hot Zone beast a readable magenta silhouette.
                for(int i=0;i<7;i++)
                {
                    float x=(i-3)*.24f;
                    float y=.70f+Mathf.Sin(i*.8f)*.11f;
                    Part("MagentaGeodeSpine"+i,plate,root,new Vector3(x,y,-.18f),new Vector3(.17f,.52f-(i%3)*.07f,.20f),energy,Quaternion.Euler(0,0,(i-3)*8f));
                }
                Part("MagentaBrow",plate,head,new Vector3(0,.30f,.31f),new Vector3(.82f,.20f,.36f),pale,Quaternion.Euler(-20,0,0));
                Part("CoreGlow",gem??plate,root,new Vector3(0,.28f,.52f),new Vector3(.24f,.30f,.16f),energy);
            }
            Muzzle=Pivot("SandPulseMuzzle",head,new Vector3(0,.05f,.57f));
        }

        Material Mat(Color color,float smooth,bool glow=false)
        {
            Material m=RuntimeShaderLibrary.Create(color,false,.08f,smooth,glow?color*2f:Color.black);
            if(m==null)throw new System.InvalidOperationException("Exoplanet sentinel shader unavailable");
            m.enableInstancing=true;materials.Add(m);return m;
        }
        Mesh Own(Mesh mesh){meshes.Add(mesh);return mesh;}
        void AddLeg(Transform leg){legs.Add(leg);rest.Add(leg.localRotation);}
        static Transform Pivot(string name,Transform parent,Vector3 p)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;return go.transform;
        }
        static void Part(string name,Mesh mesh,Transform parent,Vector3 p,Vector3 scale,Material material,Quaternion? rotation=null)
        {
            Transform part=Pivot(name,parent,p);part.localScale=scale;part.localRotation=rotation??Quaternion.identity;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;
        }
        public void PulseAttack()=>attackUntil=Time.time+.38f;
        void LateUpdate()
        {
            if(root==null)return;
            if(Time.timeScale<=0f){previous=transform.position;return;}
            Vector3 delta=transform.position-previous;delta.y=0;previous=transform.position;
            if(delta.sqrMagnitude>9f){blend=0;phase=0;delta=Vector3.zero;}
            blend=Mathf.Lerp(blend,Mathf.Clamp01(delta.magnitude/Mathf.Max(.001f,Time.deltaTime)/2f),1f-Mathf.Exp(-12f*Time.deltaTime));
            phase=Mathf.Repeat(phase+delta.magnitude*3.4f,Mathf.PI*2f);
            for(int i=0;i<legs.Count;i++)
                legs[i].localRotation=rest[i]*Quaternion.Euler(Mathf.Sin(phase+i*Mathf.PI)*(beast?19f:29f)*blend,0,0);
            float attack=Time.time<attackUntil?Mathf.Sin((1f-(attackUntil-Time.time)/.38f)*Mathf.PI):0;
            root.localPosition=Vector3.up*(Mathf.Sin(Time.time*1.5f)*.009f+blend*Mathf.Abs(Mathf.Sin(phase))*.025f);
            root.localRotation=Quaternion.Euler(attack*-8f,0,0);
            if(leftArm!=null)leftArm.localRotation=Quaternion.Euler(-attack*34f,0,beast?attack*-10f:0);
            if(rightArm!=null)rightArm.localRotation=Quaternion.Euler(-attack*34f,0,beast?attack*10f:0);
        }
        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(var material in materials)if(material!=null)Destroy(material);
        }
    }
}
