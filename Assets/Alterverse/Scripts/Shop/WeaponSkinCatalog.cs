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
            Hero=hero;Name=name;WeaponName=weaponName;Description=description;Glow=glow;Variant=variant;Price=price;
        }
    }

    public static class WeaponSkinCatalog
    {
        public const int Price=500;
        public const int PremiumPrice=1100;

        // Variant zero keeps the original catalog entry and its existing 500 shard price.
        // Variant one is a second, silhouette-changing cosmetic for each hero.
        public static readonly WeaponSkinItem[] Items=
        {
            new(HeroId.Veyr,"ECLIPSE CRESCENTS // EPIC","Eclipse Crescents","Paired black-steel crescent sickles with separated violet inner edges and luminous heart guards.",new Color(.72f,.16f,1f)),
            new(HeroId.Kaelon,"QUAKE CROWN // EPIC","Quake Crown","Twin-jawed impact hammer with a suspended amber reactor and reinforced strike crown.",new Color(1f,.72f,.12f)),
            new(HeroId.Nyra,"COMET LANCE // EPIC","Comet Lance Rifle","Long-range pulse rifle with separate red energy rails, a vented muzzle and charged optic.",new Color(1f,.04f,.025f)),
            new(HeroId.Elyra,"PRISM ASCENT // EPIC","Prism Ascent","An upright faceted prism focus held between two angled orbital frames.",new Color(.70f,.24f,1f)),
            new(HeroId.Mira,"SOLAR BLOOM // LEGENDARY","Solar Bloom Scepter","Gold ceremonial scepter with unfolding sun petals, a floating halo and warm light core.",new Color(.58f,.96f,1f)),
            new(HeroId.Rook,"RIFT TALONS // EPIC","Rift Talons","Paired armored fists with three forward talons and energized cutting channels.",new Color(1f,.035f,.02f)),
            new(HeroId.Traveler,"PARADOX COMPASS // EPIC","Paradox Compass","A suspended temporal needle inside opposing orbital frames with a bright violet core.",new Color(1f,.98f,.90f)),

            new(HeroId.Veyr,"DUSKGLASS FANGS // LEGENDARY","Duskglass Fangs","Twin hooked blades with dark mirror-steel shells and a narrow violet phase seam.",new Color(.80f,.30f,1f),1,PremiumPrice),
            new(HeroId.Kaelon,"AMETHYST BREACHER // LEGENDARY","Amethyst Breacher","A gravity maul with offset impact plates around a contained violet reactor.",new Color(.72f,.22f,1f),1,PremiumPrice),
            new(HeroId.Nyra,"AFTERBURN VECTOR // LEGENDARY","Afterburn Vector","A precision rifle with a split thermal barrel, recoil fins and a bright tracking optic.",new Color(1f,.20f,.08f),1,PremiumPrice),
            new(HeroId.Elyra,"NULLSTAR RELIQUARY // LEGENDARY","Nullstar Reliquary","A rune-cut arcane focus suspended between a compact triad of orbital rings.",new Color(.55f,.24f,1f),1,PremiumPrice),
            new(HeroId.Mira,"TIDELIGHT CONDUCTOR // LEGENDARY","Tidelight Conductor","A pearl-and-blue support staff with a forked wave crown and a suspended healing lens.",new Color(.18f,.78f,1f),1,PremiumPrice),
            new(HeroId.Rook,"RIFTBREAKER // LEGENDARY","Riftbreaker","Paired impact gauntlets with piston knuckles and forward-projecting energy rails.",new Color(1f,.20f,.055f),1,PremiumPrice),
            new(HeroId.Traveler,"CHRONOLUX SCEPTER // LEGENDARY","Chronolux Scepter","A white-gold time staff with a floating core and red-edged temporal rings.",new Color(1f,.58f,.28f),1,PremiumPrice)
        };

        public static WeaponSkinItem For(HeroId hero)=>For(hero,0);
        public static WeaponSkinItem For(HeroId hero,int variant)
        {
            for(int i=0;i<Items.Length;i++)if(Items[i].Hero==hero&&Items[i].Variant==variant)return Items[i];
            return Items[0];
        }
    }

    public static class WeaponSkinInventory
    {
        private static string OwnedKey(HeroId hero)=>"DF_WeaponSkinOwned_"+(int)hero;
        private static string EquippedKey(HeroId hero)=>"DF_WeaponSkinEquipped_"+(int)hero;
        private static string VariantOwnedKey(HeroId hero,int variant)=>$"DF_WeaponSkinOwnedItem_{(int)hero}_{variant}";
        private static string SelectedVariantKey(HeroId hero)=>$"DF_WeaponSkinSelected_{(int)hero}";

        // Compatibility with the short-lived three-tier Zhaev collection.
        private static string LegacyVariantOwnedKey(HeroId hero,int tier)=>$"DF_WeaponSkinOwned_{(int)hero}_T{tier}";
        private static string LegacyVariantEquippedKey(HeroId hero)=>$"DF_WeaponSkinEquippedVariant_{(int)hero}";

        public static bool Owned(HeroId hero,int variant)
        {
            if(variant==0&&PlayerPrefs.GetInt(OwnedKey(hero),0)==1)return true;
            if(PlayerPrefs.GetInt(VariantOwnedKey(hero,variant),0)==1)return true;
            if(hero==HeroId.Veyr&&variant==0)
                return PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,1),0)==1||
                       PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,2),0)==1||
                       PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,3),0)==1;
            return false;
        }

        public static bool Owned(HeroId hero)=>Owned(hero,0)||Owned(hero,1);

        public static int EquippedVariant(HeroId hero)
        {
            if(PlayerPrefs.HasKey(SelectedVariantKey(hero)))
            {
                int selected=PlayerPrefs.GetInt(SelectedVariantKey(hero),-1);
                return selected>=0&&Owned(hero,selected)?selected:-1;
            }
            if(PlayerPrefs.GetInt(EquippedKey(hero),0)==1&&Owned(hero,0))return 0;
            if(hero==HeroId.Veyr)
            {
                int legacyTier=Mathf.Clamp(PlayerPrefs.GetInt(LegacyVariantEquippedKey(hero),0),0,3);
                if(legacyTier>0&&Owned(hero,0))return 0;
            }
            return -1;
        }

        public static bool Equipped(HeroId hero)=>EquippedVariant(hero)>=0;

        public static AvatarPurchaseResult Purchase(HeroId hero)=>Purchase(hero,0);
        public static AvatarPurchaseResult Purchase(HeroId hero,int variant)
        {
            ProfileTransaction.Recover();
            WeaponSkinItem item=WeaponSkinCatalog.For(hero,variant);
            if(Owned(hero,variant))return AvatarPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(item.Price))return AvatarPurchaseResult.InsufficientShards;

            var writes=new System.Collections.Generic.List<ProfileTransaction.Write>
            {
                new("DF_Shards",PlayerWallet.Balance-item.Price),
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