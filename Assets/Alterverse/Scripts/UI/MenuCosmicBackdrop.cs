using UnityEngine;
using Alterverse.Visuals;

namespace Alterverse.UI
{
    /// <summary>One opaque cosmic background draw, with the same authored still as fallback.</summary>
    internal sealed class MenuCosmicBackdrop : System.IDisposable
    {
        private readonly Texture2D texture;
        private readonly bool ownsTexture;
        private readonly bool cosmicLease;
        private readonly Texture2D cards;
        public MenuCosmicBackdrop()
        {
            if(!CosmicSkyMaterial.RenderingAllowed)return;
            cards=Resources.Load<Texture2D>("FrontEnd/MenuCardAtlas");
            texture=CosmicSkyAssets.Acquire(CosmicSkyProfile.MenuPath);
            if(texture!=null){cosmicLease=true;return;}
            // Retain the previous authored poster if the new art was omitted from the build.
            texture=Resources.Load<Texture2D>("FrontEnd/InterstellarBackdropAnimatedPoster");
            if(texture!=null)return;
            // Missing artwork must still give the menu one stable, opaque dark surface.
            ownsTexture=true;
            Debug.LogWarning("DIMENSIONS FRONT: menu background art is missing; showing a dark frame.");
            texture=new Texture2D(1,1,TextureFormat.RGBA32,false)
            {name="DF_MenuPosterUnavailable",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixel(0,0,FrontEndTheme.Background);
            texture.Apply(false,true);
        }
        public void Draw(Rect rect,Texture animated=null)
        {
            // Keep crop, opacity and GUI tint stable across pages and animation frames.
            Texture image=animated!=null?animated:texture;
            if(image!=null)GUI.DrawTexture(rect,image,ScaleMode.ScaleAndCrop,false);
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
        private bool disposed;
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(cosmicLease)CosmicSkyAssets.Release(CosmicSkyProfile.MenuPath,texture);
            else if(ownsTexture&&texture!=null)Object.Destroy(texture);
        }
    }
}
