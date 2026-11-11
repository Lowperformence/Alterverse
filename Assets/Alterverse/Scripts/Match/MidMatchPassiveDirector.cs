using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Alterverse.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Match
{
    /// <summary>
    /// Unlocks a one-time Mid-Match Passive Perk choice at 50% match progress
    /// (+15% Movement Speed, +15% Outgoing Damage, or +15% Max HP).
    /// Auto-bootstraps into every match scene without manual scene/prefab wiring.
    /// </summary>
    [DefaultExecutionOrder(450)]
    public sealed class MidMatchPassiveDirector : MonoBehaviour
    {
        public const float TriggerProgressRatio = 0.50f;
        public const float AutoPickWindowSeconds = 20f;

        private static MidMatchPassiveDirector instance;
        private DimensionsFront front;
        private FrontEndTheme theme;
        private Font readableFont;
        private GUIStyle headerStyle, instructionStyle, valueStyle, cardLabelStyle, recommendStyle, badgeStyle;
        private float nextScanTime;
        private bool midMatchTriggered;
        private float unlockedAtUnscaledTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (FindAnyObjectByType<MidMatchPassiveDirector>() == null)
            {
                GameObject go = new GameObject("DF_MidMatchPassiveDirector");
                DontDestroyOnLoad(go);
                go.AddComponent<MidMatchPassiveDirector>();
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextScanTime)
            {
                nextScanTime = Time.unscaledTime + 0.25f;
                if (front == null)
                    front = FindAnyObjectByType<DimensionsFront>();
            }

            if (front == null || front.HasEnded || MainMenuController.IsVisible)
            {
                if (front != null && front.HasEnded)
                    midMatchTriggered = false;
                return;
            }

            // Reset state when a new match starts near 0 elapsed seconds
            if (midMatchTriggered && front.ElapsedSeconds < 3f && !front.IsOvertime)
            {
                midMatchTriggered = false;
                ResetAllHeroPerks();
            }

            float totalDuration = front.ElapsedSeconds + front.TimeRemaining;
            bool reachedMidpoint = front.IsOvertime ||
                (totalDuration > 10f && front.ElapsedSeconds >= totalDuration * TriggerProgressRatio);

            if (!reachedMidpoint) return;

            if (!midMatchTriggered)
            {
                midMatchTriggered = true;
                unlockedAtUnscaledTime = Time.unscaledTime;
                AssignPerksToBots();
            }

            GameObject localHero = LocalHeroContext.Find();
            if (localHero == null) return;

            PlanetBuffReceiver receiver = localHero.GetComponent<PlanetBuffReceiver>()
                ?? localHero.AddComponent<PlanetBuffReceiver>();

            if (!receiver.HasSelectedMidMatchPerk)
            {
                HandleKeyboardShortcuts(receiver);

                if (Time.unscaledTime - unlockedAtUnscaledTime >= AutoPickWindowSeconds)
                {
                    HeroIdentity id = localHero.GetComponent<HeroIdentity>();
                    HeroRole role = id != null ? id.Role : HeroRole.Skirmisher;
                    receiver.SelectMidMatchPerk(RecommendForRole(role));
                }
            }
        }

        private static void HandleKeyboardShortcuts(PlanetBuffReceiver receiver)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) receiver.SelectMidMatchPerk(MidMatchPerkType.Movement);
            else if (kb.digit2Key.wasPressedThisFrame) receiver.SelectMidMatchPerk(MidMatchPerkType.Damage);
            else if (kb.digit3Key.wasPressedThisFrame) receiver.SelectMidMatchPerk(MidMatchPerkType.MaxHealth);
        }

        private static void AssignPerksToBots()
        {
            if (!CoopAuthority.CanSimulateHostGameplay) return;
            var activeHealth = Health.Active;
            for (int i = 0; i < activeHealth.Count; i++)
            {
                Health h = activeHealth[i];
                if (h == null || h.GetComponent<MobaBotBrain>() == null) continue;
                PlanetBuffReceiver buff = h.GetComponent<PlanetBuffReceiver>()
                    ?? h.gameObject.AddComponent<PlanetBuffReceiver>();
                if (buff.HasSelectedMidMatchPerk) continue;

                HeroIdentity identity = h.GetComponent<HeroIdentity>();
                HeroRole role = identity != null ? identity.Role : HeroRole.Bruiser;
                buff.SelectMidMatchPerk(RecommendForRole(role));
            }
        }

        private static void ResetAllHeroPerks()
        {
            var receivers = FindObjectsByType<PlanetBuffReceiver>(FindObjectsSortMode.None);
            for (int i = 0; i < receivers.Length; i++)
                if (receivers[i] != null)
                    receivers[i].ResetMidMatchPerk();
        }

        public static MidMatchPerkType RecommendForRole(HeroRole role) => role switch
        {
            HeroRole.Assassin => MidMatchPerkType.Movement,
            HeroRole.Skirmisher => MidMatchPerkType.Movement,
            HeroRole.Tank => MidMatchPerkType.MaxHealth,
            HeroRole.Support => MidMatchPerkType.MaxHealth,
            _ => MidMatchPerkType.Damage
        };

        private void OnGUI()
        {
            if (!midMatchTriggered || front == null || front.HasEnded || MainMenuController.IsVisible || InMatchSettingsMenu.IsOpen)
                return;

            GameObject localHero = LocalHeroContext.Find();
            if (localHero == null) return;

            PlanetBuffReceiver receiver = localHero.GetComponent<PlanetBuffReceiver>();
            if (receiver == null) return;

            CombatHudLayout layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width <= 0 || layout.Safe.height <= 0) return;

            theme ??= new FrontEndTheme();
            // A plain sans-serif face stays legible at mobile HUD sizes.
            readableFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? FrontEndTypography.Body;
            headerStyle ??= new GUIStyle(theme.Body) { font=readableFont, alignment=TextAnchor.MiddleLeft, fontStyle=FontStyle.Bold, fontSize=22, wordWrap=false };
            instructionStyle ??= new GUIStyle(theme.Body) { font=readableFont, alignment=TextAnchor.MiddleLeft, fontSize=16, wordWrap=false };
            valueStyle ??= new GUIStyle(theme.Body) { font=readableFont, alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold, fontSize=30, wordWrap=false };
            cardLabelStyle ??= new GUIStyle(theme.Body) { font=readableFont, alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold, fontSize=18, wordWrap=true };
            recommendStyle ??= new GUIStyle(theme.Small) { font=readableFont, alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold, fontSize=14, wordWrap=false };
            badgeStyle ??= new GUIStyle(theme.Body) { font=readableFont, fontSize=16, fontStyle=FontStyle.Bold, wordWrap=false };

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            try
            {
                GUI.matrix = layout.Matrix;

                if (receiver.HasSelectedMidMatchPerk)
                {
                    Rect activeBadge = new Rect(16f, 214f, 254f, 36f);
                    theme.HudPanel(activeBadge, .94f);
                    FrontEndTheme.Fill(new Rect(activeBadge.x, activeBadge.y, 4f, activeBadge.height), FrontEndTheme.Positive);
                    string activeLabel = receiver.SelectedPerk == MidMatchPerkType.Movement ? "MOVEMENT SPEED" :
                        receiver.SelectedPerk == MidMatchPerkType.Damage ? "OUTGOING DAMAGE" : "MAX HEALTH";
                    theme.FitLabel(new Rect(activeBadge.x + 12f, activeBadge.y + 5f, activeBadge.width - 24f, 26f),
                        "+15%  " + GameLanguage.Text(activeLabel), badgeStyle);
                    return;
                }

                float panelWidth = Mathf.Min(856f, layout.Width - 32f);
                Rect container = new Rect((layout.Width - panelWidth) * .5f, 82f, panelWidth, 256f);

                // Outer shadow & obsidian-glass tactical shell
                FrontEndTheme.Fill(new Rect(container.x - 4f, container.y - 4f, container.width + 8f, container.height + 8f),
                    new Color(.004f, .008f, .016f, .72f));
                theme.Panel(container);
                FrontEndTheme.Fill(new Rect(container.x + 2f, container.y + 2f, container.width - 4f, container.height - 4f),
                    new Color(.010f, .018f, .032f, .96f));
                FrontEndTheme.Fill(new Rect(container.x + 2f, container.y + 2f, container.width - 4f, 64f),
                    new Color(.020f, .036f, .062f, .88f));
                DimensionUiMotif.DrawCornerCuts(container, FrontEndTheme.Pixel, FrontEndTheme.Zero, FrontEndTheme.Accent);

                float elapsedSinceUnlock = Mathf.Max(0f, Time.unscaledTime - unlockedAtUnscaledTime);
                float remainingRatio = Mathf.Min(1f, Mathf.Max(0f, (AutoPickWindowSeconds - elapsedSinceUnlock) / AutoPickWindowSeconds));
                int remainingSec = Mathf.CeilToInt(Mathf.Max(0f, AutoPickWindowSeconds - elapsedSinceUnlock));

                // Animated top progress rail
                FrontEndTheme.Fill(new Rect(container.x, container.y, container.width, 4f), new Color(.06f, .11f, .18f, .95f));
                FrontEndTheme.Fill(new Rect(container.x, container.y, container.width * remainingRatio, 4f), FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(container.x + 20f, container.y + 66f, container.width - 40f, 1f), new Color(1f, 1f, 1f, .09f));

                theme.FitLabel(new Rect(container.x + 22f, container.y + 10f, panelWidth - 130f, 28f),
                    GameLanguage.Text("MID-MATCH AUGMENT READY"), headerStyle);

                Rect timerPill = new Rect(container.xMax - 92f, container.y + 12f, 70f, 26f);
                FrontEndTheme.Fill(timerPill, new Color(.03f, .06f, .10f, .95f));
                FrontEndTheme.Fill(new Rect(timerPill.x, timerPill.y, 3f, timerPill.height), FrontEndTheme.Accent);
                theme.FitLabel(new Rect(timerPill.x + 10f, timerPill.y + 2f, timerPill.width - 14f, 22f), remainingSec + " s", headerStyle);

                theme.Label(new Rect(container.x + 22f, container.y + 38f, panelWidth - 44f, 24f),
                    GameLanguage.Text("CHOOSE ONE BONUS"), instructionStyle);

                HeroIdentity identity = localHero.GetComponent<HeroIdentity>();
                MidMatchPerkType recommended = RecommendForRole(identity != null ? identity.Role : HeroRole.Bruiser);

                float gap = 14f;
                float cardW = (panelWidth - 44f - gap * 2f) / 3f;
                float cardY = container.y + 78f;
                float cardH = 162f;

                DrawPerkCard(new Rect(container.x + 22f, cardY, cardW, cardH), "MOVEMENT SPEED",
                    0, "[1]", recommended == MidMatchPerkType.Movement, FrontEndTheme.Positive,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Movement));

                DrawPerkCard(new Rect(container.x + 22f + cardW + gap, cardY, cardW, cardH), "OUTGOING DAMAGE",
                    1, "[2]", recommended == MidMatchPerkType.Damage, FrontEndTheme.Accent,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Damage));

                DrawPerkCard(new Rect(container.x + 22f + (cardW + gap) * 2f, cardY, cardW, cardH), "MAX HEALTH",
                    2, "[3]", recommended == MidMatchPerkType.MaxHealth, FrontEndTheme.Zero,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.MaxHealth));
            }
            finally
            {
                GUI.matrix = prevMatrix;
                GUI.color = prevColor;
            }
        }

        private void DrawPerkCard(Rect rect, string label, int iconKind, string keyHint, bool recommended, Color accentColor, System.Action onSelect)
        {
            if (theme.Action(rect, string.Empty, selected: recommended)) onSelect?.Invoke();

            float pulse = .85f + .15f * (float)System.Math.Sin(Time.unscaledTime * 3.6f + iconKind * 1.1f);
            Color vividAccent = new Color(accentColor.r, accentColor.g, accentColor.b, 1f);
            Color softGlow = new Color(accentColor.r, accentColor.g, accentColor.b, recommended ? .22f * pulse : .10f);
            Color cardBg = recommended
                ? new Color(.022f + accentColor.r * .05f, .036f + accentColor.g * .05f, .058f + accentColor.b * .05f, .94f)
                : new Color(.015f, .025f, .042f, .92f);

            // Sleek multi-layer glass surface
            FrontEndTheme.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), cardBg);
            FrontEndTheme.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 44f), softGlow);

            // Top neon energy bar + side rails + corner cuts
            FrontEndTheme.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, recommended ? 4f : 3f), vividAccent);
            FrontEndTheme.Fill(new Rect(rect.x + 2f, rect.y + 6f, 2f, rect.height - 12f),
                new Color(accentColor.r, accentColor.g, accentColor.b, recommended ? .65f : .28f));
            FrontEndTheme.Fill(new Rect(rect.xMax - 4f, rect.y + 6f, 2f, rect.height - 12f),
                new Color(accentColor.r, accentColor.g, accentColor.b, recommended ? .65f : .28f));
            DimensionUiMotif.DrawCornerCuts(rect, FrontEndTheme.Pixel, vividAccent, vividAccent);

            // Keybind chip in top-left
            Rect keyRect = new Rect(rect.x + 10f, rect.y + 10f, 32f, 20f);
            FrontEndTheme.Fill(keyRect, new Color(.008f, .014f, .024f, .88f));
            FrontEndTheme.Fill(new Rect(keyRect.x, keyRect.yMax - 2f, keyRect.width, 2f), vividAccent);
            theme.Label(keyRect, keyHint, recommendStyle, true);

            // Procedural geometric perk emblem in top-right
            DrawPerkEmblem(new Rect(rect.xMax - 36f, rect.y + 10f, 24f, 24f), iconKind, vividAccent);

            // Glowing value showcase box (+15%)
            Rect valueBox = new Rect(rect.x + 14f, rect.y + 34f, rect.width - 28f, 40f);
            FrontEndTheme.Fill(valueBox, new Color(.008f, .015f, .028f, .78f));
            FrontEndTheme.Fill(new Rect(valueBox.x + 18f, valueBox.yMax - 2f, valueBox.width - 36f, 2f),
                new Color(accentColor.r, accentColor.g, accentColor.b, .48f));
            theme.Label(new Rect(rect.x + 8f, rect.y + 35f, rect.width - 16f, 38f), "+15%", valueStyle);

            // Crisp stat title
            theme.Label(new Rect(rect.x + 10f, rect.y + 78f, rect.width - 20f, 44f), GameLanguage.Text(label), cardLabelStyle);

            // Modern action pill footer at bottom of card
            Rect footerPill = new Rect(rect.x + 10f, rect.yMax - 32f, rect.width - 20f, 22f);
            FrontEndTheme.Fill(footerPill, recommended
                ? new Color(accentColor.r * .38f, accentColor.g * .38f, accentColor.b * .38f, .92f)
                : new Color(.028f, .044f, .070f, .88f));
            FrontEndTheme.Fill(new Rect(footerPill.x, footerPill.y, footerPill.width, 2f), vividAccent);
            if (recommended)
                theme.Label(footerPill, GameLanguage.Text("RECOMMENDED"), recommendStyle);
            else
                theme.Label(footerPill, GameLanguage.Text("SELECT BONUS"), recommendStyle, true);
        }

        private static void DrawPerkEmblem(Rect iconRect, int iconKind, Color accent)
        {
            FrontEndTheme.Fill(iconRect, new Color(.008f, .014f, .024f, .90f));
            FrontEndTheme.Fill(new Rect(iconRect.x, iconRect.y, iconRect.width, 2f), accent);
            float cx = iconRect.x + iconRect.width * .5f;
            float cy = iconRect.y + iconRect.height * .5f;
            if (iconKind == 0)
            {
                // Speed thruster bars
                FrontEndTheme.Fill(new Rect(cx - 7f, cy - 5f, 10f, 2f), accent);
                FrontEndTheme.Fill(new Rect(cx - 4f, cy - 1f, 11f, 2f), accent);
                FrontEndTheme.Fill(new Rect(cx - 7f, cy + 3f, 10f, 2f), accent);
            }
            else if (iconKind == 1)
            {
                // Damage reticle cross + hot core
                FrontEndTheme.Fill(new Rect(cx - 7f, cy - 1f, 14f, 2f), accent);
                FrontEndTheme.Fill(new Rect(cx - 1f, cy - 7f, 2f, 14f), accent);
                FrontEndTheme.Fill(new Rect(cx - 3f, cy - 3f, 6f, 6f), accent);
            }
            else
            {
                // Vitality medical cross
                FrontEndTheme.Fill(new Rect(cx - 6f, cy - 2f, 12f, 4f), accent);
                FrontEndTheme.Fill(new Rect(cx - 2f, cy - 6f, 4f, 12f), accent);
            }
        }
    }
}
