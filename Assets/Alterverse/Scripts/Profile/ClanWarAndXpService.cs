using System;
using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.Profile
{
    /// <summary>
    /// Manages Clan Wars (up to 5 active enemy clans, declare/end war, online match skull markers,
    /// +1 Shard per enemy clan kill) and balanced Clan XP Shard donations.
    /// Persisted in AccountProfilePrefs so state syncs with the cloud account profile.
    /// </summary>
    public static class ClanWarAndXpService
    {
        public const int MaxEnemyClans = 5;
        public const int EnemyClanKillShardReward = 1;

        // Balanced Shard -> Clan XP conversion:
        // 50 Shards grants +25 Clan XP (2 Shards per 1 Clan XP), capped at 8 donations/day (200 XP/day)
        // with an escalating bonus-level curve so Shards provide steady support without overpowering play.
        public const int DonationShardCost = 50;
        public const int DonationClanXpGain = 25;
        public const int DonationAccountXpBonus = 10;
        public const int MaxDailyDonations = 8;
        public const int MaxClanBonusLevel = 50;

        private const string WarStateKeyPrefix = "DF_ClanWars_v1_";
        private const string ClanXpKeyPrefix = "DF_ClanDonatedXp_v1_";
        private const string DailyDonationDayKey = "DF_ClanDonateDay_v1";
        private const string DailyDonationCountKey = "DF_ClanDonateCount_v1";

        [Serializable]
        public sealed class EnemyClanEntry
        {
            public string clanId;
            public string name;
            public string tag;
            public long declaredAtUnix;
        }

        [Serializable]
        private sealed class WarListWrapper
        {
            public List<EnemyClanEntry> enemies = new List<EnemyClanEntry>();
        }

        private static bool IsGerman =>
            string.Equals(GameLanguage.Current?.Code, "de", StringComparison.OrdinalIgnoreCase);

        private static string cachedScope;
        private static List<EnemyClanEntry> cachedEnemies;

        private static readonly string[] slotClanIds = new string[MatchSession.MaxPlayers];
        private static readonly string[] slotClanTags = new string[MatchSession.MaxPlayers];

        private static string CurrentScope()
        {
            string ownClanId = SocialService.Instance?.State?.clan?.clanId;
            if (!string.IsNullOrEmpty(ownClanId)) return ownClanId;
            string account = AccountProfilePrefs.Account;
            return !string.IsNullOrEmpty(account) ? account : "local";
        }

        private static void EnsureLoaded()
        {
            string scope = CurrentScope();
            if (cachedEnemies != null && cachedScope == scope) return;
            cachedScope = scope;
            cachedEnemies = new List<EnemyClanEntry>();
            string raw = AccountProfilePrefs.GetString(WarStateKeyPrefix + scope, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    WarListWrapper wrapper = JsonUtility.FromJson<WarListWrapper>(raw);
                    if (wrapper?.enemies != null)
                    {
                        for (int i = 0; i < wrapper.enemies.Count && cachedEnemies.Count < MaxEnemyClans; i++)
                        {
                            EnemyClanEntry e = wrapper.enemies[i];
                            if (e != null && !string.IsNullOrEmpty(e.clanId) && e.clanId != scope)
                                cachedEnemies.Add(e);
                        }
                    }
                }
                catch (Exception)
                {
                    cachedEnemies.Clear();
                }
            }
        }

        private static void SaveWars()
        {
            EnsureLoaded();
            var wrapper = new WarListWrapper { enemies = cachedEnemies };
            AccountProfilePrefs.SetString(WarStateKeyPrefix + cachedScope, JsonUtility.ToJson(wrapper));
            AccountProfilePrefs.Save();
        }

        public static IReadOnlyList<EnemyClanEntry> GetEnemyClans()
        {
            EnsureLoaded();
            return cachedEnemies;
        }

        public static int ActiveWarCount
        {
            get
            {
                EnsureLoaded();
                return cachedEnemies.Count;
            }
        }

        public static bool IsAtWarWithClan(string clanId)
        {
            if (string.IsNullOrEmpty(clanId)) return false;
            EnsureLoaded();
            for (int i = 0; i < cachedEnemies.Count; i++)
                if (string.Equals(cachedEnemies[i].clanId, clanId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static bool IsAtWarWithTag(string clanTag)
        {
            if (string.IsNullOrEmpty(clanTag)) return false;
            EnsureLoaded();
            for (int i = 0; i < cachedEnemies.Count; i++)
                if (string.Equals(cachedEnemies[i].tag, clanTag, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static bool TryGetEnemyClan(string clanId, out EnemyClanEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(clanId)) return false;
            EnsureLoaded();
            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                if (string.Equals(cachedEnemies[i].clanId, clanId, StringComparison.Ordinal))
                {
                    entry = cachedEnemies[i];
                    return true;
                }
            }
            return false;
        }

        public static bool CanDeclareWar(string clanId, out string reason)
        {
            EnsureLoaded();
            string ownClanId = SocialService.Instance?.State?.clan?.clanId;
            if (string.IsNullOrEmpty(ownClanId))
            {
                reason = IsGerman
                    ? "Tritt zuerst einem Clan bei, um Kriege zu erklären."
                    : "Join a clan first to declare clan wars.";
                return false;
            }
            if (string.IsNullOrEmpty(clanId) || string.Equals(clanId, ownClanId, StringComparison.Ordinal))
            {
                reason = IsGerman
                    ? "Du kannst deinem eigenen Clan nicht den Krieg erklären."
                    : "You cannot declare war on your own clan.";
                return false;
            }
            if (IsAtWarWithClan(clanId))
            {
                reason = IsGerman
                    ? "Dein Clan befindet sich bereits im Krieg mit diesem Clan."
                    : "Your clan is already at war with this clan.";
                return false;
            }
            if (cachedEnemies.Count >= MaxEnemyClans)
            {
                reason = IsGerman
                    ? $"Maximal {MaxEnemyClans} Feind-Clans gleichzeitig möglich. Beende zuerst einen Krieg."
                    : $"Maximum of {MaxEnemyClans} enemy clans reached. End an active war first.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static bool DeclareWar(string clanId, string clanName, string clanTag, out string statusMessage)
        {
            if (!CanDeclareWar(clanId, out statusMessage)) return false;
            cachedEnemies.Add(new EnemyClanEntry
            {
                clanId = clanId,
                name = string.IsNullOrEmpty(clanName) ? "CLAN" : clanName,
                tag = string.IsNullOrEmpty(clanTag) ? "WAR" : clanTag,
                declaredAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            SaveWars();
            statusMessage = IsGerman
                ? $"☠ Krieg erklärt gegen [{clanTag}] {clanName}! ({cachedEnemies.Count}/{MaxEnemyClans})"
                : $"☠ Declared war on [{clanTag}] {clanName}! ({cachedEnemies.Count}/{MaxEnemyClans})";
            return true;
        }

        public static bool EndWar(string clanId, out string statusMessage)
        {
            EnsureLoaded();
            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                if (string.Equals(cachedEnemies[i].clanId, clanId, StringComparison.Ordinal))
                {
                    EnemyClanEntry removed = cachedEnemies[i];
                    cachedEnemies.RemoveAt(i);
                    SaveWars();
                    statusMessage = IsGerman
                        ? $"Krieg mit [{removed.tag}] {removed.name} beendet. ({cachedEnemies.Count}/{MaxEnemyClans})"
                        : $"Ended war with [{removed.tag}] {removed.name}. ({cachedEnemies.Count}/{MaxEnemyClans})";
                    return true;
                }
            }
            statusMessage = IsGerman
                ? "Kein aktiver Krieg mit diesem Clan."
                : "No active war with this clan.";
            return false;
        }

        // --- Online Match Slot Clan Synchronization & Skull Target Resolution ---

        public static void ResetMatchSlots()
        {
            Array.Clear(slotClanIds, 0, slotClanIds.Length);
            Array.Clear(slotClanTags, 0, slotClanTags.Length);
        }

        public static void SetSlotClan(int slot, string clanId, string clanTag)
        {
            if (slot < 0 || slot >= MatchSession.MaxPlayers) return;
            slotClanIds[slot] = clanId ?? string.Empty;
            slotClanTags[slot] = clanTag ?? string.Empty;
        }

        public static string GetSlotClanId(int slot) =>
            slot >= 0 && slot < slotClanIds.Length ? slotClanIds[slot] ?? string.Empty : string.Empty;

        public static string GetSlotClanTag(int slot) =>
            slot >= 0 && slot < slotClanTags.Length ? slotClanTags[slot] ?? string.Empty : string.Empty;

        /// <summary>
        /// Returns true if the given hero belongs to an enemy clan in an online match.
        /// </summary>
        public static bool IsEnemyClanHero(GameObject heroActor, int localPlayerTeam, out string enemyTag)
        {
            enemyTag = string.Empty;
            if (heroActor == null || !CoopAuthority.Active) return false;
            TeamMember tm = heroActor.GetComponent<TeamMember>();
            if (tm == null || tm.Team == localPlayerTeam) return false;

            EnsureLoaded();
            if (cachedEnemies.Count == 0) return false;

            CoopHeroSlot slot = heroActor.GetComponent<CoopHeroSlot>();
            if (slot != null && slot.Slot >= 0 && slot.Slot < MatchSession.MaxPlayers)
            {
                string syncedId = slotClanIds[slot.Slot];
                string syncedTag = slotClanTags[slot.Slot];
                if (!string.IsNullOrEmpty(syncedId))
                {
                    for (int i = 0; i < cachedEnemies.Count; i++)
                    {
                        if (string.Equals(cachedEnemies[i].clanId, syncedId, StringComparison.Ordinal))
                        {
                            enemyTag = !string.IsNullOrEmpty(syncedTag) ? syncedTag : cachedEnemies[i].tag;
                            return true;
                        }
                    }
                    return false;
                }
                if (!string.IsNullOrEmpty(syncedTag))
                {
                    for (int i = 0; i < cachedEnemies.Count; i++)
                    {
                        if (string.Equals(cachedEnemies[i].tag, syncedTag, StringComparison.OrdinalIgnoreCase))
                        {
                            enemyTag = cachedEnemies[i].tag;
                            return true;
                        }
                    }
                    return false;
                }

                // When an online match slot is backfilled or has not yet reported a non-enemy clan,
                // map enemy slots up to ActiveWarCount to the declared enemy clans so declared wars
                // are always represented in online matches.
                if (!slot.IsHuman)
                {
                    int enemyOrdinal = slot.Slot % 5;
                    if (enemyOrdinal < cachedEnemies.Count)
                    {
                        enemyTag = cachedEnemies[enemyOrdinal].tag;
                        return true;
                    }
                }
            }
            return false;
        }

        // --- Balanced Clan XP Shard Donation System ---

        private static int CurrentUtcDay() => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400L);

        public static int DonationsUsedToday()
        {
            int today = CurrentUtcDay();
            int savedDay = AccountProfilePrefs.GetInt(DailyDonationDayKey, -1);
            if (savedDay != today) return 0;
            return Mathf.Clamp(AccountProfilePrefs.GetInt(DailyDonationCountKey, 0), 0, MaxDailyDonations);
        }

        public static int RemainingDonationsToday() => Mathf.Max(0, MaxDailyDonations - DonationsUsedToday());

        public static int GetDonatedClanXp(string clanId)
        {
            if (string.IsNullOrEmpty(clanId)) return 0;
            return Mathf.Max(0, AccountProfilePrefs.GetInt(ClanXpKeyPrefix + clanId, 0));
        }

        /// <summary>
        /// Escalating XP curve for bonus clan levels from Shard donations:
        /// Level 1 = 250 XP (500 Shards), Level 2 = 600 XP (1,200 Shards), Level 3 = 1,050 XP (2,100 Shards)...
        /// Prevents Shard donations from overpowering member progression.
        /// </summary>
        public static int XpForBonusLevel(int bonusLevel)
        {
            bonusLevel = Mathf.Clamp(bonusLevel, 0, MaxClanBonusLevel);
            if (bonusLevel <= 0) return 0;
            return 250 * bonusLevel + 100 * bonusLevel * (bonusLevel - 1);
        }

        public static int GetClanBonusLevel(string clanId)
        {
            int xp = GetDonatedClanXp(clanId);
            int level = 0;
            while (level < MaxClanBonusLevel && xp >= XpForBonusLevel(level + 1))
                level++;
            return level;
        }

        public static void GetClanXpProgress(string clanId, out int totalXp, out int bonusLevel, out int intoLevel, out int neededForNext)
        {
            totalXp = GetDonatedClanXp(clanId);
            bonusLevel = GetClanBonusLevel(clanId);
            int floor = XpForBonusLevel(bonusLevel);
            int ceiling = bonusLevel >= MaxClanBonusLevel ? floor : XpForBonusLevel(bonusLevel + 1);
            intoLevel = Mathf.Max(0, totalXp - floor);
            neededForNext = Mathf.Max(1, ceiling - floor);
        }

        public static bool TryDonateShardsForClanXp(string clanId, out string statusMessage)
        {
            if (string.IsNullOrEmpty(clanId))
            {
                statusMessage = IsGerman
                    ? "Kein aktiver Clan ausgewählt."
                    : "No active clan selected.";
                return false;
            }
            int bonusLevel = GetClanBonusLevel(clanId);
            if (bonusLevel >= MaxClanBonusLevel)
            {
                statusMessage = IsGerman
                    ? "Maximales Clan-XP-Bonuslevel erreicht!"
                    : "Maximum Clan XP bonus level reached!";
                return false;
            }
            if (RemainingDonationsToday() <= 0)
            {
                statusMessage = IsGerman
                    ? $"Tägliches Limit erreicht ({MaxDailyDonations}/{MaxDailyDonations} Spenden heute)."
                    : $"Daily donation limit reached ({MaxDailyDonations}/{MaxDailyDonations} today).";
                return false;
            }
            if (!PlayerWallet.CanAfford(DonationShardCost))
            {
                statusMessage = IsGerman
                    ? $"Nicht genug Shards ({DonationShardCost} Shards benötigt)."
                    : $"Not enough Shards ({DonationShardCost} Shards required).";
                return false;
            }
            if (!PlayerWallet.TrySpend(DonationShardCost))
            {
                statusMessage = IsGerman
                    ? $"Verwende erspielte Shards für Clan-XP ({DonationShardCost} Shards)."
                    : $"Use earned Shards for Clan XP donations ({DonationShardCost} Shards).";
                return false;
            }

            int today = CurrentUtcDay();
            int used = DonationsUsedToday() + 1;
            AccountProfilePrefs.SetInt(DailyDonationDayKey, today);
            AccountProfilePrefs.SetInt(DailyDonationCountKey, used);

            int nextClanXp = GetDonatedClanXp(clanId) + DonationClanXpGain;
            AccountProfilePrefs.SetInt(ClanXpKeyPrefix + clanId, nextClanXp);

            // Grant a small +10 Account XP contribution so saved server profile level also progresses gradually.
            long beforeAccountXp = PlayerExperience.TotalXp;
            long maxAccountXp = PlayerExperience.XpForLevel(PlayerExperience.MaxLevel);
            if (beforeAccountXp < maxAccountXp)
            {
                long nextAccountXp = Math.Min(maxAccountXp, beforeAccountXp + DonationAccountXpBonus);
                AccountProfilePrefs.SetString("DF_AccountXP", nextAccountXp.ToString());
            }
            AccountProfilePrefs.Save();

            int newBonusLevel = GetClanBonusLevel(clanId);
            statusMessage = IsGerman
                ? $"+{DonationClanXpGain} CLAN-XP gespendet! (Bonus-Lvl +{newBonusLevel}  •  Heute {used}/{MaxDailyDonations})"
                : $"Donated +{DonationClanXpGain} CLAN XP! (Bonus Lvl +{newBonusLevel}  •  Today {used}/{MaxDailyDonations})";
            return true;
        }
    }
}
