using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Shop
{
    // Shop and inventory share one cached, model-rendered thumbnail per wing.
    internal static class WingPreviewIcons
    {
        private static readonly Dictionary<UniversalHeroCosmetic,Texture2D> cache=new();
        internal static string ResourceFor(UniversalHeroCosmetic id)=>id switch
        {
            UniversalHeroCosmetic.Voidfall=>"Wings/VoidfallIcon",
            UniversalHeroCosmetic.PlagueWings=>"Wings/PlagueIcon",
            UniversalHeroCosmetic.BetaSupporterWings=>"Wings/BetaSupporterIcon",
            UniversalHeroCosmetic.DeluxeWings=>"Wings/DeluxeIcon",
            UniversalHeroCosmetic.AngelWings=>"Wings/AngelIcon",
            UniversalHeroCosmetic.RgbRiftWings=>"Wings/RgbIcon",
            _=>null
        };
        internal static Texture2D Load(UniversalHeroCosmetic id)
        {
            string path=ResourceFor(id);
            if(path==null)return null;
            if(!cache.TryGetValue(id,out Texture2D icon))
            {
                icon=Resources.Load<Texture2D>(path);
                cache.Add(id,icon);
            }
            return icon;
        }
    }
}
