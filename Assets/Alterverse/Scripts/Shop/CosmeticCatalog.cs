using System.Collections.Generic;
using Alterverse.Match;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public enum CosmeticCategory { Skins, Wings, Trails, SpawnEffects, RecallEffects, Emotes }
    public sealed class CosmeticDefinition
    {
        public readonly UniversalHeroCosmetic Id;
        public readonly string Name, OwnedKey, Description, PriceLabel, BonusLabel, PurchaseSku, HeadwearResource;
        public readonly int Price;
        public readonly CosmeticCategory Category;
        public readonly Color Accent;
        public readonly bool Headwear, Purchasable;
        public float ProgressionPercent=>WingMatchRewardBonus.Percent(Id);
        public CosmeticDefinition(UniversalHeroCosmetic id,string name,int price,string ownedKey,CosmeticCategory category,string description,Color accent,
            bool headwear=false,bool purchasable=true,string unlockLabel=null,string purchaseSku="",string headwearResource=null)
        {
            Id=id;Name=name;Price=price;OwnedKey=ownedKey;Category=category;Description=description;Accent=accent;
            Headwear=headwear;Purchasable=purchasable;PurchaseSku=purchaseSku;HeadwearResource=headwearResource;
            PriceLabel=unlockLabel??(price==0?"FREE":price.ToString("N0")+" SHARDS");
            float bonus=WingMatchRewardBonus.Percent(id);
            BonusLabel=bonus==0?"NO PROGRESSION BONUS":WingMatchRewardBonus.ShopBonus(id);
        }
    }
    // Add a definition here to expose supported visual content in both collection views.
    // New geometry still belongs in the existing hero visual builders, never the menu.
    public static class CosmeticCatalog
    {
        private static readonly CosmeticDefinition[] definitions={
            new(UniversalHeroCosmetic.Original,"ORIGINAL",0,"",CosmeticCategory.Skins,"The hero's original appearance.",new Color(.48f,.65f,.72f)),
            new(UniversalHeroCosmetic.Voidfall,UniversalSkin.Name,UniversalSkin.Price,UniversalSkin.OwnedKey,CosmeticCategory.Wings,"Fractured violet wings. Preserves the hero's original colors.",new Color(.64f,.43f,.94f)),
            new(UniversalHeroCosmetic.PlagueWings,PlagueWings.Name,PlagueWings.Price,PlagueWings.OwnedKey,CosmeticCategory.Wings,"Toxic wings from the Green Planet.",new Color(.44f,.85f,.27f)),
            new(UniversalHeroCosmetic.BetaSupporterWings,BetaSupporterWings.Name,BetaSupporterWings.Price,BetaSupporterWings.OwnedKey,CosmeticCategory.Wings,"Crimson dimensional wings for Beta Supporters.",new Color(.95f,.26f,.36f)),
            new(UniversalHeroCosmetic.AngelWings,AngelWings.Name,AngelWings.Price,AngelWings.OwnedKey,CosmeticCategory.Wings,"Golden feathered wings with warm radiant quills and an aureole.",new Color(1f,.78f,.30f)),
            new(UniversalHeroCosmetic.RgbRiftWings,RgbRiftWings.Name,RgbRiftWings.Price,RgbRiftWings.OwnedKey,CosmeticCategory.Wings,"Articulated anthracite metal wings with flowing RGB light strips.",new Color(.16f,.78f,1f)),
            new(UniversalHeroCosmetic.DeluxeWings,DeluxeWings.Name,DeluxeWings.Price,DeluxeWings.OwnedKey,CosmeticCategory.Wings,"Faceted silver crystal wings with icy cyan fractures.",new Color(.72f,.92f,1f)),
            new(UniversalHeroCosmetic.BetaCap,HeadwearSelection.BetaCapName,0,HeadwearSelection.BetaCapOwnedKey,CosmeticCategory.Skins,
                "Exclusive BETA cap. Separate headwear slot for every hero; can be worn with wings.",new Color(.45f,.83f,1f),
                headwear:true,purchasable:false,unlockLabel:"PREMIUM BATTLE PASS / LEVEL 50",headwearResource:"BetaCap"),
            new(UniversalHeroCosmetic.UfoTestHat,HeadwearSelection.UfoName,HeadwearSelection.UfoPrice,HeadwearSelection.UfoOwnedKey,
                CosmeticCategory.Skins,"A playful UFO with crooked antennae. Separate headwear slot; wear it with wings.",
                new Color(.25f,.9f,.7f),headwear:true,purchaseSku:"ufo_test_hat",headwearResource:"UfoTestHat"),
            new(UniversalHeroCosmetic.WizardHat,"ASTRAL WIZARD",HeadwearSelection.ShopPrice,"DF_HeadwearOwned_WizardHat",CosmeticCategory.Skins,
                "A crooked violet wizard hat with embroidered stars and a moonstone buckle.",new Color(.65f,.38f,.95f),
                headwear:true,purchaseSku:"wizard_hat",headwearResource:"WizardHat"),
            new(UniversalHeroCosmetic.PropellerCap,"SKY SPINNER",HeadwearSelection.ShopPrice,"DF_HeadwearOwned_PropellerCap",CosmeticCategory.Skins,
                "A cheerful four-panel cap with a colorful three-blade propeller.",new Color(.20f,.80f,.70f),
                headwear:true,purchaseSku:"propeller_cap",headwearResource:"PropellerCap"),
            new(UniversalHeroCosmetic.MushroomHat,"COSMIC MUSHROOM",HeadwearSelection.ShopPrice,"DF_HeadwearOwned_MushroomHat",CosmeticCategory.Skins,
                "A red mushroom canopy with cream spots and a soft fitted headband.",new Color(.95f,.30f,.35f),
                headwear:true,purchaseSku:"mushroom_hat",headwearResource:"MushroomHat")
        };
        public static IReadOnlyList<CosmeticDefinition> All { get; }=System.Array.AsReadOnly(definitions);
        public static CosmeticDefinition Find(UniversalHeroCosmetic id)
        {for(int i=0;i<definitions.Length;i++)if(definitions[i].Id==id)return definitions[i];return null;}
        public static bool IsOwned(UniversalHeroCosmetic id)
        {
            var definition=Find(id);if(definition==null)return false;
            if(id==UniversalHeroCosmetic.BetaCap&&!CosmeticProfile.IsTest)BattlePassProgress.ReconcileCosmeticRewards();
            return id==UniversalHeroCosmetic.Original||CosmeticProfile.GetInt(definition.OwnedKey,0)==1||
                id==UniversalHeroCosmetic.Voidfall&&CosmeticProfile.PaidVoidfall;
        }
        public static string Sku(UniversalHeroCosmetic id)
        {
            var definition=Find(id);
            if(definition!=null&&definition.Headwear)return definition.PurchaseSku;
            return id switch
            {
            UniversalHeroCosmetic.Voidfall=>"voidfall",
            UniversalHeroCosmetic.PlagueWings=>"plague_wings",
            UniversalHeroCosmetic.BetaSupporterWings=>"beta_supporter_wings",
            UniversalHeroCosmetic.DeluxeWings=>"deluxe_wings",
            UniversalHeroCosmetic.AngelWings=>"angel_wings",
            UniversalHeroCosmetic.RgbRiftWings=>"rgb_rift_wings",
            _=>""
            };
        }
        public static AvatarPurchaseResult Purchase(UniversalHeroCosmetic id)
        {
            CosmeticProfile.Recover();
            var definition=Find(id);
            if(definition==null)return AvatarPurchaseResult.InvalidItem;
            if(IsOwned(id))return AvatarPurchaseResult.AlreadyOwned;
            if(!definition.Purchasable)return AvatarPurchaseResult.InvalidItem;
            if(CosmeticProfile.Balance<definition.Price)return AvatarPurchaseResult.InsufficientShards;
            if(!CosmeticProfile.IsTest&&PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready)
                return PlayerWallet.BeginServerPurchase(Sku(id))?AvatarPurchaseResult.Pending:AvatarPurchaseResult.InvalidItem;
            // Purchase grants account ownership only. Hero equip is an explicit inventory action.
            CosmeticProfile.Commit(
                new ProfileTransaction.Write("DF_Shards",(CosmeticProfile.IsTest?CosmeticProfile.Balance:PlayerWallet.EarnedBalance)-definition.Price),
                new ProfileTransaction.Write(definition.OwnedKey,1));
            if(!CosmeticProfile.IsTest)Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }
    }
}



