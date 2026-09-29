using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Pyraxis-specific presentation pass. Keeps the base/spawn areas free of molten markings,
    /// concentrates lava into the middle 50 percent of the arena, and presents both healing guards
    /// as lava golems derived from the existing Mars stone-golem visual. Visual only: no gameplay
    /// colliders, health, AI, damage or pathing are changed.
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
            groundMaterial=new Material(source){name="DF_Pyraxis_CentralLavaGround"};
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
                name="DF_PyraxisCentralLavaAlbedo",
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
                    float macro=Mathf.PerlinNoise(wx*.055f+31f,wz*.055f+73f);
                    float detail=Mathf.PerlinNoise(wx*.22f+11f,wz*.22f+47f);
                    float basaltValue=.72f+macro*.19f+(detail-.5f)*.10f;
                    Color basalt=new(.055f*basaltValue,.031f*basaltValue,.024f*basaltValue,1f);

                    float mask=0f;
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
                            mask=Mathf.Max(mask,1f-Mathf.SmoothStep(.72f,1.06f,d));
                        }
                        float band=1f-Mathf.SmoothStep(18.5f,21f,Mathf.Abs(wx));
                        mask*=band;
                    }

                    float hot=Mathf.SmoothStep(.38f,.92f,mask);
                    Color molten=Color.Lerp(new Color(.78f,.055f,.004f,1f),new Color(1f,.56f,.025f,1f),hot);
                    pixels[y*width+x]=Color.Lerp(basalt,molten,Mathf.Clamp01(mask*.96f));
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
