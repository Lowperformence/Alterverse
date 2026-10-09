#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Profile;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Shop
{
    public static class ArenaShopChecks
    {
        private const string ShardsKey="DF_Shards";

        private readonly struct IntPrefSnapshot
        {
            public readonly string Key;
            public readonly bool HadValue;
            public readonly int Value;
            public IntPrefSnapshot(string key)
            {
                Key=key;
                HadValue=Alterverse.Profile.AccountProfilePrefs.HasKey(key);
                Value=Alterverse.Profile.AccountProfilePrefs.GetInt(key,0);
            }
            public void Restore()
            {
                if(HadValue)Alterverse.Profile.AccountProfilePrefs.SetInt(Key,Value);
                else Alterverse.Profile.AccountProfilePrefs.DeleteKey(Key);
            }
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Arena purchases (non-destructive)")]
        public static void Run()
        {
            if(AccountProfilePrefs.Attached||PaidShardCommerce.Instance!=null)
                throw new InvalidOperationException("Run purchase fixtures with cloud account and commerce disconnected.");
            IntPrefSnapshot shards=new(ShardsKey);
            IntPrefSnapshot earthOwned=new(ArenaUnlockInventory.EarthOwnedKey);
            IntPrefSnapshot darkOwned=new(ArenaUnlockInventory.Dark00OwnedKey);
            IntPrefSnapshot moor=new(ArenaUnlockInventory.MoorOwnedKey),pyraxis=new(ArenaUnlockInventory.PyraxisOwnedKey),psr=new(ArenaUnlockInventory.PsrOwnedKey);
            IntPrefSnapshot selected=new("DF_SelectedArenaVariant"),shuffle=new("DF_ArenaShuffle");
            IntPrefSnapshot wasp=new(ArenaUnlockInventory.Wasp76bOwnedKey),trappist=new(ArenaUnlockInventory.Trappist1eOwnedKey);
            try
            {
                CheckPaidArena(ArenaVariant.GreenPlanet,ArenaUnlockInventory.MoorOwnedKey,900);
                CheckPaidArena(ArenaVariant.Pyraxis,ArenaUnlockInventory.PyraxisOwnedKey,900);
                CheckPaidArena(ArenaVariant.DiamondCore,ArenaUnlockInventory.PsrOwnedKey,900);
                CheckPaidArena(ArenaVariant.Wasp76b,ArenaUnlockInventory.Wasp76bOwnedKey,900);
                CheckPaidArena(ArenaVariant.Trappist1e,ArenaUnlockInventory.Trappist1eOwnedKey,900);
                CheckEarthPriceAndLock();
                CheckEarthInsufficientShards();
                CheckEarthExactPurchaseAndDuplicateProtection();
                CheckDarkPriceAndLock();
                CheckDarkInsufficientShards();
                CheckDarkExactPurchaseAndDuplicateProtection();
                Debug.Log("DIMENSIONS FRONT: arena purchases passed, including WASP-76b and TRAPPIST-1e at 900 shards each.");
            }
            finally
            {
                moor.Restore();pyraxis.Restore();psr.Restore();selected.Restore();shuffle.Restore();
                wasp.Restore();trappist.Restore();
                shards.Restore();
                earthOwned.Restore();
                darkOwned.Restore();
                Alterverse.Profile.AccountProfilePrefs.Save();
            }
        }

        private static void CheckPaidArena(ArenaVariant variant,string key,int price)
        {
            ResetArena(key,price-1);
            Assert(ArenaShopCatalog.For(variant).Price==price,"Shop price mismatch: "+variant);
            Assert(System.Array.Exists(ArenaShopCatalog.PurchasableItems,item=>item.Variant==variant&&item.Price==price),"Paid map missing from STORE: "+variant);
            Assert(ArenaUnlockInventory.UnlockRequirement(variant)==price+" SHARDS","Unlock requirement mismatch: "+variant);
            Assert(!ArenaUnlockInventory.Owned(variant)&&!ArenaVariantRuntime.CanSelect(variant),"Unowned map must stay locked: "+variant);
            Assert(ArenaUnlockInventory.Purchase(variant)==ArenaPurchaseResult.InsufficientShards,"Insufficient balance must be rejected.");
            Assert(PlayerWallet.Balance==price-1&&!ArenaUnlockInventory.Owned(variant),"Rejected purchase must preserve balance and ownership.");
            Alterverse.Profile.AccountProfilePrefs.SetInt(ShardsKey,price);
            Assert(ArenaUnlockInventory.Purchase(variant)==ArenaPurchaseResult.Purchased,"Exact balance must purchase map.");
            Assert(PlayerWallet.Balance==0&&ArenaUnlockInventory.Owned(variant),"Purchase must debit exact price and grant ownership.");
            Assert(AccountProfilePrefs.GetInt(key,0)==1,"Purchase must save the map entitlement: "+variant);
            Assert(ArenaVariantRuntime.Select(variant)&&ArenaVariantRuntime.Selected==variant,"Purchased map must be selectable.");
            Assert(ArenaUnlockInventory.Purchase(variant)==ArenaPurchaseResult.AlreadyOwned&&PlayerWallet.Balance==0,"Duplicate purchase must never debit again.");
        }

        private static void ResetArena(string ownedKey,int shards)
        {
            Alterverse.Profile.AccountProfilePrefs.SetInt(ShardsKey,shards);
            Alterverse.Profile.AccountProfilePrefs.DeleteKey(ownedKey);
            Alterverse.Profile.AccountProfilePrefs.Save();
        }

        private static void CheckEarthPriceAndLock()
        {
            ResetArena(ArenaUnlockInventory.EarthOwnedKey,900);
            ArenaShopItem earth=ArenaShopCatalog.For(ArenaVariant.Earth);
            Assert(ArenaShopCatalog.EarthPrice==900,"EarthPrice must be exactly 900.");
            Assert(earth.Price==900,"Earth shop card must display the 900-shard price.");
            Assert(ArenaUnlockInventory.UnlockRequirement(ArenaVariant.Earth)=="900 SHARDS","Earth requirement text must show 900 SHARDS.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Earth must start locked without its entitlement.");
            Assert(!ArenaVariantRuntime.CanSelect(ArenaVariant.Earth),"Locked Earth must not be selectable for a match.");
            Assert(ArenaShopCatalog.For(ArenaVariant.Noxara).Price==ArenaShopCatalog.NoxaraPrice&&ArenaShopCatalog.NoxaraPrice==900,"Noxara must cost exactly 900 shards.");
        }

        private static void CheckEarthInsufficientShards()
        {
            ResetArena(ArenaUnlockInventory.EarthOwnedKey,899);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.InsufficientShards,"899 shards must not unlock Earth.");
            Assert(PlayerWallet.Balance==899,"Failed Earth purchase must not deduct shards.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Failed Earth purchase must not grant ownership.");
        }

        private static void CheckEarthExactPurchaseAndDuplicateProtection()
        {
            ResetArena(ArenaUnlockInventory.EarthOwnedKey,900);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.Purchased,"900 shards must unlock Earth.");
            Assert(PlayerWallet.Balance==0,"Earth purchase must deduct exactly 900 shards.");
            Assert(ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Successful purchase must persist Earth ownership.");
            Assert(ArenaVariantRuntime.CanSelect(ArenaVariant.Earth),"Purchased Earth must become selectable immediately.");

            result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.AlreadyOwned,"A second Earth purchase must be rejected as already owned.");
            Assert(PlayerWallet.Balance==0,"Duplicate Earth purchase must never charge again.");
        }

        private static void CheckDarkPriceAndLock()
        {
            ResetArena(ArenaUnlockInventory.Dark00OwnedKey,ArenaShopCatalog.Dark00Price);
            ArenaShopItem dark=ArenaShopCatalog.For(ArenaVariant.Dark00);
            Assert(ArenaShopCatalog.Dark00Price==900,"Dark00Price must be exactly 900.");
            Assert(dark.Price==900,"Dark 00 shop card must display the 900-shard price.");
            Assert(ArenaUnlockInventory.UnlockRequirement(ArenaVariant.Dark00)=="900 SHARDS","Dark 00 requirement text must show 900 SHARDS.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Dark00),"Dark 00 must start locked without its entitlement.");
            Assert(!ArenaVariantRuntime.CanSelect(ArenaVariant.Dark00),"Locked Dark 00 must not be selectable for a match.");
        }

        private static void CheckDarkInsufficientShards()
        {
            ResetArena(ArenaUnlockInventory.Dark00OwnedKey,899);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Dark00);
            Assert(result==ArenaPurchaseResult.InsufficientShards,"899 shards must not unlock Dark 00.");
            Assert(PlayerWallet.Balance==899,"Failed Dark 00 purchase must not deduct shards.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Dark00),"Failed Dark 00 purchase must not grant ownership.");
        }

        private static void CheckDarkExactPurchaseAndDuplicateProtection()
        {
            ResetArena(ArenaUnlockInventory.Dark00OwnedKey,900);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Dark00);
            Assert(result==ArenaPurchaseResult.Purchased,"900 shards must unlock Dark 00.");
            Assert(PlayerWallet.Balance==0,"Dark 00 purchase must deduct exactly 900 shards.");
            Assert(ArenaUnlockInventory.Owned(ArenaVariant.Dark00),"Successful purchase must persist Dark 00 ownership.");
            Assert(ArenaVariantRuntime.CanSelect(ArenaVariant.Dark00),"Purchased Dark 00 must become selectable immediately.");

            result=ArenaUnlockInventory.Purchase(ArenaVariant.Dark00);
            Assert(result==ArenaPurchaseResult.AlreadyOwned,"A second Dark 00 purchase must be rejected as already owned.");
            Assert(PlayerWallet.Balance==0,"Duplicate Dark 00 purchase must never charge again.");
        }

        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("Arena shop regression: "+message);
        }
    }
}
#endif


