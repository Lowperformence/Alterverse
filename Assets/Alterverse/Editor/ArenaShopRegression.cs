#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class ArenaShopRegression
    {
        private const string BalanceKey="DF_Shards";
        private const string MarsKey="DF_ArenaOwned_Mars";
        private const string GreenKey="DF_ArenaOwned_GreenPlanet";
        private const string MatchesKey="DF_MatchesCompleted";
        private const string SelectedKey="DF_SelectedArenaVariant";

        private readonly struct PrefSnapshot
        {
            public readonly string Key;
            public readonly bool Exists;
            public readonly int Value;
            public PrefSnapshot(string key){Key=key;Exists=PlayerPrefs.HasKey(key);Value=PlayerPrefs.GetInt(key);}
            public void Restore(){if(Exists)PlayerPrefs.SetInt(Key,Value);else PlayerPrefs.DeleteKey(Key);}
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Arena shop regression")]
        public static void Run()
        {
            ProfileTransaction.Recover();
            PrefSnapshot balance=new(BalanceKey),mars=new(MarsKey),green=new(GreenKey),matches=new(MatchesKey),selected=new(SelectedKey);
            try
            {
                PlayerPrefs.SetInt(BalanceKey,1000);
                PlayerPrefs.DeleteKey(MarsKey);
                PlayerPrefs.DeleteKey(GreenKey);
                PlayerPrefs.SetInt(MatchesKey,0);
                PlayerPrefs.DeleteKey(SelectedKey);
                PlayerPrefs.Save();

                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Moon),"Moon must always be owned.");
                Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Mars),"Mars must start locked.");
                Assert(!ArenaUnlockInventory.Owned(ArenaVariant.GreenPlanet),"Green Planet must start locked.");
                Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Noxara),"Noxara must start locked before five completed matches.");
                Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Kryon),"Kryon must start locked before ten completed matches.");
                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.Noxara)==ArenaPurchaseResult.NotPurchasable,
                    "Noxara must never be purchasable with shards.");
                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.Kryon)==ArenaPurchaseResult.NotPurchasable,
                    "Kryon must never be purchasable with shards.");
                Assert(!ArenaVariantRuntime.Select(ArenaVariant.Noxara),"Locked Noxara must not be selectable.");
                Assert(!ArenaVariantRuntime.Select(ArenaVariant.Kryon),"Locked Kryon must not be selectable.");
                Assert(!ArenaVariantRuntime.Select(ArenaVariant.Mars),"Locked Mars must not be selectable.");
                Assert(ArenaVariantRuntime.Selected==ArenaVariant.Moon,"Locked selection must remain Moon.");

                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.Mars)==ArenaPurchaseResult.Purchased,
                    "Mars must unlock with sufficient shards.");
                Assert(PlayerWallet.Balance==500,"Mars must debit exactly 500 shards.");
                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Mars),"Mars ownership must persist immediately.");
                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.Mars)==ArenaPurchaseResult.AlreadyOwned,
                    "Owned Mars must not be purchasable twice.");
                Assert(PlayerWallet.Balance==500,"Duplicate Mars purchase must not debit shards.");
                Assert(ArenaVariantRuntime.Select(ArenaVariant.Mars)&&ArenaVariantRuntime.Selected==ArenaVariant.Mars,
                    "Owned Mars must be selectable.");

                PlayerPrefs.SetInt(BalanceKey,499);
                PlayerPrefs.Save();
                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.GreenPlanet)==ArenaPurchaseResult.InsufficientShards,
                    "Green Planet must reject a balance below 500.");
                Assert(PlayerWallet.Balance==499&&!ArenaUnlockInventory.Owned(ArenaVariant.GreenPlanet),
                    "Failed Green Planet purchase must change neither balance nor ownership.");

                PlayerPrefs.SetInt(SelectedKey,(int)ArenaVariant.GreenPlanet);
                PlayerPrefs.Save();
                Assert(ArenaVariantRuntime.Selected==ArenaVariant.Moon,
                    "A stale locked Green Planet selection must fall back to Moon.");

                PlayerPrefs.SetInt(SelectedKey,999);
                PlayerPrefs.Save();
                Assert(ArenaVariantRuntime.Selected==ArenaVariant.Moon,
                    "An invalid stored arena value must fall back to Moon.");

                PlayerPrefs.SetInt(BalanceKey,500);
                PlayerPrefs.Save();
                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.GreenPlanet)==ArenaPurchaseResult.Purchased,
                    "Green Planet must unlock for exactly 500 shards.");
                Assert(PlayerWallet.Balance==0&&ArenaUnlockInventory.Owned(ArenaVariant.GreenPlanet),
                    "Green Planet purchase must atomically debit 500 and grant ownership.");
                Assert(ArenaVariantRuntime.Select(ArenaVariant.GreenPlanet)&&ArenaVariantRuntime.Selected==ArenaVariant.GreenPlanet,
                    "Owned Green Planet must be selectable.");

                PlayerPrefs.SetInt(MatchesKey,ArenaUnlockInventory.NoxaraMatchesRequired);
                PlayerPrefs.Save();
                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Noxara),
                    "Noxara must unlock from completed-match progression.");
                Assert(ArenaVariantRuntime.Select(ArenaVariant.Noxara)&&ArenaVariantRuntime.Selected==ArenaVariant.Noxara,
                    "Progression-unlocked Noxara must be selectable.");

                PlayerPrefs.SetInt(MatchesKey,ArenaUnlockInventory.KryonMatchesRequired);
                PlayerPrefs.Save();
                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Kryon),
                    "Kryon must unlock from completed-match progression.");
                Assert(ArenaVariantRuntime.Select(ArenaVariant.Kryon)&&ArenaVariantRuntime.Selected==ArenaVariant.Kryon,
                    "Progression-unlocked Kryon must be selectable.");

                Debug.Log("DIMENSIONS FRONT: arena shop regression assertions passed.");
            }
            finally
            {
                balance.Restore();mars.Restore();green.Restore();matches.Restore();selected.Restore();PlayerPrefs.Save();
            }
        }

        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("DIMENSIONS FRONT arena regression: "+message);
        }
    }
}
#endif
