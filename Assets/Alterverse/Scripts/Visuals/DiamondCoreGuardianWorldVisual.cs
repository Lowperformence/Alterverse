using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Robust world-space presentation for the PSR J1719-1438 b Hot Zone monster.
    /// The visible diamond is deliberately NOT parented below HotZoneTank so legacy creature
    /// visual scripts cannot hide it. Gameplay root, colliders, AI, targeting and combat stay intact.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public sealed class DiamondCoreGuardianWorldVisual : MonoBehaviour
    {
        sealed class VisualState
        {
            public GameObject root;
            public Mesh mesh;
            public readonly List<Material> materials=new();
            public float phase;
            public ReferenceMonsterRig reference;
            public bool wasWindingUp;
        }

        readonly Dictionary<HotZoneTank,VisualState> visuals=new();
        readonly List<HotZoneTank> stale=new();
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_PSRJ1719_DiamondGuardianWorldVisual")!=null)return;
            GameObject go=new("DF_PSRJ1719_DiamondGuardianWorldVisual");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DiamondCoreGuardianWorldVisual>();
        }

        public static void Prepare(HotZoneTank tank)
        {
            if(tank==null||!ArenaVariantRuntime.IsDiamondCore)return;
            var host=GameObject.Find("DF_PSRJ1719_DiamondGuardianWorldVisual");
            if(host==null)
            {
                host=new GameObject("DF_PSRJ1719_DiamondGuardianWorldVisual");
                Object.DontDestroyOnLoad(host);
            }
            var manager=host.GetComponent<DiamondCoreGuardianWorldVisual>()??host.AddComponent<DiamondCoreGuardianWorldVisual>();
            manager.SyncGuardians();
        }

        public static void UploadPreparedGeometry(HotZoneTank tank,ISet<Mesh> uploaded=null)
        {
            var host=GameObject.Find("DF_PSRJ1719_DiamondGuardianWorldVisual");
            var manager=host!=null?host.GetComponent<DiamondCoreGuardianWorldVisual>():null;
            if(manager!=null&&manager.visuals.TryGetValue(tank,out VisualState state)&&state.root!=null)
                AuthoredHotZoneArt.UploadPreparedGeometry(state.root.transform,uploaded);
        }

        public static void SetReservedVisibility(HotZoneTank tank,bool visible)
        {
            if(!ArenaVariantRuntime.IsDiamondCore)return;
            var manager=GameObject.Find("DF_PSRJ1719_DiamondGuardianWorldVisual")?.GetComponent<DiamondCoreGuardianWorldVisual>();
            if(manager!=null&&manager.visuals.TryGetValue(tank,out var state)&&state.root!=null)state.root.SetActive(visible);
        }

        public static bool TryGetBounds(HotZoneTank tank,out Bounds bounds)
        {
            bounds=default;
            if(!ArenaVariantRuntime.IsDiamondCore)return false;
            var manager=GameObject.Find("DF_PSRJ1719_DiamondGuardianWorldVisual")?.GetComponent<DiamondCoreGuardianWorldVisual>();
            if(manager==null||!manager.visuals.TryGetValue(tank,out var state)||state.root==null)return false;
            bool found=false;
            foreach(var renderer in state.root.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled)continue;
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        void Update()
        {
            if(!ArenaVariantRuntime.IsDiamondCore)
            {
                if(visuals.Count>0)ClearAll();
                return;
            }

            if(Time.unscaledTime>=nextScan)
            {
                nextScan=Time.unscaledTime+.15f;
                SyncGuardians();
            }

            AnimateGuardians();
        }

        void SyncGuardians()
        {
            IReadOnlyList<HotZoneTank> active=HotZoneTank.Active;
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank tank=active[i];
                if(tank==null||!tank.isActiveAndEnabled||tank.TankHealth==null||tank.TankHealth.IsDead)continue;

                HideLegacyMonsterMeshes(tank);

                // Disable the older child-based override. Its already-hidden legacy mesh state is kept,
                // while this controller supplies a separate world-space diamond that cannot be hidden by it.
                DiamondCoreCrystalGuardianVisualOverride legacy=tank.GetComponent<DiamondCoreCrystalGuardianVisualOverride>();
                if(legacy!=null)legacy.enabled=false;
                Transform oldChild=tank.transform.Find("DF_PSRJ1719_CrystalGuardianVisual");
                if(oldChild!=null)oldChild.gameObject.SetActive(false);

                if(!visuals.ContainsKey(tank))visuals.Add(tank,BuildVisual(tank));
            }

            stale.Clear();
            foreach(KeyValuePair<HotZoneTank,VisualState> pair in visuals)
            {
                HotZoneTank tank=pair.Key;
                if(tank==null||!tank.IsReservedReward&&(!tank.isActiveAndEnabled||tank.TankHealth==null||tank.TankHealth.IsDead))
                    stale.Add(tank);
            }
            for(int i=0;i<stale.Count;i++)RemoveVisual(stale[i]);
            stale.Clear();
        }

        void AnimateGuardians()
        {
            foreach(KeyValuePair<HotZoneTank,VisualState> pair in visuals)
            {
                HotZoneTank tank=pair.Key;
                VisualState state=pair.Value;
                if(tank==null||state==null||state.root==null)continue;
                if(tank.IsReservedReward){state.root.SetActive(false);continue;}

                // SyncGuardians already hides legacy meshes at the bounded scan interval.
                // Repeated hierarchy-array allocations here previously happened every frame.
                if(state.reference!=null)
                {
                    if(tank.AttackWindup&&!state.wasWindingUp)
                    {var gesture=tank.GetComponent<CreatureAttackMotion>();state.reference.PulseAttack(gesture!=null?gesture.ContactTime:.22f);if(gesture!=null)state.reference.Seek(gesture.Age);}
                    state.wasWindingUp=tank.AttackWindup;
                    continue;
                }
                float t=Time.time+state.phase;
                float cast=tank.AttackWindup?1f:0f;
                float hover=2.95f+Mathf.Sin(t*.92f)*.18f+cast*.12f;
                state.root.transform.localScale=Vector3.one*(1f+cast*.055f);
                state.root.transform.position=tank.transform.position+Vector3.up*hover;
                state.root.transform.rotation=Quaternion.Euler(
                    Mathf.Sin(t*.39f)*1.3f,
                    t*7.2f,
                    Mathf.Sin(t*.33f)*1.5f);
            }
        }

        VisualState BuildVisual(HotZoneTank tank)
        {
            VisualState state=new();
            state.reference=ReferenceMonsterRig.Build(tank.transform,"PSRCrystalTitan",transform);
            if(state.reference!=null)
            {
                state.root=state.reference.gameObject;
                tank.SetPresentationMuzzle(state.reference.Muzzle);
                return state;
            }
            state.phase=Mathf.Abs(tank.gameObject.name.GetHashCode()%1000)*.00628f;
            state.mesh=DiamondCoreCrystalVisuals.BuildGuardianGemMesh();

            state.root=new GameObject("DF_PSRJ1719_WorldDiamond_"+tank.gameObject.name);
            state.root.transform.SetParent(transform,false);
            state.root.transform.position=tank.transform.position+Vector3.up*2.95f;

            Material shell=CreateTransparentMaterial(state,"DF_PSRJ1719_DiamondShell",
                new Color(.34f,.29f,.31f,.72f),.96f,.65f,.07f);
            Material clear=CreateTransparentMaterial(state,"DF_PSRJ1719_DiamondInnerFacet",
                new Color(.78f,.91f,1f,.34f),.99f,1.15f,.04f);
            Material core=CreateCoreMaterial(state);

            CreateGem("MainDiamond",state.root.transform,state.mesh,shell,
                Vector3.zero,new Vector3(2.35f,3.25f,1.82f),Quaternion.Euler(-3f,18f,4f));
            CreateGem("InnerPrism",state.root.transform,state.mesh,clear,
                new Vector3(.04f,.01f,.02f),new Vector3(1.74f,2.52f,1.30f),Quaternion.Euler(178f,-8f,2f));
            CreateGem("FluorescentCore",state.root.transform,state.mesh,core,
                new Vector3(-.02f,-.08f,0f),new Vector3(.76f,1.62f,.56f),Quaternion.Euler(180f,12f,0f));

            AuthoredHotZoneArt.Apply(state.root.transform,"Crystal");
            return state;
        }

        static void HideLegacyMonsterMeshes(HotZoneTank tank)
        {
            MeshRenderer[] meshRenderers=tank.GetComponentsInChildren<MeshRenderer>(true);
            for(int i=0;i<meshRenderers.Length;i++)
                if(meshRenderers[i]!=null)meshRenderers[i].enabled=false;

            SkinnedMeshRenderer[] skinned=tank.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for(int i=0;i<skinned.Length;i++)
                if(skinned[i]!=null)skinned[i].enabled=false;
        }

        static GameObject CreateGem(string name,Transform parent,Mesh mesh,Material material,Vector3 position,Vector3 scale,Quaternion rotation)
        {
            GameObject go=new(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localScale=scale;
            go.transform.localRotation=rotation;
            MeshFilter filter=go.AddComponent<MeshFilter>();
            filter.sharedMesh=mesh;
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.enabled=true;
            return go;
        }

        static Material CreateTransparentMaterial(VisualState state,string name,Color color,float smoothness,float emission,float metallic)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material material=new(shader){name=name};
            state.materials.Add(material);
            DiamondCoreCrystalVisuals.ConfigureTransparentMaterial(material,color,smoothness,emission,metallic);
            return material;
        }

        static Material CreateCoreMaterial(VisualState state)
        {
            // Opaque emissive inner core guarantees the monster remains readable even when the
            // transparent shell is viewed against a bright arena surface.
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material material=new(shader){name="DF_PSRJ1719_DiamondVisibleCore"};
            state.materials.Add(material);
            Color color=new(.08f,.72f,1f,1f);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.04f);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.92f);
            material.EnableKeyword("_EMISSION");
            if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*4.8f);
            material.renderQueue=(int)RenderQueue.Geometry;
            return material;
        }

        void RemoveVisual(HotZoneTank tank)
        {
            if(!visuals.TryGetValue(tank,out VisualState state))return;
            visuals.Remove(tank);
            DestroyState(state);
        }

        void ClearAll()
        {
            foreach(KeyValuePair<HotZoneTank,VisualState> pair in visuals)DestroyState(pair.Value);
            visuals.Clear();
        }

        static void DestroyState(VisualState state)
        {
            if(state==null)return;
            if(state.root!=null)Object.Destroy(state.root);
            if(state.mesh!=null)Object.Destroy(state.mesh);
            for(int i=0;i<state.materials.Count;i++)
                if(state.materials[i]!=null)Object.Destroy(state.materials[i]);
            state.materials.Clear();
        }

        void OnDestroy(){ClearAll();}
    }
}





