using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Weapon-shop variants are colorways of each hero's authored standard weapon only.
    /// Legacy EPIC/LEGENDARY replacement geometry is hidden presentation-only; gameplay,
    /// pivots, grip, animation, damage, range and timing remain untouched.
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

            // Old builders are allowed to exist for save compatibility, but never win visually.
            SuppressReplacementGeometry();
            EnsureOriginalVisible();

            SkinPreviewSelection preview=identity.GetComponent<SkinPreviewSelection>();
            int variant=preview!=null
                ?preview.WeaponSkinVariant
                :identity.GetComponent<HeroRespawn>()!=null
                    ?WeaponSkinInventory.EquippedVariant(identity.Hero)
                    :-1;

            if(variant==appliedVariant)return;
            RestoreMaterialsOnly();
            appliedVariant=variant;

            if(variant>=0&&WeaponSkinCatalog.Contains(identity.Hero,variant))
                ApplyColorway(identity.Hero,variant);
        }

        private void CaptureOriginalWeaponRenderers()
        {
            baseRenderers.Clear();
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
                if(renderer==null||IsReplacement(renderer.transform))continue;

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

        private void SuppressReplacementGeometry()
        {
            if(visualRoot==null)return;
            Renderer[] all=visualRoot.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<all.Length;i++)
            {
                Renderer renderer=all[i];
                if(renderer!=null&&IsReplacement(renderer.transform))renderer.enabled=false;
            }
        }

        private void EnsureOriginalVisible()
        {
            for(int i=0;i<baseRenderers.Count;i++)
            {
                Renderer renderer=baseRenderers[i].Renderer;
                if(renderer!=null)renderer.enabled=true;
            }
        }

        private void ApplyColorway(HeroId hero,int variant)
        {
            WeaponSkinItem item=WeaponSkinCatalog.For(hero,variant);

            // Variant 0 is the darker saturated treatment; variant 1 is the brighter polished treatment.
            Color accent=variant==0
                ?Color.Lerp(item.Glow,Color.black,.12f)
                :Color.Lerp(item.Glow,Color.white,.24f);
            float tint=variant==0?.62f:.72f;
            float metallic=variant==0?.66f:.60f;
            float smoothness=variant==0?.74f:.86f;
            float emission=variant==0?2.15f:2.65f;

            for(int i=0;i<baseRenderers.Count;i++)
            {
                RendererState state=baseRenderers[i];
                Renderer renderer=state.Renderer;
                if(renderer==null)continue;

                Material[] styled=new Material[state.Original.Length];
                for(int m=0;m<state.Original.Length;m++)
                {
                    Material source=state.Original[m];
                    if(source==null)continue;

                    Material material=new Material(source)
                    {
                        name=source.name+"_DF_Colorway_"+variant
                    };
                    Color baseColor=ReadColor(source);
                    Color target=Color.Lerp(baseColor,accent,tint);
                    target.a=baseColor.a;
                    WriteColor(material,target);

                    if(material.HasProperty("_Metallic"))
                        material.SetFloat("_Metallic",Mathf.Max(material.GetFloat("_Metallic"),metallic));
                    if(material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness",Mathf.Max(material.GetFloat("_Smoothness"),smoothness));
                    if(material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor",accent*emission);
                    }
                    styled[m]=material;
                }

                state.Styled=styled;
                renderer.sharedMaterials=styled;
            }
        }

        private void RestoreMaterialsOnly()
        {
            for(int i=0;i<baseRenderers.Count;i++)
            {
                RendererState state=baseRenderers[i];
                if(state.Renderer!=null&&state.Original!=null)
                    state.Renderer.sharedMaterials=state.Original;
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

        private static Color ReadColor(Material material)
        {
            if(material.HasProperty("_BaseColor"))return material.GetColor("_BaseColor");
            if(material.HasProperty("_Color"))return material.GetColor("_Color");
            return material.color;
        }

        private static void WriteColor(Material material,Color color)
        {
            material.color=color;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
        }

        private static void Release(Material[] materials)
        {
            if(materials==null)return;
            for(int i=0;i<materials.Length;i++)
                if(materials[i]!=null)Destroy(materials[i]);
        }

        private bool IsReplacement(Transform t)
        {
            Transform current=t;
            while(current!=null&&current!=visualRoot.parent)
            {
                string n=current.name;
                if(n.StartsWith("DF_Signature")||
                   n.StartsWith("DF_EpicWeaponVariant_")||
                   n.StartsWith("DF_EpicLuxuryDetailPass")||
                   n.StartsWith("DF_Epic_")||
                   n.StartsWith("DF_Legendary")||
                   n.StartsWith("DF_LegendaryV2_"))return true;
                if(current==visualRoot)break;
                current=current.parent;
            }
            return false;
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
