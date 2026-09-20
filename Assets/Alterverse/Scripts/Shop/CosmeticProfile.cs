using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    // Production always uses the existing wallet/journal. Editor checks can redirect this
    // small cosmetic boundary to memory, without writing or deleting a player's save.
    internal static class CosmeticProfile
    {
#if UNITY_EDITOR
        internal static System.Collections.Generic.Dictionary<string,int> TestData;
        internal static ProfileTransaction.Write[] LastTestTransaction;
        public static bool IsTest=>TestData!=null;
#else
        public static bool IsTest=>false;
#endif
        public static bool HasKey(string key)
        {
#if UNITY_EDITOR
            if(TestData!=null)return TestData.ContainsKey(key);
#endif
            return PlayerPrefs.HasKey(key);
        }
        public static int GetInt(string key,int fallback=0)
        {
#if UNITY_EDITOR
            if(TestData!=null)return TestData.TryGetValue(key,out int value)?value:fallback;
#endif
            return PlayerPrefs.GetInt(key,fallback);
        }
        public static int Balance=>IsTest?Mathf.Max(0,GetInt("DF_Shards")):PlayerWallet.Balance;
        public static bool PaidVoidfall=>!IsTest&&PaidShardCommerce.OwnsVoidfall;
        public static void Recover(){if(!IsTest)ProfileTransaction.Recover();}
        public static void Commit(params ProfileTransaction.Write[] writes)
        {
#if UNITY_EDITOR
            if(TestData!=null)
            {
                LastTestTransaction=writes;
                foreach(var write in writes)TestData[write.key]=write.number;
                return;
            }
#endif
            ProfileTransaction.Commit(writes);
        }
    }
}
