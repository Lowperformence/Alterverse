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
            new(HeroId.Veyr,"CRIMSON STRIPE","Crescent Rift Blades","Blutrote Klingen mit scharfem Tiger-Streifenmuster. Gleiches Waffenmodell und Kampfverhalten.",new Color(1f,.08f,.16f),0),
            new(HeroId.Veyr,"EMERALD HEX","Crescent Rift Blades","Smaragdgrüne Klingen auf Platin-Weiß mit leuchtendem Waben-Gittermuster. Gleiches Waffenmodell und Kampfverhalten.",new Color(.08f,1f,.36f),1),

            new(HeroId.Kaelon,"SOLAR WHITE-GOLD","Anchor Hammer","Strahlend weiß-goldener Hammer mit diagonalem Sonnen-Chevron-Muster. Gleiches Waffenmodell und Kampfverhalten.",new Color(1f,.82f,.12f),0),
            new(HeroId.Kaelon,"NEON PINK CORE","Anchor Hammer","Knalliges Neon-Pink mit hexagonalem Reaktor-Wabenmuster. Gleiches Waffenmodell und Kampfverhalten.",new Color(1f,.14f,.68f),1),

            new(HeroId.Nyra,"ARCTIC WHITE STRIPE","Pulse Rifle","Reinweißes Spec-Ops-Gewehr mit scharfen taktischen Kontraststreifen. Gleiches Waffenmodell und Kampfverhalten.",new Color(.95f,.98f,1f),0),
            new(HeroId.Nyra,"VENOM GREEN HEX","Pulse Rifle","Leuchtend giftgrünes Gewehr mit kristallinem Waben-Netzwerk. Gleiches Waffenmodell und Kampfverhalten.",new Color(.12f,1f,.28f),1),

            new(HeroId.Elyra,"SAKURA PINK","Arcane Focus Staff","Kräftiges Sakura-Pink mit diagonalem Sternen-Streifenmuster. Gleiches Waffenmodell und Kampfverhalten.",new Color(1f,.20f,.74f),0),
            new(HeroId.Elyra,"PEARL WHITE ASTRAL","Arcane Focus Staff","Edles Reinweiß mit leuchtenden Astral-Ringen und Kristallgitter. Gleiches Waffenmodell und Kampfverhalten.",new Color(.96f,.98f,1f),1),

            new(HeroId.Mira,"JADE EMERALD","Lifeweave Staff","Leuchtendes Smaragdgrün mit geschwungenen Lebens-Streifen. Gleiches Waffenmodell und Kampfverhalten.",new Color(.06f,1f,.38f),0),
            new(HeroId.Mira,"RUBY ROSE","Lifeweave Staff","Kräftiges Rubinrot-Pink mit kristallinem Rosetten-Wabenmuster. Gleiches Waffenmodell und Kampfverhalten.",new Color(1f,.12f,.38f),1),

            new(HeroId.Rook,"REACTOR BREAKER // EPIC","Titan Gauntlets","Feuerrote Handschuhe mit markanten Warn-Streifen. Gleiches Waffenmodell und Kampfverhalten.",new Color(1f,.06f,.08f),0),
            new(HeroId.Rook,"GLACIER WHITE TITAN","Titan Gauntlets","Reinweiße Titan-Handschuhe mit eisblauem Panzer-Wabenmuster. Gleiches Waffenmodell und Kampfverhalten.",new Color(.94f,.98f,1f),1),

            new(HeroId.Traveler,"PARADOX PINK","Chronolux Focus","Leuchtendes Neon-Pink mit gezackten Zeit-Riss-Streifen. Gleiches Waffenmodell und Kampfverhalten.",new Color(1f,.16f,.72f),0),
            new(HeroId.Traveler,"MATRIX EMERALD","Chronolux Focus","Kräftiges Matrix-Grün mit konzentrischen Chronolux-Ringen und Waben. Gleiches Waffenmodell und Kampfverhalten.",new Color(.10f,1f,.42f),1),

            new(HeroId.Nerok,"CRIMSON SHARK","Abyssal Anchor","Tiefroter Anker mit scharfem Haifischzahn-Streifenmuster. Rein kosmetisch.",new Color(1f,.10f,.18f),0),
            new(HeroId.Nerok,"PEARL WHITE ABYSS","Abyssal Anchor","Strahlend weißer Perlen-Anker mit türkisem Schuppen-Wabenmuster. Rein kosmetisch.",new Color(.95f,.99f,1f),1),

            new(HeroId.VoidKnight,"BLOOD ECLIPSE","Rift Edge Greatsword","Blutrotes Großschwert mit diagonalem Riss-Streifenmuster. Rein kosmetisch.",new Color(1f,.08f,.18f),0),
            new(HeroId.VoidKnight,"ROYAL WHITE RIFT","Rift Edge Greatsword","Reinweißes Großschwert mit violett-goldenem Diamant-Wabenmuster. Rein kosmetisch.",new Color(.96f,.94f,1f),1),

            new(HeroId.Bolt7,"CYBER PINK BARRAGE","Gatling & Grenade Launcher","Knalliges Cyber-Pink mit markanten Rennstreifen und Tech-Chevrons. Rein kosmetisch.",new Color(1f,.14f,.66f),0),
            new(HeroId.Bolt7,"TOXIC GREEN BARRAGE","Gatling & Grenade Launcher","Leuchtendes Giftgrün mit hexagonalem Reaktor-Wabenmuster. Rein kosmetisch.",new Color(.14f,1f,.26f),1)
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

        // Launching the hero does not release the two preview-only weapon colorways.
        public static bool Released(HeroId hero)=>HeroUnlockCatalog.Released(hero)&&hero!=HeroId.VoidKnight;

        public static AvatarPurchaseResult Purchase(HeroId hero)=>Purchase(hero,0);

        public static AvatarPurchaseResult Purchase(HeroId hero,int variant)
        {
            if(!Released(hero))return AvatarPurchaseResult.InvalidItem;
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


