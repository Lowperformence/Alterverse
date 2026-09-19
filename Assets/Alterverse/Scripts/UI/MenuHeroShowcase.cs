using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One isolated preview of the actual hero model; never a combat actor.</summary>
    public sealed class MenuHeroShowcase : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private GameObject stage, actor;
        private Camera previewCamera;
        private RenderTexture texture;
        private HeroId selected;
        private bool hasSelection,selectedSkin,selectedWeapon;
        private float selectedAt;
        private Transform orbitLeft, orbitRight, wingLeft, wingRight;
        public Texture Image => texture;

        public void Present(HeroId hero, bool visible, bool? skinOverride=null,bool? weaponOverride=null)
        {
            if (!visible) { if (stage != null) stage.SetActive(false); return; }
            if (stage == null) CreateStage();
            stage.SetActive(true);
            bool skin=skinOverride??Alterverse.Shop.UniversalSkin.Equipped;
            bool weapon=weaponOverride??Alterverse.Shop.WeaponSkinInventory.Equipped(hero);
            if (hasSelection && selected == hero && selectedSkin==skin && selectedWeapon==weapon) return;
            selectedSkin=skin;selectedWeapon=weapon;
            if(previewCamera!=null)previewCamera.orthographicSize=weapon?1.82f:1.65f;
            if (actor != null) { actor.SetActive(false); Destroy(actor); }
            selected = hero; hasSelection = true; selectedAt = Time.unscaledTime;
            actor = new GameObject("DF_MenuHero");
            actor.transform.SetParent(stage.transform, false);
            actor.AddComponent<HeroIdentity>().Configure(hero);
            var preview=actor.AddComponent<Alterverse.Shop.SkinPreviewSelection>();preview.Voidfall=skin;preview.WeaponOverdrive=weapon;
            actor.AddComponent<StylizedHeroVisual>().Build();
            Transform visual=actor.transform.Find("DF_VisualRoot");
            orbitLeft=visual.Find("DF_OrbitL"); orbitRight=visual.Find("DF_OrbitR");
            wingLeft=visual.Find("DF_WingL"); wingRight=visual.Find("DF_WingR");
            // No health, colliders, input, abilities, team membership or game-world registration.
            foreach (Transform part in actor.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = PreviewLayer;
            foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
        private void CreateStage()
        {
            stage = new GameObject("DF_MenuPortraitStage");
            stage.transform.SetParent(transform, false);
            stage.transform.position = new Vector3(12000,12000,12000);
            texture = new RenderTexture(640,720,24,RenderTextureFormat.ARGB32)
            { name = "DF_HeroPortrait", antiAliasing = 1, useMipMap = false };
            texture.Create();
            var cameraObject = new GameObject("DF_PortraitCamera"); cameraObject.transform.SetParent(stage.transform,false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.orthographic = true; previewCamera.orthographicSize = 1.65f;
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 12;
            previewCamera.allowHDR = false; previewCamera.allowMSAA = false;
            previewCamera.useOcclusionCulling = false; previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.targetTexture = texture; previewCamera.depth = -20;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.renderShadows = false;
            data.volumeLayerMask = 0;
            cameraObject.transform.localPosition = new Vector3(0,.65f,5);
            cameraObject.transform.LookAt(stage.transform.position + Vector3.up*.08f);
            AddLight("Key",new Vector3(-2,3,3),new Color(.91f,.95f,1),3.5f);
            AddLight("Fill",new Vector3(2,1,2),new Color(.51f,.72f,.78f),1.8f);
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
            float facing=selected==HeroId.Nyra?-42: selected==HeroId.Kaelon?-24:-16;
            actor.transform.localRotation = Quaternion.Euler(0,Mathf.Lerp(facing-16,facing,settle)+Mathf.Sin(t*.55f)*4,0);
            actor.transform.localPosition = new Vector3(0,Mathf.Lerp(-.10f,0,settle)+Mathf.Sin(t*1.7f)*.015f,0);
            if(orbitLeft!=null) orbitLeft.localPosition=new Vector3(-.72f,.46f+Mathf.Sin(t*1.4f)*.06f,.03f);
            if(orbitRight!=null) orbitRight.localPosition=new Vector3(.72f,.28f-Mathf.Sin(t*1.4f)*.06f,.03f);
            if(wingLeft!=null) wingLeft.localRotation=Quaternion.Euler(0,-18,-18-Mathf.Sin(t*2)*3);
            if(wingRight!=null) wingRight.localRotation=Quaternion.Euler(0,18,18+Mathf.Sin(t*2)*3);
        }
        private void OnDisable() { if (stage != null) stage.SetActive(false); }
        private void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (stage != null) Destroy(stage);
        }
    }
}
