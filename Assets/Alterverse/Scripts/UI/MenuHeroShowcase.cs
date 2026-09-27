using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One isolated preview of the actual hero model; never a combat actor.</summary>
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
        private float selectedAt;
        private bool loadoutDirty=true,cachedWeapon;
        private int cachedWeaponVariant=-1,selectedWeaponVariant=-1;
        private HeroId cachedHero;
        private Alterverse.Shop.UniversalHeroCosmetic cachedCosmetic;
        private HeroOriginBackdrop originBackdrop;
        private SpriteRenderer originWorld;
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
            GUI.color=new Color(.010f,.014f,.024f,1f);
            GUI.DrawTexture(local,Texture2D.whiteTexture,ScaleMode.StretchToFill,false);
            GUI.color=oldColor;

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
                cachedWeaponVariant=Alterverse.Shop.WeaponSkinInventory.EquippedVariant(hero);
                cachedWeapon=cachedWeaponVariant>=0;
                loadoutDirty=false;
            }
            Alterverse.Shop.UniversalHeroCosmetic cosmetic=cosmeticOverride ??
                (skinOverride.HasValue
                    ? (skinOverride.Value?Alterverse.Shop.UniversalHeroCosmetic.Voidfall:Alterverse.Shop.UniversalHeroCosmetic.Original)
                    : cachedCosmetic);
            bool weapon=weaponOverride??cachedWeapon;
            int weaponVariant=weaponVariantOverride??(weapon?(cachedWeaponVariant>=0?cachedWeaponVariant:0):-1);
            weapon=weapon&&weaponVariant>=0;

            if (hasSelection && selected == hero && selectedCosmetic==cosmetic && selectedWeapon==weapon && selectedWeaponVariant==weaponVariant) return;
            selectedCosmetic=cosmetic;selectedWeapon=weapon;selectedWeaponVariant=weaponVariant;
            if(previewCamera!=null)previewCamera.orthographicSize=weapon?1.82f:
                cosmetic==Alterverse.Shop.UniversalHeroCosmetic.BetaSupporterWings?2.08f:
                cosmetic==Alterverse.Shop.UniversalHeroCosmetic.PlagueWings?2.04f:1.65f;
            if (actor != null) { actor.SetActive(false); Destroy(actor); }
            selected = hero; hasSelection = true; selectedAt = Time.unscaledTime;
            UpdateOriginWorld(hero);
            actor = new GameObject("DF_MenuHero");
            actor.transform.SetParent(stage.transform, false);
            actor.AddComponent<HeroIdentity>().Configure(hero);
            var preview=actor.AddComponent<Alterverse.Shop.SkinPreviewSelection>();
            preview.UniversalCosmetic=cosmetic;
            preview.WeaponOverdrive=weapon;
            preview.WeaponSkinVariant=weaponVariant;
            actor.AddComponent<StylizedHeroVisual>().Build();
            Transform visual=actor.transform.Find("DF_VisualRoot");
            orbitLeft=visual.Find("DF_OrbitL"); orbitRight=visual.Find("DF_OrbitR");
            wingLeft=visual.Find("DF_WingL"); wingRight=visual.Find("DF_WingR");
            float facing=hero==HeroId.Nyra?-24:hero==HeroId.Kaelon?-24:-16;
            actor.transform.localRotation=Quaternion.Euler(0,facing,0);
            FitPreviewBounds();
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
        }
        private void UpdateOriginWorld(HeroId hero)
        {
            if(originWorld==null)return;
            originBackdrop??=new HeroOriginBackdrop();
            originWorld.sprite=originBackdrop.SpriteFor(hero);
        }
        private void FitPreviewBounds()
        {
            Renderer[] renderers=actor.GetComponentsInChildren<Renderer>();
            bool found=false;Bounds bounds=default;
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled)continue;
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
            }
            if(!found)return;
            Vector3 forward=previewCamera.transform.forward;
            previewCamera.transform.position=bounds.center-forward*5f;
            Vector3 extent=bounds.extents;
            Vector3 right=previewCamera.transform.right,up=previewCamera.transform.up;
            float horizontal=Mathf.Abs(right.x)*extent.x+Mathf.Abs(right.y)*extent.y+Mathf.Abs(right.z)*extent.z;
            float vertical=Mathf.Abs(up.x)*extent.x+Mathf.Abs(up.y)*extent.y+Mathf.Abs(up.z)*extent.z;
            // Slightly tighter framing than before, while preserving the complete silhouette.
            previewCamera.orthographicSize=Mathf.Max(1.40f,Mathf.Max(vertical,horizontal/(PreviewWidth/(float)PreviewHeight))*1.08f);
        }
        private void CreateStage()
        {
            stage = new GameObject("DF_MenuPortraitStage");
            stage.transform.SetParent(transform, false);
            stage.transform.position = new Vector3(12000,12000,12000);

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
            cameraObject.transform.localPosition = new Vector3(0,.65f,5);
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
        private void LateUpdate()
        {
            if (actor == null || stage == null || !stage.activeSelf) return;
            float t = Time.unscaledTime - selectedAt;
            float settle = 1-Mathf.Exp(-t*7);
            float facing=selected==HeroId.Nyra?-24: selected==HeroId.Kaelon?-24:-16;
            actor.transform.localRotation = Quaternion.Euler(0,Mathf.Lerp(facing-16,facing,settle)+Mathf.Sin(t*.55f)*4,0);
            actor.transform.localPosition = new Vector3(0,Mathf.Lerp(-.10f,0,settle)+Mathf.Sin(t*1.7f)*.015f,0);
            if(orbitLeft!=null) orbitLeft.localPosition=new Vector3(-.58f,.28f+Mathf.Sin(t*1.4f)*.06f,.03f);
            if(orbitRight!=null) orbitRight.localPosition=new Vector3(.58f,.28f-Mathf.Sin(t*1.4f)*.06f,.03f);
            if(wingLeft!=null) wingLeft.localRotation=Quaternion.Euler(0,-18,-18-Mathf.Sin(t*2)*3);
            if(wingRight!=null) wingRight.localRotation=Quaternion.Euler(0,18,18+Mathf.Sin(t*2)*3);
        }
        private void OnDisable() { Alterverse.Profile.ProfileTransaction.Committed-=InvalidateLoadout;Alterverse.Shop.PaidShardCommerce.EntitlementsChanged-=InvalidateLoadout; if (stage != null) stage.SetActive(false); }
        private void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
            originBackdrop?.Dispose();
            originBackdrop=null;
            if (stage != null) Destroy(stage);
        }
    }
}



