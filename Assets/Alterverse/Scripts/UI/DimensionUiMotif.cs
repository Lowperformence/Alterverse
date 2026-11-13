using UnityEngine;

namespace Alterverse.UI
{
    public static class DimensionUiMotif
    {
        private static Texture2D orbitalRingTex;
        private static Texture2D softGlowTex;
        private static Texture2D loadingPlanetTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMotifTextures()
        {
            if (orbitalRingTex != null) Object.Destroy(orbitalRingTex);
            if (softGlowTex != null) Object.Destroy(softGlowTex);
            if (loadingPlanetTex != null) Object.Destroy(loadingPlanetTex);
            orbitalRingTex = null;
            softGlowTex = null;
            loadingPlanetTex = null;
        }

        private static void EnsureMotifTextures()
        {
            if (softGlowTex == null)
            {
                const int size = 128;
                softGlowTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_MotifSoftGlow",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= 1f) continue;
                    float d = Mathf.Sqrt(d2);
                    float falloff = Mathf.Pow(1f - d, 2.4f);
                    float core = Mathf.Exp(-d2 * 9.5f) * .45f;
                    float a = Mathf.Clamp01(falloff + core);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                softGlowTex.SetPixels32(pixels);
                softGlowTex.Apply(false, true);
            }

            if (orbitalRingTex == null)
            {
                const int size = 256;
                orbitalRingTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_MotifOrbitalRing",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= .98f) continue;

                    float angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f) angle += Mathf.PI * 2f;

                    float alpha = 0f;

                    // 1. Outer precision ring (r = 0.88) with cardinal arc highlights
                    float outerDist = Mathf.Abs(r - .88f);
                    float outerRing = Mathf.Clamp01(1f - outerDist / .013f);
                    float cardinalArc = Mathf.Abs(Mathf.Cos(angle * 2f)) > .35f ? 1f : .28f;
                    alpha += outerRing * outerRing * cardinalArc * .78f;

                    // 2. Outer degree ticks (72 ticks between r = 0.81 and r = 0.855)
                    if (r >= .81f && r <= .855f)
                    {
                        float tickWave = Mathf.Cos(angle * 72f);
                        if (tickWave > .82f)
                        {
                            float tickStrength = (tickWave - .82f) / .18f;
                            float radialFade = 1f - Mathf.Abs(r - .832f) / .023f;
                            alpha += tickStrength * Mathf.Clamp01(radialFade) * .48f;
                        }
                    }

                    // 3. Mid segmented astrolabe ring (r = 0.68)
                    float midDist = Mathf.Abs(r - .68f);
                    float midRing = Mathf.Clamp01(1f - midDist / .011f);
                    float midGap = Mathf.Sin(angle * 6f) > -.45f ? 1f : .12f;
                    alpha += midRing * midRing * midGap * .65f;

                    // 4. 6 glowing orbital nodes on the mid ring
                    for (int n = 0; n < 6; n++)
                    {
                        float na = n * (Mathf.PI / 3f);
                        float nx = Mathf.Cos(na) * .68f;
                        float ny = Mathf.Sin(na) * .68f;
                        float nd = Mathf.Sqrt((dx - nx) * (dx - nx) + (dy - ny) * (dy - ny));
                        if (nd < .042f)
                        {
                            float nodeGlow = Mathf.Pow(1f - nd / .042f, 2f);
                            alpha += nodeGlow * .85f;
                        }
                    }

                    // 5. Inner delicate resonance ring (r = 0.46)
                    float innerDist = Mathf.Abs(r - .46f);
                    float innerRing = Mathf.Clamp01(1f - innerDist / .009f);
                    float innerMask = Mathf.Cos(angle * 4f) > -.2f ? .52f : .15f;
                    alpha += innerRing * innerMask;

                    // 6. Subtle radial halo falloff near the outer ring
                    float halo = Mathf.Exp(-Mathf.Pow((r - .78f) / .14f, 2f)) * .10f;
                    alpha = Mathf.Clamp01(alpha + halo);

                    if (alpha > .004f)
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
                orbitalRingTex.SetPixels32(pixels);
                orbitalRingTex.Apply(false, true);
            }
        }

