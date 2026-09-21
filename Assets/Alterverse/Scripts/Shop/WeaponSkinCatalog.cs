using Alterverse.Characters;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public enum WeaponSkinTier { Original=0, Rare=1, Epic=2, Legendary=3 }

    public readonly struct WeaponSkinItem
    {
        public readonly HeroId Hero;
        public readonly string Name;
        public readonly string WeaponName;
        public readonly string Description;
        public readonly Color Glow;
        public readonly WeaponSkinTier Tier;
        public readonly int Price;

        public WeaponSkinItem(HeroId hero,string name,string weaponName,string description,Color glow,
            WeaponSkinTier tier=WeaponSkinTier.Legendary,int price=500)
        {
            Hero=hero;Name=name;WeaponName=weaponName;Description=description;Glow=glow;Tier=tier;Price=price;
        }
    }

    public static class WeaponSkinCatalog
    {
        // Backward-compatible default price for the five heroes that still have one overdrive skin.
        public const int Price=500;

        public static readonly WeaponSkinItem[] VeyrSkins=
        {
            new(HeroId.Veyr,"VOID EDGE // RARE","Twin Void Blades",
                "Sharper black-steel blades with a brighter violet edge.",
                new Color(.56f,.12f,.90f),WeaponSkinTier.Rare,350),
            new(HeroId.Veyr,"NIGHT BLOSSOM // EPIC","Twin Void Blades",
                "Ornamented shadow blades with a void gem and split guards.",
                new Color(.72f,.16f,1f),WeaponSkinTier.Epic,500),
            new(HeroId.Veyr,"OBLIVION // LEGENDARY","Twin Void Blades",
                "Black steel, split void guards, floating energy halos and the strongest shadow core.",
                new Color(.86f,.24f,1f),WeaponSkinTier.Legendary,750)
        };

        public static readonly WeaponSkinItem[] Items=
        {
            VeyrSkins[2],
            new(HeroId.Kaelon,"AEGIS BREAKER // OVERDRIVE","Anchor Hammer","",new Color(1f,.72f,.12f)),
            new(HeroId.Nyra,"STARLINE // OVERDRIVE","Precision Rifle","",new Color(.08f,.94f,1f)),
            new(HeroId.Elyra,"RIFT CATALYST // OVERDRIVE","Orbit Catalysts","",new Color(1f,.04f,.02f)),
            new(HeroId.Mira,"LIFEWEAVE // OVERDRIVE","Support Staff","",new Color(.12f,1f,.48f)),
            new(HeroId.Rook,"BREAKER FIST // OVERDRIVE","Breaker Gauntlet","",new Color(1f,.32f,.08f))
        };

        public static WeaponSkinItem For(HeroId hero)
        {
            if(hero==HeroId.Veyr)
            {
                WeaponSkinTier equipped=WeaponSkinInventory.EquippedTier(hero);
                return For(hero,equipped==WeaponSkinTier.Original?WeaponSkinTier.Legendary:equipped);
            }
            return Items[Mathf.Clamp((int)hero,0,Items.Length-1)];
        }

        public static WeaponSkinItem For(HeroId hero,WeaponSkinTier tier)
        {
            if(hero!=HeroId.Veyr)return For(hero);
            int index=Mathf.Clamp((int)tier-1,0,VeyrSkins.Length-1);
            return VeyrSkins[index];
        }
    }

    public static class WeaponSkinInventory
    {
        private static string OwnedKey(HeroId hero)=>"DF_WeaponSkinOwned_"+(int)hero;
        private static string EquippedKey(HeroId hero)=>"DF_WeaponSkinEquipped_"+(int)hero;
        private static string VariantOwnedKey(HeroId hero,WeaponSkinTier tier)=>$"DF_WeaponSkinOwned_{(int)hero}_T{(int)tier}";
        private static string EquippedVariantKey(HeroId hero)=>$"DF_WeaponSkinEquippedVariant_{(int)hero}";

        public static bool Owned(HeroId hero)
        {
            if(hero!=HeroId.Veyr)return PlayerPrefs.GetInt(OwnedKey(hero),0)==1;
            return Owned(hero,WeaponSkinTier.Rare)||Owned(hero,WeaponSkinTier.Epic)||Owned(hero,WeaponSkinTier.Legendary);
        }

        public static bool Owned(HeroId hero,WeaponSkinTier tier)
        {
            if(tier==WeaponSkinTier.Original)return true;
            if(hero!=HeroId.Veyr)return PlayerPrefs.GetInt(OwnedKey(hero),0)==1;

            // Preserve the old single NULL EDGE purchase as ownership of the new top-tier replacement.
            if(tier==WeaponSkinTier.Legendary&&PlayerPrefs.GetInt(OwnedKey(hero),0)==1)return true;
            return PlayerPrefs.GetInt(VariantOwnedKey(hero,tier),0)==1;
        }

        public static WeaponSkinTier EquippedTier(HeroId hero)
        {
            if(hero!=HeroId.Veyr)
                return PlayerPrefs.GetInt(OwnedKey(hero),0)==1&&PlayerPrefs.GetInt(EquippedKey(hero),0)==1
                    ?WeaponSkinTier.Legendary:WeaponSkinTier.Original;

            int saved=PlayerPrefs.GetInt(EquippedVariantKey(hero),0);
            WeaponSkinTier tier=(WeaponSkinTier)Mathf.Clamp(saved,0,3);
            if(tier!=WeaponSkinTier.Original&&Owned(hero,tier))return tier;

            // Legacy equipped NULL EDGE becomes OBLIVION so existing users keep their cosmetic.
            if(PlayerPrefs.GetInt(OwnedKey(hero),0)==1&&PlayerPrefs.GetInt(EquippedKey(hero),0)==1)
                return WeaponSkinTier.Legendary;
            return WeaponSkinTier.Original;
        }

        public static bool Equipped(HeroId hero)=>EquippedTier(hero)!=WeaponSkinTier.Original;

        public static AvatarPurchaseResult Purchase(HeroId hero)
        {
            return hero==HeroId.Veyr?Purchase(hero,WeaponSkinTier.Legendary):PurchaseSingle(hero);
        }

        public static AvatarPurchaseResult Purchase(HeroId hero,WeaponSkinTier tier)
        {
            if(hero!=HeroId.Veyr)return PurchaseSingle(hero);
            if(tier==WeaponSkinTier.Original)return AvatarPurchaseResult.AlreadyOwned;

            ProfileTransaction.Recover();
            if(Owned(hero,tier))return AvatarPurchaseResult.AlreadyOwned;
            WeaponSkinItem item=WeaponSkinCatalog.For(hero,tier);
            if(!PlayerWallet.CanAfford(item.Price))return AvatarPurchaseResult.InsufficientShards;

            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-item.Price),
                new ProfileTransaction.Write(VariantOwnedKey(hero,tier),1),
                new ProfileTransaction.Write(EquippedVariantKey(hero),(int)tier),
                new ProfileTransaction.Write(EquippedKey(hero),1));
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }

        private static AvatarPurchaseResult PurchaseSingle(HeroId hero)
        {
            ProfileTransaction.Recover();
            if(PlayerPrefs.GetInt(OwnedKey(hero),0)==1)return AvatarPurchaseResult.AlreadyOwned;
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
            if(hero==HeroId.Veyr)
            {
                if(!enabled)
                {
                    ProfileTransaction.Commit(
                        new ProfileTransaction.Write(EquippedVariantKey(hero),0),
                        new ProfileTransaction.Write(EquippedKey(hero),0));
                    return true;
                }

                WeaponSkinTier tier=EquippedTier(hero);
                if(tier==WeaponSkinTier.Original)
                {
                    if(Owned(hero,WeaponSkinTier.Legendary))tier=WeaponSkinTier.Legendary;
                    else if(Owned(hero,WeaponSkinTier.Epic))tier=WeaponSkinTier.Epic;
                    else if(Owned(hero,WeaponSkinTier.Rare))tier=WeaponSkinTier.Rare;
                    else return false;
                }
                return Equip(hero,tier);
            }

            if(enabled&&PlayerPrefs.GetInt(OwnedKey(hero),0)!=1)return false;
            ProfileTransaction.Commit(new ProfileTransaction.Write(EquippedKey(hero),enabled?1:0));
            return true;
        }

        public static bool Equip(HeroId hero,WeaponSkinTier tier)
        {
            ProfileTransaction.Recover();
            if(hero!=HeroId.Veyr)return Equip(hero,tier!=WeaponSkinTier.Original);
            if(tier!=WeaponSkinTier.Original&&!Owned(hero,tier))return false;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write(EquippedVariantKey(hero),(int)tier),
                new ProfileTransaction.Write(EquippedKey(hero),tier==WeaponSkinTier.Original?0:1));
            return true;
        }
    }
}
