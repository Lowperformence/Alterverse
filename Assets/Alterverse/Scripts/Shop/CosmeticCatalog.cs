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
        public readonly string Name, OwnedKey, Description, PriceLabel, BonusLabel;
        public readonly int Price;
        public readonly CosmeticCategory Category;
        public readonly Color Accent;
        public int ProgressionPercent=>WingMatchRewardBonus.Percent(Id);
        public CosmeticDefinition(UniversalHeroCosmetic id,string name,int price,string ownedKey,CosmeticCategory category,string description,Color accent)
        {
            Id=id;Name=name;Price=price;OwnedKey=ownedKey;Category=category;Description=description;Accent=accent;
            PriceLabel=price==0?"FREE":price.ToString("N0")+" SHARDS";
            int bonus=WingMatchRewardBonus.Percent(id);
            BonusLabel=bonus==0?"NO PROGRESSION BONUS":"+"+bonus+"% MATCH XP   /   +"+bonus+"% MATCH SHARDS";
        }
    }
    // Add a definition here to expose supported visual content in both collection views.
    // New geometry still belongs in the existing hero visual builders, never the menu.
    public static class CosmeticCatalog
    {
        private static readonly CosmeticDefinition[] definitions={
            new(UniversalHeroCosmetic.Original,"ORIGINAL",0,"",CosmeticCategory.Skins,"The hero's original appearance.",new Color(.48f,.65f,.72f)),
            new(UniversalHeroCosmetic.Voidfall,UniversalSkin.Name,UniversalSkin.Price,UniversalSkin.OwnedKey,CosmeticCategory.Skins,"Obsidian armor. Fractured violet wings.",new Color(.64f,.43f,.94f)),
            new(UniversalHeroCosmetic.PlagueWings,PlagueWings.Name,PlagueWings.Price,PlagueWings.OwnedKey,CosmeticCategory.Wings,"Toxic wings from the Green Planet.",new Color(.44f,.85f,.27f)),
            new(UniversalHeroCosmetic.BetaSupporterWings,BetaSupporterWings.Name,BetaSupporterWings.Price,BetaSupporterWings.OwnedKey,CosmeticCategory.Wings,"Crimson dimensional wings for Beta Supporters.",new Color(.95f,.26f,.36f))
        };
        public static IReadOnlyList<CosmeticDefinition> All { get; }=System.Array.AsReadOnly(definitions);
        public static CosmeticDefinition Find(UniversalHeroCosmetic id)
        {for(int i=0;i<definitions.Length;i++)if(definitions[i].Id==id)return definitions[i];return null;}
        public static bool IsOwned(UniversalHeroCosmetic id)
        {
            var definition=Find(id);if(definition==null)return false;
            return id==UniversalHeroCosmetic.Original||CosmeticProfile.GetInt(definition.OwnedKey,0)==1||
                id==UniversalHeroCosmetic.Voidfall&&CosmeticProfile.PaidVoidfall;
        }
        public static AvatarPurchaseResult Purchase(UniversalHeroCosmetic id)
        {
            CosmeticProfile.Recover();
            var definition=Find(id);
            if(definition==null)return AvatarPurchaseResult.InvalidItem;
            if(IsOwned(id))return AvatarPurchaseResult.AlreadyOwned;
            if(CosmeticProfile.Balance<definition.Price)return AvatarPurchaseResult.InsufficientShards;
            // Purchase grants account ownership only. Hero equip is an explicit inventory action.
            CosmeticProfile.Commit(
                new ProfileTransaction.Write("DF_Shards",CosmeticProfile.Balance-definition.Price),
                new ProfileTransaction.Write(definition.OwnedKey,1));
            if(!CosmeticProfile.IsTest)Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }
    }
}
