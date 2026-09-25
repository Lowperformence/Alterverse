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

        public WeaponSkinItem(HeroId hero,string name,string weaponName,string description,Color glow)
        {
            Hero=hero;Name=name;WeaponName=weaponName;Description=description;Glow=glow;
        }
    }

    public static class WeaponSkinCatalog
    {
        public const int Price=500;

        public static readonly WeaponSkinItem[] Items=
        {
            new(HeroId.Veyr,"ECLIPSE CRESCENTS // EPIC","Eclipse Crescents",
                "Paired black-steel crescent sickles with separated violet inner edges and luminous heart guards.",
                new Color(.72f,.16f,1f)),
            new(HeroId.Kaelon,"QUAKE CROWN // EPIC","Quake Crown",
                "Twin-jawed impact hammer with a suspended amber reactor and reinforced strike crown.",
                new Color(1f,.72f,.12f)),
            new(HeroId.Nyra,"COMET LANCE // EPIC","Comet Lance Rifle",
                "Long-range pulse rifle with separate red energy rails, a vented muzzle and charged optic.",
                new Color(1f,.04f,.025f)),
            new(HeroId.Elyra,"PRISM ASCENT // EPIC","Prism Ascent",
                "An upright faceted prism focus held between two angled orbital frames.",
                new Color(.70f,.24f,1f)),
            new(HeroId.Mira,"SOLAR BLOOM // LEGENDARY","Solar Bloom Scepter",
                "Gold ceremonial scepter with unfolding sun petals, a floating halo and warm light core.",
                new Color(.58f,.96f,1f)),
            new(HeroId.Rook,"RIFT TALONS // EPIC","Rift Talons",
                "Paired armored fists with three forward talons and energized cutting channels.",
                new Color(1f,.035f,.02f)),
            new(HeroId.Traveler,"PARADOX COMPASS // EPIC","Paradox Compass",
                "A suspended temporal needle inside opposing orbital frames with a bright violet core.",
                new Color(1f,.98f,.90f))
        };

        public static WeaponSkinItem For(HeroId hero)=>Items[Mathf.Clamp((int)hero,0,Items.Length-1)];
    }

    public static class WeaponSkinInventory
    {
        private static string OwnedKey(HeroId hero)=>"DF_WeaponSkinOwned_"+(int)hero;
        private static string EquippedKey(HeroId hero)=>"DF_WeaponSkinEquipped_"+(int)hero;

        // Compatibility with the short-lived three-tier Zhaev collection.
        private static string LegacyVariantOwnedKey(HeroId hero,int tier)=>$"DF_WeaponSkinOwned_{(int)hero}_T{tier}";
        private static string LegacyVariantEquippedKey(HeroId hero)=>$"DF_WeaponSkinEquippedVariant_{(int)hero}";

        public static bool Owned(HeroId hero)
        {
            if(PlayerPrefs.GetInt(OwnedKey(hero),0)==1)return true;
            if(hero!=HeroId.Veyr)return false;
            return PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,1),0)==1||
                   PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,2),0)==1||
                   PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,3),0)==1;
        }

        public static bool Equipped(HeroId hero)
        {
            if(!Owned(hero))return false;
            if(PlayerPrefs.GetInt(EquippedKey(hero),0)==1)return true;
            if(hero!=HeroId.Veyr)return false;
            int legacyTier=Mathf.Clamp(PlayerPrefs.GetInt(LegacyVariantEquippedKey(hero),0),0,3);
            return legacyTier>0&&PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,legacyTier),0)==1;
        }

        public static AvatarPurchaseResult Purchase(HeroId hero)
        {
            ProfileTransaction.Recover();
            if(Owned(hero))return AvatarPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(WeaponSkinCatalog.Price))return AvatarPurchaseResult.InsufficientShards;

            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-WeaponSkinCatalog.Price),
                new ProfileTransaction.Write(OwnedKey(hero),1),
                new ProfileTransaction.Write(EquippedKey(hero),1));
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }

        public static bool Equip(HeroId hero,bool enabled)
        {
            ProfileTransaction.Recover();
            if(enabled&&!Owned(hero))return false;

            if(hero==HeroId.Veyr)
            {
                ProfileTransaction.Commit(
                    new ProfileTransaction.Write(EquippedKey(hero),enabled?1:0),
                    new ProfileTransaction.Write(LegacyVariantEquippedKey(hero),0));
            }
            else
            {
                ProfileTransaction.Commit(new ProfileTransaction.Write(EquippedKey(hero),enabled?1:0));
            }
            return true;
        }
    }
}
