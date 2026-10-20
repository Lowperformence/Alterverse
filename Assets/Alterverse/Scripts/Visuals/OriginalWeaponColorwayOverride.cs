using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Weapon-shop variants are colorways of each hero's authored standard weapon only.
    /// A translucent metallic finish and fluorescent runes share the original mesh.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    internal sealed class OriginalWeaponColorwayOverride : MonoBehaviour
    {
        private static OriginalWeaponColorwayOverride instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_OriginalWeaponColorwayOverride");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<OriginalWeaponColorwayOverride>();
        }

        private void LateUpdate()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.10f;

            HeroIdentity[] heroes=FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None);
            for(int i=0;i<heroes.Length;i++)
            {
                HeroIdentity hero=heroes[i];
                if(hero==null)continue;
                OriginalWeaponColorwayState state=hero.GetComponent<OriginalWeaponColorwayState>();
                if(state==null)state=hero.gameObject.AddComponent<OriginalWeaponColorwayState>();
                state.Sync(hero);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class OriginalWeaponColorwayState : MonoBehaviour
    {
        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Styled;
            public Renderer Overlay;
            public Mesh PatternMesh;
        }

        private readonly List<RendererState> baseRenderers=new();
        private Transform visualRoot;
        private int appliedVariant=int.MinValue;

        public void Sync(HeroIdentity identity)
        {
            if(identity==null)return;
            Transform nextRoot=FindRecursive(identity.transform,"DF_VisualRoot");
            if(nextRoot==null)return;

            if(visualRoot!=nextRoot)
            {
                RestoreAndRelease();
                visualRoot=nextRoot;
                CaptureOriginalWeaponRenderers();
                appliedVariant=int.MinValue;
            }

            // HeroRespawn hides the complete hero on death. Never let this late-running colorway
            // system re-enable the authored weapon while the hero is dead/awaiting respawn.
            Health health=identity.GetComponent<Health>();
            if(health!=null&&health.IsDead)
            {
                SetOriginalVisible(false);
                return;
            }

            SetOriginalVisible(true);

            SkinPreviewSelection preview=identity.GetComponent<SkinPreviewSelection>();
            int variant=preview!=null
                ?preview.WeaponSkinVariant
                :LocalHeroContext.IsLocal(identity.gameObject)
                    ?WeaponSkinInventory.EquippedVariant(identity.Hero)
                    :-1;
            if(preview!=null&&preview.WeaponOverdrive&&variant<0)variant=0;

            if(variant==appliedVariant)return;
            RestoreMaterialsOnly();
            appliedVariant=variant;

            if(variant>=0&&WeaponSkinCatalog.Contains(identity.Hero,variant))
                ApplyColorway(identity.Hero,variant);
        }

        private void CaptureOriginalWeaponRenderers()
        {
            baseRenderers.Clear();
            AuthoredHeroPresentation authored = GetComponent<AuthoredHeroPresentation>();
            if (authored != null)
            {
                var seen = new HashSet<Renderer>();
                foreach (Renderer renderer in authored.WeaponRenderers)
                    if (renderer != null && seen.Add(renderer)) baseRenderers.Add(new RendererState { Renderer = renderer, Original = (Material[])renderer.sharedMaterials.Clone() });
                return;
            }
            AddWeaponRoot("DF_WeaponL");
            AddWeaponRoot("DF_WeaponR");
            AddWeaponRoot("DF_GauntletL");
            AddWeaponRoot("DF_GauntletR");
        }

        private void AddWeaponRoot(string name)
        {
            Transform root=FindRecursive(visualRoot,name);
            if(root==null)return;

            Renderer[] renderers=root.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer renderer=renderers[i];
                if(renderer==null||renderer.name=="DF_WeaponEnergyOverlay")continue;

                bool duplicate=false;
                for(int r=0;r<baseRenderers.Count;r++)
                {
                    if(baseRenderers[r].Renderer==renderer)
                    {
                        duplicate=true;
                        break;
                    }
                }
                if(duplicate)continue;

                baseRenderers.Add(new RendererState
                {
                    Renderer=renderer,
                    Original=(Material[])renderer.sharedMaterials.Clone()
                });
            }
        }

        private void SetOriginalVisible(bool visible)
        {
            for(int i=0;i<baseRenderers.Count;i++)
            {
                Renderer renderer=baseRenderers[i].Renderer;
                if(renderer!=null)renderer.enabled=visible;
                if(baseRenderers[i].Overlay!=null)baseRenderers[i].Overlay.enabled=visible;
            }
        }

        private void ApplyColorway(HeroId hero,int variant)
        {
            WeaponSkinItem item=WeaponSkinCatalog.For(hero,variant);

            Shader shader=Resources.Load<Shader>("HeroArt/WeaponEnergyOverlay");
            if(shader==null)
            {
                Debug.LogError("DF: WeaponEnergyOverlay shader is missing; keeping standard weapon visible.",this);
                return;
            }
            foreach(RendererState state in baseRenderers)
            {
                Renderer source=state.Renderer;
                if(source==null)continue;
                var layer=new GameObject("DF_WeaponEnergyOverlay");
                layer.layer=source.gameObject.layer;
                layer.transform.SetParent(source.transform,false);
                Renderer overlay;
                int submeshes;
                if(source is SkinnedMeshRenderer skin && skin.sharedMesh!=null)
                {
                    var copy=layer.AddComponent<SkinnedMeshRenderer>();
                    state.PatternMesh=CreatePatternMesh(skin.sharedMesh);
                    copy.sharedMesh=state.PatternMesh;
                    copy.bones=skin.bones;
                    copy.rootBone=skin.rootBone;
                    copy.localBounds=skin.localBounds;
                    copy.quality=skin.quality;
                    copy.updateWhenOffscreen=skin.updateWhenOffscreen;
                    overlay=copy;
                    submeshes=skin.sharedMesh.subMeshCount;
                }
                else if(source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh!=null)
                {
                    state.PatternMesh=CreatePatternMesh(filter.sharedMesh);
                    layer.AddComponent<MeshFilter>().sharedMesh=state.PatternMesh;
                    overlay=layer.AddComponent<MeshRenderer>();
                    submeshes=filter.sharedMesh.subMeshCount;
                }
                else { Destroy(layer);continue; }
                var material=new Material(shader) { name="DF_PremiumColorway_"+hero+"_"+variant };
                Color glow=item.Glow;
                // Leave the authored texture visible between the brighter inset runes.
                glow.a=.24f;
                material.SetColor("_GlowColor",glow);
                material.SetColor("_MetalColor",new Color(.16f+glow.r*.12f,.18f+glow.g*.12f,.21f+glow.b*.12f));
                material.SetFloat("_Metallic",.88f);
                material.SetFloat("_Smoothness",.72f);
                material.SetFloat("_RimOpacity",.55f);
                material.SetFloat("_PatternStyle",PatternStyle(hero));
                material.SetFloat("_PatternOffset",variant*.37f);
                material.SetFloat("_PatternStrength",1f);
                var slots=new Material[submeshes];
                Material inscription=null;
                for(int i=0;i<slots.Length;i++)
                {
                    slots[i]=material;
                    if(i<state.Original.Length&&state.Original[i]!=null&&state.Original[i].name.Contains("Shield DR"))
                    {
                        if(inscription==null)
                        {
                            inscription=new Material(shader){name="DF_DR_ClearOverlay"};
                            inscription.SetColor("_GlowColor",Color.clear);
                            inscription.SetFloat("_RimOpacity",0f);
                        }
                        slots[i]=inscription;
                    }
                }
                overlay.sharedMaterials=slots;
                overlay.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                overlay.receiveShadows=false;
                overlay.lightProbeUsage=source.lightProbeUsage;
                overlay.reflectionProbeUsage=source.reflectionProbeUsage;
                overlay.probeAnchor=source.probeAnchor;
                overlay.enabled=source.enabled;
                state.Overlay=overlay;
                state.Styled=inscription!=null?new[]{material,inscription}:new[]{material};
            }
        }

        private static int PatternStyle(HeroId hero)
        {
            if(hero==HeroId.Nyra||hero==HeroId.Traveler)return 0;
            if(hero==HeroId.Elyra||hero==HeroId.Mira)return 1;
            if(hero==HeroId.Kaelon||hero==HeroId.Rook)return 2;
            return 3;
        }

        // Rest coordinates keep patterns attached during skinning. Only the overlay owns this copy.
        private static Mesh CreatePatternMesh(Mesh source)
        {
            Mesh mesh=Instantiate(source);
            mesh.name="DF_EpicDetail_"+source.name;
            if(!source.isReadable)return mesh;
            Vector3[] vertices=source.vertices;
            Bounds bounds=source.bounds;
            float scale=Mathf.Max(.001f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
            var coordinates=new List<Vector3>(vertices.Length);
            foreach(Vector3 vertex in vertices)coordinates.Add((vertex-bounds.center)/scale);
            mesh.SetUVs(2,coordinates);
            // Rest normals keep projection stable when the weapon turns or bends.
            Vector3[] normals=source.normals;
            if(normals.Length==vertices.Length)mesh.SetUVs(3,new List<Vector3>(normals));
            return mesh;
        }

        private void RestoreMaterialsOnly()
        {
            for(int i=0;i<baseRenderers.Count;i++)
            {
                RendererState state=baseRenderers[i];
                if(state.Renderer!=null&&state.Original!=null)
                    state.Renderer.sharedMaterials=state.Original;
                if(state.Overlay!=null)
                {
                    state.Overlay.gameObject.SetActive(false);
                    Destroy(state.Overlay.gameObject);
                    state.Overlay=null;
                }
                if(state.PatternMesh!=null)Destroy(state.PatternMesh);
                state.PatternMesh=null;
                Release(state.Styled);
                state.Styled=null;
            }
        }

        private void RestoreAndRelease()
        {
            RestoreMaterialsOnly();
            baseRenderers.Clear();
            visualRoot=null;
        }

        private void OnDestroy()=>RestoreAndRelease();

        private static void Release(Material[] materials)
        {
            if(materials==null)return;
            for(int i=0;i<materials.Length;i++)
                if(materials[i]!=null)Destroy(materials[i]);
        }

        private static Transform FindRecursive(Transform root,string name)
        {
            if(root==null)return null;
            if(root.name==name)return root;
            for(int i=0;i<root.childCount;i++)
            {
                Transform found=FindRecursive(root.GetChild(i),name);
                if(found!=null)return found;
            }
            return null;
        }
    }
}



