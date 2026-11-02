using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// High-resolution 3D hero mini-portrait card composited over each champion's Origin World backdrop.
    /// Renders an authentic 3D bust portrait of every HeroId once on demand and caches the Texture2D.
    /// </summary>
    public static class StylizedHeroCard
    {
        private const int PortraitWidth = 256;
        private const int PortraitHeight = 320;
        private const int PreviewLayer = 31;
        private static readonly Vector3 StageOrigin = new Vector3(12600f, 12000f, 12000f);

        private static readonly Dictionary<HeroId, Texture2D> PortraitCache = new Dictionary<HeroId, Texture2D>();
        private static readonly Dictionary<HeroId, Texture2D> BackdropCache = new Dictionary<HeroId, Texture2D>();
        private static Shader portraitShader;

        public static void Draw(Rect rect, HeroId hero, Texture2D pixel, float time)
        {
            if (rect.width <= 4f || rect.height <= 4f) return;
            pixel ??= Texture2D.whiteTexture;

            Color accent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(hero);
            Color deepBg = new Color(0.035f, 0.055f, 0.095f, 0.98f);
            Color before = GUI.color;

            GUI.BeginGroup(rect);
            try
            {
                Rect local = new Rect(0f, 0f, rect.width, rect.height);

                // Base dark glass fill
                DrawRect(local, pixel, deepBg);

                // Origin World backdrop texture
                Texture2D backdrop = GetOriginBackdrop(hero);
                if (backdrop != null)
                {
                    GUI.color = new Color(0.88f, 0.92f, 1f, 0.92f * before.a);
                    DrawCoverTexture(local, backdrop);
                }

                // Subtle vignette and top-to-bottom contrast gradient for readability
                DrawRect(new Rect(0f, 0f, local.width, local.height * 0.28f), pixel,
                    new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.18f));
                DrawRect(new Rect(0f, local.height * 0.62f, local.width, local.height * 0.38f), pixel,
                    new Color(0.015f, 0.025f, 0.045f, 0.52f));

                // Cached 3D hero bust portrait
                Texture2D portrait = GetOrRenderPortrait(hero);
                if (portrait != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, before.a);
                    DrawPortraitTexture(local, portrait);
                }

                // Crisp orbital glass frame + bottom hero accent bar
                Color frameColor = Color.Lerp(accent, new Color(0.75f, 0.86f, 0.98f, 1f), 0.45f);
                frameColor.a = 0.42f;
                DrawBorder(local, pixel, frameColor, 1f);

                float barH = Mathf.Clamp(Mathf.Round(local.height * 0.045f), 2f, 4f);
                DrawRect(new Rect(0f, local.height - barH, local.width, barH), pixel,
                    new Color(accent.r, accent.g, accent.b, 0.92f));
            }
            finally
            {
                GUI.color = before;
                GUI.EndGroup();
            }
        }

        private static Texture2D GetOriginBackdrop(HeroId hero)
        {
            if (BackdropCache.TryGetValue(hero, out Texture2D cached) && cached != null)
                return cached;

            Texture2D loaded = Resources.Load<Texture2D>("UI/HeroOriginBackdrops/" + hero);
            BackdropCache[hero] = loaded;
            return loaded;
        }

        private static Texture2D GetOrRenderPortrait(HeroId hero)
        {
            if (PortraitCache.TryGetValue(hero, out Texture2D cached) && cached != null)
                return cached;

            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return null;

            Texture2D rendered = RenderHeroBustPortrait(hero);
            if (rendered != null)
                PortraitCache[hero] = rendered;
            return rendered;
        }

        private static Texture2D RenderHeroBustPortrait(HeroId hero)
        {
            GameObject stage = null;
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(PortraitWidth, PortraitHeight, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 1;

                stage = new GameObject("DF_HeroMiniPortraitStage")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                stage.transform.position = StageOrigin;

                // Build hero model on isolated preview layer
                GameObject heroRoot = new GameObject("PortraitHero")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                heroRoot.transform.SetParent(stage.transform, false);
                heroRoot.transform.localPosition = Vector3.zero;
                heroRoot.transform.localRotation = Quaternion.Euler(0f, GetPortraitYaw(hero), 0f);

                HeroIdentity identity = heroRoot.AddComponent<HeroIdentity>();
                identity.Configure(hero);
                Alterverse.Visuals.StylizedHeroVisual visual = heroRoot.AddComponent<Alterverse.Visuals.StylizedHeroVisual>();
                visual.Build();

                ApplyPortraitMaterialsAndLayer(heroRoot);

                // Compute head & upper-body framing bounds
                Bounds bustBounds = CalculateBustBounds(heroRoot, hero);

                // 3-point studio portrait lighting
                Color accent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(hero);
                CreateLight(stage.transform, "KeyLight", new Vector3(-1.8f, 2.4f, -2.6f),
                    new Vector3(24f, 34f, 0f), new Color(1f, 0.98f, 0.95f), 2.35f, 14f);
                CreateLight(stage.transform, "FillLight", new Vector3(2.1f, 1.3f, -2.2f),
                    new Vector3(12f, -38f, 0f), new Color(0.68f, 0.82f, 1f), 1.45f, 14f);
                CreateLight(stage.transform, "RimLight", new Vector3(0.2f, 2.5f, 2.2f),
                    new Vector3(32f, 180f, 0f), Color.Lerp(accent, Color.white, 0.35f), 2.1f, 14f);

                // Portrait camera
                GameObject camObj = new GameObject("PortraitCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                camObj.transform.SetParent(stage.transform, false);

                Camera cam = camObj.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << PreviewLayer;
                cam.orthographic = true;

                float aspect = PortraitWidth / (float)PortraitHeight;
                float halfHeight = Mathf.Max(0.26f, bustBounds.extents.y * 1.06f);
                float halfWidth = Mathf.Max(0.22f, bustBounds.extents.x * 1.08f / Mathf.Max(0.5f, aspect));
                cam.orthographicSize = Mathf.Max(halfHeight, halfWidth);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 18f;

                Vector3 lookTarget = bustBounds.center + new Vector3(0f, bustBounds.extents.y * 0.04f, 0f);
                Vector3 camPos = lookTarget + new Vector3(0f, bustBounds.extents.y * 0.10f, -3.5f);
                cam.transform.position = camPos;
                cam.transform.LookAt(lookTarget, Vector3.up);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D result = new Texture2D(PortraitWidth, PortraitHeight, TextureFormat.RGBA32, false)
                {
                    name = "DF_HeroMiniPortrait_" + hero,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                result.ReadPixels(new Rect(0, 0, PortraitWidth, PortraitHeight), 0, 0, false);
                result.Apply(false, false);
                cam.targetTexture = null;
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (stage != null) Object.DestroyImmediate(stage);
            }
        }

        private static float GetPortraitYaw(HeroId hero)
        {
            return hero switch
            {
                HeroId.Veyr => 162f,
                HeroId.Kaelon => 166f,
                HeroId.Nyra => 196f,
                HeroId.Elyra => 164f,
                HeroId.Mira => 194f,
                HeroId.Rook => 165f,
                HeroId.Traveler => 162f,
                HeroId.Nerok => 166f,
                HeroId.Timmy => 168f,
                HeroId.VoidKnight => 162f,
                HeroId.Bolt7 => 164f,
                _ => 165f
            };
        }

        private static Bounds CalculateBustBounds(GameObject heroRoot, HeroId hero)
        {
            Renderer[] renderers = heroRoot.GetComponentsInChildren<Renderer>(true);
            Bounds full = new Bounds(heroRoot.transform.position + Vector3.up * 1.2f, new Vector3(0.7f, 0.8f, 0.7f));
            bool hasAny = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled) continue;
                string n = r.gameObject.name;
                if (n.Contains("Weapon") || n.Contains("Sword") || n.Contains("Anchor") || n.Contains("Bow"))
                    continue;
                if (!hasAny)
                {
                    full = r.bounds;
                    hasAny = true;
                }
                else
                {
                    full.Encapsulate(r.bounds);
                }
            }

            // Focus on the upper 46% of the hero (head, helmet/ears, shoulders, upper chest)
            float totalHeight = Mathf.Max(0.6f, full.size.y);
            float bustFraction = hero == HeroId.Timmy ? 0.58f : hero == HeroId.Bolt7 ? 0.54f : 0.45f;
            float bustHeight = totalHeight * bustFraction;
            float topY = full.max.y + totalHeight * 0.03f;
            float centerY = topY - bustHeight * 0.5f;
            float bustWidth = Mathf.Clamp(bustHeight * 0.86f, 0.44f, full.size.x * 0.78f);

            return new Bounds(
                new Vector3(full.center.x, centerY, full.center.z),
                new Vector3(bustWidth, bustHeight, full.size.z));
        }

        private static void ApplyPortraitMaterialsAndLayer(GameObject root)
        {
            portraitShader ??= Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                    transforms[i].gameObject.layer = PreviewLayer;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static void CreateLight(Transform parent, string name, Vector3 localPos, Vector3 localEuler, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << PreviewLayer;
        }

        private static void DrawCoverTexture(Rect rect, Texture2D texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0) return;
            float rectAspect = rect.width / Mathf.Max(1f, rect.height);
            float texAspect = texture.width / (float)texture.height;
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            if (rectAspect > texAspect)
            {
                float h = texAspect / rectAspect;
                uv = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
            else
            {
                float w = rectAspect / texAspect;
                uv = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }

        private static void DrawPortraitTexture(Rect rect, Texture2D texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0) return;
            float rectAspect = rect.width / Mathf.Max(1f, rect.height);
            float texAspect = texture.width / (float)texture.height;
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            if (rectAspect > texAspect)
            {
                float h = texAspect / rectAspect;
                // Anchor slightly toward top so head and shoulders stay centered in wide cards
                uv = new Rect(0f, Mathf.Clamp01(1f - h - 0.06f), 1f, h);
            }
            else
            {
                float w = rectAspect / texAspect;
                uv = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }

        private static void DrawRect(Rect r, Texture2D pixel, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c * new Color(1f, 1f, 1f, prev.a);
            GUI.DrawTexture(r, pixel);
            GUI.color = prev;
        }

        private static void DrawBorder(Rect r, Texture2D pixel, Color c, float t)
        {
            DrawRect(new Rect(r.x, r.y, r.width, t), pixel, c);
            DrawRect(new Rect(r.x, r.yMax - t, r.width, t), pixel, c);
            DrawRect(new Rect(r.x, r.y, t, r.height), pixel, c);
            DrawRect(new Rect(r.xMax - t, r.y, t, r.height), pixel, c);
        }
    }
}

