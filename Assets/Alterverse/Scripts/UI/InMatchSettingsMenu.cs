using Alterverse.AI;
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
        private float master, music, sfx, voice;

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
            voice = PlayerProfile.VoiceChatVolume;
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
            voice = PlayerProfile.VoiceChatVolume;
            audioDirty = false;
            confirmLeave = false;
            IsOpen = true;

            combatHud = FindAnyObjectByType<PrototypeHUD>();
            combatHudWasEnabled = combatHud != null && combatHud.enabled;
            if (combatHudWasEnabled) combatHud.enabled = false;

            // Offline play can be paused locally. A co-op client must never pause the shared match.
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
            // Saving either preference group flushes all current PlayerPrefs, so persist once here.
            PlayerProfile.SetAudioVolumes(master, music, sfx, false);
            PlayerProfile.SetVoiceChatVolume(voice, true);
            audioDirty = false;
        }

        private void ApplyAudioLive()
        {
            PlayerProfile.SetAudioVolumes(master, music, sfx, false);
            PlayerProfile.SetVoiceChatVolume(voice, false);
            // This changes incoming Vivox playback only. It never changes microphone gain.
            CoopVoiceChatService.Instance?.RefreshOutputVolume();
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
            // Co-op difficulty is a match-scoped override. Clear it on an aborted/left match so
            // the next local match always starts from the player's selected difficulty.
            BotDifficultySettings.EndMatchOverride();

            if (!coop)
            {
                // Leaving an unfinished local match intentionally grants no result reward.
                if (world != null) Destroy(world);
                MainMenuController.ShowHome();
                leaving = false;
                return;
            }

            // A running co-op match is shared state. Leave the session through the existing lobby
            // service so Relay/NGO and Vivox can unwind through their normal disconnect paths.
            if (lobby != null)
                await lobby.LeaveLobbyAsync();
            else
                CoopNetworkBootstrap.ShutdownLocalNetwork();

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
                    Rect settingsButton = new Rect(layout.Width - 62, 16, 46, 46);
                    bool openSettings = theme.Action(settingsButton, string.Empty);
                    DrawSettingsGear(settingsButton);
                    if (openSettings)
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

        private static void DrawSettingsGear(Rect rect)
        {
            Vector2 center = rect.center;
            Color color = FrontEndTheme.Text;
            Matrix4x4 originalMatrix = GUI.matrix;

            // Eight broad rectangular teeth create an unmistakable cog silhouette instead of
            // the previous sun-like radial strokes. Keep everything vector-drawn for crisp mobile UI.
            Rect tooth = new Rect(center.x - 3.25f, center.y - 18f, 6.5f, 7f);
            for (int i = 0; i < 8; i++)
            {
                GUI.matrix = originalMatrix;
                GUIUtility.RotateAroundPivot(i * 45f, center);
                FrontEndTheme.Fill(tooth, color);
            }

            GUI.matrix = originalMatrix;
            // Thick body ring + clearly separated center bore reads like a conventional settings gear.
            CombatGlyphs.Ring(center, 11f, 5f, color, 32);
            CombatGlyphs.Ring(center, 3.4f, 2.2f, color, 20);
        }

        private void DrawOverlay(CombatHudLayout layout)
        {
            FrontEndTheme.Fill(new Rect(0, 0, layout.Width, layout.Height), new Color(.008f, .012f, .02f, .82f));

            // Fit all four audio controls and the leave/resume actions inside a 16:9 mobile safe area.
            float width = Mathf.Min(720f, layout.Width - 28f);
            float height = Mathf.Min(520f, layout.Height - 16f);
            Rect panel = new Rect((layout.Width - width) * .5f, (layout.Height - height) * .5f, width, height);
            theme.Panel(panel);
            FrontEndTheme.Fill(new Rect(panel.x, panel.y, panel.width, 2), FrontEndTheme.Accent);

            theme.Label(new Rect(panel.x + 24, panel.y + 14, panel.width - 48, 36), "MATCH-OPTIONEN", theme.Heading);
            string state = CoopAuthority.Active
                ? "CO-OP-MATCH LÄUFT WEITER, WÄHREND DIE OPTIONEN GEÖFFNET SIND"
                : "MATCH PAUSIERT";
            theme.Label(new Rect(panel.x + 24, panel.y + 50, panel.width - 48, 22), state, theme.Small, true);

            float previousMaster = master, previousMusic = music, previousSfx = sfx, previousVoice = voice;
            const float rowStart = 78f;
            const float rowStep = 64f;
            DrawVolume(new Rect(panel.x + 24, panel.y + rowStart, panel.width - 48, 60), "Spiellautstärke", ref master);
            DrawVolume(new Rect(panel.x + 24, panel.y + rowStart + rowStep, panel.width - 48, 60), "Musik", ref music);
            DrawVolume(new Rect(panel.x + 24, panel.y + rowStart + rowStep * 2f, panel.width - 48, 60), "Soundeffekte", ref sfx);
            DrawVolume(new Rect(panel.x + 24, panel.y + rowStart + rowStep * 3f, panel.width - 48, 60), "Sprachchat-Lautstärke", ref voice);
            theme.Label(new Rect(panel.x + 88, panel.y + rowStart + rowStep * 4f - 3f, panel.width - 176, 20),
                "Nur empfangene Co-op-Stimmen • Mikrofonpegel bleibt unverändert", theme.Small, true);

            if (previousMaster != master || previousMusic != music || previousSfx != sfx || previousVoice != voice)
                ApplyAudioLive();

            if (!confirmLeave)
            {
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 100, panel.width - 48, 42), "MATCH FORTSETZEN", true))
                    CloseOverlay();

                string destination = CoopAuthority.Active
                    ? "CO-OP VERLASSEN  •  HAUPTMENÜ"
                    : "MATCH VERLASSEN  •  HAUPTMENÜ";
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 50, panel.width - 48, 34), destination))
                    confirmLeave = true;
            }
            else
            {
                theme.Label(new Rect(panel.x + 24, panel.yMax - 116, panel.width - 48, 28), "AKTUELLEN MATCH VERLASSEN?", theme.Heading);
                string warning = CoopAuthority.Active
                    ? "Du verlässt die aktuelle Co-op-Sitzung. Es gibt keine Match-Belohnung."
                    : "Der aktuelle Match endet ohne Match-Belohnung.";
                theme.Label(new Rect(panel.x + 24, panel.yMax - 86, panel.width - 48, 22), warning, theme.Small, true);
                float gap = 12f, buttonWidth = (panel.width - 48 - gap) * .5f;
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 50, buttonWidth, 34), "ABBRECHEN"))
                    confirmLeave = false;
                if (theme.Action(new Rect(panel.x + 24 + buttonWidth + gap, panel.yMax - 50, buttonWidth, 34), "MATCH VERLASSEN", true))
                    LeaveMatchNow();
            }
        }

        private void DrawVolume(Rect r, string label, ref float value)
        {
            theme.Label(new Rect(r.x, r.y, r.width - 90, 24), label, theme.Body);
            theme.Label(new Rect(r.xMax - 82, r.y, 82, 24), $"{Mathf.RoundToInt(value * 100)}%", theme.Small);

            if (theme.Action(new Rect(r.x, r.y + 27, 48, 30), "−"))
                value = Mathf.Clamp01(value - .1f);

            Rect track = new Rect(r.x + 64, r.y + 39, r.width - 128, 8);
            FrontEndTheme.Fill(track, FrontEndTheme.Raised);
            FrontEndTheme.Fill(new Rect(track.x, track.y, track.width * value, track.height), FrontEndTheme.Accent);

            // Use a generous touch lane around the thin visual track for phones/tablets.
            Rect touchTrack = new Rect(track.x, r.y + 25, track.width, 34);
            int id = GUIUtility.GetControlID(FocusType.Passive, touchTrack);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && touchTrack.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = id;
                value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                e.Use();
            }
            if (GUIUtility.hotControl == id && e.type == EventType.MouseDrag)
            {
                value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                e.Use();
            }
            if (GUIUtility.hotControl == id && e.type == EventType.MouseUp)
            {
                value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                GUIUtility.hotControl = 0;
                e.Use();
            }

            FrontEndTheme.Fill(new Rect(track.x + track.width * value - 5, track.center.y - 9, 10, 18), FrontEndTheme.Text);

            if (theme.Action(new Rect(r.xMax - 48, r.y + 27, 48, 30), "+"))
                value = Mathf.Clamp01(value + .1f);
        }
    }
}
