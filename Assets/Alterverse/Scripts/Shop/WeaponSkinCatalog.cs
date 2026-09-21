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
            new(HeroId.Veyr,"NIGHT BLOSSOM // OVERDRIVE","Twin Void Blades",
                "Dark black-steel dual blades with split guards and violet void energy.",
                new Color(.72f,.16f,1f)),
            new(HeroId.Kaelon,"SIEGE BREAKER // OVERDRIVE","Breacher Hammer",
                "Blackened siege steel, reinforced impact caps and an amber energy spine matched to Auren Kest's heavy breacher armor.",
                new Color(1f,.72f,.12f)),
            new(HeroId.Nyra,"RED JUDGEMENT // EPIC","Red Judgement Precision Rifle",
                "Extended black-steel marksman rifle with split muzzle armor, twin red energy rails and upgraded targeting optics.",
                new Color(1f,.04f,.025f)),
            new(HeroId.Elyra,"ASTRAL ORBIT // EPIC","Astral Crystal Staff",
                "A void-lit crystal focus with silver orbital rings, suspended shards and a deep violet stellar core.",
                new Color(.70f,.24f,1f)),
            new(HeroId.Mira,"ASCENSION OF AURORA // LEGENDARY","Ascendant Light Staff",
                "Ceremonial pearl-and-gold healing staff with layered cyan halos, an enlarged light crystal and floating crown geometry.",
                new Color(.58f,.96f,1f)),
            new(HeroId.Rook,"REACTOR BREAKER // EPIC","Reactor Breaker Gauntlets",
                "Reinforced yellow-black impact gauntlets with a dominant right-hand rack of four glowing red reactor claws, inspired by the supplied reference.",
                new Color(1f,.035f,.02f))
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
