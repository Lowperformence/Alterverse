using Alterverse.Characters;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public readonly struct WeaponSkinItem
    {
        public readonly HeroId Hero;
        public readonly string Name;
        public readonly string WeaponName;
        public readonly string Description;
        public readonly Color Glow;
        public readonly int Variant;
        public readonly int Price;

        public WeaponSkinItem(HeroId hero,string name,string weaponName,string description,Color glow,int variant=0,int price=WeaponSkinCatalog.Price)
        {
            Hero=hero;
            Name=name;
            WeaponName=weaponName;
            Description=description;
            Glow=glow;
            Variant=variant;
            Price=price;
        }
    }

    public static class WeaponSkinCatalog
    {
        public const int Price=500;

        // Exactly two purchasable color designs per hero. Both keep the hero's authored
        // standard weapon model, pivot, animation, grip, range, damage and timing unchanged.
        public static readonly WeaponSkinItem[] Items=
        {
            new(HeroId.Veyr,"VIOLET ECLIPSE","Crescent Rift Blades","The authored crescent blades in a deep black-violet finish with restrained dimensional glow. Same weapon model and combat behavior.",new Color(.66f,.16f,1f),0),
            new(HeroId.Veyr,"CRIMSON ECLIPSE","Crescent Rift Blades","The authored crescent blades in a dark crimson-metal finish with a brighter red edge glow. Same weapon model and combat behavior.",new Color(1f,.10f,.20f),1),

            new(HeroId.Kaelon,"EMBER FORGE","Anchor Hammer","Kaelon's original Anchor Hammer in forged charcoal metal with hot amber accents. Same weapon model and combat behavior.",new Color(1f,.48f,.08f),0),
            new(HeroId.Kaelon,"ION FORGE","Anchor Hammer","Kaelon's original Anchor Hammer in cold gunmetal with electric cyan accents. Same weapon model and combat behavior.",new Color(.12f,.78f,1f),1),

            new(HeroId.Nyra,"REDLINE","Pulse Rifle","Nyra's original Pulse Rifle in blackened alloy with a sharp red energy treatment. Same weapon model and combat behavior.",new Color(1f,.05f,.035f),0),
            new(HeroId.Nyra,"ARCTIC COIL","Pulse Rifle","Nyra's original Pulse Rifle in pale gunmetal with an icy blue energy treatment. Same weapon model and combat behavior.",new Color(.20f,.72f,1f),1),

            new(HeroId.Elyra,"VOID ARCANA","Arcane Focus Staff","Elyra's original Arcane Focus Staff in dark violet crystal-metal tones. Same weapon model and combat behavior.",new Color(.66f,.18f,1f),0),
            new(HeroId.Elyra,"STARLIGHT ARCANA","Arcane Focus Staff","Elyra's original Arcane Focus Staff in bright blue-white starlight tones. Same weapon model and combat behavior.",new Color(.40f,.72f,1f),1),

            new(HeroId.Mira,"MINT BLOOM","Lifeweave Staff","Mira's original Lifeweave Staff in a clean pearl finish with light-green healing accents. Same weapon model and combat behavior.",new Color(.38f,1f,.52f),0),
            new(HeroId.Mira,"GOLDEN BLOOM","Lifeweave Staff","Mira's original Lifeweave Staff in warm ivory-gold with soft solar accents. Same weapon model and combat behavior.",new Color(1f,.78f,.30f),1),

            new(HeroId.Rook,"MAGMA STEEL","Titan Gauntlets","Rook's original Titan Gauntlets in dark forged steel with molten orange-red accents. Same weapon model and combat behavior.",new Color(1f,.24f,.04f),0),
            new(HeroId.Rook,"FROST STEEL","Titan Gauntlets","Rook's original Titan Gauntlets in cool metallic steel with bright cyan accents. Same weapon model and combat behavior.",new Color(.16f,.82f,1f),1),

            new(HeroId.Traveler,"CHRONO VIOLET","Chronolux Focus","The Traveler's original Chronolux Focus in dark temporal metal with violet chronolight. Same weapon model and combat behavior.",new Color(.72f,.28f,1f),0),
            new(HeroId.Traveler,"CHRONO GOLD","Chronolux Focus","The Traveler's original Chronolux Focus in pale royal metal with warm golden chronolight. Same weapon model and combat behavior.",new Color(1f,.62f,.22f),1),
            new(HeroId.Nerok,"TIDAL CIRCUIT","Abyssal Anchor","Epic cyan anchor engraving over the original anchor. Cosmetic only.",new Color(.08f,.82f,1f),0),
            new(HeroId.Nerok,"DEEP CORAL","Abyssal Anchor","Epic coral anchor engraving over the original anchor. Cosmetic only.",new Color(1f,.25f,.36f),1),

            new(HeroId.VoidKnight,"AMETHYST RIFT","Rift Edge Greatsword","The original two-handed greatsword with amethyst void engraving and violet edge energy. Cosmetic only.",new Color(.72f,.22f,1f),0),
            new(HeroId.VoidKnight,"GLACIAL RIFT","Rift Edge Greatsword","The original two-handed greatsword with cold cyan rift engraving and glacial edge energy. Cosmetic only.",new Color(.16f,.82f,1f),1)
        };

        public static WeaponSkinItem For(HeroId hero)=>For(hero,0);

        public static WeaponSkinItem For(HeroId hero,int variant)
        {
            if(hero==HeroId.Timmy)return new WeaponSkinItem(hero,"ORIGINAL","Suit Claws","Original astronaut claws; Season 2 cosmetics are not for sale.",new Color(.04f,.68f,1f),0,0);
            variant=Mathf.Clamp(variant,0,1);
            for(int i=0;i<Items.Length;i++)
                if(Items[i].Hero==hero&&Items[i].Variant==variant)return Items[i];
            for(int i=0;i<Items.Length;i++)
                if(Items[i].Hero==hero)return Items[i];
            return Items[0];
        }

        public static bool Contains(HeroId hero,int variant)
        {
            if(variant<0||variant>1)return false;
            for(int i=0;i<Items.Length;i++)
                if(Items[i].Hero==hero&&Items[i].Variant==variant)return true;
            return false;
        }

        public static int VariantCount(HeroId hero)
        {
            int count=0;
            for(int i=0;i<Items.Length;i++)if(Items[i].Hero==hero)count++;
            return count;
        }
    }

    public static class WeaponSkinInventory
    {
        private static string OwnedKey(HeroId hero)=>"DF_WeaponSkinOwned_"+(int)hero;
        private static string EquippedKey(HeroId hero)=>"DF_WeaponSkinEquipped_"+(int)hero;
        private static string VariantOwnedKey(HeroId hero,int variant)=>$"DF_WeaponSkinOwnedItem_{(int)hero}_{variant}";
        private static string SelectedVariantKey(HeroId hero)=>$"DF_WeaponSkinSelected_{(int)hero}";

        // Compatibility with the older Veyr-only tier experiment.
        private static string LegacyVariantOwnedKey(HeroId hero,int tier)=>$"DF_WeaponSkinOwned_{(int)hero}_T{tier}";
        private static string LegacyVariantEquippedKey(HeroId hero)=>$"DF_WeaponSkinEquippedVariant_{(int)hero}";

        private static bool LegacyMappedOwned(HeroId hero,int variant)
        {
            // The retired four-weapon catalog used variants 2/3 as additional purchases.
            // Preserve those purchases by mapping 2 -> colorway 0 and 3 -> colorway 1.
            if(variant==0&&Alterverse.Profile.AccountProfilePrefs.GetInt(VariantOwnedKey(hero,2),0)==1)return true;
            if(variant==1&&Alterverse.Profile.AccountProfilePrefs.GetInt(VariantOwnedKey(hero,3),0)==1)return true;

            if(hero==HeroId.Veyr&&variant==0)
                return Alterverse.Profile.AccountProfilePrefs.GetInt(LegacyVariantOwnedKey(hero,1),0)==1||
                       Alterverse.Profile.AccountProfilePrefs.GetInt(LegacyVariantOwnedKey(hero,2),0)==1||
                       Alterverse.Profile.AccountProfilePrefs.GetInt(LegacyVariantOwnedKey(hero,3),0)==1;
            return false;
        }

        public static bool Owned(HeroId hero,int variant)
        {
            if(!WeaponSkinCatalog.Contains(hero,variant))return false;
            if(variant==0&&Alterverse.Profile.AccountProfilePrefs.GetInt(OwnedKey(hero),0)==1)return true;
            if(Alterverse.Profile.AccountProfilePrefs.GetInt(VariantOwnedKey(hero,variant),0)==1)return true;
            return LegacyMappedOwned(hero,variant);
        }

        public static bool Owned(HeroId hero)
        {
            return Owned(hero,0)||Owned(hero,1);
        }

        public static int EquippedVariant(HeroId hero)
        {
            if(Alterverse.Profile.AccountProfilePrefs.HasKey(SelectedVariantKey(hero)))
            {
                int selected=Alterverse.Profile.AccountProfilePrefs.GetInt(SelectedVariantKey(hero),-1);
                if(selected>=0&&selected<=1&&Owned(hero,selected))return selected;

                // Migrate a currently equipped retired variant without changing the save file.
                if(selected==2&&Owned(hero,0))return 0;
                if(selected==3&&Owned(hero,1))return 1;
                return -1;
            }

            if(Alterverse.Profile.AccountProfilePrefs.GetInt(EquippedKey(hero),0)==1&&Owned(hero,0))return 0;
            if(hero==HeroId.Veyr)
            {
                int legacyTier=Mathf.Clamp(Alterverse.Profile.AccountProfilePrefs.GetInt(LegacyVariantEquippedKey(hero),0),0,3);
                if(legacyTier>0&&Owned(hero,0))return 0;
            }
            return -1;
        }

        public static bool Equipped(HeroId hero)=>EquippedVariant(hero)>=0;

        public static AvatarPurchaseResult Purchase(HeroId hero)=>Purchase(hero,0);

        public static AvatarPurchaseResult Purchase(HeroId hero,int variant)
        {
            if(!HeroUnlockCatalog.Released(hero))return AvatarPurchaseResult.InvalidItem;
            ProfileTransaction.Recover();
            if(!WeaponSkinCatalog.Contains(hero,variant))return AvatarPurchaseResult.AlreadyOwned;
            WeaponSkinItem item=WeaponSkinCatalog.For(hero,variant);
            if(Owned(hero,variant))return AvatarPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(item.Price))return AvatarPurchaseResult.InsufficientShards;

            if(PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready)
                return PlayerWallet.BeginServerPurchase($"weapon_{(int)hero}_{variant}")?AvatarPurchaseResult.Pending:AvatarPurchaseResult.InvalidItem;
            var writes=new System.Collections.Generic.List<ProfileTransaction.Write>
            {
                new("DF_Shards",PlayerWallet.EarnedBalance-item.Price),
                new(VariantOwnedKey(hero,variant),1),
                new(SelectedVariantKey(hero),variant),
                new(EquippedKey(hero),1)
            };
            if(variant==0)writes.Add(new ProfileTransaction.Write(OwnedKey(hero),1));
            if(hero==HeroId.Veyr)writes.Add(new ProfileTransaction.Write(LegacyVariantEquippedKey(hero),0));
            ProfileTransaction.Commit(writes.ToArray());
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }

        public static bool Equip(HeroId hero,bool enabled)
        {
            return enabled?Equip(hero,0):EquipOriginal(hero);
        }

        public static bool Equip(HeroId hero,int variant)
        {
            ProfileTransaction.Recover();
            if(!Owned(hero,variant))return false;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write(EquippedKey(hero),1),
                new ProfileTransaction.Write(SelectedVariantKey(hero),variant),
                new ProfileTransaction.Write(LegacyVariantEquippedKey(hero),0));
            return true;
        }

        public static bool EquipOriginal(HeroId hero)
        {
            ProfileTransaction.Recover();
            ProfileTransaction.Commit(
                new ProfileTransaction.Write(EquippedKey(hero),0),
                new ProfileTransaction.Write(SelectedVariantKey(hero),-1),
                new ProfileTransaction.Write(LegacyVariantEquippedKey(hero),0));
            return true;
        }
    }
}
