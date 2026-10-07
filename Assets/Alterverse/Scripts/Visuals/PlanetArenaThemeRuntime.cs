using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Adds visual-only arena dressing for the procedural Earth, Pyraxis and Neptune variants.
    /// All props are collider-free so gameplay navigation, hitboxes and lane geometry remain unchanged.
    /// It also swaps only the rendered Hot Zone creature body for Pyraxis / Neptune while preserving
    /// the existing HotZoneTank AI, health, team ownership and combat behaviour.
    /// </summary>
    public sealed class PlanetArenaThemeRuntime : MonoBehaviour
    {
        readonly List<Material> ownedMaterials=new();
        Transform detailRoot;
        ArenaVariant builtFor=(ArenaVariant)(-1);
        float nextBossScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_PlanetArenaThemeRuntime")!=null)return;
            GameObject go=new("DF_PlanetArenaThemeRuntime");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PlanetArenaThemeRuntime>();
        }

        void Update()
        {
            GameObject world=GameObject.Find("DF_MatchWorld");
            ArenaVariant variant=ArenaVariantRuntime.Current;
            bool themed=variant==ArenaVariant.Earth||variant==ArenaVariant.Pyraxis||variant==ArenaVariant.Neptune;

            if(world==null)
            {
                if(detailRoot==null&&ownedMaterials.Count>0)ReleaseMaterials();
                builtFor=(ArenaVariant)(-1);
                return;
            }

            if(themed)
            {
                if(detailRoot==null||builtFor!=variant)BuildTheme(world.transform,variant);
            }
            else if(detailRoot!=null||ownedMaterials.Count>0)
            {
                ClearTheme();
                builtFor=variant;
            }

            if(Time.unscaledTime>=nextBossScan)
            {
                nextBossScan=Time.unscaledTime+.30f;
                ApplyHotZoneCreatureVisual(variant);
            }
        }

        void BuildTheme(Transform world,ArenaVariant variant)
        {
            ClearTheme();
            clearance=new SceneryBuildingClearance();
            builtFor=variant;
            GameObject root=new("DF_PlanetTheme_"+variant);
            detailRoot=root.transform;
            detailRoot.SetParent(world,false);

            switch(variant)
            {
                case ArenaVariant.Earth: BuildEarth(); break;
                case ArenaVariant.Pyraxis: BuildPyraxis(); break;
                case ArenaVariant.Neptune: BuildNeptune(); break;
            }
        }

        void BuildEarth()
        {
            Material water=MakeMaterial("DF_Earth_Water",new Color(.025f,.22f,.52f,1f),.05f,.88f,true,.45f);
            Material bark=MakeMaterial("DF_Earth_Bark",new Color(.20f,.095f,.035f,1f),0f,.16f);
            Material leaves=MakeMaterial("DF_Earth_Leaves",new Color(.08f,.34f,.055f,1f),0f,.22f);
            Material leavesLight=MakeMaterial("DF_Earth_LeavesLight",new Color(.16f,.52f,.08f,1f),0f,.20f);
            Material stone=MakeMaterial("DF_Earth_Stone",new Color(.22f,.18f,.12f,1f),0f,.18f);

            Vector3[] pools={new(-28,0,17),new(27,0,-18),new(-17,0,-27),new(18,0,27)};
            Vector3[] poolScales={new(7.8f,.09f,4.6f),new(6.6f,.09f,4.2f),new(5.6f,.08f,3.4f),new(5.2f,.08f,3.2f)};
            for(int i=0;i<pools.Length;i++)
            {
                Vector3 p=Surface(pools[i].x,pools[i].z,.035f);
                Primitive(PrimitiveType.Cylinder,"WaterPool",detailRoot,p,poolScales[i],Quaternion.Euler(0,i*29f,0),water);
            }

            Vector3[] treePositions={
                new(-38,0,12),new(-35,0,-16),new(-27,0,29),new(29,0,-28),
                new(37,0,15),new(35,0,-10),new(-12,0,32),new(13,0,-32),
                new(-31,0,4),new(31,0,-2)
            };
            for(int i=0;i<treePositions.Length;i++)
            {
                Vector3 basePos=Surface(treePositions[i].x,treePositions[i].z,0f);
                float height=2.4f+(i%3)*.42f;
                Primitive(PrimitiveType.Cylinder,"TreeTrunk",detailRoot,basePos+Vector3.up*height*.5f,
                    new Vector3(.35f,height*.5f,.35f),Quaternion.Euler(0,i*31f,0),bark);
                Primitive(PrimitiveType.Sphere,"TreeCrown",detailRoot,basePos+Vector3.up*(height+.75f),
                    new Vector3(1.45f,.90f,1.30f),Quaternion.Euler(0,i*19f,0),i%2==0?leaves:leavesLight);
                Primitive(PrimitiveType.Sphere,"TreeCrownSmall",detailRoot,basePos+new Vector3((i%2==0?.55f:-.55f),height+.45f,.20f),
                    new Vector3(.92f,.66f,.82f),Quaternion.identity,leaves);
            }

            Vector3[] rocks={new(-40,0,-25),new(39,0,25),new(-23,0,-32),new(24,0,32),new(-33,0,28),new(33,0,-29)};
            for(int i=0;i<rocks.Length;i++)
            {
                Vector3 p=Surface(rocks[i].x,rocks[i].z,.35f);
                Primitive(PrimitiveType.Sphere,"EarthRock",detailRoot,p,new Vector3(1.4f,.75f,1.1f),
                    Quaternion.Euler(0,i*41f,12f),stone);
            }
        }

        SceneryBuildingClearance clearance;

        void BuildPyraxis()
        {
            Material basalt=MakeMaterial("DF_Pyraxis_Basalt",new Color(.035f,.024f,.021f,1f),.10f,.18f);
            Material basaltEdge=MakeMaterial("DF_Pyraxis_BasaltEdge",new Color(.10f,.045f,.018f,1f),.05f,.22f);
            Material lava=MakeMaterial("DF_Pyraxis_Lava",new Color(1f,.23f,.012f,1f),0f,.45f,true,3.3f);
            Material hotLava=MakeMaterial("DF_Pyraxis_HotLava",new Color(1f,.58f,.045f,1f),0f,.55f,true,4.2f);

            Vector3[] vents={new(-31,0,21),new(32,0,-20),new(-17,0,-29),new(18,0,29),new(-38,0,-9),new(38,0,10)};
            for(int i=0;i<vents.Length;i++)
            {
                Vector3 p=Surface(vents[i].x,vents[i].z,0f);
                Primitive(PrimitiveType.Sphere,"BasaltMass",detailRoot,p+Vector3.up*.55f,
                    new Vector3(2.2f,1.05f,1.65f),Quaternion.Euler(0,i*37f,0),basalt);
                Primitive(PrimitiveType.Cylinder,"LavaVent",detailRoot,p+Vector3.up*.88f,
                    new Vector3(.70f,.055f,.70f),Quaternion.identity,i%2==0?hotLava:lava);
                Primitive(PrimitiveType.Cube,"BasaltSpire",detailRoot,p+new Vector3((i%2==0?1.6f:-1.6f),1.35f,.35f),
                    new Vector3(.55f,2.6f,.70f),Quaternion.Euler(10f,i*53f,8f),basaltEdge);
            }

            Vector3[] streams={new(-24,0,31),new(24,0,-31),new(-37,0,16),new(37,0,-16)};
            for(int i=0;i<streams.Length;i++)
            {
                Vector3 p=Surface(streams[i].x,streams[i].z,.04f);
                Primitive(PrimitiveType.Cube,"LavaStream",detailRoot,p,new Vector3(4.3f,.055f,.42f),
                    Quaternion.Euler(0,18f+i*44f,0),i%2==0?lava:hotLava);
            }
        }

        void BuildNeptune()
        {
            Material abyss=MakeMaterial("DF_Neptune_Abyss",new Color(.006f,.018f,.065f,1f),.08f,.35f);
            Material coral=MakeMaterial("DF_Neptune_Coral",new Color(.025f,.10f,.34f,1f),.05f,.42f);
            Material bio=MakeMaterial("DF_Neptune_Biolume",new Color(.025f,.52f,1f,1f),0f,.62f,true,2.8f);
            Material pool=MakeMaterial("DF_Neptune_Pool",new Color(.01f,.12f,.39f,1f),.02f,.90f,true,.55f);

            Vector3[] pools={new(-28,0,18),new(28,0,-18),new(-16,0,-29),new(17,0,29)};
            for(int i=0;i<pools.Length;i++)
            {
                Vector3 p=Surface(pools[i].x,pools[i].z,.03f);
                Primitive(PrimitiveType.Cylinder,"AbyssPool",detailRoot,p,new Vector3(5.6f,.07f,3.4f),
                    Quaternion.Euler(0,i*36f,0),pool);
            }

            Vector3[] coralFields={
                new(-38,0,9),new(-34,0,-19),new(-24,0,31),new(25,0,-31),
                new(36,0,18),new(38,0,-8),new(-9,0,33),new(10,0,-33)
            };
            for(int i=0;i<coralFields.Length;i++)
            {
                Vector3 basePos=Surface(coralFields[i].x,coralFields[i].z,0f);
                Transform cluster=new GameObject("AbyssCoralCluster").transform;
                cluster.SetParent(detailRoot,false);
                cluster.position=basePos;
                for(int b=0;b<3;b++)
                {
                    float side=(b-1)*.65f;
                    float h=1.7f+b*.55f;
                    Primitive(PrimitiveType.Capsule,"CoralBranch",cluster,new Vector3(side,h*.55f,(b%2)*.35f),
                        new Vector3(.30f,h*.52f,.30f),Quaternion.Euler((b-1)*12f,20f+b*41f,(b-1)*14f),b==1?coral:abyss);
                    Primitive(PrimitiveType.Sphere,"BioTip",cluster,new Vector3(side,h+0.05f,(b%2)*.35f),
                        new Vector3(.25f,.25f,.25f),Quaternion.identity,bio);
                }
            }
        }

        void ApplyHotZoneCreatureVisual(ArenaVariant variant)
        {
            if(variant!=ArenaVariant.Pyraxis&&variant!=ArenaVariant.Neptune)return;
            HotZoneTank[] monsters=Object.FindObjectsByType<HotZoneTank>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<monsters.Length;i++)
            {
                HotZoneTank monster=monsters[i];
                if(monster==null)continue;
                if(variant==ArenaVariant.Pyraxis)
                {
                    if(monster.GetComponent<PyraxisScorpionVisualOverride>()==null)
                        monster.gameObject.AddComponent<PyraxisScorpionVisualOverride>();
                }
                else if(monster.GetComponent<NeptuneOctopusVisualOverride>()==null)
                {
                    monster.gameObject.AddComponent<NeptuneOctopusVisualOverride>();
                }
            }
        }

        Vector3 Surface(float x,float z,float extraY)
        {
            Vector3 p=LunarSurfaceField.Surface(x,z);
            p.y+=extraY;
            return p;
        }

        Material MakeMaterial(string name,Color color,float metallic,float smoothness,bool emission=false,float emissionPower=1f)
        {
            Material material=RuntimeShaderLibrary.Create(color,false,metallic,smoothness,
                emission?color*emissionPower:Color.black);
            if(material==null)return null;
            material.name=name;
            if(!emission)
            {
                SceneryFinish finish=name.Contains("Bark")?SceneryFinish.Bark:
                    name.Contains("Leaves")?SceneryFinish.Leaf:
                    ArenaVariantRuntime.IsNeptune?SceneryFinish.WetStone:SceneryFinish.Stone;
                PlanetSceneryMaterials.Apply(material,ArenaVariantRuntime.Current,finish);
            }
            ownedMaterials.Add(material);
            return material;
        }

        GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localRotation=rotation;
            go.transform.localScale=scale;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
            MeshRenderer renderer=go.GetComponent<MeshRenderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            clearance.RemoveIfBlocked(go);
            return go;
        }

        void ClearTheme()
        {
            if(detailRoot!=null)Object.Destroy(detailRoot.gameObject);
            detailRoot=null;
            ReleaseMaterials();
        }

        void ReleaseMaterials()
        {
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)Object.Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }

        void OnDestroy()=>ClearTheme();
    }

    internal static class HotZoneCreatureVisualFactory
    {
        public static void HideExistingMeshes(Transform root)
        {
            MeshRenderer[] renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=false;
        }

        public static Material Material(List<Material> owned,string name,Color color,float metallic,float smoothness,bool emission=false,float power=1f)
        {
            Material material=RuntimeShaderLibrary.Create(color,false,metallic,smoothness,
                emission?color*power:Color.black);
            if(material==null)return null;
            material.name=name;
            owned.Add(material);
            return material;
        }

        public static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Quaternion localRotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localRotation=localRotation;
            go.transform.localScale=localScale;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
            MeshRenderer renderer=go.GetComponent<MeshRenderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            return go;
        }

        public static void Release(List<Material> owned)
        {
            for(int i=0;i<owned.Count;i++)if(owned[i]!=null)Object.Destroy(owned[i]);
            owned.Clear();
        }
    }

    /// <summary>Visual body used by the Pyraxis Hot Zone monster. Gameplay remains HotZoneTank.</summary>
    public sealed class PyraxisScorpionVisualOverride : MonoBehaviour
    {
        readonly List<Material> ownedMaterials=new();
        Transform visualRoot,tailRoot,leftClaw,rightClaw;
        Vector3 rootBase;

        void Awake()
        {
            HotZoneCreatureVisualFactory.HideExistingMeshes(transform);
            Material shell=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_PyraxisScorpionShell",new Color(.018f,.012f,.010f,1f),.32f,.28f);
            Material armor=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_PyraxisScorpionArmor",new Color(.09f,.025f,.010f,1f),.28f,.32f);
            Material glow=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_PyraxisScorpionGlow",new Color(1f,.25f,.012f,1f),0f,.58f,true,4.2f);

            visualRoot=new GameObject("DF_PyraxisScorpionVisual").transform;
            visualRoot.SetParent(transform,false);
            visualRoot.localPosition=new Vector3(0,.04f,0);
            rootBase=visualRoot.localPosition;

            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"Abdomen",visualRoot,new Vector3(0,.68f,-.20f),new Vector3(1.35f,.55f,1.65f),Quaternion.identity,shell);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"Thorax",visualRoot,new Vector3(0,.72f,1.05f),new Vector3(1.12f,.50f,1.00f),Quaternion.identity,armor);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"Head",visualRoot,new Vector3(0,.70f,1.82f),new Vector3(.76f,.42f,.68f),Quaternion.identity,shell);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"EyeL",visualRoot,new Vector3(-.28f,.84f,2.17f),new Vector3(.12f,.12f,.12f),Quaternion.identity,glow);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"EyeR",visualRoot,new Vector3(.28f,.84f,2.17f),new Vector3(.12f,.12f,.12f),Quaternion.identity,glow);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"BellyHeat",visualRoot,new Vector3(0,.39f,.05f),new Vector3(.64f,.08f,1.06f),Quaternion.identity,glow);

            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                for(int leg=0;leg<4;leg++)
                {
                    float z=1.18f-leg*.72f;
                    Transform joint=new GameObject("LegJoint").transform;
                    joint.SetParent(visualRoot,false);
                    joint.localPosition=new Vector3(side*.90f,.58f,z);
                    joint.localRotation=Quaternion.Euler(0,side*(12f+leg*8f),side*4f);
                    HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"UpperLeg",joint,new Vector3(side*.62f,-.03f,0),new Vector3(1.16f,.13f,.15f),Quaternion.Euler(0,0,side*13f),armor);
                    HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"LowerLeg",joint,new Vector3(side*1.25f,-.27f,.04f),new Vector3(.82f,.11f,.13f),Quaternion.Euler(0,0,side*31f),shell);
                }
            }

            leftClaw=BuildClaw(-1f,shell,armor,glow);
            rightClaw=BuildClaw(1f,shell,armor,glow);

            tailRoot=new GameObject("Tail").transform;
            tailRoot.SetParent(visualRoot,false);
            tailRoot.localPosition=new Vector3(0,.82f,-1.45f);
            for(int i=0;i<7;i++)
            {
                float t=i/6f;
                Vector3 p=new(0,.18f+i*.30f,-i*.20f+i*i*.025f);
                float s=Mathf.Lerp(.46f,.24f,t);
                HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"TailSegment",tailRoot,p,new Vector3(s,s*.78f,s*1.10f),Quaternion.identity,i<5?armor:shell);
            }
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"Stinger",tailRoot,new Vector3(0,2.18f,-.23f),new Vector3(.20f,.38f,.20f),Quaternion.Euler(18f,0,0),glow);
        }

        Transform BuildClaw(float side,Material shell,Material armor,Material glow)
        {
            Transform root=new GameObject(side<0?"LeftClaw":"RightClaw").transform;
            root.SetParent(visualRoot,false);
            root.localPosition=new Vector3(side*.72f,.72f,1.78f);
            root.localRotation=Quaternion.Euler(0,side*18f,0);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"ClawArm",root,new Vector3(side*.55f,0,.42f),new Vector3(.92f,.18f,.18f),Quaternion.Euler(0,side*25f,0),armor);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"PincerPalm",root,new Vector3(side*1.02f,.02f,.78f),new Vector3(.52f,.25f,.50f),Quaternion.identity,shell);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"PincerTop",root,new Vector3(side*1.30f,.15f,1.05f),new Vector3(.48f,.10f,.15f),Quaternion.Euler(0,side*22f,side*18f),shell);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"PincerBottom",root,new Vector3(side*1.30f,-.12f,1.05f),new Vector3(.48f,.10f,.15f),Quaternion.Euler(0,side*22f,-side*18f),glow);
            return root;
        }

        void Update()
        {
            if(visualRoot==null)return;
            float t=Time.time;
            visualRoot.localPosition=rootBase+Vector3.up*(Mathf.Sin(t*3.2f)*.045f);
            if(tailRoot!=null)tailRoot.localRotation=Quaternion.Euler(6f+Mathf.Sin(t*2.2f)*5f,Mathf.Sin(t*1.6f)*8f,0);
            if(leftClaw!=null)leftClaw.localRotation=Quaternion.Euler(0,-18f-Mathf.Sin(t*3.0f)*5f,Mathf.Sin(t*2.4f)*4f);
            if(rightClaw!=null)rightClaw.localRotation=Quaternion.Euler(0,18f+Mathf.Sin(t*3.0f)*5f,-Mathf.Sin(t*2.4f)*4f);
        }

        void OnDestroy()=>HotZoneCreatureVisualFactory.Release(ownedMaterials);
    }

    /// <summary>Visual body used by the Neptune Hot Zone monster. Gameplay remains HotZoneTank.</summary>
    public sealed class NeptuneOctopusVisualOverride : MonoBehaviour
    {
        readonly List<Material> ownedMaterials=new();
        readonly List<Transform> tentacles=new();
        readonly List<float> tentacleBaseYaw=new();
        Transform visualRoot;
        Vector3 rootBase;

        void Awake()
        {
            HotZoneCreatureVisualFactory.HideExistingMeshes(transform);
            Material skin=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_NeptuneOctopusSkin",new Color(.006f,.018f,.085f,1f),.05f,.48f);
            Material skinBlue=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_NeptuneOctopusBlue",new Color(.015f,.075f,.30f,1f),.08f,.55f);
            Material glow=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_NeptuneOctopusGlow",new Color(.025f,.65f,1f,1f),0f,.72f,true,3.5f);
            Material pupil=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_NeptuneOctopusPupil",new Color(.001f,.003f,.009f,1f),.12f,.75f);

            visualRoot=new GameObject("DF_NeptuneOctopusVisual").transform;
            visualRoot.SetParent(transform,false);
            visualRoot.localPosition=new Vector3(0,.08f,0);
            rootBase=visualRoot.localPosition;

            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"Mantle",visualRoot,new Vector3(0,1.45f,-.15f),new Vector3(1.30f,1.45f,1.20f),Quaternion.identity,skin);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"Head",visualRoot,new Vector3(0,.68f,.32f),new Vector3(1.45f,.82f,1.32f),Quaternion.identity,skinBlue);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"SingleEye",visualRoot,new Vector3(0,.92f,1.38f),new Vector3(.50f,.42f,.18f),Quaternion.identity,glow);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"EyePupil",visualRoot,new Vector3(0,.92f,1.52f),new Vector3(.19f,.22f,.08f),Quaternion.identity,pupil);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"CrownFin",visualRoot,new Vector3(0,2.65f,-.28f),new Vector3(.20f,.55f,.16f),Quaternion.Euler(0,0,15f),glow);

            for(int i=0;i<8;i++)
            {
                float yaw=i*45f;
                Transform tentacle=new GameObject("Tentacle_"+i).transform;
                tentacle.SetParent(visualRoot,false);
                tentacle.localPosition=new Vector3(0,.30f,0);
                tentacle.localRotation=Quaternion.Euler(0,yaw,0);
                tentacles.Add(tentacle);
                tentacleBaseYaw.Add(yaw);

                for(int s=0;s<5;s++)
                {
                    float t=s/4f;
                    float z=.52f+s*.55f;
                    float y=-.04f-s*.07f+Mathf.Sin((i+s)*1.7f)*.025f;
                    float width=Mathf.Lerp(.34f,.14f,t);
                    HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"TentacleSegment",tentacle,new Vector3(0,y,z),
                        new Vector3(width,.23f+width*.12f,.46f),Quaternion.Euler(0,0,(s-2)*2f),s%2==0?skinBlue:skin);
                    if(s==1||s==3)
                        HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"BioSucker",tentacle,new Vector3(0,-.17f,z+.08f),
                            new Vector3(.11f,.055f,.14f),Quaternion.identity,glow);
                }
            }
            AuthoredHotZoneArt.Apply(visualRoot,"Xeno");
        }

        void Update()
        {
            if(visualRoot==null)return;
            float t=Time.time;
            visualRoot.localPosition=rootBase+Vector3.up*(Mathf.Sin(t*2.0f)*.07f);
            visualRoot.localRotation=Quaternion.Euler(0,Mathf.Sin(t*.75f)*4f,Mathf.Sin(t*1.15f)*1.5f);
            for(int i=0;i<tentacles.Count;i++)
            {
                Transform tentacle=tentacles[i];
                if(tentacle==null)continue;
                float phase=t*1.7f+i*.73f;
                tentacle.localRotation=Quaternion.Euler(Mathf.Sin(phase*.73f)*5f,tentacleBaseYaw[i]+Mathf.Sin(phase)*9f,Mathf.Cos(phase*.81f)*5f);
            }
        }

        void OnDestroy()=>HotZoneCreatureVisualFactory.Release(ownedMaterials);
    }
}




