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
                    ["Online lobby could not open. Please try again."] = "Online-Lobby konnte nicht geöffnet werden. Bitte erneut versuchen.",
                    ["GAME LINK"] = "SPIEL-LINK",
                    ["INVITE TO GAME"] = "ZUM SPIEL EINLADEN",
                    ["Play Dimensions Front with me!"] = "Spiele Dimensions Front mit mir!",
                    ["Party code: "] = "Gruppencode: ",
                    ["Open Play Online, then join the party with this code."] = "Öffne Online spielen und tritt mit diesem Code der Gruppe bei.",
                    ["Choose an app to share the invitation."] = "Wähle eine App zum Teilen der Einladung.",
                    ["Invitation copied. Paste it into WhatsApp or another app."] = "Einladung kopiert. Füge sie in WhatsApp oder einer anderen App ein.",

                    ["OFFLINE LOBBY"] = "OFFLINE-LOBBY",
                    ["Choose your hero and both teams before starting a local 4v4 match."] = "Wähle deinen Helden und beide Teams für dein lokales 4-gegen-4-Match.",
                    ["YOUR TEAM"] = "DEIN TEAM",
                    ["OPPONENT TEAM"] = "GEGNERTEAM",
                    ["YOU"] = "DU",
                    ["AI BOT"] = "KI-SPIELER",
                    ["CHOOSE YOUR HERO"] = "WÄHLE DEINEN HELDEN",
                    ["CHOOSE BOT HERO"] = "WÄHLE DEN KI-HELDEN",
                    ["START OFFLINE MATCH"] = "OFFLINE-MATCH STARTEN",
                    ["SHUFFLE BOTS"] = "KI-HELDEN AUSLOSEN",
                    ["CHOOSE HERO"] = "HELD WÄHLEN",
                    ["LOCKED"] = "GESPERRT",
                    ["TAKEN"] = "BELEGT",
                    ["One hero per team. Locked and taken heroes cannot be selected."] = "Jeder Held nur einmal pro Team. Gesperrte und belegte Helden sind nicht auswählbar.",
                    ["USE AN OPEN MICROPHONE?"] = "OFFENES MIKROFON VERWENDEN?",
                    ["Your party can hear you while the microphone is on. You can mute it at any time. Playing without voice is always possible."] = "Deine Gruppe hört dich, solange das Mikrofon eingeschaltet ist. Du kannst es jederzeit stummschalten und auch ohne Sprachchat spielen.",
                    ["WITHOUT MICROPHONE"] = "OHNE MIKROFON",
                    ["ENABLE MICROPHONE"] = "MIKROFON EINSCHALTEN",
                    ["MICROPHONE ON"] = "MIKROFON AN",
                    ["MICROPHONE MUTED"] = "MIKROFON STUMM",
                    ["MICROPHONE PERMISSION DENIED"] = "MIKROFON NICHT FREIGEGEBEN",
                    ["VOICE OFF"] = "SPRACHCHAT AUS",
                    ["VOICE UNAVAILABLE"] = "SPRACHCHAT NICHT VERFÜGBAR",
                    ["RECEIVED INVITATIONS"] = "ERHALTENE EINLADUNGEN",
                    ["NO INVITATIONS"] = "KEINE EINLADUNGEN",
                    ["NO ACCEPTED FRIENDS YET"] = "NOCH KEINE BESTÄTIGTEN FREUNDE",
                    ["CREATE A PARTY FIRST"] = "ERST EINE GRUPPE ERSTELLEN",
                    ["INVITE"] = "EINLADEN",

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
                    ["MISSIONS"] = "MISSIONEN",
                    ["BATTLE PASS"] = "BATTLE PASS",
                    ["SHARDS"] = "SPLITTER",
                    ["BACK"] = "ZURÜCK",
                    ["VICTORY"] = "SIEG",
                    ["DEFEAT"] = "NIEDERLAGE",
                    ["PLAY AGAIN"] = "NOCHMAL SPIELEN",
                    ["MAIN MENU"] = "HAUPTMENÜ",
                    ["SUMMARY"] = "ÜBERSICHT",
                    ["SCOREBOARD"] = "RANGLISTE",
                    ["DETAILS"] = "DETAILS",
                    ["HERO"] = "HELD",
                    ["RESULT"] = "ERGEBNIS",
                    ["K / D / A"] = "K / T / A",
                    ["KILLS"] = "KILLS",
                    ["DEATHS"] = "TODE",
                    ["ASSISTS"] = "ASSISTS",
                    ["OBJECTIVES"] = "ZIELE",
                    ["REWARDS"] = "BELOHNUNGEN",
                    ["SHARDS EARNED"] = "VERDIENTE SPLITTER",
                    ["PLAYER XP"] = "SPIELER-XP",
                    ["BATTLE PASS XP"] = "BATTLE-PASS-XP",
                    ["PROGRESSION"] = "FORTSCHRITT",
                    ["MATCH RECAP"] = "MATCH-ZUSAMMENFASSUNG",
                    ["TEAMWORK SCORE"] = "TEAMWORK-WERTUNG",
                    ["PERFORMANCE"] = "LEISTUNG",
                    ["COMBAT TELEMETRY"] = "KAMPF-TELEMETRIE",
                    ["TEAM ROSTER"] = "TEAM-AUFSTELLUNG",
                    ["MATCH MVP"] = "MATCH-MVP",
                    ["★ MATCH MVP"] = "★ MATCH-MVP",
                    ["COMBAT GRADE"] = "KAMPFWERTUNG",
                    ["BASE"] = "BASIS",
                    ["BONUS"] = "BONUS",
                    ["LEVEL UP!"] = "STUFENAUFSTIEG!",
                    ["ZERO TEAM"] = "TEAM ZERO",
                    ["LUMEN TEAM"] = "TEAM LUMEN",
                    ["ZERO REALITY"] = "ZERO-REALITÄT",
                    ["LUMEN REALITY"] = "LUMEN-REALITÄT",
                    ["ENEMY CORE DESTROYED"] = "GEGNERISCHER KERN ZERSTÖRT",
                    ["ALLY CORE DESTROYED"] = "EIGENER KERN ZERSTÖRT",
                    ["FRONT DOMINANCE VICTORY"] = "SIEG DURCH FRONT-DOMINANZ",
                    ["FRONT LOST AT TIME LIMIT"] = "FRONT NACH ZEITABLAUF VERLOREN",
                    ["HALFTIME PASSIVE UNLOCKED"] = "HALBZEIT-PASSIV FREIGESCHALTET",
                    ["CHOOSE ONE BONUS"] = "WÄHLE EINEN BONUS",
                    ["MOVEMENT SPEED"] = "BEWEGUNGSTEMPO",
                    ["OUTGOING DAMAGE"] = "VERURSACHTER SCHADEN",
                    ["MAX HEALTH"] = "MAXIMALE LEBENSPUNKTE",
                    ["RECOMMENDED"] = "EMPFOHLEN",
                    ["MID-MATCH AUGMENT READY"] = "HALBZEIT-AUGMENT BEREIT",
                    ["CHOOSE ONE BONUS"] = "WÄHLE EINEN BONUS",
                    ["MOVEMENT SPEED"] = "BEWEGUNGSTEMPO",
                    ["OUTGOING DAMAGE"] = "VERURSACHTER SCHADEN",
                    ["MAX HEALTH"] = "MAXIMALE LP",
                    ["RECOMMENDED"] = "EMPFOHLEN",
                    ["DOUBLE KILL!"] = "DOPPEL-KILL!",
                    ["TRIPLE KILL!"] = "DREIFACH-KILL!",
                    ["QUADRA FRONT WIPE!"] = "VIERFACH-WIPE!",
                    ["DOMINATING!"] = "DOMINIEREND!",
                    ["UNSTOPPABLE!"] = "UNAUFHALTSAM!",
                    ["SHUTDOWN!"] = "SERIE BEENDET!",
                    ["LAST STAND!"] = "LETZTES AUFGEBOT!",
                    ["TAKEDOWN!"] = "ELIMINIERT!",
                    ["CAPTURING"] = "EROBERT",
                    ["CONTESTED"] = "UMKÄMPFT",
                    ["SELECT 1 PERMANENT BUFF FOR THE REMAINDER OF THE MATCH  •  KEYS [1] [2] [3]"] = "WÄHLE 1 DAUERHAFTEN BONUS FÜR DEN REST DES MATCHES  •  TASTEN [1] [2] [3]",
                    ["PASSIVE ACTIVE"] = "PASSIV AKTIV",
                    ["PERMANENT HALFTIME AUGMENT"] = "DAUERHAFTER HALBZEIT-BONUS",
                    ["+15% Movement Speed"] = "+15 % Bewegungstempo",
                    ["+15% Outgoing Damage"] = "+15 % Verursachter Schaden",
                    ["+15% Max Health"] = "+15 % Maximale LP",
                    ["MOBILITY"] = "MOBILITÄT",
                    ["OFFENSE"] = "OFFENSIVE",
                    ["SURVIVABILITY"] = "ÜBERLEBENSKRAFT",
                    ["OVERDRIVE STEP"] = "ÜBERSCHUB-SCHRITT",
                    ["RIFT AMPLIFIER"] = "RISS-VERSTÄRKER",
                    ["AEGIS CORE"] = "ÄGIS-KERN",
                    ["SPRINT / STRAFE"] = "SPRINT / FLANKE",
                    ["ALL ATTACKS & SKILLS"] = "ALLE ANGRIFFE & FÄHIGKEITEN",
                    ["INSTANT HEAL + MAX HP"] = "SOFORTHEILUNG + MAX. LP",
                    ["KEY [1]  •  TAP TO EQUIP"] = "TASTE [1]  •  TIPPEN ZUM AUSRÜSTEN",
                    ["KEY [2]  •  TAP TO EQUIP"] = "TASTE [2]  •  TIPPEN ZUM AUSRÜSTEN",
                    ["KEY [3]  •  TAP TO EQUIP"] = "TASTE [3]  •  TIPPEN ZUM AUSRÜSTEN",
                    ["ENTERING THE FRONT"] = "BETRETE DIE FRONT",
                    ["PREPARING BATTLEFIELD"] = "SCHLACHTFELD WIRD VORBEREITET",
                    ["TACTICAL INTEL"] = "TAKTISCHE INFOS",
                    ["CHAMPION"] = "CHAMPION",
                    ["4v4 TACTICAL DEPLOYMENT"] = "4v4 TAKTISCHER EINSATZ",
                    ["KILLS MATTER MOST WHEN THEY CREATE SPACE FOR YOUR TEAM TO MOVE THE FRONT."] = "KILLS SIND AM WICHTIGSTEN, WENN SIE DEINEM TEAM RAUM VERSCHAFFEN, DIE FRONT ZU VERSCHIEBEN."
                },
                ["es"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["HOME"] = "INICIO",
                    ["PLAY"] = "JUGAR",
                    ["HEROES"] = "HÉROES",
                    ["STORE"] = "TIENDA",
                    ["INVENTORY"] = "INVENTARIO",
                    ["BATTLE PASS"] = "PASE DE BATALLA",
                    ["PROFILE"] = "PERFIL",
                    ["FRIENDS"] = "AMIGOS",
                    ["CLANS"] = "CLANES",
                    ["DAILY"] = "DIARIO",
                    ["RANKING"] = "CLASIFICACIÓN",
                    ["SETTINGS"] = "AJUSTES",
                    ["SHARDS"] = "FRAGMENTOS",
                    ["BACK"] = "ATRÁS",
                    ["OFFLINE LOBBY"] = "SALA SIN CONEXIÓN",
                    ["YOUR TEAM"] = "TU EQUIPO",
                    ["OPPONENT TEAM"] = "EQUIPO RIVAL",
                    ["YOU"] = "TÚ",
                    ["AI BOT"] = "BOT IA",
                    ["CHOOSE YOUR HERO"] = "ELIGE TU HÉROE",
                    ["START OFFLINE MATCH"] = "INICIAR PARTIDA",
                    ["SHUFFLE BOTS"] = "MEZCLAR BOTS",
                    ["LOCKED"] = "BLOQUEADO",
                    ["TAKEN"] = "OCUPADO",
                    ["VICTORY"] = "VICTORIA",
                    ["DEFEAT"] = "DERROTA",
                    ["PLAY AGAIN"] = "JUGAR DE NUEVO",
                    ["MAIN MENU"] = "MENÚ PRINCIPAL",
                    ["SUMMARY"] = "RESUMEN",
                    ["SCOREBOARD"] = "MARCADOR",
                    ["DETAILS"] = "DETALLES",
                    ["HERO"] = "HÉROE",
                    ["RESULT"] = "RESULTADO",
                    ["KILLS"] = "BAJAS",
                    ["DEATHS"] = "MUERTES",
                    ["ASSISTS"] = "ASISTENCIAS",
                    ["OBJECTIVES"] = "OBJETIVOS",
                    ["REWARDS"] = "RECOMPENSAS",
                    ["SHARDS EARNED"] = "FRAGMENTOS GANADOS",
                    ["PLAYER XP"] = "XP DE JUGADOR",
                    ["BATTLE PASS XP"] = "XP DEL PASE",
                    ["PROGRESSION"] = "PROGRESO",
                    ["MATCH RECAP"] = "RESUMEN DE PARTIDA",
                    ["TEAMWORK SCORE"] = "PUNTUACIÓN DE EQUIPO",
                    ["MATCH MVP"] = "MVP DE LA PARTIDA",
                    ["★ MATCH MVP"] = "★ MVP DE LA PARTIDA",
                    ["COMBAT GRADE"] = "RANGO DE COMBATE",
                    ["HALFTIME PASSIVE UNLOCKED"] = "PASIVA DE MEDIO TIEMPO",
                    ["CHOOSE ONE BONUS"] = "ELIGE UNA MEJORA",
                    ["MOVEMENT SPEED"] = "VELOCIDAD DE MOVIMIENTO",
                    ["OUTGOING DAMAGE"] = "DAÑO INFLIGIDO",
                    ["MAX HEALTH"] = "SALUD MÁXIMA",
                    ["RECOMMENDED"] = "RECOMENDADO",
                    ["MID-MATCH AUGMENT READY"] = "MEJORA DE MITAD DE PARTIDA",
                    ["PASSIVE ACTIVE"] = "PASIVA ACTIVA",
                    ["+15% Movement Speed"] = "+15 % Velocidad de movimiento",
                    ["+15% Outgoing Damage"] = "+15 % Daño infligido",
                    ["+15% Max Health"] = "+15 % Salud máxima",
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
                    ["BATTLE PASS"] = "PASSE DE COMBAT",
                    ["PROFILE"] = "PROFIL",
                    ["FRIENDS"] = "AMIS",
                    ["CLANS"] = "CLANS",
                    ["DAILY"] = "MISSIONS",
                    ["RANKING"] = "CLASSEMENT",
                    ["SETTINGS"] = "PARAMÈTRES",
                    ["SHARDS"] = "ÉCLATS",
                    ["BACK"] = "RETOUR",
                    ["OFFLINE LOBBY"] = "SALON HORS LIGNE",
                    ["YOUR TEAM"] = "VOTRE ÉQUIPE",
                    ["OPPONENT TEAM"] = "ÉQUIPE ADVERSE",
                    ["YOU"] = "VOUS",
                    ["AI BOT"] = "BOT IA",
                    ["CHOOSE YOUR HERO"] = "CHOISISSEZ VOTRE HÉROS",
                    ["START OFFLINE MATCH"] = "LANCER LA PARTIE",
                    ["SHUFFLE BOTS"] = "MÉLANGER LES BOTS",
                    ["LOCKED"] = "VERROUILLÉ",
                    ["TAKEN"] = "PRIS",
                    ["VICTORY"] = "VICTOIRE",
                    ["DEFEAT"] = "DÉFAITE",
                    ["PLAY AGAIN"] = "REJOUER",
                    ["MAIN MENU"] = "MENU PRINCIPAL",
                    ["SUMMARY"] = "RÉSUMÉ",
                    ["SCOREBOARD"] = "TABLEAU DES SCORES",
                    ["DETAILS"] = "DÉTAILS",
                    ["HERO"] = "HÉROS",
                    ["RESULT"] = "RÉSULTAT",
                    ["KILLS"] = "ÉLIMINATIONS",
                    ["DEATHS"] = "MORTS",
                    ["ASSISTS"] = "ASSISTANCES",
                    ["OBJECTIVES"] = "OBJECTIFS",
                    ["REWARDS"] = "RÉCOMPENSES",
                    ["SHARDS EARNED"] = "ÉCLATS GAGNÉS",
                    ["PLAYER XP"] = "XP JOUEUR",
                    ["BATTLE PASS XP"] = "XP PASSE DE COMBAT",
                    ["PROGRESSION"] = "PROGRESSION",
                    ["MATCH RECAP"] = "BILAN DU MATCH",
                    ["TEAMWORK SCORE"] = "SCORE D'ÉQUIPE",
                    ["MATCH MVP"] = "MVP DU MATCH",
                    ["★ MATCH MVP"] = "★ MVP DU MATCH",
                    ["COMBAT GRADE"] = "NOTE DE COMBAT",
                    ["HALFTIME PASSIVE UNLOCKED"] = "PASSIF DE MI-TEMPS DÉBLOQUÉ",
                    ["CHOOSE ONE BONUS"] = "CHOISISSEZ UN BONUS",
                    ["MOVEMENT SPEED"] = "VITESSE DE DÉPLACEMENT",
                    ["OUTGOING DAMAGE"] = "DÉGÂTS INFLIGÉS",
                    ["MAX HEALTH"] = "SANTÉ MAXIMALE",
                    ["RECOMMENDED"] = "RECOMMANDÉ",
                    ["MID-MATCH AUGMENT READY"] = "AMÉLIORATION DE MI-PARTIE PRÊTE",
                    ["PASSIVE ACTIVE"] = "PASSIF ACTIF",
                    ["+15% Movement Speed"] = "+15 % Vitesse de déplacement",
                    ["+15% Outgoing Damage"] = "+15 % Dégâts infligés",
                    ["+15% Max Health"] = "+15 % PV max",
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
            if (TryTranslateDynamic(source, baseCode, out string dynamicTranslated))
                return dynamicTranslated;
            return source;
        }

        static bool TryTranslateDynamic(string source, string baseCode, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(source) || baseCode == "en")
                return false;

            if (source.StartsWith("RESPAWN IN ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = source.Substring("RESPAWN IN ".Length);
                translated = baseCode == "de" ? "WIEDERBELEBUNG IN " + rest
                    : baseCode == "es" ? "REAPARICIÓN EN " + rest
                    : baseCode == "fr" ? "RÉAPPARITION DANS " + rest : null;
                return translated != null;
            }
            if (source.StartsWith("OBJECTIVES  ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = source.Substring("OBJECTIVES  ".Length);
                translated = baseCode == "de" ? "ZIELE  " + rest
                    : baseCode == "es" ? "OBJETIVOS  " + rest
                    : baseCode == "fr" ? "OBJECTIFS  " + rest : null;
                return translated != null;
            }
            if (source.StartsWith("ACCOUNT  /  LEVEL ", StringComparison.OrdinalIgnoreCase))
            {
                string lvl = source.Substring("ACCOUNT  /  LEVEL ".Length);
                translated = baseCode == "de" ? "KONTO  /  STUFE " + lvl
                    : baseCode == "es" ? "CUENTA  /  NIVEL " + lvl
                    : baseCode == "fr" ? "COMPTE  /  NIVEAU " + lvl : null;
                return translated != null;
            }
            if (source.StartsWith("BATTLE PASS  /  LEVEL ", StringComparison.OrdinalIgnoreCase))
            {
                string lvl = source.Substring("BATTLE PASS  /  LEVEL ".Length);
                translated = baseCode == "de" ? "BATTLE PASS  /  STUFE " + lvl
                    : baseCode == "es" ? "PASE DE BATALLA  /  NIVEL " + lvl
                    : baseCode == "fr" ? "PASSE DE COMBAT  /  NIVEAU " + lvl : null;
                return translated != null;
            }
            if (source.StartsWith("MATCH DROP  /  ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = source.Substring("MATCH DROP  /  ".Length);
                translated = baseCode == "de" ? "MATCH-BEUTE  /  " + rest
                    : baseCode == "es" ? "BOTÍN DE PARTIDA  /  " + rest
                    : baseCode == "fr" ? "BUTIN DE MATCH  /  " + rest : null;
                return translated != null;
            }
            if (source.StartsWith("DROPS ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = source.Substring("DROPS ".Length);
                translated = baseCode == "de" ? "BEUTE " + rest
                    : baseCode == "es" ? "BOTÍN " + rest
                    : baseCode == "fr" ? "BUTIN " + rest : null;
                return translated != null;
            }
            if (source.EndsWith(" BONUS", StringComparison.OrdinalIgnoreCase) && source.Contains(" BASE"))
            {
                translated = baseCode == "de" ? source.Replace(" BASE", " BASIS")
                    : baseCode == "es" ? source.Replace(" BONUS", " BONO") : null;
                return translated != null;
            }
            if (source.EndsWith(" WING BONUS", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = source.Substring(0, source.Length - " WING BONUS".Length);
                translated = baseCode == "de" ? prefix + " FLÜGEL-BONUS"
                    : baseCode == "es" ? prefix + " BONO DE ALAS"
                    : baseCode == "fr" ? prefix + " BONUS D'AILES" : null;
                return translated != null;
            }
            if (source.EndsWith("  •  LEVEL UP", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = source.Substring(0, source.Length - "  •  LEVEL UP".Length);
                translated = baseCode == "de" ? prefix + "  •  STUFENAUFSTIEG"
                    : baseCode == "es" ? prefix + "  •  SUBIDA DE NIVEL"
                    : baseCode == "fr" ? prefix + "  •  NIVEAU SUPÉRIEUR" : null;
                return translated != null;
            }
            return false;
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
