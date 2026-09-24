using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Shared frontend surfaces with cached rounded masks for clearer mobile controls.</summary>
    internal sealed class FrontEndTheme
    {
        public static readonly Color Background = new(.020f, .028f, .041f);
        // Frontend cards stay dark enough for legibility, while the cosmic backdrop still breathes through.
        public static readonly Color Surface = new(.050f, .067f, .089f, .72f);
        public static readonly Color Raised = new(.085f, .112f, .148f, .82f);
        public static readonly Color Text = new(.96f, .975f, .99f);
        public static readonly Color Muted = new(.63f, .71f, .79f);
        public static readonly Color Accent = new(.32f, .88f, .86f);
        public static readonly Color Zero = new(.60f, .39f, .82f);

        // Stronger than the previous 1px recess lines: controls should remain readable over every origin world.
        private static readonly Color Outline = new(.29f,.39f,.50f,.96f);
        private static readonly Color OutlineSoft = new(.20f,.28f,.37f,.92f);
        private static readonly Color OutlineSelected = new(.42f,.93f,.91f,.98f);
        private static readonly Color Recess = new(.018f,.024f,.035f,.84f);

        private const int RoundedTextureSize = 32;
        private const float CornerRadius = 8f;
        private const float OutlineThickness = 2.15f;
        private const int SliceBorder = 10;
        private static GUIStyle roundedFillStyle, roundedOutlineStyle;

        public readonly GUIStyle Title, Heading, Body, Small, Button, Field;
        private readonly GUIStyle centered;
        private readonly GUIContent measuredText=new GUIContent();

        public void FitLabel(Rect rect,string text,GUIStyle style,bool muted=false)
        {
            int size=style.fontSize;bool wrap=style.wordWrap;TextClipping clip=style.clipping;
            style.wordWrap=false;style.clipping=TextClipping.Clip;measuredText.text=text;
            float width=style.CalcSize(measuredText).x;
            if(width>rect.width)style.fontSize=Mathf.Max(14,Mathf.FloorToInt(size*rect.width/width));
            Label(rect,text,style,muted);
            style.fontSize=size;style.wordWrap=wrap;style.clipping=clip;
        }

        public FrontEndTheme()
        {
            EnsureRoundedStyles();
            Title = Label(42, FontStyle.Bold);
            Heading = Label(26, FontStyle.Bold);
            Body = Label(19, FontStyle.Normal);
            Small = Label(16, FontStyle.Normal);
            Button = Label(19, FontStyle.Bold); Button.alignment = TextAnchor.MiddleCenter;
            centered = Label(16, FontStyle.Normal); centered.alignment = TextAnchor.MiddleCenter;
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 23, padding = new RectOffset(14,14,10,10) };
            Field.normal.textColor = Text;
        }

        private static void EnsureRoundedStyles()
        {
            if(roundedFillStyle!=null&&roundedOutlineStyle!=null)return;
            Texture2D fill=BuildRoundedMask(false);
            Texture2D outline=BuildRoundedMask(true);
            var border=new RectOffset(SliceBorder,SliceBorder,SliceBorder,SliceBorder);
            roundedFillStyle=new GUIStyle{border=border};
            roundedFillStyle.normal.background=fill;
            roundedOutlineStyle=new GUIStyle{border=border};
            roundedOutlineStyle.normal.background=outline;
        }

        private static Texture2D BuildRoundedMask(bool outline)
        {
            var texture=new Texture2D(RoundedTextureSize,RoundedTextureSize,TextureFormat.RGBA32,false)
            {
                name=outline?"DF_UI_RoundedOutline":"DF_UI_RoundedFill",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Bilinear,
                hideFlags=HideFlags.HideAndDontSave
            };
            var pixels=new Color32[RoundedTextureSize*RoundedTextureSize];
            float half=RoundedTextureSize*.5f;
            for(int y=0;y<RoundedTextureSize;y++)
            for(int x=0;x<RoundedTextureSize;x++)
            {
                float px=x+.5f-half,py=y+.5f-half;
                float outer=RoundedSignedDistance(px,py,half-.75f,CornerRadius);
                float outerAlpha=Mathf.Clamp01(.8f-outer);
                float alpha=outerAlpha;
                if(outline)
                {
                    float innerHalf=half-.75f-OutlineThickness;
                    float innerRadius=Mathf.Max(1f,CornerRadius-OutlineThickness);
                    float inner=RoundedSignedDistance(px,py,innerHalf,innerRadius);
                    float innerAlpha=Mathf.Clamp01(.8f-inner);
                    alpha=outerAlpha*(1f-innerAlpha);
                }
                byte a=(byte)Mathf.RoundToInt(alpha*255f);
                pixels[y*RoundedTextureSize+x]=new Color32(255,255,255,a);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return texture;
        }

        private static float RoundedSignedDistance(float x,float y,float half,float radius)
        {
            float qx=Mathf.Abs(x)-(half-radius);
            float qy=Mathf.Abs(y)-(half-radius);
            float ox=Mathf.Max(qx,0),oy=Mathf.Max(qy,0);
            return Mathf.Sqrt(ox*ox+oy*oy)+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
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
        private static void RoundedFill(Rect rect,Color color)
        {
            EnsureRoundedStyles();
            Color before=GUI.color;GUI.color=color*before;
            roundedFillStyle.Draw(rect,GUIContent.none,false,false,false,false);
            GUI.color=before;
        }
        private static void RoundedOutline(Rect rect,Color color)
        {
            EnsureRoundedStyles();
            Color before=GUI.color;GUI.color=color*before;
            roundedOutlineStyle.Draw(rect,GUIContent.none,false,false,false,false);
            GUI.color=before;
        }
        public void Panel(Rect rect)
        {
            RoundedFill(rect,Surface);
            RoundedOutline(rect,OutlineSoft);
            // A restrained inner top highlight gives cards separation without returning to square bevels.
            RoundedOutline(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),new Color(.36f,.48f,.60f,.28f));
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
            RoundedFill(rect,enabled?surface:Surface);

            Color border=!enabled?new Color(OutlineSoft.r,OutlineSoft.g,OutlineSoft.b,.52f):
                primary?new Color(.69f,1f,.98f,.98f):selected?OutlineSelected:Outline;
            RoundedOutline(rect,border);

            // Selected controls get a second subtle ring instead of the old square left-side stripe.
            if(selected&&!primary)
                RoundedOutline(new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4),new Color(Accent.r,Accent.g,Accent.b,.40f));
            if(primary)
                RoundedOutline(new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4),new Color(1,1,1,.24f));

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            Color before = GUI.color;
            GUI.color *= !enabled ? Muted : primary ? new Color(.035f,.085f,.10f) : Text;
            GUI.Label(rect, text, Button); GUI.color = before;
            if (GUI.enabled && rect.Contains(Event.current.mousePosition) && GUIUtility.hotControl != 0)
                RoundedFill(new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4),new Color(1,1,1,.10f));
            GUI.enabled = previous;
            if(clicked)Alterverse.Audio.UiSoundRouter.Confirm();
            return clicked;
        }
        public void Progress(Rect rect, float fraction)
        {
            RoundedFill(rect,Raised);
            RoundedOutline(rect,OutlineSoft);
            float width=Mathf.Max(0,(rect.width-4)*Mathf.Clamp01(fraction));
            if(width>1)RoundedFill(new Rect(rect.x+2,rect.y+2,width,Mathf.Max(1,rect.height-4)),Accent);
        }
        public void Center(Rect rect, string text) => Label(rect, text, centered, true);
    }
}
