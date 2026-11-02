using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Online 4v4 party surface. Friends assemble privately through the Unity lobby/Relay link.
    /// When everyone is ready, the leader starts Edgegap Group Up matchmaking. Edgegap assigns
    /// both teams and a dedicated server; the match launches automatically after server handshake.
    /// </summary>
    public sealed class OnlinePartyOverlay : MonoBehaviour
    {
        private static OnlinePartyOverlay instance;
        public static bool IsOpen => instance != null && instance.open && MainMenuController.IsVisible;
        private FrontEndTheme theme;
        private bool open;
        private string friendInviteCode = "";
        private TouchScreenKeyboard inviteKeyboard;
        private const float ReferenceHeight = 720f;
        private const float ReferenceWidth = 1500f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance == null)
                new GameObject("DF_OnlinePartyOverlay").AddComponent<OnlinePartyOverlay>();
        }

        public static void Show()
        {
            if(instance==null)AutoCreate();
            if(instance!=null)instance.open=true;
        }

        public static void Hide()
        {
            if(instance!=null)instance.open=false;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            // Play Online opens this surface; a closed party must not draw over the menu.
            if (!MainMenuController.IsVisible || MainMenuController.ReviewPromptVisible || !open || OnlineMatchLoadingScreen.IsVisible) return;

            OnlinePartyService online = OnlinePartyService.Instance;
            if (online == null) return;
            EdgegapOnlineFlow edgegap = EdgegapOnlineFlow.Instance;
            bool coopActive = CoopLobbyService.Instance != null && CoopLobbyService.Instance.InLobby;
            if (coopActive && !online.InParty)
            {
                open = false;
                return;
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            try
            {
                GUI.depth = -6000;
                Rect safe = Screen.safeArea;
                if (safe.width <= 0f || safe.height <= 0f) return;

                float scale = Mathf.Min(safe.height / ReferenceHeight, safe.width / ReferenceWidth);
                float menuWidth = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - ReferenceHeight * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0f), Quaternion.identity, Vector3.one * scale);
                GUI.color = Color.white;
                GUI.enabled = true;
                theme ??= new FrontEndTheme();

                DrawOverlay(menuWidth, online, edgegap);
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
            }
        }

        private void DrawOverlay(float menuWidth, OnlinePartyService online, EdgegapOnlineFlow edgegap)
        {
            Texture2D pixel = FrontEndTheme.Pixel;
            FrontEndTheme.Fill(new Rect(0f, 0f, menuWidth, ReferenceHeight), new Color(.005f, .009f, .018f, .95f));
            FrontEndTheme.Fill(new Rect(0f, 0f, menuWidth * .5f, 3f), new Color(FrontEndTheme.Zero.r, FrontEndTheme.Zero.g, FrontEndTheme.Zero.b, .75f));
            FrontEndTheme.Fill(new Rect(menuWidth * .5f, 0f, menuWidth * .5f, 3f), new Color(FrontEndTheme.Positive.r, FrontEndTheme.Positive.g, FrontEndTheme.Positive.b, .75f));

            float width = Mathf.Min(1180f, menuWidth - 120f);
            float left = (menuWidth - width) * .5f;
            Rect shell = new(left, 46f, width, 628f);
            theme.Panel(shell);
            DimensionUiMotif.DrawSplitPanel(shell, pixel,
                new Color(.014f, .022f, .038f, .96f),
                new Color(.018f, .028f, .048f, .96f),
                FrontEndTheme.Zero, FrontEndTheme.Positive, .24f);
            DimensionUiMotif.DrawCornerCuts(shell, pixel, FrontEndTheme.Zero, FrontEndTheme.Positive);

            Rect headerBand = new(left + 2f, 48f, width - 4f, 84f);
            FrontEndTheme.Fill(headerBand, new Color(.008f, .014f, .025f, .78f));
            DimensionUiMotif.DrawHeaderLine(new Rect(left + 26f, 128f, width - 52f, 2f), pixel, FrontEndTheme.Zero, FrontEndTheme.Positive);

            FrontEndTheme.Fill(new Rect(left + 26f, 64f, 4f, 50f), FrontEndTheme.Accent);
            theme.Label(new Rect(left + 40f, 60f, width - 380f, 36f), "MATCHMAKING", theme.Title);
            theme.Label(new Rect(left + 42f, 96f, width - 380f, 22f),
                "4v4 TACTICAL ARENA  •  DEDICATED SERVER DEPLOYMENT & SQUAD LINK",
                theme.Small, true);

            bool searchingAny = edgegap != null && edgegap.IsSearching;
            theme.StatusBadge(new Rect(left + width - 376f, 69f, 204f, 38f),
                searchingAny ? "QUEUE ACTIVE" : online.InParty ? "PARTY LINKED" : "4v4 ONLINE READY",
                searchingAny ? FrontEndTheme.Positive : online.InParty ? FrontEndTheme.Accent : FrontEndTheme.Zero);

            if (theme.Action(new Rect(left + width - 156f, 67f, 130f, 42f), "CLOSE"))
            {
                open = false;
                return;
            }

            if (!online.InParty)
            {
                DrawEntry(left, width, online, edgegap);
                return;
            }

            DrawParty(left, width, online, edgegap);
        }

        private void DrawEntry(float left, float width, OnlinePartyService online, EdgegapOnlineFlow edgegap)
        {
            Texture2D pixel = FrontEndTheme.Pixel;
            bool busy = online.Busy || (edgegap != null && edgegap.Busy);
            bool searching = edgegap != null && edgegap.IsSearching;
            float contentLeft = left + 26f;
            float contentWidth = width - 52f;
            float gap = 18f;
            float colWidth = (contentWidth - gap) * .5f;
            float cardY = 146f;
            float cardH = 446f;

            // LEFT CARD: 4v4 QUICK MATCH DEPLOYMENT
            Rect quickCard = new(contentLeft, cardY, colWidth, cardH);
            HeroId selectedHero = PlayerHeroSelection.Selected;
            Color heroAccent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(selectedHero);
            theme.Panel(quickCard);
            DimensionUiMotif.DrawSplitPanel(quickCard, pixel,
                new Color(.016f, .026f, .044f, .96f),
                new Color(.012f, .020f, .036f, .96f),
                heroAccent, FrontEndTheme.Zero, .30f);
            DimensionUiMotif.DrawCornerCuts(quickCard, pixel, heroAccent, FrontEndTheme.Zero);

            theme.StatusBadge(new Rect(quickCard.x + 22f, quickCard.y + 20f, 196f, 26f),
                "SOLO / INSTANT QUEUE", FrontEndTheme.Zero);
            theme.Label(new Rect(quickCard.x + 22f, quickCard.y + 54f, quickCard.width - 44f, 34f),
                "QUICK MATCH 4v4", theme.Heading);
            theme.Label(new Rect(quickCard.x + 22f, quickCard.y + 90f, quickCard.width - 44f, 24f),
                "Deploy immediately into an automated 4v4 Hot Zone battle.",
                theme.Small, true);
            DimensionUiMotif.DrawHeaderLine(new Rect(quickCard.x + 22f, quickCard.y + 122f, quickCard.width - 44f, 2f),
                pixel, heroAccent, FrontEndTheme.Zero);

            // Active Hero Loadout Preview Box inside Quick Match Card
            Rect heroBox = new(quickCard.x + 22f, quickCard.y + 138f, quickCard.width - 44f, 152f);
            FrontEndTheme.Fill(heroBox, new Color(.008f, .014f, .024f, .92f));
            FrontEndTheme.Fill(new Rect(heroBox.x, heroBox.y, 4f, heroBox.height), heroAccent);
            DimensionUiMotif.DrawCornerCuts(heroBox, pixel, heroAccent, FrontEndTheme.Accent);

            Rect portraitRect = new(heroBox.x + 16f, heroBox.y + 16f, 104f, 120f);
            StylizedHeroCard.Draw(portraitRect, selectedHero, pixel, Time.unscaledTime);

            float infoX = portraitRect.xMax + 18f;
            float infoW = heroBox.xMax - infoX - 16f;
            theme.Label(new Rect(infoX, heroBox.y + 16f, infoW, 18f), "ACTIVE CHAMPION LOADOUT", theme.Small, true);
            theme.FitLabel(new Rect(infoX, heroBox.y + 38f, infoW, 30f),
                HeroIdentity.NameFor(selectedHero).ToUpperInvariant(), theme.Heading);
            theme.StatusBadge(new Rect(infoX, heroBox.y + 74f, Mathf.Min(170f, infoW), 24f),
                HeroIdentity.RoleFor(selectedHero).ToString().ToUpperInvariant() + " CLASS", heroAccent);
            theme.Label(new Rect(infoX, heroBox.y + 108f, infoW, 20f),
                "TEAM ZERO vs TEAM LUMEN  •  HOT ZONE", theme.Small, true);

            // Matchmaking telemetry bar inside Quick Match Card
            Rect queueInfo = new(quickCard.x + 22f, quickCard.y + 304f, quickCard.width - 44f, 44f);
            FrontEndTheme.Fill(queueInfo, new Color(.010f, .018f, .030f, .88f));
            FrontEndTheme.Fill(new Rect(queueInfo.x, queueInfo.y, queueInfo.width, 1f),
                new Color(FrontEndTheme.Zero.r, FrontEndTheme.Zero.g, FrontEndTheme.Zero.b, .35f));
            if (busy || searching)
            {
                float sweep = Mathf.PingPong(Time.unscaledTime * 1.4f, 1f);
                float barW = (queueInfo.width - 8f) * .35f;
                float barX = queueInfo.x + 4f + (queueInfo.width - 8f - barW) * sweep;
                FrontEndTheme.Fill(new Rect(barX, queueInfo.yMax - 4f, barW, 3f), FrontEndTheme.Positive);
            }
            theme.Label(new Rect(queueInfo.x + 14f, queueInfo.y + 11f, queueInfo.width - 28f, 22f),
                busy ? "SCANNING REGIONAL SERVERS & ASSEMBLING TEAMS..." : "DEDICATED SERVER  •  AUTO TEAM FILL ENABLED",
                theme.Small, true);

            if (theme.Action(new Rect(quickCard.x + 22f, quickCard.y + 364f, quickCard.width - 44f, 58f),
                busy ? "SEARCHING..." : "FIND MATCH", true,
                enabled: !busy && edgegap != null))
                _ = edgegap.QuickSearchAsync();

            // RIGHT CARD: FRIEND SQUAD & INVITE CODE LINK
            Rect partyCard = new(contentLeft + colWidth + gap, cardY, colWidth, cardH);
            theme.Panel(partyCard);
            DimensionUiMotif.DrawSplitPanel(partyCard, pixel,
                new Color(.014f, .024f, .042f, .96f),
                new Color(.018f, .030f, .050f, .96f),
                FrontEndTheme.Accent, FrontEndTheme.Positive, .30f);
            DimensionUiMotif.DrawCornerCuts(partyCard, pixel, FrontEndTheme.Accent, FrontEndTheme.Positive);

            theme.StatusBadge(new Rect(partyCard.x + 22f, partyCard.y + 20f, 196f, 26f),
                "CO-OP SQUAD LINK", FrontEndTheme.Positive);
            theme.Label(new Rect(partyCard.x + 22f, partyCard.y + 54f, partyCard.width - 44f, 34f),
                "PLAY ONLINE OR INVITE FRIENDS", theme.Heading);
            theme.Label(new Rect(partyCard.x + 22f, partyCard.y + 90f, partyCard.width - 44f, 24f),
                "Create a private 4-player squad on one team or join via code.",
                theme.Small, true);
            DimensionUiMotif.DrawHeaderLine(new Rect(partyCard.x + 22f, partyCard.y + 122f, partyCard.width - 44f, 2f),
                pixel, FrontEndTheme.Accent, FrontEndTheme.Positive);

            // Create Party Sub-Panel
            Rect createBox = new(partyCard.x + 22f, partyCard.y + 138f, partyCard.width - 44f, 122f);
            FrontEndTheme.Fill(createBox, new Color(.008f, .014f, .024f, .92f));
            FrontEndTheme.Fill(new Rect(createBox.x, createBox.y, 4f, createBox.height), FrontEndTheme.Positive);
            theme.Label(new Rect(createBox.x + 16f, createBox.y + 12f, createBox.width - 32f, 20f),
                "HOST A PRIVATE 4-PLAYER SQUAD", theme.Body);
            theme.Label(new Rect(createBox.x + 16f, createBox.y + 34f, createBox.width - 32f, 20f),
                "Generate an invite code, lock heroes with friends, and queue together.",
                theme.Small, true);
            if (theme.Action(new Rect(createBox.x + 16f, createBox.y + 62f, createBox.width - 32f, 46f),
                "CREATE FRIEND PARTY", true, enabled: !busy))
                _ = online.CreatePrivatePartyAsync();

            // Join By Invite Code Sub-Panel
            Rect joinBox = new(partyCard.x + 22f, partyCard.y + 274f, partyCard.width - 44f, 150f);
            FrontEndTheme.Fill(joinBox, new Color(.008f, .014f, .024f, .92f));
            FrontEndTheme.Fill(new Rect(joinBox.x, joinBox.y, 4f, joinBox.height), FrontEndTheme.Accent);
            theme.Label(new Rect(joinBox.x + 16f, joinBox.y + 12f, joinBox.width - 32f, 22f),
                "HAVE AN INVITE CODE?", theme.Body);
            theme.Label(new Rect(joinBox.x + 16f, joinBox.y + 34f, joinBox.width - 32f, 18f),
                "Enter your squad leader's invite code below to link directly.",
                theme.Small, true);

            float fieldWidth = joinBox.width - 134f;
            Rect inviteField = new(joinBox.x + 16f, joinBox.y + 58f, fieldWidth, 38f);
            Event currentEvent = Event.current;
            if (Application.isMobilePlatform && currentEvent != null &&
                currentEvent.type == EventType.MouseDown && inviteField.Contains(currentEvent.mousePosition))
            {
                inviteKeyboard = TouchScreenKeyboard.Open(friendInviteCode ?? "",
                    TouchScreenKeyboardType.Default, false, false, false, false);
            }

            friendInviteCode = GUI.TextField(inviteField, friendInviteCode ?? "", 64, theme.Field);
            if (inviteKeyboard != null)
            {
                if (inviteKeyboard.status != TouchScreenKeyboard.Status.Canceled)
                    friendInviteCode = inviteKeyboard.text ?? friendInviteCode;
                if (inviteKeyboard.status == TouchScreenKeyboard.Status.Done ||
                    inviteKeyboard.status == TouchScreenKeyboard.Status.Canceled)
                    inviteKeyboard = null;
            }

            if (theme.Action(new Rect(inviteField.xMax + 8f, joinBox.y + 58f, 94f, 38f), "PASTE",
                enabled: !busy && !string.IsNullOrEmpty(GUIUtility.systemCopyBuffer)))
            {
                friendInviteCode = GUIUtility.systemCopyBuffer ?? "";
                inviteKeyboard = null;
                GUI.FocusControl(null);
            }

            if (theme.Action(new Rect(joinBox.x + 16f, joinBox.y + 102f, joinBox.width - 32f, 38f), "JOIN PARTY",
                true, enabled: !busy && OnlinePartyService.NormalizeJoinCode(friendInviteCode).Length >= 4))
                _ = online.JoinByCodeAsync(OnlinePartyService.NormalizeJoinCode(friendInviteCode));

            // BOTTOM TELEMETRY BAR
            Rect footerBar = new(contentLeft, 604f, contentWidth, 50f);
            FrontEndTheme.Fill(footerBar, new Color(.008f, .014f, .025f, .92f));
            DimensionUiMotif.DrawHeaderLine(new Rect(footerBar.x, footerBar.y, footerBar.width, 2f),
                pixel, FrontEndTheme.Zero, FrontEndTheme.Positive);
            string state = !string.IsNullOrEmpty(online.LastError) ? online.LastError :
                edgegap != null && !string.IsNullOrEmpty(edgegap.LastError) ?
                edgegap.LastError : online.Status;
            bool hasError = !string.IsNullOrEmpty(online.LastError) || (edgegap != null && !string.IsNullOrEmpty(edgegap.LastError));
            FrontEndTheme.Fill(new Rect(footerBar.x + 16f, footerBar.y + 19f, 10f, 10f),
                hasError ? FrontEndTheme.Danger : busy ? FrontEndTheme.Positive : FrontEndTheme.Zero);
            theme.FitLabel(new Rect(footerBar.x + 36f, footerBar.y + 12f, footerBar.width - 52f, 26f),
                string.IsNullOrEmpty(state) ? "ONLINE SERVICES READY  •  SELECT QUICK MATCH OR CREATE A FRIEND PARTY" : state,
                theme.Small, true);
        }

        private void DrawParty(float left, float width, OnlinePartyService online, EdgegapOnlineFlow edgegap)
        {
            Texture2D pixel = FrontEndTheme.Pixel;
            bool edgegapSearching = edgegap != null && edgegap.IsSearching;
            bool matchFound = edgegapSearching && EdgegapMatchmakerClient.Instance?.HasAssignment == true;
            bool busy = online.Busy || (edgegap != null && edgegap.Busy);

            // TOP MATCH STATUS TELEMETRY DECK
            Rect statusDeck = new(left + 26f, 142f, width - 52f, 86f);
            theme.Panel(statusDeck);
            DimensionUiMotif.DrawSplitPanel(statusDeck, pixel,
                new Color(.014f, .024f, .042f, .96f),
                new Color(.018f, .028f, .048f, .96f),
                FrontEndTheme.Zero, FrontEndTheme.Positive, .25f);
            DimensionUiMotif.DrawCornerCuts(statusDeck, pixel, FrontEndTheme.Zero, FrontEndTheme.Positive);

            if (edgegapSearching)
            {
                float sweep = Mathf.PingPong(Time.unscaledTime * 1.35f, 1f);
                float barW = (statusDeck.width - 12f) * .32f;
                float barX = statusDeck.x + 6f + (statusDeck.width - 12f - barW) * sweep;
                FrontEndTheme.Fill(new Rect(barX, statusDeck.yMax - 4f, barW, 3f),
                    matchFound ? FrontEndTheme.Accent : FrontEndTheme.Positive);
            }

            FrontEndTheme.Fill(new Rect(statusDeck.x + 16f, statusDeck.y + 16f, 4f, 54f),
                matchFound ? FrontEndTheme.Accent : edgegapSearching ? FrontEndTheme.Positive : FrontEndTheme.Zero);
            theme.Label(new Rect(left + 50f, 154f, 420f, 18f), "MATCH STATUS  //  SQUAD TELEMETRY", theme.Small, true);
            theme.Label(new Rect(left + 50f, 176f, 430f, 32f),
                matchFound ? "CONNECTING TO MATCH" : edgegapSearching ? "SEARCHING FOR PLAYERS" : "PREPARING MATCH", theme.Heading);

            theme.Label(new Rect(left + 500f, 158f, 240f, 22f),
                $"SQUAD SLOTS  {online.PartyPlayerCount} / {online.PartyCapacity}", theme.Body);
            theme.Label(new Rect(left + 500f, 184f, 240f, 20f),
                "4v4  •  AUTO TEAM FILL", theme.Small, true);
            theme.StatusBadge(new Rect(left + 752f, 168f, 204f, 32f),
                matchFound ? "MATCH FOUND" : edgegapSearching ? "FINDING YOUR MATCH" : "MATCH LOBBY",
                matchFound ? FrontEndTheme.Accent : edgegapSearching ? FrontEndTheme.Positive : FrontEndTheme.Zero);

            if (theme.Action(new Rect(left + width - 200f, 163f, 154f, 44f),
                "LEAVE PARTY", enabled: !online.Busy))
            {
                if (edgegap != null) _ = edgegap.LeavePartyAsync();
                else _ = online.LeavePartyAsync();
            }

            // 4-PLAYER SQUAD ROSTER CARDS
            const int slots = OnlinePartyService.MaxPartyPlayers;
            const float playerGap = 12f;
            float innerWidth = width - 52f;
            float playerW = (innerWidth - playerGap * (slots - 1)) / slots;
            IReadOnlyList<OnlinePartyService.MemberView> members = online.Members;

            for (int slot = 0; slot < slots; slot++)
            {
                float x = left + 26f + slot * (playerW + playerGap);
                Rect cardRect = new(x, 240f, playerW, 202f);
                theme.Panel(cardRect);

                if (slot >= members.Count)
                {
                    DimensionUiMotif.DrawSplitPanel(cardRect, pixel,
                        new Color(.010f, .016f, .028f, .90f),
                        new Color(.012f, .020f, .034f, .90f),
                        FrontEndTheme.Zero, FrontEndTheme.Muted, .18f);
                    DimensionUiMotif.DrawCornerCuts(cardRect, pixel, FrontEndTheme.Muted, FrontEndTheme.Zero);
                    FrontEndTheme.Fill(new Rect(x, cardRect.y, playerW, 3f), new Color(FrontEndTheme.Zero.r, FrontEndTheme.Zero.g, FrontEndTheme.Zero.b, .35f));

                    theme.Label(new Rect(x + 14f, 254f, playerW - 28f, 20f), $"SQUAD SLOT 0{slot + 1}", theme.Small, true);
                    Rect openBadge = new(x + 24f, 306f, playerW - 48f, 36f);
                    theme.StatusBadge(openBadge, edgegapSearching ? "RESERVED" : "OPEN SLOT",
                        edgegapSearching ? FrontEndTheme.Positive : FrontEndTheme.Muted);
                    theme.Center(new Rect(x + 12f, 354f, playerW - 24f, 22f),
                        edgegapSearching ? "FINDING A MATCH" : "WAITING FOR PLAYERS");
                    theme.Label(new Rect(x + 14f, 382f, playerW - 28f, 20f),
                        edgegapSearching ? "AUTO-FILL TEAMMATE" : "INVITE VIA CODE BELOW", theme.Small, true);
                    continue;
                }

                OnlinePartyService.MemberView member = members[slot];
                Color heroColor = Alterverse.Visuals.StylizedArtDirection.HeroAccent(member.Hero);
                DimensionUiMotif.DrawSplitPanel(cardRect, pixel,
                    new Color(.015f, .025f, .044f, .96f),
                    new Color(.012f, .020f, .036f, .96f),
                    heroColor, member.Ready ? FrontEndTheme.Positive : FrontEndTheme.Zero, .32f);
                DimensionUiMotif.DrawCornerCuts(cardRect, pixel, heroColor, member.Ready ? FrontEndTheme.Positive : FrontEndTheme.Zero);

                // Top Hero Accent Strip & Header Band
                FrontEndTheme.Fill(new Rect(x, cardRect.y, playerW, 4f), heroColor);
                FrontEndTheme.Fill(new Rect(x + 2f, cardRect.y + 4f, playerW - 4f, 28f),
                    new Color(heroColor.r, heroColor.g, heroColor.b, .10f));
                if (member.IsLocal)
                    theme.Frame(cardRect, true);

                string role = member.IsHost ? "HOST" : $"PLAYER {slot + 1}";
                if (member.IsLocal) role += " • YOU";
                theme.Label(new Rect(x + 12f, 248f, playerW - 24f, 20f), role, theme.Small, true);

                // Stylized Hero Mini-Card + Player Identity
                Rect miniHero = new(x + 12f, 278f, 56f, 68f);
                StylizedHeroCard.Draw(miniHero, member.Hero, pixel, Time.unscaledTime + slot * .4f);

                float textX = miniHero.xMax + 10f;
                float textW = playerW - (textX - x) - 10f;
                theme.IdentityTile(new Rect(textX, 278f, 26f, 26f), member.DisplayName, member.IsLocal);
                theme.FitLabel(new Rect(textX + 32f, 277f, textW - 32f, 28f), member.DisplayName, theme.Body);
                theme.FitLabel(new Rect(textX, 308f, textW, 20f),
                    HeroIdentity.NameFor(member.Hero).ToUpperInvariant(), theme.Body);
                theme.FitLabel(new Rect(textX, 327f, textW, 18f),
                    HeroIdentity.RoleFor(member.Hero).ToString().ToUpperInvariant() + " CLASS", theme.Small, true);

                theme.StatusBadge(new Rect(x + 12f, 354f, playerW - 24f, 26f),
                    member.Ready ? "READY" : "NOT READY",
                    member.Ready ? FrontEndTheme.Positive : FrontEndTheme.Warning);

                if (member.IsLocal)
                {
                    bool editable = online.CanEditParty && !edgegapSearching;
                    if (theme.Action(new Rect(x + 12f, 388f, 36f, 38f), "<", enabled: editable))
                        _ = online.CycleLocalHeroAsync(-1);
                    if (theme.Action(new Rect(x + playerW - 48f, 388f, 36f, 38f), ">", enabled: editable))
                        _ = online.CycleLocalHeroAsync(1);

                    float readyX = x + 54f;
                    float readyW = Mathf.Max(42f, playerW - 108f);
                    string readyText = online.LocalReady ? "UNREADY" : "READY";
                    if (theme.Action(new Rect(readyX, 388f, readyW, 38f), readyText, true,
                        enabled: editable && (online.LocalReady || !online.HasHeroConflict)))
                        _ = online.SetLocalReadyAsync(!online.LocalReady);
                }
                else
                {
                    theme.Label(new Rect(x + 12f, 396f, playerW - 24f, 22f),
                        member.Ready ? "LOCKED IN FOR DEPLOYMENT" : "SELECTING LOADOUT...", theme.Small, true);
                }
            }

            // COMMAND DECK PANEL
            Rect commandDeck = new(left + 26f, 454f, width - 52f, 108f);
            theme.Panel(commandDeck);
            DimensionUiMotif.DrawSplitPanel(commandDeck, pixel,
                new Color(.012f, .022f, .038f, .96f),
                new Color(.016f, .026f, .044f, .96f),
                FrontEndTheme.Zero, FrontEndTheme.Accent, .26f);
            DimensionUiMotif.DrawCornerCuts(commandDeck, pixel, FrontEndTheme.Zero, FrontEndTheme.Accent);

            string searchText;
            bool searchEnabled;
            if (!online.IsHost)
            {
                searchText = edgegapSearching ? "LEADER IS SEARCHING" : "WAITING FOR LEADER";
                searchEnabled = false;
            }
            else if (edgegapSearching)
            {
                searchText = "CANCEL SEARCH";
                searchEnabled = edgegap.CanCancelSearch;
            }
            else if (online.HasHeroConflict)
            {
                searchText = "HERO CONFLICT";
                searchEnabled = false;
            }
            else if (!online.AllPresentPlayersReady)
            {
                searchText = "WAITING READY";
                searchEnabled = false;
            }
            else
            {
                searchText = "FIND MATCH";
                searchEnabled = !busy && edgegap != null;
            }

            float actionY = 480f;
            float searchX = left + width - 494f;
            float startX = left + width - 262f;
            if (theme.Action(new Rect(searchX, actionY, 218f, 56f), searchText, true,
                selected: edgegapSearching, enabled: searchEnabled))
            {
                if (edgegapSearching) _ = edgegap.CancelSearchAsync();
                else _ = edgegap.BeginSearchAsync();
            }

            string autoText = edgegapSearching ? "STARTS WHEN READY" : "4v4 ONLINE";
            theme.StatusBadge(new Rect(startX, actionY + 11f, 212f, 34f),
                autoText, edgegapSearching ? FrontEndTheme.Positive : FrontEndTheme.Zero);

            string partyState = online.HasHeroConflict
                ? "CHOOSE DIFFERENT HEROES"
                : matchFound
                    ? "CONNECTING TO GAME SERVER"
                    : edgegapSearching
                        ? "FINDING OPPONENTS"
                        : online.AllPresentPlayersReady
                            ? "ALL FRIENDS READY • START MATCHMAKING"
                            : "MATCH LOBBY • READY UP";
            theme.Label(new Rect(left + 46f, 468f, width - 560f, 26f), partyState, theme.Body);
            theme.Label(new Rect(left + 46f, 498f, width - 560f, 22f),
                $"PARTY {online.PartyPlayerCount}/{online.PartyCapacity}  •  TWO TEAMS OF FOUR  •  HOT ZONE",
                theme.Small, true);
            theme.FitLabel(new Rect(left + 46f, 524f, width - 560f, 20f),
                edgegap != null ? edgegap.Status : "CONNECTING TO MATCHMAKING...", theme.Small, true);

            // INVITE CODE TICKET & TELEMETRY FOOTER BAR
            Rect inviteBar = new(left + 26f, 572f, width - 52f, 84f);
            FrontEndTheme.Fill(inviteBar, new Color(.008f, .014f, .025f, .94f));
            DimensionUiMotif.DrawHeaderLine(new Rect(inviteBar.x, inviteBar.y, inviteBar.width, 2f),
                pixel, FrontEndTheme.Accent, FrontEndTheme.Positive);
            DimensionUiMotif.DrawCornerCuts(inviteBar, pixel, FrontEndTheme.Accent, FrontEndTheme.Positive);

            string codeValue = online.JoinCode.Length > 0 ? online.JoinCode : "WAITING";
            theme.Label(new Rect(left + 44f, 584f, 210f, 28f), "FRIEND INVITE CODE", theme.Small, true);
            theme.StatusBadge(new Rect(left + 230f, 582f, 190f, 32f), codeValue,
                online.JoinCode.Length > 0 ? FrontEndTheme.Accent : FrontEndTheme.Muted);

            if (theme.Action(new Rect(left + width - 242f, 580f, 198f, 38f),
                "COPY INVITE CODE", enabled: online.JoinCode.Length > 0 && !online.MatchmakingLocked))
                GUIUtility.systemCopyBuffer = online.JoinCode;

            string state;
            if (edgegap != null && !string.IsNullOrEmpty(edgegap.LastError)) state = edgegap.LastError;
            else if (!string.IsNullOrEmpty(online.LastError)) state = online.LastError;
            else state = DedicatedOnlineMatchHandshake.Status;
            theme.FitLabel(new Rect(left + 44f, 624f, width - 88f, 22f),
                string.IsNullOrEmpty(state) ? "SHARE YOUR INVITE CODE WITH UP TO 3 FRIENDS TO LOCK IN ON THE SAME TEAM" : state,
                theme.Small, true);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
