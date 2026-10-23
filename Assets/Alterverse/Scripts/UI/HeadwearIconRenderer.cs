using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    // Battle Pass and collection cards share the same model-rendered headwear art.
    internal static class HeadwearIconRenderer
    {
        static Texture2D betaIcon,ufoIcon;
        static float betaLastLoad=-1f,ufoLastLoad=-1f;
        public static void Draw(Rect rect,UniversalHeroCosmetic item=UniversalHeroCosmetic.BetaCap)
        {
            Texture2D icon;
            if(item==UniversalHeroCosmetic.BetaCap)
                icon=Load(ref betaIcon,ref betaLastLoad,"Headwear/BetaCapIcon");
            else if(item==UniversalHeroCosmetic.UfoTestHat)
                icon=Load(ref ufoIcon,ref ufoLastLoad,"Headwear/UfoTestHatIcon");
            else return;
            if(icon!=null)GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit,true);
            else GUI.Label(rect,item==UniversalHeroCosmetic.BetaCap?"BETA":"UFO");
        }
        static Texture2D Load(ref Texture2D icon,ref float lastLoad,string resource)
        {
            // Retry a missing import at most once per second; cache successful loads.
            float now=Time.unscaledTime;
            if(icon==null&&(lastLoad<0f||now<lastLoad||now-lastLoad>=1f))
            {lastLoad=now;icon=Resources.Load<Texture2D>(resource);}
            return icon;
        }
    }
}

