using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Pyraxis-specific presentation pass. Keeps the base/spawn structures free of molten markings,
    /// concentrates true lava pools into the middle 50 percent of the arena, preserves the intended
    /// orange/black mottled volcanic ground, and presents both healing guards as lava golems derived
    /// from the existing Mars stone-golem visual. Visual only: no gameplay colliders, health, AI,
    /// damage or pathing are changed.
    /// </summary>
    public sealed class PyraxisArenaCorrections : MonoBehaviour
    {
        private readonly List<Material> ownedMaterials=new();
        private readonly HashSet<HealingGuardMech> convertedGuards=new();
        private Transform appliedTheme;
        private Transform poolRoot;
        private Texture2D groundTexture;
        private Material groundMaterial;
        private float nextScan;

        private static readonly Vector2[] PoolCenters={
            new(-17f,26f),new(17f,-26f),
            new(17f,19f),new(-17f,-19f),
            new(-10f,12f),new(10f,-12f),
            new(7f,27f),new(-7f,-27f),
            new(-5f,-6f),new(5f,6f)
        };

        private static readonly Vector2[] PoolRadii={
            new(4.6f,2.8f),new(4.6f,2.8f),
            new(3.9f,2.5f),new(3.9f,2.5f),
            new(3.4f,2.2f),new(3.4f,2.2f),
            new(3.1f,2.0f),new(3.1f,2.0f),
            new(2.8f,1.8f),new(2.8f,1.8f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(GameObject.Find("DF_PyraxisArenaCorrections")!=null)return;
            GameObject go=new("DF_PyraxisArenaCorrections");
            DontDestroyOnLoad(go);
            go.AddComponent<PyraxisArenaCorrections>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.25f;

            if(!ArenaVariantRuntime.IsPyraxis)
            {
                if(appliedTheme!=null||groundTexture!=null||convertedGuards.Count>0)ResetRuntimeState();
                return;
            }

            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null)return;

            GameObject themeObject=GameObject.Find("DF_PlanetTheme_Pyraxis");
            if(themeObject!=null&&appliedTheme!=themeObject.transform)
            {
                ResetThemeState();
                appliedTheme=themeObject.transform;
                RemoveOldSpawnLava(appliedTheme);
                RemoveLavaFromBaseStructures(world.transform);
                BuildCentralLavaPools(appliedTheme);
            }

            if(groundTexture==null)ApplyCentralLavaGround(world.transform);
            ConvertHealingGuards(world.transform);
        }

        private void RemoveOldSpawnLava(Transform theme)
        {
            if(theme==null)return;
            Transform[] all=theme.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(t==null||t==theme)continue;
                if(t.name=="LavaVent"||t.name=="LavaStream")Destroy(t.gameObject);
            }
        }

        private static void RemoveLavaFromBaseStructures(Transform world)
        {
            if(world==null)return;
            Transform[] all=world.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(t==null)continue;
                if(t.name.IndexOf("Lava",System.StringComparison.OrdinalIgnoreCase)<0)continue;
                if(t.name.StartsWith("DF_PyraxisCentralLava",System.StringComparison.Ordinal))continue;

                Vector3 p=t.position;
                bool zeroBase=new Vector2(p.x+36f,p.z).sqrMagnitude<8.0f*8.0f;
                bool lumenBase=new Vector2(p.x-36f,p.z).sqrMagnitude<8.0f*8.0f;
                bool zeroSpawn=new Vector2(p.x+27.5f,p.z).sqrMagnitude<6.0f*6.0f;
                bool lumenSpawn=new Vector2(p.x-27.5f,p.z).sqrMagnitude<6.0f*6.0f;
                if(zeroBase||lumenBase||zeroSpawn||lumenSpawn)Destroy(t.gameObject);
            }
        }

        private void BuildCentralLavaPools(Transform theme)
        {
            if(theme==null)return;
            GameObject root=new("DF_PyraxisCentralLavaPools");
            poolRoot=root.transform;
            poolRoot.SetParent(theme,false);

            Material rim=MakeMaterial("DF_PyraxisPoolBasalt",new Color(.022f,.014f,.012f,1f),.08f,.12f,false,1f);
            Material lava=MakeMaterial("DF_PyraxisPoolLava",new Color(1f,.18f,.008f,1f),0f,.52f,true,3.8f);
            Material hot=MakeMaterial("DF_PyraxisPoolHot",new Color(1f,.56f,.035f,1f),0f,.62f,true,4.8f);

            for(int i=0;i<PoolCenters.Length;i++)
            {
                Vector2 center=PoolCenters[i];
                Vector2 radii=PoolRadii[i];
                Vector3 p=LunarSurfaceField.Surface(center.x,center.y);
                p.y+=.035f;
                float rotation=(i*37f)%180f;

                Primitive(PrimitiveType.Cylinder,"CentralLavaPoolRim",poolRoot,p,
                    new Vector3(radii.x+.58f,.025f,radii.y+.48f),Quaternion.Euler(0,rotation,0),rim);
                Primitive(PrimitiveType.Cylinder,"CentralLavaPool",poolRoot,p+Vector3.up*.018f,
                    new Vector3(radii.x,.022f,radii.y),Quaternion.Euler(0,rotation,0),lava);

                float side=i%2==0?1f:-1f;
                Vector3 lobe=LunarSurfaceField.Surface(center.x+side*radii.x*.42f,center.y-radii.y*.16f);
                lobe.y+=.057f;
                Primitive(PrimitiveType.Cylinder,"CentralLavaPoolLobe",poolRoot,lobe,
                    new Vector3(radii.x*.50f,.018f,radii.y*.58f),Quaternion.Euler(0,rotation+28f,0),lava);

                Vector3 core=LunarSurfaceField.Surface(center.x-side*radii.x*.18f,center.y+radii.y*.10f);
                core.y+=.060f;
                Primitive(PrimitiveType.Cylinder,"CentralLavaPoolHotCore",poolRoot,core,
                    new Vector3(radii.x*.36f,.014f,radii.y*.30f),Quaternion.Euler(0,rotation-18f,0),hot);
            }
        }

        private void ApplyCentralLavaGround(Transform world)
        {
            MeshRenderer target=null;
            MeshRenderer[] renderers=world.GetComponentsInChildren<MeshRenderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                MeshRenderer candidate=renderers[i];
                if(candidate!=null&&candidate.gameObject.name=="DF_LunarRegolith")
                {
                    target=candidate;
                    break;
                }
            }
            if(target==null)return;

            groundTexture=BuildGroundTexture(1024,896);
            Material source=target.sharedMaterial;
            if(source==null)return;
            groundMaterial=new Material(source){name="DF_Pyraxis_OrangeBlackGround"};
            if(groundMaterial.HasProperty("_BaseMap"))groundMaterial.SetTexture("_BaseMap",groundTexture);
            if(groundMaterial.HasProperty("_MainTex"))groundMaterial.SetTexture("_MainTex",groundTexture);
            if(groundMaterial.HasProperty("_BaseColor"))groundMaterial.SetColor("_BaseColor",Color.white);
            if(groundMaterial.HasProperty("_Color"))groundMaterial.SetColor("_Color",Color.white);
            target.sharedMaterial=groundMaterial;
        }

        private static Texture2D BuildGroundTexture(int width,int height)
        {
            Texture2D texture=new(width,height,TextureFormat.RGBA32,true,false)
            {
                name="DF_PyraxisOrangeBlackMottledAlbedo",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Trilinear,
                anisoLevel=8
            };
            Color32[] pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            {
                float wz=Mathf.Lerp(-42f,42f,y/(float)(height-1));
                for(int x=0;x<width;x++)
                {
                    float wx=Mathf.Lerp(-48f,48f,x/(float)(width-1));

                    // Large low-frequency islands produce the requested orange/black cow-like volcanic pattern.
                    // A second warped field prevents the spots from reading as round procedural dots.
                    float macro=Mathf.PerlinNoise(wx*.036f+31f,wz*.041f+73f);
                    float warp=Mathf.PerlinNoise(wx*.082f+9f,wz*.076f+47f);
                    float cowField=macro*.76f+warp*.24f;
                    float orangeSpot=Mathf.SmoothStep(.47f,.59f,cowField);
                    float edgeBreak=Mathf.PerlinNoise(wx*.15f+91f,wz*.14f+17f);
                    orangeSpot=Mathf.Clamp01(orangeSpot+(edgeBreak-.5f)*.15f);

                    float rockDetail=Mathf.PerlinNoise(wx*.24f+11f,wz*.24f+53f);
                    float darkValue=.74f+(rockDetail-.5f)*.15f;
                    Color darkBasalt=new(.030f*darkValue,.017f*darkValue,.013f*darkValue,1f);
                    Color orangeBasalt=Color.Lerp(
                        new Color(.68f,.105f,.006f,1f),
                        new Color(1f,.285f,.012f,1f),
                        Mathf.Clamp01(.28f+rockDetail*.58f));

                    // Keep the actual base decks, reactors and healing plates visually clean. The orange volcanic
                    // pattern fades away before reaching them so no ground patch reads as lava painted on a base part.
                    float zeroBase=1f-Mathf.SmoothStep(5.9f,8.0f,Vector2.Distance(new Vector2(wx,wz),new Vector2(-36f,0f)));
                    float lumenBase=1f-Mathf.SmoothStep(5.9f,8.0f,Vector2.Distance(new Vector2(wx,wz),new Vector2(36f,0f)));
                    float zeroSpawn=1f-Mathf.SmoothStep(4.2f,6.0f,Vector2.Distance(new Vector2(wx,wz),new Vector2(-27.5f,0f)));
                    float lumenSpawn=1f-Mathf.SmoothStep(4.2f,6.0f,Vector2.Distance(new Vector2(wx,wz),new Vector2(27.5f,0f)));
                    float baseProtection=Mathf.Clamp01(Mathf.Max(Mathf.Max(zeroBase,lumenBase),Mathf.Max(zeroSpawn,lumenSpawn)));
                    orangeSpot*=1f-baseProtection;

                    Color ground=Color.Lerp(darkBasalt,orangeBasalt,orangeSpot*.96f);

                    // True molten areas stay limited to the middle half of the arena. These texture masks sit
                    // below the raised visual lava pools so the pools blend naturally into the orange/black ground.
                    float lavaMask=0f;
                    if(Mathf.Abs(wx)<=21f)
                    {
                        for(int p=0;p<PoolCenters.Length;p++)
                        {
                            Vector2 c=PoolCenters[p];
                            Vector2 r=PoolRadii[p];
                            float dx=(wx-c.x)/r.x;
                            float dz=(wz-c.y)/r.y;
                            float wobble=(Mathf.PerlinNoise(wx*.18f+17f+p*3.1f,wz*.18f+43f+p*2.3f)-.5f)*.30f;
                            float d=Mathf.Sqrt(dx*dx+dz*dz)+wobble;
                            lavaMask=Mathf.Max(lavaMask,1f-Mathf.SmoothStep(.72f,1.06f,d));
                        }
                        float centralBand=1f-Mathf.SmoothStep(18.5f,21f,Mathf.Abs(wx));
                        lavaMask*=centralBand;
                    }
                    lavaMask*=1f-baseProtection;

                    float hot=Mathf.SmoothStep(.38f,.92f,lavaMask);
                    Color molten=Color.Lerp(new Color(.90f,.055f,.002f,1f),new Color(1f,.62f,.028f,1f),hot);
                    pixels[y*width+x]=Color.Lerp(ground,molten,Mathf.Clamp01(lavaMask*.98f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true,true);
            return texture;
        }

        private void ConvertHealingGuards(Transform world)
        {
            HealingGuardMech[] guards=world.GetComponentsInChildren<HealingGuardMech>(true);
            for(int i=0;i<guards.Length;i++)
            {
                HealingGuardMech guard=guards[i];
                if(guard==null||convertedGuards.Contains(guard))continue;

                // Hide the ordinary lunar guard shell only. The gameplay collider and all guard logic remain.
                MeshRenderer[] existing=guard.GetComponentsInChildren<MeshRenderer>(true);
                for(int r=0;r<existing.Length;r++)if(existing[r]!=null)existing[r].enabled=false;

                int team=guard.gameObject.name.Contains("Zero")?0:1;
                MarsStoneGolemMechVisual.Build(guard.transform,team,true);
                guard.gameObject.AddComponent<PyraxisLavaGolemMotion>();
                convertedGuards.Add(guard);
            }
        }

        private Material MakeMaterial(string name,Color color,float metallic,float smoothness,bool emission,float emissionPower)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return null;
            Material material=new(shader){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(emission)
            {
                material.EnableKeyword("_EMISSION");
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*emissionPower);
            }
            ownedMaterials.Add(material);
            return material;
        }

        private static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.position=position;
            go.transform.rotation=rotation;
            go.transform.localScale=scale;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null)Destroy(collider);
            MeshRenderer renderer=go.GetComponent<MeshRenderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            return go;
        }

        private void ResetThemeState()
        {
            if(poolRoot!=null)Destroy(poolRoot.gameObject);
            poolRoot=null;
            appliedTheme=null;
            ReleaseGround();
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }

        private void ResetRuntimeState()
        {
            ResetThemeState();
            convertedGuards.Clear();
        }

        private void ReleaseGround()
        {
            if(groundMaterial!=null)Destroy(groundMaterial);
            if(groundTexture!=null)Destroy(groundTexture);
            groundMaterial=null;
            groundTexture=null;
        }

        private void OnDestroy()=>ResetRuntimeState();
    }

    /// <summary>Simple heavy gait for the runtime Pyraxis lava-golem shell.</summary>
    internal sealed class PyraxisLavaGolemMotion : MonoBehaviour
    {
        private Transform legL,legR,armL,armR,visualRoot;
        private Quaternion legRestL,legRestR,armRestL,armRestR;
        private Vector3 rootRest,lastPosition;
        private float phase,gait;

        private void Start()
        {
            Transform[] all=GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                switch(t.name)
                {
                    case "DF_MarsGolemLegL": legL=t; break;
                    case "DF_MarsGolemLegR": legR=t; break;
                    case "DF_MarsGolemArmL": armL=t; break;
                    case "DF_MarsGolemArmR": armR=t; break;
                    case "DF_MarsStoneGolemVisual": visualRoot=t; break;
                }
            }
            legRestL=legL!=null?legL.localRotation:Quaternion.identity;
            legRestR=legR!=null?legR.localRotation:Quaternion.identity;
            armRestL=armL!=null?armL.localRotation:Quaternion.identity;
            armRestR=armR!=null?armR.localRotation:Quaternion.identity;
            if(visualRoot!=null)rootRest=visualRoot.localPosition;
            lastPosition=transform.position;
        }

        private void LateUpdate()
        {
            float dt=Mathf.Max(Time.deltaTime,.0001f);
            Vector3 delta=transform.position-lastPosition;
            delta.y=0f;
            lastPosition=transform.position;
            float move=Mathf.Clamp01(delta.magnitude/(dt*1.15f));
            gait=Mathf.Lerp(gait,move,1f-Mathf.Exp(-10f*dt));
            phase=Mathf.Repeat(phase+delta.magnitude*4.3f,Mathf.PI*2f);
            float swing=Mathf.Sin(phase)*24f*gait;
            if(legL!=null)legL.localRotation=legRestL*Quaternion.Euler(swing,0,0);
            if(legR!=null)legR.localRotation=legRestR*Quaternion.Euler(-swing,0,0);
            if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(-swing*.42f,0,0);
            if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(swing*.42f,0,0);
            if(visualRoot!=null)visualRoot.localPosition=rootRest+Vector3.up*(Mathf.Abs(Mathf.Sin(phase))*-.055f*gait);
        }
    }
}
