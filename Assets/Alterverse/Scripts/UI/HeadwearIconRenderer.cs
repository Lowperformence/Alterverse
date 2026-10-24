using System.Collections.Generic;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    // One real-model thumbnail per hat, shared by inventory, shop and Battle Pass.
    internal static class HeadwearIconRenderer
    {
        sealed class CachedIcon { public Texture2D Texture; public float LastLoad=-1f; }
        static readonly Dictionary<UniversalHeroCosmetic,CachedIcon> cache=new();
        public static void Draw(Rect rect,UniversalHeroCosmetic item=UniversalHeroCosmetic.BetaCap)
        {
            string resource=HeadwearSelection.ResourceFor(item);
            if(resource==null)return;
            if(!cache.TryGetValue(item,out CachedIcon entry))
            {entry=new CachedIcon();cache.Add(item,entry);}
            float now=Time.unscaledTime;
            if(entry.Texture==null&&(entry.LastLoad<0f||now<entry.LastLoad||now-entry.LastLoad>=1f))
            {entry.LastLoad=now;entry.Texture=Resources.Load<Texture2D>("Headwear/"+resource+"Icon");}
            if(entry.Texture!=null)GUI.DrawTexture(rect,entry.Texture,ScaleMode.ScaleToFit,true);
            else GUI.Label(rect,CosmeticCatalog.Find(item)?.Name??"HEADWEAR");
        }
    }
}


