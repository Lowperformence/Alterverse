using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One opaque, cached texture. No particles, scene objects or per-frame pixel work.</summary>
    internal sealed class MenuCosmicBackdrop : System.IDisposable
    {
        private readonly Texture2D texture;
        public MenuCosmicBackdrop()
        {
            const int width=768,height=384;
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=x/(float)(width-1),v=y/(float)(height-1);
                float seam=.65f+.045f*Mathf.Sin(v*11)+.014f*Mathf.Sin(v*37);
                float distance=Mathf.Abs(u-seam);
                Color tint=u<seam?FrontEndTheme.Zero:FrontEndTheme.Accent;
                float cloud=Mathf.PerlinNoise(u*4+13,v*5+7);
                float haze=Mathf.Exp(-distance*9)*(.018f+.065f*cloud);
                Color c=FrontEndTheme.Background+tint*haze;
                // Dim broken-moon horizon along the bottom, away from controls and text.
                float horizon=.11f+.023f*Mathf.Sin(u*22)+.016f*Mathf.Sin(u*73);
                if(v<horizon)c=Color.Lerp(new Color(.019f,.025f,.037f),c,Mathf.Clamp01(v/horizon)*.3f);
                if(v>horizon&&v<.92f)c+=tint*(Mathf.Exp(-distance*260)*.095f);
                c=Planet(c,u,v,new Vector2(.82f,.78f),.14f,FrontEndTheme.Accent);
                c=Planet(c,u,v,new Vector2(.54f,.86f),.065f,FrontEndTheme.Zero);
                pixels[y*width+x]=c;
            }
            // Private deterministic generator: cosmetic stars cannot change gameplay random state.
            var random=new System.Random(17041);
            for(int i=0;i<100;i++)
            {
                int x=random.Next(width),y=random.Next(height/3,height-8);
                float light=.10f+(float)random.NextDouble()*.13f;
                pixels[y*width+x]=new Color(light*.85f,light*.92f,light,1);
            }
            texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name="DF_MenuCosmos",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixels32(pixels);texture.Apply(false,true);
        }
        private static Color Planet(Color background,float u,float v,Vector2 center,float radius,Color rim)
        {
            Vector2 d=new Vector2((u-center.x)*2,v-center.y);
            float r=d.magnitude/radius;
            if(r>1.05f)return background;
            float edge=Mathf.Exp(-Mathf.Abs(r-1)*90);
            if(r<1)
            {
                float relief=.012f*Mathf.PerlinNoise(u*81,v*93);
                background=new Color(.025f+relief,.032f+relief,.046f+relief);
            }
            return background+rim*(edge*.20f);
        }
        public void Draw(Rect rect)=>GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,false);
        public void Dispose(){if(texture!=null)Object.Destroy(texture);}
    }
}
