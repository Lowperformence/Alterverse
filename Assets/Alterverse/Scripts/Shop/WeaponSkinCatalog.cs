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

        // Variants 0/1 are the existing collection. Variant 1 is the premium LEGENDARY tier:
        // each hero receives a prestige silhouette and animated presentation pass while gameplay
        // remains unchanged. Variants 2/3 are 500-shard EPIC alternatives.
        public static readonly WeaponSkinItem[] Items=
        {
            new(HeroId.Veyr,"ECLIPSE CRESCENTS // EPIC","Eclipse Crescents","Paired black-steel crescent sickles with separated violet inner edges and luminous heart guards.",new Color(.72f,.16f,1f)),
            new(HeroId.Kaelon,"QUAKE CROWN // EPIC","Quake Crown","Twin-jawed impact hammer with a suspended amber reactor and reinforced strike crown.",new Color(1f,.72f,.12f)),
            new(HeroId.Nyra,"COMET LANCE // EPIC","Comet Lance Rifle","Long-range pulse rifle with separate red energy rails, a vented muzzle and charged optic.",new Color(1f,.04f,.025f)),
            new(HeroId.Elyra,"PRISM ASCENT // EPIC","Prism Ascent","An upright faceted prism focus held between two angled orbital frames.",new Color(.70f,.24f,1f)),
            new(HeroId.Mira,"SOLAR BLOOM // EPIC","Solar Bloom Scepter","Gold ceremonial scepter with unfolding sun petals, a floating halo and warm light core.",new Color(.58f,.96f,1f)),
            new(HeroId.Rook,"RIFT TALONS // EPIC","Rift Talons","Paired armored fists with three forward talons and energized cutting channels.",new Color(1f,.035f,.02f)),
            new(HeroId.Traveler,"PARADOX COMPASS // EPIC","Paradox Compass","A suspended temporal needle inside opposing orbital frames with a bright violet core.",new Color(1f,.98f,.90f)),

            new(HeroId.Veyr,"ECLIPSE SERAPH // LEGENDARY","Eclipse Seraph Twins","Twin celestial phase blades wrapped in broken rotating eclipse halos, floating phase shards and crown-metal guards.",new Color(.80f,.30f,1f),1,PremiumPrice),
            new(HeroId.Kaelon,"WORLDCRUSHER // LEGENDARY","Worldcrusher","A colossal melee gravity maul with oversized strike faces, counter-rotating gravity cages and a suspended reactor heart.",new Color(.72f,.22f,1f),1,PremiumPrice),
            new(HeroId.Nyra,"STARPIERCER // LEGENDARY","Starpiercer Rail Rifle","A prestige rail rifle with separated luminous rails, animated charge collars, a crown optic and an oversized energy muzzle halo.",new Color(1f,.20f,.08f),1,PremiumPrice),
            new(HeroId.Elyra,"SINGULARITY CROWN // LEGENDARY","Singularity Crown Staff","A full-length mage staff crowned by three counter-rotating orbital frames around a contained black-hole core and royal rune prongs.",new Color(.55f,.24f,1f),1,PremiumPrice),
            new(HeroId.Mira,"AURELIA SERAPH // LEGENDARY","Aurelia Seraph Staff","A ceremonial healing staff with a huge double halo, six pulsing light petals and a suspended radiant healing sun.",new Color(.18f,.78f,1f),1,PremiumPrice),
            new(HeroId.Rook,"TITANFORGE // LEGENDARY","Titanforge Gauntlets","Paired powered gauntlets with enlarged armor, reactor knuckles, animated energy rings and floating pressure crowns.",new Color(1f,.20f,.055f),1,PremiumPrice),
            new(HeroId.Traveler,"LAST HOUR // LEGENDARY","Last Hour Chronostaff","A royal temporal staff built around an hourglass core, three opposing clock rings and floating time shards.",new Color(1f,.58f,.28f),1,PremiumPrice),

            // New 500-shard EPIC collection. Each weapon has a unique silhouette/signature motion.
            new(HeroId.Veyr,"VOIDFANG TWINS // EPIC","Voidfang Twins","Asymmetric long-and-short phase blades with split guards and a violet dimensional seam that makes the pair readable at a glance.",new Color(.63f,.10f,1f),2),
            new(HeroId.Kaelon,"SIEGEBREAKER // EPIC","Siegebreaker","A huge two-handed industrial melee hammer with a hydraulic strike cage, layered armor cheeks and a pressure-core crown.",new Color(1f,.43f,.08f),2),
            new(HeroId.Nyra,"STORMCOIL // EPIC","Stormcoil Rifle","A long coil rifle built around separated electromagnetic rails, rotating charge collars and a bright storm capacitor.",new Color(.16f,.72f,1f),2),
            new(HeroId.Elyra,"ASTRAL ORRERY // EPIC","Astral Orrery Staff","A full-length mage staff crowned by nested orbital rings, floating crystal planets and a luminous astral heart.",new Color(.36f,.54f,1f),2),
            new(HeroId.Mira,"SERAPH HALO // EPIC","Seraph Halo","A ceremonial support staff whose open halo crown carries rotating light vanes around a suspended healing core.",new Color(1f,.90f,.48f),2),
            new(HeroId.Rook,"TITAN BREAKERS // EPIC","Titan Breakers","Oversized mechanical fist armor with visible piston knuckles, reinforced impact plates and energized pressure rails.",new Color(1f,.54f,.10f),2),
            new(HeroId.Traveler,"PARADOX ENGINE // EPIC","Paradox Engine","A modular temporal weapon whose armored segments appear suspended around a bright chronal core and opposing orbit frames.",new Color(.74f,.34f,1f),2),

            new(HeroId.Veyr,"ECLIPSE CHAKRAMS // EPIC","Eclipse Chakrams","Paired open-ring sickle weapons with broken crescent blades, offset grips and razor-bright eclipse edges.",new Color(1f,.12f,.45f),3),
            new(HeroId.Kaelon,"AEGIS CRUSHER // EPIC","Aegis Crusher","A massive close-range reactor maul with a shield-generator ring around the head and broad armored crushing faces.",new Color(.20f,.82f,1f),3),
            new(HeroId.Nyra,"PREDATOR SPINE // EPIC","Predator Spine Rifle","A biomechanical marksman rifle built from a segmented spine, ribbed receiver and jaw-like muzzle plates.",new Color(.28f,1f,.38f),3),
            new(HeroId.Elyra,"RIFT LANTERN // EPIC","Rift Lantern Staff","A long angular mage staff carrying a caged dimensional lantern whose split crown frames a pulsing void core.",new Color(.82f,.12f,1f),3),
            new(HeroId.Mira,"LIFEWEAVER BLOOM // EPIC","Lifeweaver Bloom","An organic-mechanical healing staff with layered petals that frame a living light seed and branching luminous veins.",new Color(.38f,1f,.62f),3),
            new(HeroId.Rook,"MAGMA KNUCKLES // EPIC","Magma Knuckles","Obsidian fist armor formed from broken volcanic plates with glowing magma fissures and brutal blocky strike faces.",new Color(1f,.22f,.025f),3),
            new(HeroId.Traveler,"EVENT HORIZON // EPIC","Event Horizon","A black-hole weapon built around a dark central singularity, asymmetric gravity fins and two luminous lens rings.",new Color(.24f,.48f,1f),3)
        };

        public static WeaponSkinItem For(HeroId hero)=>For(hero,0);
        public static WeaponSkinItem For(HeroId hero,int variant)
        {
            for(int i=0;i<Items.Length;i++)if(Items[i].Hero==hero&&Items[i].Variant==variant)return Items[i];
            for(int i=0;i<Items.Length;i++)if(Items[i].Hero==hero)return Items[i];
            return Items[0];
        }

        public static bool Contains(HeroId hero,int variant)
        {
            for(int i=0;i<Items.Length;i++)if(Items[i].Hero==hero&&Items[i].Variant==variant)return true;
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

        // Compatibility with the short-lived three-tier Zhaev collection.
        private static string LegacyVariantOwnedKey(HeroId hero,int tier)=>$"DF_WeaponSkinOwned_{(int)hero}_T{tier}";
        private static string LegacyVariantEquippedKey(HeroId hero)=>$"DF_WeaponSkinEquippedVariant_{(int)hero}";

        public static bool Owned(HeroId hero,int variant)
        {
            if(!WeaponSkinCatalog.Contains(hero,variant))return false;
            if(variant==0&&PlayerPrefs.GetInt(OwnedKey(hero),0)==1)return true;
            if(PlayerPrefs.GetInt(VariantOwnedKey(hero,variant),0)==1)return true;
            if(hero==HeroId.Veyr&&variant==0)
                return PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,1),0)==1||
                       PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,2),0)==1||
                       PlayerPrefs.GetInt(LegacyVariantOwnedKey(hero,3),0)==1;
            return false;
        }

        public static bool Owned(HeroId hero)
        {
            for(int i=0;i<WeaponSkinCatalog.Items.Length;i++)
            {
                WeaponSkinItem item=WeaponSkinCatalog.Items[i];
                if(item.Hero==hero&&Owned(hero,item.Variant))return true;
            }
            return false;
        }

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
            if(!WeaponSkinCatalog.Contains(hero,variant))return AvatarPurchaseResult.AlreadyOwned;
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
