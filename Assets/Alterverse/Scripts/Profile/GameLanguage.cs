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
        static readonly Dictionary<string, Dictionary<string, string>> fallbackTranslations =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["de"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["HOME"] = "START",
                    ["PLAY"] = "SPIELEN",
                    ["HEROES"] = "HELDEN",
                    ["STORE"] = "SHOP",
                    ["INVENTORY"] = "INVENTAR",
                    ["BATTLE PASS"] = "BATTLE PASS",
                    ["PROFILE"] = "PROFIL",
                    ["FRIENDS"] = "FREUNDE",
                    ["CLANS"] = "CLANS",
                    ["DAILY"] = "MISSIONEN",
                    ["RANKING"] = "RANGLISTE",
                    ["SETTINGS"] = "OPTIONEN",
                    ["Settings"] = "Einstellungen",
                    ["Audio and language."] = "Audio und Sprache.",
                    ["Audio, language and hardware graphics adaptation."] = "Audio, Sprache und automatische Hardware-Grafikanpassung.",
                    ["Master volume"] = "Gesamtlautstärke",
                    ["Music"] = "Musik",
                    ["Sound effects"] = "Soundeffekte",
                    ["Language"] = "Sprache",
                    ["Graphics"] = "Grafik",
                    ["Graphics & Hardware"] = "Grafik & Hardware",
                    ["AUTO (HARDWARE)"] = "AUTO (HARDWARE)",
                    ["LOW / SAVER"] = "NIEDRIG / AKKU",
                    ["BALANCED"] = "AUSGEWOGEN",
                    ["HIGH / 60 FPS"] = "HOCH / 60 FPS",
                    ["AUTO"] = "AUTO",
                    ["LOW"] = "NIEDRIG",
                    ["HIGH"] = "HOCH",
                    ["Optimized for older phones: 30 FPS, 65% render scale, reduced terrain & texture memory."] = "Optimiert für ältere Handys: 30 FPS, 65% Auflösung, reduzierter Terrain- & Texturspeicher.",
                    ["Balanced mobile profile: 60 FPS, 82% render scale, medium shadows & textures."] = "Ausgewogenes Profil: 60 FPS, 82% Auflösung, mittlere Schatten & Texturen.",
                    ["High performance profile: 60 FPS, 100% render scale, full lighting & details."] = "Hochleistungsprofil: 60 FPS, 100% Auflösung, volle Beleuchtung & Details.",
                    ["Changes apply immediately."] = "Änderungen werden sofort übernommen.",
                    ["Progress and settings are saved on this device."] = "Fortschritt und Einstellungen werden gespeichert.",
                    ["Deployment"] = "Einsatz",
                    ["Choose your hero and arena before entering the Front."] = "Wähle deinen Helden und die Arena für das nächste Gefecht.",
                    ["READY TO PLAY"] = "EINSATZBEREIT",
                    ["INVITE FRIENDS"] = "FREUNDE & LOBBY",
                    ["PRIVATE CO-OP"] = "PRIVATES KOOP",
                    ["READY FOR DEPLOYMENT"] = "BEREIT FÜR DEN EINSATZ",
                    ["ARENA"] = "ARENA",
                    ["SHUFFLE"] = "ZUFALL",
                    ["SHUFFLE  ON"] = "ZUFALL AN",
                    ["START MATCH"] = "MATCH STARTEN",
                    ["FIND ONLINE MATCH"] = "ONLINE-MATCH SUCHEN",
                    ["4v4 TACTICAL LOBBY"] = "4v4 TAKTISCHE LOBBY",
                    ["ONLINE PARTY & MATCHMAKING"] = "ONLINE-GRUPPE & MATCHMAKING",
                    ["CLOSE"] = "SCHLIESSEN",
                    ["PARTY LINKED"] = "GRUPPE AKTIV",
                    ["SOLO / OPEN"] = "SOLO / OFFEN",
                    ["CREATE FRIEND PARTY"] = "GRUPPE ERSTELLEN",
                    ["JOIN PARTY"] = "BEITRETEN",
                    ["LEAVE PARTY"] = "VERLASSEN",
                    ["READY"] = "BEREIT",
                    ["READY ✓"] = "BEREIT ✓",
                    ["NOT READY"] = "NICHT BEREIT",
                    ["WAITING"] = "WARTET",
                    ["OPEN SLOT"] = "FREIER PLATZ",
                    ["INVITE FRIEND"] = "FREUND EINLADEN",
                    ["COPY CODE"] = "CODE KOPIEREN",
                    ["CANCEL SEARCH"] = "SUCHE ABBRECHEN",
                    ["WAITING READY"] = "WARTE AUF BEREIT",
                    ["FIND 4v4 MATCH"] = "4v4 MATCH SUCHEN",
                    ["PREPARING BATTLEFIELD"] = "SCHLACHTFELD WIRD GELADEN",
                    ["SYNCHRONIZING BATTLEFIELD"] = "SYNCHRONISIERE SCHLACHTFELD",
                    ["Friends & Stickers"] = "Freunde & Sticker",
                    ["Connect with squadmates, send collectible Shard Stickers, and invite friends to your clan."] = "Verbinde dich mit Freunden, verschicke Shard-Sticker (20% Umwandlung) und lade sie in deinen Clan ein.",
                    ["MY FRIENDS"] = "MEINE FREUNDE",
                    ["REQUESTS"] = "ANFRAGEN",
                    ["STICKER SHOP & INBOX"] = "STICKER-SHOP & POSTFACH",
                    ["CLAN INVITES"] = "CLAN-EINLADUNGEN",
                    ["SEND REQUEST"] = "ANFRAGE SENDEN",
                    ["SEND STICKER"] = "STICKER SENDEN",
                    ["CLAN INVITE"] = "CLAN-EINLADUNG",
                    ["ACCEPT"] = "ANNEHMEN",
                    ["DECLINE"] = "ABLEHNEN",
                    ["CONVERT TO SHARDS"] = "IN SHARDS UMWANDELN",
                    ["MATCH OPTIONS"] = "MATCH-OPTIONEN",
                    ["MATCH PAUSED"] = "MATCH PAUSIERT",
                    ["CO-OP MATCH CONTINUES WHILE OPTIONS ARE OPEN"] = "KOOP-MATCH LÄUFT IM HINTERGRUND WEITER",
                    ["RESUME MATCH"] = "WEITERSPIELEN",
                    ["LEAVE MATCH  •  MAIN MENU"] = "MATCH VERLASSEN  •  HAUPTMENÜ",
                    ["LEAVE CO-OP  •  MAIN MENU"] = "KOOP VERLASSEN  •  HAUPTMENÜ",
                    ["LEAVE CURRENT MATCH?"] = "AKTUELLES MATCH VERLASSEN?",
                    ["CANCEL"] = "ABBRECHEN",
                    ["LEAVE MATCH"] = "VERLASSEN",
                    ["MATCH PLAYERS  •  TAP +FR TO SEND FRIEND REQUEST"] = "SPIELER IM MATCH  •  TIPPE AUF +FR FÜR FREUNDSCHAFTSANFRAGE",
                    ["RESPAWN IN "] = "WIEDERBELEBUNG IN ",
                    ["ZERO TEAM"] = "TEAM ZERO",
                    ["LUMEN TEAM"] = "TEAM LUMEN",
                    ["ASSASSIN"] = "ASSASSINE",
                    ["TANK"] = "TANK",
                    ["MARKSMAN"] = "SCHÜTZE",
                    ["MAGE"] = "MAGIER",
                    ["SUPPORT"] = "UNTERSTÜTZER",
                    ["BRUISER"] = "KÄMPFER",
                    ["CHOOSE. DEPLOY. CONQUER."] = "WÄHLEN. KÄMPFEN. SIEGEN.",
                    ["TWO REALITIES.\nONE FRONT."] = "ZWEI REALITÄTEN.\nEINE FRONT.",
                    ["Choose your hero. Fight alongside your team. Claim the Hotzone and break the enemy core."] = "Wähle deinen Helden. Kämpfe mit deinem Team, erobere die Hotzone und zerstöre den gegnerischen Kern.",
                    ["ENTER THE FRONT"] = "IN DEN KAMPF",
                    ["MISSIONS"] = "MISSIONEN"
                },
                ["es"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["HOME"] = "INICIO",
                    ["PLAY"] = "JUGAR",
                    ["HEROES"] = "HÉROES",
                    ["STORE"] = "TIENDA",
                    ["INVENTORY"] = "INVENTARIO",
                    ["PROFILE"] = "PERFIL",
                    ["FRIENDS"] = "AMIGOS",
                    ["CLANS"] = "CLANES",
                    ["DAILY"] = "DIARIO",
                    ["RANKING"] = "CLASIFICACIÓN",
                    ["SETTINGS"] = "AJUSTES",
                    ["Graphics"] = "Gráficos",
                    ["Graphics & Hardware"] = "Gráficos y Hardware"
                },
                ["fr"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["HOME"] = "ACCUEIL",
                    ["PLAY"] = "JOUER",
                    ["HEROES"] = "HÉROS",
                    ["STORE"] = "BOUTIQUE",
                    ["INVENTORY"] = "INVENTAIRE",
                    ["PROFILE"] = "PROFIL",
                    ["FRIENDS"] = "AMIS",
                    ["CLANS"] = "CLANS",
                    ["DAILY"] = "MISSIONS",
                    ["RANKING"] = "CLASSEMENT",
                    ["SETTINGS"] = "PARAMÈTRES",
                    ["Graphics"] = "Graphismes",
                    ["Graphics & Hardware"] = "Graphismes & Matériel"
                }
            };

        public static string Text(string source)
        {
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
            Option language = Current;
            if (indices.TryGetValue(source, out int index) && index < language.Words.Length &&
                !string.IsNullOrEmpty(language.Words[index]))
                return language.Words[index];
            string code = language?.Code ?? "en";
            int dash = code.IndexOf('-');
            string baseCode = dash > 0 ? code.Substring(0, dash) : code;
            if (fallbackTranslations.TryGetValue(code, out var map) && map.TryGetValue(source, out string translated))
                return translated;
            if (fallbackTranslations.TryGetValue(baseCode, out var baseMap) && baseMap.TryGetValue(source, out string baseTranslated))
                return baseTranslated;
            return source;
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
