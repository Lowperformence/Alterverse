using System.Collections.Generic;
using Coffee.UIEffects;
using Coffee.UIExtensions;
using Coffee.UISoftMask;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Alterverse.UI
{
    /// <summary>
    /// Retained rendering for the existing menu layouts. IMGUI still owns input, text editing and
    /// drag capture, so migrating the visuals does not change purchases or mobile touch semantics.
    /// Only the Repaint pass updates the pooled Canvas; every graphic is non-interactive.
    /// </summary>
    internal sealed class FrontEndCanvas : MonoBehaviour
    {
        private enum Kind { Texture, Rounded, Label, Clip, Particles }
        private sealed class Node
        {
            public Kind Kind;
            public RectTransform Rect;
            public RawImage Texture;
            public Image Image;
            public Text Label;
            public UIEffect Effect;
            public ParticleSystem Particles;
            public UIParticle UiParticles;
            public RectMask2D HardMask;
            public SoftMask SoftMask;
        }
        private readonly struct Clip
        {
            public readonly RectTransform Parent;
            public readonly Rect Bounds;
            public Clip(RectTransform parent, Rect bounds) { Parent = parent; Bounds = bounds; }
        }

        internal static FrontEndCanvas Current { get; private set; }
        private readonly List<Node> nodes = new();
        private readonly Stack<Clip> clips = new();
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform root;
        private Image feedback;
        private Sprite fillSprite, outlineSprite;
        private Texture2D particleTexture;
        private Material particleMaterial;
        private Tween revealTween, feedbackTween;
        private int used, lastPage = int.MinValue;
        private bool repaint;
        private const int ParticleBudget = 40;

        private void Awake()
        {
            var surface = new GameObject("DF_MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            surface.transform.SetParent(transform, false);
            root = surface.GetComponent<RectTransform>();
            canvas = surface.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            group = surface.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            canvas.enabled = false;
            Resources.Load<ShaderVariantCollection>("FrontEnd/MenuUiVariants")?.WarmUp();
            // No GraphicRaycaster/EventSystem: the original controls retain mouse, touch and keyboard input.
            var pulse = new GameObject("Action feedback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pulse.transform.SetParent(root, false);
            feedback = pulse.GetComponent<Image>();
            feedback.raycastTarget = false;
            feedback.enabled = false;
        }

        internal void SetVisible(bool visible)
        {
            if (canvas == null) return;
            if (canvas.enabled == visible) return;
            canvas.enabled = visible;
            if (!visible)
            {
                revealTween?.Kill();
                feedbackTween?.Kill();
                feedback.enabled = false;
                lastPage = int.MinValue;
                for (int i = 0; i < nodes.Count; i++)
                    if (nodes[i].Particles != null) nodes[i].Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        internal void Begin(int page)
        {
            Current = this;
            repaint = Event.current != null && Event.current.type == EventType.Repaint;
            if (!repaint) return;
            canvas.enabled = true;
            used = 0;
            clips.Clear();
            clips.Push(new Clip(root, new Rect(0, 0, Screen.width, Screen.height)));
            if (lastPage != page)
            {
                lastPage = page;
                revealTween?.Kill();
                group.alpha = .72f;
                revealTween = DOTween.To(() => group.alpha, value => group.alpha = value, 1f, .20f)
                    .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
            }
        }

        internal void End()
        {
            if (repaint)
            {
                for (int i = used; i < nodes.Count; i++) nodes[i].Rect.gameObject.SetActive(false);
                feedback.transform.SetAsLastSibling();
                clips.Clear();
            }
            repaint = false;
            if (Current == this) Current = null;
        }

        private static Rect ScreenRect(Rect rect)
        {
            Vector2 a = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            Vector2 b = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.yMax));
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private static void Place(RectTransform target, Rect bounds, Rect parent)
        {
            target.anchorMin = target.anchorMax = new Vector2(0, 1);
            target.pivot = new Vector2(0, 1);
            target.anchoredPosition = new Vector2(bounds.x - parent.x, -(bounds.y - parent.y));
            target.sizeDelta = new Vector2(bounds.width, bounds.height);
            target.localScale = Vector3.one;
        }

        private Node Take(Kind kind, Rect bounds)
        {
            Node node;
            if (used < nodes.Count && nodes[used].Kind == kind) node = nodes[used];
            else
            {
                if (used < nodes.Count)
                {
                    // Layout changes can alter the primitive type. Retire just that slot, never grow a second page pool.
                    for (int i = 0; i < nodes.Count; i++)
                        if (i != used && nodes[i].Rect != null && nodes[i].Rect.IsChildOf(nodes[used].Rect))
                            nodes[i].Rect.SetParent(root, false);
                    Destroy(nodes[used].Rect.gameObject);
                    node = Create(kind);
                    nodes[used] = node;
                }
                else { node = Create(kind); nodes.Add(node); }
            }
            used++;
            Clip clip = clips.Peek();
            node.Rect.SetParent(clip.Parent, false);
            node.Rect.SetAsLastSibling();
            node.Rect.gameObject.SetActive(true);
            Place(node.Rect, bounds, clip.Bounds);
            return node;
        }

        private Node Create(Kind kind)
        {
            var go = new GameObject("Menu " + kind, typeof(RectTransform), typeof(CanvasRenderer));
            var node = new Node { Kind = kind, Rect = go.GetComponent<RectTransform>() };
            go.transform.SetParent(root, false);
            switch (kind)
            {
                case Kind.Texture:
                    node.Texture = go.AddComponent<RawImage>(); node.Texture.raycastTarget = false;
                    break;
                case Kind.Rounded:
                    node.Image = go.AddComponent<Image>(); node.Image.raycastTarget = false;
                    node.Image.type = Image.Type.Sliced;
                    node.Effect = go.AddComponent<UIEffect>();
                    break;
                case Kind.Label:
                    node.Label = go.AddComponent<Text>(); node.Label.raycastTarget = false;
                    node.Label.supportRichText = false;
                    break;
                case Kind.Clip:
                    node.Image = go.AddComponent<Image>(); node.Image.raycastTarget = false;
                    node.Image.type = Image.Type.Sliced;
                    node.SoftMask = go.AddComponent<SoftMask>();
                    node.SoftMask.maskingMode = SoftMask.MaskingMode.AntiAliasing;
                    node.SoftMask.showMaskGraphic = false;
                    node.HardMask = go.AddComponent<RectMask2D>();
                    break;
                case Kind.Particles:
                    CreateParticles(node);
                    break;
            }
            return node;
        }

        private void EnsureSprites()
        {
            if (fillSprite != null) return;
            Texture2D fill = FrontEndTheme.RoundedTexture(false);
            Texture2D outline = FrontEndTheme.RoundedTexture(true);
            var border = new Vector4(15, 15, 15, 15);
            fillSprite = Sprite.Create(fill, new Rect(0, 0, fill.width, fill.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            outlineSprite = Sprite.Create(outline, new Rect(0, 0, outline.width, outline.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        }

        internal void Texture(Rect rect, Texture texture, ScaleMode mode, Rect uv)
        {
            if (!repaint || texture == null || rect.width <= 0 || rect.height <= 0) return;
            Rect bounds = ScreenRect(rect);
            float imageAspect = texture.width * uv.width / (texture.height * uv.height);
            float targetAspect = bounds.width / bounds.height;
            if (mode == ScaleMode.ScaleToFit)
            {
                if (targetAspect > imageAspect)
                { float w = bounds.height * imageAspect; bounds.x += (bounds.width - w) * .5f; bounds.width = w; }
                else { float h = bounds.width / imageAspect; bounds.y += (bounds.height - h) * .5f; bounds.height = h; }
            }
            else if (mode == ScaleMode.ScaleAndCrop)
            {
                if (targetAspect > imageAspect)
                { float h = uv.height * imageAspect / targetAspect; uv.y += (uv.height - h) * .5f; uv.height = h; }
                else { float w = uv.width * targetAspect / imageAspect; uv.x += (uv.width - w) * .5f; uv.width = w; }
            }
            Node node = Take(Kind.Texture, bounds);
            node.Texture.texture = texture;
            node.Texture.uvRect = uv;
            node.Texture.color = GUI.color;
        }

        internal void Rounded(Rect rect, Color color, bool outline, bool emphasized)
        {
            if (!repaint || rect.width <= 0 || rect.height <= 0) return;
            EnsureSprites();
            Rect bounds = ScreenRect(rect);
            Node node = Take(Kind.Rounded, bounds);
            node.Image.sprite = outline ? outlineSprite : fillSprite;
            node.Image.color = color * GUI.color;
            node.Image.pixelsPerUnitMultiplier = rect.width / Mathf.Max(.001f, bounds.width);
            // A restrained vertical light falloff gives glass panels depth without obscuring menu artwork.
            node.Effect.gradationMode = outline ? GradationMode.None : GradationMode.Vertical;
            node.Effect.gradationColorFilter = GradationColorFilter.Multiply;
            node.Effect.gradationColor1 = new Color(1.12f, 1.12f, 1.12f, 1);
            node.Effect.gradationColor2 = new Color(.82f, .88f, .94f, 1);
            node.Effect.transitionFilter = emphasized ? TransitionFilter.Shiny : TransitionFilter.None;
            node.Effect.transitionWidth = .12f;
            node.Effect.transitionSoftness = .7f;
            node.Effect.transitionColor = new Color(1f, .86f, .64f, .25f);
            if (emphasized) node.Effect.transitionRate = Mathf.Repeat(Time.unscaledTime * .16f + rect.y * .001f, 1f);
        }

        internal void Label(Rect rect, string value, GUIStyle style)
        {
            if (!repaint || rect.width <= 0 || rect.height <= 0) return;
            Rect bounds = ScreenRect(rect);
            Node node = Take(Kind.Label, bounds);
            Text text = node.Label;
            text.text = value ?? string.Empty;
            text.font = style.font != null ? style.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(style.fontSize * bounds.height / rect.height));
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Min(text.fontSize, Mathf.Max(1, Mathf.RoundToInt(12 * bounds.height / rect.height)));
            text.fontStyle = style.fontStyle;
            text.alignment = style.alignment;
            text.horizontalOverflow = style.wordWrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = style.normal.textColor * GUI.color;
        }

        internal void PushClip(Rect rect, bool rounded)
        {
            if (!repaint) return;
            EnsureSprites();
            Rect bounds = ScreenRect(rect);
            Node node = Take(Kind.Clip, bounds);
            node.SoftMask.enabled = rounded;
            node.HardMask.enabled = !rounded;
            node.Image.enabled = rounded;
            node.Image.sprite = fillSprite;
            node.Image.color = Color.white;
            node.Image.pixelsPerUnitMultiplier = rect.width / Mathf.Max(.001f, bounds.width);
            clips.Push(new Clip(node.Rect, bounds));
        }

        internal void PopClip()
        {
            if (repaint && clips.Count > 1) clips.Pop();
        }

        internal void Pulse(Rect rect)
        {
            // Called during the input pass, outside rendering. The pulse never changes the hit rectangle.
            EnsureSprites();
            Rect bounds = ScreenRect(rect);
            Place(feedback.rectTransform, bounds, new Rect(0, 0, Screen.width, Screen.height));
            feedback.sprite = fillSprite;
            feedback.type = Image.Type.Sliced;
            feedback.pixelsPerUnitMultiplier = rect.width / Mathf.Max(.001f, bounds.width);
            feedback.enabled = true;
            feedback.color = new Color(1f, .88f, .68f, .19f);
            feedbackTween?.Kill();
            float alpha = .19f;
            feedbackTween = DOTween.To(() => alpha, value =>
            {
                alpha = value;
                feedback.color = new Color(1f, .88f, .68f, value);
            }, 0f, .26f).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(this)
                .OnComplete(() => feedback.enabled = false);
        }

        internal void Particles(Rect rect, Color tint)
        {
            if (!repaint || rect.width <= 0 || rect.height <= 0) return;
            Node node = Take(Kind.Particles, ScreenRect(rect));
            var main = node.Particles.main;
            main.startColor = new Color(tint.r, tint.g, tint.b, .25f);
            float scale = node.Rect.sizeDelta.x / rect.width;
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f * scale, 3.4f * scale);
            main.startSpeed = 0;
            node.Particles.transform.localPosition = new Vector3(node.Rect.sizeDelta.x * .5f, -node.Rect.sizeDelta.y * .5f, 0);
            var velocity = node.Particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2f * scale, 2f * scale);
            velocity.y = new ParticleSystem.MinMaxCurve(3f * scale, 8f * scale);
            var shape = node.Particles.shape;
            shape.scale = new Vector3(node.Rect.sizeDelta.x * .8f, node.Rect.sizeDelta.y * .75f, 0);
            if (!node.Particles.isPlaying) node.Particles.Play();
        }

        private void CreateParticles(Node node)
        {
            node.UiParticles = node.Rect.gameObject.AddComponent<UIParticle>();
            node.UiParticles.raycastTarget = false;
            node.UiParticles.autoScalingMode = UIParticle.AutoScalingMode.None;
            node.UiParticles.scale = 1;
            var go = new GameObject("Chronolight motes", typeof(ParticleSystem));
            go.transform.SetParent(node.Rect, false);
            // Centre local simulation in the Canvas rectangle, while keeping geometry and input stationary.
            node.Particles = go.GetComponent<ParticleSystem>();
            node.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            node.Particles.useAutoRandomSeed = false;
            node.Particles.randomSeed = 47021;
            var main = node.Particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.maxParticles = ParticleBudget;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var emission = node.Particles.emission; emission.rateOverTime = 6;
            var shape = node.Particles.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.enabled = true;
            var alpha = node.Particles.colorOverLifetime;
            alpha.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            alpha.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial();
            node.UiParticles.RefreshParticles();
        }

        private Material ParticleMaterial()
        {
            if (particleMaterial != null) return particleMaterial;
            var source = Resources.Load<Material>("FrontEnd/MenuParticle");
            particleMaterial = source != null ? new Material(source) : new Material(Shader.Find("UI/Default"));
            particleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "DF_MenuMote", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float r = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 2));
            }
            particleTexture.SetPixels(pixels); particleTexture.Apply(false, true);
            particleMaterial.mainTexture = particleTexture;
            return particleMaterial;
        }

        private void OnDisable() { SetVisible(false); if (Current == this) Current = null; }
        private void OnDestroy()
        {
            revealTween?.Kill(); feedbackTween?.Kill();
            if (Current == this) Current = null;
            if (fillSprite != null) Destroy(fillSprite);
            if (outlineSprite != null) Destroy(outlineSprite);
            if (particleTexture != null) Destroy(particleTexture);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (root != null) Destroy(root.gameObject);
        }
    }
}
