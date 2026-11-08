using UnityEngine;

namespace Alterverse.UI
{
    public static class DimensionUiMotif
    {
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

        /// <summary>Subtle multi-layered cosmic parallax dust and dimensional horizon bands reactive to pointer/hero rotation.</summary>
        public static void DrawCosmicParallaxOverlay(Rect viewport,Texture2D pixel,Color heroAccent,float time,Vector2 pointerNormalized)
        {
            if(pixel==null||viewport.width<=0f||viewport.height<=0f)return;
            Color old=GUI.color;
            Color cyan=new Color(.24f,.86f,1f,1f);
            Color gold=new Color(1f,.76f,.38f,1f);

            // Subtle upper & lower atmospheric horizon glow bands shifting with parallax.
            float px=Mathf.Clamp(pointerNormalized.x,-1f,1f);
            float py=Mathf.Clamp(pointerNormalized.y,-1f,1f);
            float horizonY=viewport.y+viewport.height*(.68f+py*.012f);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.045f+Mathf.Sin(time*.8f)*.015f);
            GUI.DrawTexture(new Rect(viewport.x,horizonY-18f,viewport.width,36f),pixel);
            GUI.color=new Color(cyan.r,cyan.g,cyan.b,.08f);
            GUI.DrawTexture(new Rect(viewport.x+viewport.width*(.08f-px*.015f),horizonY,viewport.width*.84f,1f),pixel);

            // Multi-depth drifting starlight & dimensional embers (deterministic, zero allocation).
            const int moteCount=26;
            for(int i=0;i<moteCount;i++)
            {
                float depth=(i%3+1)*.33f;
                float seedX=((i*73+19)%97)/97f;
                float seedY=((i*41+53)%89)/89f;
                float driftX=Mathf.Sin(time*(.18f+depth*.15f)+i*1.3f)*14f*depth - px*22f*depth;
                float driftY=((seedY*viewport.height - time*(5f+depth*9f) - py*14f*depth)%viewport.height + viewport.height)%viewport.height;
                float x=viewport.x + ((seedX*viewport.width + driftX)%viewport.width + viewport.width)%viewport.width;
                float y=viewport.y + driftY;
                float twinkle=.35f+.65f*Mathf.Sin(time*(1.5f+(i%5)*.45f)+i*2.1f);
                if(twinkle<=.15f)continue;
                Color c=i%3==0?heroAccent:i%3==1?cyan:gold;
                float size=i%4==0?3f:2f;
                GUI.color=new Color(c.r,c.g,c.b,twinkle*(.16f+depth*.18f));
                GUI.DrawTexture(new Rect(x,y,size,size),pixel);
                if(i%6==0)
                {
                    GUI.color=new Color(c.r,c.g,c.b,twinkle*.08f);
                    GUI.DrawTexture(new Rect(x-3f,y+size*.5f-.5f,size+6f,1f),pixel);
                }
            }
            GUI.color=old;
        }

