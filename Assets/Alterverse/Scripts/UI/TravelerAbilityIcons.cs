using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Dedicated premium attack art for THE TRAVELER.
    /// The textures are authored once at runtime and then cached, using the same AbilityButtonView
    /// pipeline, cooldown shader and button layout as the rest of the roster.
    /// </summary>
    internal static class TravelerAbilityIcons
    {
        private const int Size=128;
        private static readonly Texture2D[] cache=new Texture2D[5];

        private static readonly Color Void=new(.004f,.007f,.016f,1f);
        private static readonly Color Deep=new(.016f,.030f,.052f,1f);
        private static readonly Color Cyan=new(.08f,.76f,1f,1f);
        private static readonly Color Ice=new(.62f,.93f,1f,1f);
        private static readonly Color White=new(1f,.99f,.95f,1f);
        private static readonly Color Violet=new(.55f,.24f,1f,1f);
        private static readonly Color Red=new(1f,.025f,.035f,1f);
        private static readonly Color Metal=new(.42f,.48f,.56f,1f);

        public static Texture2D Get(int slot)
        {
            slot=Mathf.Clamp(slot,0,4);
            if(cache[slot]!=null)return cache[slot];

            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true)
            {
                name="DF_TravelerPremiumAbilityIcon_"+slot,
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.DontSave
            };

            var pixels=new Color[Size*Size];
            for(int y=0;y<Size;y++)
            for(int x=0;x<Size;x++)
            {
                Vector2 uv=new((x+.5f)/Size,(y+.5f)/Size);
                pixels[y*Size+x]=Render(slot,uv,x,y);
            }

            texture.SetPixels(pixels);
            texture.Apply(false,true);
            cache[slot]=texture;
            return texture;
        }

        private static Color Render(int slot,Vector2 uv,int x,int y)
        {
            Vector2 p=(uv-new Vector2(.5f,.5f))*2f;
            float radial=Mathf.Clamp01(1f-p.magnitude*.73f);
            Color c=Color.Lerp(Void,Deep,radial*.92f);

            // Tiny deterministic stars; no gameplay Random state is touched.
            float star=Hash(x,y);
            if(star>.982f)
            {
                float s=Mathf.InverseLerp(.982f,1f,star);
                c=Add(c,Color.Lerp(Ice,White,s),.18f+.52f*s);
            }

            // Faint cosmic haze keeps black detail readable on a phone display.
            float haze=Mathf.Exp(-Mathf.Pow(p.x*.65f+p.y*.28f,2f)*3.2f)*.07f;
            c=Add(c,Violet,haze);

            switch(slot)
            {
                case 0: return TemporalBeam(c,p);
                case 1: return TimeSkip(c,p);
                case 2: return ParadoxSnare(c,p);
                case 3: return ParadoxCollapse(c,p);
                default:return FluxBolt(c,p);
            }
        }

        // Q — concentrated light beam through a temporal aperture.
        private static Color TemporalBeam(Color c,Vector2 p)
        {
            Vector2 portal=new(-.42f,.02f);
            c=GlowEllipse(c,p,portal,.28f,.48f,0f,Cyan,.030f,.15f,1.1f);
            c=GlowEllipse(c,p,portal,.19f,.36f,0f,White,.018f,.075f,1.25f);
            c=GlowDisc(c,p,portal,.075f,White,.14f,.26f,1.1f);

            Vector2 a=new(-.25f,.02f),b=new(.92f,.02f);
            c=GlowLine(c,p,a,b,.045f,.20f,Cyan,1.05f);
            c=GlowLine(c,p,a,b,.017f,.075f,White,1.35f);

            for(int i=0;i<4;i++)
            {
                float yy=(i-1.5f)*.13f;
                c=GlowLine(c,p,new Vector2(-.18f,yy*.38f),new Vector2(.70f,yy),.010f,.045f,Ice,.42f);
            }
            c=Shard(c,p,new Vector2(-.70f,.35f),.10f,25f,Metal,.55f);
            c=Shard(c,p,new Vector2(-.62f,-.33f),.08f,-18f,Cyan,.60f);
            return c;
        }

        // W — two portals with the Traveler crossing the gap instead of a normal dash.
        private static Color TimeSkip(Color c,Vector2 p)
        {
            Vector2 left=new(-.48f,.02f),right=new(.48f,-.02f);
            c=Portal(c,p,left,.34f,.52f,-8f);
            c=Portal(c,p,right,.34f,.52f,8f);

            c=GlowLine(c,p,new Vector2(-.22f,.02f),new Vector2(.22f,-.02f),.018f,.10f,White,.70f);
            c=GlowLine(c,p,new Vector2(-.24f,.10f),new Vector2(.24f,.04f),.010f,.055f,Cyan,.50f);

            // Minimal hooded silhouette between the portals.
            c=GlowDisc(c,p,new Vector2(.02f,.16f),.075f,new Color(.02f,.025f,.035f),.01f,.02f,.95f);
            c=GlowLine(c,p,new Vector2(.02f,.10f),new Vector2(.02f,-.25f),.075f,.12f,new Color(.02f,.025f,.035f),.95f);
            c=GlowLine(c,p,new Vector2(.02f,-.02f),new Vector2(-.18f,-.34f),.052f,.09f,new Color(.02f,.025f,.035f),.90f);
            c=GlowLine(c,p,new Vector2(.02f,-.02f),new Vector2(.20f,-.34f),.052f,.09f,new Color(.02f,.025f,.035f),.90f);

            c=Shard(c,p,new Vector2(-.05f,.50f),.08f,50f,Red,.65f);
            c=Shard(c,p,new Vector2(.18f,-.52f),.07f,-25f,Violet,.55f);
            return c;
        }

        // E — miniature Planet Flux locked inside converging temporal orbits.
        private static Color ParadoxSnare(Color c,Vector2 p)
        {
            Vector2 center=Vector2.zero;
            c=Planet(c,p,center,.23f,false);
            c=GlowEllipse(c,p,center,.62f,.24f,22f,Ice,.018f,.060f,.95f);
            c=GlowEllipse(c,p,center,.56f,.30f,-34f,White,.015f,.050f,.92f);
            c=GlowEllipse(c,p,center,.31f,.62f,4f,Cyan,.016f,.060f,.92f);

            Vector2[] nodes={
                new(-.50f,.18f),new(.45f,.24f),new(.08f,.58f),new(-.08f,-.58f)
            };
            for(int i=0;i<nodes.Length;i++)
            {
                c=GlowDisc(c,p,nodes[i],.034f,i%2==0?White:Ice,.035f,.09f,1.05f);
                c=GlowDisc(c,p,nodes[i],.012f,White,.02f,.04f,1.3f);
            }

            c=GlowLine(c,p,new Vector2(-.16f,-.13f),new Vector2(.12f,.17f),.010f,.045f,Red,.85f);
            c=GlowLine(c,p,new Vector2(.02f,-.20f),new Vector2(.22f,.02f),.008f,.040f,Red,.72f);
            return c;
        }

        // R — bright collapsing Planet Flux, white core, cyan/violet orbit and red paradox fractures.
        private static Color ParadoxCollapse(Color c,Vector2 p)
        {
            c=GlowDisc(c,p,Vector2.zero,.46f,Violet,.10f,.32f,.38f);
            c=GlowDisc(c,p,Vector2.zero,.31f,Cyan,.08f,.24f,.50f);
            c=Planet(c,p,Vector2.zero,.30f,true);

            c=GlowEllipse(c,p,Vector2.zero,.72f,.26f,18f,White,.020f,.075f,1.05f);
            c=GlowEllipse(c,p,Vector2.zero,.61f,.36f,-42f,Cyan,.018f,.070f,1.0f);
            c=GlowEllipse(c,p,Vector2.zero,.34f,.70f,7f,Ice,.017f,.060f,.92f);

            // Signature red cracks.
            c=GlowLine(c,p,new Vector2(-.24f,.12f),new Vector2(-.55f,.39f),.012f,.055f,Red,.95f);
            c=GlowLine(c,p,new Vector2(-.18f,.06f),new Vector2(-.46f,-.24f),.012f,.055f,Red,.90f);
            c=GlowLine(c,p,new Vector2(.18f,.08f),new Vector2(.56f,.30f),.012f,.055f,Red,.95f);
            c=GlowLine(c,p,new Vector2(.14f,-.10f),new Vector2(.48f,-.40f),.012f,.055f,Red,.90f);

            // Four white compass spikes make the ultimate unmistakable at small size.
            c=GlowLine(c,p,new Vector2(0,.38f),new Vector2(0,.92f),.024f,.085f,White,1.2f);
            c=GlowLine(c,p,new Vector2(0,-.38f),new Vector2(0,-.92f),.024f,.085f,White,1.2f);
            c=GlowLine(c,p,new Vector2(.38f,0),new Vector2(.82f,0),.018f,.065f,Violet,.70f);
            c=GlowLine(c,p,new Vector2(-.38f,0),new Vector2(-.82f,0),.018f,.065f,Violet,.70f);
            return c;
        }

        // Basic — Planet Flux firing a compressed white/cyan bolt.
        private static Color FluxBolt(Color c,Vector2 p)
        {
            Vector2 planet=new(-.28f,.10f);
            c=Planet(c,p,planet,.22f,false);
            c=GlowEllipse(c,p,planet,.42f,.17f,23f,Ice,.016f,.060f,.82f);
            c=GlowEllipse(c,p,planet,.33f,.21f,-38f,Metal,.013f,.045f,.66f);

            Vector2 a=new(-.08f,.10f),b=new(.78f,.43f);
            c=GlowLine(c,p,a,b,.035f,.17f,Cyan,1.0f);
            c=GlowLine(c,p,a,b,.013f,.060f,White,1.35f);
            c=GlowDisc(c,p,b,.045f,White,.050f,.13f,1.15f);

            c=GlowLine(c,p,new Vector2(-.34f,.02f),new Vector2(-.20f,.18f),.009f,.038f,Red,.75f);
            c=GlowDisc(c,p,new Vector2(-.55f,.22f),.025f,Ice,.025f,.065f,.85f);
            c=GlowDisc(c,p,new Vector2(-.05f,-.20f),.020f,White,.020f,.055f,.72f);
            return c;
        }

        private static Color Portal(Color c,Vector2 p,Vector2 center,float rx,float ry,float angle)
        {
            c=GlowEllipse(c,p,center,rx,ry,angle,Cyan,.030f,.13f,1.0f);
            c=GlowEllipse(c,p,center,rx*.70f,ry*.70f,angle,White,.018f,.070f,1.18f);
            c=GlowDisc(c,p,center,.11f,new Color(.005f,.008f,.018f),.06f,.16f,.75f);
            return c;
        }

        private static Color Planet(Color c,Vector2 p,Vector2 center,float radius,bool collapsing)
        {
            float d=Vector2.Distance(p,center);
            if(d<radius)
            {
                float t=1f-d/radius;
                float noise=Hash(
                    Mathf.FloorToInt((p.x-center.x)*91f+211f),
                    Mathf.FloorToInt((p.y-center.y)*97f+431f));
                Color surface=collapsing
                    ?Color.Lerp(new Color(.10f,.12f,.16f),White,Mathf.Pow(t,2.2f)*.82f)
                    :Color.Lerp(new Color(.025f,.032f,.045f),new Color(.18f,.23f,.30f),t*.62f);
                surface=Color.Lerp(surface,collapsing?Ice:Metal,noise*.16f);
                c=Color.Lerp(c,surface,.96f);
            }
            c=GlowRing(c,p,center,radius,.012f,.075f,collapsing?White:Ice,collapsing?1.0f:.72f);
            if(collapsing)c=GlowDisc(c,p,center,.075f,White,.07f,.20f,1.15f);
            return c;
        }

        private static Color Shard(Color c,Vector2 p,Vector2 center,float size,float degrees,Color color,float strength)
        {
            float a=degrees*Mathf.Deg2Rad,ca=Mathf.Cos(a),sa=Mathf.Sin(a);
            Vector2 d=p-center;
            Vector2 q=new(d.x*ca+d.y*sa,-d.x*sa+d.y*ca);
            float diamond=Mathf.Abs(q.x)/(size*.42f)+Mathf.Abs(q.y)/size;
            float core=1f-Mathf.SmoothStep(.70f,1f,diamond);
            float halo=1f-Mathf.SmoothStep(.85f,1.65f,diamond);
            return Add(Add(c,color,halo*.22f*strength),color,core*.82f*strength);
        }

        private static Color GlowLine(Color c,Vector2 p,Vector2 a,Vector2 b,float core,float halo,Color color,float strength)
            =>Add(c,color,GlowLineValue(p,a,b,core,halo)*strength);

        private static Color GlowDisc(Color c,Vector2 p,Vector2 center,float radius,Color color,float core,float halo,float strength)
        {
            float d=Vector2.Distance(p,center);
            float v=d<=radius?1f:1f-Mathf.SmoothStep(radius+core,radius+halo,d);
            return Add(c,color,Mathf.Clamp01(v)*strength);
        }

        private static Color GlowRing(Color c,Vector2 p,Vector2 center,float radius,float core,float halo,Color color,float strength)
            =>Add(c,color,GlowDistance(Mathf.Abs(Vector2.Distance(p,center)-radius),core,halo)*strength);

        private static Color GlowEllipse(Color c,Vector2 p,Vector2 center,float rx,float ry,float degrees,Color color,float core,float halo,float strength)
        {
            float a=-degrees*Mathf.Deg2Rad,ca=Mathf.Cos(a),sa=Mathf.Sin(a);
            Vector2 d=p-center;
            Vector2 q=new(d.x*ca-d.y*sa,d.x*sa+d.y*ca);
            float n=Mathf.Sqrt(q.x*q.x/(rx*rx)+q.y*q.y/(ry*ry));
            float dist=Mathf.Abs(n-1f)*Mathf.Min(rx,ry);
            return Add(c,color,GlowDistance(dist,core,halo)*strength);
        }

        private static float GlowLineValue(Vector2 p,Vector2 a,Vector2 b,float core,float halo)
        {
            Vector2 ab=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(.0001f,ab.sqrMagnitude));
            return GlowDistance(Vector2.Distance(p,a+ab*t),core,halo);
        }

        private static float GlowDistance(float d,float core,float halo)
        {
            if(d<=core)return 1f;
            return 1f-Mathf.SmoothStep(core,Mathf.Max(core+.001f,halo),d);
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

        private static float Hash(int x,int y)
        {
            float n=Mathf.Sin(x*12.9898f+y*78.233f)*43758.5453f;
            return n-Mathf.Floor(n);
        }
    }
}
