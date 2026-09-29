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

        [MenuItem("DIMENSIONS FRONT/Checks/Earth arena purchase (non-destructive)")]
        public static void Run()
        {
            IntPrefSnapshot shards=new(ShardsKey);
            IntPrefSnapshot earthOwned=new(ArenaUnlockInventory.EarthOwnedKey);
            try
            {
                CheckEarthPriceAndLock();
                CheckInsufficientShards();
                CheckExactPurchaseAndDuplicateProtection();
                Debug.Log("DIMENSIONS FRONT: Earth arena 500-shard purchase, lock, unlock and duplicate-purchase checks passed.");
            }
            finally
            {
                shards.Restore();
                earthOwned.Restore();
                PlayerPrefs.Save();
            }
        }

        private static void ResetEarth(int shards)
        {
            PlayerPrefs.SetInt(ShardsKey,shards);
            PlayerPrefs.DeleteKey(ArenaUnlockInventory.EarthOwnedKey);
            PlayerPrefs.Save();
        }

        private static void CheckEarthPriceAndLock()
        {
            ResetEarth(500);
            ArenaShopItem earth=ArenaShopCatalog.For(ArenaVariant.Earth);
            Assert(ArenaShopCatalog.EarthPrice==500,"EarthPrice must be exactly 500.");
            Assert(earth.Price==500,"Earth shop card must display the 500-shard price.");
            Assert(ArenaUnlockInventory.UnlockRequirement(ArenaVariant.Earth)=="500 SHARDS","Earth requirement text must show 500 SHARDS.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Earth must start locked without its entitlement.");
            Assert(!ArenaVariantRuntime.CanSelect(ArenaVariant.Earth),"Locked Earth must not be selectable for a match.");
            Assert(ArenaShopCatalog.For(ArenaVariant.Noxara).Price==ArenaShopCatalog.NoxaraPrice&&ArenaShopCatalog.NoxaraPrice==1000,"Existing Noxara price must remain unchanged.");
        }

        private static void CheckInsufficientShards()
        {
            ResetEarth(499);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.InsufficientShards,"499 shards must not unlock Earth.");
            Assert(PlayerWallet.Balance==499,"Failed Earth purchase must not deduct shards.");
            Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Failed Earth purchase must not grant ownership.");
        }

        private static void CheckExactPurchaseAndDuplicateProtection()
        {
            ResetEarth(500);
            ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.Purchased,"500 shards must unlock Earth.");
            Assert(PlayerWallet.Balance==0,"Earth purchase must deduct exactly 500 shards.");
            Assert(ArenaUnlockInventory.Owned(ArenaVariant.Earth),"Successful purchase must persist Earth ownership.");
            Assert(ArenaVariantRuntime.CanSelect(ArenaVariant.Earth),"Purchased Earth must become selectable immediately.");

            result=ArenaUnlockInventory.Purchase(ArenaVariant.Earth);
            Assert(result==ArenaPurchaseResult.AlreadyOwned,"A second Earth purchase must be rejected as already owned.");
            Assert(PlayerWallet.Balance==0,"Duplicate Earth purchase must never charge again.");
        }

        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("Arena shop regression: "+message);
        }
    }
}
#endif
