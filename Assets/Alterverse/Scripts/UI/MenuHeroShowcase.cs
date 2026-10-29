using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One isolated preview of the actual hero model; never a combat actor.</summary>
    [DefaultExecutionOrder(1200)]
    public sealed class MenuHeroShowcase : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        // Higher-resolution portrait target keeps the hero crisp when the UI card is scaled up.
        private const int PreviewWidth = 1536;
        private const int PreviewHeight = 1728;
        private GameObject stage, actor;
        private Camera previewCamera;
        private RenderTexture texture;
        private HeroId selected;
        private bool hasSelection,selectedWeapon;
        private Alterverse.Shop.UniversalHeroCosmetic selectedCosmetic;
        private Alterverse.Shop.UniversalHeroCosmetic selectedHeadwear,cachedHeadwear;
        private float selectedAt;
        private bool needsFit;
        private bool loadoutDirty=true,cachedWeapon;
        private int cachedWeaponVariant=-1,selectedWeaponVariant=-1;
        private HeroId cachedHero;
        private Alterverse.Shop.UniversalHeroCosmetic cachedCosmetic;
        private HeroOriginBackdrop originBackdrop;
        private SpriteRenderer originWorld;
        private MenuHeroGrounding grounding;
        private MenuHeroPedestal pedestal;
        // Keep only the active preview and its two most recent variants. Returning
        // to a hero reuses its model/materials instead of rebuilding everything.
        private const int PreviewCacheCapacity=3;
        private sealed class CachedPreview
        {
            public HeroId Hero;
            public Alterverse.Shop.UniversalHeroCosmetic Cosmetic,Headwear;
            public bool Weapon;
            public int WeaponVariant;
            public GameObject Actor;
            public MenuHeroGrounding Grounding;
        }
        private readonly System.Collections.Generic.List<CachedPreview> previews=new();
        // Older local skin scripts expose only Equipped/WeaponOverdrive. Preserve the
        // selected variant when the newer API is present without requiring it to compile.
        private static int EquippedWeaponVariant(HeroId hero)
        {
            var method=typeof(Alterverse.Shop.WeaponSkinInventory).GetMethod("EquippedVariant",
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
            if(method!=null&&method.ReturnType==typeof(int))
                return (int)method.Invoke(null,new object[]{hero});
            return Alterverse.Shop.WeaponSkinInventory.Equipped(hero)?0:-1;
        }
        private static void SetPreviewWeaponVariant(Alterverse.Shop.SkinPreviewSelection preview,int variant)
        {
            var field=typeof(Alterverse.Shop.SkinPreviewSelection).GetField("WeaponSkinVariant",
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);
            if(field!=null&&field.FieldType==typeof(int))field.SetValue(preview,variant);
        }
        public void InvalidateLoadout()=>loadoutDirty=true;
        private void OnEnable(){Alterverse.Profile.ProfileTransaction.Committed+=InvalidateLoadout;Alterverse.Shop.PaidShardCommerce.EntitlementsChanged+=InvalidateLoadout;loadoutDirty=true;}
        private Transform orbitLeft, orbitRight, wingLeft, wingRight;
        public Texture Image => texture;
        public Texture BackgroundImage => originWorld != null && originWorld.sprite != null ? originWorld.sprite.texture : null;

        public void DrawPreview(Rect rect)
        {
            if(rect.width<=0f||rect.height<=0f)return;

            // Clip everything to the authored card, then deliberately overscan the backdrop a little.
            // This prevents thin uncovered borders on differently shaped hero cards while keeping the hero undistorted.
            GUI.BeginGroup(rect);
            Rect local=new Rect(0f,0f,rect.width,rect.height);
            Color oldColor=GUI.color;
            // A missing imported asset still uses only the selected hero's palette.
            GUI.color=hasSelection?HeroOriginBackdrop.BaseColorFor(selected):new Color(.010f,.014f,.024f,1f);
            GUI.DrawTexture(local,Texture2D.whiteTexture,ScaleMode.StretchToFill,false);
            GUI.color=new Color(1f,1f,1f,oldColor.a);

            if(BackgroundImage!=null)
            {
                float bleed=Mathf.Max(2f,Mathf.Min(rect.width,rect.height)*.025f);
                Rect backdropRect=new Rect(-bleed,-bleed,rect.width+bleed*2f,rect.height+bleed*2f);
                GUI.DrawTexture(backdropRect,BackgroundImage,ScaleMode.ScaleAndCrop,false);
            }

            if(Image!=null)
            {
                float heroBleed=Mathf.Min(rect.width,rect.height)*.015f;
                Rect heroRect=new Rect(-heroBleed,-heroBleed,rect.width+heroBleed*2f,rect.height+heroBleed*2f);
                GUI.DrawTexture(heroRect,Image,ScaleMode.ScaleToFit,true);
            }
            GUI.color=oldColor;
            GUI.EndGroup();
        }

        public void Present(HeroId hero, bool visible, bool? skinOverride=null,bool? weaponOverride=null,
            Alterverse.Shop.UniversalHeroCosmetic? cosmeticOverride=null,int? weaponVariantOverride=null)
        {
            if (!visible) { if (stage != null) stage.SetActive(false); return; }
            if (stage == null) CreateStage();
            stage.SetActive(true);

            if(loadoutDirty||cachedHero!=hero)
            {
                cachedHero=hero;
                cachedCosmetic=Alterverse.Shop.UniversalCosmeticSelection.EquippedFor(hero);
                cachedHeadwear=Alterverse.Shop.HeadwearSelection.EquippedFor(hero);
                cachedWeaponVariant=EquippedWeaponVariant(hero);
                cachedWeapon=cachedWeaponVariant>=0;
                loadoutDirty=false;
            }
            Alterverse.Shop.UniversalHeroCosmetic cosmetic=cosmeticOverride ??
                (skinOverride.HasValue
                    ? (skinOverride.Value?Alterverse.Shop.UniversalHeroCosmetic.Voidfall:Alterverse.Shop.UniversalHeroCosmetic.Original)
                    : cachedCosmetic);
            var headwear=cachedHeadwear;
            if(Alterverse.Shop.CosmeticCatalog.Find(cosmetic)?.Headwear==true)
            {headwear=cosmetic;cosmetic=cachedCosmetic;}
            bool weapon=weaponOverride??cachedWeapon;
            int weaponVariant=weaponVariantOverride??(weapon?(cachedWeaponVariant>=0?cachedWeaponVariant:0):-1);
            weapon=weapon&&weaponVariant>=0;

            if (hasSelection && selected == hero && selectedCosmetic==cosmetic && selectedHeadwear==headwear && selectedWeapon==weapon && selectedWeaponVariant==weaponVariant) return;
            selectedHeadwear=headwear;
            selectedCosmetic=cosmetic;selectedWeapon=weapon;selectedWeaponVariant=weaponVariant;
            if (actor != null) actor.SetActive(false);
            selected = hero; hasSelection = true; selectedAt = Time.unscaledTime;
            UpdateOriginWorld(hero);
            actor=null;grounding=null;
            for(int i=0;i<previews.Count;i++)
            {
                var cached=previews[i];
                if(cached.Actor==null||cached.Hero!=hero||cached.Cosmetic!=cosmetic||cached.Headwear!=headwear||cached.Weapon!=weapon||cached.WeaponVariant!=weaponVariant)continue;
                actor=cached.Actor;grounding=cached.Grounding;previews.RemoveAt(i);previews.Add(cached);break;
            }
            if(actor==null)
            {
                while(previews.Count>=PreviewCacheCapacity)
                {
                    var stale=previews[0];previews.RemoveAt(0);
                    if(stale.Actor!=null)Destroy(stale.Actor);
                }
                actor = new GameObject("DF_MenuHero");
                actor.transform.SetParent(stage.transform, false);
                actor.AddComponent<HeroIdentity>().Configure(hero);
                var preview=actor.AddComponent<Alterverse.Shop.SkinPreviewSelection>();
                preview.UniversalCosmetic=cosmetic;
                preview.Headwear=headwear;
                preview.WeaponOverdrive=weapon;
                SetPreviewWeaponVariant(preview,weaponVariant);
                actor.AddComponent<StylizedHeroVisual>().Build();
                // No health, colliders, input, abilities, team membership or game-world registration.
                foreach (Transform part in actor.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = PreviewLayer;
                foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.HasProperty("_ShowcaseLighting"))
                    {
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                        block.SetFloat("_ShowcaseLighting",1f);renderer.SetPropertyBlock(block);
                    }
                }
                grounding=new MenuHeroGrounding(actor);
                previews.Add(new CachedPreview{Hero=hero,Cosmetic=cosmetic,Headwear=headwear,Weapon=weapon,WeaponVariant=weaponVariant,Actor=actor,Grounding=grounding});
            }
            actor.SetActive(true);
            Transform visual=actor.transform.Find("DF_VisualRoot");
            orbitLeft=visual!=null?visual.Find("DF_OrbitL"):null; orbitRight=visual!=null?visual.Find("DF_OrbitR"):null;
            wingLeft=visual!=null?visual.Find("DF_WingL"):null; wingRight=visual!=null?visual.Find("DF_WingR"):null;
            float facing=hero==HeroId.Nyra?-24:hero==HeroId.Kaelon?-24:-16;
            actor.transform.localRotation=Quaternion.Euler(0,facing,0);
            needsFit = true;
        }
        private void UpdateOriginWorld(HeroId hero)
        {
            if(originWorld==null)return;
            originBackdrop??=new HeroOriginBackdrop();
            var sprite=originBackdrop.SpriteFor(hero);
            // A missing asset must never leave another hero's world behind.
            originWorld.sprite=sprite;
        }
        private void FitPreviewBounds()
        {
            // Frame stable skeleton anchors. BakeMesh can report unsuitable coordinates
            // for imported FBX scale/bindpose combinations in the remote portrait stage.
            bool found = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            Quaternion cameraSpace = Quaternion.Inverse(previewCamera.transform.rotation);
            foreach (Transform joint in actor.GetComponentsInChildren<Transform>(true))
            {
                string name = joint.name;
                bool body = name == "Hips" || name == "Spine" || name == "Chest" || name == "Head" ||
                    name.StartsWith("UpperArm", System.StringComparison.Ordinal) || name.StartsWith("Hand", System.StringComparison.Ordinal) ||
                    name.StartsWith("Foot", System.StringComparison.Ordinal) || name.StartsWith("Shin", System.StringComparison.Ordinal);
                bool tip = name == "DF_Muzzle" || name == "DF_SupportGrip" || name == "DF_TankHammerTop" || name == "DF_MageStaffTop" ||
                    name == "socket_gatling_muzzle" || name == "socket_grenade_muzzle";
                if (!body && !tip) continue;
                Vector3 point = cameraSpace * (joint.position - actor.transform.position);
                // Discard outliers rather than pointing the portrait camera out of its stage.
                if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) || point.sqrMagnitude > 100f) continue;
                Vector3 padding = name == "Head" ? new Vector3(.24f,.52f,.24f) :
                    name.StartsWith("Foot", System.StringComparison.Ordinal) ? new Vector3(.18f,.13f,.25f) :
                    name.StartsWith("Hand", System.StringComparison.Ordinal) ? new Vector3(.30f,.30f,.30f) : new Vector3(.20f,.20f,.20f);
                Include(point - padding, ref found, ref min, ref max);
                Include(point + padding, ref found, ref min, ref max);
            }
            // Generated wing meshes have stable local bounds, unlike imported FBX skins.
            // Include their transformed corners so every cosmetic fits its actual preview.
            foreach (MeshFilter filter in actor.GetComponentsInChildren<MeshFilter>())
            {
                string name = filter.name;
                if (filter.sharedMesh == null ||
                    !(name.EndsWith("WingLeft", System.StringComparison.Ordinal) ||
                      name.EndsWith("WingRight", System.StringComparison.Ordinal) ||
                      name.StartsWith("DF_Headwear_", System.StringComparison.Ordinal))) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 point = cameraSpace * (filter.transform.TransformPoint(local) - actor.transform.position);
                    Include(point - Vector3.one * .05f, ref found, ref min, ref max);
                    Include(point + Vector3.one * .05f, ref found, ref min, ref max);
                }
            }
            if (!found) { min = new Vector3(-.8f,-1.1f,-.5f); max = new Vector3(.8f,1.65f,.5f); }
            if(pedestal!=null)
            {
                Bounds deck=pedestal.Bounds;
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 world=deck.center+Vector3.Scale(deck.extents,new Vector3(
                        (corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    Include(cameraSpace*(world-actor.transform.position),ref found,ref min,ref max);
                }
            }
            Vector3 center = actor.transform.position + previewCamera.transform.rotation * ((min + max) * .5f);
            previewCamera.transform.position = center - previewCamera.transform.forward * 5f;
            Vector3 size = max - min;
            previewCamera.orthographicSize = Mathf.Clamp(
                Mathf.Max(size.y * .5f, size.x * .5f / (PreviewWidth / (float)PreviewHeight)) * 1.10f, 1.3f, 3.5f);
            // Keep small pose changes from invalidating imported renderer culling bounds.
            foreach (SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
        }
        private static void Include(Vector3 point, ref bool found, ref Vector3 min, ref Vector3 max)
        {
            if (!found) { min = max = point; found = true; }
            else { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
        }
        private void CreateStage()
        {
            stage = new GameObject("DF_MenuPortraitStage");
            stage.transform.SetParent(transform, false);
            stage.transform.position = new Vector3(12000,12000,12000);
            pedestal=new MenuHeroPedestal(stage.transform,PreviewLayer);

            // A single cached SpriteRenderer sits behind the real hero preview. Swapping heroes only swaps
            // the cached origin-world sprite; there are no particles, extra cameras or gameplay objects.
            var worldObject=new GameObject("DF_HeroOriginWorld");
            worldObject.transform.SetParent(stage.transform,false);
            worldObject.transform.localPosition=new Vector3(0,.20f,-2.2f);
            worldObject.layer=PreviewLayer;
            originWorld=worldObject.AddComponent<SpriteRenderer>();
            originWorld.sortingOrder=-100;
            // Background is composited at card resolution, never through the narrow hero camera.
            originWorld.enabled=false;
            originWorld.color=new Color(1,1,1,.96f);
            originBackdrop=new HeroOriginBackdrop();

            texture = new RenderTexture(PreviewWidth,PreviewHeight,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB)
            { name = "DF_HeroPortrait_HD", antiAliasing = 4, useMipMap = false, autoGenerateMips = false, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.Create();
            var cameraObject = new GameObject("DF_PortraitCamera"); cameraObject.transform.SetParent(stage.transform,false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.orthographic = true; previewCamera.orthographicSize = 1.65f;
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 12;
            previewCamera.allowHDR = false; previewCamera.allowMSAA = true;
            previewCamera.allowDynamicResolution = false;
            previewCamera.useOcclusionCulling = false; previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.targetTexture = texture; previewCamera.depth = -20;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.renderShadows = false;
            data.volumeLayerMask = 0;
            cameraObject.transform.localPosition = new Vector3(0,1.10f,5);
            cameraObject.transform.LookAt(stage.transform.position + Vector3.up*.08f);
            AddLight("Key",new Vector3(-2,3,3),new Color(.91f,.95f,1),3.5f);
            AddLight("Fill",new Vector3(2,1,2),new Color(.51f,.72f,.78f),1.8f);
            AddLight("Rim",new Vector3(1.8f,2.7f,-2.5f),new Color(.33f,.82f,1f),2.2f);
        }
        private void AddLight(string label, Vector3 position, Color color, float intensity)
        {
            var go = new GameObject("DF_Portrait"+label); go.transform.SetParent(stage.transform,false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>(); light.type = LightType.Point;
            light.color = color; light.intensity = intensity; light.range = 8;
            light.shadows = LightShadows.None; light.cullingMask = 1 << PreviewLayer;
        }
        private void Update()
        {
            if(actor!=null&&stage!=null&&stage.activeSelf)grounding?.Prepare();
        }
        private void LateUpdate()
        {
            if (actor == null || stage == null || !stage.activeSelf) return;
            UpdateOriginWorld(selected);
            float t = Time.unscaledTime - selectedAt;
            float facing=selected==HeroId.Nyra?-24: selected==HeroId.Kaelon?-24:-16;
            actor.transform.localRotation = Quaternion.Euler(0,facing+Mathf.Sin(t*.55f)*4,0);
            actor.transform.localPosition = Vector3.zero;
            if(orbitLeft!=null) orbitLeft.localPosition=new Vector3(-.58f,.28f+Mathf.Sin(t*1.4f)*.06f,.03f);
            if(orbitRight!=null) orbitRight.localPosition=new Vector3(.58f,.28f-Mathf.Sin(t*1.4f)*.06f,.03f);
            if(wingLeft!=null) wingLeft.localRotation=Quaternion.Euler(0,-18,-18-Mathf.Sin(t*2)*3);
            if(wingRight!=null) wingRight.localRotation=Quaternion.Euler(0,18,18+Mathf.Sin(t*2)*3);
            if(grounding!=null&&grounding.PlaceFeet(stage.transform,MenuHeroPedestal.SurfaceY))
            {
                pedestal?.SetRadius(grounding.Radius);
                pedestal?.UpdateContacts(grounding,actor.transform);
            }
            // Fit once, after the retargeted pose is ready, before this frame renders.
            if (needsFit)
            {
                FitPreviewBounds();
                needsFit = false;
            }
        }
        private void OnDisable() { Alterverse.Profile.ProfileTransaction.Committed-=InvalidateLoadout;Alterverse.Shop.PaidShardCommerce.EntitlementsChanged-=InvalidateLoadout; if (stage != null) stage.SetActive(false); }
        private void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
            originBackdrop?.Dispose();
            originBackdrop=null;
            pedestal?.Dispose();pedestal=null;grounding=null;
            previews.Clear();
            if (stage != null) Destroy(stage);
        }
    }
}



