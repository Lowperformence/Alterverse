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

                // Compact, semi-transparent top HUD bar so mid-match combat in the center of the screen is never blocked.
                float panelWidth = Mathf.Min(648f, layout.Width - 24f);
                Rect container = new Rect((layout.Width - panelWidth) * .5f, 42f, panelWidth, 144f);

                theme.HudPanel(container, .36f);
                FrontEndTheme.Fill(new Rect(container.x + 2f, container.y + 2f, container.width - 4f, container.height - 4f),
                    new Color(.008f, .016f, .030f, .38f));
                DimensionUiMotif.DrawCornerCuts(container, FrontEndTheme.Pixel, FrontEndTheme.Zero, FrontEndTheme.Accent);

                float elapsedSinceUnlock = Mathf.Max(0f, Time.unscaledTime - unlockedAtUnscaledTime);
                float remainingRatio = Mathf.Min(1f, Mathf.Max(0f, (AutoPickWindowSeconds - elapsedSinceUnlock) / AutoPickWindowSeconds));
                int remainingSec = Mathf.CeilToInt(Mathf.Max(0f, AutoPickWindowSeconds - elapsedSinceUnlock));

                // Slim top progress rail
                FrontEndTheme.Fill(new Rect(container.x, container.y, container.width, 3f), new Color(.06f, .11f, .18f, .55f));
                FrontEndTheme.Fill(new Rect(container.x, container.y, container.width * remainingRatio, 3f), FrontEndTheme.Accent);

                theme.FitLabel(new Rect(container.x + 14f, container.y + 5f, 290f, 24f),
                    GameLanguage.Text("MID-MATCH AUGMENT READY"), headerStyle);
                theme.Label(new Rect(container.x + 310f, container.y + 7f, 230f, 20f),
                    GameLanguage.Text("CHOOSE ONE BONUS"), instructionStyle);

                Rect timerPill = new Rect(container.xMax - 76f, container.y + 5f, 62f, 22f);
                FrontEndTheme.Fill(timerPill, new Color(.02f, .05f, .09f, .52f));
                FrontEndTheme.Fill(new Rect(timerPill.x, timerPill.y, 2f, timerPill.height), FrontEndTheme.Accent);
                theme.FitLabel(new Rect(timerPill.x + 8f, timerPill.y + 1f, timerPill.width - 12f, 20f), remainingSec + " s", headerStyle);

                HeroIdentity identity = localHero.GetComponent<HeroIdentity>();
                MidMatchPerkType recommended = RecommendForRole(identity != null ? identity.Role : HeroRole.Bruiser);

                float gap = 10f;
                float cardW = (panelWidth - 24f - gap * 2f) / 3f;
                float cardY = container.y + 34f;
                float cardH = 102f;

                DrawPerkCard(new Rect(container.x + 12f, cardY, cardW, cardH), "MOVEMENT SPEED",
                    0, "[1]", recommended == MidMatchPerkType.Movement, FrontEndTheme.Positive,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Movement));

                DrawPerkCard(new Rect(container.x + 12f + cardW + gap, cardY, cardW, cardH), "OUTGOING DAMAGE",
                    1, "[2]", recommended == MidMatchPerkType.Damage, FrontEndTheme.Accent,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Damage));

                DrawPerkCard(new Rect(container.x + 12f + (cardW + gap) * 2f, cardY, cardW, cardH), "MAX HEALTH",
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
            Color before = GUI.color;
            GUI.color = new Color(before.r, before.g, before.b, before.a * .62f);
            bool clicked = theme.Action(rect, string.Empty, selected: recommended);
            GUI.color = before;
            if (clicked) onSelect?.Invoke();

            float pulse = .85f + .15f * (float)System.Math.Sin(Time.unscaledTime * 3.6f + iconKind * 1.1f);
            Color vividAccent = new Color(accentColor.r, accentColor.g, accentColor.b, .92f);
            Color softGlow = new Color(accentColor.r, accentColor.g, accentColor.b, recommended ? .14f * pulse : .06f);
            Color cardBg = recommended
                ? new Color(.020f + accentColor.r * .04f, .034f + accentColor.g * .04f, .054f + accentColor.b * .04f, .44f)
                : new Color(.012f, .022f, .038f, .38f);

            // Translucent glass surface so battlefield remains visible behind cards
            FrontEndTheme.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), cardBg);
            FrontEndTheme.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 26f), softGlow);

            // Top neon energy bar + subtle corner cuts
            FrontEndTheme.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, recommended ? 3f : 2f), vividAccent);
            DimensionUiMotif.DrawCornerCuts(rect, FrontEndTheme.Pixel, vividAccent, vividAccent);

            // Keybind chip in top-left
            Rect keyRect = new Rect(rect.x + 8f, rect.y + 7f, 28f, 18f);
            FrontEndTheme.Fill(keyRect, new Color(.008f, .014f, .024f, .52f));
            theme.Label(keyRect, keyHint, recommendStyle, true);

            // Procedural geometric perk emblem in top-right
            DrawPerkEmblem(new Rect(rect.xMax - 28f, rect.y + 7f, 20f, 20f), iconKind, vividAccent);

            // High-contrast value (+15%) and stat title
            theme.Label(new Rect(rect.x + 8f, rect.y + 18f, rect.width - 16f, 32f), "+15%", valueStyle);
            theme.Label(new Rect(rect.x + 8f, rect.y + 49f, rect.width - 16f, 28f), GameLanguage.Text(label), cardLabelStyle);

            // Slim bottom badge pill
            Rect footerPill = new Rect(rect.x + 8f, rect.yMax - 22f, rect.width - 16f, 17f);
            FrontEndTheme.Fill(footerPill, recommended
                ? new Color(accentColor.r * .32f, accentColor.g * .32f, accentColor.b * .32f, .56f)
                : new Color(.020f, .034f, .056f, .44f));
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
