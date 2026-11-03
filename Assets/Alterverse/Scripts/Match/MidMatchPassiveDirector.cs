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
        private GUIStyle headerStyle, cardTitleStyle, cardBodyStyle, badgeStyle;
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
            headerStyle ??= new GUIStyle(theme.Small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            cardTitleStyle ??= new GUIStyle(theme.Body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15 };
            cardBodyStyle ??= new GUIStyle(theme.Small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            badgeStyle ??= new GUIStyle(theme.Small) { fontSize = 12, fontStyle = FontStyle.Bold };

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            try
            {
                GUI.matrix = layout.Matrix;

                if (receiver.HasSelectedMidMatchPerk)
                {
                    Rect activeBadge = new Rect(16f, 214f, 218f, 28f);
                    theme.HudPanel(activeBadge, .78f);
                    FrontEndTheme.Fill(new Rect(activeBadge.x, activeBadge.y, 3f, activeBadge.height), FrontEndTheme.Positive);
                    theme.Label(new Rect(activeBadge.x + 10f, activeBadge.y + 4f, activeBadge.width - 16f, 20f),
                        GameLanguage.Text(receiver.MidMatchPerkLabel), badgeStyle);
                    return;
                }

                float panelWidth = Mathf.Min(560f, layout.Width - 32f);
                Rect container = new Rect((layout.Width - panelWidth) * .5f, 104f, panelWidth, 118f);
                theme.Panel(container);
                DimensionUiMotif.DrawCornerCuts(container, FrontEndTheme.Pixel, FrontEndTheme.Zero, FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(container.x, container.y, container.width, 2f), FrontEndTheme.Accent);

                int remainingSec = Mathf.CeilToInt(Mathf.Max(0f, AutoPickWindowSeconds - (Time.unscaledTime - unlockedAtUnscaledTime)));
                theme.Label(
                    new Rect(container.x + 12f, container.y + 6f, container.width - 24f, 20f),
                    GameLanguage.Text($"MID-MATCH PASSIVE UNLOCKED • CHOOSE 1 ({remainingSec}s)"),
                    headerStyle);

                HeroIdentity identity = localHero.GetComponent<HeroIdentity>();
                MidMatchPerkType recommended = RecommendForRole(identity != null ? identity.Role : HeroRole.Bruiser);

                float gap = 10f;
                float cardW = (container.width - 24f - gap * 2f) / 3f;
                float cardY = container.y + 30f;
                float cardH = 78f;

                DrawPerkCard(
                    new Rect(container.x + 12f, cardY, cardW, cardH),
                    "+15% MOVE",
                    "IMPULS-SCHRITT",
                    recommended == MidMatchPerkType.Movement,
                    FrontEndTheme.Positive,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Movement));

                DrawPerkCard(
                    new Rect(container.x + 12f + cardW + gap, cardY, cardW, cardH),
                    "+15% DAMAGE",
                    "RISS-ÜBERLADUNG",
                    recommended == MidMatchPerkType.Damage,
                    FrontEndTheme.Accent,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.Damage));

                DrawPerkCard(
                    new Rect(container.x + 12f + (cardW + gap) * 2f, cardY, cardW, cardH),
                    "+15% MAX HP",
                    "KERN-VITALITÄT",
                    recommended == MidMatchPerkType.MaxHealth,
                    FrontEndTheme.Zero,
                    () => receiver.SelectMidMatchPerk(MidMatchPerkType.MaxHealth));
            }
            finally
            {
                GUI.matrix = prevMatrix;
                GUI.color = prevColor;
            }
        }

        private void DrawPerkCard(Rect rect, string statHeadline, string subtitle, bool recommended, Color accentColor, System.Action onSelect)
        {
            string label = recommended ? $"{statHeadline} ★" : statHeadline;
            if (theme.Action(rect, string.Empty, primary: recommended, selected: recommended, accent: accentColor))
                onSelect?.Invoke();

            theme.Label(new Rect(rect.x + 6f, rect.y + 12f, rect.width - 12f, 26f), label, cardTitleStyle);
            theme.Label(new Rect(rect.x + 6f, rect.y + 42f, rect.width - 12f, 22f), subtitle, cardBodyStyle, true);
        }
    }
}
