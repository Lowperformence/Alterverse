using System;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.UI;
using UnityEngine;

namespace Alterverse.Profile
{
    public enum DailyQuestId
    {
        Kills,
        HotZones,
        SoldierKills
    }

    /// <summary>
    /// Persistent local daily-quest progress. The day boundary is midnight in Germany
    /// (Europe/Berlin, including daylight-saving time). A stored future day is never
    /// rolled backwards, which prevents repeated resets by simply moving the device clock back.
    /// </summary>
    public static class DailyQuestProgress
    {
        public const int KillTarget = 50;
        public const int KillReward = 20;
        public const int HotZoneTarget = 20;
        public const int HotZoneReward = 15;
        public const int SoldierKillTarget = 60;
        public const int SoldierKillReward = 10;

        private const string Prefix = "DF_DailyQuest_v1_";
        private const string DayKey = Prefix + "Day";
        private const string KillsKey = Prefix + "Kills";
        private const string HotZonesKey = Prefix + "HotZones";
        private const string SoldierKillsKey = Prefix + "SoldierKills";
        private const string KillClaimKey = Prefix + "Claim_Kills";
        private const string HotZoneClaimKey = Prefix + "Claim_HotZones";
        private const string SoldierClaimKey = Prefix + "Claim_SoldierKills";

        private static bool timeZoneResolved;
        private static TimeZoneInfo berlinTimeZone;

        public static event Action Changed;

