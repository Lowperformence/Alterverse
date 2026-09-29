using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// DARK 00 presentation layer. Gameplay geometry and combat rules come from the Moon profile;
    /// this component only replaces presentation: near-black terrain, technical floodlights,
    /// white guard-mech light strips and the rolling Hot Zone orb.
    /// </summary>
    public sealed class Dark00ArenaRuntime : MonoBehaviour
    {
        private readonly List<Material> ownedMaterials=new();
        private readonly HashSet<HealingGuardMech> styledGuards=new();
        private GameObject activeWorld;
        private Transform visualRoot;
        private Material fixtureDark,fixtureMid,lampGlow,terrainBlack;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(GameObject.Find("DF_Dark00ArenaRuntime")!=null)return;
            GameObject go=new("DF_Dark00ArenaRuntime");
            DontDestroyOnLoad(go);
            go.AddComponent<Dark00ArenaRuntime>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.20f;

            if(!ArenaVariantRuntime.IsDark00)
            {
                if(activeWorld!=null)ResetState();
                return;
            }

            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null)return;

            Dark00GroundGuard.Ensure(world);

            if(activeWorld!=world)
            {
                ResetState();
                activeWorld=world;
                BuildPresentation(world.transform);
            }

            StyleGuards(world.transform);
            InstallHotZoneOrb(world.transform);
        }

        private void BuildPresentation(Transform world)
        {
            fixtureDark=MakeMaterial("DF_Dark00FixtureDark",new Color(.010f,.012f,.014f,1f),.78f,.28f,false,1f);
            fixtureMid=MakeMaterial("DF_Dark00FixtureMid",new Color(.045f,.050f,.055f,1f),.74f,.36f,false,1f);
            lampGlow=MakeMaterial("DF_Dark00FloodGlow",new Color(.92f,.96f,1f,1f),.02f,.82f,true,4.8f);
            terrainBlack=MakeMaterial("DF_Dark00TerrainBlack",new Color(.010f,.012f,.014f,1f),.03f,.10f,false,1f);

            visualRoot=new GameObject("DF_Dark00TechnicalLighting").transform;
            visualRoot.SetParent(world,false);

            StyleTerrainProps(world);
            BuildFloodlightGrid();
        }

        private void StyleTerrainProps(Transform world)
        {
            Renderer[] renderers=world.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer r=renderers[i];
                if(r==null||r.gameObject.name=="DF_LunarRegolith")continue;
                string n=r.gameObject.name;
                bool terrain=n.Contains("LunarRock")||n.Contains("LunarEdge")||n.Contains("Crater")||
                             n.Contains("Geological")||n.Contains("Debris")||n.Contains("Fragment")||
                             n.Contains("ImpactBed")||n.Contains("GroundInset")||n.Contains("BuriedRim");
                bool keepEnergy=n.Contains("Energy")||n.Contains("Fracture")||n.Contains("Seam")||n.Contains("Indicator");
                if(terrain&&!keepEnergy&&terrainBlack!=null)r.sharedMaterial=terrainBlack;
            }
        }

        private void BuildFloodlightGrid()
        {
            // Rotationally mirrored placements keep both teams' illumination and visual cover identical.
            Vector3[] positions={
                new(-38f,0f, 25f),new( 38f,0f,-25f),
                new(-38f,0f,-25f),new( 38f,0f, 25f),
                new(-25f,0f, 32f),new( 25f,0f,-32f),
                new(-25f,0f,-32f),new( 25f,0f, 32f),
                new(-16f,0f, 17f),new( 16f,0f,-17f),
                new(-16f,0f,-17f),new( 16f,0f, 17f),
                new( -6f,0f, 30f),new(  6f,0f,-30f),
                new( -6f,0f,-30f),new(  6f,0f, 30f)
            };

            for(int i=0;i<positions.Length;i++)BuildFloodlight(positions[i],i);
        }

        private void BuildFloodlight(Vector3 xz,int index)
        {
            Vector3 ground=LunarSurfaceField.Surface(xz.x,xz.z);
            Transform rig=new GameObject("DF_Dark00Floodlight_"+index).transform;
            rig.SetParent(visualRoot,false);
            rig.position=ground;

            float inward=xz.x<0f?1f:-1f;
            Primitive(PrimitiveType.Cylinder,"FloodPole",rig,new Vector3(0,2.4f,0),new Vector3(.16f,2.4f,.16f),Quaternion.identity,fixtureDark);
            Primitive(PrimitiveType.Cube,"FloodBase",rig,new Vector3(0,.12f,0),new Vector3(.72f,.20f,.72f),Quaternion.identity,fixtureMid);
            Primitive(PrimitiveType.Cube,"FloodCrossbar",rig,new Vector3(inward*.17f,4.77f,0),new Vector3(1.02f,.12f,.22f),Quaternion.Euler(0,0,inward*-8f),fixtureMid);

            for(int lamp=-1;lamp<=1;lamp++)
            {
                Vector3 local=new(inward*.46f,4.68f,lamp*.42f);
                Primitive(PrimitiveType.Cube,"FloodHousing",rig,local,new Vector3(.32f,.25f,.34f),Quaternion.Euler(0,inward>0?90f:-90f,0),fixtureDark);
                Primitive(PrimitiveType.Cube,"FloodEmitter",rig,local+new Vector3(inward*.18f,-.03f,0),new Vector3(.035f,.20f,.27f),Quaternion.identity,lampGlow);
            }

            GameObject lightObject=new("FloodSpot");
            lightObject.transform.SetParent(rig,false);
            lightObject.transform.localPosition=new Vector3(inward*.60f,4.66f,0);
            Vector3 target=LunarSurfaceField.Surface(xz.x+inward*9.5f,xz.z);
            target.y+=.15f;
            lightObject.transform.rotation=Quaternion.LookRotation((target-lightObject.transform.position).normalized,Vector3.up);
            Light light=lightObject.AddComponent<Light>();
            light.type=LightType.Spot;
            light.color=new Color(.86f,.92f,1f,1f);
            light.intensity=6.4f;
            light.range=24f;
            light.spotAngle=58f;
            light.shadows=LightShadows.None;
        }

        private void StyleGuards(Transform world)
        {
            HealingGuardMech[] guards=world.GetComponentsInChildren<HealingGuardMech>(true);
            for(int i=0;i<guards.Length;i++)
            {
                HealingGuardMech guard=guards[i];
                if(guard==null||styledGuards.Contains(guard))continue;

                Renderer[] renderers=guard.GetComponentsInChildren<Renderer>(true);
                for(int r=0;r<renderers.Length;r++)
                {
                    Renderer renderer=renderers[r];
                    if(renderer==null||renderer.sharedMaterial==null)continue;
                    string n=renderer.gameObject.name;
                    bool luminous=n.Contains("Strip")||n.Contains("Visor")||n.Contains("SensorEye")||
                                  n.Contains("PodLens")||n.Contains("AntennaTip")||n.Contains("Energy");
                    if(!luminous)continue;

                    Material m=renderer.sharedMaterial;
                    Color white=new(.96f,.98f,1f,1f);
                    if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",white);
                    if(m.HasProperty("_Color"))m.SetColor("_Color",white);
                    m.EnableKeyword("_EMISSION");
                    if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",white*4.2f);
                }
                styledGuards.Add(guard);
            }
        }

        private static void InstallHotZoneOrb(Transform world)
        {
            HotZoneTank[] tanks=world.GetComponentsInChildren<HotZoneTank>(true);
            for(int i=0;i<tanks.Length;i++)
            {
                HotZoneTank tank=tanks[i];
                if(tank!=null&&tank.GetComponent<Dark00OrbVisualOverride>()==null)
                    tank.gameObject.AddComponent<Dark00OrbVisualOverride>();
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

        private static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Quaternion localRotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            go.transform.localRotation=localRotation;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null)Destroy(collider);
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            return go;
        }

        private void ResetState()
        {
            if(visualRoot!=null)Destroy(visualRoot.gameObject);
            visualRoot=null;
            activeWorld=null;
            styledGuards.Clear();
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
            fixtureDark=fixtureMid=lampGlow=terrainBlack=null;
        }

        private void OnDestroy()=>ResetState();
    }

    /// <summary>Applies the black surface before the game camera renders, avoiding a one-frame Moon-colour flash.</summary>
    internal static class Dark00GroundGuard
    {
        private static GameObject activeWorld;
        private static MeshRenderer ground;
        private static Material original;
        private static Material darkMaterial;
        private static Texture2D blackTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCameraRendering;
            RenderPipelineManager.beginCameraRendering+=BeforeCameraRendering;
        }

        private static void BeforeCameraRendering(ScriptableRenderContext context,Camera camera)
        {
            if(camera==null||camera.cameraType!=CameraType.Game||camera.targetTexture!=null)return;
            if(!ArenaVariantRuntime.IsDark00)
            {
                Release();
                return;
            }
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)Ensure(world);
        }

        public static void Ensure(GameObject world)
        {
            if(world==null)return;
            if(activeWorld!=world||ground==null)
            {
                Release();
                activeWorld=world;
                MeshRenderer[] renderers=world.GetComponentsInChildren<MeshRenderer>(true);
                for(int i=0;i<renderers.Length;i++)
                    if(renderers[i]!=null&&renderers[i].gameObject.name=="DF_LunarRegolith")
                    {ground=renderers[i];break;}

                if(ground==null||ground.sharedMaterial==null)return;
                original=ground.sharedMaterial;
                blackTexture=new Texture2D(2,2,TextureFormat.RGBA32,false,false)
                {name="DF_Dark00BlackSurface",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                Color black=new(.010f,.012f,.014f,1f);
                blackTexture.SetPixels(new[]{black,black,black,black});
                blackTexture.Apply(false,true);
                darkMaterial=new Material(original){name="DF_Dark00BlackRegolith"};
                if(darkMaterial.HasProperty("_BaseMap"))darkMaterial.SetTexture("_BaseMap",blackTexture);
                if(darkMaterial.HasProperty("_MainTex"))darkMaterial.SetTexture("_MainTex",blackTexture);
                if(darkMaterial.HasProperty("_BaseColor"))darkMaterial.SetColor("_BaseColor",Color.white);
                if(darkMaterial.HasProperty("_Color"))darkMaterial.SetColor("_Color",Color.white);
            }
            if(ground!=null&&darkMaterial!=null&&ground.sharedMaterial!=darkMaterial)ground.sharedMaterial=darkMaterial;
        }

        private static void Release()
        {
            if(ground!=null&&original!=null&&ground.sharedMaterial==darkMaterial)ground.sharedMaterial=original;
            if(darkMaterial!=null)Object.Destroy(darkMaterial);
            if(blackTexture!=null)Object.Destroy(blackTexture);
            activeWorld=null;ground=null;original=null;darkMaterial=null;blackTexture=null;
        }
    }

    /// <summary>
    /// DARK 00 Hot Zone shell. The dark sphere truly rolls with travel distance. The red equator ring
    /// stays upright and only oscillates vertically by ten percent of the body radius, as requested.
    /// </summary>
    internal sealed class Dark00OrbVisualOverride : MonoBehaviour
    {
        private const float Radius=1.52f;
        private readonly List<Material> materials=new();
        private readonly List<Mesh> meshes=new();
        private Renderer[] hidden;
        private Transform visualRoot,bodyRoot,redRing;
        private Vector3 lastPosition,ringRest;
        private float ringPhase;

        private void Start()
        {
            hidden=GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<hidden.Length;i++)if(hidden[i]!=null)hidden[i].enabled=false;

            Material shell=MakeMaterial("DF_Dark00OrbShell",new Color(.105f,.115f,.125f,1f),.62f,.30f,false,1f);
            Material panel=MakeMaterial("DF_Dark00OrbPanel",new Color(.025f,.028f,.032f,1f),.72f,.20f,false,1f);
            Material red=MakeMaterial("DF_Dark00OrbRed",new Color(1f,.015f,.008f,1f),.02f,.70f,true,4.4f);

            visualRoot=new GameObject("DF_Dark00OrbVisual").transform;
            visualRoot.SetParent(transform,false);
            visualRoot.localPosition=new Vector3(0,.48f,0);

            bodyRoot=new GameObject("RollingBody").transform;
            bodyRoot.SetParent(visualRoot,false);
            GameObject body=Primitive(PrimitiveType.Sphere,"DarkOrbBody",bodyRoot,Vector3.zero,Vector3.one*(Radius*2f),Quaternion.identity,shell);

            // Subtle shell details rotate with the sphere so the rolling motion remains readable on a dark map.
            Vector3[] panelPoints={
                new(0f,.83f,1.25f),new(.88f,.50f,1.10f),new(-.92f,-.42f,1.08f),
                new(1.20f,-.50f,-.72f),new(-1.10f,.55f,-.82f),new(.15f,-1.25f,-.68f)
            };
            for(int i=0;i<panelPoints.Length;i++)
                Primitive(PrimitiveType.Sphere,"OrbShellPanel",bodyRoot,panelPoints[i],new Vector3(.28f,.12f,.18f),Quaternion.Euler(i*19f,i*47f,0),panel);

            redRing=new GameObject("RedPendulumRing").transform;
            redRing.SetParent(visualRoot,false);
            ringRest=Vector3.zero;
            redRing.localPosition=ringRest;
            Mesh ringMesh=ProductionMeshFactory.Ring(Radius*1.015f,.095f,44,8);
            meshes.Add(ringMesh);
            GameObject ring=ProductionMeshFactory.MeshObject("DF_Dark00RedStripe",redRing,ringMesh,red);
            ring.transform.localPosition=Vector3.zero;

            // Eye is independent from the rolling shell and remains aimed with the Hot Zone unit.
            Primitive(PrimitiveType.Sphere,"EyeSocket",visualRoot,new Vector3(0,.22f,Radius*.94f),new Vector3(.48f,.48f,.18f),Quaternion.identity,panel);
            Primitive(PrimitiveType.Sphere,"RedEye",visualRoot,new Vector3(0,.22f,Radius*1.025f),new Vector3(.31f,.31f,.12f),Quaternion.identity,red);

            lastPosition=transform.position;
        }

        private void LateUpdate()
        {
            if(!ArenaVariantRuntime.IsDark00)
            {
                Destroy(this);
                return;
            }
            if(bodyRoot==null||redRing==null)return;

            Vector3 delta=transform.position-lastPosition;
            delta.y=0f;
            lastPosition=transform.position;
            float distance=delta.magnitude;
            float dt=Mathf.Max(Time.deltaTime,.0001f);

            if(distance>.0001f)
            {
                Vector3 worldAxis=Vector3.Cross(Vector3.up,delta/distance).normalized;
                Vector3 localAxis=transform.InverseTransformDirection(worldAxis).normalized;
                float degrees=distance/Radius*Mathf.Rad2Deg;
                bodyRoot.localRotation=Quaternion.AngleAxis(degrees,localAxis)*bodyRoot.localRotation;

                ringPhase+=distance/Radius*2.1f;
                float amplitude=Radius*.10f;
                redRing.localPosition=ringRest+Vector3.up*(Mathf.Sin(ringPhase)*amplitude);
            }
            else
            {
                float t=1f-Mathf.Exp(-3.5f*dt);
                redRing.localPosition=Vector3.Lerp(redRing.localPosition,ringRest,t);
            }
        }

        private Material MakeMaterial(string name,Color color,float metallic,float smoothness,bool emission,float power)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return null;
            Material m=new(shader){name=name};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Color"))m.SetColor("_Color",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
            if(emission)
            {
                m.EnableKeyword("_EMISSION");
                if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",color*power);
            }
            materials.Add(m);
            return m;
        }

        private static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Quaternion rotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            go.transform.localRotation=rotation;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null)Destroy(collider);
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            return go;
        }

        private void OnDestroy()
        {
            if(visualRoot!=null)Destroy(visualRoot.gameObject);
            if(hidden!=null)for(int i=0;i<hidden.Length;i++)if(hidden[i]!=null)hidden[i].enabled=true;
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Destroy(materials[i]);
            for(int i=0;i<meshes.Count;i++)if(meshes[i]!=null)Destroy(meshes[i]);
            materials.Clear();meshes.Clear();
        }
    }
}
