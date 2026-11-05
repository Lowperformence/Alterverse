using System;
using System.Collections.Generic;
using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Profile
{
    /// <summary>
    /// Full Friends, In-Match Friend Requests, Clan Invites, and Multi-Tier Sticker Gifting & Shard Salvage service.
    /// Persists in AccountProfilePrefs (cloud-synced per account) and bridges with SocialService.
    /// </summary>
    public static class FriendStickerService
    {
        private const string SaveKey = "DF_FriendStickerState_v1";

        [Serializable]
        public sealed class StickerCatalogItem
        {
            public int id;
            public string name;
            public string subtitle;
            public int priceShards;
            public int salvageShards;
            public Color primaryColor;
            public Color accentColor;
        }

        [Serializable]
        public sealed class FriendEntry
        {
            public string playerId;
            public string name;
            public int hero;
            public int level;
            public string clanTag;
            public string clanName;
            public bool online;
            public int stickersSent;
        }

        [Serializable]
        public sealed class ReceivedStickerGift
        {
            public string giftId;
            public string senderName;
            public int senderHero;
            public int stickerId;
            public long receivedAtUnix;
        }

        [Serializable]
        public sealed class ClanInviteEntry
        {
            public string inviteId;
            public string clanId;
            public string clanTag;
            public string clanName;
            public string inviterName;
            public int inviterHero;
            public bool outgoing;
        }

        [Serializable]
        private sealed class PersistentState
        {
            public List<FriendEntry> friends = new();
            public List<FriendEntry> incomingRequests = new();
            public List<FriendEntry> outgoingRequests = new();
            public List<ReceivedStickerGift> receivedStickers = new();
            public List<ClanInviteEntry> clanInvites = new();
            public bool seeded;
        }

        public static readonly StickerCatalogItem[] Stickers =
        {
            new()
            {
                id = 0,
                name = "GG STAR",
                subtitle = "TACTICAL SALUTE EMBLEM",
                priceShards = 50,
                salvageShards = 10,
                primaryColor = new Color(.28f, .78f, .96f, 1f),
                accentColor = new Color(.96f, .86f, .44f, 1f)
            },
            new()
            {
                id = 1,
                name = "COSMIC HEART",
                subtitle = "ORBITAL FRIENDSHIP CORE",
                priceShards = 100,
                salvageShards = 20,
                primaryColor = new Color(.94f, .36f, .58f, 1f),
                accentColor = new Color(1f, .78f, .88f, 1f)
            },
            new()
            {
                id = 2,
                name = "VICTORY FLAME",
                subtitle = "SOLAR IGNITION CREST",
                priceShards = 250,
                salvageShards = 50,
                primaryColor = new Color(.98f, .52f, .18f, 1f),
                accentColor = new Color(1f, .88f, .36f, 1f)
            },
            new()
            {
                id = 3,
                name = "CHRONO CROWN",
                subtitle = "SOVEREIGN RIFT INSIGNIA",
                priceShards = 500,
                salvageShards = 100,
                primaryColor = new Color(.95f, .76f, .28f, 1f),
                accentColor = new Color(.44f, .92f, .86f, 1f)
            },
            new()
            {
                id = 4,
                name = "VOID DRAGON",
                subtitle = "ABYSSAL VANGUARD SEAL",
                priceShards = 1000,
                salvageShards = 200,
                primaryColor = new Color(.64f, .36f, .94f, 1f),
                accentColor = new Color(.36f, .88f, 1f, 1f)
            },
            new()
            {
                id = 5,
                name = "OMEGA NOVA",
                subtitle = "MYTHIC CELESTIAL STARBURST",
                priceShards = 2500,
                salvageShards = 500,
                primaryColor = new Color(.98f, .42f, .28f, 1f),
                accentColor = new Color(1f, .92f, .62f, 1f)
            }
        };

        private static PersistentState state;
        private static string loadedAccount;
        private static readonly Dictionary<int, Texture2D> stickerIcons = new();
        private static readonly HashSet<string> sessionRequested = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> mutedPlayers = new(StringComparer.OrdinalIgnoreCase);
        private static string localStatus = "Connect your account to load friends and gifts.";
        private static bool showServerStatus;
        private static SocialService.Snapshot appliedFriends;
        private static float nextFriendsRefresh;
        public static bool Busy => SocialService.Instance?.Busy == true || PaidShardCommerce.Spending;
        public static bool Ready => SocialService.Instance?.Connected == true && SocialService.Instance.Friends?.stickerProtocolVersion == 1 && PaidShardCommerce.Instance?.Ready == true;
        public static string Status
        {
            get => showServerStatus && SocialService.Instance != null ? SocialService.Instance.Status : localStatus;
            private set { localStatus = value; showServerStatus = false; }
        }

        public static IReadOnlyList<FriendEntry> Friends
        {
            get { EnsureLoaded(); return state.friends; }
        }

        public static IReadOnlyList<FriendEntry> IncomingRequests
        {
            get { EnsureLoaded(); return state.incomingRequests; }
        }

        public static IReadOnlyList<FriendEntry> OutgoingRequests
        {
            get { EnsureLoaded(); return state.outgoingRequests; }
        }

        public static IReadOnlyList<ReceivedStickerGift> ReceivedStickers
        {
            get { EnsureLoaded(); return state.receivedStickers; }
        }

        public static IReadOnlyList<ClanInviteEntry> ClanInvites
        {
            get { EnsureLoaded(); return state.clanInvites; }
        }

        public static StickerCatalogItem GetSticker(int id)
        {
            for (int i = 0; i < Stickers.Length; i++)
                if (Stickers[i].id == id) return Stickers[i];
            return Stickers[1];
        }

        public static bool IsFriendOrRequested(string nameOrId)
        {
            if (string.IsNullOrWhiteSpace(nameOrId)) return false;
            EnsureLoaded();
            string clean = nameOrId.Trim();
            if (sessionRequested.Contains(clean)) return true;
            for (int i = 0; i < state.friends.Count; i++)
                if (Matches(state.friends[i], clean)) return true;
            for (int i = 0; i < state.outgoingRequests.Count; i++)
                if (Matches(state.outgoingRequests[i], clean)) return true;
            for (int i = 0; i < state.incomingRequests.Count; i++)
                if (Matches(state.incomingRequests[i], clean)) return true;
            return false;
        }

        private static bool Matches(FriendEntry entry, string query)
        {
            return entry != null &&
                   (string.Equals(entry.playerId, query, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.name, query, StringComparison.OrdinalIgnoreCase));
        }

        public static bool SendFriendRequest(string nameOrId, HeroId hero = HeroId.Veyr, int level = 1)
        {
            if (string.IsNullOrWhiteSpace(nameOrId)) { Status = "Enter a player name or ID."; return false; }
            string clean = nameOrId.Trim();
            bool isId = clean.Length == 20;
            if (isId) foreach (char c in clean) if (!Uri.IsHexDigit(c)) { isId = false; break; }
            return FriendAction("/social/friend/request", new SocialService.Request { playerId = isId ? clean.ToLowerInvariant() : null, query = isId ? null : clean });
        }

        public static void RequestFriendFromScoreboard(string nameOrId, HeroId hero = HeroId.Veyr, int level = 1)
        {
            if (string.IsNullOrWhiteSpace(nameOrId)) return;
            string clean = nameOrId.Trim();
            sessionRequested.Add(clean);
            UiSoundRouter.Confirm();
            SendFriendRequest(clean, hero, level);
        }

        public static bool IsPlayerMuted(string nameOrId)
        {
            return !string.IsNullOrWhiteSpace(nameOrId) && mutedPlayers.Contains(nameOrId.Trim());
        }

        public static bool ToggleMutePlayer(string nameOrId)
        {
            if (string.IsNullOrWhiteSpace(nameOrId)) return false;
            string clean = nameOrId.Trim();
            bool nowMuted;
            if (mutedPlayers.Contains(clean))
            {
                mutedPlayers.Remove(clean);
                nowMuted = false;
            }
            else
            {
                mutedPlayers.Add(clean);
                nowMuted = true;
            }
            UiSoundRouter.Confirm();
            return nowMuted;
        }

        public static bool AcceptFriendRequest(string playerId)
        { return FriendAction("/social/friend/accept", new SocialService.Request { playerId = playerId }); }

        public static bool DeclineFriendRequest(string playerId)
        { return FriendAction("/social/friend/remove", new SocialService.Request { playerId = playerId }); }

        public static bool RemoveFriend(string playerId)
        { return FriendAction("/social/friend/remove", new SocialService.Request { playerId = playerId }); }

        public static bool InviteFriendToClan(string playerId)
        {
            EnsureLoaded();
            FriendEntry friend = null;
            for (int i = 0; i < state.friends.Count; i++)
            {
                if (Matches(state.friends[i], playerId))
                {
                    friend = state.friends[i];
                    break;
                }
            }
            if (friend == null) return false;

            var socialClan = SocialService.Instance?.State?.clan;
            string myTag = socialClan != null && !string.IsNullOrEmpty(socialClan.tag) ? socialClan.tag : "ZERO";
            string myClanName = socialClan != null && !string.IsNullOrEmpty(socialClan.name) ? socialClan.name : "ORBITAL VANGUARD";
            string myClanId = socialClan != null && !string.IsNullOrEmpty(socialClan.clanId) ? socialClan.clanId : "clan_local";

            for (int i = 0; i < state.clanInvites.Count; i++)
            {
                var existing = state.clanInvites[i];
                if (existing.outgoing && string.Equals(existing.inviterName, friend.name, StringComparison.OrdinalIgnoreCase))
                {
                    Status = friend.name + " has already been invited to [" + myTag + "] " + myClanName + ".";
                    return false;
                }
            }

            state.clanInvites.Insert(0, new ClanInviteEntry
            {
                inviteId = Guid.NewGuid().ToString("N"),
                clanId = myClanId,
                clanTag = myTag,
                clanName = myClanName,
                inviterName = friend.name,
                inviterHero = friend.hero,
                outgoing = true
            });
            friend.clanTag = myTag;
            friend.clanName = myClanName;
            Save();
            UiSoundRouter.Confirm();
            Status = "Invited " + friend.name + " to join clan [" + myTag + "] " + myClanName + "!";
            return true;
        }

        public static bool AcceptClanInvite(string inviteId)
        {
            EnsureLoaded();
            for (int i = 0; i < state.clanInvites.Count; i++)
            {
                var inv = state.clanInvites[i];
                if (inv.inviteId != inviteId) continue;
                state.clanInvites.RemoveAt(i);
                Save();
                if (SocialService.Instance != null && SocialService.Instance.Connected && !string.IsNullOrEmpty(inv.clanId))
                {
                    SocialService.Instance.Load("/social/clan/join", new SocialService.Request { clanId = inv.clanId });
                }
                UiSoundRouter.Confirm();
                Status = "Accepted clan invite to [" + inv.clanTag + "] " + inv.clanName + "!";
                return true;
            }
            return false;
        }

        public static bool DeclineClanInvite(string inviteId)
        {
            EnsureLoaded();
            for (int i = 0; i < state.clanInvites.Count; i++)
            {
                if (state.clanInvites[i].inviteId != inviteId) continue;
                string clanName = state.clanInvites[i].clanName;
                state.clanInvites.RemoveAt(i);
                Save();
                Status = "Removed clan invite for " + clanName + ".";
                return true;
            }
            return false;
        }

        public static bool SendStickerToFriend(string playerId, int stickerId)
        {
            EnsureLoaded();
            if (!Ready || Busy) { Status = "Connect and synchronize your account before sending gifts."; return false; }
            bool knownSticker = false;
            foreach (var sticker in Stickers) if (sticker.id == stickerId) { knownSticker = true; break; }
            if (!knownSticker || !state.friends.Exists(friend => friend.playerId == playerId))
            { Status = "Choose an accepted friend and a valid sticker."; return false; }
            showServerStatus = true;
            return SocialService.Instance.SendStickerGift(new SocialService.Request { playerId = playerId, stickerId = stickerId });
        }

        public static bool ConvertReceivedStickerToShards(string giftId)
        { return FriendAction("/social/sticker/claim", new SocialService.Request { giftId = giftId }); }

        private static bool FriendAction(string route, SocialService.Request request)
        {
            EnsureLoaded();
            if (!Ready || Busy) { Status = "Connect and synchronize your account before using friends and gifts."; return false; }
            showServerStatus = true;
            SocialService.Instance.Load(route, request);
            return true;
        }

        private static List<FriendEntry> ServerFriends(SocialService.Player[] players)
        {
            var result = new List<FriendEntry>();
            if (players != null) foreach (var player in players)
                if (player != null && !string.IsNullOrEmpty(player.playerId))
                    result.Add(new FriendEntry { playerId = player.playerId, name = player.name, level = player.level });
            return result;
        }

        private static void SyncServerFriends()
        {
            var service = SocialService.Instance;
            var snapshot = service?.Friends;
            if (!ReferenceEquals(snapshot, appliedFriends))
            {
                appliedFriends = snapshot;
                state.friends = ServerFriends(snapshot?.friends);
                state.incomingRequests = ServerFriends(snapshot?.incoming);
                state.outgoingRequests = ServerFriends(snapshot?.outgoing);
                state.receivedStickers = new List<ReceivedStickerGift>();
                if (snapshot?.stickerProtocolVersion == 1 && snapshot.receivedStickers != null)
                    foreach (var gift in snapshot.receivedStickers)
                        if (gift != null) state.receivedStickers.Add(new ReceivedStickerGift { giftId = gift.giftId, senderName = gift.senderName, senderHero = gift.senderHero, stickerId = gift.stickerId, receivedAtUnix = gift.receivedAtUnix });
                if (snapshot != null && (service.FriendsLastAction == "/social/sticker/send" || service.FriendsLastAction == "/social/sticker/claim"))
                    DimensionAudio.Play(DimensionSound.Purchase, .7f);
                else if (snapshot != null && (service.FriendsLastAction == "/social/friend/request" || service.FriendsLastAction == "/social/friend/accept"))
                    UiSoundRouter.Confirm();
            }
            if (service?.Connected != true || PaidShardCommerce.Instance?.Ready != true || service.Busy || Time.unscaledTime < nextFriendsRefresh) return;
            nextFriendsRefresh = Time.unscaledTime + 15f;
            showServerStatus = true;
            if (service.HasPendingStickerGift) service.SendStickerGift(null);
            else service.Load("/social/friend/state");
        }

        public static Texture2D GetStickerIcon(int stickerId)
        {
            if (stickerIcons.TryGetValue(stickerId, out Texture2D cached) && cached != null)
                return cached;

            StickerCatalogItem item = GetSticker(stickerId);
            const int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DF_Sticker_" + item.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * .5f;
            float maxR = size * .46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxR;
                    float dy = (y - center) / maxR;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > 1.04f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float badgeAlpha = Mathf.Clamp01((1.04f - r) * 14f);
                    Color bg = Color.Lerp(new Color(.04f, .07f, .12f, 1f), item.primaryColor * .45f, Mathf.Clamp01(1f - r * .7f));
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - .88f) * 18f);
                    Color col = Color.Lerp(bg, item.accentColor, ring * .85f);

                    float shape = SampleStickerMotif(stickerId, dx, dy, r);
                    if (shape > 0f)
                    {
                        Color motifCol = Color.Lerp(item.primaryColor, item.accentColor, Mathf.Clamp01(dy * .5f + .5f));
                        motifCol = Color.Lerp(motifCol, Color.white, Mathf.Clamp01((shape - .65f) * 2.2f));
                        col = Color.Lerp(col, motifCol, Mathf.Clamp01(shape));
                    }

                    col.a = badgeAlpha;
                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            stickerIcons[stickerId] = tex;
            return tex;
        }

        private static float SampleStickerMotif(int id, float x, float y, float r)
        {
            switch (id)
            {
                case 0: // 5-pointed star
                {
                    float angle = Mathf.Atan2(y, x) + Mathf.PI * .5f;
                    float starR = .46f + .18f * Mathf.Cos(angle * 5f);
                    return Mathf.Clamp01((starR - r) * 10f);
                }
                case 1: // Cosmic Heart (100 -> 20 Shards)
                {
                    float hx = x * 1.35f;
                    float hy = y * 1.35f - .08f;
                    float a = hx * hx + hy * hy - .32f;
                    float v = a * a * a - hx * hx * hy * hy * hy;
                    return Mathf.Clamp01((.02f - v) * 24f);
                }
                case 2: // Victory Flame
                {
                    float wave = Mathf.Sin(y * 5f) * .08f;
                    float width = Mathf.Clamp01((.65f - y) * .75f) * (.48f - Mathf.Abs(y) * .35f);
                    float d = width - Mathf.Abs(x + wave);
                    return y > -.55f && y < .65f ? Mathf.Clamp01(d * 10f) : 0f;
                }
                case 3: // Chrono Crown
                {
                    if (y < -.32f || y > .38f || Mathf.Abs(x) > .58f) return 0f;
                    float spikes = .12f + .24f * Mathf.Abs(Mathf.Cos(x * 8.2f));
                    return y <= spikes ? Mathf.Clamp01((spikes - y) * 12f) : 0f;
                }
                case 4: // Void Dragon Emblem
                {
                    float ring = 1f - Mathf.Abs(r - .46f) * 11f;
                    float slash = 1f - Mathf.Abs(x + y * .45f) * 12f;
                    return Mathf.Clamp01(Mathf.Max(ring * (r < .62f ? 1f : 0f), slash * (r < .58f ? 1f : 0f)));
                }
                default: // Omega Nova (8-ray sunburst)
                {
                    float angle = Mathf.Atan2(y, x);
                    float rays = .44f + .22f * Mathf.Cos(angle * 8f);
                    float core = 1f - r * 2.2f;
                    return Mathf.Clamp01(Mathf.Max((rays - r) * 10f, core * 2f));
                }
            }
        }

        private static void EnsureLoaded()
        {
            string account = AccountProfilePrefs.Account ?? "local";
            if (state != null && loadedAccount == account) { SyncServerFriends(); return; }
            appliedFriends = null; nextFriendsRefresh = 0; showServerStatus = false;
            state = null;
            loadedAccount = account;
            string json = AccountProfilePrefs.GetString(SaveKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { state = JsonUtility.FromJson<PersistentState>(json); }
                catch (Exception) { state = null; }
            }
            state ??= new PersistentState();
            // Legacy local entries are retained in the saved file, but cannot authorize a real gift.
            state.friends = new List<FriendEntry>();
            state.incomingRequests = new List<FriendEntry>();
            state.outgoingRequests = new List<FriendEntry>();
            state.receivedStickers = new List<ReceivedStickerGift>();
            state.clanInvites ??= new List<ClanInviteEntry>();

            if (!state.seeded)
            {
                state.seeded = true;
                state.clanInvites.RemoveAll(c => c.inviteId == "cinv_initial_01");
            }
            SyncServerFriends();
        }

        private static void Save()
        {
            if (state == null) return;
            AccountProfilePrefs.SetString(SaveKey, JsonUtility.ToJson(state));
            AccountProfilePrefs.Save();
        }
    }
}

