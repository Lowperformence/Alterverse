using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Pyraxis-specific presentation pass. Keeps the base/spawn structures free of molten markings,
    /// concentrates true cow-shaped lava pools into the middle 50 percent of the arena, preserves the
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
            new(-16.0f,25.5f),new(16.0f,-25.5f),
            new(15.0f,18.0f),new(-15.0f,-18.0f),
            new(-9.0f,11.0f),new(9.0f,-11.0f),
            new(6.0f,26.0f),new(-6.0f,-26.0f),
            new(-4.5f,-5.5f),new(4.5f,5.5f)
        };

        private static readonly Vector2[] PoolRadii={
            new(5.4f,3.5f),new(5.4f,3.5f),
            new(4.8f,3.1f),new(4.8f,3.1f),
            new(4.2f,2.8f),new(4.2f,2.8f),
            new(3.8f,2.5f),new(3.8f,2.5f),
            new(3.5f,2.3f),new(3.5f,2.3f)
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
                bool zeroBase=new Vector2(p.x+ArenaLayout.CoreX,p.z).sqrMagnitude<11.8f*11.8f;
                bool lumenBase=new Vector2(p.x-ArenaLayout.CoreX,p.z).sqrMagnitude<11.8f*11.8f;
                bool zeroSpawn=new Vector2(p.x+ArenaLayout.HealingX,p.z).sqrMagnitude<9.8f*9.8f;
                bool lumenSpawn=new Vector2(p.x-ArenaLayout.HealingX,p.z).sqrMagnitude<9.8f*9.8f;
                if(zeroBase||lumenBase||zeroSpawn||lumenSpawn)Destroy(t.gameObject);
            }
        }

        private SceneryBuildingClearance clearance;

        private void BuildCentralLavaPools(Transform theme)
        {
            if(theme==null)return;
            clearance=new SceneryBuildingClearance();
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
                float rotation=(i*37f)%180f;

                // Main body of each pool. The additional overlapping lobes below turn the ellipse into
                // a broad irregular cow-spot silhouette with no stream-like direction.
                Vector3 p=LunarSurfaceField.Surface(center.x,center.y);
                p.y+=.035f;
                Primitive(PrimitiveType.Cylinder,"CentralLavaPoolRim",poolRoot,p,
                    new Vector3(radii.x+.72f,.025f,radii.y+.62f),Quaternion.Euler(0,rotation,0),rim);
                Primitive(PrimitiveType.Cylinder,"CentralLavaPool",poolRoot,p+Vector3.up*.018f,
                    new Vector3(radii.x,.022f,radii.y),Quaternion.Euler(0,rotation,0),lava);

                Vector2[] offsets={
                    new(radii.x*.50f,-radii.y*.10f),
                    new(-radii.x*.43f,radii.y*.17f),
                    new(radii.x*.10f,radii.y*.47f),
                    new(-radii.x*.16f,-radii.y*.43f)
                };
                Vector2[] scales={
                    new(.48f,.45f),new(.44f,.50f),new(.40f,.42f),new(.42f,.39f)
                };

                for(int l=0;l<offsets.Length;l++)
                {
                    Vector2 o=offsets[l];
                    Vector2 s=scales[l];
                    Vector3 lp=LunarSurfaceField.Surface(center.x+o.x,center.y+o.y);
                    lp.y+=.050f+l*.002f;
                    float lobeRotation=rotation+(l-1.5f)*27f;

                    Primitive(PrimitiveType.Cylinder,"CentralLavaPoolRimLobe",poolRoot,lp,
                        new Vector3(radii.x*s.x+.34f,.020f,radii.y*s.y+.30f),
                        Quaternion.Euler(0,lobeRotation,0),rim);
                    Primitive(PrimitiveType.Cylinder,"CentralLavaPoolLobe",poolRoot,lp+Vector3.up*.016f,
                        new Vector3(radii.x*s.x,.016f,radii.y*s.y),
                        Quaternion.Euler(0,lobeRotation,0),lava);
                }

                // A couple of hot cores inside each blob make the lava read as molten without creating lines.
                Vector3 coreA=LunarSurfaceField.Surface(center.x-radii.x*.13f,center.y+radii.y*.08f);
                coreA.y+=.064f;
                Primitive(PrimitiveType.Cylinder,"CentralLavaPoolHotCore",poolRoot,coreA,
                    new Vector3(radii.x*.30f,.014f,radii.y*.26f),Quaternion.Euler(0,rotation-18f,0),hot);

                Vector3 coreB=LunarSurfaceField.Surface(center.x+radii.x*.24f,center.y-radii.y*.18f);
                coreB.y+=.066f;
                Primitive(PrimitiveType.Cylinder,"CentralLavaPoolHotCore",poolRoot,coreB,
                    new Vector3(radii.x*.20f,.012f,radii.y*.18f),Quaternion.Euler(0,rotation+31f,0),hot);
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
            groundMaterial=new Material(source){name="DF_Pyraxis_CowLavaGround"};
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
                name="DF_PyraxisCowLavaAlbedo",
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

                    float blobA=Mathf.PerlinNoise(wx*.016f+21f,wz*.018f+77f);
                    float blobB=Mathf.PerlinNoise(wx*.022f+91f,wz*.020f+13f);
                    float blobC=Mathf.PerlinNoise(wx*.029f+143f,wz*.026f+51f);
                    float cowField=blobA*.50f+blobB*.32f+blobC*.18f;
                    float orangeSpot=Mathf.SmoothStep(.335f,.475f,cowField);
                    float edgeBreak=Mathf.PerlinNoise(wx*.067f+44f,wz*.061f+99f);
                    orangeSpot=Mathf.Clamp01(orangeSpot+(edgeBreak-.5f)*.075f);

                    float rockDetail=Mathf.PerlinNoise(wx*.18f+11f,wz*.18f+53f);
                    float darkValue=.80f+(rockDetail-.5f)*.10f;
                    Color darkBasalt=new(.026f*darkValue,.014f*darkValue,.010f*darkValue,1f);
                    Color orangeBasalt=Color.Lerp(
                        new Color(.78f,.115f,.004f,1f),
                        new Color(1f,.35f,.012f,1f),
                        Mathf.Clamp01(.28f+rockDetail*.60f));

                    float zeroBase=1f-Mathf.SmoothStep(8.8f,11.8f,Vector2.Distance(new Vector2(wx,wz),new Vector2(-36f,0f)));
                    float lumenBase=1f-Mathf.SmoothStep(8.8f,11.8f,Vector2.Distance(new Vector2(wx,wz),new Vector2(36f,0f)));
                    float zeroSpawn=1f-Mathf.SmoothStep(7.2f,9.8f,Vector2.Distance(new Vector2(wx,wz),new Vector2(-ArenaLayout.HealingX/ArenaLayout.LengthScale,0f)));
                    float lumenSpawn=1f-Mathf.SmoothStep(7.2f,9.8f,Vector2.Distance(new Vector2(wx,wz),new Vector2(ArenaLayout.HealingX/ArenaLayout.LengthScale,0f)));
                    float baseProtection=Mathf.Clamp01(Mathf.Max(Mathf.Max(zeroBase,lumenBase),Mathf.Max(zeroSpawn,lumenSpawn)));
                    orangeSpot*=1f-baseProtection;

                    Color ground=Color.Lerp(darkBasalt,orangeBasalt,orangeSpot*.98f);

                    // The molten layer follows the same broad cow-spot language and is strictly central.
                    float moltenA=Mathf.PerlinNoise(wx*.032f+201f,wz*.036f+37f);
                    float moltenB=Mathf.PerlinNoise(wx*.049f+17f,wz*.043f+151f);
                    float moltenField=moltenA*.70f+moltenB*.30f;
                    float lavaMask=Mathf.SmoothStep(.535f,.665f,moltenField);
                    float centralBand=1f-Mathf.SmoothStep(18.0f,21.0f,Mathf.Abs(wx));
                    lavaMask*=centralBand*(1f-baseProtection);

                    float hot=Mathf.SmoothStep(.34f,.88f,lavaMask);
                    Color molten=Color.Lerp(new Color(.94f,.055f,.002f,1f),new Color(1f,.63f,.025f,1f),hot);
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
            if(!emission)PlanetSceneryMaterials.Apply(material,ArenaVariantRuntime.Current,SceneryFinish.Stone);
            return material;
        }

        private GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
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
            clearance.RemoveIfBlocked(go);
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




