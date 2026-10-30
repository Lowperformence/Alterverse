using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Alterverse.Profile
{
    /// <summary>Device-wide interface preference. Never changes account/profile or server data.</summary>
    public static class GameLanguage
    {
        public const string PreferenceKey = "DF_InterfaceLanguage_v1";
        public const string DeviceDefault = "system";
        public sealed class Option
        {
            public string Code { get; }
            public string Name { get; }
            public string EnglishName { get; }
            internal string[] Words { get; }
            internal Option(string code, string name, string englishName, string[] words)
            { Code = code; Name = name; EnglishName = englishName; Words = words; }
        }
        [Serializable] sealed class Catalog { public string[] keys; public Entry[] languages; }
        [Serializable] sealed class Entry
        { public string code, name, englishName; public string[] words; }
        static readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Option> options = new Dictionary<string, Option>(StringComparer.OrdinalIgnoreCase);
        static IReadOnlyList<Option> languages;
        static string selected;
        public static int Version { get; private set; }
        public static IReadOnlyList<Option> Languages { get { Load(); return languages; } }
        public static string SelectedCode
        {
            get
            {
                Load();
                if (selected == null)
                {
                    string saved = PlayerPrefs.GetString(PreferenceKey, DeviceDefault);
                    selected = saved == DeviceDefault ? DeviceDefault : Normalize(saved) ?? DeviceDefault;
                }
                return selected;
            }
        }
        public static Option Current
        {
            get
            {
                string code = SelectedCode;
                if (code == DeviceDefault) code = DetectDeviceLanguage();
                return options.TryGetValue(code, out var option) ? option : options["en"];
            }
        }
        public static bool Select(string code)
        {
            Load();
            code = code == DeviceDefault ? DeviceDefault : Normalize(code);
            if (code == null) return false;
            // Explicitly choosing the current device language must also persist a manual override.
            if (selected == code && PlayerPrefs.HasKey(PreferenceKey)) return true;
            selected = code;
            PlayerPrefs.SetString(PreferenceKey, code);
            PlayerPrefs.Save();
            Version++;
            return true;
        }
        public static string Text(string source)
        {
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
            Option language = Current;
            return indices.TryGetValue(source, out int index) && index < language.Words.Length &&
                !string.IsNullOrEmpty(language.Words[index]) ? language.Words[index] : source;
        }
        public static string Normalize(string code)
        {
            Load();
            if (string.IsNullOrWhiteSpace(code)) return null;
            code = code.Trim().Replace('_', '-');
            if (options.TryGetValue(code, out var exact)) return exact.Code;
            string lower = code.ToLowerInvariant();
            if (lower == "no" || lower.StartsWith("no-")) return "nb";
            if (lower == "iw" || lower.StartsWith("iw-")) return "he";
            if (lower == "in" || lower.StartsWith("in-")) return "id";
            if (lower == "zh" || lower.StartsWith("zh-"))
                return lower.Contains("hant") || lower.Contains("tw") || lower.Contains("hk") || lower.Contains("mo") ? "zh-Hant" : "zh-Hans";
            int separator = code.IndexOf('-');
            return separator > 0 && options.TryGetValue(code.Substring(0, separator), out var parent) ? parent.Code : null;
        }
        static string DetectDeviceLanguage()
        {
            string system = Application.systemLanguage.ToString();
            string code;
            switch (system)
            {
                case "German": code = "de"; break; case "French": code = "fr"; break;
                case "Spanish": code = "es"; break; case "Italian": code = "it"; break;
                case "Portuguese": code = "pt"; break; case "Dutch": code = "nl"; break;
                case "Polish": code = "pl"; break; case "Czech": code = "cs"; break;
                case "Slovak": code = "sk"; break; case "Hungarian": code = "hu"; break;
                case "Romanian": code = "ro"; break; case "Bulgarian": code = "bg"; break;
                case "Greek": code = "el"; break; case "Russian": code = "ru"; break;
                case "Ukrainian": code = "uk"; break; case "Turkish": code = "tr"; break;
                case "Swedish": code = "sv"; break; case "Danish": code = "da"; break;
                case "Norwegian": code = "nb"; break; case "Finnish": code = "fi"; break;
                case "SerboCroatian": code = "hr"; break; case "Slovenian": code = "sl"; break;
                case "Lithuanian": code = "lt"; break; case "Latvian": code = "lv"; break;
                case "Estonian": code = "et"; break; case "Arabic": code = "ar"; break;
                case "Hebrew": code = "he"; break; case "Hindi": code = "hi"; break;
                case "ChineseTraditional": code = "zh-Hant"; break;
                case "Chinese": case "ChineseSimplified": code = "zh-Hans"; break;
                case "Japanese": code = "ja"; break; case "Korean": code = "ko"; break;
                case "Indonesian": code = "id"; break; case "Vietnamese": code = "vi"; break;
                case "Thai": code = "th"; break; default: code = "en"; break;
            }
            string regional = Normalize(CultureInfo.CurrentUICulture.Name);
            if (regional != null && (regional == code || regional.StartsWith(code + "-", StringComparison.OrdinalIgnoreCase)))
                code = regional;
            return code;
        }
        static void Load()
        {
            if (languages != null) return;
            var list = new List<Option>();
            try
            {
                TextAsset asset = Resources.Load<TextAsset>("Localization/InterfaceLanguages");
                Catalog catalog = asset == null ? null : JsonUtility.FromJson<Catalog>(asset.text);
                if (catalog?.keys != null && catalog.languages != null)
                {
                    for (int i = 0; i < catalog.keys.Length; i++) indices[catalog.keys[i]] = i;
                    foreach (Entry entry in catalog.languages)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.code) || options.ContainsKey(entry.code) ||
                            entry.words == null || entry.words.Length != catalog.keys.Length) continue;
                        var option = new Option(entry.code, entry.name, entry.englishName, entry.words);
                        list.Add(option); options.Add(option.Code, option);
                    }
                }
            }
            catch (Exception error) { Debug.LogWarning("Interface language catalog could not load: " + error.Message); }
            if (!options.ContainsKey("en"))
            {
                var english = new Option("en", "English", "English", Array.Empty<string>());
                list.Insert(0, english); options["en"] = english;
            }
            languages = list.AsReadOnly();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        { languages = null; selected = null; indices.Clear(); options.Clear(); Version = 0; }
    }
}
