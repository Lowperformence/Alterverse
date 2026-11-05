using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Renders floating damage/shield/heal numbers (when PlayerPreferences.DamageNumbers is enabled),
    /// a live top-right Kill-Feed, and Multi-Kill / Killstreak / Shutdown banners with comeback Front bonuses.
    /// </summary>
    public sealed class CombatFeedbackHUD : MonoBehaviour
    {
        private struct FloatingNumber
        {
            public Vector3 WorldPos;
            public string Text;
            public Color Color;
            public float SpawnTime;
            public float Duration;
            public int FontSize;
            public float DriftX;
        }

        private struct KillFeedRow
        {
            public string Killer;
            public string Victim;
            public int KillerTeam;
            public string Badge;
            public float Until;
        }

        private const int MaxFloating = 28;
        private const int MaxFeedRows = 3;
        private static CombatFeedbackHUD instance;

        private readonly List<FloatingNumber> numbers = new(MaxFloating);
        private readonly List<KillFeedRow> feed = new(MaxFeedRows);
        private readonly HashSet<Health> subscribed = new();
        private readonly Dictionary<Health, float> lastHealth = new();
        private readonly Dictionary<Health, int> streakByHero = new();
        private readonly Dictionary<Health, int> multiKillCount = new();
        private readonly Dictionary<Health, float> lastKillAt = new();

        private Camera cam;
        private DimensionsFront front;
        private FrontEndTheme theme;
        private Font readableFont;
        private GUIStyle numStyle, shadowStyle, bannerTitleStyle, bannerSubStyle, feedStyle;
        private float nextScan;

        private string activeBannerTitle;
        private string activeBannerSub;
        private Color activeBannerColor = Color.white;
        private float activeBannerUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            var go = new GameObject("DF_CombatFeedbackHUD");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatFeedbackHUD>();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
            if (instance == this) instance = null;
        }

        private void UnsubscribeAll()
        {
            foreach (Health h in subscribed)
            {
                if (h == null) continue;
                h.Damaged -= OnAnyDamaged;
                h.DiedWithSource -= OnAnyDiedWithSource;
            }
            subscribed.Clear();
            lastHealth.Clear();
        }

        private void Update()
        {
            if (MainMenuController.IsVisible || Time.timeScale <= 0f)
            {
                if (MainMenuController.IsVisible && numbers.Count > 0)
                {
                    numbers.Clear();
                    feed.Clear();
                    streakByHero.Clear();
                    multiKillCount.Clear();
                    lastKillAt.Clear();
                    activeBannerUntil = 0f;
                }
                return;
            }

            if (cam == null) cam = Camera.main;
            if (front == null) front = FindAnyObjectByType<DimensionsFront>();

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.4f;
                RefreshSubscriptions();
            }

            // Expire old floating numbers and feed rows
            float now = Time.unscaledTime;
            for (int i = numbers.Count - 1; i >= 0; i--)
                if (now - numbers[i].SpawnTime >= numbers[i].Duration)
                    numbers.RemoveAt(i);

            for (int i = feed.Count - 1; i >= 0; i--)
                if (now >= feed[i].Until)
                    feed.RemoveAt(i);
        }

        private void RefreshSubscriptions()
        {
            var active = Health.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Health h = active[i];
                if (h == null) continue;
                if (subscribed.Add(h))
                {
                    h.Damaged += (applied, absorbed, source) => OnHealthDamaged(h, applied, absorbed, source);
                    h.Changed += (cur, max) => OnHealthChanged(h, cur);
                    h.DiedWithSource += OnAnyDiedWithSource;
                    lastHealth[h] = h.Current;
                }
            }
        }

        private void OnAnyDamaged(float applied, float absorbed, GameObject source) { }

        private void OnHealthChanged(Health target, float current)
        {
            if (target == null) return;
            if (!lastHealth.TryGetValue(target, out float prev))
            {
                lastHealth[target] = current;
                return;
            }
            float delta = current - prev;
            lastHealth[target] = current;

            if (delta >= 8f && !target.IsDead && PlayerPreferences.DamageNumbers)
            {
                GameObject local = LocalHeroContext.Find();
                if (local != null && (target.gameObject == local || Vector3.Distance(local.transform.position, target.transform.position) <= 18f))
                {
                    if (!CombatVisibility.HiddenFrom(local, target))
                    {
                        SpawnFloating(target.transform.position + Vector3.up * 1.55f,
                            "+" + Mathf.RoundToInt(delta),
                            new Color(0.26f, 0.96f, 0.58f, 1f),
                            delta >= 45f ? 17 : 14,
                            0.85f);
                    }
                }
            }
        }

        private void OnHealthDamaged(Health victim, float applied, float absorbed, GameObject source)
        {
            if (victim == null) return;
            lastHealth[victim] = victim.Current;
            if (!PlayerPreferences.DamageNumbers) return;

            float total = applied + absorbed;
            if (total < 1f) return;

            GameObject local = LocalHeroContext.Find();
            if (local == null) return;

            bool localInvolved = victim.gameObject == local ||
                (source != null && (source == local || source.transform.IsChildOf(local.transform)));
            if (!localInvolved && Vector3.Distance(local.transform.position, victim.transform.position) > 16f)
                return;
            if (CombatVisibility.HiddenFrom(local, victim))
                return;

            bool isHeroVictim = victim.GetComponent<HeroIdentity>() != null;
            bool bigHit = total >= 42f || (isHeroVictim && total >= 28f);
            Color c;
            if (victim.gameObject == local)
                c = new Color(1f, 0.34f, 0.36f, 1f);
            else if (applied <= 0.5f && absorbed > 0f)
                c = new Color(0.38f, 0.78f, 1f, 1f);
            else if (bigHit)
                c = new Color(1f, 0.80f, 0.22f, 1f);
            else
                c = new Color(0.96f, 0.98f, 1f, 1f);

            string text = (bigHit && victim.gameObject != local ? "✦ " : "") + Mathf.Max(1, Mathf.RoundToInt(total));
            SpawnFloating(
                victim.transform.position + Vector3.up * (isHeroVictim ? 1.75f : 1.25f),
                text,
                c,
                bigHit ? 19 : 15,
                bigHit ? 0.95f : 0.75f);
        }

        private void SpawnFloating(Vector3 worldPos, string text, Color color, int size, float duration)
        {
            if (numbers.Count >= MaxFloating)
                numbers.RemoveAt(0);
            float drift = ((numbers.Count % 5) - 2) * 0.22f;
            numbers.Add(new FloatingNumber
            {
                WorldPos = worldPos + new Vector3(drift, 0f, 0f),
                Text = text,
                Color = color,
                SpawnTime = Time.unscaledTime,
                Duration = duration,
                FontSize = size,
                DriftX = drift * 18f
            });
        }

        private void OnAnyDiedWithSource(Health victim, GameObject source)
        {
            if (victim == null || victim.GetComponent<HeroIdentity>() == null) return;

            int victimPrevStreak = streakByHero.TryGetValue(victim, out int vs) ? vs : 0;
            streakByHero[victim] = 0;

            Health killerHealth = source != null ? source.GetComponentInParent<Health>() : null;
            HeroIdentity killerHero = killerHealth != null ? killerHealth.GetComponent<HeroIdentity>() : null;
            HeroIdentity victimHero = victim.GetComponent<HeroIdentity>();
            TeamMember killerTeam = killerHealth != null ? killerHealth.GetComponent<TeamMember>() : null;
            TeamMember victimTeam = victim.GetComponent<TeamMember>();

            if (killerHero == null || killerTeam == null || victimTeam == null || killerTeam.Team == victimTeam.Team)
                return;

            int newStreak = (streakByHero.TryGetValue(killerHealth, out int ks) ? ks : 0) + 1;
            streakByHero[killerHealth] = newStreak;

            float now = Time.unscaledTime;
            int multi = (lastKillAt.TryGetValue(killerHealth, out float lastT) && now - lastT <= 9.5f &&
                         multiKillCount.TryGetValue(killerHealth, out int prevM)) ? prevM + 1 : 1;
            multiKillCount[killerHealth] = multi;
            lastKillAt[killerHealth] = now;

            string killerName = MatchScoreboard.ResolveDisplayName(killerHero);
            string victimName = MatchScoreboard.ResolveDisplayName(victimHero);

            bool wasShutdown = victimPrevStreak >= 3;
            bool isComebackKill = false;
            if (front != null && !front.HasEnded && CoopAuthority.CanSimulateHostGameplay)
            {
                float killerControl = killerTeam.Team == 0 ? front.ZeroControl : front.LumenControl;
                if (wasShutdown)
                    front.Push(killerTeam.Team, 1.5f);
                if (killerControl < 25f)
                {
                    front.Push(killerTeam.Team, 0.75f);
                    isComebackKill = true;
                }
            }

            string badge = wasShutdown ? GameLanguage.Text("SHUTDOWN!")
                : multi >= 4 ? GameLanguage.Text("QUADRA FRONT WIPE!")
                : multi == 3 ? GameLanguage.Text("TRIPLE KILL!")
                : multi == 2 ? GameLanguage.Text("DOUBLE KILL!")
                : newStreak >= 5 ? GameLanguage.Text("UNSTOPPABLE!")
                : newStreak >= 3 ? GameLanguage.Text("DOMINATING!")
                : isComebackKill ? GameLanguage.Text("LAST STAND!") : string.Empty;

            if (feed.Count >= MaxFeedRows)
                feed.RemoveAt(0);
            feed.Add(new KillFeedRow
            {
                Killer = killerName,
                Victim = victimName,
                KillerTeam = killerTeam.Team,
                Badge = badge,
                Until = now + 4.5f
            });

            GameObject local = LocalHeroContext.Find();
            bool localKiller = local != null && killerHealth.gameObject == local;
            if (localKiller || !string.IsNullOrEmpty(badge))
            {
                activeBannerTitle = !string.IsNullOrEmpty(badge) ? badge : GameLanguage.Text("TAKEDOWN!");
                activeBannerSub = $"{killerName}  ⚔  {victimName}" +
                    (wasShutdown ? "   •   +1.5% FRONT" : isComebackKill ? "   •   +0.75% FRONT" : string.Empty);
                activeBannerColor = killerTeam.Team == 0 ? FrontEndTheme.Zero : FrontEndTheme.Accent;
                activeBannerUntil = now + 2.6f;
            }
        }

        private void EnsureStyles()
        {
            theme ??= new FrontEndTheme();
            readableFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? FrontEndTypography.Display;
            numStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = readableFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            shadowStyle ??= new GUIStyle(numStyle);
            bannerTitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = readableFont,
                fontStyle = FontStyle.Bold,
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            bannerSubStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = readableFont,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            feedStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = readableFont,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false
            };
        }

        private void OnGUI()
        {
            if (Time.timeScale <= 0f || MainMenuController.IsVisible || Event.current.type != EventType.Repaint)
                return;
            if (numbers.Count == 0 && feed.Count == 0 && Time.unscaledTime >= activeBannerUntil)
                return;

            EnsureStyles();
            Color oldColor = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;

            // 1. World-space Floating Damage & Heal Numbers
            if (cam != null && numbers.Count > 0 && PlayerPreferences.DamageNumbers)
            {
                float now = Time.unscaledTime;
                for (int i = 0; i < numbers.Count; i++)
                {
                    FloatingNumber fn = numbers[i];
                    float t = Mathf.Clamp01((now - fn.SpawnTime) / Mathf.Max(0.1f, fn.Duration));
                    Vector3 world = fn.WorldPos + Vector3.up * (t * 0.85f);
                    Vector3 screen = cam.WorldToScreenPoint(world);
                    if (screen.z <= 0f) continue;
                    float sx = screen.x + fn.DriftX * t;
                    float sy = Screen.height - screen.y;
                    if (sx < -60f || sx > Screen.width + 60f || sy < -40f || sy > Screen.height + 40f) continue;

                    float alpha = t < 0.7f ? 1f : Mathf.Clamp01(1f - (t - 0.7f) / 0.3f);
                    int fSize = Mathf.RoundToInt(fn.FontSize * Mathf.Clamp(Screen.height / 720f, 0.85f, 1.35f));
                    numStyle.fontSize = fSize;
                    shadowStyle.fontSize = fSize;

                    Rect r = new Rect(sx - 60f, sy - 14f, 120f, 28f);
                    shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.88f * alpha);
                    GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), fn.Text, shadowStyle);
                    Color c = fn.Color;
                    c.a = alpha;
                    numStyle.normal.textColor = c;
                    GUI.Label(r, fn.Text, numStyle);
                }
            }

            // 2. HUD-space Kill-Feed & Streak Callout Banner
            CombatHudLayout layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width > 0f && layout.Safe.height > 0f)
            {
                try
                {
                    GUI.matrix = layout.Matrix;
                    float now = Time.unscaledTime;

                    if (now < activeBannerUntil && !string.IsNullOrEmpty(activeBannerTitle))
                    {
                        float bw = 310f;
                        Rect bRect = new Rect((layout.Width - bw) * 0.5f, 134f, bw, 44f);
                        theme.HudPanel(bRect, 0.78f);
                        FrontEndTheme.Fill(new Rect(bRect.x, bRect.y, bRect.width, 2f), activeBannerColor);
                        bannerTitleStyle.normal.textColor = Color.Lerp(activeBannerColor, Color.white, 0.35f);
                        bannerSubStyle.normal.textColor = new Color(0.88f, 0.93f, 1f, 0.92f);
                        GUI.Label(new Rect(bRect.x + 8f, bRect.y + 3f, bRect.width - 16f, 22f), activeBannerTitle, bannerTitleStyle);
                        GUI.Label(new Rect(bRect.x + 8f, bRect.y + 23f, bRect.width - 16f, 18f), activeBannerSub, bannerSubStyle);
                    }

                    for (int i = 0; i < feed.Count; i++)
                    {
                        KillFeedRow row = feed[i];
                        float fw = 248f;
                        Rect fRect = new Rect(layout.Width - fw - 14f, 92f + i * 28f, fw, 24f);
                        theme.HudPanel(fRect, 0.65f);
                        Color tc = row.KillerTeam == 0 ? FrontEndTheme.Zero : FrontEndTheme.Accent;
                        FrontEndTheme.Fill(new Rect(fRect.xMax - 3f, fRect.y, 3f, fRect.height), tc);
                        string line = (!string.IsNullOrEmpty(row.Badge) ? "[" + row.Badge + "] " : "") +
                            row.Killer + " ⚔ " + row.Victim;
                        feedStyle.normal.textColor = Color.white;
                        GUI.Label(new Rect(fRect.x + 8f, fRect.y + 2f, fRect.width - 16f, 20f), line, feedStyle);
                    }
                }
                finally
                {
                    GUI.matrix = oldMatrix;
                    GUI.color = oldColor;
                }
            }
        }
    }
}