        public static int Progress(DailyQuestId id)
        {
            EnsureCurrentDay();
            return Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey(id), 0), 0, Target(id));
        }

        public static int Target(DailyQuestId id)
        {
            return id switch
            {
                DailyQuestId.Kills => KillTarget,
                DailyQuestId.HotZones => HotZoneTarget,
                DailyQuestId.SoldierKills => SoldierKillTarget,
                _ => 1
            };
        }

        public static int Reward(DailyQuestId id)
        {
            return id switch
            {
                DailyQuestId.Kills => KillReward,
                DailyQuestId.HotZones => HotZoneReward,
                DailyQuestId.SoldierKills => SoldierKillReward,
                _ => 0
            };
        }

        public static string Label(DailyQuestId id)
        {
            return id switch
            {
                DailyQuestId.Kills => "50 KILLS",
                DailyQuestId.HotZones => "20 HOT ZONES EINGENOMMEN",
                DailyQuestId.SoldierKills => "60 SOLDATEN-KILLS",
                _ => "DAILY QUEST"
            };
        }

        public static bool IsComplete(DailyQuestId id) => Progress(id) >= Target(id);

        public static bool IsClaimed(DailyQuestId id)
        {
            EnsureCurrentDay();
            return PlayerPrefs.GetInt(ClaimKey(id), 0) != 0;
        }

        public static void RecordKill(bool soldierKill)
        {
            EnsureCurrentDay();
            bool changed = Increment(KillsKey, KillTarget, 1);
            if (soldierKill)
                changed |= Increment(SoldierKillsKey, SoldierKillTarget, 1);
            if (!changed) return;
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void RecordHotZoneCapture()
        {
            EnsureCurrentDay();
            if (!Increment(HotZonesKey, HotZoneTarget, 1)) return;
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool TryClaim(DailyQuestId id)
        {
            EnsureCurrentDay();
            if (!IsComplete(id) || IsClaimed(id)) return false;

            // Lock the claim before crediting the wallet. This deliberately favors never paying
            // the same daily reward twice if the app is interrupted during the claim operation.
            PlayerPrefs.SetInt(ClaimKey(id), 1);
            PlayerPrefs.Save();
            PlayerWallet.Grant(Reward(id));
            Changed?.Invoke();
            return true;
        }

        public static void EnsureCurrentDay()
        {
            int today = CurrentBerlinDay();
            int stored = PlayerPrefs.GetInt(DayKey, 0);

            if (stored == 0)
            {
                PlayerPrefs.SetInt(DayKey, today);
                PlayerPrefs.Save();
                return;
            }

            // Only move the daily state forward. Clock rollback therefore cannot create a second
            // claim window for a day that was already observed on this installation.
            if (today <= stored) return;

            PlayerPrefs.SetInt(DayKey, today);
            PlayerPrefs.SetInt(KillsKey, 0);
            PlayerPrefs.SetInt(HotZonesKey, 0);
            PlayerPrefs.SetInt(SoldierKillsKey, 0);
            PlayerPrefs.SetInt(KillClaimKey, 0);
            PlayerPrefs.SetInt(HotZoneClaimKey, 0);
            PlayerPrefs.SetInt(SoldierClaimKey, 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static bool Increment(string key, int maximum, int amount)
        {
            int before = Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, maximum);
            int after = Mathf.Clamp(before + Mathf.Max(0, amount), 0, maximum);
            if (after == before) return false;
            PlayerPrefs.SetInt(key, after);
            return true;
        }

        private static string ProgressKey(DailyQuestId id)
        {
            return id switch
            {
                DailyQuestId.Kills => KillsKey,
                DailyQuestId.HotZones => HotZonesKey,
                DailyQuestId.SoldierKills => SoldierKillsKey,
                _ => KillsKey
            };
        }

        private static string ClaimKey(DailyQuestId id)
        {
            return id switch
            {
                DailyQuestId.Kills => KillClaimKey,
                DailyQuestId.HotZones => HotZoneClaimKey,
                DailyQuestId.SoldierKills => SoldierClaimKey,
                _ => KillClaimKey
            };
        }

        private static int CurrentBerlinDay()
        {
            ResolveBerlinTimeZone();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset berlin = berlinTimeZone != null ? TimeZoneInfo.ConvertTime(now, berlinTimeZone) : now.ToLocalTime();
            return berlin.Year * 10000 + berlin.Month * 100 + berlin.Day;
        }

        private static void ResolveBerlinTimeZone()
        {
            if (timeZoneResolved) return;
            timeZoneResolved = true;

            // Android/Unix uses IANA IDs; Windows Editor commonly uses the Windows ID.
            string[] ids = { "Europe/Berlin", "W. Europe Standard Time" };
            for (int i = 0; i < ids.Length; i++)
            {
                try
                {
                    berlinTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ids[i]);
                    if (berlinTimeZone != null) return;
                }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            berlinTimeZone = TimeZoneInfo.Local;
        }
    }

    /// <summary>
    /// Runtime bridge for authoritative gameplay events plus the profile-page presentation.
    /// No Android plugin, package or network dependency is required.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class DailyQuestRuntime : MonoBehaviour
    {
        private static DailyQuestRuntime instance;
        private static readonly FieldInfo MenuPageField = typeof(MainMenuController).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly DailyQuestId[] QuestOrder =
        {
            DailyQuestId.Kills,
            DailyQuestId.HotZones,
            DailyQuestId.SoldierKills
        };

        private readonly HashSet<Health> trackedHealth = new();
        private FrontEndTheme theme;
        private float nextHealthScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<DailyQuestRuntime>() == null)
                new GameObject("DF_DailyQuests").AddComponent<DailyQuestRuntime>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            DailyQuestProgress.EnsureCurrentDay();
        }

        private void OnEnable() => HotZoneDirector.HotZoneResolved += OnHotZoneResolved;

        private void OnDisable()
        {
            HotZoneDirector.HotZoneResolved -= OnHotZoneResolved;
            foreach (Health health in trackedHealth)
                if (health != null) health.DiedWithSource -= OnHealthDied;
            trackedHealth.Clear();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            // Calling this while the app stays open across midnight makes the reset immediate
            // without requiring the player to restart or reopen the profile page.
            DailyQuestProgress.EnsureCurrentDay();

            if (Time.unscaledTime < nextHealthScan) return;
            nextHealthScan = Time.unscaledTime + .08f;
            TrackActiveHealth();
        }

        private void TrackActiveHealth()
        {
            trackedHealth.RemoveWhere(health => health == null);
            IReadOnlyList<Health> active = Health.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Health health = active[i];
                if (health == null || !trackedHealth.Add(health)) continue;
                health.DiedWithSource += OnHealthDied;
            }
        }

        private void OnHealthDied(Health victim, GameObject source)
        {
            if (victim == null || source == null || !IsLocalPlayerSource(source)) return;

            bool soldier = victim.GetComponent<LaneMinion>() != null;
            bool hero = victim.GetComponent<HeroIdentity>() != null;
            if (!soldier && !hero) return;

            // A soldier kill advances both the general kill quest and the soldier-specific quest.
            DailyQuestProgress.RecordKill(soldier);
        }

        private static bool IsLocalPlayerSource(GameObject source)
        {
            TeamMember member = source != null ? source.GetComponentInParent<TeamMember>() : null;
            if (member == null) return false;

            if (CoopAuthority.Active)
            {
                CoopHeroSlot slot = member.GetComponent<CoopHeroSlot>();
                return slot != null && slot.IsLocalHuman;
            }

            return member.GetComponent<HeroRespawn>() != null;
        }

        private void OnHotZoneResolved(int winner, float zeroScore, float lumenScore)
        {
            if (winner < 0 || !TryGetLocalTeam(out int localTeam) || winner != localTeam) return;
            DailyQuestProgress.RecordHotZoneCapture();
        }

        private static bool TryGetLocalTeam(out int team)
        {
            if (CoopAuthority.Active)
            {
                CoopHeroSlot[] slots = FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
                for (int i = 0; i < slots.Length; i++)
                {
                    CoopHeroSlot slot = slots[i];
                    if (slot == null || !slot.IsLocalHuman) continue;
                    TeamMember member = slot.GetComponent<TeamMember>();
                    if (member == null) continue;
                    team = member.Team;
                    return true;
                }
            }
            else
            {
                HeroRespawn respawn = FindAnyObjectByType<HeroRespawn>();
                TeamMember member = respawn != null ? respawn.GetComponent<TeamMember>() : null;
                if (member != null)
                {
                    team = member.Team;
                    return true;
                }
            }

            team = -1;
            return false;
        }

        private void OnGUI()
        {
            if (!MainMenuController.IsVisible) return;
            MainMenuController menu = FindAnyObjectByType<MainMenuController>();
            if (menu == null || !IsProfilePage(menu)) return;

            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) return;
            float scale = Mathf.Min(safe.height / 720f, safe.width / 1200f);
            if (scale <= 0) return;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            try
            {
                GUI.depth = -1000;
                float menuWidth = safe.width / scale;
                float contentWidth = Mathf.Min(1360f, menuWidth - 64f);
                float left = (menuWidth - contentWidth) * .5f;
                float identityWidth = contentWidth * .54f;
                float x = left + identityWidth + 32f;
                float w = contentWidth - identityWidth - 32f;
                float yOffset = Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, yOffset, 0), Quaternion.identity, Vector3.one * scale);
                GUI.color = Color.white;
                GUI.enabled = true;

                theme ??= new FrontEndTheme();
                DrawProfileQuests(new Rect(x, 220f, w, 450f));
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
            }
        }

        private static bool IsProfilePage(MainMenuController menu)
        {
            if (MenuPageField == null || menu == null) return false;
            object page = MenuPageField.GetValue(menu);
            return page != null && string.Equals(page.ToString(), "Profile", StringComparison.Ordinal);
        }

        private void DrawProfileQuests(Rect area)
        {
            // Opaque base fully hides the old lifetime-stat rows before the new profile content is drawn.
            FrontEndTheme.Fill(area, FrontEndTheme.Background);
            theme.Panel(area);

            ProfileStats stats = ProfileStatsSummary.Current;
            theme.Label(new Rect(area.x + 20f, area.y + 14f, area.width - 40f, 22f), "WIN RATE", theme.Small, true);
            theme.Label(new Rect(area.x + 20f, area.y + 34f, area.width - 40f, 52f), $"{stats.WinRate:0}%", theme.Title);

            FrontEndTheme.Fill(new Rect(area.x + 20f, area.y + 91f, area.width - 40f, 1f), new Color(1f, 1f, 1f, .13f));
            theme.Label(new Rect(area.x + 20f, area.y + 102f, area.width - 40f, 30f), "DAILY QUESTS", theme.Heading);
            theme.Label(new Rect(area.x + 20f, area.y + 132f, area.width - 40f, 20f), "RESET 00:00  •  DEUTSCHE ZEIT", theme.Small, true);

            float rowY = area.y + 160f;
            for (int i = 0; i < QuestOrder.Length; i++)
            {
                DrawQuestRow(new Rect(area.x + 16f, rowY + i * 88f, area.width - 32f, 78f), QuestOrder[i]);
            }
        }

        private void DrawQuestRow(Rect row, DailyQuestId id)
        {
            int progress = DailyQuestProgress.Progress(id);
            int target = DailyQuestProgress.Target(id);
            int reward = DailyQuestProgress.Reward(id);
            bool complete = progress >= target;
            bool claimed = DailyQuestProgress.IsClaimed(id);

            FrontEndTheme.Fill(row, new Color(FrontEndTheme.Raised.r, FrontEndTheme.Raised.g, FrontEndTheme.Raised.b, .72f));
            float buttonWidth = Mathf.Clamp(row.width * .30f, 102f, 132f);
            float textWidth = row.width - buttonWidth - 28f;

            theme.Label(new Rect(row.x + 12f, row.y + 7f, textWidth, 24f), DailyQuestProgress.Label(id), theme.Small);
            theme.Label(new Rect(row.x + 12f, row.y + 31f, textWidth, 18f), $"{progress} / {target}   •   +{reward} SHARDS", theme.Small, true);
            theme.Progress(new Rect(row.x + 12f, row.y + 57f, textWidth, 6f), target <= 0 ? 0f : progress / (float)target);

            Rect button = new Rect(row.xMax - buttonWidth - 10f, row.y + 16f, buttonWidth, 46f);
            string label = claimed ? "ABGEHOLT" : complete ? "ABHOLEN" : $"{reward} SHARDS";
            if (theme.Action(button, label, primary: complete && !claimed, enabled: complete && !claimed))
                DailyQuestProgress.TryClaim(id);
        }
    }
}
