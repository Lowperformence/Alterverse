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
        {Hero=hero;Name=name;WeaponName=weaponName;Description=description;Glow=glow;}
    }

    public static class WeaponSkinCatalog
    {
        public const int Price=500;
        public static readonly WeaponSkinItem[] Items=
        {
            new(HeroId.Veyr,"NULL EDGE // OVERDRIVE","Twin Rift Blade","Forged dark-metal twin blades with dimensional edge channels, reinforced guards and reactive void cores.",new Color(1f,.16f,.86f)),
            new(HeroId.Kaelon,"AEGIS BREAKER // OVERDRIVE","Anchor Hammer","Rebuilt fortress hammer with crown plates, a central reactor spine and reactive gold energy core.",new Color(1f,.72f,.12f)),
            new(HeroId.Nyra,"STARLINE // OVERDRIVE","Precision Rifle","Premium rail rifle with twin energy rails, armored receiver shroud and energized muzzle core.",new Color(.08f,.94f,1f)),
            new(HeroId.Elyra,"RIFT CATALYST // OVERDRIVE","Orbit Catalysts","Upgraded orbital catalysts with armored focus blades and a reactive dimensional core.",new Color(.68f,.24f,1f)),
            new(HeroId.Mira,"LIFEWEAVE // OVERDRIVE","Support Staff","Refined life-energy staff with a split crown, luminous channel and reactive crystal core.",new Color(.12f,1f,.48f)),
            new(HeroId.Rook,"BREAKER FIST // OVERDRIVE","Breaker Gauntlet","Heavy overdrive gauntlets with reinforced fist plates, powered knuckles and molten reactor channels.",new Color(1f,.32f,.08f))
        };

        public static WeaponSkinItem For(HeroId hero)=>Items[Mathf.Clamp((int)hero,0,Items.Length-1)];
    }

    public static class WeaponSkinInventory
    {
        private static string OwnedKey(HeroId hero)=>"DF_WeaponSkinOwned_"+(int)hero;
        private static string EquippedKey(HeroId hero)=>"DF_WeaponSkinEquipped_"+(int)hero;

        public static bool Owned(HeroId hero)=>PlayerPrefs.GetInt(OwnedKey(hero),0)==1;
        public static bool Equipped(HeroId hero)=>Owned(hero)&&PlayerPrefs.GetInt(EquippedKey(hero),0)==1;

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
            ProfileTransaction.Commit(new ProfileTransaction.Write(EquippedKey(hero),enabled?1:0));
            return true;
        }
    }
}
