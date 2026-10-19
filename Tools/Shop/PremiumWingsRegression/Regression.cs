using System;
using System.Globalization;
using Alterverse.Match;
using Alterverse.Shop;
class Regression
{
    static int checks;
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static void Main()
    {
        // Runs actual production editor checks with in-memory profile data, including old cosmetics.
        Alterverse.UI.CosmeticCollectionChecks.Run();
        foreach(string culture in new[]{"en-US","de-DE"})
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
            foreach(var id in new[]{UniversalHeroCosmetic.AngelWings,UniversalHeroCosmetic.RgbRiftWings})
            {
                var item=CosmeticCatalog.Find(id);
                Check(item.ProgressionPercent==item.Price/400f,"Price/bonus ratio");
                Check(item.BonusLabel.Contains(WingMatchRewardBonus.PercentLabel(item.ProgressionPercent)+"%"),"Fractional localized bonus label");
                Check(WingMatchRewardBonus.ShopBonus(id).Contains(WingMatchRewardBonus.PercentLabel(item.ProgressionPercent)+"%"),"Fractional shop badge");
                for(int amount=0;amount<=4000;amount++)
                {
                    int expected=(int)Math.Round(amount*(double)item.Price/40000d,MidpointRounding.ToEven);
                    Check(WingMatchRewardBonus.CalculateBonus(amount,id)==expected,"Round only the final whole reward");
                }
            }
        }
        Check((int)UniversalHeroCosmetic.DeluxeWings==4&&(int)UniversalHeroCosmetic.AngelWings==5&&(int)UniversalHeroCosmetic.RgbRiftWings==6,"Saved IDs stay stable");
        Check(CosmeticCatalog.Sku(UniversalHeroCosmetic.AngelWings)=="angel_wings"&&CosmeticCatalog.Sku(UniversalHeroCosmetic.RgbRiftWings)=="rgb_rift_wings","Client/server SKU agreement");
        var paths=new System.Collections.Generic.HashSet<string>();
        foreach(var item in CosmeticCatalog.All)
        {
            if(item.Category!=CosmeticCategory.Wings)continue;
            string path=WingPreviewIcons.ResourceFor(item.Id);
            Check(!string.IsNullOrEmpty(path)&&paths.Add(path),"Every wing has a distinct model thumbnail");
            Check(System.IO.File.Exists("Assets/Alterverse/Resources/"+path+".png"),"Mapped thumbnail exists in Resources");
            var first=WingPreviewIcons.Load(item.Id);
            for(int draw=0;draw<20;draw++)Check(object.ReferenceEquals(first,WingPreviewIcons.Load(item.Id)),"Repeated card drawing reuses its texture");
            Check(UnityEngine.Resources.Loads[path]==1,"No repeated texture load per GUI event");
        }
        Check(paths.Count==6,"All six existing and new wing types are covered");
        Check(WingPreviewIcons.Load(UniversalHeroCosmetic.Original)==null&&WingPreviewIcons.Load((UniversalHeroCosmetic)999)==null,"Original and unknown cosmetics retain fallback");
        Console.WriteLine("PASS: production collection checks and "+checks+" premium wing bonus assertions.");
    }
}
