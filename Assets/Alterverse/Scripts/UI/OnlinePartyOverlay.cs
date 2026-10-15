using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Online 5v5 party surface. Friends assemble privately through the Unity lobby/Relay link.
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
            if (!MainMenuController.IsVisible || !open) return;

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
            FrontEndTheme.Fill(new Rect(0f, 0f, menuWidth, ReferenceHeight), new Color(.005f, .009f, .016f, .94f));

            float width = Mathf.Min(1180f, menuWidth - 120f);
            float left = (menuWidth - width) * .5f;
            Rect shell = new(left, 54f, width, 612f);
            theme.Panel(shell);

            theme.Label(new Rect(left + 28f, 70f, width - 220f, 38f), "MATCHMAKING", theme.Title);
            theme.Label(new Rect(left + 30f, 110f, width - 250f, 24f),
                "5v5 • Automatic matchmaking or invite friends into a private party.",
                theme.Small, true);

            if (theme.Action(new Rect(left + width - 158f, 72f, 126f, 42f), "CLOSE"))
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
            bool busy = online.Busy || (edgegap != null && edgegap.Busy);
            theme.Panel(new Rect(left + 27, 158, width - 54, 406));
            theme.Label(new Rect(left + 54, 181, width - 108, 35),
                "PLAY ONLINE OR INVITE FRIENDS", theme.Heading);
            theme.Label(new Rect(left + 54, 226, width - 108, 34),
                "Match automatically, or create a private party (up to 5 friends on one team).",
                theme.Body, true);
            if (theme.Action(new Rect(left + 54, 276, width - 108, 54),
                busy ? "SEARCHING..." : "FIND MATCH", true,
                enabled: !busy && edgegap != null))
                _ = edgegap.QuickSearchAsync();
            if (theme.Action(new Rect(left + 54, 344, width - 108, 50),
                "CREATE FRIEND PARTY", enabled: !busy))
                _ = online.CreatePrivatePartyAsync();
            theme.Label(new Rect(left + 54, 406, width - 108, 24),
                "HAVE AN INVITE CODE?", theme.Small, true);
            friendInviteCode = GUI.TextField(new Rect(left + 54, 439, width - 334, 48),
                friendInviteCode ?? "", 16, theme.Field);
            if (theme.Action(new Rect(left + width - 264, 439, 210, 48), "JOIN PARTY",
                enabled: !busy && OnlinePartyService.NormalizeJoinCode(friendInviteCode).Length >= 4))
                _ = online.JoinByCodeAsync(OnlinePartyService.NormalizeJoinCode(friendInviteCode));
            string state = !string.IsNullOrEmpty(online.LastError) ? online.LastError :
                edgegap != null && !string.IsNullOrEmpty(edgegap.LastError) ?
                edgegap.LastError : online.Status;
            theme.FitLabel(new Rect(left + 54, 522, width - 108, 26), state, theme.Small, true);
        }

        private void DrawParty(float left, float width, OnlinePartyService online, EdgegapOnlineFlow edgegap)
        {
            bool edgegapSearching = edgegap != null && edgegap.IsSearching;
            bool busy = online.Busy || (edgegap != null && edgegap.Busy);

            theme.Panel(new Rect(left + 26f, 150f, width - 52f, 84f));
            theme.Label(new Rect(left + 46f, 164f, 420f, 20f), "MATCH STATUS", theme.Small, true);
            theme.Label(new Rect(left + 46f, 188f, 420f, 30f),
                edgegapSearching ? "SEARCHING FOR PLAYERS" : "PREPARING MATCH", theme.Heading);

            theme.Label(new Rect(left + 500f, 166f, 250f, 22f),
                $"PLAYERS  {online.PartyPlayerCount} / {online.PartyCapacity}", theme.Small, true);
            theme.Label(new Rect(left + 500f, 190f, 250f, 22f),
                "5v5  •  AUTO TEAM FILL", theme.Small, true);
            theme.StatusBadge(new Rect(left + 758f, 176f, 198f, 26f),
                edgegapSearching ? "FINDING YOUR MATCH" : "MATCH LOBBY",edgegapSearching?FrontEndTheme.Positive:FrontEndTheme.Muted);

            if (theme.Action(new Rect(left + width - 200f, 172f, 150f, 42f),
                busy ? "PLEASE WAIT" : "LEAVE PARTY", enabled: !busy))
            {
                if (edgegap != null) _ = edgegap.LeavePartyAsync();
                else _ = online.LeavePartyAsync();
            }

            const int slots = OnlinePartyService.MaxPartyPlayers;
            const float playerGap = 8f;
            float innerWidth = width - 52f;
            float playerW = (innerWidth - playerGap * (slots - 1)) / slots;
            IReadOnlyList<OnlinePartyService.MemberView> members = online.Members;

            for (int slot = 0; slot < slots; slot++)
            {
                float x = left + 26f + slot * (playerW + playerGap);
                theme.Panel(new Rect(x, 250f, playerW, 190f));

                if (slot >= members.Count)
                {
                    theme.Label(new Rect(x + 10f, 262f, playerW - 20f, 20f), $"SLOT {slot + 1}", theme.Small, true);
                    theme.Center(new Rect(x + 10f, 312f, playerW - 20f, 30f), edgegapSearching ? "RESERVED" : "OPEN SLOT");
                    theme.Label(new Rect(x + 10f, 382f, playerW - 20f, 20f),
                        edgegapSearching ? "FINDING A MATCH" : "WAITING FOR PLAYERS", theme.Small, true);
                    continue;
                }

                OnlinePartyService.MemberView member = members[slot];
                string role = member.IsHost ? "HOST" : $"PLAYER {slot + 1}";
                if (member.IsLocal) role += " • YOU";
                if(member.IsLocal)theme.Frame(new Rect(x,250,playerW,190),true);
                theme.Label(new Rect(x + 10f, 262f, playerW - 20f, 20f), role, theme.Small, true);
                theme.IdentityTile(new Rect(x+10,287,30,30),member.DisplayName,member.IsLocal);
                theme.FitLabel(new Rect(x + 48f, 286f, playerW - 58f, 28f), member.DisplayName, theme.Body);
                theme.FitLabel(new Rect(x + 10f, 320f, playerW - 20f, 20f),
                    HeroIdentity.NameFor(member.Hero).ToUpperInvariant(), theme.Small, true);
                theme.StatusBadge(new Rect(x + 10f, 344f, playerW - 20f, 26f), member.Ready ? "READY" : "NOT READY",member.Ready?FrontEndTheme.Positive:FrontEndTheme.Warning);

                if (member.IsLocal)
                {
                    bool editable = !busy && !edgegapSearching && !OnlinePlanHandshake.Preparing;
                    if (theme.Action(new Rect(x + 8f, 382f, 32f, 34f), "<", enabled: editable))
                        _ = online.CycleLocalHeroAsync(-1);
                    if (theme.Action(new Rect(x + playerW - 40f, 382f, 32f, 34f), ">", enabled: editable))
                        _ = online.CycleLocalHeroAsync(1);

                    float readyX = x + 45f;
                    float readyW = Mathf.Max(38f, playerW - 90f);
                    string readyText = online.LocalReady ? "UNREADY" : "READY";
                    if (theme.Action(new Rect(readyX, 382f, readyW, 34f), readyText, true,
                        enabled: editable && (online.LocalReady || !online.HasHeroConflict)))
                        _ = online.SetLocalReadyAsync(!online.LocalReady);
                }
            }

            theme.Panel(new Rect(left + 26f, 456f, width - 52f, 112f));

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
                searchEnabled = !busy;
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

            float actionY = 474f;
            float searchX = left + width - 490f;
            float startX = left + width - 260f;
            if (theme.Action(new Rect(searchX, actionY, 212f, 54f), searchText, true,
                selected: edgegapSearching, enabled: searchEnabled))
            {
                if (edgegapSearching) _ = edgegap.CancelSearchAsync();
                else _ = edgegap.BeginSearchAsync();
            }

            string autoText = edgegapSearching ? "STARTS WHEN READY" : "5v5 ONLINE";
            theme.StatusBadge(new Rect(startX, actionY+12, 208f, 30f),autoText,edgegapSearching?FrontEndTheme.Positive:FrontEndTheme.Muted);

            string partyState = online.HasHeroConflict
                ? "CHOOSE DIFFERENT HEROES"
                : edgegapSearching
                    ? "FINDING OPPONENTS"
                    : online.AllPresentPlayersReady
                        ? "ALL FRIENDS READY • START MATCHMAKING"
                        : "MATCH LOBBY • READY UP";
            theme.Label(new Rect(left + 48f, 470f, width - 560f, 26f), partyState, theme.Small, true);
            theme.Label(new Rect(left + 48f, 502f, width - 560f, 22f),
                $"PARTY {online.PartyPlayerCount}/{online.PartyCapacity}  •  TWO TEAMS OF FIVE",
                theme.Small, true);
            theme.Label(new Rect(left + 48f, 532f, width - 560f, 20f),
                edgegap != null ? edgegap.Status : "CONNECTING TO MATCHMAKING...", theme.Small, true);

            string state;
            if (edgegap != null && !string.IsNullOrEmpty(edgegap.LastError)) state = edgegap.LastError;
            else if (!string.IsNullOrEmpty(online.LastError)) state = online.LastError;
            else state = DedicatedOnlineMatchHandshake.Status;
            theme.Label(new Rect(left + 28f, 579f, 420f, 26f),
                "FRIEND INVITE CODE  " + (online.JoinCode.Length > 0 ? online.JoinCode : "WAITING"),
                theme.Body, true);
            if (theme.Action(new Rect(left + width - 225f, 578f, 195f, 40f),
                "COPY INVITE CODE", enabled: online.JoinCode.Length > 0))
                GUIUtility.systemCopyBuffer = online.JoinCode;
            theme.FitLabel(new Rect(left + 28f, 630f, width - 56f, 24f),
                state, theme.Small, true);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}

