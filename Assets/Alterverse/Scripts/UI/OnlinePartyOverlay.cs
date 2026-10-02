using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Online-party surface: friends join privately by code first, the leader explicitly opens
    /// public search, and the leader can start once every currently connected human is ready.
    /// Remaining 5v5 slots are filled by bots by the authoritative online match plan.
    /// </summary>
    public sealed class OnlinePartyOverlay : MonoBehaviour
    {
        private static OnlinePartyOverlay instance;
        private FrontEndTheme theme;
        private bool open;
        private string joinCodeInput = "";
        private const float ReferenceHeight = 720f;
        private const float ReferenceWidth = 1500f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance == null)
                new GameObject("DF_OnlinePartyOverlay").AddComponent<OnlinePartyOverlay>();
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
            if (!MainMenuController.IsVisible) return;

            OnlinePartyService online = OnlinePartyService.Instance;
            if (online == null) return;

            bool coopActive = CoopLobbyService.Instance != null && CoopLobbyService.Instance.InLobby;
            if (coopActive && !online.InParty)
            {
                open = false;
                return;
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            try
            {
                Rect safe = Screen.safeArea;
                if (safe.width <= 0f || safe.height <= 0f) return;

                float scale = Mathf.Min(safe.height / ReferenceHeight, safe.width / ReferenceWidth);
                float menuWidth = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - ReferenceHeight * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0f), Quaternion.identity, Vector3.one * scale);
                GUI.color = Color.white;
                GUI.enabled = true;
                theme ??= new FrontEndTheme();

                if (!open)
                {
                    string label = online.InParty
                        ? (online.IsSearching ? "ONLINE  •  SUCHE AKTIV" : "ONLINE  •  PARTY")
                        : "ONLINE 5v5";
                    if (theme.Action(new Rect(menuWidth - 274f, 648f, 238f, 48f), label, true,
                        selected: online.InParty || online.IsSearching))
                        open = true;
                    return;
                }

                DrawOverlay(menuWidth, online);
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.enabled = oldEnabled;
            }
        }

        private void DrawOverlay(float menuWidth, OnlinePartyService online)
        {
            FrontEndTheme.Fill(new Rect(0f, 0f, menuWidth, ReferenceHeight), new Color(.005f, .009f, .016f, .94f));

            float width = Mathf.Min(1180f, menuWidth - 120f);
            float left = (menuWidth - width) * .5f;
            Rect shell = new(left, 54f, width, 612f);
            theme.Panel(shell);

            theme.Label(new Rect(left + 28f, 70f, width - 220f, 38f), "ONLINE 5v5 PARTY", theme.Title);
            theme.Label(new Rect(left + 30f, 110f, width - 250f, 24f),
                "Invite friends first. Then search public players. Start anytime when everyone present is ready; bots fill open match slots.",
                theme.Small, true);

            if (theme.Action(new Rect(left + width - 158f, 72f, 126f, 42f), "CLOSE"))
            {
                open = false;
                return;
            }

            if (!online.InParty)
            {
                DrawEntry(left, width, online);
                return;
            }

            DrawParty(left, width, online);
        }

        private void DrawEntry(float left, float width, OnlinePartyService online)
        {
            float gap = 22f;
            float panelW = (width - 76f - gap) * .5f;
            float firstX = left + 27f;
            float secondX = firstX + panelW + gap;

            theme.Panel(new Rect(firstX, 158f, panelW, 318f));
            theme.Label(new Rect(firstX + 24f, 176f, panelW - 48f, 30f), "FRIENDS FIRST", theme.Heading);
            theme.Label(new Rect(firstX + 24f, 214f, panelW - 48f, 66f),
                "Create a private party, send the code to up to four friends, then start public player search when your group is ready.",
                theme.Body, true);
            if (theme.Action(new Rect(firstX + 24f, 302f, panelW - 48f, 54f),
                online.Busy ? "CONNECTING..." : "CREATE PRIVATE PARTY", true, enabled: !online.Busy))
                _ = online.CreatePrivatePartyAsync();

            theme.Label(new Rect(firstX + 24f, 378f, panelW - 48f, 22f), "SOLO", theme.Small, true);
            if (theme.Action(new Rect(firstX + 24f, 410f, panelW - 48f, 44f),
                online.Busy ? "SEARCHING..." : "QUICK SEARCH", enabled: !online.Busy))
                _ = online.QuickSearchAsync();

            theme.Panel(new Rect(secondX, 158f, panelW, 318f));
            theme.Label(new Rect(secondX + 24f, 176f, panelW - 48f, 30f), "JOIN FRIEND", theme.Heading);
            theme.Label(new Rect(secondX + 24f, 214f, panelW - 48f, 48f),
                "Enter the private party code your friend sent you.", theme.Body, true);

            GUI.SetNextControlName("DF_OnlineJoinCode");
            joinCodeInput = OnlinePartyService.NormalizeJoinCode(
                GUI.TextField(new Rect(secondX + 24f, 286f, panelW - 48f, 52f), joinCodeInput, 12, theme.Field));
            theme.Center(new Rect(secondX + 24f, 344f, panelW - 48f, 22f),
                string.IsNullOrEmpty(joinCodeInput) ? "ENTER CODE" : joinCodeInput);

            if (theme.Action(new Rect(secondX + 24f, 390f, panelW - 48f, 64f),
                online.Busy ? "CONNECTING..." : "JOIN PRIVATE PARTY", true,
                enabled: !online.Busy && joinCodeInput.Length >= 4))
                _ = online.JoinByCodeAsync(joinCodeInput);

            string state = string.IsNullOrEmpty(online.LastError)
                ? online.Status
                : online.Status + "  •  " + online.LastError;
            theme.Label(new Rect(left + 28f, 506f, width - 56f, 36f), state, theme.Small, true);
            theme.Label(new Rect(left + 28f, 552f, width - 56f, 54f),
                "PRIVATE FRIEND PARTY 5 MAX  •  ONLINE MATCH 10 MAX  •  PUBLIC SEARCH ONLY ON COMMAND",
                theme.Small, true);
        }

        private void DrawParty(float left, float width, OnlinePartyService online)
        {
            theme.Panel(new Rect(left + 26f, 150f, width - 52f, 84f));
            theme.Label(new Rect(left + 46f, 164f, 190f, 20f), online.IsHost ? "PARTY LEADER" : "FRIEND PARTY", theme.Small, true);
            theme.Label(new Rect(left + 46f, 188f, 300f, 30f), "CODE  " + online.JoinCode, theme.Heading);

            if (theme.Action(new Rect(left + 354f, 172f, 122f, 42f), "COPY CODE",
                enabled: !string.IsNullOrEmpty(online.JoinCode)))
            {
                GUIUtility.systemCopyBuffer = online.JoinCode;
                joinCodeInput = online.JoinCode;
            }

            theme.Label(new Rect(left + 500f, 166f, 250f, 22f),
                $"FRIENDS  {online.PartyPlayerCount} / {online.PartyCapacity}", theme.Small, true);
            theme.Label(new Rect(left + 500f, 190f, 250f, 22f),
                $"MATCH  {online.PlayerCount} / {online.MatchCapacity}", theme.Small, true);
            theme.Label(new Rect(left + 748f, 178f, 220f, 20f),
                online.IsSearching ? "PUBLIC SEARCH ACTIVE" : "PRIVATE / FRIENDS FIRST", theme.Small, true);

            if (theme.Action(new Rect(left + width - 200f, 172f, 150f, 42f),
                online.Busy ? "PLEASE WAIT" : "LEAVE PARTY", enabled: !online.Busy))
                _ = online.LeavePartyAsync();

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
                    theme.Center(new Rect(x + 10f, 312f, playerW - 20f, 30f), online.IsSearching ? "SEARCHING..." : "OPEN");
                    theme.Label(new Rect(x + 10f, 382f, playerW - 20f, 20f),
                        online.IsSearching ? "PUBLIC" : "FRIEND CODE", theme.Small, true);
                    continue;
                }

                OnlinePartyService.MemberView member = members[slot];
                string role = member.IsHost ? "LEADER" : member.IsPartyMember ? $"FRIEND {slot + 1}" : "PUBLIC PLAYER";
                if (member.IsLocal) role += " • YOU";
                theme.Label(new Rect(x + 10f, 262f, playerW - 20f, 20f), role, theme.Small, true);
                theme.FitLabel(new Rect(x + 10f, 286f, playerW - 20f, 24f), member.DisplayName, theme.Heading);
                theme.FitLabel(new Rect(x + 10f, 316f, playerW - 20f, 20f),
                    HeroIdentity.NameFor(member.Hero).ToUpperInvariant(), theme.Small, true);
                theme.Center(new Rect(x + 10f, 344f, playerW - 20f, 20f), member.Ready ? "READY" : "NOT READY");

                if (member.IsLocal)
                {
                    bool editable = !online.Busy && !OnlinePlanHandshake.Preparing;
                    if (theme.Action(new Rect(x + 8f, 382f, 32f, 34f), "<", enabled: editable))
                        _ = online.CycleLocalHeroAsync(-1);
                    if (theme.Action(new Rect(x + playerW - 40f, 382f, 32f, 34f), ">", enabled: editable))
                        _ = online.CycleLocalHeroAsync(1);

                    float readyX = x + 45f;
                    float readyW = Mathf.Max(38f, playerW - 90f);
                    string readyText = online.LocalReady ? "UNREADY" : "READY";
                    if (theme.Action(new Rect(readyX, 382f, readyW, 34f), readyText, true,
                        enabled: editable && !online.HasHeroConflict))
                        _ = online.SetLocalReadyAsync(!online.LocalReady);
                }
            }

            theme.Panel(new Rect(left + 26f, 456f, width - 52f, 112f));

            string searchText;
            bool searchEnabled;
            if (!online.IsHost)
            {
                searchText = online.IsSearching ? "LEADER IS SEARCHING" : "WAITING FOR LEADER";
                searchEnabled = false;
            }
            else if (online.IsFull)
            {
                searchText = "MATCH FULL";
                searchEnabled = false;
            }
            else if (online.IsSearching)
            {
                searchText = "SUCHE ABBRECHEN";
                searchEnabled = !online.Busy && !OnlinePlanHandshake.Preparing;
            }
            else
            {
                searchText = "SPIELER SUCHEN";
                searchEnabled = !online.Busy && !OnlinePlanHandshake.Preparing;
            }

            float actionY = 474f;
            float searchX = left + width - 490f;
            float startX = left + width - 260f;
            if (theme.Action(new Rect(searchX, actionY, 212f, 54f), searchText, false,
                selected: online.IsSearching, enabled: searchEnabled))
            {
                if (online.IsSearching) _ = online.CancelPlayerSearchAsync();
                else _ = online.BeginPlayerSearchAsync();
            }

            string startText;
            bool startEnabled;
            if (!online.IsHost)
            {
                startText = "LEADER STARTS";
                startEnabled = false;
            }
            else if (OnlinePlanHandshake.Preparing)
            {
                startText = "SYNCING...";
                startEnabled = false;
            }
            else if (online.HasHeroConflict)
            {
                startText = "HERO CONFLICT";
                startEnabled = false;
            }
            else if (!online.AllPresentPlayersReady)
            {
                startText = "WAITING READY";
                startEnabled = false;
            }
            else
            {
                startText = "MATCH STARTEN";
                startEnabled = !online.Busy;
            }

            if (theme.Action(new Rect(startX, actionY, 208f, 54f), startText, true,
                enabled: startEnabled))
                _ = OnlinePlanHandshake.StartOnlineMatchAsync();

            string partyState = online.HasHeroConflict
                ? "FRIEND PARTY: CHOOSE DIFFERENT HEROES"
                : online.AllPresentPlayersReady
                    ? "ALL CONNECTED PLAYERS READY • OPEN SLOTS WILL BECOME BOTS"
                    : online.IsSearching
                        ? "SEARCHING PUBLIC PLAYERS • FRIEND PARTY STAYS TOGETHER"
                        : "PRIVATE PARTY • INVITE FRIENDS OR START PLAYER SEARCH";
            theme.Label(new Rect(left + 48f, 470f, width - 560f, 26f), partyState, theme.Small, true);
            theme.Label(new Rect(left + 48f, 502f, width - 560f, 22f),
                $"FRIENDS {online.PartyPlayerCount}/{online.PartyCapacity}  •  PUBLIC {online.PublicPlayerCount}  •  MATCH {online.PlayerCount}/{online.MatchCapacity}",
                theme.Small, true);
            theme.Label(new Rect(left + 48f, 532f, width - 560f, 20f),
                OnlinePlanHandshake.Preparing ? OnlinePlanHandshake.Status : "Bots fill every unoccupied slot when the match starts.",
                theme.Small, true);

            string state = string.IsNullOrEmpty(online.LastError)
                ? online.Status
                : online.Status + "  •  " + online.LastError;
            theme.Label(new Rect(left + 28f, 588f, width - 56f, 26f), state, theme.Small, true);
            theme.Label(new Rect(left + 28f, 618f, width - 56f, 22f),
                "FRIENDS FIRST  •  LEADER CONTROLS PUBLIC SEARCH  •  PARTY STAYS TOGETHER  •  BOT FILL ENABLED",
                theme.Small, true);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
