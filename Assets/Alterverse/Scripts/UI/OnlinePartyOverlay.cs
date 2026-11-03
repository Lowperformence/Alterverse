using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using Alterverse.Profile;
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
        public static bool IsOpen => instance != null && instance.open && OnlinePartyService.Instance != null && MainMenuController.IsVisible;
        public static string LastError { get; private set; } = "";
        private FrontEndTheme theme;
        private bool open;
        private bool heroPickerOpen,invitePanelOpen,inviteInbox;
        private Vector2 invitationScroll;
        private float nextFriendPoll;
        private string consumedInvitation;
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
            LastError="";
            OnlinePartyService.EnsureAvailable();
            if (instance == null) AutoCreate();
            if (instance != null)
            {instance.heroPickerOpen=false;instance.invitePanelOpen=false;instance.open=true;}
        }

        public static void Hide()
        {
            if (instance != null) instance.open = false;
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

                GUI.enabled=!heroPickerOpen&&!invitePanelOpen&&PartyVoiceService.Instance?.NeedsConsent!=true;
                DrawOverlay(menuWidth, online, edgegap);
                GUI.enabled=true;
                GUI.enabled=PartyVoiceService.Instance?.NeedsConsent!=true;
                if(heroPickerOpen)DrawHeroPicker(menuWidth,online,edgegap);
                if(invitePanelOpen)DrawInvitations(menuWidth,online,edgegap);
                GUI.enabled=true;
                if(PartyVoiceService.Instance?.NeedsConsent==true)DrawVoiceConsent(menuWidth);
            }
            catch (System.Exception error) when (!(error is ExitGUIException))
            {
                // A broken/initializing lobby must never leave only the menu backdrop.
                LastError="Online lobby could not open. Please try again.";
                open=false;
                Debug.LogException(error);
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
            // Soft translucent cosmic dim so the orbital universe backdrop remains visible behind the lobby
            FrontEndTheme.Fill(new Rect(0f, 0f, menuWidth, ReferenceHeight), new Color(0.012f, 0.020f, 0.036f, 0.68f));

            float width = Mathf.Min(1220f, menuWidth - 96f);
            float left = (menuWidth - width) * .5f;

            // Clean top header bar matching the main menu command deck
            Rect headerPanel = new(left, 36f, width, 84f);
            theme.Panel(headerPanel);
            theme.Label(new Rect(left + 28f, 46f, width - 620f, 38f), "ONLINE MATCHMAKING", theme.Title);
            theme.Label(new Rect(left + 28f, 84f, width - 620f, 22f),
                "4v4 TACTICAL ARENA  •  DEDICATED SERVER DEPLOYMENT & SQUAD LINK",
                theme.Small, true);

            var voice=PartyVoiceService.Instance;
            if(online.InParty&&voice!=null&&theme.Action(new Rect(left+width-550f,59f,170f,38f),voice.Status,
                enabled:!voice.Busy))
            {if(voice.MicrophoneOn)voice.Mute();else voice.AskAgain();}
            bool searchingAny = edgegap != null && edgegap.IsSearching;
            theme.StatusBadge(new Rect(left + width - 368f, 59f, 200f, 38f),
                searchingAny ? "QUEUE ACTIVE" : online.InParty ? "PARTY LINKED" : "4v4 ONLINE READY",
                searchingAny ? FrontEndTheme.Positive : online.InParty ? FrontEndTheme.Accent : FrontEndTheme.Zero);

            if (theme.Action(new Rect(left + width - 154f, 57f, 128f, 42f), "CLOSE"))
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
            float gap = 20f;
            float colWidth = (width - gap) * .5f;
            float cardY = 136f;
            float cardH = 472f;

            // LEFT CARD: 4v4 QUICK MATCH DEPLOYMENT
            Rect quickCard = new(left, cardY, colWidth, cardH);
            HeroId selectedHero = PlayerHeroSelection.Selected;
            Color heroAccent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(selectedHero);
            theme.Panel(quickCard);

            theme.StatusBadge(new Rect(quickCard.x + 24f, quickCard.y + 22f, 200f, 26f),
                "SOLO / INSTANT QUEUE", FrontEndTheme.Zero);
            theme.Label(new Rect(quickCard.x + 24f, quickCard.y + 56f, quickCard.width - 48f, 34f),
                "QUICK MATCH 4v4", theme.Heading);
            theme.Label(new Rect(quickCard.x + 24f, quickCard.y + 92f, quickCard.width - 48f, 24f),
                "Deploy immediately into an automated 4v4 Hot Zone battle.",
                theme.Small, true);

            // Active Hero Loadout Preview Card
            Rect heroBox = new(quickCard.x + 24f, quickCard.y + 132f, quickCard.width - 48f, 172f);
            theme.Panel(heroBox);
            theme.Frame(heroBox, true);
            if(theme.Action(new Rect(heroBox.xMax-190f,heroBox.yMax-38f,174f,30f),"CHOOSE HERO",enabled:!busy))heroPickerOpen=true;

            Rect portraitRect = new(heroBox.x + 16f, heroBox.y + 16f, 112f, 140f);
            StylizedHeroCard.Draw(portraitRect, selectedHero, pixel, Time.unscaledTime);

            float infoX = portraitRect.xMax + 20f;
            float infoW = heroBox.xMax - infoX - 18f;
            theme.Label(new Rect(infoX, heroBox.y + 22f, infoW, 20f), "ACTIVE CHAMPION LOADOUT", theme.Small, true);
            theme.FitLabel(new Rect(infoX, heroBox.y + 46f, infoW, 34f),
                HeroIdentity.NameFor(selectedHero).ToUpperInvariant(), theme.Heading);
            theme.StatusBadge(new Rect(infoX, heroBox.y + 88f, Mathf.Min(184f, infoW), 26f),
                HeroIdentity.RoleFor(selectedHero).ToString().ToUpperInvariant(), heroAccent);


            // Matchmaking status strip
            Rect queueInfo = new(quickCard.x + 24f, quickCard.y + 320f, quickCard.width - 48f, 50f);
            theme.Panel(queueInfo);
            theme.Label(new Rect(queueInfo.x + 16f, queueInfo.y + 13f, queueInfo.width - 32f, 24f),
                busy ? "SCANNING REGIONAL SERVERS & ASSEMBLING TEAMS..." : "DEDICATED SERVER  •  AUTO TEAM FILL ENABLED",
                theme.Small, true);
            if (busy || searching)
            {
                float pulse = 0.25f + 0.75f * Mathf.PingPong(Time.unscaledTime * 1.4f, 1f);
                theme.Progress(new Rect(queueInfo.x + 16f, queueInfo.yMax - 10f, queueInfo.width - 32f, 5f), pulse);
            }

            if (theme.Action(new Rect(quickCard.x + 24f, quickCard.y + 388f, quickCard.width - 48f, 60f),
                busy ? "SEARCHING..." : "FIND MATCH", true,
                enabled: !busy && edgegap != null))
                _ = edgegap.QuickSearchAsync();

            // RIGHT CARD: FRIEND SQUAD & INVITE CODE LINK
            Rect partyCard = new(left + colWidth + gap, cardY, colWidth, cardH);
            theme.Panel(partyCard);

            theme.StatusBadge(new Rect(partyCard.x + 24f, partyCard.y + 22f, 200f, 26f),
                "CO-OP SQUAD LINK", FrontEndTheme.Positive);
            theme.Label(new Rect(partyCard.x + 24f, partyCard.y + 56f, partyCard.width - 48f, 34f),
                "PLAY WITH FRIENDS", theme.Heading);
            theme.Label(new Rect(partyCard.x + 24f, partyCard.y + 92f, partyCard.width - 48f, 24f),
                "Create a private 4-player squad on one team or join via code.",
                theme.Small, true);

            // Create Party Sub-Panel
            Rect createBox = new(partyCard.x + 24f, partyCard.y + 132f, partyCard.width - 48f, 138f);
            theme.Panel(createBox);
            theme.Label(new Rect(createBox.x + 18f, createBox.y + 14f, createBox.width - 36f, 24f),
                "HOST A PRIVATE 4-PLAYER SQUAD", theme.Body);
            theme.Label(new Rect(createBox.x + 18f, createBox.y + 40f, createBox.width - 36f, 22f),
                "Generate an invite code, lock heroes with friends, and queue together.",
                theme.Small, true);
            if (theme.Action(new Rect(createBox.x + 18f, createBox.y + 74f, createBox.width - 36f, 48f),
                "CREATE FRIEND PARTY", true, enabled: !busy))
                _ = online.CreatePrivatePartyAsync();

            // Join By Invite Code Sub-Panel
            Rect joinBox = new(partyCard.x + 24f, partyCard.y + 286f, partyCard.width - 48f, 162f);
            theme.Panel(joinBox);
            theme.Label(new Rect(joinBox.x + 18f, joinBox.y + 14f, joinBox.width - 36f, 24f),
                "HAVE AN INVITE CODE?", theme.Body);
            theme.Label(new Rect(joinBox.x + 18f, joinBox.y + 38f, joinBox.width - 36f, 20f),
                "Enter your squad leader's invite code below to link directly.",
                theme.Small, true);

            float fieldWidth = joinBox.width - 142f;
            Rect inviteField = new(joinBox.x + 18f, joinBox.y + 64f, fieldWidth, 40f);
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

            if (theme.Action(new Rect(inviteField.xMax + 8f, joinBox.y + 64f, 98f, 40f), "PASTE",
                enabled: !busy && !string.IsNullOrEmpty(GUIUtility.systemCopyBuffer)))
            {
                friendInviteCode = GUIUtility.systemCopyBuffer ?? "";
                inviteKeyboard = null;
                GUI.FocusControl(null);
            }

            if (theme.Action(new Rect(joinBox.x + 18f, joinBox.y + 112f, joinBox.width - 36f, 40f), "JOIN PARTY",
                true, enabled: !busy && OnlinePartyService.NormalizeJoinCode(friendInviteCode).Length >= 4))
                _ = online.JoinByCodeAsync(OnlinePartyService.NormalizeJoinCode(friendInviteCode));

            // BOTTOM STATUS BAR
            Rect footerBar = new(left, 622f, width, 54f);
            theme.Panel(footerBar);
            string state = !string.IsNullOrEmpty(online.LastError) ? online.LastError :
                edgegap != null && !string.IsNullOrEmpty(edgegap.LastError) ?
                edgegap.LastError : GameInviteShare.Status.Length>0?GameInviteShare.Status:online.Status;
            theme.FitLabel(new Rect(footerBar.x + 24f, footerBar.y + 14f, footerBar.width - 500f, 26f),
                string.IsNullOrEmpty(state) ? "ONLINE SERVICES READY  •  SELECT QUICK MATCH OR CREATE A FRIEND PARTY" : state,
                theme.Small, true);
            if(theme.Action(new Rect(left+width-230,630,208,36),"INVITE TO GAME",enabled:!GameInviteShare.Busy))_=GameInviteShare.ShareAsync();
            int inviteCount=SocialService.Instance?.Friends?.partyInvites?.Length??0;
            if(inviteCount>0&&theme.Action(new Rect(left+width-450,630,208,36),"INVITATIONS ("+inviteCount+")",true))
            {invitePanelOpen=true;inviteInbox=true;}
        }

        private void DrawParty(float left, float width, OnlinePartyService online, EdgegapOnlineFlow edgegap)
        {
            Texture2D pixel = FrontEndTheme.Pixel;
            bool edgegapSearching = edgegap != null && edgegap.IsSearching;
            bool matchFound = edgegapSearching && EdgegapMatchmakerClient.Instance?.HasAssignment == true;
            bool busy = online.Busy || (edgegap != null && edgegap.Busy);

            // TOP SQUAD STATUS BAR
            Rect statusDeck = new(left, 132f, width, 78f);
            theme.Panel(statusDeck);

            theme.Label(new Rect(left + 26f, 142f, 440f, 18f), "SQUAD LOBBY  •  4v4 HOT ZONE", theme.Small, true);
            theme.Label(new Rect(left + 26f, 162f, 440f, 34f),
                matchFound ? "CONNECTING TO MATCH" : edgegapSearching ? "SEARCHING FOR PLAYERS" : "PREPARE YOUR SQUAD", theme.Heading);

            theme.Label(new Rect(left + 490f, 146f, 240f, 24f),
                $"SQUAD SLOTS  {online.PartyPlayerCount} / {online.PartyCapacity}", theme.Body);
            theme.Label(new Rect(left + 490f, 172f, 240f, 20f),
                "4v4  •  AUTO TEAM FILL", theme.Small, true);
            theme.StatusBadge(new Rect(left + width - 388f, 154f, 196f, 34f),
                matchFound ? "MATCH FOUND" : edgegapSearching ? "FINDING MATCH" : "MATCH LOBBY",
                matchFound ? FrontEndTheme.Accent : edgegapSearching ? FrontEndTheme.Positive : FrontEndTheme.Zero);

            if (theme.Action(new Rect(left + width - 178f, 149f, 154f, 44f),
                "LEAVE PARTY", enabled: !online.Busy))
            {
                if (edgegap != null) _ = edgegap.LeavePartyAsync();
                else _ = online.LeavePartyAsync();
            }

            if (edgegapSearching)
            {
                float pulse = 0.2f + 0.8f * Mathf.PingPong(Time.unscaledTime * 1.35f, 1f);
                theme.Progress(new Rect(statusDeck.x + 24f, statusDeck.yMax - 8f, statusDeck.width - 48f, 4f), pulse);
            }

            // 4-PLAYER SQUAD ROSTER CARDS
            const int slots = OnlinePartyService.MaxPartyPlayers;
            const float playerGap = 14f;
            float playerW = (width - playerGap * (slots - 1)) / slots;
            IReadOnlyList<OnlinePartyService.MemberView> members = online.Members;

            for (int slot = 0; slot < slots; slot++)
            {
                float x = left + slot * (playerW + playerGap);
                Rect cardRect = new(x, 222f, playerW, 252f);
                theme.Panel(cardRect);

                if (slot >= members.Count)
                {
                    theme.Label(new Rect(x + 18f, 238f, playerW - 36f, 20f), $"SQUAD SLOT 0{slot + 1}", theme.Small, true);
                    Rect openBadge = new(x + 24f, 316f, playerW - 48f, 38f);
                    theme.StatusBadge(openBadge, edgegapSearching ? "RESERVED" : "OPEN SLOT",
                        edgegapSearching ? FrontEndTheme.Positive : FrontEndTheme.Muted);
                    theme.Center(new Rect(x + 14f, 368f, playerW - 28f, 24f),
                        edgegapSearching ? "FINDING A MATCH" : "WAITING FOR PLAYERS");
                    theme.Center(new Rect(x + 14f, 396f, playerW - 28f, 20f),
                        edgegapSearching ? "AUTO-FILL TEAMMATE" : "SHARE INVITE CODE BELOW");
                    continue;
                }

                OnlinePartyService.MemberView member = members[slot];
                Color heroColor = Alterverse.Visuals.StylizedArtDirection.HeroAccent(member.Hero);
                if (member.IsLocal)
                    theme.Frame(cardRect, true);

                string role = member.IsHost ? "PARTY LEADER" : $"SQUAD MEMBER 0{slot + 1}";
                if (member.IsLocal) role += "  •  YOU";
                theme.FitLabel(new Rect(x + 16f, 234f, playerW - 32f, 20f), role, theme.Small, true);

                // Large 3D Hero Mini-Portrait + Clean Player Identity Stack
                Rect miniHero = new(x + 16f, 260f, 84f, 104f);
                StylizedHeroCard.Draw(miniHero, member.Hero, pixel, Time.unscaledTime + slot * .4f);

                float textX = miniHero.xMax + 14f;
                float textW = playerW - (textX - x) - 14f;
                theme.FitLabel(new Rect(textX, 264f, textW, 28f), member.DisplayName, theme.Heading);
                theme.FitLabel(new Rect(textX, 296f, textW, 24f),
                    HeroIdentity.NameFor(member.Hero).ToUpperInvariant(), theme.Body);
                theme.StatusBadge(new Rect(textX, 326f, Mathf.Min(156f, textW), 24f),
                    HeroIdentity.RoleFor(member.Hero).ToString().ToUpperInvariant(), heroColor);

                theme.StatusBadge(new Rect(x + 16f, 376f, playerW - 32f, 28f),
                    member.Ready ? "READY" : "NOT READY",
                    member.Ready ? FrontEndTheme.Positive : FrontEndTheme.Warning);

                if (member.IsLocal)
                {
                    bool editable = online.CanEditParty && !edgegapSearching;
                    if(theme.Action(new Rect(x+16f,414f,playerW*.48f-20f,44f),"CHOOSE HERO",enabled:editable))heroPickerOpen=true;
                    float readyX = x + playerW*.48f;
                    float readyW = playerW*.52f-16f;
                    string readyText = online.LocalReady ? "UNREADY" : "READY";
                    if (theme.Action(new Rect(readyX, 414f, readyW, 44f), readyText, true,
                        enabled: editable && (online.LocalReady || !online.HasHeroConflict)))
                        _ = online.SetLocalReadyAsync(!online.LocalReady);
                }
                else
                {
                    theme.Center(new Rect(x + 16f, 422f, playerW - 32f, 26f),
                        member.Ready ? "LOCKED IN FOR DEPLOYMENT" : "SELECTING LOADOUT...");
                }
            }

            // MATCH LAUNCH & INVITE CODE COMMAND BAR
            Rect commandDeck = new(left, 488f, width, 102f);
            theme.Panel(commandDeck);

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

            float actionY = 511f;
            float searchX = left + width - 488f;
            float startX = left + width - 246f;
            if (theme.Action(new Rect(searchX, actionY, 226f, 56f), searchText, true,
                selected: edgegapSearching, enabled: searchEnabled))
            {
                if (edgegapSearching) _ = edgegap.CancelSearchAsync();
                else _ = edgegap.BeginSearchAsync();
            }

            string autoText = edgegapSearching ? "STARTS WHEN READY" : "4v4 ONLINE";
            theme.StatusBadge(new Rect(startX, actionY + 11f, 220f, 34f),
                autoText, edgegapSearching ? FrontEndTheme.Positive : FrontEndTheme.Zero);

            string partyState = online.HasHeroConflict
                ? "CHOOSE DIFFERENT HEROES"
                : matchFound
                    ? "CONNECTING TO GAME SERVER"
                    : edgegapSearching
                        ? "FINDING OPPONENTS"
                        : online.AllPresentPlayersReady
                            ? "ALL FRIENDS READY  •  START MATCHMAKING"
                            : "MATCH LOBBY  •  READY UP";
            theme.Label(new Rect(left + 26f, 502f, width - 530f, 28f), partyState, theme.Heading);
            theme.Label(new Rect(left + 26f, 532f, width - 530f, 22f),
                $"PARTY {online.PartyPlayerCount}/{online.PartyCapacity}  •  TWO TEAMS OF FOUR  •  HOT ZONE",
                theme.Small, true);
            theme.FitLabel(new Rect(left + 26f, 556f, width - 530f, 20f),
                edgegap != null ? edgegap.Status : "CONNECTING TO MATCHMAKING...", theme.Small, true);

            // INVITE CODE BAR
            Rect inviteBar = new(left, 602f, width, 74f);
            theme.Panel(inviteBar);

            string codeValue = online.JoinCode.Length > 0 ? online.JoinCode : "WAITING";
            theme.Label(new Rect(left + 26f, 616f, 200f, 24f), "FRIEND INVITE CODE", theme.Small, true);
            theme.StatusBadge(new Rect(left + 220f, 612f, 196f, 32f), codeValue,
                online.JoinCode.Length > 0 ? FrontEndTheme.Accent : FrontEndTheme.Muted);

            if (theme.Action(new Rect(left + width - 234f, 610f, 208f, 38f),
                "INVITE FRIENDS", enabled: online.JoinCode.Length > 0 && !online.MatchmakingLocked))
                {invitePanelOpen=true;inviteInbox=false;}

            if(theme.Action(new Rect(left+width-456f,610f,210f,38f),"INVITE TO GAME",enabled:!GameInviteShare.Busy))
                _=GameInviteShare.ShareAsync();
            string state;
            if (edgegap != null && !string.IsNullOrEmpty(edgegap.LastError)) state = edgegap.LastError;
            else if (!string.IsNullOrEmpty(online.LastError)) state = online.LastError;
            else state = GameInviteShare.Status.Length>0?GameInviteShare.Status:DedicatedOnlineMatchHandshake.Status;
            theme.FitLabel(new Rect(left + 26f, 646f, width - 280f, 22f),
                string.IsNullOrEmpty(state) ? "SHARE YOUR INVITE CODE WITH UP TO 3 FRIENDS TO LOCK IN ON THE SAME TEAM" : state,
                theme.Small, true);
        }

        private void DrawHeroPicker(float menuWidth,OnlinePartyService online,EdgegapOnlineFlow edgegap)
        {
            FrontEndTheme.Fill(new Rect(0,0,menuWidth,ReferenceHeight),new Color(0,0,0,.84f));
            float width=Mathf.Min(1180,menuWidth-80),left=(menuWidth-width)/2;
            Rect panel=new(left,210,width,290);theme.Panel(panel);
            theme.Label(new Rect(left+24,230,width-48,32),"CHOOSE HERO",theme.Heading);
            theme.Label(new Rect(left+24,267,width-48,24),"One hero per team. Locked and taken heroes cannot be selected.",theme.Small,true);
            HeroId selected=online.InParty?online.LocalHero:PlayerHeroSelection.Selected;
            bool editable=!online.Busy&&edgegap?.IsSearching!=true&&(!online.InParty||online.CanEditParty);
            LobbyHeroPicker.Draw(theme,new Rect(left+24,305,width-48,125),selected,editable,
                hero=>!online.InParty||!online.HeroBlockedForLocal(hero),hero=>_=PickHeroAsync(online,hero));
            if(theme.Action(new Rect(left+width-190,447,166,36),"CLOSE"))heroPickerOpen=false;
        }
        private async System.Threading.Tasks.Task PickHeroAsync(OnlinePartyService online,HeroId hero)
        {
            if(online.InParty&&!await online.SetLocalHeroAsync(hero))return;
            MainMenuController.SelectLobbyHero(hero);heroPickerOpen=false;
        }

        private void Update()
        {
            if(!open||!MainMenuController.IsVisible)return;
            var social=SocialService.Instance;
            if(social==null||social.Busy||!social.Connected||Alterverse.Shop.PaidShardCommerce.Instance?.Ready!=true)return;
            if(consumedInvitation!=null)
            {social.Load("/social/party/decline",new SocialService.Request{inviteId=consumedInvitation});consumedInvitation=null;return;}
            if(Time.unscaledTime>=nextFriendPoll)
            {nextFriendPoll=Time.unscaledTime+8f;social.Load("/social/friend/state");}
        }
        private void DrawInvitations(float menuWidth,OnlinePartyService online,EdgegapOnlineFlow edgegap)
        {
            FrontEndTheme.Fill(new Rect(0,0,menuWidth,ReferenceHeight),new Color(0,0,0,.88f));
            float width=Mathf.Min(900,menuWidth-80),left=(menuWidth-width)/2;
            theme.Panel(new Rect(left,160,width,440));
            if(theme.Action(new Rect(left+20,180,250,40),"INVITE FRIENDS",selected:!inviteInbox))inviteInbox=false;
            if(theme.Action(new Rect(left+282,180,250,40),"RECEIVED INVITATIONS",selected:inviteInbox))inviteInbox=true;
            if(theme.Action(new Rect(left+width-140,180,120,40),"CLOSE"))invitePanelOpen=false;
            var social=SocialService.Instance;
            bool enabled=social!=null&&social.Connected&&!social.Busy&&!online.Busy&&edgegap?.IsSearching!=true;
            var friends=social?.Friends?.friends??System.Array.Empty<SocialService.Player>();
            var incoming=social?.Friends?.partyInvites??System.Array.Empty<SocialService.PartyInvitation>();
            int count=inviteInbox?incoming.Length:friends.Length;
            Rect viewport=new(left+20,240,width-40,275);
            invitationScroll=GUI.BeginScrollView(viewport,invitationScroll,new Rect(0,0,viewport.width-20,Mathf.Max(275,count*66)));
            if(count==0)theme.Label(new Rect(12,15,viewport.width-40,55),inviteInbox?"NO INVITATIONS":"NO ACCEPTED FRIENDS YET",theme.Body,true);
            for(int i=0;i<count;i++)
            {
                float y=i*66;theme.Panel(new Rect(0,y,viewport.width-20,58));
                if(inviteInbox)
                {
                    var invite=incoming[i];
                    theme.FitLabel(new Rect(12,y+12,viewport.width-330,30),invite.senderName,theme.Body);
                    bool fresh=invite.expiresAtUnix>System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if(theme.Action(new Rect(viewport.width-306,y+9,132,40),"JOIN PARTY",true,enabled:enabled&&fresh))_=AcceptInvitation(online,invite);
                    if(theme.Action(new Rect(viewport.width-164,y+9,132,40),"DECLINE",enabled:enabled))
                        social.Load("/social/party/decline",new SocialService.Request{inviteId=invite.inviteId});
                }
                else
                {
                    var friend=friends[i];theme.FitLabel(new Rect(12,y+12,viewport.width-200,30),friend.name,theme.Body);
                    if(theme.Action(new Rect(viewport.width-174,y+9,142,40),"INVITE",true,
                        enabled:enabled&&online.InParty&&online.IsHost&&!online.IsPartyFull&&!online.MatchmakingLocked))
                        social.Load("/social/party/invite",new SocialService.Request{playerId=friend.playerId,partyCode=online.JoinCode,partySessionId=online.VoiceRoomId});
                }
            }
            GUI.EndScrollView();
            theme.Label(new Rect(left+24,530,width-48,48),!inviteInbox&&!online.InParty?"CREATE A PARTY FIRST":social?.Status??"Sign in to load friends and clans.",theme.Small,true);
        }
        private async System.Threading.Tasks.Task AcceptInvitation(OnlinePartyService online,SocialService.PartyInvitation invite)
        {
            if(await online.JoinByCodeAsync(invite.partyCode))
            {consumedInvitation=invite.inviteId;invitePanelOpen=false;}
        }

        private void DrawVoiceConsent(float menuWidth)
        {
            var voice=PartyVoiceService.Instance;
            FrontEndTheme.Fill(new Rect(0,0,menuWidth,ReferenceHeight),new Color(0,0,0,.88f));
            float width=Mathf.Min(760,menuWidth-80),left=(menuWidth-width)/2;
            theme.Panel(new Rect(left,220,width,265));
            theme.Label(new Rect(left+24,242,width-48,36),"USE AN OPEN MICROPHONE?",theme.Heading);
            theme.Label(new Rect(left+24,292,width-48,75),
                "Your party can hear you while the microphone is on. You can mute it at any time. Playing without voice is always possible.",theme.Body,true);
            if(theme.Action(new Rect(left+24,394,(width-60)/2,58),"WITHOUT MICROPHONE"))voice.Decline();
            if(theme.Action(new Rect(left+36+(width-60)/2,394,(width-60)/2,58),"ENABLE MICROPHONE",true))_=voice.EnableAsync();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
