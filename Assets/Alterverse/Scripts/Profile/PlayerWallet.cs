using System.Security.Cryptography;
using System.Text;
using Alterverse.Shop;
using UnityEngine;
namespace Alterverse.Profile
{
    public static class PlayerWallet
    {
        const string BalanceKey = "DF_Shards";
        const string GooglePlayTestBalanceKey = "DF_CommerceGooglePlayTestBalance";
        const string GooglePlayTestReceiptPrefix = "DF_CommerceGooglePlayTestReceipt_";

        public static int EarnedBalance => Mathf.Max(0, AccountProfilePrefs.GetInt(BalanceKey,0));

        // This balance exists only in the special internal Google Play shard-test build.
        // DF_Commerce* keys are device-local and deliberately excluded from cloud profile sync.
        public static int GooglePlayTestBalance
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && DF_GOOGLE_PLAY_SHARD_TEST
                return Mathf.Max(0,PlayerPrefs.GetInt(GooglePlayTestBalanceKey,0));
#else
                return 0;
#endif
            }
        }

        // One displayed balance. Production paid value lives only in the verified server ledger.
        public static int Balance => ProfileTransaction.Add(ProfileTransaction.Add(EarnedBalance,PaidShardCommerce.Balance),GooglePlayTestBalance);
        public static bool CanAfford(int amount) => amount >= 0 && Balance >= amount && !PaidShardCommerce.Spending;

        public static bool TrySpend(int amount)
        {
            ProfileTransaction.Recover();
#if UNITY_ANDROID && !UNITY_EDITOR && DF_GOOGLE_PLAY_SHARD_TEST
            if(amount<0||PaidShardCommerce.Spending)return false;
            int available=ProfileTransaction.Add(EarnedBalance,GooglePlayTestBalance);
            if(available<amount)return false;
            int earnedDebit=Mathf.Min(EarnedBalance,amount);
            int testDebit=amount-earnedDebit;
            if(earnedDebit>0)
                ProfileTransaction.Commit(new ProfileTransaction.Write(BalanceKey,EarnedBalance-earnedDebit));
            if(testDebit>0)
            {
                PlayerPrefs.SetInt(GooglePlayTestBalanceKey,GooglePlayTestBalance-testDebit);
                PlayerPrefs.Save();
            }
            return true;
#else
            // Anonymous amount-only debits cannot authorize a production purchase against paid currency.
            if (amount < 0 || EarnedBalance < amount || PaidShardCommerce.Spending) return false;
            ProfileTransaction.Commit(new ProfileTransaction.Write(BalanceKey,EarnedBalance-amount));
            return true;
#endif
        }

        public static void Grant(int amount)
        {
            if (amount <= 0) return;
            ProfileTransaction.Recover();
            ProfileTransaction.Commit(new ProfileTransaction.Write(BalanceKey,ProfileTransaction.Add(EarnedBalance,amount)));
        }

        // Internal Play-license-test helper. It is not compiled into normal Android builds.
        // Purchase tokens are only used to make the local test grant idempotent.
        public static bool GrantGooglePlayBillingTest(string productId,string purchaseToken)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && DF_GOOGLE_PLAY_SHARD_TEST
            if(string.IsNullOrEmpty(productId)||string.IsNullOrEmpty(purchaseToken))return false;
            ShardPack pack=null;
            foreach(var candidate in ShardPacks.All)
                if(candidate.Id==productId){pack=candidate;break;}
            if(pack==null||pack.Amount<=0)return false;

            string receiptKey=GooglePlayTestReceiptPrefix+TokenHash(purchaseToken);
            if(PlayerPrefs.HasKey(receiptKey))return true;

            int next=ProfileTransaction.Add(GooglePlayTestBalance,pack.Amount);
            PlayerPrefs.SetInt(GooglePlayTestBalanceKey,next);
            PlayerPrefs.SetInt(receiptKey,1);
            PlayerPrefs.Save();
            return true;
#else
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && DF_GOOGLE_PLAY_SHARD_TEST
        static string TokenHash(string token)
        {
            using var sha=SHA256.Create();
            byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            var text=new StringBuilder(hash.Length*2);
            foreach(byte value in hash)text.Append(value.ToString("x2"));
            return text.ToString();
        }
#endif

        public static bool BeginServerPurchase(string sku)
        {
            var commerce = PaidShardCommerce.Instance;
            return commerce != null && commerce.Ready && commerce.BeginPurchase(sku);
        }
    }
}