        public static void DrawSplitPanel(Rect r,Texture2D pixel,Color zero,Color lumen,float time)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            GUI.color=new Color(0,0,0,.72f);
            GUI.DrawTexture(r,pixel);
            float inset=Mathf.Min(3f,Mathf.Min(r.width,r.height)*.08f),innerH=Mathf.Max(0,r.height-inset*2),split=r.x+r.width*.5f+Mathf.Sin(time*.35f)*r.width*.004f;
            GUI.color=new Color(zero.r,zero.g,zero.b,.13f);
            GUI.DrawTexture(new Rect(r.x+inset,r.y+inset,Mathf.Max(0,split-r.x-inset),innerH),pixel);
            GUI.color=new Color(lumen.r,lumen.g,lumen.b,.13f);
            GUI.DrawTexture(new Rect(split,r.y+inset,Mathf.Max(0,r.xMax-split-inset),innerH),pixel);
            if(r.width>=40&&r.height>=14)
            {
                GUI.color=new Color(1,1,1,.08f);
                GUI.DrawTexture(new Rect(r.x+8,r.y+7,r.width*.27f,2),pixel);
                GUI.DrawTexture(new Rect(r.xMax-r.width*.22f-8,r.yMax-9,r.width*.22f,2),pixel);
            }
            for(int i=0;i<5;i++)
            {
                float y=r.y+r.height*(.12f+i*.19f),drift=Mathf.Sin(time*.45f+i*1.7f)*Mathf.Min(1f,r.width*.004f);
                GUI.color=i%2==0?new Color(zero.r,zero.g,zero.b,.24f):new Color(lumen.r,lumen.g,lumen.b,.24f);
                GUI.DrawTexture(new Rect(split-1+drift,y,2,Mathf.Max(1,r.height*.08f)),pixel);
            }
            GUI.color=old;
        }

        public static void DrawSplitPanel(Rect r,Texture2D pixel,Color leftTint,Color rightTint,Color zero,Color lumen,float accentStrength)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            float half=r.width*.5f;
            GUI.color=leftTint;
            GUI.DrawTexture(new Rect(r.x,r.y,half,r.height),pixel);
            GUI.color=rightTint;
            GUI.DrawTexture(new Rect(r.x+half,r.y,r.width-half,r.height),pixel);
            DrawSplitPanel(r,pixel,Color.Lerp(zero,Color.white,.1f*accentStrength),Color.Lerp(lumen,Color.white,.1f*accentStrength),Time.unscaledTime);
            GUI.color=old;
        }

        public static void DrawHeaderLine(Rect r,Texture2D pixel,Color zero,Color lumen,float time)
        {
            if(pixel==null)return;
            Color old=GUI.color;
            float split=r.x+r.width*(.48f+Mathf.Sin(time*.35f)*.02f);
            GUI.color=new Color(zero.r,zero.g,zero.b,.75f);
            GUI.DrawTexture(new Rect(r.x,r.y,Mathf.Max(0,split-r.x),2),pixel);
            GUI.color=new Color(lumen.r,lumen.g,lumen.b,.75f);
            GUI.DrawTexture(new Rect(split,r.y,Mathf.Max(0,r.xMax-split),2),pixel);
            GUI.color=old;
        }

        public static void DrawHeaderLine(Rect r,Texture2D pixel,Color zero,Color lumen)=>DrawHeaderLine(r,pixel,zero,lumen,Time.unscaledTime);

        public static void DrawCornerCuts(Rect r,Texture2D pixel,Color accent)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            GUI.color=new Color(accent.r,accent.g,accent.b,.48f);
            float w=Mathf.Min(r.width*.45f,Mathf.Max(8,r.width*.08f)),h=Mathf.Min(18,r.height*.18f),t=Mathf.Min(2f,Mathf.Max(1f,r.height*.08f));
            GUI.DrawTexture(new Rect(r.x,r.y,w,t),pixel);
            GUI.DrawTexture(new Rect(r.x,r.y,t,h),pixel);
            GUI.DrawTexture(new Rect(r.xMax-w,r.yMax-t,w,t),pixel);
            GUI.DrawTexture(new Rect(r.xMax-t,r.yMax-h,t,h),pixel);
            GUI.color=old;
        }

        public static void DrawCornerCuts(Rect r,Texture2D pixel,Color topLeftAccent,Color bottomRightAccent)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            float w=Mathf.Min(r.width*.45f,Mathf.Max(8,r.width*.08f)),h=Mathf.Min(18,r.height*.18f),t=Mathf.Min(2f,Mathf.Max(1f,r.height*.08f));
            GUI.color=new Color(topLeftAccent.r,topLeftAccent.g,topLeftAccent.b,.56f);
            GUI.DrawTexture(new Rect(r.x,r.y,w,t),pixel);
            GUI.DrawTexture(new Rect(r.x,r.y,t,h),pixel);
            GUI.color=new Color(bottomRightAccent.r,bottomRightAccent.g,bottomRightAccent.b,.56f);
            GUI.DrawTexture(new Rect(r.xMax-w,r.yMax-t,w,t),pixel);
            GUI.DrawTexture(new Rect(r.xMax-t,r.yMax-h,t,h),pixel);
            GUI.color=old;
        }

        /// <summary>Subtle multi-layered cosmic parallax dust and soft nebula horizon glow reactive to pointer/hero rotation.</summary>
        public static void DrawCosmicParallaxOverlay(Rect viewport,Texture2D pixel,Color heroAccent,float time,Vector2 pointerNormalized)
        {
            if(pixel==null||viewport.width<=0f||viewport.height<=0f)return;
            EnsureMotifTextures();
            Color old=GUI.color;
            Color cyan=new Color(.28f,.88f,1f,1f);
            Color gold=new Color(1f,.78f,.42f,1f);

            float px=Mathf.Clamp(pointerNormalized.x,-1f,1f);
            float py=Mathf.Clamp(pointerNormalized.y,-1f,1f);

            // Soft atmospheric nebula bloom in the background (uses smooth radial texture, never blocky bars).
            float bloomX=viewport.x+viewport.width*(.64f-px*.025f);
            float bloomY=viewport.y+viewport.height*(.42f-py*.020f);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,(.08f+Mathf.Sin(time*.7f)*.02f)*old.a);
            GUI.DrawTexture(new Rect(bloomX-340f,bloomY-260f,680f,520f),softGlowTex);

            // Soft-glow drifting cosmic bokeh motes across 3 depth planes.
            const int moteCount=22;
            for(int i=0;i<moteCount;i++)
            {
                float depth=(i%3+1)*.33f;
                float seedX=((i*73+19)%97)/97f;
                float seedY=((i*41+53)%89)/89f;
                float driftX=Mathf.Sin(time*(.16f+depth*.12f)+i*1.3f)*16f*depth - px*24f*depth;
                float driftY=((seedY*viewport.height - time*(4f+depth*7f) - py*14f*depth)%viewport.height + viewport.height)%viewport.height;
                float x=viewport.x + ((seedX*viewport.width + driftX)%viewport.width + viewport.width)%viewport.width;
                float y=viewport.y + driftY;
                float twinkle=.35f+.65f*Mathf.Sin(time*(1.3f+(i%5)*.4f)+i*2.1f);
                if(twinkle<=.15f)continue;
                Color c=i%3==0?heroAccent:i%3==1?cyan:gold;
                float size=(i%4==0?16f:10f)*(.65f+depth*.55f);
                GUI.color=new Color(c.r,c.g,c.b,twinkle*(.18f+depth*.16f)*old.a);
                GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),softGlowTex);
            }
            GUI.color=old;
        }

        /// <summary>A softly shaded planet with slowly rotating surface detail for loading screens.</summary>
        public static void DrawLoadingPlanet(Rect rect,Color tint,float rotation)
        {
            if(rect.width<=0f||rect.height<=0f)return;
            EnsureMotifTextures();
            if(loadingPlanetTex==null)
            {
                // Generate once; bilinear sampling and a soft edge keep the planet smooth at mobile sizes.
                const int size=256;
                loadingPlanetTex=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {
                    name="DF_LoadingPlanet",hideFlags=HideFlags.HideAndDontSave,
                    wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear
                };
                var pixels=new Color32[size*size];
                float half=(size-1)*.5f;
                for(int y=0;y<size;y++)
                for(int x=0;x<size;x++)
                {
                    float nx=(x-half)/half,ny=(y-half)/half;
                    float radius2=nx*nx+ny*ny;
                    if(radius2>=1f)continue;
                    float depth=Mathf.Sqrt(1f-radius2);
                    float light=Mathf.Clamp01(.32f+depth*.58f-nx*.23f+ny*.16f);
                    float band=.5f+.5f*Mathf.Sin(ny*15f+Mathf.Sin(nx*5.5f+ny*3f)*2.1f);
                    float detail=.5f+.5f*Mathf.Sin(nx*18f-ny*7f+Mathf.Sin(ny*8f)*1.3f);
                    float color=light*(.60f+band*.25f+detail*.15f);
                    float edge=Mathf.Clamp01((1f-Mathf.Sqrt(radius2))*size*.40f);
                    pixels[y*size+x]=new Color32((byte)Mathf.RoundToInt(255f*color),
                        (byte)Mathf.RoundToInt(255f*color),(byte)Mathf.RoundToInt(255f*color),
                        (byte)Mathf.RoundToInt(255f*edge));
                }
                loadingPlanetTex.SetPixels32(pixels);
                loadingPlanetTex.Apply(false,true);
            }

            Color old=GUI.color;Matrix4x4 matrix=GUI.matrix;
            try
            {
                GUI.color=new Color(tint.r,tint.g,tint.b,.30f*old.a);
                GUI.DrawTexture(new Rect(rect.x-rect.width*.28f,rect.y-rect.height*.28f,
                    rect.width*1.56f,rect.height*1.56f),softGlowTex);
                GUIUtility.RotateAroundPivot(rotation,rect.center);
                GUI.color=new Color(tint.r,tint.g,tint.b,.95f*old.a);
                GUI.DrawTexture(rect,loadingPlanetTex);
            }
            finally {GUI.matrix=matrix;GUI.color=old;}
        }

        /// <summary>
        /// High-resolution holographic orbital astrolabe, radial backlight bloom, and perspective pedestal light-well.
        /// Must ALWAYS be drawn BEHIND the 3D hero texture so the hero stays 100% crisp and unobstructed in the foreground.
        /// </summary>
        public static void DrawDimensionalRiftStage(Rect stageRect,Texture2D pixel,Color heroAccent,float time,float switchPulse,float yawOffset)
        {
            if(stageRect.width<=0f||stageRect.height<=0f)return;
            EnsureMotifTextures();
            Color old=GUI.color;
            Color brightAccent=Color.Lerp(heroAccent,Color.white,.55f);
            Color cyan=new Color(.32f,.90f,1f,1f);

            float cx=stageRect.center.x;
            float upperCy=stageRect.y+stageRect.height*.43f;
            float minDim=Mathf.Min(stageRect.width,stageRect.height);
            float breathe=1f+Mathf.Sin(time*1.8f)*.035f+switchPulse*.12f;

            // 1. Smooth radial backlight aura behind the hero silhouette.
            float backlightSize=minDim*(.92f*breathe);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.24f+switchPulse*.22f);
            GUI.DrawTexture(new Rect(cx-backlightSize*.5f,upperCy-backlightSize*.5f,backlightSize,backlightSize),softGlowTex);

            float coreGlowSize=minDim*(.54f*breathe);
            GUI.color=new Color(brightAccent.r,brightAccent.g,brightAccent.b,.20f+switchPulse*.25f);
            GUI.DrawTexture(new Rect(cx-coreGlowSize*.5f,upperCy-coreGlowSize*.5f,coreGlowSize,coreGlowSize),softGlowTex);

            // 2. Dual counter-rotating anti-aliased Holographic Orbital Rings behind the hero's shoulders/upper torso.
            float ringSize=minDim*(.78f+switchPulse*.06f);
            Rect ringRect=new Rect(cx-ringSize*.5f,upperCy-ringSize*.5f,ringSize,ringSize);
            Vector2 pivot=new Vector2(cx,upperCy);

            Matrix4x4 prevMatrix=GUI.matrix;
            float angleA=(time*9f + yawOffset*.45f)%360f;
            GUIUtility.RotateAroundPivot(angleA,pivot);
            GUI.color=new Color(brightAccent.r,brightAccent.g,brightAccent.b,.34f+switchPulse*.28f);
            GUI.DrawTexture(ringRect,orbitalRingTex);
            GUI.matrix=prevMatrix;

            float innerRingSize=ringSize*.76f;
            Rect innerRingRect=new Rect(cx-innerRingSize*.5f,upperCy-innerRingSize*.5f,innerRingSize,innerRingSize);
            float angleB=(-time*6.5f - yawOffset*.30f + 30f)%360f;
            GUIUtility.RotateAroundPivot(angleB,pivot);
            GUI.color=new Color(cyan.r,cyan.g,cyan.b,.22f+switchPulse*.18f);
            GUI.DrawTexture(innerRingRect,orbitalRingTex);
            GUI.matrix=prevMatrix;

            // 3. Perspective-projected elliptical holographic light-well under the 3D pedestal.
            float deckY=stageRect.yMax-stageRect.height*.115f;
            float wellW=stageRect.width*(.76f+switchPulse*.08f);
            float wellH=Mathf.Max(22f,stageRect.height*.13f);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.32f+switchPulse*.24f);
            GUI.DrawTexture(new Rect(cx-wellW*.5f,deckY-wellH*.5f,wellW,wellH),softGlowTex);

            // Anamorphic horizontal light flare across the pedestal rim.
            float flareW=stageRect.width*(.64f+switchPulse*.12f);
            GUI.color=new Color(brightAccent.r,brightAccent.g,brightAccent.b,.38f+switchPulse*.30f);
            GUI.DrawTexture(new Rect(cx-flareW*.5f,deckY-5f,flareW,10f),softGlowTex);

            // 4. Soft-glow floating energy bokeh rising gently behind the hero.
            const int sparkCount=8;
            for(int i=0;i<sparkCount;i++)
            {
                float progress=((time*(.22f+(i%3)*.08f)+i*.14f)%1f);
                float lane=((i*37%9)-4f)/4f;
                float sx=cx + lane*(minDim*.34f) + Mathf.Sin(time*1.6f+i*1.9f)*6f;
                float sy=deckY - progress*(stageRect.height*.68f);
                float alpha=Mathf.Sin(progress*Mathf.PI)*(.42f+switchPulse*.28f);
                float sSize=(10f+(i%3)*5f)*(1f-progress*.35f);
                Color sc=i%2==0?brightAccent:cyan;
                GUI.color=new Color(sc.r,sc.g,sc.b,alpha);
                GUI.DrawTexture(new Rect(sx-sSize*.5f,sy-sSize*.5f,sSize,sSize),softGlowTex);
            }

            GUI.color=old;
        }

        /// <summary>Draws an animated diagonal shimmer sweep and tactical corner brackets on primary CTA buttons/cards.</summary>
        public static void DrawButtonShimmer(Rect rect,Texture2D pixel,Color accent,float time,bool hovered=false)
        {
            if(pixel==null||rect.width<=0f||rect.height<=0f)return;
            EnsureMotifTextures();
            Color old=GUI.color;
            float pulse=.5f+.5f*Mathf.Sin(time*3.2f);
            Color bracketColor=Color.Lerp(accent,Color.white,hovered?.55f:.25f*pulse);
            DrawCornerCuts(rect,pixel,bracketColor);

            float cycle=(time%2.8f)/1.15f;
            if(cycle>=0f&&cycle<=1f)
            {
                float sweepX=Mathf.Lerp(rect.x+8f,rect.xMax-36f,cycle);
                float edgeFade=Mathf.Sin(cycle*Mathf.PI);
                GUI.color=new Color(1f,1f,1f,edgeFade*(hovered?.20f:.12f));
                GUI.DrawTexture(new Rect(sweepX,rect.y+1f,28f,rect.height-2f),softGlowTex);
            }
            GUI.color=old;
        }
    }
}
