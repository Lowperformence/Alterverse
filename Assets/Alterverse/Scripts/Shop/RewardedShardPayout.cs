using System;
using System.Collections.Generic;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    // Uses the existing earned-shard profile ledger. This is SDK callback validation,
    // not AdMob server-side verification; no client endpoint can mint paid currency.
    internal static class RewardedShardPayout
    {
        public const int Amount = 5;
        const string PendingKey = "DF_CommerceRewardedAdsPending_v1";
        const string ReceiptKey = "DF_RewardedAdLastReceipt_v1";
        [Serializable] internal sealed class Receipt { public string id, account; public int day; }
        [Serializable] sealed class Pending { public Receipt[] receipts = Array.Empty<Receipt>(); }
        public static bool HasPending => PlayerPrefs.HasKey(PendingKey);
        public static Receipt NewReceipt() => new Receipt
        {
            id = Guid.NewGuid().ToString("N"), account = AccountProfilePrefs.Account ?? ""
        };

        static List<Receipt> Read()
        {
            if (!HasPending) return new List<Receipt>();
            var saved = JsonUtility.FromJson<Pending>(PlayerPrefs.GetString(PendingKey));
            if (saved?.receipts == null || saved.receipts.Length > 64)
                throw new InvalidOperationException("Stored ad reward needs support.");
            foreach (var receipt in saved.receipts)
                if (receipt == null || !Guid.TryParseExact(receipt.id, "N", out _) ||
                    receipt.account == null || receipt.account.Length > 64)
                    throw new InvalidOperationException("Stored ad reward needs support.");
            return new List<Receipt>(saved.receipts);
        }

        static void Save(List<Receipt> receipts)
        {
            if (receipts.Count == 0) PlayerPrefs.DeleteKey(PendingKey);
            else PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(new Pending { receipts = receipts.ToArray() }));
            PlayerPrefs.Save();
        }

        // Called only by Google's earned-reward callback, on the Unity thread.
        public static void Enqueue(Receipt receipt, int? completedDay = null)
        {
            var receipts = Read();
            if (receipts.Exists(r => r.id == receipt.id)) return;
            if (receipts.Count >= 64) throw new InvalidOperationException("Stored ad reward needs support.");
            receipt.day = completedDay ?? DailyCalendar.Today;
            receipts.Add(receipt);
            Save(receipts);
        }

        public static int Collect()
        {
            var receipts = Read();
            int granted = 0;
            for (int i = 0; i < receipts.Count;)
            {
                var receipt = receipts[i];
                if (receipt.account != (AccountProfilePrefs.Account ?? "")) { i++; continue; }
                ProfileTransaction.Recover();
                if (AccountProfilePrefs.GetString(ReceiptKey, "") != receipt.id)
                {
                    // Persist balance and receipt together in the existing crash-recoverable
                    // journal. Never award from OnClosed or OnFailed callbacks.
                    var writes = new List<ProfileTransaction.Write>
                    {
                        new ProfileTransaction.Write("DF_Shards", ProfileTransaction.Add(PlayerWallet.EarnedBalance, Amount)),
                        new ProfileTransaction.Write(ReceiptKey, receipt.id)
                    };
                    writes.AddRange(DailyQuestProgress.RewardedAdWrites(receipt.day));
                    ProfileTransaction.Commit(writes.ToArray());
                    granted += Amount;
                }
                receipts.RemoveAt(i);
                // Persist each removal before processing the next receipt, so a crash
                // cannot replay an earlier receipt after a later one was committed.
                Save(receipts);
            }
            return granted;
        }
    }
}
