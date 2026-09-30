using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Adds lightweight visual-only detail to the Neptune Hot Zone octopus.
    /// Gameplay, colliders, combat behaviour and the corrected eye positions remain unchanged.
    /// </summary>
    public sealed class NeptuneOctopusDetailPolish : MonoBehaviour
    {
        const string VisualRootName="DF_NeptuneOctopusVisual";
        const string DetailRootName="DF_NeptuneOctopusDetails";

        Transform detailRoot;
        Material cyanGlow,violetGlow,deepAccent,suckerMaterial;
        float nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_NeptuneOctopusDetailPolishManager")!=null)return;
            GameObject go=new("DF_NeptuneOctopusDetailPolishManager");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<NeptuneOctopusDetailPolishManager>();
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

            cyanGlow=MakeMaterial("DF_NeptuneOctopusDetailCyan",new Color(.015f,.72f,1f,1f),true,2.6f,.68f);
            violetGlow=MakeMaterial("DF_NeptuneOctopusDetailViolet",new Color(.33f,.08f,.72f,1f),true,1.9f,.58f);
            deepAccent=MakeMaterial("DF_NeptuneOctopusDeepAccent",new Color(.018f,.008f,.075f,1f),false,1f,.42f);
            suckerMaterial=MakeMaterial("DF_NeptuneOctopusSucker",new Color(.06f,.18f,.28f,1f),true,.75f,.52f);

            detailRoot=new GameObject(DetailRootName).transform;
            detailRoot.SetParent(visual,false);
            detailRoot.localPosition=Vector3.zero;
            detailRoot.localRotation=Quaternion.identity;

            BuildEyeSockets();
            BuildMantleRidges();
            BuildBioluminescentSpots();
            BuildSideFins();
            BuildMouthDetail();
            BuildTentacleDetails(visual);
        }

        void BuildEyeSockets()
        {
            Part(PrimitiveType.Sphere,"EyeSocketL",detailRoot,new Vector3(-.48f,.98f,.45f),new Vector3(.62f,.54f,.12f),Quaternion.identity,deepAccent);
            Part(PrimitiveType.Sphere,"EyeSocketR",detailRoot,new Vector3(.48f,.98f,.45f),new Vector3(.62f,.54f,.12f),Quaternion.identity,deepAccent);
        }

        void BuildMantleRidges()
        {
            // Keep one subtle cyan centre ridge; the former violet head stripes are intentionally removed.
            Part(PrimitiveType.Capsule,"MantleRidgeCenter",detailRoot,new Vector3(0,1.78f,.38f),
                new Vector3(.075f,.46f,.07f),Quaternion.Euler(8f,0,0),cyanGlow);
        }

        void BuildBioluminescentSpots()
        {
            Vector3[] spots={
                new(-.88f,1.95f,.18f),new(-.62f,1.48f,.46f),new(-.86f,1.12f,.37f),
                new(.88f,1.95f,.18f),new(.62f,1.48f,.46f),new(.86f,1.12f,.37f),
                new(-.36f,2.30f,.02f),new(.36f,2.30f,.02f),new(0,2.08f,.43f),
                new(-1.02f,.78f,.24f),new(1.02f,.78f,.24f),new(0,.62f,.74f)
            };

            for(int i=0;i<spots.Length;i++)
            {
                float size=.075f+(i%3)*.018f;
                Part(PrimitiveType.Sphere,"BioSpot",detailRoot,spots[i],new Vector3(size,size,size*.55f),Quaternion.identity,i%3==0?violetGlow:cyanGlow);
            }
        }

        void BuildSideFins()
        {
            Part(PrimitiveType.Sphere,"SideFinL",detailRoot,new Vector3(-1.06f,1.45f,-.08f),new Vector3(.58f,.26f,.18f),Quaternion.Euler(0,-8f,-24f),deepAccent);
            Part(PrimitiveType.Sphere,"SideFinR",detailRoot,new Vector3(1.06f,1.45f,-.08f),new Vector3(.58f,.26f,.18f),Quaternion.Euler(0,8f,24f),deepAccent);
            Part(PrimitiveType.Sphere,"SideFinGlowL",detailRoot,new Vector3(-1.12f,1.48f,-.01f),new Vector3(.31f,.07f,.05f),Quaternion.Euler(0,-8f,-24f),cyanGlow);
            Part(PrimitiveType.Sphere,"SideFinGlowR",detailRoot,new Vector3(1.12f,1.48f,-.01f),new Vector3(.31f,.07f,.05f),Quaternion.Euler(0,8f,24f),cyanGlow);
        }

        void BuildMouthDetail()
        {
            Part(PrimitiveType.Sphere,"BeakShadow",detailRoot,new Vector3(0,.43f,.82f),new Vector3(.28f,.16f,.12f),Quaternion.identity,deepAccent);
            Part(PrimitiveType.Sphere,"MouthGlow",detailRoot,new Vector3(0,.38f,.88f),new Vector3(.14f,.035f,.04f),Quaternion.identity,violetGlow);
        }

        void BuildTentacleDetails(Transform visual)
        {
            for(int i=0;i<8;i++)
            {
                Transform tentacle=visual.Find("Tentacle_"+i);
                if(tentacle==null)continue;

                for(int s=0;s<5;s+=2)
                {
                    float z=.52f+s*.55f;
                    float y=-.18f-s*.07f;
                    float side=((i+s)&1)==0?-.09f:.09f;
                    Part(PrimitiveType.Sphere,"SuckerDetail",tentacle,new Vector3(side,y,z+.10f),
                        new Vector3(.075f,.038f,.095f),Quaternion.identity,suckerMaterial);
                }

                Part(PrimitiveType.Sphere,"TentacleTipGlow",tentacle,new Vector3(0,-.36f,2.96f),
                    new Vector3(.10f,.07f,.15f),Quaternion.identity,i%2==0?cyanGlow:violetGlow);
            }
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

        static Material MakeMaterial(string name,Color color,bool emission,float emissionPower,float smoothness)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material material=new(shader){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(emission)
            {
                material.EnableKeyword("_EMISSION");
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*emissionPower);
            }
            return material;
        }

        void OnDestroy()
        {
            if(cyanGlow!=null)Object.Destroy(cyanGlow);
            if(violetGlow!=null)Object.Destroy(violetGlow);
            if(deepAccent!=null)Object.Destroy(deepAccent);
            if(suckerMaterial!=null)Object.Destroy(suckerMaterial);
        }
    }

    internal sealed class NeptuneOctopusDetailPolishManager : MonoBehaviour
    {
        float nextScan;

        void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.30f;

            NeptuneOctopusVisualOverride[] octopuses=Object.FindObjectsByType<NeptuneOctopusVisualOverride>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<octopuses.Length;i++)
            {
                NeptuneOctopusVisualOverride octopus=octopuses[i];
                if(octopus==null)continue;
                if(octopus.GetComponent<NeptuneOctopusDetailPolish>()==null)
                    octopus.gameObject.AddComponent<NeptuneOctopusDetailPolish>();
            }
        }
    }
}
