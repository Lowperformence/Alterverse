using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Shared frontend surfaces. No global GUI.skin mutation or generated textures.</summary>
    internal sealed class FrontEndTheme
    {
        public static readonly Color Background = new(.027f, .035f, .049f);
        public static readonly Color Surface = new(.061f, .075f, .095f);
        public static readonly Color Raised = new(.096f, .117f, .145f);
        public static readonly Color Text = new(.93f, .95f, .97f);
        public static readonly Color Muted = new(.61f, .68f, .75f);
        public static readonly Color Accent = new(.39f, .82f, .80f);
        public static readonly Color Zero = new(.51f, .38f, .69f);
        public readonly GUIStyle Title, Heading, Body, Small, Button, Field;
        private readonly GUIStyle centered;

        public FrontEndTheme()
        {
            Title = Label(42, FontStyle.Bold);
            Heading = Label(26, FontStyle.Bold);
            Body = Label(19, FontStyle.Normal);
            Small = Label(16, FontStyle.Normal);
            Button = Label(19, FontStyle.Bold); Button.alignment = TextAnchor.MiddleCenter;
            centered = Label(16, FontStyle.Normal); centered.alignment = TextAnchor.MiddleCenter;
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 23, padding = new RectOffset(14,14,10,10) };
            Field.normal.textColor = Text;
        }
        private static GUIStyle Label(int size, FontStyle weight)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, wordWrap = true,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0,0,0,0), richText = false };
            style.normal.textColor = Text;
            return style;
        }
        public static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color; GUI.color = color * before;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before;
        }
        public void Panel(Rect rect)
        {
            Fill(rect, Surface);
            Fill(new Rect(rect.x, rect.y, rect.width, 1), new Color(.24f,.29f,.35f));
        }
        public void Label(Rect rect, string text, GUIStyle style, bool muted = false)
        {
            Color before = GUI.color; if (muted) GUI.color *= Muted;
            GUI.Label(rect, text, style); GUI.color = before;
        }
        public bool Action(Rect rect, string text, bool primary = false, bool selected = false, bool enabled = true)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            Color surface = primary ? Accent : selected ? Raised : Surface;
            Fill(rect, enabled ? surface : Surface);
            if (selected) Fill(new Rect(rect.x, rect.y, 3, rect.height), Accent);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            Color before = GUI.color;
            GUI.color *= !enabled ? Muted : primary ? new Color(.045f,.095f,.11f) : Text;
            GUI.Label(rect, text, Button); GUI.color = before;
            // Press feedback also works for touch; no hover is required.
            if (GUI.enabled && rect.Contains(Event.current.mousePosition) && GUIUtility.hotControl != 0)
                Fill(rect, new Color(1,1,1,.07f));
            GUI.enabled = previous;
            if(clicked)Alterverse.Audio.UiSoundRouter.Confirm();
            return clicked;
        }
        public void Progress(Rect rect, float fraction)
        {
            Fill(rect, Raised);
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Accent);
        }
        public void Center(Rect rect, string text) => Label(rect, text, centered, true);
    }
}
