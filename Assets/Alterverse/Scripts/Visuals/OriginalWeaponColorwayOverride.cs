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
            bool inMatch = !Alterverse.UI.MainMenuController.IsVisible && Health.Active.Count > 0;
            nextScan=Time.unscaledTime+(inMatch ? .35f : .15f);
            if (inMatch)
            {
                var activeHealth = Health.Active;
                for (int i = 0; i < activeHealth.Count; i++)
                {
                    Health h = activeHealth[i];
                    if (h == null) continue;
                    HeroIdentity hero = h.GetComponent<HeroIdentity>();
                    if (hero == null) continue;
                    OriginalWeaponColorwayState state = hero.GetComponent<OriginalWeaponColorwayState>();
                    if (state == null) state = hero.gameObject.AddComponent<OriginalWeaponColorwayState>();
                    state.Sync(hero);
                }
                return;
            }
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
            public bool CombinedBody;
        }

        private static readonly UniversalHeroCosmetic[] BotHatPool =
        {
            UniversalHeroCosmetic.UfoTestHat,
            UniversalHeroCosmetic.WizardHat,
            UniversalHeroCosmetic.PropellerCap,
            UniversalHeroCosmetic.MushroomHat,
            UniversalHeroCosmetic.TvHead,
            UniversalHeroCosmetic.SharkHeadMask,
            UniversalHeroCosmetic.CyberPumpkinHead,
            UniversalHeroCosmetic.CardboardRobotHead,
            UniversalHeroCosmetic.GoldenCrown,
            UniversalHeroCosmetic.VikingHelm,
            UniversalHeroCosmetic.ChefHat,
            UniversalHeroCosmetic.PirateHat
        };

        private readonly List<RendererState> baseRenderers=new();
        private Transform visualRoot;
        private int appliedVariant=int.MinValue;
        private bool botCosmeticsRolled;
        private int rolledBotWeaponVariant=-1;
        private UniversalHeroCosmetic rolledBotHat=UniversalHeroCosmetic.Original;

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
                    :BotWeaponVariant(identity);
            if(preview!=null&&preview.WeaponOverdrive&&variant<0)variant=0;

            if(variant==appliedVariant)return;
            RestoreMaterialsOnly();
            appliedVariant=variant;

            if(variant>=0&&WeaponSkinCatalog.Contains(identity.Hero,variant))
                ApplyColorway(identity.Hero,variant);
        }

        private int BotWeaponVariant(HeroIdentity identity)
        {
            if(identity==null||identity.GetComponent<Alterverse.AI.MobaBotBrain>()==null)return -1;
            if(!botCosmeticsRolled)
            {
                botCosmeticsRolled=true;
                // Exactly 10% chance for a bot to equip a weapon skin, and 10% chance to wear a hat/helmet.
                rolledBotWeaponVariant=Random.value<0.10f?Random.Range(0,2):-1;
                rolledBotHat=Random.value<0.10f
                    ?BotHatPool[Random.Range(0,BotHatPool.Length)]
                    :UniversalHeroCosmetic.Original;
                if(rolledBotHat!=UniversalHeroCosmetic.Original)
                {
                    HeadwearPresentation headwear=identity.GetComponent<HeadwearPresentation>();
                    if(headwear!=null)headwear.Apply(rolledBotHat);
                }
            }
            return rolledBotWeaponVariant;
        }
        private void CaptureOriginalWeaponRenderers()
        {
            baseRenderers.Clear();
            // BOLT-7 keeps both weapons in one rigidly skinned body mesh.
            // The overlay filters weapon-bone triangles; it never changes body materials.
            if(GetComponent<HeroIdentity>()?.Hero==HeroId.Bolt7)
            {
                Transform body=FindRecursive(visualRoot,"BOLT7_LOD0");
                SkinnedMeshRenderer skin=body!=null?body.GetComponent<SkinnedMeshRenderer>():null;
                if(skin!=null)baseRenderers.Add(new RendererState{Renderer=skin,Original=(Material[])skin.sharedMaterials.Clone(),CombinedBody=true});
                return;
            }
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
                if(renderer!=null&&!baseRenderers[i].CombinedBody)renderer.enabled=visible;
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
                    state.PatternMesh=CreatePatternMesh(skin.sharedMesh,state.CombinedBody?skin.bones:null);
                    if(state.PatternMesh==null){Destroy(layer);continue;}
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
                bool isWhiteTheme = glow.r > .88f && glow.g > .88f && glow.b > .88f;
                Color metalCoat = isWhiteTheme
                    ? new Color(.92f, .95f, .99f, 1f)
                    : Color.Lerp(new Color(.10f, .11f, .14f, 1f), new Color(glow.r, glow.g, glow.b, 1f), .68f);
                Color secondaryAccent = SecondaryPatternAccent(hero, variant, glow, isWhiteTheme);
                // Strong, unmistakable lacquer coat so Red, Green, White, Pink and Gold read immediately.
                glow.a = isWhiteTheme ? .84f : .76f;
                material.SetColor("_GlowColor",glow);
                material.SetColor("_MetalColor",metalCoat);
                material.SetColor("_SecondaryColor",secondaryAccent);
                material.SetFloat("_Metallic",isWhiteTheme ? .65f : .82f);
                material.SetFloat("_Smoothness",isWhiteTheme ? .88f : .82f);
                material.SetFloat("_RimOpacity",.92f);
                material.SetFloat("_PatternStyle",variant == 0 ? 0f : 2f);
                material.SetFloat("_PatternVariant",variant);
                material.SetFloat("_PatternOffset",variant*1.37f+((int)hero)*.31f);
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
            if(hero==HeroId.Nyra||hero==HeroId.Traveler||hero==HeroId.Bolt7)return 0;
            if(hero==HeroId.Elyra||hero==HeroId.Mira)return 1;
            if(hero==HeroId.Kaelon||hero==HeroId.Rook)return 2;
            return 3;
        }

        private static Color SecondaryPatternAccent(HeroId hero, int variant, Color primary, bool isWhiteTheme)
        {
            if (isWhiteTheme)
            {
                return hero switch
                {
                    HeroId.Nyra => new Color(.12f, .55f, 1f, 1f),       // White + Cobalt Blue stripes
                    HeroId.Elyra => new Color(1f, .76f, .18f, 1f),      // White + Celestial Gold rings
                    HeroId.Rook => new Color(.08f, .82f, 1f, 1f),       // White + Glacier Cyan hex
                    HeroId.Nerok => new Color(.06f, .92f, .78f, 1f),    // White + Deep Turquoise scales
                    HeroId.VoidKnight => new Color(.68f, .22f, 1f, 1f), // White + Royal Violet diamond
                    _ => new Color(.20f, .78f, 1f, 1f)
                };
            }
            if (variant == 0)
            {
                // Variant 0: High-contrast obsidian/white/gold stripe accent
                return primary.g > .7f && primary.r < .5f
                    ? new Color(.92f, 1f, .95f, 1f) // Emerald Green + Crisp White stripes
                    : new Color(1f, .86f, .28f, 1f); // Red / Pink / Gold + Bright Solar Gold-White stripes
            }
            // Variant 1: High-contrast secondary glow for honeycomb/crystal lattice
            return primary.g > .7f
                ? new Color(.94f, 1f, .98f, 1f)     // Green + Pearl White hex lattice
                : new Color(.16f, .95f, 1f, 1f);    // Pink / Ruby + Electric Cyan-White hex lattice
        }

        // Rest coordinates keep patterns attached during skinning. Only the overlay owns this copy.
        private static Mesh CreatePatternMesh(Mesh source,Transform[] weaponBones=null)
        {
            if(weaponBones!=null&&!source.isReadable)return null;
            Mesh mesh=Instantiate(source);
            mesh.name="DF_EpicDetail_"+source.name;
            if(weaponBones!=null)Bolt7WeaponColorwayMask.KeepWeapons(mesh,weaponBones);
            if(!source.isReadable)return mesh;
            Vector3[] vertices=source.vertices;
            Bounds bounds=mesh.bounds;
            Bounds leftBounds=default,rightBounds=default;
            bool hasLeft=false,hasRight=false;
            if(weaponBones!=null)
            {
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    int[] tris=mesh.GetTriangles(s);
                    for(int t=0;t<tris.Length;t++)
                    {
                        Vector3 v=vertices[tris[t]];
                        if(v.x<bounds.center.x)
                        {
                            if(!hasLeft){leftBounds=new Bounds(v,Vector3.zero);hasLeft=true;}
                            else leftBounds.Encapsulate(v);
                        }
                        else
                        {
                            if(!hasRight){rightBounds=new Bounds(v,Vector3.zero);hasRight=true;}
                            else rightBounds.Encapsulate(v);
                        }
                    }
                }
            }
            var coordinates=new List<Vector3>(vertices.Length);
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 vertex=vertices[i];
                Bounds activeBounds=bounds;
                if(weaponBones!=null)
                {
                    if(vertex.x<bounds.center.x&&hasLeft)activeBounds=leftBounds;
                    else if(hasRight)activeBounds=rightBounds;
                }
                Vector3 size=activeBounds.size;
                float maxSpan=Mathf.Max(.001f,Mathf.Max(size.x,Mathf.Max(size.y,size.z)));
                float minAxis=maxSpan*.32f;
                float sx=Mathf.Max(minAxis,size.x);
                float sy=Mathf.Max(minAxis,size.y);
                float sz=Mathf.Max(minAxis,size.z);
                Vector3 delta=vertex-activeBounds.center;
                coordinates.Add(new Vector3(delta.x/sx,delta.y/sy,delta.z/sz));
            }
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
                if(!state.CombinedBody&&state.Renderer!=null&&state.Original!=null)
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




