using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Runtime-authored premium icon art for The Traveler. Keeps the mobile HUD texture budget tiny
    /// while giving each skill a dedicated readable image instead of borrowing another hero's atlas row.
    /// </summary>
    internal static class TravelerAbilityIcons
    {
        private const int Size=128;
        private static readonly Texture2D[] cache=new Texture2D[5];

        public static Texture2D Get(int slot)
        {
            slot=Mathf.Clamp(slot,0,4);
            if(cache[slot]!=null)return cache[slot];

            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true)
            {
                name="DF_TravelerAbilityIcon_"+slot,
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.DontSave
            };
            Color[] pixels=new Color[Size*Size];
            for(int y=0;y<Size;y++)
            for(int x=0;x<Size;x++)
            {
                Vector2 uv=new((x+.5f)/Size,(y+.5f)/Size);
                pixels[y*Size+x]=Pixel(slot,uv);
            }
            texture.SetPixels(pixels);
            texture.Apply(false,true);
            cache[slot]=texture;
            return texture;
        }

        private static Color Pixel(int slot,Vector2 uv)
        {
            Vector2 p=(uv-new Vector2(.5f,.5f))*2f;
            float radial=Mathf.Clamp01(1f-p.magnitude*.72f);
            Color black=new(.008f,.010f,.018f,1f);
            Color deep=new(.026f,.030f,.050f,1f);
            Color color=Color.Lerp(black,deep,radial*.85f);

            // Subtle red edge vignette connects every icon to the hero's eyes/paradox accent.
            float edge=Mathf.SmoothStep(.74f,1.12f,p.magnitude);
            color=Color.Lerp(color,new Color(.12f,.006f,.010f,1f),edge*.36f);

            float gold=0f,white=0f,red=0f;
            switch(slot)
            {
                case 0: // Temporal Beam
                {
                    Vector2 a=new(-.72f,.35f),b=new(.72f,-.32f);
                    gold+=GlowLine(p,a,b,.055f,.22f);
                    white+=GlowLine(p,a,b,.020f,.085f);
                    gold+=GlowRing(p,new Vector2(-.38f,.18f),.28f,.035f,.16f);
                    red+=GlowRing(p,new Vector2(-.38f,.18f),.16f,.018f,.08f)*.65f;
                    break;
                }
                case 1: // Rift Step
                {
                    gold+=GlowRing(p,new Vector2(-.48f,.02f),.32f,.050f,.17f);
                    gold+=GlowRing(p,new Vector2(.48f,-.02f),.32f,.050f,.17f);
                    white+=GlowRing(p,new Vector2(-.48f,.02f),.22f,.020f,.075f);
                    white+=GlowRing(p,new Vector2(.48f,-.02f),.22f,.020f,.075f);
                    Vector2 a=new(-.22f,.06f),b=new(.22f,-.04f);
                    white+=GlowLine(p,a,b,.025f,.10f);
                    red+=GlowLine(p,new Vector2(-.08f,.18f),new Vector2(.08f,-.18f),.018f,.08f);
                    break;
                }
                case 2: // Chrono Snare
                {
                    gold+=GlowRing(p,Vector2.zero,.55f,.045f,.16f);
                    gold+=GlowRing(p,Vector2.zero,.35f,.025f,.11f);
                    white+=GlowRing(p,Vector2.zero,.12f,.030f,.09f);
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI*.25f;
                        Vector2 d=new(Mathf.Cos(a),Mathf.Sin(a));
                        gold+=GlowLine(p,d*.43f,d*.68f,.018f,.07f);
                    }
                    red+=GlowLine(p,new Vector2(-.10f,-.10f),new Vector2(.22f,.26f),.018f,.08f);
                    break;
                }
                case 3: // Paradox Collapse
                {
                    gold+=GlowRing(p,Vector2.zero,.62f,.055f,.20f);
                    white+=GlowRing(p,Vector2.zero,.30f,.030f,.12f);
                    white+=GlowDisc(p,Vector2.zero,.11f,.16f);
                    red+=GlowLine(p,new Vector2(-.68f,.16f),new Vector2(-.16f,.04f),.025f,.10f);
                    red+=GlowLine(p,new Vector2(.15f,-.02f),new Vector2(.68f,-.28f),.025f,.10f);
                    red+=GlowLine(p,new Vector2(.05f,.15f),new Vector2(.30f,.62f),.020f,.08f);
                    break;
                }
                default: // Lumen Bolt basic
                {
                    Vector2 a=new(-.62f,.42f),b=new(.60f,-.36f);
                    gold+=GlowLine(p,a,b,.060f,.20f);
                    white+=GlowLine(p,a,b,.020f,.075f);
                    white+=GlowDisc(p,b,.075f,.14f);
                    red+=GlowRing(p,new Vector2(-.40f,.27f),.18f,.018f,.07f)*.65f;
                    break;
                }
            }

            Color chronolight=new(.96f,.82f,.52f,1f);
            Color hotWhite=new(1f,.98f,.88f,1f);
            Color paradox=new(1f,.025f,.020f,1f);
            color=Add(color,chronolight,Mathf.Clamp01(gold));
            color=Add(color,hotWhite,Mathf.Clamp01(white));
            color=Add(color,paradox,Mathf.Clamp01(red));

            // Framed corners keep the icon visually consistent with the existing DIMENSIONS FRONT HUD.
            float border=Mathf.Max(
                Mathf.SmoothStep(.91f,.97f,Mathf.Abs(p.x)),
                Mathf.SmoothStep(.91f,.97f,Mathf.Abs(p.y)));
            color=Color.Lerp(color,new Color(.32f,.28f,.20f,1f),border*.55f);
            color.a=1f;
            return color;
        }

        private static Color Add(Color baseColor,Color glow,float amount)
        {
            float a=Mathf.Clamp01(amount);
            return new Color(
                Mathf.Clamp01(baseColor.r+glow.r*a),
                Mathf.Clamp01(baseColor.g+glow.g*a),
                Mathf.Clamp01(baseColor.b+glow.b*a),
                1f);
        }

        private static float GlowLine(Vector2 p,Vector2 a,Vector2 b,float core,float halo)
        {
            Vector2 ab=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(.0001f,ab.sqrMagnitude));
            float d=Vector2.Distance(p,a+ab*t);
            return GlowDistance(d,core,halo);
        }

        private static float GlowRing(Vector2 p,Vector2 center,float radius,float core,float halo)
            =>GlowDistance(Mathf.Abs(Vector2.Distance(p,center)-radius),core,halo);

        private static float GlowDisc(Vector2 p,Vector2 center,float radius,float halo)
        {
            float d=Vector2.Distance(p,center);
            if(d<=radius)return 1f;
            return 1f-Mathf.SmoothStep(radius,radius+halo,d);
        }

        private static float GlowDistance(float d,float core,float halo)
        {
            if(d<=core)return 1f;
            return 1f-Mathf.SmoothStep(core,halo,d);
        }
    }
}
