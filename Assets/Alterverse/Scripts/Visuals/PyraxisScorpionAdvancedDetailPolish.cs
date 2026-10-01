using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Adds an additional lightweight visual detail layer to the Pyraxis Hot Zone scorpion.
    /// Visual-only: AI, attacks, hitboxes and movement stay untouched.
    /// </summary>
    [DefaultExecutionOrder(1330)]
    public sealed class PyraxisScorpionAdvancedDetailPolish : MonoBehaviour
    {
        const string VisualRootName="DF_PyraxisScorpionVisual";
        const string DetailRootName="DF_PyraxisScorpionAdvancedDetails";

        Transform detailRoot;
        Material hot,ember,obsidian,armor;
        float nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_PyraxisScorpionAdvancedDetailPolishManager")!=null)return;
            GameObject manager=new("DF_PyraxisScorpionAdvancedDetailPolishManager");
            Object.DontDestroyOnLoad(manager);
            manager.AddComponent<PyraxisScorpionAdvancedDetailPolishManager>();
        }

        void Update()
        {
            if(detailRoot!=null)return;
            if(Time.unscaledTime<nextResolve)return;
            nextResolve=Time.unscaledTime+.20f;

            Transform visual=transform.Find(VisualRootName);
            if(visual==null)return;
            BuildDetails(visual);
        }

        void BuildDetails(Transform visual)
        {
            Transform existing=visual.Find(DetailRootName);
            if(existing!=null){detailRoot=existing;return;}

            hot=MakeMaterial("DF_PyraxisScorpionAdvancedHot",new Color(1f,.34f,.012f,1f),true,4.2f,.58f,.02f);
            ember=MakeMaterial("DF_PyraxisScorpionAdvancedEmber",new Color(.82f,.055f,.004f,1f),true,2.4f,.45f,.02f);
            obsidian=MakeMaterial("DF_PyraxisScorpionAdvancedObsidian",new Color(.012f,.007f,.006f,1f),false,1f,.28f,.52f);
            armor=MakeMaterial("DF_PyraxisScorpionAdvancedArmor",new Color(.16f,.034f,.009f,1f),false,1f,.52f,.46f);

            detailRoot=new GameObject(DetailRootName).transform;
            detailRoot.SetParent(visual,false);
            detailRoot.localPosition=Vector3.zero;
            detailRoot.localRotation=Quaternion.identity;

            BuildSideCarapace();
            BuildUnderbodyVents();
            BuildHeadCrown();
            BuildLegArmor(visual);
            BuildClawBlades(visual.Find("LeftClaw"),-1f);
            BuildClawBlades(visual.Find("RightClaw"),1f);
            BuildTailSpines(visual.Find("Tail"));
        }

        void BuildSideCarapace()
        {
            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                for(int i=0;i<4;i++)
                {
                    float z=.55f-i*.52f;
                    float y=.82f-i*.025f;
                    Part(PrimitiveType.Sphere,"SidePlate",detailRoot,new Vector3(side*(1.04f-i*.035f),y,z),
                        new Vector3(.34f,.16f,.38f),Quaternion.Euler(0,side*(18f+i*4f),side*8f),i%2==0?armor:obsidian);
                    Part(PrimitiveType.Cube,"CarapaceRim",detailRoot,new Vector3(side*(1.04f-i*.035f),y+.09f,z),
                        new Vector3(.27f,.028f,.30f),Quaternion.Euler(0,side*(18f+i*4f),side*8f),armor);
                    for(int cut=0;cut<3;cut++)
                        Part(PrimitiveType.Cube,"CarapaceScoring",detailRoot,
                            new Vector3(side*(1.04f-i*.035f),y+.11f,z-.08f+cut*.07f),
                            new Vector3(.17f,.012f,.015f),Quaternion.Euler(0,side*(18f+i*4f),0),obsidian);
                    Part(PrimitiveType.Cube,"SideHeatSlit",detailRoot,new Vector3(side*(1.13f-i*.03f),y+.015f,z+.05f),
                        new Vector3(.035f,.035f,.22f),Quaternion.Euler(0,side*(18f+i*4f),0),i%2==0?hot:ember);
                }
            }
        }

        void BuildUnderbodyVents()
        {
            for(int i=0;i<5;i++)
            {
                float z=-.82f+i*.40f;
                float width=.54f-i*.035f;
                Part(PrimitiveType.Cube,"BellyVent",detailRoot,new Vector3(0,.31f,z),
                    new Vector3(width,.035f,.07f),Quaternion.identity,i==2?hot:ember);
            }

            Part(PrimitiveType.Sphere,"CoreGlow",detailRoot,new Vector3(0,.34f,.30f),
                new Vector3(.24f,.055f,.34f),Quaternion.identity,hot);
        }

        void BuildHeadCrown()
        {
            Part(PrimitiveType.Capsule,"CrownSpikeCenter",detailRoot,new Vector3(0,1.13f,1.67f),
                new Vector3(.085f,.32f,.085f),Quaternion.Euler(34f,0,0),obsidian);
            Part(PrimitiveType.Capsule,"CrownSpikeL",detailRoot,new Vector3(-.32f,1.08f,1.70f),
                new Vector3(.07f,.25f,.07f),Quaternion.Euler(38f,0,-26f),armor);
            Part(PrimitiveType.Capsule,"CrownSpikeR",detailRoot,new Vector3(.32f,1.08f,1.70f),
                new Vector3(.07f,.25f,.07f),Quaternion.Euler(38f,0,26f),armor);

            Part(PrimitiveType.Cube,"ForeheadHeatMark",detailRoot,new Vector3(0,.96f,2.05f),
                new Vector3(.10f,.035f,.28f),Quaternion.Euler(-8f,0,0),hot);
        }

        void BuildLegArmor(Transform visual)
        {
            int index=0;
            for(int i=0;i<visual.childCount;i++)
            {
                Transform leg=visual.GetChild(i);
                if(leg==null||leg.name!="LegJoint")continue;
                float side=leg.localPosition.x<0f?-1f:1f;

                Part(PrimitiveType.Cube,"LegArmor",leg,new Vector3(side*.72f,.07f,0),
                    new Vector3(.38f,.18f,.20f),Quaternion.Euler(0,0,side*12f),index%2==0?armor:obsidian);
                Part(PrimitiveType.Sphere,"LegHeatNode",leg,new Vector3(side*1.26f,-.23f,.04f),
                    new Vector3(.095f,.075f,.095f),Quaternion.identity,index%3==0?hot:ember);
                Part(PrimitiveType.Capsule,"LegBlade",leg,new Vector3(side*1.55f,-.10f,.03f),
                    new Vector3(.045f,.16f,.045f),Quaternion.Euler(0,0,side*62f),obsidian);
                index++;
            }
        }

        void BuildClawBlades(Transform claw,float side)
        {
            if(claw==null)return;

            Part(PrimitiveType.Cube,"ClawEdgeTop",claw,new Vector3(side*1.36f,.19f,1.08f),
                new Vector3(.34f,.035f,.055f),Quaternion.Euler(0,side*22f,side*18f),hot);
            Part(PrimitiveType.Cube,"ClawEdgeBottom",claw,new Vector3(side*1.36f,-.15f,1.08f),
                new Vector3(.34f,.035f,.055f),Quaternion.Euler(0,side*22f,-side*18f),ember);

            for(int i=0;i<2;i++)
            {
                float z=.97f+i*.14f;
                Part(PrimitiveType.Capsule,"ClawSerration",claw,new Vector3(side*(1.50f+i*.05f),.03f,z),
                    new Vector3(.04f,.10f,.04f),Quaternion.Euler(72f,0,side*(22f-i*6f)),obsidian);
            }

            Part(PrimitiveType.Sphere,"ClawCore",claw,new Vector3(side*1.01f,.02f,.78f),
                new Vector3(.15f,.09f,.15f),Quaternion.identity,hot);
        }

        void BuildTailSpines(Transform tail)
        {
            if(tail==null)return;

            for(int i=1;i<6;i++)
            {
                float t=i/6f;
                Vector3 p=new(0,.18f+i*.30f,-i*.20f+i*i*.025f);
                float side=i%2==0?-1f:1f;
                float scale=Mathf.Lerp(.10f,.065f,t);

                Part(PrimitiveType.Capsule,"TailSideSpine",tail,p+new Vector3(side*.22f,.08f,0),
                    new Vector3(scale,.22f,scale),Quaternion.Euler(0,0,side*-58f),obsidian);
                Part(PrimitiveType.Sphere,"TailHeatNode",tail,p+new Vector3(-side*.16f,.02f,.07f),
                    new Vector3(.08f,.045f,.09f),Quaternion.identity,i%2==0?hot:ember);
            }

            Part(PrimitiveType.Capsule,"StingerSideBarbL",tail,new Vector3(-.13f,2.29f,-.28f),
                new Vector3(.05f,.18f,.05f),Quaternion.Euler(48f,0,-42f),obsidian);
            Part(PrimitiveType.Capsule,"StingerSideBarbR",tail,new Vector3(.13f,2.29f,-.28f),
                new Vector3(.05f,.18f,.05f),Quaternion.Euler(48f,0,42f),obsidian);
            Part(PrimitiveType.Sphere,"StingerHeatCore",tail,new Vector3(0,2.18f,-.21f),
                new Vector3(.095f,.12f,.095f),Quaternion.identity,hot);
        }

        static GameObject Part(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Quaternion localRotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            go.transform.localRotation=localRotation;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null)Object.Destroy(collider);
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            return go;
        }

        static Material MakeMaterial(string name,Color color,bool emission,float emissionPower,float smoothness,float metallic)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material material=new(shader){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(emission)
            {
                material.EnableKeyword("_EMISSION");
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*emissionPower);
            }
            return material;
        }

        void OnDestroy()
        {
            if(hot!=null)Object.Destroy(hot);
            if(ember!=null)Object.Destroy(ember);
            if(obsidian!=null)Object.Destroy(obsidian);
            if(armor!=null)Object.Destroy(armor);
        }
    }

    internal sealed class PyraxisScorpionAdvancedDetailPolishManager : MonoBehaviour
    {
        float nextScan;

        void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.30f;
            if(!Alterverse.Match.ArenaVariantRuntime.IsPyraxis)return;

            PyraxisScorpionVisualOverride[] scorpions=Object.FindObjectsByType<PyraxisScorpionVisualOverride>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<scorpions.Length;i++)
            {
                PyraxisScorpionVisualOverride scorpion=scorpions[i];
                if(scorpion==null)continue;
                if(scorpion.GetComponent<PyraxisScorpionAdvancedDetailPolish>()==null)
                    scorpion.gameObject.AddComponent<PyraxisScorpionAdvancedDetailPolish>();
            }
        }
    }
}
