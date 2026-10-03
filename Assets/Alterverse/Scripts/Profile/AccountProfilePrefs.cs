using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Profile
{
    // Account-scoped cache plus a complete typed profile. Credentials and transaction
    // journals stay on this device and never become part of a cloud profile payload.
    public static class AccountProfilePrefs
    {
        [Serializable] public sealed class Entry
        {
            public string key, text;
            public int kind, number;
            public float real;
        }
        [Serializable] public sealed class State
        {
            public long revision;
            public bool dirty;
            public Entry[] profile = Array.Empty<Entry>();
        }
        [Serializable] sealed class Registry { public Entry[] profile; }
        static readonly Dictionary<string, Entry> values = new();
        static readonly Dictionary<string, Entry> known = new();
        const string RegistryKey = "DF_CloudLocalRegistry_v1";
        const string ImportKey = "DF_CloudImportedLocalProfile_v1";
        public static string Account { get; private set; }
        public static long Revision { get; private set; }
        public static bool Dirty { get; private set; }
        public static bool HadConflict { get; private set; }
        public static bool Attached => !string.IsNullOrEmpty(Account);
        static bool registryLoaded;

        static bool ProfileKey(string key) => key != null && key.StartsWith("DF_", StringComparison.Ordinal) &&
            !key.StartsWith("DF_ProfileTransaction", StringComparison.Ordinal) &&
            !key.StartsWith("DF_Cloud", StringComparison.Ordinal) &&
            !key.StartsWith("DF_Commerce", StringComparison.Ordinal) &&
            !key.StartsWith("DF_ShardClaim_", StringComparison.Ordinal) &&
            !key.StartsWith("DF_AccountProfile", StringComparison.Ordinal);
        static string DeviceKey(string key) => Attached && key.StartsWith("DF_ProfileTransaction", StringComparison.Ordinal)
            ? key + "_" + Account : key;
        static string CacheKey(string account) => "DF_AccountProfileCache_" + account;
        static Entry Clone(Entry e) => new() { key = e.key, kind = e.kind, number = e.number, text = e.text, real = e.real };

        static void Register(string key, int kind)
        {
            if (!ProfileKey(key) || Attached) return;
            LoadRegistry();
            known[key] = new Entry { key = key, kind = kind };
        }
        static void LoadRegistry()
        {
            if (registryLoaded) return;
            registryLoaded = true;
            try
            {
                var bundled = Resources.Load<TextAsset>("Commerce/LegacyProfileKeys");
                if (bundled != null)
                    foreach (Entry e in JsonUtility.FromJson<Registry>(bundled.text).profile)
                        if (ProfileKey(e.key)) known[e.key] = e;
                var saved = JsonUtility.FromJson<Registry>(PlayerPrefs.GetString(RegistryKey, "{}"));
                if (saved?.profile != null)
                    foreach (Entry e in saved.profile) if (ProfileKey(e.key)) known[e.key] = e;
            }
            catch (Exception) { Debug.LogWarning("DIMENSIONS FRONT: local profile key registry could not be read."); }
        }
        static Entry[] ExportLocal()
        {
            LoadRegistry();
            var result = new List<Entry>();
            foreach (Entry e in known.Values)
            {
                if (!PlayerPrefs.HasKey(e.key)) continue;
                Entry value = Clone(e);
                if (e.kind == 1) value.text = PlayerPrefs.GetString(e.key, "");
                else if (e.kind == 2) value.real = PlayerPrefs.GetFloat(e.key, 0);
                else value.number = PlayerPrefs.GetInt(e.key, 0);
                result.Add(value);
            }
            return result.ToArray();
        }

        public static void Attach(string account, long revision, Entry[] remote)
        {
            if (Account == account) return;
            Detach();
            State cache = null;
            try { cache = JsonUtility.FromJson<State>(PlayerPrefs.GetString(CacheKey(account), "{}")); }
            catch (Exception) { }
            Account = account; Revision = revision; HadConflict = false;
            Entry[] source = remote ?? Array.Empty<Entry>();
            bool pending = cache != null && cache.dirty && cache.profile != null;
            Dirty = pending && cache.revision == revision;
            if (Dirty) source = cache.profile;
            else if (pending)
            {
                // Preserve the offline branch for support; never overwrite a newer cloud save.
                PlayerPrefs.SetString("DF_AccountProfileConflict_" + account, JsonUtility.ToJson(cache));
                HadConflict = true;
            }
            if (source.Length == 0 && !pending && !PlayerPrefs.HasKey(CacheKey(account)) &&
                string.IsNullOrEmpty(PlayerPrefs.GetString(ImportKey, "")))
            {
                source = ExportLocal(); Dirty = source.Length > 0;
                PlayerPrefs.SetString(ImportKey, account);
            }
            values.Clear();
            foreach (Entry e in source) if (ProfileKey(e.key)) values[e.key] = Clone(e);
            Persist();
        }
        public static void Detach()
        {
            if (Attached) Persist();
            Account = null; Revision = 0; Dirty = false; values.Clear();
        }
        public static Entry[] Capture()
        {
            var entries = new List<Entry>(values.Count);
            foreach (Entry e in values.Values) entries.Add(Clone(e));
            entries.Sort((a,b) => string.CompareOrdinal(a.key,b.key));
            return entries.ToArray();
        }
        static bool Same(Entry a, Entry b) => a != null && b != null && a.kind == b.kind &&
            a.number == b.number && a.text == b.text && a.real == b.real;

        public static void ApplyRemote(long revision, Entry[] remote, Entry[] submitted)
        {
            // Keep local changes made while the request was in flight. Currency gains
            // are rebased as a delta, never by copying the old pre-purchase balance back.
            var before = new Dictionary<string, Entry>();
            foreach (Entry e in submitted ?? Array.Empty<Entry>()) before[e.key] = e;
            var changed = new List<Entry>(); var deleted = new List<string>();
            foreach (Entry e in values.Values)
                if (!before.TryGetValue(e.key, out Entry old) || !Same(e,old)) changed.Add(Clone(e));
            foreach (string key in before.Keys) if (!values.ContainsKey(key)) deleted.Add(key);
            values.Clear();
            foreach (Entry e in remote ?? Array.Empty<Entry>()) if (ProfileKey(e.key)) values[e.key] = Clone(e);
            foreach (Entry e in changed)
            {
                if (e.key == "DF_Shards" && before.TryGetValue(e.key,out Entry old) && values.TryGetValue(e.key,out Entry earned))
                    e.number = (int)Math.Min(int.MaxValue, Math.Max(0L,(long)earned.number + e.number - old.number));
                values[e.key] = e;
            }
            foreach (string key in deleted) values.Remove(key);
            Revision = revision; Dirty = changed.Count > 0 || deleted.Count > 0; Persist();
        }
        public static void ResolveConflict(long revision, Entry[] remote)
        {
            PlayerPrefs.SetString("DF_AccountProfileConflict_" + Account,
                JsonUtility.ToJson(new State { revision = Revision, dirty = Dirty, profile = Capture() }));
            values.Clear();
            foreach (Entry e in remote ?? Array.Empty<Entry>()) if (ProfileKey(e.key)) values[e.key] = Clone(e);
            Revision = revision; Dirty = false; HadConflict = true; Persist();
        }
        static void Persist()
        {
            if (!Attached) return;
            PlayerPrefs.SetString(CacheKey(Account), JsonUtility.ToJson(new State { revision = Revision, dirty = Dirty, profile = Capture() }));
            PlayerPrefs.Save();
        }
        static bool Read(string key, int kind, out Entry value)
        {
            Register(key,kind); value = null;
            return Attached && ProfileKey(key) && values.TryGetValue(key,out value) && value.kind == kind;
        }
        public static int GetInt(string key, int fallback = 0) => Read(key,0,out Entry e) ? e.number :
            Attached && ProfileKey(key) ? fallback : PlayerPrefs.GetInt(DeviceKey(key),fallback);
        public static string GetString(string key, string fallback = "") => Read(key,1,out Entry e) ? e.text ?? fallback :
            Attached && ProfileKey(key) ? fallback : PlayerPrefs.GetString(DeviceKey(key),fallback);
        public static float GetFloat(string key, float fallback = 0) => Read(key,2,out Entry e) ? e.real :
            Attached && ProfileKey(key) ? fallback : PlayerPrefs.GetFloat(DeviceKey(key),fallback);
        public static bool HasKey(string key) => Attached && ProfileKey(key) ? values.ContainsKey(key) : PlayerPrefs.HasKey(DeviceKey(key));
        static bool Store(Entry value)
        {
            Register(value.key,value.kind);
            if (!Attached || !ProfileKey(value.key)) return false;
            values[value.key] = value; Dirty = true; return true;
        }
        public static void SetInt(string key, int value) { if (!Store(new Entry {key=key,kind=0,number=value})) PlayerPrefs.SetInt(DeviceKey(key),value); }
        public static void SetString(string key, string value) { if (!Store(new Entry {key=key,kind=1,text=value})) PlayerPrefs.SetString(DeviceKey(key),value); }
        public static void SetFloat(string key, float value) { if (!Store(new Entry {key=key,kind=2,real=value})) PlayerPrefs.SetFloat(DeviceKey(key),value); }
        public static void DeleteKey(string key)
        {
            if (Attached && ProfileKey(key)) { if (values.Remove(key)) Dirty = true; }
            else PlayerPrefs.DeleteKey(DeviceKey(key));
        }
        public static void Save()
        {
            if (Attached) Persist();
            else { LoadRegistry(); PlayerPrefs.SetString(RegistryKey,JsonUtility.ToJson(new Registry {profile=new List<Entry>(known.Values).ToArray()})); PlayerPrefs.Save(); }
        }
    }
}
