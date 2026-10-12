using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using UnityEngine;

namespace Alterverse.Profile
{
    public static class SafeIdentityCatalog
    {
        [Serializable] sealed class Catalog
        {
            public int version, suffixDigits;
            public string[] playerNames, clanNames, clanDescriptions;
        }
        static Catalog catalog;
        static readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
        static readonly object randomLock = new object();
        static Catalog Data
        {
            get
            {
                if (catalog != null) return catalog;
                var asset = Resources.Load<TextAsset>("Social/SafeIdentityCatalog");
                if (asset != null)
                {
                    try
                    {
                        var value = JsonUtility.FromJson<Catalog>(asset.text);
                        if (value != null && value.version == 1 && value.suffixDigits == 7 &&
                            value.playerNames?.Length > 0 && value.clanNames?.Length > 0 && value.clanDescriptions?.Length > 0)
                            catalog = value;
                    }
                    catch (Exception) { }
                }
                // Missing resource fails closed; arbitrary old names are never shown.
                if (catalog == null)
                {
                    Debug.LogWarning("DIMENSIONS FRONT: identity catalog unavailable.");
                    catalog = new Catalog { version = 1, suffixDigits = 7,
                        playerNames = new[] { "SolarFox" }, clanNames = new[] { "Solar Guard" },
                        clanDescriptions = new[] { "A friendly crew that values teamwork and steady progress." } };
                }
                return catalog;
            }
        }
        public static IReadOnlyList<string> Descriptions => Data.clanDescriptions;
        public static long PlayerNameCapacity => (long)Data.playerNames.Length * 10000000;
        public static long ClanNameCapacity => (long)Data.clanNames.Length * 10000000;
        public static bool IsPlayerName(string name) => HasApprovedRoot(name, Data.playerNames, false);
        public static bool IsClanName(string name) => HasApprovedRoot(name, Data.clanNames, true);
        public static bool IsClanTag(string value) => value != null && value.Length == 8 && value[0] == 'C' && Digits(value, 1);
        static bool HasApprovedRoot(string name, string[] roots, bool space)
        {
            if (name == null) return false;
            foreach (string root in roots)
            {
                int start = root.Length + (space ? 1 : 0);
                if (name.Length == start + 7 && name.StartsWith(root, StringComparison.Ordinal) &&
                    (!space || name[root.Length] == ' ') && Digits(name, start)) return true;
            }
            return false;
        }
        static bool Digits(string value, int start)
        {
            for (int i = start; i < value.Length; i++) if (value[i] < '0' || value[i] > '9') return false;
            return true;
        }
        static int RandomBelow(int bound)
        {
            byte[] bytes = new byte[4]; uint value;
            uint limit = uint.MaxValue - uint.MaxValue % (uint)bound;
            lock (randomLock)
            { do { random.GetBytes(bytes); value = BitConverter.ToUInt32(bytes, 0); } while (value >= limit); }
            return (int)(value % (uint)bound);
        }
        static string Suffix() => RandomBelow(10000000).ToString("D7", CultureInfo.InvariantCulture);
        public static string GeneratePlayerName(string previous = null)
        {
            for (int i = 0; i < 64; i++)
            { string value = Data.playerNames[RandomBelow(Data.playerNames.Length)] + Suffix(); if (value != previous) return value; }
            throw new InvalidOperationException("Name generation unavailable.");
        }
        public static string GenerateClanName(string previous = null)
        {
            for (int i = 0; i < 64; i++)
            { string value = Data.clanNames[RandomBelow(Data.clanNames.Length)] + " " + Suffix(); if (value != previous) return value; }
            throw new InvalidOperationException("Clan name generation unavailable.");
        }
        public static string GenerateClanTag() => "C" + Suffix();
        public static string DisplayPlayerName(string value, string identity = null)
        {
            if (IsPlayerName(value)) return value;
            uint hash = IdentityHash(identity ?? value ?? "player");
            return Data.playerNames[hash % (uint)Data.playerNames.Length] +
                (hash / (uint)Data.playerNames.Length % 10000000).ToString("D7", CultureInfo.InvariantCulture);
        }
        static uint IdentityHash(string identity)
        {
            uint hash = 2166136261;
            foreach (char ch in identity) hash = unchecked((hash ^ ch) * 16777619);
            return hash;
        }
        public static string DisplayClanName(string value, string identity)
        {
            if (IsClanName(value)) return value;
            uint hash = IdentityHash(identity ?? "clan");
            return Data.clanNames[hash % (uint)Data.clanNames.Length] + " " +
                (hash / (uint)Data.clanNames.Length % 10000000).ToString("D7", CultureInfo.InvariantCulture);
        }
        public static string DisplayClanTag(string value, string identity) => IsClanTag(value) ? value :
            "C" + (IdentityHash(identity ?? "clan") % 10000000).ToString("D7", CultureInfo.InvariantCulture);
        public static int DescriptionIndex(string text)
        { int index = Array.IndexOf(Data.clanDescriptions, text); return index < 0 ? 0 : index; }
    }
}
