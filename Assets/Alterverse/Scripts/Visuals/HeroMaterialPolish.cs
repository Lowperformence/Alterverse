using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Upgrades imported FBX and runtime hero armor, metal, enamel, and tactical cloth materials
    /// to use Alterverse/RuntimeLit (DF_SurfaceLighting) with calibrated PBR metallic/smoothness
    /// and a procedural micro-brushed anodized normal map so heroes never look flat or plastic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroMaterialPolish : MonoBehaviour
    {
        private readonly List<Material> ownedMaterials = new List<Material>();
        private static Texture2D sharedMicroNormal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => sharedMicroNormal = null;

        private static Texture2D GetMicroNormalMap()
        {
            if (sharedMicroNormal != null) return sharedMicroNormal;
            const int size = 128;
            sharedMicroNormal = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "DF_HeroMicroAnodizedNormal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    // Fine anisotropic brushed metal grain + subtle hexagonal micro-facet relief
                    float brush = Mathf.Sin(u * 64f * Mathf.PI * 2f + Mathf.Cos(v * 8f * Mathf.PI * 2f) * 0.35f) * 0.11f;
                    float cross = Mathf.Cos((u * 24f + v * 24f) * Mathf.PI * 2f) * 0.06f;
                    float hexX = Mathf.Sin((u * 16f + v * 9.2f) * Mathf.PI * 2f) * 0.07f;
                    float hexY = Mathf.Cos((v * 16f - u * 9.2f) * Mathf.PI * 2f) * 0.07f;
                    Vector3 n = new Vector3(brush + hexX, cross + hexY, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            sharedMicroNormal.SetPixels(pixels);
            sharedMicroNormal.Apply(false, false);
            return sharedMicroNormal;
        }

        public static void Apply(Transform model, HeroId hero)
        {
            if (model == null || hero == HeroId.VoidKnight || hero == HeroId.Bolt7) return;
            if (model.GetComponent<HeroMaterialPolish>() != null) return;
            Shader litShader = RuntimeShaderLibrary.Lit;
            if (litShader == null) return;

            var polish = model.gameObject.AddComponent<HeroMaterialPolish>();
            Texture2D microNormal = GetMicroNormalMap();
            Color heroAccent = StylizedArtDirection.HeroAccent(hero);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                    continue;
                if (renderer.name == "DF_WeaponEnergyOverlay") continue;

                Material[] slots = renderer.sharedMaterials;
                if (slots == null || slots.Length == 0) continue;
                bool changed = false;

                for (int i = 0; i < slots.Length; i++)
                {
                    Material original = slots[i];
                    if (original == null || original.shader == null) continue;
                    string shaderName = original.shader.name;

                    // Keep custom procedural shaders intact (undersuit weave, skin SSS ornaments, camo, shark skin, unlit VFX).
                    if (shaderName.Contains("HeroUndersuitFabric") ||
                        shaderName.Contains("SkinOrnaments") ||
                        shaderName.Contains("TankWoodlandCamo") ||
                        shaderName.Contains("SharkSkin") ||
                        shaderName.Contains("JacketFabric") ||
                        shaderName.Contains("AnchorMetal") ||
                        shaderName.Contains("VoidArmor") ||
                        shaderName.Contains("WeaponEnergyOverlay") ||
                        shaderName.Contains("Unlit") ||
                        original.renderQueue >= 2450)
                        continue;

                    string matName = original.name.Replace(" (Instance)", "").ToLowerInvariant();
                    if (matName.Contains("skin") || matName.Contains("flesh") || matName.Contains("eye") || matName.Contains("hair"))
                        continue;

                    bool isEnergy = matName.Contains("energy") || matName.Contains("glow") || matName.Contains("lens") ||
                                    matName.Contains("circuit") || matName.Contains("crystal") || matName.Contains("focus") ||
                                    matName.Contains("capacitor") || matName.Contains("light") || matName.Contains("neon") ||
                                    matName.Contains("stripe") || matName.Contains("reactor");

                    bool isMetal = matName.Contains("metal") || matName.Contains("silver") || matName.Contains("steel") ||
                                   matName.Contains("titanium") || matName.Contains("gold") || matName.Contains("bronze") ||
                                   matName.Contains("brass") || matName.Contains("iron") || matName.Contains("blade") ||
                                   matName.Contains("fastener") || matName.Contains("trim") || matName.Contains("buckle");

                    bool isClothOrLeather = matName.Contains("cloth") || matName.Contains("cape") || matName.Contains("cloak") ||
                                            matName.Contains("hood") || matName.Contains("leather") || matName.Contains("strap") ||
                                            matName.Contains("belt") || matName.Contains("coat") || matName.Contains("robe");

                    Material upgraded = new Material(original)
                    {
                        shader = litShader,
                        name = original.name + "_Polished"
                    };

                    Color baseColor = Color.white;
                    if (original.HasProperty("_BaseColor")) baseColor = original.GetColor("_BaseColor");
                    else if (original.HasProperty("_Color")) baseColor = original.GetColor("_Color");

                    // Preserve main albedo texture if present
                    Texture mainTex = null;
                    Vector2 mainScale = Vector2.one, mainOffset = Vector2.zero;
                    if (original.HasProperty("_BaseMap") && original.GetTexture("_BaseMap") != null)
                    {
                        mainTex = original.GetTexture("_BaseMap");
                        mainScale = original.GetTextureScale("_BaseMap");
                        mainOffset = original.GetTextureOffset("_BaseMap");
                    }
                    else if (original.HasProperty("_MainTex") && original.GetTexture("_MainTex") != null)
                    {
                        mainTex = original.GetTexture("_MainTex");
                        mainScale = original.GetTextureScale("_MainTex");
                        mainOffset = original.GetTextureOffset("_MainTex");
                    }
                    if (mainTex != null && mainTex.dimension == TextureDimension.Tex2D && upgraded.HasProperty("_BaseMap"))
                    {
                        upgraded.SetTexture("_BaseMap", mainTex);
                        upgraded.SetTextureScale("_BaseMap", mainScale);
                        upgraded.SetTextureOffset("_BaseMap", mainOffset);
                    }

                    float metallic = original.HasProperty("_Metallic") ? original.GetFloat("_Metallic") : 0.25f;
                    float smoothness = original.HasProperty("_Smoothness") ? original.GetFloat("_Smoothness")
                        : original.HasProperty("_Glossiness") ? original.GetFloat("_Glossiness") : 0.35f;

                    if (isEnergy)
                    {
                        metallic = Mathf.Max(metallic, 0.28f);
                        smoothness = Mathf.Max(smoothness, 0.74f);
                        Color currentEmission = original.HasProperty("_EmissionColor") ? original.GetColor("_EmissionColor") : Color.black;
                        if (currentEmission.maxColorComponent < 0.15f)
                            currentEmission = Color.Lerp(baseColor, heroAccent, 0.35f) * 1.35f;
                        if (upgraded.HasProperty("_EmissionColor"))
                        {
                            upgraded.SetColor("_EmissionColor", currentEmission);
                            upgraded.EnableKeyword("_EMISSION");
                        }
                    }
                    else if (isMetal)
                    {
                        metallic = Mathf.Clamp(Mathf.Max(metallic, 0.82f), 0.72f, 0.94f);
                        smoothness = Mathf.Clamp(Mathf.Max(smoothness, 0.68f), 0.58f, 0.84f);
                        // Ensure dark metals still have enough specular albedo to catch crisp highlights
                        if (baseColor.maxColorComponent < 0.14f)
                            baseColor = Color.Lerp(baseColor, new Color(0.20f, 0.22f, 0.26f, baseColor.a), 0.42f);
                    }
                    else if (isClothOrLeather)
                    {
                        metallic = Mathf.Clamp(Mathf.Max(metallic, 0.14f), 0.08f, 0.28f);
                        smoothness = Mathf.Clamp(Mathf.Max(smoothness, 0.38f), 0.32f, 0.50f);
                        if (baseColor.maxColorComponent < 0.08f)
                            baseColor = Color.Lerp(baseColor, new Color(0.095f, 0.105f, 0.130f, baseColor.a), 0.55f);
                    }
                    else
                    {
                        // Armor plates, ceramic enamel, pauldrons, greaves, helmets, and weapon housings
                        metallic = Mathf.Clamp(Mathf.Max(metallic, 0.62f), 0.48f, 0.86f);
                        smoothness = Mathf.Clamp(Mathf.Max(smoothness, 0.62f), 0.52f, 0.78f);
                        if (baseColor.maxColorComponent < 0.09f)
                            baseColor = Color.Lerp(baseColor, new Color(0.11f, 0.125f, 0.155f, baseColor.a), 0.48f);
                    }

                    if (upgraded.HasProperty("_BaseColor")) upgraded.SetColor("_BaseColor", baseColor);
                    if (upgraded.HasProperty("_Color")) upgraded.SetColor("_Color", baseColor);
                    if (upgraded.HasProperty("_Metallic")) upgraded.SetFloat("_Metallic", metallic);
                    if (upgraded.HasProperty("_Smoothness")) upgraded.SetFloat("_Smoothness", smoothness);

                    // Attach micro-brushed anodized normal relief if the material has no imported 2D normal map
                    Texture existingBump = original.HasProperty("_BumpMap") ? original.GetTexture("_BumpMap") : null;
                    if (existingBump != null && existingBump.dimension == TextureDimension.Tex2D)
                    {
                        upgraded.SetTexture("_BumpMap", existingBump);
                        upgraded.SetTextureScale("_BumpMap", original.GetTextureScale("_BumpMap"));
                        upgraded.SetTextureOffset("_BumpMap", original.GetTextureOffset("_BumpMap"));
                        float bumpScale = original.HasProperty("_BumpScale") ? Mathf.Max(0.25f, original.GetFloat("_BumpScale")) : 0.65f;
                        if (upgraded.HasProperty("_BumpScale")) upgraded.SetFloat("_BumpScale", bumpScale);
                        if (upgraded.HasProperty("_NormalEncoding"))
                            upgraded.SetFloat("_NormalEncoding", original.HasProperty("_NormalEncoding") ? original.GetFloat("_NormalEncoding") : 1f);
                    }
                    else if (!isEnergy && microNormal != null && upgraded.HasProperty("_BumpMap"))
                    {
                        upgraded.SetTexture("_BumpMap", microNormal);
                        upgraded.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
                        if (upgraded.HasProperty("_BumpScale"))
                            upgraded.SetFloat("_BumpScale", isMetal ? 0.18f : isClothOrLeather ? 0.15f : 0.14f);
                        if (upgraded.HasProperty("_NormalEncoding"))
                            upgraded.SetFloat("_NormalEncoding", 0f);
                    }

                    polish.ownedMaterials.Add(upgraded);
                    slots[i] = upgraded;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = slots;
            }
        }

        private void OnDestroy()
        {
            foreach (Material mat in ownedMaterials)
                if (mat != null) Destroy(mat);
            ownedMaterials.Clear();
        }
    }
}
