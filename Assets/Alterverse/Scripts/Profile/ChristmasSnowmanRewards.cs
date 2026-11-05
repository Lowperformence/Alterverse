using System;
using System.Security.Cryptography;
using System.Text;
using Alterverse.Match;

namespace Alterverse.Profile
{
    /// <summary>Account-scoped cumulative receipt and earned balance committed in one crash-safe journal.</summary>
    public static class ChristmasSnowmanRewards
    {
        public static int Apply(string matchId,int confirmedCount,string expectedAccount)
        {
            if (string.IsNullOrEmpty(matchId) || matchId.Length > 128 || confirmedCount <= 0 ||
                !string.Equals(expectedAccount ?? "",AccountProfilePrefs.Account ?? "",StringComparison.Ordinal)) return 0;
            ProfileTransaction.Recover();
            string receipt = ReceiptKey(matchId);
            int previous = Math.Max(0,AccountProfilePrefs.GetInt(receipt,0));
            if (confirmedCount <= previous) return 0;
            int delta = confirmedCount - previous;
            int amount = checked(delta * ChristmasSnowmanRules.ShardsPerSnowman);
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",ProfileTransaction.Add(PlayerWallet.EarnedBalance,amount)),
                new ProfileTransaction.Write(receipt,confirmedCount));
            return amount;
        }

        static string ReceiptKey(string matchId)
        {
            using var hash = SHA256.Create();
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(matchId));
            // Retain the receipt namespace: upgrading an active match must never pay its pickups twice.
            var key = new StringBuilder("DF_HalloweenPumpkins_");
            foreach (byte value in bytes) key.Append(value.ToString("x2"));
            return key.ToString();
        }
    }
}

