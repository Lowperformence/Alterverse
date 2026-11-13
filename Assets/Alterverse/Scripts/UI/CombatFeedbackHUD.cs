using Alterverse.Audio;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Alterverse.UI
{
    /// <summary>
    /// Renders floating damage/shield/heal numbers (when PlayerPreferences.DamageNumbers is enabled),
    /// a live top-right Kill-Feed, Multi-Kill / Killstreak / Shutdown banners with comeback Front bonuses,
    /// and the 3-Killstreak Deployable Bolt Turret ("Geschütz") placement HUD and world-tap controller.
    /// </summary>
    [DefaultExecutionOrder(-260)]
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
        public const int KillsPerTurret = 3;

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
        private GUIStyle numStyle, shadowStyle, bannerTitleStyle, bannerSubStyle, feedStyle, turretBtnTitleStyle, turretBtnSubStyle;
        private float nextScan;
        private string activeBannerTitle;
        private string activeBannerSub;
        private Color activeBannerColor = Color.white;
        private float activeBannerUntil;

        private int localTurretCharges;
        private bool placingTurret;
        private int placementToggleFrame = -1;
        private float nextPreviewPulse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            var go = new GameObject("DF_CombatFeedbackHUD");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatFeedbackHUD>();
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

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
                if (MainMenuController.IsVisible && (numbers.Count > 0 || streakByHero.Count > 0 || localTurretCharges > 0 || placingTurret))
                {
                    numbers.Clear();
                    feed.Clear();
                    streakByHero.Clear();
                    multiKillCount.Clear();
                    lastKillAt.Clear();
                    activeBannerUntil = 0f;
                    localTurretCharges = 0;
                    placingTurret = false;
                }
                return;
            }

            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (front == null) front = FindAnyObjectByType<DimensionsFront>();
            if (front != null && front.HasEnded)
            {
                placingTurret = false;
                return;
            }

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.4f;
                RefreshSubscriptions();
            }

            HandleTurretPlacementInput();

            // Expire old floating numbers and feed rows
            float now = Time.unscaledTime;
            for (int i = numbers.Count - 1; i >= 0; i--)
                if (now - numbers[i].SpawnTime >= numbers[i].Duration)
                    numbers.RemoveAt(i);
            for (int i = feed.Count - 1; i >= 0; i--)
                if (now >= feed[i].Until)
                    feed.RemoveAt(i);
        }

        private static Rect GetTurretButtonRect(CombatHudLayout layout)
        {
            float w = 248f;
            float h = 50f;
            return new Rect((layout.Width - w) * 0.5f, layout.Height - h - 18f, w, h);
        }

        private void HandleTurretPlacementInput()
        {
            if (localTurretCharges <= 0 || InMatchSettingsMenu.IsOpen)
            {
                placingTurret = false;
                return;
            }

            GameObject localHero = LocalHeroContext.Find();
            if (localHero == null) return;
            Health localHp = localHero.GetComponent<Health>();
            if (localHp != null && localHp.IsDead)
            {
                placingTurret = false;
                return;
            }

            CombatHudLayout layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width <= 0f || layout.Safe.height <= 0f) return;
            Rect btnRect = GetTurretButtonRect(layout);

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame && placingTurret)
                {
                    placingTurret = false;
                    return;
                }
                if (kb.tKey.wasPressedThisFrame)
                {
                    placingTurret = !placingTurret;
                    placementToggleFrame = Time.frameCount;
                    UiSoundRouter.Confirm();
                    return;
                }
            }

            // Live ground preview ring while aiming with mouse in placement mode
            if (placingTurret && Mouse.current != null && Time.unscaledTime >= nextPreviewPulse)
            {
                Vector2 mScreen = Mouse.current.position.ReadValue();
                if (!btnRect.Contains(layout.Point(mScreen)) && TryScreenToArenaGround(mScreen, localHero.transform.position, out Vector3 previewPoint))
                {
                    nextPreviewPulse = Time.unscaledTime + 0.14f;
                    TeamMember tm = localHero.GetComponent<TeamMember>();
                    Color previewColor = (tm != null && tm.Team == 1) ? StylizedArtDirection.LumenEnergy : StylizedArtDirection.ZeroEnergy;
                    PrototypeVfx.Ring(previewPoint, previewColor, 0.85f, 0.16f);
                }
            }

            // Touch input
            var touches = ETouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                Vector2 hudPt = layout.Point(touch.screenPosition);
                if (btnRect.Contains(hudPt))
                {
                    placingTurret = !placingTurret;
                    placementToggleFrame = Time.frameCount;
                    UiSoundRouter.Confirm();
                    return;
                }
                if (placingTurret && Time.frameCount > placementToggleFrame)
                {
                    if (TryDeployTurretAtScreenPoint(touch.screenPosition, localHero))
                        return;
                }
            }

            // Mouse input (when no touch is active)
            if (touches.Count == 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 screenPos = Mouse.current.position.ReadValue();
                Vector2 hudPt = layout.Point(screenPos);
                if (btnRect.Contains(hudPt))
                {
                    placingTurret = !placingTurret;
                    placementToggleFrame = Time.frameCount;
                    UiSoundRouter.Confirm();
                    return;
                }
                if (placingTurret && Time.frameCount > placementToggleFrame)
                {
                    TryDeployTurretAtScreenPoint(screenPos, localHero);
                }
            }
        }

        private bool TryDeployTurretAtScreenPoint(Vector2 screenPosition, GameObject localHero)
        {
            if (localTurretCharges <= 0 || localHero == null) return false;
            if (!TryScreenToArenaGround(screenPosition, localHero.transform.position, out Vector3 worldPoint))
                return false;

            TeamMember tm = localHero.GetComponent<TeamMember>();
            int team = tm != null ? tm.Team : 0;
            KillstreakBoltTurret.Spawn(team, worldPoint, localHero);
            localTurretCharges = Mathf.Max(0, localTurretCharges - 1);
            placingTurret = false;

            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.TowerTarget, 0.85f);
            activeBannerTitle = GameLanguage.Text("BOLT-GESCHÜTZ PLATZIERT!");
            activeBannerSub = GameLanguage.Text("250 HP  •  Bolt-Basisangriff (1 Schaden)");
            activeBannerColor = team == 0 ? FrontEndTheme.Zero : FrontEndTheme.Accent;
            activeBannerUntil = Time.unscaledTime + 2.2f;
            return true;
        }

        private bool TryScreenToArenaGround(Vector2 screenPosition, Vector3 fallbackOrigin, out Vector3 groundPoint)
        {
            groundPoint = fallbackOrigin;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            int groundMask = 1 << GameplayCollision.Ground;
            Vector3 hitPos;
            if (Physics.Raycast(ray, out RaycastHit hit, 220f, groundMask, QueryTriggerInteraction.Ignore))
            {
                hitPos = hit.point;
            }
            else
            {
                Plane plane = new Plane(Vector3.up, new Vector3(0f, fallbackOrigin.y, 0f));
                if (!plane.Raycast(ray, out float enter) || enter <= 0f || enter > 250f)
                    return false;
                hitPos = ray.GetPoint(enter);
            }

            hitPos = GameplayCollision.ClampToRoundedArena(
                hitPos,
                ArenaLayout.MovementHalfWidth - 1.25f,
                ArenaLayout.MovementHalfHeight - 1.25f,
                1f);
            Vector3 surface = LunarSurfaceField.WorldSurface(hitPos.x, hitPos.z);
            groundPoint = new Vector3(hitPos.x, surface.y, hitPos.z);
            return true;
        }

        private void RefreshSubscriptions()
        {
            if (subscribed.Count > 256)
            {
                subscribed.RemoveWhere(x => x == null);
            }
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

            GameObject local = LocalHeroContext.Find();
            if (local != null && victim.gameObject == local)
                placingTurret = false;

            Health killerHealth = source != null ? source.GetComponentInParent<Health>() : null;
            if (killerHealth != null && killerHealth.GetComponent<KillstreakBoltTurret>() is KillstreakBoltTurret turretOwner && turretOwner.OwnerHero != null)
            {
                Health ownerHp = turretOwner.OwnerHero.GetComponent<Health>();
                if (ownerHp != null) killerHealth = ownerHp;
            }

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

            bool localKiller = local != null && killerHealth.gameObject == local;
            bool earnedTurret = newStreak > 0 && (newStreak % KillsPerTurret == 0);
            if (earnedTurret)
            {
                if (localKiller)
                {
                    localTurretCharges++;
                    Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.PlanetArrival, 0.85f);
                }
                else if (CoopAuthority.CanSimulateHostGameplay && killerHealth.GetComponent<Alterverse.AI.MobaBotBrain>() != null)
                {
                    Vector3 botPos = killerHealth.transform.position + killerHealth.transform.forward * 2.1f;
                    botPos = GameplayCollision.ClampToRoundedArena(botPos, ArenaLayout.MovementHalfWidth - 1.5f, ArenaLayout.MovementHalfHeight - 1.5f, 1f);
                    botPos.y = LunarSurfaceField.WorldSurface(botPos.x, botPos.z).y;
                    KillstreakBoltTurret.Spawn(killerTeam.Team, botPos, killerHealth.gameObject);
                }
            }

            string badge = earnedTurret && localKiller ? GameLanguage.Text("3ER KILLSTREAK — GESCHÜTZ BEREIT!")
                : wasShutdown ? GameLanguage.Text("SHUTDOWN!")
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

            if (localKiller || !string.IsNullOrEmpty(badge))
            {
                activeBannerTitle = !string.IsNullOrEmpty(badge) ? badge : GameLanguage.Text("TAKEDOWN!");
                activeBannerSub = earnedTurret && localKiller
                    ? GameLanguage.Text("Tippe unten auf [GESCHÜTZ PLATZIEREN], um es frei aufzustellen!")
                    : $"{killerName}  ⚔  {victimName}" +
                      (wasShutdown ? "   •   +1.5% FRONT" : isComebackKill ? "   •   +0.75% FRONT" : string.Empty);
                activeBannerColor = killerTeam.Team == 0 ? FrontEndTheme.Zero : FrontEndTheme.Accent;
                activeBannerUntil = now + (earnedTurret && localKiller ? 3.6f : 2.6f);
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
            turretBtnTitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = readableFont,
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            turretBtnSubStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = readableFont,
                fontStyle = FontStyle.Bold,
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
        }

        private int GetLocalStreakProgress()
        {
            GameObject local = LocalHeroContext.Find();
            if (local == null) return 0;
            Health hp = local.GetComponent<Health>();
            if (hp == null || hp.IsDead) return 0;
            return streakByHero.TryGetValue(hp, out int s) ? (s % KillsPerTurret) : 0;
        }

        private void OnGUI()
        {
            if (Time.timeScale <= 0f || MainMenuController.IsVisible || Event.current.type != EventType.Repaint)
                return;
            int streakProgress = GetLocalStreakProgress();
            if (numbers.Count == 0 && feed.Count == 0 && Time.unscaledTime >= activeBannerUntil && localTurretCharges <= 0 && streakProgress <= 0)
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

            // 2. HUD-space Kill-Feed, Streak Callout Banner & 3-Killstreak Turret Placement UI
            CombatHudLayout layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width > 0f && layout.Safe.height > 0f)
            {
                try
                {
                    GUI.matrix = layout.Matrix;
                    float now = Time.unscaledTime;

                    if (now < activeBannerUntil && !string.IsNullOrEmpty(activeBannerTitle))
                    {
                        float bw = 340f;
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

                    DrawTurretHudControl(layout, streakProgress);
                }
                finally
                {
                    GUI.matrix = oldMatrix;
                    GUI.color = oldColor;
                }
            }
        }

        private void DrawTurretHudControl(CombatHudLayout layout, int streakProgress)
        {
            if (InMatchSettingsMenu.IsOpen || (front != null && front.HasEnded)) return;

            if (localTurretCharges > 0)
            {
                Rect btn = GetTurretButtonRect(layout);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
                Color accent = placingTurret
                    ? new Color(1f, 0.62f, 0.18f, 1f)
                    : Color.Lerp(new Color(0.18f, 0.86f, 1f, 1f), new Color(1f, 0.84f, 0.28f, 1f), pulse * 0.45f);

                theme.HudPanel(btn, 0.90f);
                DimensionUiMotif.DrawCornerCuts(btn, FrontEndTheme.Pixel, accent, FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(btn.x, btn.y, btn.width, 2.5f), accent);
                FrontEndTheme.Fill(new Rect(btn.x, btn.yMax - 2f, btn.width, 2f), new Color(accent.r, accent.g, accent.b, 0.55f));

                string title = placingTurret
                    ? GameLanguage.Text("🎯 AUF MAP TIPPEN ZUM AUFSTELLEN")
                    : GameLanguage.Text($"⚡ GESCHÜTZ PLATZIEREN ({localTurretCharges})  [T]");
                string sub = placingTurret
                    ? GameLanguage.Text("Hier tippen oder [ESC] zum Abbrechen")
                    : GameLanguage.Text("250 HP  •  BOLT-GATLING (1 DMG)  •  FREI WÄHLBAR");

                turretBtnTitleStyle.normal.textColor = Color.Lerp(accent, Color.white, 0.35f);
                turretBtnSubStyle.normal.textColor = new Color(0.86f, 0.92f, 0.98f, 0.92f);
                GUI.Label(new Rect(btn.x + 8f, btn.y + 5f, btn.width - 16f, 22f), title, turretBtnTitleStyle);
                GUI.Label(new Rect(btn.x + 8f, btn.y + 26f, btn.width - 16f, 18f), sub, turretBtnSubStyle);
            }
            else if (streakProgress > 0)
            {
                float w = 186f;
                float h = 26f;
                Rect pill = new Rect((layout.Width - w) * 0.5f, layout.Height - h - 16f, w, h);
                theme.HudPanel(pill, 0.72f);
                Color barColor = new Color(0.24f, 0.84f, 1f, 0.88f);
                FrontEndTheme.Fill(new Rect(pill.x, pill.yMax - 2f, pill.width * (streakProgress / (float)KillsPerTurret), 2f), barColor);
                turretBtnSubStyle.normal.textColor = new Color(0.88f, 0.94f, 1f, 0.90f);
                GUI.Label(pill, GameLanguage.Text($"GESCHÜTZ-STREAK: {streakProgress} / {KillsPerTurret} KILLS"), turretBtnSubStyle);
            }
        }
    }

    /// <summary>
    /// Deployable 3-Killstreak Turret ("Geschütz") with 250 HP that fires Bolt-7's rapid-fire basic attack for 1 damage.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(TeamMember))]
    public sealed class KillstreakBoltTurret : MonoBehaviour
    {
        public const float MaxHp = 250f;
        public const float AttackDamage = 1f;
        public const float AttackRange = Bolt7Balance.BasicRange;

        private static readonly List<KillstreakBoltTurret> active = new(8);
        private static readonly Dictionary<PrimitiveType, Mesh> sharedMeshes = new();
        private static Material armorMat, darkMetalMat, boltGoldMat;
        private static readonly Material[] teamGlowMats = new Material[2];
        private static Transform cachedMatchWorld;

        private Health health;
        private Health currentTarget;
        private int team;
        private GameObject ownerHero;
        private Transform turretHead;
        private Transform barrelCluster;
        private GameObject muzzleActor;
        private float nextScanTime;
        private float nextFireTime;
        private float barrelSpinAngle;
        private float recoilOffset;

        public GameObject OwnerHero => ownerHero;
        public int Team => team;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            active.Clear();
            sharedMeshes.Clear();
            armorMat = darkMetalMat = boltGoldMat = null;
            teamGlowMats[0] = teamGlowMats[1] = null;
            cachedMatchWorld = null;
        }

        public static KillstreakBoltTurret Spawn(int teamId, Vector3 position, GameObject owner)
        {
            int clampedTeam = Mathf.Clamp(teamId, 0, 1);
            Vector3 surface = LunarSurfaceField.WorldSurface(position.x, position.z);
            position.y = surface.y;

            GameObject go = new GameObject($"DF_{(clampedTeam == 0 ? "Zero" : "Lumen")}_BoltTurret");
            go.layer = GameplayCollision.Units;
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(clampedTeam == 0 ? Vector3.right : Vector3.left, Vector3.up);

            if (cachedMatchWorld == null)
            {
                GameObject world = GameObject.Find("DF_MatchWorld");
                if (world != null) cachedMatchWorld = world.transform;
            }
            if (cachedMatchWorld != null)
                go.transform.SetParent(cachedMatchWorld, true);

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.42f;
            col.height = 1.25f;
            col.center = new Vector3(0f, 0.62f, 0f);

            Health hp = go.AddComponent<Health>();
            hp.Configure(MaxHp, true);

            TeamMember tm = go.AddComponent<TeamMember>();
            tm.Initialize(clampedTeam);

            KillstreakBoltTurret turret = go.AddComponent<KillstreakBoltTurret>();
            turret.Initialize(clampedTeam, owner);

            Color teamColor = clampedTeam == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            PrototypeVfx.Ring(position, teamColor, 1.35f, 0.32f);
            PrototypeVfx.Burst(position + Vector3.up * 0.45f, teamColor, 0.55f, 6);
            return turret;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            if (health != null) health.Died += OnTurretDied;
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        private void OnDestroy()
        {
            active.Remove(this);
            if (health != null) health.Died -= OnTurretDied;
        }

        private void Initialize(int assignedTeam, GameObject owner)
        {
            team = assignedTeam;
            ownerHero = owner;
            nextScanTime = Time.time + 0.05f;
            nextFireTime = Time.time + 0.15f;
            BuildTurretVisual();
            Bolt7Vfx.Warm();
        }

        private void OnTurretDied()
        {
            Color teamColor = team == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            PrototypeVfx.RadialBurst(transform.position, teamColor, 1.65f);
            PrototypeVfx.Burst(transform.position + Vector3.up * 0.5f, teamColor, 0.65f, 6);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || health == null || health.IsDead) return;
            if (recoilOffset > 0.0001f)
                recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, Time.deltaTime * 0.35f);

            if (!CoopAuthority.CanSimulateHostGameplay) return;

            if (currentTarget != null && !IsValidTarget(currentTarget))
                currentTarget = null;

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + 0.16f;
                currentTarget = FindTarget();
            }

            if (currentTarget == null) return;

            AimAt(currentTarget.transform.position);
            if (barrelCluster != null)
            {
                barrelSpinAngle = Mathf.Repeat(barrelSpinAngle + Time.deltaTime * 720f, 360f);
                barrelCluster.localRotation = Quaternion.Euler(0f, 0f, barrelSpinAngle);
            }

            float attackInterval = Mathf.Max(0.10f, Bolt7Balance.BasicInterval);
            if (Time.time < nextFireTime) return;
            nextFireTime = Time.time + attackInterval;
            FireBoltBasicAttack(currentTarget);
        }

        private void AimAt(Vector3 targetPos)
        {
            if (turretHead == null) return;
            Vector3 delta = targetPos - turretHead.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(delta.normalized, Vector3.up);
                turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, desired, 360f * Time.deltaTime);
            }
            turretHead.localPosition = new Vector3(0f, 0.72f, -recoilOffset);
        }

        private void FireBoltBasicAttack(Health victim)
        {
            if (victim == null || victim.IsDead) return;
            recoilOffset = 0.045f;

            GameObject vfxOrigin = muzzleActor != null ? muzzleActor : gameObject;
            Vector3 hitPoint = victim.transform.position;
            Bolt7Vfx.Basic(vfxOrigin, hitPoint, 0.06f);
            Bolt7Vfx.BasicImpact(vfxOrigin, hitPoint);

            // Credit the owner hero on a lethal blow for scoreboard/killstreak tracking,
            // while passing the turret GameObject on non-lethal hits so damage stays strictly 1.0.
            GameObject damageSource = (victim.Current + victim.Shield <= AttackDamage + 0.05f && ownerHero != null)
                ? ownerHero
                : gameObject;
            victim.TakeDamage(AttackDamage, damageSource);
        }

        private bool IsValidTarget(Health candidate)
        {
            if (candidate == null || candidate == health || candidate.IsDead || !candidate.isActiveAndEnabled)
                return false;
            if (CombatVisibility.HiddenFrom(gameObject, candidate))
                return false;
            Vector3 d = candidate.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > AttackRange * AttackRange)
                return false;
            return IsEnemyUnit(candidate);
        }

        private bool IsEnemyUnit(Health candidate)
        {
            if (candidate.GetComponent<RealityCore>() != null || candidate.GetComponent<LaneTower>() != null)
                return false;
            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            if (minion != null) return minion.Team != team;
            TeamMember member = candidate.GetComponent<TeamMember>();
            return member != null && member.Team != team;
        }

        private Health FindTarget()
        {
            Health bestHero = null;
            Health bestOther = null;
            float bestHeroSqr = AttackRange * AttackRange;
            float bestOtherSqr = AttackRange * AttackRange;
            var activeHealth = Health.Active;
            for (int i = 0; i < activeHealth.Count; i++)
            {
                Health candidate = activeHealth[i];
                if (!IsValidTarget(candidate)) continue;
                Vector3 d = candidate.transform.position - transform.position;
                d.y = 0f;
                float sqr = d.sqrMagnitude * (candidate == currentTarget ? 0.72f : 1f);
                if (candidate.GetComponent<HeroIdentity>() != null)
                {
                    if (sqr < bestHeroSqr)
                    {
                        bestHeroSqr = sqr;
                        bestHero = candidate;
                    }
                }
                else if (sqr < bestOtherSqr)
                {
                    bestOtherSqr = sqr;
                    bestOther = candidate;
                }
            }
            return bestHero != null ? bestHero : bestOther;
        }

        private static Mesh GetSharedMesh(PrimitiveType type)
        {
            if (sharedMeshes.TryGetValue(type, out Mesh m) && m != null) return m;
            GameObject temp = GameObject.CreatePrimitive(type);
            MeshFilter mf = temp.GetComponent<MeshFilter>();
            m = mf != null ? mf.sharedMesh : null;
            Destroy(temp);
            if (m != null) sharedMeshes[type] = m;
            return m;
        }

        private static void EnsureMaterials(int teamIndex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            if (armorMat == null)
                armorMat = CreateMat(shader, new Color(0.24f, 0.28f, 0.34f, 1f), 0.55f, 0.52f, Color.black);
            if (darkMetalMat == null)
                darkMetalMat = CreateMat(shader, new Color(0.11f, 0.13f, 0.16f, 1f), 0.72f, 0.44f, Color.black);
            if (boltGoldMat == null)
                boltGoldMat = CreateMat(shader, new Color(0.95f, 0.68f, 0.18f, 1f), 0.65f, 0.62f, new Color(0.95f, 0.62f, 0.12f) * 0.45f);
            int idx = Mathf.Clamp(teamIndex, 0, 1);
            if (teamGlowMats[idx] == null)
            {
                Color c = idx == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
                teamGlowMats[idx] = CreateMat(shader, c, 0.10f, 0.75f, c * 2.2f);
            }
        }

        private static Material CreateMat(Shader shader, Color color, float metallic, float smoothness, Color emission)
        {
            Material m = new Material(shader) { color = color, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        private void BuildTurretVisual()
        {
            EnsureMaterials(team);
            Material glowMat = teamGlowMats[Mathf.Clamp(team, 0, 1)];

            Transform root = new GameObject("DF_BoltTurretVisual").transform;
            root.SetParent(transform, false);

            // Armored base ring & tripod stabilizers
            Part("BasePlinth", PrimitiveType.Cylinder, root, new Vector3(0f, 0.08f, 0f), new Vector3(0.78f, 0.08f, 0.78f), darkMetalMat, Quaternion.identity, false);
            Part("BaseCoreRing", PrimitiveType.Cylinder, root, new Vector3(0f, 0.18f, 0f), new Vector3(0.56f, 0.06f, 0.56f), glowMat, Quaternion.identity, false);
            Part("CenterColumn", PrimitiveType.Cylinder, root, new Vector3(0f, 0.42f, 0f), new Vector3(0.32f, 0.28f, 0.32f), armorMat, Quaternion.identity, true);

            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Transform legPivot = new GameObject("Leg_" + i).transform;
                legPivot.SetParent(root, false);
                legPivot.localRotation = rot;
                Part("Strut", PrimitiveType.Cube, legPivot, new Vector3(0f, 0.16f, 0.36f), new Vector3(0.14f, 0.12f, 0.44f), armorMat, Quaternion.Euler(18f, 0f, 0f), false);
                Part("FootPad", PrimitiveType.Cube, legPivot, new Vector3(0f, 0.05f, 0.54f), new Vector3(0.20f, 0.08f, 0.22f), boltGoldMat, Quaternion.identity, false);
            }

            // Rotating Bolt-7 style turret housing
            turretHead = new GameObject("DF_TurretHead").transform;
            turretHead.SetParent(root, false);
            turretHead.localPosition = new Vector3(0f, 0.72f, 0f);

            Part("HousingMain", PrimitiveType.Cube, turretHead, new Vector3(0f, 0.04f, -0.04f), new Vector3(0.52f, 0.36f, 0.62f), armorMat, Quaternion.identity, true);
            Part("HousingTopCrest", PrimitiveType.Cube, turretHead, new Vector3(0f, 0.25f, -0.06f), new Vector3(0.34f, 0.10f, 0.46f), boltGoldMat, Quaternion.identity, false);
            Part("SensorVisor", PrimitiveType.Cube, turretHead, new Vector3(0f, 0.12f, 0.28f), new Vector3(0.36f, 0.10f, 0.06f), glowMat, Quaternion.identity, false);
            Part("AmmoDrumL", PrimitiveType.Cylinder, turretHead, new Vector3(-0.32f, 0.02f, -0.05f), new Vector3(0.22f, 0.14f, 0.22f), darkMetalMat, Quaternion.Euler(0f, 0f, 90f), false);
            Part("AmmoDrumR", PrimitiveType.Cylinder, turretHead, new Vector3(0.32f, 0.02f, -0.05f), new Vector3(0.22f, 0.14f, 0.22f), darkMetalMat, Quaternion.Euler(0f, 0f, 90f), false);

            // Spinning triple Gatling barrel assembly (Bolt-7 basic weapon signature)
            barrelCluster = new GameObject("DF_GatlingCluster").transform;
            barrelCluster.SetParent(turretHead, false);
            barrelCluster.localPosition = new Vector3(0f, -0.02f, 0.34f);

            Part("GatlingCollar", PrimitiveType.Cylinder, barrelCluster, new Vector3(0f, 0f, 0.10f), new Vector3(0.22f, 0.08f, 0.22f), boltGoldMat, Quaternion.Euler(90f, 0f, 0f), false);
            for (int b = 0; b < 3; b++)
            {
                float rad = b * 120f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad) * 0.065f, Mathf.Sin(rad) * 0.065f, 0.26f);
                Part("Barrel_" + b, PrimitiveType.Cylinder, barrelCluster, offset, new Vector3(0.048f, 0.28f, 0.048f), darkMetalMat, Quaternion.Euler(90f, 0f, 0f), false);
            }
            Part("MuzzleCore", PrimitiveType.Cylinder, barrelCluster, new Vector3(0f, 0f, 0.52f), new Vector3(0.12f, 0.03f, 0.12f), glowMat, Quaternion.Euler(90f, 0f, 0f), false);

            muzzleActor = new GameObject("DF_TurretMuzzle");
            muzzleActor.transform.SetParent(turretHead, false);
            muzzleActor.transform.localPosition = new Vector3(0f, -0.02f, 0.92f);
        }

        private static void Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion localRot, bool castShadows)
        {
            Mesh mesh = GetSharedMesh(type);
            GameObject go = new GameObject("DF_BoltTurret_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRot;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }
        }
    }
}
