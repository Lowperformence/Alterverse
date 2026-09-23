using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Universal match overlay for local and co-op play. Local matches pause while it is open;
    /// co-op keeps simulating so one client can never pause the authoritative match for everyone.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class InMatchSettingsMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private FrontEndTheme theme;
        private PrototypeHUD combatHud;
        private bool combatHudWasEnabled;
        private bool ownsPause;
        private bool confirmLeave;
        private bool leaving;
        private bool audioDirty;
        private float master, music, sfx;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (FindAnyObjectByType<InMatchSettingsMenu>() == null)
                new GameObject("DF_InMatchSettings").AddComponent<InMatchSettingsMenu>();
        }

        private void Awake()
        {
            master = PlayerProfile.MasterVolume;
            music = PlayerProfile.MusicVolume;
            sfx = PlayerProfile.SfxVolume;
        }

        private void Update()
        {
            if (!IsOpen) return;
            DimensionsFront front = FindAnyObjectByType<DimensionsFront>();
            if (front == null || front.HasEnded || MainMenuController.IsVisible)
                CloseOverlay();
        }

        private void OnDisable()
        {
            if (IsOpen) CloseOverlay();
        }

        private void OnDestroy()
        {
            if (IsOpen) CloseOverlay();
        }

        private void OpenOverlay()
        {
            if (IsOpen || leaving) return;

            master = PlayerProfile.MasterVolume;
            music = PlayerProfile.MusicVolume;
            sfx = PlayerProfile.SfxVolume;
            audioDirty = false;
            confirmLeave = false;
            IsOpen = true;

            combatHud = FindAnyObjectByType<PrototypeHUD>();
            combatHudWasEnabled = combatHud != null && combatHud.enabled;
            if (combatHudWasEnabled) combatHud.enabled = false;

            if (!CoopAuthority.Active)
            {
                ownsPause = true;
                MatchClock.Set(PauseReason.Settings, true);
            }
        }

        private void CloseOverlay()
        {
            FlushAudio();
            confirmLeave = false;
            IsOpen = false;

            if (ownsPause)
            {
                MatchClock.Set(PauseReason.Settings, false);
                ownsPause = false;
            }

            if (combatHud != null && combatHudWasEnabled)
                combatHud.enabled = true;
            combatHud = null;
            combatHudWasEnabled = false;
        }

        private void FlushAudio()
        {
            if (!audioDirty) return;
            PlayerProfile.SetAudioVolumes(master, music, sfx, true);
            audioDirty = false;
        }

        private void ApplyAudioLive()
        {
            PlayerProfile.SetAudioVolumes(master, music, sfx, false);
            audioDirty = true;
        }

        private async void LeaveMatchNow()
        {
            if (leaving) return;
            leaving = true;

            bool coop = CoopAuthority.Active;
            CoopLobbyService lobby = coop ? CoopLobbyService.Instance : null;

            FlushAudio();
            CloseOverlay();

            GameObject world = GameObject.Find("DF_MatchWorld");
            if (world != null) world.SetActive(false);
            MatchClock.Set(PauseReason.Result, false);
            MatchClock.Set(PauseReason.Settings, false);

            if (!coop)
            {
                if (world != null) Destroy(world);
                MainMenuController.ShowHome();
                leaving = false;
                return;
            }

            // A running co-op match is shared state. Leaving the session is the safe universal
            // behavior: guests cannot force the host back to lobby, and a host leaving closes the
            // active room so every other participant receives the existing disconnect/abort flow.
            if (lobby != null) await lobby.LeaveLobbyAsync();
            CoopAuthority.EndMatch();
            if (world != null) Destroy(world);
            MainMenuController.ShowHome();
            leaving = false;
        }

        private void OnGUI()
        {
            if (MainMenuController.IsVisible || leaving) return;

            DimensionsFront front = FindAnyObjectByType<DimensionsFront>();
            if (front == null || front.HasEnded) return;
            if (!IsOpen && Time.timeScale <= 0f) return;

            theme ??= new FrontEndTheme();
            CombatHudLayout layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width <= 0 || layout.Safe.height <= 0) return;

            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            int depth = GUI.depth;
            try
            {
                GUI.depth = -5000;
                GUI.matrix = layout.Matrix;

                if (!IsOpen)
                {
                    if (theme.Action(new Rect(layout.Width - 166, 16, 150, 46), "SETTINGS"))
                        OpenOverlay();
                    return;
                }

                DrawOverlay(layout);
            }
            finally
            {
                GUI.depth = depth;
                GUI.matrix = matrix;
                GUI.color = color;
            }
        }

        private void DrawOverlay(CombatHudLayout layout)
        {
            FrontEndTheme.Fill(new Rect(0, 0, layout.Width, layout.Height), new Color(.008f, .012f, .02f, .82f));

            float width = Mathf.Min(700f, layout.Width - 36f);
            float height = Mathf.Min(486f, layout.Height - 28f);
            Rect panel = new Rect((layout.Width - width) * .5f, (layout.Height - height) * .5f, width, height);
            theme.Panel(panel);
            FrontEndTheme.Fill(new Rect(panel.x, panel.y, panel.width, 2), FrontEndTheme.Accent);

            theme.Label(new Rect(panel.x + 24, panel.y + 16, panel.width - 48, 38), "MATCH SETTINGS", theme.Heading);
            string state = CoopAuthority.Active
                ? "CO-OP MATCH CONTINUES WHILE SETTINGS ARE OPEN"
                : "MATCH PAUSED";
            theme.Label(new Rect(panel.x + 24, panel.y + 54, panel.width - 48, 24), state, theme.Small, true);

            float previousMaster = master, previousMusic = music, previousSfx = sfx;
            DrawVolume(new Rect(panel.x + 24, panel.y + 88, panel.width - 48, 78), "Master volume", ref master);
            DrawVolume(new Rect(panel.x + 24, panel.y + 174, panel.width - 48, 78), "Music", ref music);
            DrawVolume(new Rect(panel.x + 24, panel.y + 260, panel.width - 48, 78), "Sound effects", ref sfx);
            if (previousMaster != master || previousMusic != music || previousSfx != sfx)
                ApplyAudioLive();

            if (!confirmLeave)
            {
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 122, panel.width - 48, 48), "RESUME MATCH", true))
                    CloseOverlay();

                string destination = CoopAuthority.Active
                    ? "LEAVE CO-OP  •  MAIN MENU"
                    : "LEAVE MATCH  •  MAIN MENU";
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 64, panel.width - 48, 40), destination))
                    confirmLeave = true;
            }
            else
            {
                theme.Label(new Rect(panel.x + 24, panel.yMax - 138, panel.width - 48, 34), "LEAVE THE CURRENT MATCH?", theme.Heading);
                string warning = CoopAuthority.Active
                    ? "You leave the current co-op session. The match reward is not granted."
                    : "The current match ends without a match reward.";
                theme.Label(new Rect(panel.x + 24, panel.yMax - 103, panel.width - 48, 28), warning, theme.Small, true);
                float gap = 12f, buttonWidth = (panel.width - 48 - gap) * .5f;
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 64, buttonWidth, 40), "CANCEL"))
                    confirmLeave = false;
                if (theme.Action(new Rect(panel.x + 24 + buttonWidth + gap, panel.yMax - 64, buttonWidth, 40), "CONFIRM LEAVE", true))
                    LeaveMatchNow();
            }
        }

        private void DrawVolume(Rect r, string label, ref float value)
        {
            theme.Label(new Rect(r.x, r.y, r.width - 90, 28), label, theme.Body);
            theme.Label(new Rect(r.xMax - 82, r.y, 82, 28), $"{Mathf.RoundToInt(value * 100)}%", theme.Small);

            if (theme.Action(new Rect(r.x, r.y + 35, 48, 36), "−"))
                value = Mathf.Clamp01(value - .1f);

            Rect track = new Rect(r.x + 64, r.y + 48, r.width - 128, 10);
            FrontEndTheme.Fill(track, FrontEndTheme.Raised);
            FrontEndTheme.Fill(new Rect(track.x, track.y, track.width * value, track.height), FrontEndTheme.Accent);

            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && track.Contains(e.mousePosition))
            {
                value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                e.Use();
            }

            FrontEndTheme.Fill(new Rect(track.x + track.width * value - 5, track.center.y - 10, 10, 20), FrontEndTheme.Text);

            if (theme.Action(new Rect(r.xMax - 48, r.y + 35, 48, 36), "+"))
                value = Mathf.Clamp01(value + .1f);
        }
    }
}
