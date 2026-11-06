using System;
using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Profile;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    internal sealed class MatchResultScreenData
    {
        public readonly struct DropReward
        {
            public readonly BattleItemType Item;
            public readonly int Count;
            public readonly string Label, Name;
            public DropReward(BattleItemType item, int count)
            {
                Item = item;
                Count = Mathf.Max(1, count);
                Label = "MATCH DROP  /  ×" + Count;
                Name = BattleItemInventory.Name(item);
            }
        }

        public readonly struct Metric
        {
            public readonly string Label, Value;
            public Metric(string label, string value)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
            }
        }

        public struct RosterRow
        {
            public int Team;
            public string Name;
            public string PlayerId;
            public string Hero;
            public int Kills;
            public int Deaths;
            public int Assists;
            public int Score;
            public bool IsLocal;
            public bool IsMvp;
        }

        public bool Won, Ready, Retrying, IsLocalMvp;
        public string ExitLabel = "BACK TO MENU", Difficulty = "MEDIUM", Hero = "PLAYER", DropButton = "PROGRESS";
        public DropReward[] Drops;
        public int Kills, Deaths, Assists, Objectives, Xp, Shards, BaseXp, XpBonus, BaseShards, ShardBonus;
        public float BonusPercent;
        public string BonusName = "";
        public int AccountLevel = 1, PassLevel = 1, PassXpGained;
        public bool AccountMax, PassMax, PassLevelUp;
        public long AccountInto, AccountRequired;
        public int PassInto, PassRequired;
        public float AccountProgress, PassProgress;
        public BattleItemType Drop;
        public Metric[] Details = Array.Empty<Metric>();
        public RosterRow[] Roster = Array.Empty<RosterRow>();

        public string Result, KillsText, DeathsText, AssistsText, ObjectivesText, XpText, ShardsText,
            XpBreakdown, ShardBreakdown, BonusBadge, DropName, AccountTitle, AccountText, PassTitle, PassText, PassGain, Status,
            CombatGrade, KdaRatioText;

        public void Prepare()
        {
            Result = Won ? "VICTORY" : "DEFEAT";
            KillsText = Number(Kills);
            DeathsText = Number(Deaths);
            AssistsText = Number(Assists);
            ObjectivesText = "OBJECTIVES  " + Number(Objectives);
            XpText = Ready ? "+" + Number(Xp) : "—";
            ShardsText = Ready ? "+" + Number(Shards) : "—";
            XpBreakdown = Number(BaseXp) + " BASE  +" + Number(XpBonus) + " BONUS";
            ShardBreakdown = Number(BaseShards) + " BASE  +" + Number(ShardBonus) + " BONUS";
            BonusBadge = !Ready ? "FINALIZING…" : BonusPercent > 0 ? "+" + Alterverse.Match.WingMatchRewardBonus.PercentLabel(BonusPercent) + "% WING BONUS" : (XpBonus > 0 || ShardBonus > 0) ? "BONUS REWARD" : "MATCH REWARD";
            if (Drops == null) Drops = Drop == BattleItemType.None ? Array.Empty<DropReward>() : new[] { new DropReward(Drop, 1) };
            DropButton = Drops.Length == 1 ? "DROP ×1" : Drops.Length > 1 ? "DROPS ×" + Drops.Length : "PROGRESS";
            DropName = Drop == BattleItemType.None ? "NO BATTLE ITEM DROP" : BattleItemInventory.Name(Drop);
            AccountTitle = "ACCOUNT  /  LEVEL " + AccountLevel;
            AccountText = AccountMax ? "MAX LEVEL" : AccountInto.ToString("N0") + " / " + AccountRequired.ToString("N0") + " XP";
            PassTitle = "BATTLE PASS  /  LEVEL " + PassLevel;
            PassText = PassMax ? "MAX LEVEL" : PassInto.ToString("N0") + " / " + PassRequired.ToString("N0") + " XP";
            PassGain = "+" + PassXpGained.ToString("N0") + " XP" + (PassLevelUp ? "  •  LEVEL UP" : "");
            Status = Ready ? "REWARDS RECEIVED" : Retrying ? "SAVE PENDING  •  RETRYING…" : "FINALIZING REWARDS…";

            float kda = (Kills + Assists * 0.75f) / Mathf.Max(1f, Deaths);
            KdaRatioText = kda.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " KDA";
            float rating = Kills * 3.2f + Assists * 2.1f + Objectives * 3.5f - Deaths * 1.6f + (Won ? 4f : 0f);
            CombatGrade = rating >= 24f ? "S+" : rating >= 17f ? "S" : rating >= 11f ? "A" : rating >= 6f ? "B" : "C";
        }

        // Preserve exact values in Details; abbreviate only extreme display values, never rewards.
        public static string Number(int value)
        {
            if (value >= 1000000000) return (value / 1000000000d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "B";
            if (value >= 1000000) return (value / 1000000d).ToString(value >= 100000000 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
            if (value >= 1000) return (value / 1000d).ToString(value >= 10000 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "K";
            return value.ToString("N0");
        }
    }

    internal readonly struct MatchResultLayout
    {
        public readonly Rect Header, Kda, Xp, Shards, Lower, Footer, Page;
        public readonly bool Stacked, ShowBreakdown;
        public static float Scale(Rect safe) => Mathf.Max(1f, Mathf.Min(safe.width / 760f, safe.height / 480f));

        public MatchResultLayout(float width, float height)
        {
            float margin = height < 320 ? 8 : height < 420 ? 10 : 18, gap = height < 320 ? 4 : 8;
            float content = Mathf.Min(1080, width - margin * 2), left = (width - content) * .5f;
            Stacked = width < 600 && height >= 500;
            ShowBreakdown = height >= 460;
            float header = Stacked ? 82 : height < 320 ? 48 : 62;
            float stats = height < 420 ? 66 : 82;
            float reward = height < 270 ? 58 : ShowBreakdown ? 116 : height < 320 ? 86 : 92;
            Header = new Rect(left, margin, content, header);
            Kda = new Rect(left, Header.yMax + gap, content, stats);
            float rewardY = Kda.yMax + gap;
            float cardWidth = Stacked ? content : (content - gap) * .5f;
            Xp = new Rect(left, rewardY, cardWidth, reward);
            Shards = Stacked ? new Rect(left, Xp.yMax + gap, cardWidth, reward) : new Rect(Xp.xMax + gap, rewardY, cardWidth, reward);
            Footer = new Rect(left, height - margin - 52, content, 52);
            Lower = new Rect(left, Shards.yMax + gap, content, Mathf.Max(0, Footer.y - gap - Shards.yMax - gap));
            Page = new Rect(left, Header.yMax + gap, content, Mathf.Max(0, Footer.y - gap - Header.yMax - gap));
        }
    }

    internal sealed class MatchResultScreen
    {
        private enum Page { Summary, Progress, Details }
        private Page page;
        private MatchResultScreenData data;
        private FrontEndTheme theme;
        private GUIStyle title, number, label, small, button, badgeStyle, gradeStyle;
        private float shownAt;
        private Vector2 summaryScroll, progressScroll, detailScroll, pointerStart, scrollStart;
        private bool pointerDown, dragging;

        public void SetData(MatchResultScreenData value)
        {
            if (data == null) shownAt = Time.unscaledTime;
            data = value;
            data.Prepare();
        }

        public bool Draw(float width, float height)
        {
            if (data == null) return false;
            EnsureStyles();
            var layout = new MatchResultLayout(width, height);

            float elapsed = Mathf.Max(0f, Time.unscaledTime - shownAt);
            float reveal = Mathf.Clamp01(elapsed / 0.28f);
            float countUp = Mathf.Clamp01(elapsed / 0.65f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 3.2f);

            // Deep atmospheric backdrop with victory/defeat energy wash
            FrontEndTheme.Fill(new Rect(0, 0, width, height), FrontEndTheme.Background);
            Color heroBannerTint = data.Won
                ? new Color(0.10f, 0.36f, 0.44f, 0.22f + 0.06f * pulse)
                : new Color(0.34f, 0.10f, 0.38f, 0.22f + 0.06f * pulse);
            FrontEndTheme.Fill(new Rect(0, 0, width, Mathf.Min(height * 0.36f, 180f)), heroBannerTint);
            FrontEndTheme.Fill(new Rect(0, Mathf.Min(height * 0.36f, 180f), width, 2f),
                data.Won ? new Color(0.24f, 0.90f, 0.96f, 0.32f) : new Color(0.76f, 0.30f, 0.96f, 0.32f));

            Color original = GUI.color;
            GUI.color = original * new Color(1, 1, 1, Mathf.Lerp(.84f, 1f, reveal));
            Color accent = data.Won ? FrontEndTheme.Accent : new Color(.72f, .44f, .96f);

            Header(layout, accent, pulse);
            switch (page)
            {
                case Page.Progress:
                    Progress(layout.Page, ref progressScroll, countUp, pulse);
                    break;
                case Page.Details:
                    Details(layout.Page, accent);
                    break;
                default:
                    Kda(layout.Kda, accent, countUp);
                    Reward(layout.Xp, "ACCOUNT XP", data.XpText, data.XpBreakdown, data.BaseXp, data.XpBonus, layout.ShowBreakdown, accent, countUp);
                    Reward(layout.Shards, "SHARDS", data.ShardsText, data.ShardBreakdown, data.BaseShards, data.ShardBonus, layout.ShowBreakdown, new Color(0.26f, 0.88f, 0.98f), countUp);
                    if (layout.Lower.height >= 40) Progress(layout.Lower, ref summaryScroll, countUp, pulse);
                    break;
            }
            GUI.color = original;

            float smallWidth = (layout.Footer.width - 24) * .25f;
            string progressLabel = page == Page.Progress ? "SUMMARY" : data.Ready ? data.DropButton : "PROGRESS";
            string detailsBtnLabel = page == Page.Details ? "SUMMARY" : (data.Roster != null && data.Roster.Length > 0 ? "SCOREBOARD" : "DETAILS");
            if (Action(new Rect(layout.Footer.x, layout.Footer.y, smallWidth, 52), progressLabel, data.Ready && data.Drops.Length > 0 && page != Page.Progress, true))
            { page = page == Page.Progress ? Page.Summary : Page.Progress; pointerDown = dragging = false; }
            if (Action(new Rect(layout.Footer.x + smallWidth + 8, layout.Footer.y, smallWidth, 52), detailsBtnLabel, false, true))
            { page = page == Page.Details ? Page.Summary : Page.Details; pointerDown = dragging = false; }

            // Animation and alternate pages cannot bypass the receipt-completion gate.
            return Action(new Rect(layout.Footer.x + smallWidth * 2 + 16, layout.Footer.y, layout.Footer.width - smallWidth * 2 - 16, 52),
                data.Ready ? data.ExitLabel : "FINALIZING…", true, data.Ready);
        }

        private void EnsureStyles()
        {
            if (theme != null) return;
            theme = new FrontEndTheme();
            title = Style(40, true);
            title.font = FrontEndTypography.Brand;
            number = Style(32, true);
            label = Style(18, true);
            small = Style(17, false);
            button = Style(18, true);
            button.alignment = TextAnchor.MiddleCenter;
            badgeStyle = Style(14, true);
            badgeStyle.alignment = TextAnchor.MiddleCenter;
            gradeStyle = Style(26, true);
            gradeStyle.alignment = TextAnchor.MiddleCenter;
        }

        private static GUIStyle Style(int size, bool bold) => new(GUI.skin.label)
        {
            font = bold ? FrontEndTypography.Display : FrontEndTypography.Body,
            fontSize = size,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            wordWrap = false,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(0, 0, 0, 0),
            richText = false,
            normal = { textColor = FrontEndTheme.Text }
        };

        private void Header(MatchResultLayout layout, Color accent, float pulse)
        {
            Rect r = layout.Header;
            string ranked = Alterverse.Multiplayer.CoopAuthority.ActivePlan?.Session?.Mode == Alterverse.Multiplayer.SessionMode.OnlineRanked
                ? Alterverse.Multiplayer.RankedService.Instance?.ResultText : null;
            theme.Panel(r);

            // Top glowing accent strip
            FrontEndTheme.Fill(new Rect(r.x, r.y, r.width, 3f),
                Color.Lerp(accent, Color.white, 0.25f + 0.15f * pulse));

            float titleWidth = layout.Stacked ? r.width - 130f : r.width * 0.44f;
            title.normal.textColor = data.Won ? new Color(1f, 0.90f, 0.52f) : new Color(0.92f, 0.78f, 1f);
            theme.Label(new Rect(r.x + 18, r.y + 4, titleWidth, 46), data.Result, title);

            // Combat Grade Pill & MVP Badge
            float gradeW = 54f;
            float gradeH = Mathf.Min(40f, r.height - 14f);
            Rect gradeRect = new Rect(r.xMax - gradeW - 14f, r.y + (r.height - gradeH) * 0.5f, gradeW, gradeH);
            Color gradeColor = data.CombatGrade.StartsWith("S") ? new Color(1f, 0.78f, 0.22f)
                : data.CombatGrade == "A" ? new Color(0.24f, 0.90f, 0.96f)
                : new Color(0.68f, 0.78f, 0.94f);
            FrontEndTheme.Fill(gradeRect, new Color(gradeColor.r * 0.22f, gradeColor.g * 0.22f, gradeColor.b * 0.22f, 0.92f));
            FrontEndTheme.Fill(new Rect(gradeRect.x, gradeRect.y, gradeRect.width, 2f), gradeColor);
            gradeStyle.normal.textColor = gradeColor;
            GUI.Label(gradeRect, data.CombatGrade ?? "A", gradeStyle);

            if (data.IsLocalMvp && !layout.Stacked)
            {
                Rect mvpRect = new Rect(gradeRect.x - 118f, gradeRect.y + 6f, 110f, 26f);
                FrontEndTheme.Fill(mvpRect, new Color(0.98f, 0.72f, 0.14f, 0.24f + 0.08f * pulse));
                FrontEndTheme.Fill(new Rect(mvpRect.x, mvpRect.y, mvpRect.width, 2f), new Color(1f, 0.82f, 0.28f));
                badgeStyle.normal.textColor = new Color(1f, 0.88f, 0.42f);
                GUI.Label(mvpRect, GameLanguage.Text("★ MATCH MVP"), badgeStyle);
            }

            if (layout.Stacked)
            {
                theme.Label(new Rect(r.x + 18, r.y + 48, r.width - 90, 24), string.IsNullOrEmpty(ranked) ? data.ObjectivesText : ranked, small, true);
            }
            else
            {
                float rightReserved = data.IsLocalMvp ? 200f : 82f;
                float x = r.x + r.width * .44f, w = Mathf.Max(120f, r.width * .56f - rightReserved);
                theme.FitLabel(new Rect(x, r.y + 6, w, 24), (data.Hero ?? "MATCH COMPLETE") + "  •  " + (data.KdaRatioText ?? ""), label);
                theme.FitLabel(new Rect(x, r.y + 31, w, 22), string.IsNullOrEmpty(ranked) ? data.ObjectivesText : ranked, small, true);
            }
            FrontEndTheme.Fill(new Rect(r.x + 16, r.yMax - 2, r.width - 32, 2), accent);
        }

        private void Kda(Rect r, Color accent, float countUp)
        {
            float w = (r.width - 16) / 3;
            int animK = countUp >= 0.99f ? data.Kills : Mathf.RoundToInt(data.Kills * countUp);
            int animD = countUp >= 0.99f ? data.Deaths : Mathf.RoundToInt(data.Deaths * countUp);
            int animA = countUp >= 0.99f ? data.Assists : Mathf.RoundToInt(data.Assists * countUp);

            string kText = countUp >= 0.99f ? data.KillsText : MatchResultScreenData.Number(animK);
            string dText = countUp >= 0.99f ? data.DeathsText : MatchResultScreenData.Number(animD);
            string aText = countUp >= 0.99f ? data.AssistsText : MatchResultScreenData.Number(animA);

            float maxStat = Mathf.Max(1f, Mathf.Max(data.Kills, Mathf.Max(data.Deaths, data.Assists)));
            Stat(new Rect(r.x, r.y, w, r.height), "KILLS", kText, new Color(0.24f, 0.92f, 0.72f), Mathf.Clamp01(data.Kills / maxStat) * countUp);
            Stat(new Rect(r.x + w + 8, r.y, w, r.height), "DEATHS", dText, new Color(0.96f, 0.42f, 0.52f), Mathf.Clamp01(data.Deaths / maxStat) * countUp);
            Stat(new Rect(r.x + (w + 8) * 2, r.y, w, r.height), "ASSISTS", aText, accent, Mathf.Clamp01(data.Assists / maxStat) * countUp);
        }

        private void Stat(Rect r, string name, string value, Color accent, float meter)
        {
            theme.Panel(r);
            FrontEndTheme.Fill(new Rect(r.x, r.y, 3, r.height), accent);
            FrontEndTheme.Fill(new Rect(r.x + 3, r.y, r.width - 3, 22), new Color(accent.r * 0.14f, accent.g * 0.14f, accent.b * 0.14f, 0.35f));
            theme.Label(new Rect(r.x + 14, r.y + 3, r.width - 24, 20), name, label, true);
            theme.Label(new Rect(r.x + 14, r.y + 24, r.width - 24, r.height - 32), value, number);

            // Subtle bottom contribution meter bar
            Rect barBg = new Rect(r.x + 14, r.yMax - 7, r.width - 28, 3);
            FrontEndTheme.Fill(barBg, new Color(1f, 1f, 1f, 0.08f));
            if (meter > 0.01f)
                FrontEndTheme.Fill(new Rect(barBg.x, barBg.y, barBg.width * Mathf.Clamp01(meter), barBg.height), accent);
        }

        private void Reward(Rect r, string name, string value, string breakdown, int baseVal, int bonusVal, bool showBreakdown, Color accent, float countUp)
        {
            theme.Panel(r);
            FrontEndTheme.Fill(new Rect(r.x, r.y, r.width, 3), accent);
            FrontEndTheme.Fill(new Rect(r.x, r.y + 3, r.width, 24), new Color(accent.r * 0.15f, accent.g * 0.15f, accent.b * 0.15f, 0.28f));
            if (r.height < 76)
            {
                theme.Label(new Rect(r.x + 10, r.y + 14, 68, 24), name == "ACCOUNT XP" ? "XP" : name, label, true);
                theme.Label(new Rect(r.x + 82, r.y + 7, r.width - 92, 40), value, number);
                return;
            }
            theme.Label(new Rect(r.x + 14, r.y + 4, r.width - 28, 21), name, label, true);
            theme.Label(new Rect(r.x + 14, r.y + 25, r.width - 28, 36), value, number);
            theme.Label(new Rect(r.x + 14, r.y + 60, r.width - 28, 22), data.BonusBadge, small, true);
            if (showBreakdown && data.Ready)
            {
                theme.Label(new Rect(r.x + 14, r.y + 82, r.width - 28, 20), breakdown, small, true);
                int total = Mathf.Max(1, baseVal + bonusVal);
                float baseFrac = Mathf.Clamp01(baseVal / (float)total) * countUp;
                float bonusFrac = Mathf.Clamp01(bonusVal / (float)total) * countUp;
                Rect bar = new Rect(r.x + 14, r.yMax - 8, r.width - 28, 4);
                FrontEndTheme.Fill(bar, new Color(1f, 1f, 1f, 0.08f));
                FrontEndTheme.Fill(new Rect(bar.x, bar.y, bar.width * baseFrac, bar.height), accent);
                if (bonusVal > 0)
                    FrontEndTheme.Fill(new Rect(bar.x + bar.width * baseFrac, bar.y, bar.width * bonusFrac, bar.height), new Color(1f, 0.78f, 0.24f));
            }
        }

        private void Progress(Rect viewport, ref Vector2 scroll, float countUp, float pulse)
        {
            bool compact = page == Page.Summary && viewport.height < 76 && data.Drops.Length == 1;
            int columns = viewport.width >= 700 ? 2 : 1;
            if (data.Drops.Length == 1) columns = 1;
            float cardHeight = compact ? 52 : 76;
            int rows = Mathf.CeilToInt(data.Drops.Length / (float)columns);
            float dropHeight = data.Ready ? (data.Drops.Length == 0 ? 30 : rows * (cardHeight + 8) - 8) : 48;
            float content = data.Ready ? dropHeight + 216 : 48;
            BeginScroll(viewport, ref scroll, content);
            float w = viewport.width - 20;
            if (!data.Ready) theme.Label(new Rect(10, 4, w - 20, 38), data.Status, label, true);
            else if (data.Drops.Length == 0) theme.Label(new Rect(10, 0, w - 20, 28), data.DropName, small, true);
            else
            {
                float cardWidth = (w - (columns - 1) * 8) / columns;
                for (int i = 0; i < data.Drops.Length; i++)
                {
                    var reward = data.Drops[i];
                    float x = (i % columns) * (cardWidth + 8), y = (i / columns) * (cardHeight + 8);
                    Rect drop = new(x, y, cardWidth, cardHeight);
                    theme.Panel(drop);
                    FrontEndTheme.Fill(new Rect(drop.x, drop.y, 3f, drop.height), new Color(1f, 0.76f, 0.24f));
                    BattleItemIconRenderer.Draw(new Rect(x + 12, y + (compact ? 5 : 9), compact ? 42 : 56, compact ? 42 : 56), reward.Item, true);
                    float textX = x + (compact ? 68 : 82), textWidth = cardWidth - (compact ? 80 : 94);
                    theme.Label(new Rect(textX, y + (compact ? 0 : 5), textWidth, 22), reward.Label, small, true);
                    theme.Label(new Rect(textX, y + (compact ? 24 : 27), textWidth, 26), reward.Name, label);
                    if (!compact) theme.Label(new Rect(textX, y + 52, textWidth, 20), "RECEIVED", small, true);
                }
            }
            if (data.Ready)
            {
                ProgressCard(new Rect(0, dropHeight + 8, w, 96), data.PassTitle, data.PassText, data.PassGain, data.PassProgress * countUp, data.PassLevelUp, pulse);
                ProgressCard(new Rect(0, dropHeight + 112, w, 96), data.AccountTitle, data.AccountText, "", data.AccountProgress * countUp, false, pulse);
            }
            GUI.EndScrollView();
        }

        private void ProgressCard(Rect r, string heading, string detail, string gain, float fraction, bool levelUp, float pulse)
        {
            theme.Panel(r);
            Color barAccent = levelUp ? new Color(1f, 0.80f, 0.24f) : new Color(0.24f, 0.88f, 0.98f);
            FrontEndTheme.Fill(new Rect(r.x, r.y, 3f, r.height), barAccent);
            theme.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 26), heading, label);
            theme.Label(new Rect(r.x + 14, r.y + 33, r.width - 28, 23), detail, small, true);
            theme.Progress(new Rect(r.x + 14, r.y + 60, r.width - 28, 9), fraction);
            if (!string.IsNullOrEmpty(gain))
                theme.Label(new Rect(r.x + 14, r.y + 71, r.width - 28, 23), gain, small, true);
            if (levelUp)
            {
                Rect badge = new Rect(r.xMax - 122f, r.y + 8f, 108f, 22f);
                FrontEndTheme.Fill(badge, new Color(1f, 0.78f, 0.18f, 0.24f + 0.10f * pulse));
                badgeStyle.normal.textColor = new Color(1f, 0.88f, 0.38f);
                GUI.Label(badge, GameLanguage.Text("LEVEL UP!"), badgeStyle);
            }
        }

        private void Details(Rect viewport, Color accent)
        {
            int rosterCount = data.Roster != null ? data.Roster.Length : 0;
            float rosterSectionHeight = rosterCount > 0 ? (42f + rosterCount * 42f + 18f) : 0f;
            float metricsHeight = data.Details.Length * 68f;
            BeginScroll(viewport, ref detailScroll, rosterSectionHeight + metricsHeight);

            float w = viewport.width - 20f;
            float curY = 0f;

            if (rosterCount > 0)
            {
                Rect headerRect = new Rect(0, curY, w, 34f);
                theme.Panel(headerRect);
                FrontEndTheme.Fill(new Rect(0, curY, w, 2f), accent);
                theme.Label(new Rect(14, curY + 5, w * 0.46f, 24), "TEAM ROSTER", label);
                theme.Label(new Rect(w - 210, curY + 5, 110, 24), "K / D / A", small, true);
                theme.Label(new Rect(w - 86, curY + 5, 74, 24), "SCORE", small, true);
                curY += 40f;

                for (int i = 0; i < rosterCount; i++)
                {
                    var row = data.Roster[i];
                    Rect rr = new Rect(0, curY, w, 38f);
                    theme.Panel(rr);
                    Color teamTint = row.Team == 0 ? FrontEndTheme.Zero : FrontEndTheme.Positive;
                    FrontEndTheme.Fill(new Rect(0, curY, 3f, 38f), teamTint);
                    if (row.IsLocal)
                        FrontEndTheme.Fill(new Rect(3f, curY, w - 3f, 38f), new Color(teamTint.r * 0.22f, teamTint.g * 0.22f, teamTint.b * 0.22f, 0.32f));

                    string playerLabel = (row.IsMvp ? "* " : "") + row.Name + "  (" + row.Hero + ")";
                    float socialReserve = row.IsLocal ? 0f : 104f;
                    theme.FitLabel(new Rect(14, curY + 6, Mathf.Max(80f, w - 236 - socialReserve), 26), playerLabel, row.IsLocal ? label : small);
                    if (!row.IsLocal && !string.IsNullOrWhiteSpace(row.Name))
                    {
                        bool connected = FriendStickerService.IsFriendOrRequested(row.Name);
                        if (theme.Action(new Rect(w - 314, curY + 5, 44, 28), connected ? "OK" : "+FR", !connected, enabled: !connected))
                        {
                            FriendStickerService.RequestFriendFromScoreboard(row.Name, Alterverse.Characters.HeroId.Veyr, Mathf.Max(1, PlayerExperience.Level));
                        }
                        var voice = PartyVoiceService.Instance;
                        PartyVoiceService.VoicePlayer participant = default;
                        bool available = voice != null && voice.TryGetVoicePlayer(row.PlayerId, out participant);
                        if (theme.Action(new Rect(w - 266, curY + 5, 50, 28), participant.Muted ? "MUTED" : "MUTE", false,
                            selected: available && participant.Muted, enabled: available && !participant.Pending))
                        {
                            voice.SetPlayerMuted(row.PlayerId, !participant.Muted);
                        }
                    }
                    theme.Label(new Rect(w - 210, curY + 6, 118, 26), $"{row.Kills} / {row.Deaths} / {row.Assists}", label);
                    theme.Label(new Rect(w - 86, curY + 6, 74, 26), row.Score.ToString("N0"), small, true);
                    curY += 42f;
                }
                curY += 12f;
            }

            // Two lines per value also accommodate long MVP names and exact large reward totals.
            for (int i = 0; i < data.Details.Length; i++)
            {
                float y = curY + i * 68f;
                if (y + 64 < detailScroll.y || y > detailScroll.y + viewport.height) continue;
                Rect r = new(0, y, w, 62);
                theme.Panel(r);
                FrontEndTheme.Fill(new Rect(0, y, 2f, 62f), accent);
                theme.Label(new Rect(14, y + 4, r.width - 28, 22), data.Details[i].Label, small, true);
                theme.Label(new Rect(14, y + 27, r.width - 28, 30), data.Details[i].Value, label);
            }
            GUI.EndScrollView();
        }

        private void BeginScroll(Rect viewport, ref Vector2 scroll, float content)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.MouseDown && e.button == 0 && viewport.Contains(e.mousePosition))
            { pointerDown = true; dragging = false; pointerStart = e.mousePosition; scrollStart = scroll; }
            if (e.rawType == EventType.MouseDrag && pointerDown && (dragging || Mathf.Abs(e.mousePosition.y - pointerStart.y) > 7))
            { dragging = true; GUIUtility.hotControl = id; scroll.y = scrollStart.y + pointerStart.y - e.mousePosition.y; e.Use(); }
            if (e.rawType == EventType.MouseUp && pointerDown)
            { pointerDown = false; if (dragging) { GUIUtility.hotControl = 0; e.Use(); dragging = false; } }
            scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, content - viewport.height));
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, viewport.width - 20, Mathf.Max(viewport.height, content)), false, false);
        }

        private bool Action(Rect r, string text, bool primary, bool enabled)
        {
            return theme.Action(r, text, primary: primary, enabled: enabled);
        }
    }
}
