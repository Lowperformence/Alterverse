using System;
using UnityEngine;
namespace Alterverse.Profile
{
    /// <summary>Absolute-value journal: replay is idempotent after interrupted persistence.</summary>
    public static class ProfileTransaction
    {
        public static event Action Committed;
        private const string Pending="DF_ProfileTransaction_v1";
        [Serializable] public sealed class Write
        {
            public string key,text; public int number; public bool isText;
            public Write(string key,int value){this.key=key;number=value;}
            public Write(string key,string value){this.key=key;text=value;isText=true;}
        }
        [Serializable] private sealed class Journal { public int version=1; public Write[] writes; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RestoreAtBoot()=>Recover();
        public static void Recover()
        {
            if(!Alterverse.Profile.AccountProfilePrefs.HasKey(Pending))return;
            string json=Alterverse.Profile.AccountProfilePrefs.GetString(Pending);
            Journal journal;
            try
            {
                journal=JsonUtility.FromJson<Journal>(json);
                if(journal==null||journal.version!=1||journal.writes==null||journal.writes.Length>32)throw new FormatException();
                foreach(Write write in journal.writes)
                    if(write==null||string.IsNullOrEmpty(write.key)||!write.key.StartsWith("DF_",StringComparison.Ordinal)||write.key==Pending)throw new FormatException();
            }
            catch(Exception)
            {
                // Keep an inspectable copy; malformed data must not erase the existing profile.
                Alterverse.Profile.AccountProfilePrefs.SetString(Pending+"_Corrupt",json.Length<=65536?json:json.Substring(0,65536));
                Alterverse.Profile.AccountProfilePrefs.DeleteKey(Pending);Alterverse.Profile.AccountProfilePrefs.Save();
                Debug.LogWarning("DIMENSIONS FRONT: invalid pending profile transaction quarantined.");return;
            }
            Apply(journal);
        }
        public static void PreserveConflictingJournal()
        {
            if(!AccountProfilePrefs.HasKey(Pending))return;
            AccountProfilePrefs.SetString(Pending+"_Conflict",AccountProfilePrefs.GetString(Pending));
            AccountProfilePrefs.DeleteKey(Pending);AccountProfilePrefs.Save();
        }
        public static void Commit(params Write[] writes)
        {
            Recover();
            var journal=new Journal{writes=writes};
            Alterverse.Profile.AccountProfilePrefs.SetString(Pending,JsonUtility.ToJson(journal));Alterverse.Profile.AccountProfilePrefs.Save();
            Apply(journal);
        }
        private static void Apply(Journal journal)
        {
            foreach(Write write in journal.writes)
                if(write.isText)Alterverse.Profile.AccountProfilePrefs.SetString(write.key,write.text);else Alterverse.Profile.AccountProfilePrefs.SetInt(write.key,write.number);
            Alterverse.Profile.AccountProfilePrefs.Save();
            Alterverse.Profile.AccountProfilePrefs.DeleteKey(Pending);Alterverse.Profile.AccountProfilePrefs.Save();
            Committed?.Invoke();
        }
        public static int Add(int value,int amount)=>(int)Math.Min(int.MaxValue,Math.Max(0L,(long)value+amount));
    }
}


