using System;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Shared frontend surfaces with cached rounded masks for clear mobile controls.</summary>
    internal sealed class FrontEndTheme
    {
        public static readonly Color Background = new(.020f, .028f, .041f);
        // Keep the space/origin-world art visible while controls remain easy to read.
        public static readonly Color Surface = new(.045f, .061f, .083f, .80f);
        public static readonly Color Raised = new(.078f, .108f, .146f, .90f);
        public static readonly Color Text = new(.96f, .975f, .99f);
        public static readonly Color Muted = new(.63f, .71f, .79f);
        public static readonly Color Accent = new(.32f, .88f, .86f);
        public static readonly Color Zero = new(.60f, .39f, .82f);

        // Clearer outlines than the old flat IMGUI rectangles. The selected state stays cyan,
        // while ordinary controls use a cool steel-blue frame that remains visible over space art.
        private static readonly Color Outline = new(.36f, .49f, .63f, .98f);
        private static readonly Color OutlineSoft = new(.25f, .35f, .46f, .94f);
        private static readonly Color OutlineSelected = new(.48f, .97f, .94f, 1f);

        private const int RoundedTextureSize = 48;
        private const float CornerRadius = 12.5f;
        private const float OutlineThickness = 2.8f;
        private const int SliceBorder = 15;
        private static GUIStyle roundedFillStyle, roundedOutlineStyle;

        public readonly GUIStyle Title, Heading, Body, Small, Button, Field;
        private readonly GUIStyle centered;
        private readonly GUIContent measuredText = new GUIContent();
        private Rect hiddenDifficultyCenter;
        private bool hasHiddenDifficultyCenter;

        private static bool IsHiddenDifficultyText(string text)
        {
            if(string.IsNullOrEmpty(text))return false;
            if(text.IndexOf("difficulty",StringComparison.OrdinalIgnoreCase)>=0)return true;
            return string.Equals(text,"EASY",StringComparison.OrdinalIgnoreCase)||
                   string.Equals(text,"MEDIUM",StringComparison.OrdinalIgnoreCase)||
                   string.Equals(text,"HARD",StringComparison.OrdinalIgnoreCase)||
                   string.Equals(text,"EXPERT",StringComparison.OrdinalIgnoreCase);
        }

        // The weapon-skins store entry is retired. Keep the underlying weapon cosmetic data intact,
        // but remove its frontend tab and collapse the remaining store tabs into the freed slot.
        private static bool IsRetiredWeaponStoreTab(Rect rect,string text)
        {
            return Mathf.Abs(rect.y-204f)<=1f&&Mathf.Abs(rect.height-52f)<=1f&&
                   string.Equals(text,"WEAPONS",StringComparison.OrdinalIgnoreCase);
        }

        private static Rect CollapseRetiredWeaponStoreTabGap(Rect rect,string text)
        {
            if(Mathf.Abs(rect.y-204f)>1f||Mathf.Abs(rect.height-52f)>1f)return rect;
            bool followsWeaponTab=
                string.Equals(text,"HERO",StringComparison.OrdinalIgnoreCase)||
                string.Equals(text,"MAPS",StringComparison.OrdinalIgnoreCase)||
                string.Equals(text,"BATTLE ITEM",StringComparison.OrdinalIgnoreCase)||
                string.Equals(text,"BATTLE PASS",StringComparison.OrdinalIgnoreCase);
            if(followsWeaponTab)rect.x-=111f; // 104px store tab + 7px authored gap.
            return rect;
        }

        private bool IsHiddenDifficultyArrow(Rect rect,string text)
        {
            if(!hasHiddenDifficultyCenter||(text!="<"&&text!=">"))return false;
            if(Mathf.Abs(rect.y-hiddenDifficultyCenter.y)>1f||Mathf.Abs(rect.height-hiddenDifficultyCenter.height)>1f)return false;
            bool left=Mathf.Abs(rect.xMax-(hiddenDifficultyCenter.x-6f))<=8f;
            bool right=Mathf.Abs(rect.x-(hiddenDifficultyCenter.xMax+6f))<=8f;
            return left||right;
        }

        public void FitLabel(Rect rect, string text, GUIStyle style, bool muted = false)
        {
            if(IsHiddenDifficultyText(text))return;
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
            Heading = Label(26, FontStyle.Bold);
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

            Field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 23,
                padding = new RectOffset(14, 14, 10, 10),
                border = new RectOffset(SliceBorder, SliceBorder, SliceBorder, SliceBorder)
            };
            Field.normal.textColor = Text;
            Field.focused.textColor = Text;
            // Give text fields the same rounded visual language. The stronger outer frame is drawn
            // by callers where needed; this background removes the remaining square field corners.
            Field.normal.background = roundedFillStyle.normal.background;
            Field.focused.background = roundedFillStyle.normal.background;
        }

        private static void EnsureRoundedStyles()
        {
            if (roundedFillStyle != null && roundedOutlineStyle != null) return;

            Texture2D fill = BuildRoundedMask(false);
            Texture2D outline = BuildRoundedMask(true);
            var border = new RectOffset(SliceBorder, SliceBorder, SliceBorder, SliceBorder);

            roundedFillStyle = new GUIStyle { border = border };
            roundedFillStyle.normal.background = fill;
            roundedOutlineStyle = new GUIStyle { border = border };
            roundedOutlineStyle.normal.background = outline;
        }

        private static Texture2D BuildRoundedMask(bool outline)
        {
            var texture = new Texture2D(RoundedTextureSize, RoundedTextureSize, TextureFormat.RGBA32, false)
            {
                name = outline ? "DF_UI_RoundedOutline" : "DF_UI_RoundedFill",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[RoundedTextureSize * RoundedTextureSize];
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

                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * RoundedTextureSize + x] = new Color32(255, 255, 255, a);
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
                fontSize = size,
                fontStyle = weight,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
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

        public void Panel(Rect rect)
        {
            RoundedFill(rect, Surface);
            RoundedOutline(rect, OutlineSoft);
            // Second subtle ring gives panels the crisp framed look from the visual target.
            if (rect.width > 8 && rect.height > 8)
                RoundedOutline(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4),
                    new Color(.43f, .57f, .70f, .22f));
        }

        public void Label(Rect rect, string text, GUIStyle style, bool muted = false)
        {
            if(IsHiddenDifficultyText(text))return;
            Color before = GUI.color;
            if (muted) GUI.color *= Muted;
            GUI.Label(rect, text, style);
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
            // Weapon skins are no longer exposed as a store category. Returning before hit-testing
            // guarantees the retired tab is neither rendered nor clickable.
            if(IsRetiredWeaponStoreTab(rect,text))return false;
            rect=CollapseRetiredWeaponStoreTabGap(rect,text);

            // Adaptive bot difficulty is intentionally invisible and non-interactive. Keeping this
            // guard in the shared frontend also makes stale menu layouts harmless during migration.
            if(IsHiddenDifficultyText(text)||IsHiddenDifficultyArrow(rect,text))return false;

            // Buttons are menu actions, not paragraphs: normalize any legacy hard line break.
            text = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
            rect = FitActionRect(rect, text);

            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;

            Color surface = primary ? Accent : selected ? Raised : Surface;
            RoundedFill(rect, enabled ? surface : Surface);

            Color border = !enabled
                ? new Color(OutlineSoft.r, OutlineSoft.g, OutlineSoft.b, .52f)
                : primary
                    ? new Color(.76f, 1f, .99f, 1f)
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

            GUI.color *= !enabled ? Muted : primary ? new Color(.035f, .085f, .10f) : Text;
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
            if(IsHiddenDifficultyText(text))
            {
                hiddenDifficultyCenter=rect;
                hasHiddenDifficultyCenter=true;
                return;
            }
            Label(rect, text, centered, true);
        }
    }
}