        /// <summary>Draws a pulsing dimensional rift behind the 3D hero and projected holographic pedestal rings underneath.</summary>
        public static void DrawDimensionalRiftStage(Rect heroRect,Texture2D pixel,Color heroAccent,float time,float switchPulse,float yawOffset)
        {
            if(pixel==null||heroRect.width<=0f||heroRect.height<=0f)return;
            Color old=GUI.color;
            Color coreWhite= Color.Lerp(heroAccent,Color.white,.68f);
            Color secondary=new Color(.22f,.88f,1f,1f);
            float cx=heroRect.center.x;
            float riftTop=heroRect.y+heroRect.height*.07f;
            float riftBottom=heroRect.yMax-heroRect.height*.12f;
            float riftHeight=Mathf.Max(40f,riftBottom-riftTop);
            float breathe=1f+Mathf.Sin(time*2.1f)*.08f+switchPulse*.32f;

            // 1. Soft atmospheric aura columns behind the hero silhouette.
            float auraW=heroRect.width*(.44f*breathe);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.055f+switchPulse*.09f);
            GUI.DrawTexture(new Rect(cx-auraW*.5f,riftTop+16f,auraW,riftHeight-24f),pixel);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.085f+switchPulse*.12f);
            GUI.DrawTexture(new Rect(cx-auraW*.26f,riftTop+28f,auraW*.52f,riftHeight-48f),pixel);

            // 2. Stepped dimensional rift spine & fracture filaments behind the hero.
            const int segments=18;
            float segH=riftHeight/segments;
            for(int i=0;i<segments;i++)
            {
                float norm=i/(float)(segments-1);
                float envelope=Mathf.Sin(norm*Mathf.PI);
                float jag=Mathf.Sin(norm*9.5f+time*2.6f)*10f*envelope + Mathf.Cos(norm*17f-time*1.8f)*4f*envelope;
                float w=Mathf.Max(2f,(6f+switchPulse*10f)*envelope);
                float y=riftTop+i*segH;
                GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,(.22f+switchPulse*.30f)*envelope);
                GUI.DrawTexture(new Rect(cx+jag-w*1.6f,y,w*3.2f,segH+1f),pixel);
                GUI.color=new Color(coreWhite.r,coreWhite.g,coreWhite.b,(.48f+switchPulse*.38f)*envelope);
                GUI.DrawTexture(new Rect(cx+jag-w*.5f,y,w,segH+1f),pixel);

                if(i%3==1)
                {
                    float branchW=(18f+16f*envelope)*breathe;
                    float dir=i%2==0?1f:-1f;
                    GUI.color=new Color(secondary.r,secondary.g,secondary.b,.24f*envelope);
                    GUI.DrawTexture(new Rect(cx+jag+(dir>0?2f:-branchW-2f),y+segH*.5f,branchW,1.5f),pixel);
                }
            }

            // 3. Switch-surge shockwave frame when selecting a new hero.
            if(switchPulse>.02f)
            {
                float expand=(1f-switchPulse)*38f;
                Rect wave=new Rect(heroRect.x+24f-expand,heroRect.y+28f-expand*.6f,heroRect.width-48f+expand*2f,heroRect.height-56f+expand*1.2f);
                Color waveColor=new Color(coreWhite.r,coreWhite.g,coreWhite.b,switchPulse*.36f);
                DrawCornerCuts(wave,pixel,waveColor);
            }

            // 4. Projected 2.5D Holographic Pedestal Rings & Rotating Energy Nodes at deck level.
            float deckY=heroRect.yMax-heroRect.height*.105f;
            float outerW=heroRect.width*(.72f+switchPulse*.06f);
            float innerW=outerW*.68f;
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.18f+switchPulse*.20f);
            GUI.DrawTexture(new Rect(cx-outerW*.5f,deckY-2f,outerW,4f),pixel);
            GUI.color=new Color(coreWhite.r,coreWhite.g,coreWhite.b,.42f+switchPulse*.32f);
            GUI.DrawTexture(new Rect(cx-innerW*.5f,deckY-1f,innerW,2f),pixel);

            float phase=time*1.35f + yawOffset*Mathf.Deg2Rad*1.5f;
            const int ringNodes=10;
            for(int i=0;i<ringNodes;i++)
            {
                float a=phase + i*(Mathf.PI*2f/ringNodes);
                float nx=cx + Mathf.Cos(a)*(outerW*.48f);
                float ny=deckY + Mathf.Sin(a)*11f;
                float depthAlpha=.32f+.68f*(Mathf.Sin(a)*.5f+.5f);
                Color nodeCol=i%2==0?coreWhite:secondary;
                GUI.color=new Color(nodeCol.r,nodeCol.g,nodeCol.b,depthAlpha*(.42f+switchPulse*.30f));
                GUI.DrawTexture(new Rect(nx-4f,ny-1.5f,8f,3f),pixel);
            }

            // 5. Rising pedestal energy motes around the hero.
            for(int i=0;i<10;i++)
            {
                float progress=((time*(.32f+(i%3)*.11f)+i*.13f)%1f);
                float spread=((i*37%10)-4.5f)/4.5f;
                float mx=cx + spread*(outerW*.42f) + Mathf.Sin(time*2f+i)*5f;
                float my=deckY - progress*(riftHeight*.78f);
                float alpha=Mathf.Sin(progress*Mathf.PI)*(.45f+switchPulse*.30f);
                Color mc=i%2==0?coreWhite:heroAccent;
                GUI.color=new Color(mc.r,mc.g,mc.b,alpha);
                GUI.DrawTexture(new Rect(mx-1.5f,my-4f,3f,8f),pixel);
            }
            GUI.color=old;
        }

        /// <summary>Draws an animated diagonal shimmer sweep and tactical corner brackets on primary CTA buttons/cards.</summary>
        public static void DrawButtonShimmer(Rect rect,Texture2D pixel,Color accent,float time,bool hovered=false)
        {
            if(pixel==null||rect.width<=0f||rect.height<=0f)return;
            Color old=GUI.color;
            float pulse=.5f+.5f*Mathf.Sin(time*3.2f);
            Color bracketColor=Color.Lerp(accent,Color.white,hovered?.55f:.25f*pulse);
            DrawCornerCuts(rect,pixel,bracketColor);

            // Animated shimmer sweep every 2.8 seconds.
            float cycle=(time%.28f<0f)?0f:(time%2.8f)/1.15f;
            if(cycle>=0f&&cycle<=1f)
            {
                float sweepX=Mathf.Lerp(rect.x+6f,rect.xMax-22f,cycle);
                float edgeFade=Mathf.Sin(cycle*Mathf.PI);
                GUI.color=new Color(1f,1f,1f,edgeFade*(hovered?.22f:.14f));
                GUI.DrawTexture(new Rect(sweepX,rect.y+2f,14f,rect.height-4f),pixel);
                GUI.color=new Color(accent.r,accent.g,accent.b,edgeFade*.28f);
                GUI.DrawTexture(new Rect(sweepX-4f,rect.y+1f,22f,2f),pixel);
                GUI.DrawTexture(new Rect(sweepX-4f,rect.yMax-3f,22f,2f),pixel);
            }
            GUI.color=old;
        }
    }
}
