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

                float panelWidth = Mathf.Min(820f, layout.Width - 32f);
                Rect container = new Rect((layout.Width - panelWidth) * .5f, 92f, panelWidth, 230f);
                theme.Panel(container);
                FrontEndTheme.Fill(new Rect(container.x + 2f, container.y + 2f, container.width - 4f, container.height - 4f),
                    new Color(.012f, .020f, .035f, .96f));
                DimensionUiMotif.DrawCornerCuts(container, FrontEndTheme.Pixel, FrontEndTheme.Zero, FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(container.x, container.y, container.width, 3f), FrontEndTheme.Accent);

                int remainingSec = Mathf.CeilToInt(Mathf.Max(0f, AutoPickWindowSeconds - (Time.unscaledTime - unlockedAtUnscaledTime)));
                theme.FitLabel(new Rect(container.x + 20f, container.y + 10f, panelWidth - 110f, 30f),
                    "MID-MATCH AUGMENT READY", headerStyle);
                theme.FitLabel(new Rect(container.xMax - 80f, container.y + 10f, 60f, 30f), remainingSec + " s", headerStyle);
                theme.Label(new Rect(container.x + 20f, container.y + 43f, panelWidth - 40f, 25f),
                    "CHOOSE ONE BONUS", instructionStyle);

                HeroIdentity identity = localHero.GetComponent<HeroIdentity>();
                MidMatchPerkType recommended = RecommendForRole(identity != null ? identity.Role : HeroRole.Bruiser);
                float gap = 12f;
                float cardW = (panelWidth - 40f - gap * 2f) / 3f;
                float cardY = container.y + 76f;
                float cardH = 138f;
                DrawPerkCard(new Rect(container.x + 20f, cardY, cardW, cardH), "MOVEMENT SPEED",
                    recommended == MidMatchPerkType.Movement, FrontEndTheme.Positive,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Movement));
                DrawPerkCard(new Rect(container.x + 20f + cardW + gap, cardY, cardW, cardH), "OUTGOING DAMAGE",
                    recommended == MidMatchPerkType.Damage, FrontEndTheme.Accent,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Damage));
                DrawPerkCard(new Rect(container.x + 20f + (cardW + gap) * 2f, cardY, cardW, cardH), "MAX HEALTH",
                    recommended == MidMatchPerkType.MaxHealth, FrontEndTheme.Zero,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.MaxHealth));
            }
            finally
            {
                GUI.matrix = prevMatrix;
                GUI.color = prevColor;
            }
        }

        private void DrawPerkCard(Rect rect, string label, bool recommended, Color accentColor, System.Action onSelect)
        {
            if (theme.Action(rect, string.Empty, selected: recommended)) onSelect?.Invoke();
            FrontEndTheme.Fill(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 4f), accentColor);
            theme.Label(new Rect(rect.x + 8f, rect.y + 13f, rect.width - 16f, 38f), "+15%", valueStyle);
            theme.Label(new Rect(rect.x + 8f, rect.y + 54f, rect.width - 16f, 50f), label, cardLabelStyle);
            if (recommended)
                theme.Label(new Rect(rect.x + 8f, rect.yMax - 25f, rect.width - 16f, 20f), "RECOMMENDED", recommendStyle);
        }
    }
}
