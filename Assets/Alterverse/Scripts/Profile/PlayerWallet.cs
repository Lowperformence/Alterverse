using Alterverse.Shop;
using UnityEngine;
namespace Alterverse.Profile
{
    public static class PlayerWallet
    {
        const string BalanceKey = "DF_Shards";
        public static int EarnedBalance => Mathf.Max(0, AccountProfilePrefs.GetInt(BalanceKey,0));
        // One displayed balance; Google-verified value is kept only in the server ledger.
        public static int Balance => ProfileTransaction.Add(EarnedBalance,PaidShardCommerce.Balance);
        public static bool CanAfford(int amount) => amount >= 0 && Balance >= amount && !PaidShardCommerce.Spending;
        public static bool TrySpend(int amount)
        {
            ProfileTransaction.Recover();
            // Anonymous amount-only debits cannot authorize a purchase against paid currency.
            if (amount < 0 || EarnedBalance < amount || PaidShardCommerce.Spending) return false;
            ProfileTransaction.Commit(new ProfileTransaction.Write(BalanceKey,EarnedBalance-amount));
            return true;
        }
        public static void Grant(int amount)
        {
            if (amount <= 0) return;
            ProfileTransaction.Recover();
            ProfileTransaction.Commit(new ProfileTransaction.Write(BalanceKey,ProfileTransaction.Add(EarnedBalance,amount)));
        }
        public static bool BeginServerPurchase(string sku)
        {
            var commerce = PaidShardCommerce.Instance;
            return commerce != null && commerce.Ready && commerce.BeginPurchase(sku);
        }
    }
}
