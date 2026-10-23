using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Draws the seeded video target, with a still fallback when playback is unavailable.</summary>
    internal sealed class MenuCosmicBackdrop : System.IDisposable
    {
        private readonly Texture2D texture;
        private readonly bool ownsTexture;
        private readonly Texture2D cards;
        public MenuCosmicBackdrop()
        {
            cards=Resources.Load<Texture2D>("FrontEnd/MenuCardAtlas");
            // Use the exact first video frame, at the same aspect ratio and crop, during decoding.
            texture=Resources.Load<Texture2D>("FrontEnd/InterstellarBackdropAnimatedPoster");
            if(texture!=null)return;
            // Never flash the obsolete menu art when the animated poster is unavailable.
            // A neutral dark frame is preferable until the video decoder has its first frame.
            ownsTexture=true;
            Debug.LogWarning("DIMENSIONS FRONT: animated menu poster is missing; showing a dark frame until video is ready.");
            texture=new Texture2D(1,1,TextureFormat.RGBA32,false)
            {name="DF_MenuPosterUnavailable",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixel(0,0,FrontEndTheme.Background);
            texture.Apply(false,true);
        }
        public void Draw(Rect rect,Texture animated=null)
        {
            // MenuBackgroundVideo seeds its render target with the first-frame poster.
            // Keep one crop and one opaque GUI draw throughout startup and playback.
            GUI.DrawTexture(rect,animated!=null?animated:texture,ScaleMode.ScaleAndCrop,false);
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
        public void Dispose(){if(ownsTexture&&texture!=null)Object.Destroy(texture);}
    }
}
