using System.Collections.Generic;
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
        private readonly LanguagePicker languagePicker = new LanguagePicker();
        private float master, music, sfx;
        private Texture2D settingsGearIcon;
        private bool showVoicePlayers;
        private Vector2 voiceScroll;
        private readonly List<PartyVoiceService.VoicePlayer> voicePlayers = new();

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
            if (settingsGearIcon != null)
                Destroy(settingsGearIcon);
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
            showVoicePlayers = false;
            voiceScroll = Vector2.zero;

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
            languagePicker.Close();
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

            FlushAudio();
            CloseOverlay();
            await MatchExit.ReturnToMenuAsync();
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

                bool choosingLanguage = languagePicker.IsOpen;
                bool wasEnabled = GUI.enabled;
                try
                {
                    if (choosingLanguage) GUI.enabled = false;
                    DrawOverlay(layout);
                    if (choosingLanguage)
                    {
                        GUI.enabled = wasEnabled;
                        languagePicker.Draw(theme, new Rect(0, 0, layout.Width, layout.Height));
                    }
                }
                finally { GUI.enabled = wasEnabled; }
            }
            finally
            {
                GUI.depth = depth;
                GUI.matrix = matrix;
                GUI.color = color;
            }
        }

        private void DrawSettingsGear(Rect rect)
        {
            settingsGearIcon ??= CreateSettingsGearIcon();
            if (settingsGearIcon == null) return;

            const float iconSize = 30f;
            Rect iconRect = new Rect(
                rect.center.x - iconSize * .5f,
                rect.center.y - iconSize * .5f,
                iconSize,
                iconSize);

            Color previous = GUI.color;
            GUI.color = FrontEndTheme.Text;
            GUI.DrawTexture(iconRect, settingsGearIcon, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static Texture2D CreateSettingsGearIcon()
        {
            // Build the icon as pixels once. This avoids RotateAroundPivot entirely, which can distort
            // IMGUI shapes when the combat HUD already has a non-identity scaling matrix.
            const int size = 64;
            const int samples = 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "DF_SettingsGearIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = ((x + (sx + .5f) / samples) - center) / (size * .5f);
                            float py = ((y + (sy + .5f) / samples) - center) / (size * .5f);
                            if (InsideGear(px, py)) covered++;
                        }
                    }

                    float alpha = covered / (float)(samples * samples);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool InsideGear(float x, float y)
        {
            float radius = Mathf.Sqrt(x * x + y * y);
            const float holeRadius = .22f;
            const float bodyRadius = .66f;
            const float outerRadius = .92f;
            const float toothHalfWidth = .14f;

            if (radius < holeRadius) return false;
            if (radius <= bodyRadius) return true;

            // Union of eight broad radial rectangles gives a conventional cog silhouette.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float radial = x * cos + y * sin;
                float tangent = -x * sin + y * cos;

                if (radial >= bodyRadius * .78f && radial <= outerRadius && Mathf.Abs(tangent) <= toothHalfWidth)
                    return true;
            }

            return false;
        }

        private void DrawOverlay(CombatHudLayout layout)
        {
            FrontEndTheme.Fill(new Rect(0, 0, layout.Width, layout.Height), new Color(.008f, .012f, .02f, .86f));

            float width = Mathf.Min(780f, layout.Width - 24f);
            float height = Mathf.Min(showVoicePlayers ? 620f : 536f, layout.Height - 16f);
            Rect panel = new Rect((layout.Width - width) * .5f, (layout.Height - height) * .5f, width, height);
            theme.Panel(panel);
            FrontEndTheme.Fill(new Rect(panel.x, panel.y, panel.width, 2), FrontEndTheme.Accent);

            theme.Label(new Rect(panel.x + 24, panel.y + 12, panel.width - 252, 32), GameLanguage.Text("MATCH OPTIONS"), theme.Heading);
            if (theme.Action(new Rect(panel.xMax - 204, panel.y + 12, 180, 32),
                GameLanguage.Text(showVoicePlayers ? "AUDIO SETTINGS" : "VOICE / MUTE"), selected: showVoicePlayers))
                showVoicePlayers = !showVoicePlayers;
            string state = CoopAuthority.Active
                ? "CO-OP MATCH CONTINUES WHILE OPTIONS ARE OPEN"
                : "MATCH PAUSED";
            theme.Label(new Rect(panel.x + 24, panel.y + 42, panel.width - 48, 20), GameLanguage.Text(state), theme.Small, true);

            if (showVoicePlayers)
            {
                DrawVoicePlayers(new Rect(panel.x + 24, panel.y + 66, panel.width - 48, Mathf.Max(40, panel.height - 168)));
            }
            else
            {
                float previousMaster = master, previousMusic = music, previousSfx = sfx;
                const float rowStart = 66f;
                const float rowStep = 56f;
                DrawVolume(new Rect(panel.x + 24, panel.y + rowStart, panel.width - 48, 52), GameLanguage.Text("Master volume"), ref master);
                DrawVolume(new Rect(panel.x + 24, panel.y + rowStart + rowStep, panel.width - 48, 52), GameLanguage.Text("Music"), ref music);
                DrawVolume(new Rect(panel.x + 24, panel.y + rowStart + rowStep * 2f, panel.width - 48, 52), GameLanguage.Text("Sound effects"), ref sfx);
                if (previousMaster != master || previousMusic != music || previousSfx != sfx)
                    ApplyAudioLive();

                float langY = panel.y + 240f;
                theme.Label(new Rect(panel.x + 24, langY, 150, 36), GameLanguage.Text("Language"), theme.Body);
                languagePicker.DrawControl(theme, new Rect(panel.x + 184, langY, panel.width - 208, 36));

                // Graphics Quality & Hardware Auto-Detection bar
                float gfxY = langY + 44f;
                theme.Label(new Rect(panel.x + 24, gfxY, 150, 34), GameLanguage.Text("Graphics"), theme.Body);
                float gfxBtnW = (panel.width - 208f - 18f) / 4f;
                bool isAuto = Alterverse.Performance.MobilePerformanceProfile.AutoModeEnabled;
                var currentQ = Alterverse.Performance.MobilePerformanceProfile.Current;
                if (theme.Action(new Rect(panel.x + 184, gfxY, gfxBtnW, 34), "AUTO", selected: isAuto))
                    Alterverse.Performance.MobilePerformanceProfile.EnableAutoHardwareProfile();
                if (theme.Action(new Rect(panel.x + 184 + (gfxBtnW + 6f), gfxY, gfxBtnW, 34), "LOW", selected: !isAuto && currentQ == Alterverse.Performance.MobileQuality.Battery))
                    Alterverse.Performance.MobilePerformanceProfile.Current = Alterverse.Performance.MobileQuality.Battery;
                if (theme.Action(new Rect(panel.x + 184 + (gfxBtnW + 6f) * 2f, gfxY, gfxBtnW, 34), "BALANCED", selected: !isAuto && currentQ == Alterverse.Performance.MobileQuality.Balanced))
                    Alterverse.Performance.MobilePerformanceProfile.Current = Alterverse.Performance.MobileQuality.Balanced;
                if (theme.Action(new Rect(panel.x + 184 + (gfxBtnW + 6f) * 3f, gfxY, gfxBtnW, 34), "HIGH", selected: !isAuto && currentQ == Alterverse.Performance.MobileQuality.Performance))
                    Alterverse.Performance.MobilePerformanceProfile.Current = Alterverse.Performance.MobileQuality.Performance;


            }

            if (!confirmLeave)
            {
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 86, panel.width - 48, 38), GameLanguage.Text("RESUME MATCH"), true))
                    CloseOverlay();

                string destination = CoopAuthority.Active
                    ? "LEAVE CO-OP  -  MAIN MENU"
                    : "LEAVE MATCH  -  MAIN MENU";
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 42, panel.width - 48, 32), destination))
                    confirmLeave = true;
            }
            else
            {
                theme.Label(new Rect(panel.x + 24, panel.yMax - 102, panel.width - 48, 24), "LEAVE CURRENT MATCH?", theme.Heading);
                string warning = CoopAuthority.Active
                    ? "You will leave the current co-op session. No match reward will be granted."
                    : "The current match will end without a match reward.";
                theme.Label(new Rect(panel.x + 24, panel.yMax - 78, panel.width - 48, 20), warning, theme.Small, true);
                float gap = 12f, buttonWidth = (panel.width - 48 - gap) * .5f;
                if (theme.Action(new Rect(panel.x + 24, panel.yMax - 44, buttonWidth, 32), GameLanguage.Text("CANCEL")))
                    confirmLeave = false;
                if (theme.Action(new Rect(panel.x + 24 + buttonWidth + gap, panel.yMax - 44, buttonWidth, 32), GameLanguage.Text("LEAVE MATCH"), true))
                    LeaveMatchNow();
            }
        }

        private void DrawVoicePlayers(Rect rect)
        {
            var voice = PartyVoiceService.Instance;
            theme.Label(new Rect(rect.x, rect.y, rect.width - 190, 28), GameLanguage.Text("VOICE PLAYERS"), theme.Body);
            bool microphoneOn = voice?.MicrophoneOn == true;
            if (theme.Action(new Rect(rect.xMax - 184, rect.y, 184, 30),
                GameLanguage.Text(microphoneOn ? "MUTE MY MICROPHONE" : "ENABLE MY MICROPHONE"),
                selected: microphoneOn, enabled: voice != null && voice.HasVoiceRoom && !voice.Busy))
            {
                if (microphoneOn) voice.Mute();
                else _ = voice.EnableAsync();
            }
            theme.Label(new Rect(rect.x, rect.y + 34, rect.width, 34),
                GameLanguage.Text("Muted players are silent only for you. Enabling your microphone lets your party hear you."), theme.Small, true);
            voicePlayers.Clear();
            voice?.CopyVoicePlayers(voicePlayers);
            if (voicePlayers.Count == 0)
            {
                theme.Label(new Rect(rect.x, rect.y + 82, rect.width, 52),
                    GameLanguage.Text("No voice players connected. Join your party's voice chat to manage participants."), theme.Body, true);
                return;
            }
            Rect viewport = new Rect(rect.x, rect.y + 76, rect.width, Mathf.Max(0, rect.height - 100));
            Rect content = new Rect(0, 0, Mathf.Max(1, viewport.width - 20), voicePlayers.Count * 46f);
            voiceScroll = GUI.BeginScrollView(viewport, voiceScroll, content);
            try
            {
                for (int i = 0; i < voicePlayers.Count; i++)
                {
                    var player = voicePlayers[i];
                    Rect row = new Rect(0, i * 46f, content.width, 42);
                    theme.HudPanel(row, .55f);
                    theme.FitLabel(new Rect(row.x + 12, row.y + 4, row.width - 158, 34), player.DisplayName, theme.Body);
                    string action = player.Pending ? "APPLYING…" : player.Muted ? "UNMUTE PLAYER" : "MUTE PLAYER";
                    if (theme.Action(new Rect(row.xMax - 138, row.y + 4, 130, 34), GameLanguage.Text(action),
                        selected: player.Muted, enabled: !player.Pending))
                        voice.SetPlayerMuted(player.PlayerId, !player.Muted);
                }
            }
            finally { GUI.EndScrollView(); }
            if (!string.IsNullOrEmpty(voice.PlayerMuteStatus))
                theme.Label(new Rect(rect.x, rect.yMax - 22, rect.width, 22),
                    GameLanguage.Text(voice.PlayerMuteStatus), theme.Small, true);
        }

        private void DrawMatchFriendBar(Rect rect)
        {
            theme.HudPanel(rect, .55f);
            theme.FitLabel(new Rect(rect.x + 12, rect.y + 4, rect.width - 24, 18), GameLanguage.Text("MATCH PLAYERS  •  TAP +FR TO SEND FRIEND REQUEST"), theme.Small, true);

            var controllers = FindObjectsByType<Alterverse.Characters.HeroIdentity>(FindObjectsSortMode.None);
            int shown = 0;
            float chipW = (rect.width - 24f - 18f) / 4f;
            for (int i = 0; i < controllers.Length && shown < 8; i++)
            {
                var hc = controllers[i];
                if (hc == null) continue;
                bool isOnlineMatch = Alterverse.Multiplayer.CoopAuthority.ActivePlan is Alterverse.Multiplayer.OnlineMatchPlan;
                if (isOnlineMatch)
                {
                    var slot = hc.GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
                    if (slot != null && slot.IsLocalHuman) continue;
                }
                string pName = isOnlineMatch ? Alterverse.Match.MatchScoreboard.ResolveDisplayName(hc) : hc.name;
                if (string.IsNullOrWhiteSpace(pName)) continue;
                
                var hero = hc.Hero;
                int col = shown % 4;
                int row = shown / 4;
                Rect chip = new Rect(rect.x + 12f + col * (chipW + 6f), rect.y + 24f + row * 29f, chipW, 26f);
                bool already = FriendStickerService.IsFriendOrRequested(pName);
                theme.FitLabel(new Rect(chip.x + 4, chip.y + 2, chip.width - 48, 22), pName, theme.Small);
                if (theme.Action(new Rect(chip.xMax - 42, chip.y + 1, 40, 24), already ? "OK" : "+FR", !already, enabled: !already))
                {
                    FriendStickerService.SendFriendRequest(pName, hero, 1);
                }
                shown++;
            }
        }

        private void DrawVolume(Rect r, string label, ref float value)
        {
            theme.Label(new Rect(r.x, r.y, r.width - 90, 24), label, theme.Body);
            theme.Label(new Rect(r.xMax - 82, r.y, 82, 24), $"{Mathf.RoundToInt(value * 100)}%", theme.Small);

            if (theme.Action(new Rect(r.x, r.y + 27, 48, 30), "-"))
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




