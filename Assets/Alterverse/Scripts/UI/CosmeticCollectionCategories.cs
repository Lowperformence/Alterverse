using System.Collections.Generic;
using Alterverse.Shop;

namespace Alterverse.UI
{
    // Shop and inventory share two distinct, catalog-driven accessory collections.
    internal static class CosmeticCollectionCategories
    {
        public const int Count=2;
        public static string Label(int category)=>category==0?"WINGS":category==1?"HATS":"";
        public static bool Matches(CosmeticDefinition definition,int category)=>definition!=null&&
            (category==0?definition.Category==CosmeticCategory.Wings&&!definition.Headwear:
             category==1&&definition.Headwear);

        public static UniversalHeroCosmetic Choose(IEnumerable<CosmeticDefinition> available,int category,UniversalHeroCosmetic preferred)
        {
            var fallback=UniversalHeroCosmetic.Original;
            foreach(var definition in available)
            {
                if(!Matches(definition,category))continue;
                if(definition.Id==preferred)return preferred;
                if(fallback==UniversalHeroCosmetic.Original)fallback=definition.Id;
            }
            return fallback;
        }
    }
}
