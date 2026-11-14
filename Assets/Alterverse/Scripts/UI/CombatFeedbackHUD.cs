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
            public Health Target;
            public float AccumulatedValue;
            public Vector3 WorldPos;
            public string Text;
            public Color Color;
            public float SpawnTime;
            public float PopTime;
            public float Duration;
            public int FontSize;
            public float DriftX;
            public bool IsRapidStackable;
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
        private int localMobileTurretCharges;
        private int networkLocalStreak;
        private bool localUpgradeEligible;
        private readonly KillstreakRewardState offlineRewards = new();
        private readonly KillstreakRewardState[] networkRewards = new KillstreakRewardState[MatchSession.MaxPlayers];
        private CoopMatchPlan rewardPlan;
        private float nextTurretRequestAt;
        private bool placingTurret;
        private int placementToggleFrame = -1;
        private float nextPreviewPulse;
        private float nextPingAllowedAt;
        private Vector3 activePingWorldPos;
        private Color activePingColor = Color.white;
        private string activePingLabel;
        private float activePingUntil;

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
                if (MainMenuController.IsVisible && (numbers.Count > 0 || streakByHero.Count > 0 || localTurretCharges > 0 || localMobileTurretCharges > 0 || placingTurret))
                {
                    numbers.Clear();
                    feed.Clear();
                    streakByHero.Clear();
                    multiKillCount.Clear();
                    lastKillAt.Clear();
                    activeBannerUntil = 0f;
                    localTurretCharges = 0;
                    localMobileTurretCharges = 0;localUpgradeEligible = false;networkLocalStreak = 0;
                    offlineRewards.Reset();
                    placingTurret = false;
                }
                return;
            }

            // Dedicated servers do not visit the menu between network rounds.
            if (!ReferenceEquals(rewardPlan, CoopAuthority.ActivePlan))
            {
                rewardPlan=CoopAuthority.ActivePlan;
                for(int i=0;i<networkRewards.Length;i++)networkRewards[i]=null;
                streakByHero.Clear();multiKillCount.Clear();lastKillAt.Clear();
                localTurretCharges=localMobileTurretCharges=networkLocalStreak=0;
                localUpgradeEligible=false;placingTurret=false;nextTurretRequestAt=0f;
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
            HandleTacticalPingInput();

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
            if (localTurretCharges <= 0 && localMobileTurretCharges <= 0 || InMatchSettingsMenu.IsOpen)
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
            if ((localTurretCharges <= 0 && localMobileTurretCharges <= 0) || localHero == null || Time.unscaledTime < nextTurretRequestAt) return false;
            if (!TryScreenToArenaGround(screenPosition, localHero.transform.position, out Vector3 worldPoint))
                return false;

            TeamMember tm = localHero.GetComponent<TeamMember>();
            int team = tm != null ? tm.Team : 0;
            if(CoopAuthority.Active&&!CoopAuthority.LocalHasServerAuthority)
            {
                if(!KillstreakNetworkSync.TrySendPlacement(localHero,worldPoint))return false;
                nextTurretRequestAt=Time.unscaledTime+1f;
                placingTurret=false;
                return true; // Wait for the authoritative reward snapshot before consuming.
            }
            KillstreakRewardState rewards=CoopAuthority.Active?RewardsFor(localHero):offlineRewards;
            if(rewards==null||!rewards.HasReward)return false;
            bool mobile=rewards.NextIsMobile;
            if(KillstreakBoltTurret.Spawn(team, worldPoint, localHero, mobile)==null)return false;
            rewards.Consume();CopyLocalRewards(rewards);
            placingTurret = false;

            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.TowerTarget, 0.85f);
            activeBannerTitle = GameLanguage.Text(mobile?"MOBILE TURRET DEPLOYED!":"TURRET DEPLOYED!");
            activeBannerSub = GameLanguage.Text(mobile?"250 HP  •  2 DAMAGE  •  MOBILE":"250 HP  •  1 DAMAGE");
            activeBannerColor = team == 0 ? FrontEndTheme.Zero : FrontEndTheme.Accent;
            activeBannerUntil = Time.unscaledTime + 2.2f;
            return true;
        }

        private KillstreakRewardState RewardsFor(GameObject actor)
        {
            CoopHeroSlot slot=actor!=null?actor.GetComponent<CoopHeroSlot>():null;
            if(slot==null||!slot.IsHuman||slot.Slot<0||slot.Slot>=networkRewards.Length)return null;
            return networkRewards[slot.Slot]??=new KillstreakRewardState();
        }

        private void CopyLocalRewards(KillstreakRewardState state)
        {localTurretCharges=state.Standard;localMobileTurretCharges=state.Mobile;localUpgradeEligible=state.UpgradeEligible;}

        // The server owns rewards and validates placements. Clients receive only their UI snapshot.
        public static bool GetNetworkReward(int slot,out int standard,out int mobile,out bool eligible,out int streak)
        {
            standard=mobile=streak=0;eligible=false;
            if(instance==null||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||slot<0||slot>=MatchSession.MaxPlayers)return false;
            var state=instance.networkRewards[slot];
            if(state!=null){standard=state.Standard;mobile=state.Mobile;eligible=state.UpgradeEligible;}
            foreach(Health hp in Health.Active)
            {
                var hero=hp!=null?hp.GetComponent<CoopHeroSlot>():null;
                if(hero!=null&&hero.Slot==slot&&instance.streakByHero.TryGetValue(hp,out int current)){streak=current;break;}
            }
            return true;
        }

        public static void ApplyNetworkReward(int slot,int standard,int mobile,bool eligible,int streak)
        {
            if(instance==null||!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority)return;
            GameObject player=LocalHeroContext.Find();
            CoopHeroSlot hero=player!=null?player.GetComponent<CoopHeroSlot>():null;
            if(hero==null||!hero.IsLocalHuman||hero.Slot!=slot)return;
            bool mobileEarned=mobile>instance.localMobileTurretCharges;
            bool standardEarned=standard>instance.localTurretCharges;
            instance.localTurretCharges=Mathf.Max(0,standard);
            instance.localMobileTurretCharges=Mathf.Max(0,mobile);
            instance.localUpgradeEligible=eligible&&standard>0;
            instance.networkLocalStreak=Mathf.Max(0,streak);
            if(mobileEarned||standardEarned)
            {
                TeamMember member=player.GetComponent<TeamMember>();
                instance.activeBannerTitle=GameLanguage.Text(mobileEarned?"MOBILE TURRET READY!":"TURRET READY!");
                instance.activeBannerSub=GameLanguage.Text("TAP THE MAP TO DEPLOY");
                instance.activeBannerColor=member!=null&&member.Team==1?FrontEndTheme.Accent:FrontEndTheme.Zero;
                instance.activeBannerUntil=Time.unscaledTime+3.6f;
                Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.PlanetArrival,.85f);
            }
        }

        public static bool DeployNetworkTurret(GameObject actor,Vector3 position)
        {
            if(instance==null||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||actor==null)return false;
            Health hp=actor.GetComponent<Health>();TeamMember member=actor.GetComponent<TeamMember>();
            KillstreakRewardState state=instance.RewardsFor(actor);
            if(hp==null||hp.IsDead||member==null||state==null||!state.HasReward||
                !MatchNetworkProtocol.Finite(position.x)||!MatchNetworkProtocol.Finite(position.y)||!MatchNetworkProtocol.Finite(position.z))return false;
            position=GameplayCollision.ClampToRoundedArena(position,ArenaLayout.MovementHalfWidth-1.25f,ArenaLayout.MovementHalfHeight-1.25f,1f);
            position.y=LunarSurfaceField.WorldSurface(position.x,position.z).y;
            if(KillstreakBoltTurret.Spawn(member.Team,position,actor,state.NextIsMobile)==null)return false;
            state.Consume();
            if(actor==LocalHeroContext.Find())instance.CopyLocalRewards(state);
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

            bool rapidSmallHit = !bigHit && total <= 14f && victim.gameObject != local;
            Vector3 anchorPos = victim.transform.position + Vector3.up * (isHeroVictim ? 1.75f : 1.25f);
            if (rapidSmallHit)
            {
                float now = Time.unscaledTime;
                for (int i = numbers.Count - 1; i >= 0; i--)
                {
                    FloatingNumber existing = numbers[i];
                    if (existing.IsRapidStackable && existing.Target == victim && now - existing.PopTime <= 0.26f)
                    {
                        existing.AccumulatedValue += total;
                        int stackedInt = Mathf.Max(1, Mathf.RoundToInt(existing.AccumulatedValue));
                        existing.Text = stackedInt.ToString();
                        existing.WorldPos = anchorPos;
                        existing.PopTime = now;
                        existing.SpawnTime = Mathf.Max(existing.SpawnTime, now - 0.12f);
                        existing.FontSize = stackedInt >= 25 ? 18 : 16;
                        numbers[i] = existing;
                        return;
                    }
                }
            }
            string text = (bigHit && victim.gameObject != local ? "✦ " : "") + Mathf.Max(1, Mathf.RoundToInt(total));
            SpawnFloating(
                anchorPos,
                text,
                c,
                bigHit ? 20 : 15,
                bigHit ? 0.95f : 0.75f,
                victim,
                total,
                rapidSmallHit);
        }

        private void SpawnFloating(Vector3 worldPos, string text, Color color, int size, float duration, Health target = null, float rawValue = 0f, bool rapidStackable = false)
        {
            if (numbers.Count >= MaxFloating)
                numbers.RemoveAt(0);
            float drift = ((numbers.Count % 5) - 2) * 0.22f;
            float now = Time.unscaledTime;
            numbers.Add(new FloatingNumber
            {
                Target = target,
                AccumulatedValue = rawValue,
                WorldPos = worldPos + new Vector3(drift, 0f, 0f),
                Text = text,
                Color = color,
                SpawnTime = now,
                PopTime = now,
                Duration = duration,
                FontSize = size,
                DriftX = drift * 18f,
                IsRapidStackable = rapidStackable
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
            if(CoopAuthority.CanSimulateHostGameplay)
            {
                if(CoopAuthority.Active)
                {
                    var victimRewards=RewardsFor(victim.gameObject);
                    if(victimRewards!=null){victimRewards.RecordDeath();if(local==victim.gameObject)CopyLocalRewards(victimRewards);}
                }
                else if(local!=null&&victim.gameObject==local){offlineRewards.RecordDeath();CopyLocalRewards(offlineRewards);}
            }

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
            bool earnedTurret = false;
            bool earnedMobile = false;
            if(CoopAuthority.CanSimulateHostGameplay)
            {
                KillstreakRewardState reward=CoopAuthority.Active?RewardsFor(killerHealth.gameObject):localKiller?offlineRewards:null;
                if(reward!=null)
                {
                    int previousMobile=reward.Mobile;
                    earnedTurret=reward.RecordKill(newStreak);
                    earnedMobile=reward.Mobile>previousMobile;
                    if(localKiller&&earnedTurret)
                    {
                        CopyLocalRewards(reward);
                        Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.PlanetArrival, 0.85f);
                    }
                }
                else if (newStreak % KillsPerTurret == 0 && killerHealth.GetComponent<Alterverse.AI.MobaBotBrain>() != null)
                {
                    Vector3 botPos = killerHealth.transform.position + killerHealth.transform.forward * 2.1f;
                    botPos = GameplayCollision.ClampToRoundedArena(botPos, ArenaLayout.MovementHalfWidth - 1.5f, ArenaLayout.MovementHalfHeight - 1.5f, 1f);
                    botPos.y = LunarSurfaceField.WorldSurface(botPos.x, botPos.z).y;
                    KillstreakBoltTurret.Spawn(killerTeam.Team, botPos, killerHealth.gameObject);
                }
            }

            string badge = earnedTurret && localKiller ? GameLanguage.Text(earnedMobile?"MOBILE TURRET READY!":"TURRET READY!")
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
                    ? GameLanguage.Text("TAP THE MAP TO DEPLOY")
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
            if(CoopAuthority.Active&&!CoopAuthority.LocalHasServerAuthority)return networkLocalStreak % KillsPerTurret;
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
            EnsureStyles();
            Color oldColor = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;

            // 0. World-space Active Tactical Ping & Turret Range Preview
            if (cam != null)
            {
                DrawWorldPingAndTurretPreview();
            }

            // 1. World-space Floating Damage & Heal Numbers
            if (cam != null && numbers.Count > 0 && PlayerPreferences.DamageNumbers)
            {
                float now = Time.unscaledTime;
                for (int i = 0; i < numbers.Count; i++)
                {
                    FloatingNumber fn = numbers[i];
                    float t = Mathf.Clamp01((now - fn.SpawnTime) / Mathf.Max(0.1f, fn.Duration));
                    float lift = 1f - Mathf.Pow(1f - t, 2.2f);
                    Vector3 world = fn.WorldPos + Vector3.up * (lift * 0.92f);
                    Vector3 screen = cam.WorldToScreenPoint(world);
                    if (screen.z <= 0f) continue;

                    float sx = screen.x + fn.DriftX * t;
                    float sy = Screen.height - screen.y;
                    if (sx < -60f || sx > Screen.width + 60f || sy < -40f || sy > Screen.height + 40f) continue;

                    float alpha = t < 0.7f ? 1f : Mathf.Clamp01(1f - (t - 0.7f) / 0.3f);
                    float popElapsed = now - fn.PopTime;
                    float popScale = popElapsed < 0.12f ? Mathf.Lerp(1.32f, 1f, popElapsed / 0.12f) : 1f;
                    int fSize = Mathf.RoundToInt(fn.FontSize * popScale * Mathf.Clamp(Screen.height / 720f, 0.85f, 1.35f));
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
                    DrawTacticalPingHud(layout);
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

            if (localTurretCharges > 0 || localMobileTurretCharges > 0)
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

                bool mobile=localMobileTurretCharges>0;
                string title = placingTurret
                    ? GameLanguage.Text("TAP THE MAP TO DEPLOY")
                    : GameLanguage.Text(mobile?"DEPLOY MOBILE TURRET":"DEPLOY TURRET") + " (" + (mobile?localMobileTurretCharges:localTurretCharges) + ")  [T]";
                string sub = placingTurret
                    ? GameLanguage.Text("TAP A LOCATION OR PRESS ESC TO CANCEL")
                    : GameLanguage.Text(mobile?"250 HP  •  2 DAMAGE  •  MOBILE":localUpgradeEligible?"250 HP  •  1 DAMAGE  •  5 KILLS: MOBILE":"250 HP  •  1 DAMAGE");

                turretBtnTitleStyle.normal.textColor = Color.Lerp(accent, Color.white, 0.35f);
                turretBtnSubStyle.normal.textColor = new Color(0.86f, 0.92f, 0.98f, 0.92f);
                theme.FitLabel(new Rect(btn.x + 8f, btn.y + 5f, btn.width - 16f, 22f), title, turretBtnTitleStyle);
                theme.FitLabel(new Rect(btn.x + 8f, btn.y + 26f, btn.width - 16f, 18f), sub, turretBtnSubStyle);
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

        private static Rect GetPingButtonRect(CombatHudLayout layout, int index)
        {
            // One row along the bottom of the safe area, below the ability cluster.
            const float width = 104f, height = 28f, gap = 6f, inset = 12f;
            float x = layout.Width - inset - (width * 3f + gap * 2f) + index * (width + gap);
            return new Rect(x, layout.Height - inset - height, width, height);
        }

        private void HandleTacticalPingInput()
        {
            if (InMatchSettingsMenu.IsOpen || (front != null && front.HasEnded) || Time.unscaledTime < nextPingAllowedAt)
                return;

            GameObject localHero = LocalHeroContext.Find();
            if (localHero == null) return;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { TriggerTacticalPing(0, localHero); return; }
                if (kb.digit2Key.wasPressedThisFrame) { TriggerTacticalPing(1, localHero); return; }
                if (kb.digit3Key.wasPressedThisFrame) { TriggerTacticalPing(2, localHero); return; }
            }

            if (placingTurret) return;

            CombatHudLayout layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width <= 0f || layout.Safe.height <= 0f) return;

            var touches = ETouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                Vector2 pt = layout.Point(touch.screenPosition);
                for (int p = 0; p < 3; p++)
                {
                    if (GetPingButtonRect(layout, p).Contains(pt))
                    {
                        TriggerTacticalPing(p, localHero);
                        return;
                    }
                }
            }

            if (touches.Count == 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 pt = layout.Point(Mouse.current.position.ReadValue());
                for (int p = 0; p < 3; p++)
                {
                    if (GetPingButtonRect(layout, p).Contains(pt))
                    {
                        TriggerTacticalPing(p, localHero);
                        return;
                    }
                }
            }
        }

        private void TriggerTacticalPing(int kind, GameObject localHero)
        {
            nextPingAllowedAt = Time.unscaledTime + 1.1f;
            TeamMember tm = localHero.GetComponent<TeamMember>();
            int team = tm != null ? tm.Team : 0;
            SoftTargeting targeting = localHero.GetComponent<SoftTargeting>();
            Health focus = targeting != null ? targeting.Current : null;

            Vector3 pingPos;
            if (kind == 0) // Attack
            {
                pingPos = focus != null ? focus.transform.position : localHero.transform.position + localHero.transform.forward * 5.5f;
                activePingColor = new Color(1f, 0.36f, 0.32f, 0.96f);
                activePingLabel = "⚔ " + GameLanguage.Text("ATTACK") + "!";
            }
            else if (kind == 1) // Rally / Objective
            {
                DimensionPlanet planet = FindAnyObjectByType<DimensionPlanet>();
                pingPos = (planet != null && planet.Available) ? planet.transform.position : Vector3.zero;
                activePingColor = new Color(1f, 0.82f, 0.24f, 0.96f);
                activePingLabel = "🪐 " + GameLanguage.Text("RALLY") + "!";
            }
            else // Defend
            {
                float homeX = team == 0 ? -ArenaLayout.HealingX * 0.65f : ArenaLayout.HealingX * 0.65f;
                pingPos = new Vector3(homeX, 0f, 0f);
                activePingColor = new Color(0.24f, 0.86f, 1f, 0.96f);
                activePingLabel = "🛡 " + GameLanguage.Text("DEFEND") + "!";
            }
            pingPos.y = LunarSurfaceField.WorldSurface(pingPos.x, pingPos.z).y;
            activePingWorldPos = pingPos;
            activePingUntil = Time.unscaledTime + 3.2f;

            UiSoundRouter.Confirm();
            PrototypeVfx.Ring(pingPos, activePingColor, 1.85f, 0.45f);
            if (CoopAuthority.CanSimulateHostGameplay)
                Alterverse.AI.BotTeamCoordinator.NotifyPlayerPing(team, kind, pingPos, focus);
        }

        private void DrawTacticalPingHud(CombatHudLayout layout)
        {
            if (InMatchSettingsMenu.IsOpen || (front != null && front.HasEnded)) return;
            string[] labels = { "⚔ " + GameLanguage.Text("ATTACK") + " [1]", "🪐 " + GameLanguage.Text("RALLY") + " [2]", "🛡 " + GameLanguage.Text("DEFEND") + " [3]" };
            Color[] colors = {
                new Color(1f, 0.42f, 0.38f, 0.92f),
                new Color(1f, 0.82f, 0.28f, 0.92f),
                new Color(0.28f, 0.86f, 1f, 0.92f)
            };
            for (int i = 0; i < 3; i++)
            {
                Rect r = GetPingButtonRect(layout, i);
                theme.HudPanel(r, 0.74f);
                FrontEndTheme.Fill(new Rect(r.x, r.y, 3f, r.height), colors[i]);
                turretBtnSubStyle.normal.textColor = Color.Lerp(colors[i], Color.white, 0.42f);
                theme.FitLabel(new Rect(r.x + 6f, r.y + 3f, r.width - 8f, r.height - 6f), labels[i], turretBtnSubStyle);
            }
        }

        private void DrawWorldPingAndTurretPreview()
        {
            float scale = Mathf.Clamp(Screen.height / 900f, 0.75f, 1.25f);
            float now = Time.unscaledTime;

            // Active Tactical Ping Marker in world space
            if (now < activePingUntil && !string.IsNullOrEmpty(activePingLabel))
            {
                Vector3 sp = cam.WorldToScreenPoint(activePingWorldPos + Vector3.up * 0.35f);
                if (sp.z > 0f)
                {
                    Vector2 center = new Vector2(sp.x, Screen.height - sp.y);
                    float wave = Mathf.Repeat(now * 2.2f, 1f);
                    float r = Mathf.Lerp(16f, 44f, wave) * scale;
                    Color ringCol = activePingColor;
                    ringCol.a = (1f - wave) * 0.85f;
                    CombatGlyphs.Ring(center, r, 2.2f * scale, ringCol, 28);
                    CombatGlyphs.Ring(center, 14f * scale, 2.0f * scale, activePingColor, 24);
                    bannerSubStyle.normal.textColor = activePingColor;
                    GUI.Label(new Rect(center.x - 90f, center.y - 32f * scale, 180f, 20f), activePingLabel, bannerSubStyle);
                }
            }

            // Live Turret Range & Target Reticle when in placement mode
            if (placingTurret && localTurretCharges > 0)
            {
                GameObject localHero = LocalHeroContext.Find();
                if (localHero != null)
                {
                    Vector2 pointer = Mouse.current != null
                        ? Mouse.current.position.ReadValue()
                        : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    if (ETouch.activeTouches.Count > 0)
                        pointer = ETouch.activeTouches[0].screenPosition;

                    if (TryScreenToArenaGround(pointer, localHero.transform.position, out Vector3 groundPt))
                    {
                        Vector3 centerScreen = cam.WorldToScreenPoint(groundPt + Vector3.up * 0.1f);
                        Vector3 edgeScreen = cam.WorldToScreenPoint(groundPt + cam.transform.right * KillstreakBoltTurret.AttackRange + Vector3.up * 0.1f);
                        if (centerScreen.z > 0f && edgeScreen.z > 0f)
                        {
                            Vector2 c = new Vector2(centerScreen.x, Screen.height - centerScreen.y);
                            float pixelRadius = Mathf.Clamp(Vector2.Distance(c, new Vector2(edgeScreen.x, Screen.height - edgeScreen.y)), 28f, Screen.width * 0.45f);
                            Color rangeCol = new Color(0.22f, 0.90f, 1f, 0.65f);
                            CombatGlyphs.Ring(c, pixelRadius, 1.8f * scale, rangeCol, 48);
                            CombatGlyphs.Ring(c, 12f * scale, 2.0f * scale, new Color(1f, 0.82f, 0.25f, 0.92f), 20);
                        }
                    }
                }
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
        public static IReadOnlyList<KillstreakBoltTurret> Active=>active;
        public static event System.Action<KillstreakBoltTurret,Vector3> NetworkShot;
        private static readonly Dictionary<PrimitiveType, Mesh> sharedMeshes = new();
        private static Material armorMat, darkMetalMat, boltGoldMat;
        private static readonly Material[] teamGlowMats = new Material[2];
        private static Transform cachedMatchWorld;
        private static CoopMatchPlan serialPlan;
        private static int networkSerial;

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
        private bool mobile;
        private readonly Transform[] wheels=new Transform[4];
        private float nextSupportScan;
        private Health supportLeader;
        private Vector3 supportAnchor;

        public GameObject OwnerHero => ownerHero;
        public int Team => team;
        public Health TurretHealth=>health;
        public bool IsMobile=>mobile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            active.Clear();
            NetworkShot=null;
            sharedMeshes.Clear();
            armorMat = darkMetalMat = boltGoldMat = null;
            teamGlowMats[0] = teamGlowMats[1] = null;
            cachedMatchWorld = null;
            serialPlan=null;networkSerial=0;
        }

        public static KillstreakBoltTurret Spawn(int teamId, Vector3 position, GameObject owner,bool mobile=false,string networkKey=null)
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
            if(mobile)go.transform.localScale=Vector3.one*2f;

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.42f;
            col.height = 1.25f;
            col.center = new Vector3(0f, 0.62f, 0f);

            Health hp = go.AddComponent<Health>();
            hp.Configure(MaxHp, true);

            TeamMember tm = go.AddComponent<TeamMember>();
            tm.Initialize(clampedTeam);

            KillstreakBoltTurret turret = go.AddComponent<KillstreakBoltTurret>();
            turret.Initialize(clampedTeam, owner,mobile);
            if(CoopAuthority.Active)
            {
                if(string.IsNullOrEmpty(networkKey)&&CoopAuthority.LocalHasServerAuthority)
                {
                    if(!ReferenceEquals(serialPlan,CoopAuthority.ActivePlan)){serialPlan=CoopAuthority.ActivePlan;networkSerial=0;}
                    networkKey=$"KST:{clampedTeam}:{(mobile?1:0)}:{++networkSerial}";
                }
                if(!string.IsNullOrEmpty(networkKey))
                    (go.GetComponent<CoopWorldEntityKey>()??go.AddComponent<CoopWorldEntityKey>()).Configure(networkKey);
            }

            Color teamColor = clampedTeam == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            PrototypeVfx.Ring(position, teamColor, 1.35f, 0.32f);
            PrototypeVfx.Burst(position + Vector3.up * 0.45f, teamColor, 0.55f, 6);
            return turret;
        }

        public static KillstreakBoltTurret EnsureNetworkReplica(string key,Vector3 position)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||string.IsNullOrEmpty(key))return null;
            string[] parts=key.Split(':');
            if(parts.Length!=4||parts[0]!="KST"||!int.TryParse(parts[1],out int teamId)||teamId<0||teamId>1||
                !int.TryParse(parts[2],out int type)||(type!=0&&type!=1)||!int.TryParse(parts[3],out int serial)||serial<=0)return null;
            for(int i=0;i<active.Count;i++)
                if(active[i]!=null&&active[i].GetComponent<CoopWorldEntityKey>()?.Key==key)return active[i];
            return Spawn(teamId,position,null,type==1,key);
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

        private void Initialize(int assignedTeam, GameObject owner,bool mobileTurret)
        {
            team = assignedTeam;
            ownerHero = owner;
            mobile=mobileTurret;
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

            if (currentTarget == null)
            {
                if(mobile)AdvanceWithTeam();
                return;
            }

            Vector3 targetDelta=currentTarget.transform.position-transform.position;targetDelta.y=0f;
            if(mobile&&targetDelta.sqrMagnitude>AttackRange*AttackRange)
            {
                MoveToward(currentTarget.transform.position,AttackRange*.8f);
                return;
            }

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
            if(CoopAuthority.Active&&CoopAuthority.LocalHasServerAuthority)NetworkShot?.Invoke(this,hitPoint);

            // Credit the owner hero on a lethal blow for scoreboard/killstreak tracking,
            // while passing the turret GameObject on non-lethal hits so damage stays strictly 1.0.
            float damage=AttackDamage*(mobile?2f:1f);
            GameObject damageSource = (victim.Current + victim.Shield <= damage + 0.05f && ownerHero != null)
                ? ownerHero
                : gameObject;
            victim.TakeDamage(damage, damageSource);
        }

        public void PresentNetworkShot(Vector3 hitPoint)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||health==null||health.IsDead)return;
            AimAt(hitPoint);recoilOffset=.045f;
            if(barrelCluster!=null){barrelSpinAngle=Mathf.Repeat(barrelSpinAngle+75f,360f);barrelCluster.localRotation=Quaternion.Euler(0f,0f,barrelSpinAngle);}
            GameObject origin=muzzleActor!=null?muzzleActor:gameObject;
            Bolt7Vfx.Basic(origin,hitPoint,.06f);Bolt7Vfx.BasicImpact(origin,hitPoint);
        }

        private bool IsValidTarget(Health candidate)
        {
            if (candidate == null || candidate == health || candidate.IsDead || !candidate.isActiveAndEnabled)
                return false;
            if (CombatVisibility.HiddenFrom(gameObject, candidate))
                return false;
            Vector3 d = candidate.transform.position - transform.position;
            d.y = 0f;
            float range=mobile?AttackRange+7f:AttackRange;
            if (d.sqrMagnitude > range * range)
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
            float range=mobile?AttackRange+7f:AttackRange;
            float bestHeroSqr = range * range;
            float bestOtherSqr = range * range;
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

        private void AdvanceWithTeam()
        {
            if(Time.time>=nextSupportScan)
            {
                nextSupportScan=Time.time+.42f;
                if(!TeamSupportGroup.TryGetMonsterAnchor(team,transform.position,out supportLeader,out supportAnchor))supportLeader=null;
            }
            if(supportLeader!=null&&!supportLeader.IsDead)
                MoveToward(supportAnchor,3f);
            else
                MoveToward(new Vector3(team==0?ArenaLayout.MovementHalfWidth-3f:-ArenaLayout.MovementHalfWidth+3f,0f,0f),3f);
        }

        private void MoveToward(Vector3 destination,float stopDistance)
        {
            Vector3 delta=destination-transform.position;delta.y=0f;
            if(delta.sqrMagnitude<=stopDistance*stopDistance)return;
            Vector3 next=Vector3.MoveTowards(transform.position,transform.position+delta,1.8f*Time.deltaTime);
            next=GameplayCollision.ClampToRoundedArena(next,ArenaLayout.MovementHalfWidth-1.5f,ArenaLayout.MovementHalfHeight-1.5f,1f);
            next.y=LunarSurfaceField.WorldSurface(next.x,next.z).y;
            if(Physics.CheckSphere(next+Vector3.up*.7f,.7f,GameplayCollision.SightMask,QueryTriggerInteraction.Ignore))return;
            transform.position=next;
            if(delta.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta,Vector3.up),180f*Time.deltaTime);
            for(int i=0;i<wheels.Length;i++)if(wheels[i]!=null)wheels[i].Rotate(Vector3.up,220f*Time.deltaTime,Space.Self);
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

            if(mobile)
            {
                Part("MobileChassis",PrimitiveType.Cube,root,new Vector3(0f,.13f,0f),new Vector3(.88f,.16f,1.1f),darkMetalMat,Quaternion.identity,true);
                for(int i=0;i<4;i++)
                {
                    float side=i%2==0?-.48f:.48f,front=i<2?-.38f:.38f;
                    wheels[i]=Part("Wheel_"+i,PrimitiveType.Cylinder,root,new Vector3(side,.15f,front),
                        new Vector3(.30f,.10f,.30f),darkMetalMat,Quaternion.Euler(0f,0f,90f),false);
                }
            }

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

        private static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion localRot, bool castShadows)
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
            return go.transform;
        }
    }
}
