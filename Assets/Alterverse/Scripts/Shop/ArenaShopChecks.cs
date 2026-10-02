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
                HadValue=PlayerPrefs.HasKey(key);
                Value=PlayerPrefs.GetInt(key,0);
            }
            public void Restore()
            {
                if(HadValue)PlayerPrefs.SetInt(Key,Value);
                else PlayerPrefs.DeleteKey(Key);
            }
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Arena purchases (non-destructive)")]
        public static void Run()
        {
            IntPrefSnapshot shards=new(ShardsKey);
            IntPrefSnapshot earthOwned=new(ArenaUnlockInventory.EarthOwnedKey);
            IntPrefSnapshot darkOwned=new(ArenaUnlockInventory.Dark00OwnedKey);
            IntPrefSnapshot moor=new(ArenaUnlockInventory.MoorOwnedKey),pyraxis=new(ArenaUnlockInventory.PyraxisOwnedKey),psr=new(ArenaUnlockInventory.PsrOwnedKey);
            IntPrefSnapshot selected=new("DF_SelectedArenaVariant"),shuffle=new("DF_ArenaShuffle");
            try
            {
                CheckPaidArena(ArenaVariant.GreenPlanet,ArenaUnlockInventory.MoorOwnedKey,800);
                CheckPaidArena(ArenaVariant.Pyraxis,ArenaUnlockInventory.PyraxisOwnedKey,800);
                CheckPaidArena(ArenaVariant.DiamondCore,ArenaUnlockInventory.PsrOwnedKey,1200);
                CheckEarthPriceAndLock();
                CheckEarthInsufficientShards();
                CheckEarthExactPurchaseAndDuplicateProtection();
                CheckDarkPriceAndLock();
                CheckDarkInsufficientShards();
                CheckDarkExactPurchaseAndDuplicateProtection();
                Debug.Log("DIMENSIONS FRONT: MOOR12C 800, PYRAXIS 800, PSR 1200, Earth 500 and Dark 00 1800 purchase checks passed.");
            }
            finally
            {
                moor.Restore();pyraxis.Restore();psr.Restore();selected.Restore();shuffle.Restore();
                shards.Restore();
                earthOwned.Restore();
                darkOwned.Restore();
                PlayerPrefs.Save();
            }
        }

        private static void CheckPaidArena(ArenaVariant variant,string key,int price)
        {
            ResetArena(key,price-1);
            Assert(ArenaShopCatalog.For(variant).Price==price,"Shop price mismatch: "+variant);
            Assert(!ArenaUnlockInventory.Owned(variant)&&!ArenaVariantRuntime.CanSelect(variant),"Unowned map must stay locked: "+variant);
            Assert(ArenaUnlockInventory.Purchase(variant)==ArenaPurchaseResult.InsufficientShards,"Insufficient balance must be rejected.");
            Assert(PlayerWallet.Balance==price-1&&!ArenaUnlockInventory.Owned(variant),"Rejected purchase must preserve balance and ownership.");
            PlayerPrefs.SetInt(ShardsKey,price);
            Assert(ArenaUnlockInventory.Purchase(variant)==ArenaPurchaseResult.Purchased,"Exact balance must purchase map.");
            Assert(PlayerWallet.Balance==0&&ArenaUnlockInventory.Owned(variant),"Purchase must debit exact price and grant ownership.");
            Assert(ArenaVariantRuntime.Select(variant)&&ArenaVariantRuntime.Selected==variant,"Purchased map must be selectable.");
            Assert(ArenaUnlockInventory.Purchase(variant)==ArenaPurchaseResult.AlreadyOwned&&PlayerWallet.Balance==0,"Duplicate purchase must never debit again.");
        }

        private static void ResetArena(string ownedKey,int shards)
        {
            PlayerPrefs.SetInt(ShardsKey,shards);
            PlayerPrefs.DeleteKey(ownedKey);
            PlayerPrefs.Save();
        }

        private static void CheckEarthPriceAndLock()
        {
            ResetArena(ArenaUnlockInventory.EarthOwnedKey,500);
            ArenaShopItem earth=ArenaShopCatalog.For(ArenaVariant.Earth);
            Assert(ArenaShopCatalog.EarthPrice==500,"EarthPrice must be exactly 500.");
            Assert(earth.Price==500,"Earth shop card must display the 500-shard price.");
            Assert(ArenaUnlockInventory.UnlockRequirement(ArenaVariant.Earth)=="500 SHARDS","Earth requirement text must show 500 SHARDS.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Earth must start locked without its entitlement.");
            Assert(!ArenaVariantRuntime.CanSelect(ArenaVariant.Earth),"Locked Earth must not be selectable for a match.");
            Assert(ArenaShopCatalog.For(ArenaVariant.Noxara).Price==ArenaShopCatalog.NoxaraPrice&&ArenaShopCatalog.NoxaraPrice==1000,"Existing Noxara price must remain unchanged.");
        }

        private static void CheckEarthInsufficientShards()
        {
            ResetArena(ArenaUnlockInventory.EarthOwnedKey,499);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.InsufficientShards,"499 shards must not unlock Earth.");
            Assert(PlayerWallet.Balance==499,"Failed Earth purchase must not deduct shards.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Failed Earth purchase must not grant ownership.");
        }

        private static void CheckEarthExactPurchaseAndDuplicateProtection()
        {
            ResetArena(ArenaUnlockInventory.EarthOwnedKey,500);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.Purchased,"500 shards must unlock Earth.");
            Assert(PlayerWallet.Balance==0,"Earth purchase must deduct exactly 500 shards.");
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
            Assert(ArenaShopCatalog.Dark00Price==1800,"Dark00Price must be exactly 1800.");
            Assert(dark.Price==1800,"Dark 00 shop card must display the 1800-shard price.");
            Assert(ArenaUnlockInventory.UnlockRequirement(ArenaVariant.Dark00)=="1800 SHARDS","Dark 00 requirement text must show 1800 SHARDS.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Dark00),"Dark 00 must start locked without its entitlement.");
            Assert(!ArenaVariantRuntime.CanSelect(ArenaVariant.Dark00),"Locked Dark 00 must not be selectable for a match.");
        }

        private static void CheckDarkInsufficientShards()
        {
            ResetArena(ArenaUnlockInventory.Dark00OwnedKey,1799);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Dark00);
            Assert(result==ArenaPurchaseResult.InsufficientShards,"1799 shards must not unlock Dark 00.");
            Assert(PlayerWallet.Balance==1799,"Failed Dark 00 purchase must not deduct shards.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Dark00),"Failed Dark 00 purchase must not grant ownership.");
        }

        private static void CheckDarkExactPurchaseAndDuplicateProtection()
        {
            ResetArena(ArenaUnlockInventory.Dark00OwnedKey,1800);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Dark00);
            Assert(result==ArenaPurchaseResult.Purchased,"1800 shards must unlock Dark 00.");
            Assert(PlayerWallet.Balance==0,"Dark 00 purchase must deduct exactly 1800 shards.");
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

