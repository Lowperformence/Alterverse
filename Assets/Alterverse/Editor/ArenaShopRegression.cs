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
        private const string NoxaraKey="DF_ArenaOwned_Noxara";
        private const string SelectedKey="DF_SelectedArenaVariant";
        private const string ShuffleKey="DF_ArenaShuffle";
        private const string LastShuffleKey="DF_LastShuffledArena";

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
            PrefSnapshot balance=new(BalanceKey),noxara=new(NoxaraKey),selected=new(SelectedKey),
                shuffle=new(ShuffleKey),lastShuffle=new(LastShuffleKey);
            try
            {
                PlayerPrefs.SetInt(BalanceKey,1000);
                PlayerPrefs.DeleteKey(NoxaraKey);
                PlayerPrefs.DeleteKey(SelectedKey);
                PlayerPrefs.DeleteKey(ShuffleKey);
                PlayerPrefs.DeleteKey(LastShuffleKey);
                PlayerPrefs.Save();

                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Moon),"Moon must always be free.");
                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Mars),"Mars must always be free.");
                Assert(ArenaUnlockInventory.Owned(ArenaVariant.GreenPlanet),"Green Planet must always be free.");
                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Kryon),"Kryon must always be free.");
                Assert(!ArenaUnlockInventory.Owned(ArenaVariant.Noxara),"Noxara must start locked.");
                Assert(ArenaShopCatalog.For(ArenaVariant.Noxara).Price==1000,"Noxara must cost exactly 1000 shards.");

                Assert(ArenaVariantRuntime.Select(ArenaVariant.Mars)&&ArenaVariantRuntime.Selected==ArenaVariant.Mars,
                    "Free Mars must be selectable.");
                Assert(ArenaVariantRuntime.Select(ArenaVariant.GreenPlanet)&&ArenaVariantRuntime.Selected==ArenaVariant.GreenPlanet,
                    "Free Green Planet must be selectable.");
                Assert(ArenaVariantRuntime.Select(ArenaVariant.Kryon)&&ArenaVariantRuntime.Selected==ArenaVariant.Kryon,
                    "Free Kryon must be selectable.");
                Assert(!ArenaVariantRuntime.Select(ArenaVariant.Noxara),"Locked Noxara must not be selectable.");

                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.Noxara)==ArenaPurchaseResult.Purchased,
                    "Noxara must unlock with 1000 shards.");
                Assert(PlayerWallet.Balance==0,"Noxara must debit exactly 1000 shards.");
                Assert(ArenaUnlockInventory.Owned(ArenaVariant.Noxara),"Noxara ownership must persist.");
                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.Noxara)==ArenaPurchaseResult.AlreadyOwned,
                    "Owned Noxara must not be purchasable twice.");
                Assert(PlayerWallet.Balance==0,"Duplicate Noxara purchase must not debit shards.");
                Assert(ArenaVariantRuntime.Select(ArenaVariant.Noxara)&&ArenaVariantRuntime.Selected==ArenaVariant.Noxara,
                    "Owned Noxara must be selectable.");

                ArenaVariantRuntime.SetShuffleEnabled(true);
                Assert(ArenaVariantRuntime.ShuffleEnabled,"Arena shuffle must persist when enabled.");

                PlayerPrefs.DeleteKey(NoxaraKey);
                PlayerPrefs.SetInt(BalanceKey,999);
                PlayerPrefs.SetInt(SelectedKey,(int)ArenaVariant.Noxara);
                PlayerPrefs.Save();
                Assert(ArenaUnlockInventory.Purchase(ArenaVariant.Noxara)==ArenaPurchaseResult.InsufficientShards,
                    "Noxara must reject a balance below 1000.");
                Assert(PlayerWallet.Balance==999&&!ArenaUnlockInventory.Owned(ArenaVariant.Noxara),
                    "Failed Noxara purchase must change neither balance nor ownership.");
                Assert(ArenaVariantRuntime.Selected==ArenaVariant.Moon,
                    "A stale locked Noxara selection must fall back to Moon.");

                PlayerPrefs.SetInt(SelectedKey,999);
                PlayerPrefs.Save();
                Assert(ArenaVariantRuntime.Selected==ArenaVariant.Moon,
                    "An invalid stored arena value must fall back to Moon.");

                Debug.Log("DIMENSIONS FRONT: arena shop regression assertions passed.");
            }
            finally
            {
                balance.Restore();noxara.Restore();selected.Restore();
                shuffle.Restore();lastShuffle.Restore();PlayerPrefs.Save();
            }
        }

        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("DIMENSIONS FRONT arena regression: "+message);
        }
    }
}
#endif
