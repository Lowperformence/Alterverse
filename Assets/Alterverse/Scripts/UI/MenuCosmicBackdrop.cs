using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Cached menu artwork with a single animated GPU pass; no scene objects or per-frame pixel uploads.</summary>
    internal sealed class MenuCosmicBackdrop : System.IDisposable
    {
        private readonly Texture2D texture;
        private readonly bool ownsTexture;
        private readonly Texture2D cards;
        private readonly Material motion;
        private readonly Texture2D motionNoise;
        private static readonly int AnimationTime = Shader.PropertyToID("_AnimationTime");
        private static readonly int MotionStrength = Shader.PropertyToID("_MotionStrength");
        public MenuCosmicBackdrop()
        {
            cards=Resources.Load<Texture2D>("FrontEnd/MenuCardAtlas");
            texture=Resources.Load<Texture2D>("FrontEnd/InterstellarBackdrop");
            if(texture!=null)
            {
                Shader shader=Resources.Load<Shader>("FrontEnd/MenuCosmosMotion");
                if(shader!=null&&shader.isSupported)
                {
                    motionNoise=BuildMotionNoise();
                    motion=new Material(shader){name="DF_MenuCosmosMotion",hideFlags=HideFlags.HideAndDontSave};
                    motion.SetTexture("_NoiseTex",motionNoise);
                    // Anchor effects to the existing artwork's 2:1 reference framing, so
                    // resizing/cropping a phone or tablet keeps motion on the same subjects.
                    Rect framing=Crop(texture.width/(float)texture.height,2f);
                    motion.SetVector("_ReferenceCrop",new Vector4(framing.x,framing.y,framing.width,framing.height));
                }
                return;
            }
            ownsTexture=true;
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
        private static Texture2D BuildMotionNoise()
        {
            const int size=96;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)(size-1),v=y/(float)(size-1);
                // Blend opposite edges to create a seamless, deterministic cloud tile.
                float a=Mathf.PerlinNoise(u*4+13,v*4+7);
                float b=Mathf.PerlinNoise((u-1)*4+13,v*4+7);
                float c=Mathf.PerlinNoise(u*4+13,(v-1)*4+7);
                float d=Mathf.PerlinNoise((u-1)*4+13,(v-1)*4+7);
                float n=Mathf.Lerp(Mathf.Lerp(a,b,Mathf.SmoothStep(0,1,u)),
                    Mathf.Lerp(c,d,Mathf.SmoothStep(0,1,u)),Mathf.SmoothStep(0,1,v));
                byte value=(byte)Mathf.RoundToInt(Mathf.Clamp01(n)*255);
                pixels[y*size+x]=new Color32(value,value,value,255);
            }
            var noise=new Texture2D(size,size,TextureFormat.RGBA32,true,true)
            {name="DF_MenuCloudNoise",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
            noise.SetPixels32(pixels);noise.Apply(true,true);
            return noise;
        }

        private static Rect Crop(float sourceAspect,float targetAspect)
        {
            float width=Mathf.Min(1f,targetAspect/sourceAspect);
            float height=Mathf.Min(1f,sourceAspect/targetAspect);
            return new Rect((1-width)*.5f,(1-height)*.5f,width,height);
        }

        public void Draw(Rect rect)
        {
            if(rect.width<=0||rect.height<=0||Event.current==null||Event.current.type!=EventType.Repaint)return;
            if(motion==null)
            {GUI.DrawTexture(rect,texture,ScaleMode.ScaleAndCrop,false);return;}

            // The menu pauses gameplay, so the animation must use an independent clock.
            motion.SetFloat(AnimationTime,Time.unscaledTime);
            float strength=Application.isMobilePlatform&&
                Alterverse.Performance.MobilePerformanceProfile.Current==Alterverse.Performance.MobileQuality.Battery ? .65f : 1f;
            motion.SetFloat(MotionStrength,strength);
            Rect source=Crop(texture.width/(float)texture.height,rect.width/rect.height);
            Graphics.DrawTexture(rect,texture,source,0,0,0,0,motion,0);
        }
        public void DrawCard(Rect rect,int index)
        {
            Texture2D art=cards!=null?cards:texture;
            float sourceAspect=art.width/(float)art.height/3f;
            float targetAspect=rect.width/rect.height;
            float height=Mathf.Min(1f,sourceAspect/targetAspect);
            float width=Mathf.Min(1f,targetAspect/sourceAspect)/3f;
            // Crop, never stretch. Focus on the subject in the upper-middle of each panel.
            float u=Mathf.Clamp(index,0,2)/3f+(1f/3f-width)*.5f;
            float v=Mathf.Clamp(.67f-height*.5f,0,1-height);
            GUI.DrawTextureWithTexCoords(rect,art,new Rect(u,v,width,height));
        }
        public void Dispose()
        {
            if(motion!=null)Object.Destroy(motion);
            if(motionNoise!=null)Object.Destroy(motionNoise);
            if(ownsTexture&&texture!=null)Object.Destroy(texture);
        }
    }
}

