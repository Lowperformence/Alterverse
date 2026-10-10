using System;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Shared frontend surfaces with cached rounded masks for clear mobile controls.</summary>
    internal sealed class FrontEndTheme
    {
        public static readonly Color Background = new(.020f, .028f, .041f);
        // Dark glass and champagne highlights leave the orbital artwork visible.
        public static readonly Color Surface = new(.025f, .040f, .062f, .94f);
        public static readonly Color Raised = new(.060f, .105f, .145f, .98f);
        public static readonly Color Text = new(.96f, .975f, .99f);
        public static readonly Color Muted = new(.63f, .71f, .79f);
        public static readonly Color Accent = new(.91f, .73f, .48f);
        public static readonly Color Zero = new(.60f, .39f, .82f);
        public static readonly Color Positive = new(.37f, .86f, .73f);
        public static readonly Color Warning = new(.96f, .70f, .39f);

        // Thin steel frames and warm selected edges match the orbital artwork.
        private static readonly Color Outline = new(.30f, .43f, .57f, .76f);
        private static readonly Color OutlineSoft = new(.23f, .34f, .46f, .58f);
        private static readonly Color OutlineSelected = new(1f, .84f, .60f, 1f);

        private const int RoundedTextureSize = 48;
        private const float CornerRadius = 10f;
        private const float OutlineThickness = 1.2f;
        private const int SliceBorder = 15;
        private static GUIStyle roundedFillStyle, roundedOutlineStyle;
        private static Texture2D fieldBackground;

        public readonly GUIStyle Title, Heading, Body, Small, Button, Field;
        private readonly GUIStyle centered, badge, monogram;
        private readonly GUIContent measuredText = new GUIContent();
        public void FitLabel(Rect rect, string text, GUIStyle style, bool muted = false)
        {
            int size = style.fontSize;
            bool wrap = style.wordWrap;
            TextClipping clip = style.clipping;
            style.wordWrap = false;
            style.clipping = TextClipping.Clip;
            measuredText.text = text;
            float width = style.CalcSize(measuredText).x;
            if (width > rect.width)
                style.fontSize = Mathf.Max(14, Mathf.FloorToInt(size * rect.width / width));
            Label(rect, text, style, muted);
            style.fontSize = size;
            style.wordWrap = wrap;
            style.clipping = clip;
        }

        public FrontEndTheme()
        {
            EnsureRoundedStyles();
            Title = Label(42, FontStyle.Bold);
            Title.font = FrontEndTypography.Brand;
            Heading = Label(26, FontStyle.Bold);
            Heading.font = FrontEndTypography.Brand;
            Body = Label(19, FontStyle.Normal);
            Small = Label(16, FontStyle.Normal);
            Button = Label(19, FontStyle.Bold);
            Button.alignment = TextAnchor.MiddleCenter;
            // Menu actions are always single-line controls. Their authored rectangle owns the
            // layout; labels are reduced slightly when needed instead of letting a button expand
            // into the neighbouring control.
            Button.wordWrap = false;
            Button.clipping = TextClipping.Clip;
            centered = Label(16, FontStyle.Normal);
            centered.alignment = TextAnchor.MiddleCenter;
            badge = Label(14, FontStyle.Bold);
            badge.alignment = TextAnchor.MiddleCenter;
            badge.wordWrap = false;
            monogram = Label(22, FontStyle.Bold);
            monogram.alignment = TextAnchor.MiddleCenter;

            Field = new GUIStyle(GUI.skin.textField)
            {
                font = FrontEndTypography.Body,
                fontSize = 23,
                padding = new RectOffset(14, 14, 4, 4),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                richText = false,
                border = new RectOffset(SliceBorder, SliceBorder, SliceBorder, SliceBorder)
            };
            // TextField draws its own background. Bake the dark tint into its texture so
            // the white mask used by panels cannot hide the light text, including on focus.
            foreach (GUIStyleState state in new[]
            {
                Field.normal, Field.hover, Field.active, Field.focused,
                Field.onNormal, Field.onHover, Field.onActive, Field.onFocused
            })
            {
                state.textColor = Text;
                state.background = fieldBackground;
#if UNITY_EDITOR
                // Do not inherit a bright high-DPI background from the editor's skin.
                state.scaledBackgrounds = Array.Empty<Texture2D>();
#endif
            }
        }

        private static void EnsureRoundedStyles()
        {
            if (roundedFillStyle != null && roundedOutlineStyle != null && fieldBackground != null) return;

            Texture2D fill = BuildRoundedMask(false, Color.white);
            Texture2D outline = BuildRoundedMask(true, Color.white);
            fieldBackground = BuildRoundedMask(false, Raised);
            fieldBackground.name = "DF_UI_FieldBackground";
            var border = new RectOffset(SliceBorder, SliceBorder, SliceBorder, SliceBorder);

            roundedFillStyle = new GUIStyle { border = border };
            roundedFillStyle.normal.background = fill;
            roundedOutlineStyle = new GUIStyle { border = border };
            roundedOutlineStyle.normal.background = outline;
        }

        private static Texture2D BuildRoundedMask(bool outline, Color tint)
        {
            var texture = new Texture2D(RoundedTextureSize, RoundedTextureSize, TextureFormat.RGBA32, false)
            {
                name = outline ? "DF_UI_RoundedOutline" : "DF_UI_RoundedFill",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[RoundedTextureSize * RoundedTextureSize];
            Color32 tintBytes = tint;
            float half = RoundedTextureSize * .5f;

            for (int y = 0; y < RoundedTextureSize; y++)
            for (int x = 0; x < RoundedTextureSize; x++)
            {
                float px = x + .5f - half;
                float py = y + .5f - half;
                float outer = RoundedSignedDistance(px, py, half - .75f, CornerRadius);
                float outerAlpha = Mathf.Clamp01(.85f - outer);
                float alpha = outerAlpha;

                if (outline)
                {
                    float innerHalf = half - .75f - OutlineThickness;
                    float innerRadius = Mathf.Max(1f, CornerRadius - OutlineThickness);
                    float inner = RoundedSignedDistance(px, py, innerHalf, innerRadius);
                    float innerAlpha = Mathf.Clamp01(.85f - inner);
                    alpha = outerAlpha * (1f - innerAlpha);
                }

                byte a = (byte)Mathf.RoundToInt(alpha * tint.a * 255f);
                pixels[y * RoundedTextureSize + x] = new Color32(tintBytes.r, tintBytes.g, tintBytes.b, a);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float RoundedSignedDistance(float x, float y, float half, float radius)
        {
            float qx = Mathf.Abs(x) - (half - radius);
            float qy = Mathf.Abs(y) - (half - radius);
            float ox = Mathf.Max(qx, 0);
            float oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
        }

        private static GUIStyle Label(int size, FontStyle weight)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = weight == FontStyle.Bold ? FrontEndTypography.Display : FrontEndTypography.Body,
                fontSize = size,
                fontStyle = weight,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                clipping = TextClipping.Clip,
                richText = false
            };
            style.normal.textColor = Text;
            return style;
        }

        public static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color * before;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }

        private static bool CanPaintRounded()
        {
            // GUIStyle.Draw throws when called during Layout/Mouse/etc. OnGUI runs several times per frame,
            // so rounded artwork must only be emitted in the Repaint pass. Button hit-testing still happens
            // normally because GUI.Button below remains active on every IMGUI event.
            return Event.current != null && Event.current.type == EventType.Repaint;
        }

        private static void RoundedFill(Rect rect, Color color)
        {
            if (!CanPaintRounded()) return;
            EnsureRoundedStyles();
            Color before = GUI.color;
            GUI.color = color * before;
            roundedFillStyle.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = before;
        }

        private static void RoundedOutline(Rect rect, Color color)
        {
            if (!CanPaintRounded()) return;
            EnsureRoundedStyles();
            Color before = GUI.color;
            GUI.color = color * before;
            roundedOutlineStyle.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = before;
        }

        public void Frame(Rect rect,bool emphasized=false)
        {
            RoundedOutline(rect,emphasized?OutlineSelected:OutlineSoft);
        }

        public void Panel(Rect rect)
        {
            RoundedFill(new Rect(rect.x, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, .18f));
            RoundedFill(rect, Surface);
            RoundedOutline(rect, OutlineSoft);
        }

        public void HudPanel(Rect rect)
        {
            // One fill at 40% opacity: a stacked shadow would make the backdrop less transparent.
            RoundedFill(rect, new Color(Surface.r, Surface.g, Surface.b, .40f));
            RoundedOutline(rect, OutlineSoft);
        }

        public void StatusBadge(Rect rect, string text, Color tint)
        {
            RoundedFill(rect, new Color(tint.r, tint.g, tint.b, .12f));
            RoundedOutline(rect, new Color(tint.r, tint.g, tint.b, .36f));
            Color before = GUI.color;
            GUI.color *= tint;
            FitLabel(new Rect(rect.x + 6, rect.y, rect.width - 12, rect.height), text, badge);
            GUI.color = before;
        }

        public void IdentityTile(Rect rect, string name, bool highlighted = false)
        {
            RoundedFill(rect, highlighted ? Raised : new Color(.10f, .16f, .22f, 1f));
            RoundedOutline(rect, highlighted ? Positive : OutlineSoft);
            string initial = string.IsNullOrWhiteSpace(name) ? "?" : System.Globalization.StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
            Label(rect, initial, monogram);
        }

        public void EmptyState(Rect rect, string title, string detail)
        {
            Panel(rect);
            Label(new Rect(rect.x + 24, rect.y + 18, rect.width - 48, 32), title, Heading);
            Label(new Rect(rect.x + 24, rect.y + 56, rect.width - 48, rect.height - 70), detail, Body, true);
        }

        public void Label(Rect rect, string text, GUIStyle style, bool muted = false)
        {
            Color before = GUI.color;
            if (muted) GUI.color *= Muted;
            int size=style.fontSize;
            measuredText.text=text;
            if(style.wordWrap&&rect.width>0&&rect.height>0)
                while(style.fontSize>12&&style.CalcHeight(measuredText,rect.width)>rect.height+1)
                    style.fontSize--;
            GUI.Label(rect, text, style);
            style.fontSize=size;
            GUI.color = before;
        }

        private Rect FitActionRect(Rect rect, string text)
        {
            // Never alter a control's authored rectangle here. Expanding individual IMGUI buttons
            // around their centre caused the top navigation controls to overlap one another.
            return rect;
        }

        public bool Action(Rect rect, string text, bool primary = false, bool selected = false, bool enabled = true)
        {

            // Buttons are menu actions, not paragraphs: normalize any legacy hard line break.
            text = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
            rect = FitActionRect(rect, text);

            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;

            bool hovered=enabled && rect.Contains(Event.current.mousePosition);
            Color surface = primary ? new Color(.23f,.18f,.11f,.90f) : selected || hovered ? Raised : Surface;
            RoundedFill(rect, enabled ? surface : Surface);

            Color border = !enabled
                ? new Color(OutlineSoft.r, OutlineSoft.g, OutlineSoft.b, .52f)
                : primary
                    ? new Color(1f, .85f, .63f, 1f)
                    : selected
                        ? OutlineSelected
                        : Outline;
            RoundedOutline(rect, border);

            // Important/selected controls receive one inner ring rather than the old square side stripe.
            if ((selected || primary) && rect.width > 8 && rect.height > 8)
            {
                Color inner = primary
                    ? new Color(1f, 1f, 1f, .32f)
                    : new Color(Accent.r, Accent.g, Accent.b, .48f);
                RoundedOutline(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), inner);
            }

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            Color before = GUI.color;
            int originalFontSize = Button.fontSize;
            measuredText.text = text;
            float labelWidth = Button.CalcSize(measuredText).x;
            float availableTextWidth = Mathf.Max(1f, rect.width - 14f);
            if (labelWidth > availableTextWidth)
                Button.fontSize = Mathf.Max(12, Mathf.FloorToInt(originalFontSize * availableTextWidth / labelWidth));

            GUI.color *= !enabled ? Muted : primary ? new Color(1f,.91f,.75f) : Text;
            GUI.Label(rect, text, Button);
            GUI.color = before;
            Button.fontSize = originalFontSize;

            if (GUI.enabled && rect.Contains(Event.current.mousePosition) && GUIUtility.hotControl != 0 &&
                rect.width > 8 && rect.height > 8)
            {
                RoundedFill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4),
                    new Color(1, 1, 1, .11f));
            }

            GUI.enabled = previous;
            if (clicked) Alterverse.Audio.UiSoundRouter.Confirm();
            return clicked;
        }

        public bool Navigation(Rect rect,string text,int icon,bool primary=false,bool selected=false)
        {
            bool clicked=Action(rect,"",primary,selected);
            Color tint=primary||selected?Accent:Muted;
            FrontEndNavigation.DrawIcon(new Rect(rect.x+16,rect.y+12,26,26),icon,tint);
            TextAnchor previousAlignment=Button.alignment;
            Button.alignment=TextAnchor.MiddleLeft;
            FitLabel(new Rect(rect.x+57,rect.y,rect.width-74,rect.height),text,Button);
            Button.alignment=previousAlignment;
            if(primary||selected)Fill(new Rect(rect.x+1,rect.y+9,2,rect.height-18),Accent);
            return clicked;
        }

        public void Progress(Rect rect, float fraction)
        {
            // Very thin progress bars do not benefit from a 9-sliced corner texture. Draw a clean framed rail
            // and inset fill instead; visually it still reads rounded at mobile scale without slicing artefacts.
            Fill(rect, OutlineSoft);
            Rect rail = new Rect(rect.x + 1, rect.y + 1, Mathf.Max(0, rect.width - 2), Mathf.Max(1, rect.height - 2));
            Fill(rail, Raised);
            float width = rail.width * Mathf.Clamp01(fraction);
            if (width > 0) Fill(new Rect(rail.x, rail.y, width, rail.height), Accent);
        }

        public void Center(Rect rect, string text)
        {
            Label(rect, text, centered, true);
        }
    }
}


