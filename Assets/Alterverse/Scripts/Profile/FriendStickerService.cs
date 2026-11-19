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
                name = "BRO STICKER",
                subtitle = "GOLDEN BRO • SQUAD SALUTE",
                priceShards = 50,
                salvageShards = 10,
                primaryColor = new Color(.16f, .72f, .98f, 1f),
                accentColor = new Color(1f, .86f, .32f, 1f)
            },
            new()
            {
                id = 1,
                name = "HEART & RING",
                subtitle = "RUBY HEART • DIAMOND HALO RING",
                priceShards = 100,
                salvageShards = 20,
                primaryColor = new Color(.96f, .22f, .46f, 1f),
                accentColor = new Color(1f, .84f, .34f, 1f)
            },
            new()
            {
                id = 2,
                name = "ROYAL GIFT BOX",
                subtitle = "GOLDEN BOW • LUXURY PRESENT",
                priceShards = 250,
                salvageShards = 50,
                primaryColor = new Color(.88f, .26f, .68f, 1f),
                accentColor = new Color(1f, .86f, .32f, 1f)
            },
            new()
            {
                id = 3,
                name = "KING CROWN",
                subtitle = "SOVEREIGN GOLD CROWN • VIP GEMS",
                priceShards = 500,
                salvageShards = 100,
                primaryColor = new Color(.96f, .74f, .22f, 1f),
                accentColor = new Color(.32f, .96f, .88f, 1f)
            },
            new()
            {
                id = 4,
                name = "WINGED SHARD",
                subtitle = "ANGEL WINGS • MVP CRYSTAL",
                priceShards = 1000,
                salvageShards = 200,
                primaryColor = new Color(.56f, .34f, .98f, 1f),
                accentColor = new Color(.38f, .94f, 1f, 1f)
            },
            new()
            {
                id = 5,
                name = "GOAT TROPHY",
                subtitle = "CHAMPION CUP • MYTHIC TRIBUTE",
                priceShards = 2500,
                salvageShards = 500,
                primaryColor = new Color(.98f, .46f, .18f, 1f),
                accentColor = new Color(1f, .92f, .48f, 1f)
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
            const int size = 160;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DF_Sticker_" + item.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * .5f;
            float maxR = size * .465f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxR;
                    float dy = (y - center) / maxR;
                    pixels[y * size + x] = RenderStickerPixel(stickerId, item, dx, dy);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            stickerIcons[stickerId] = tex;
            return tex;
        }

        private static Color RenderStickerPixel(int id, StickerCatalogItem item, float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            // Slight octagon/shield contour for BRO (0) and WINGED SHARD (4), rounded badge for others
            float badgeR = id == 0
                ? Mathf.Max(r, (Mathf.Abs(x) * .86f + Mathf.Abs(y) * .86f) * .84f)
                : r;

            if (badgeR > 1.03f)
                return new Color(0f, 0f, 0f, 0f);

            // Outer anti-aliased die-cut vinyl sticker edge
            float outerAlpha = Mathf.Clamp01((1.03f - badgeR) * 26f);

            // Die-cut white/cream vinyl border (0.87..0.99) with dark outer drop rim
            Color vinylWhite = new Color(.98f, .97f, .94f, 1f);
            Color darkInk = new Color(.04f, .05f, .09f, 1f);
            Color goldLight = new Color(1f, .93f, .58f, 1f);
            Color goldMid = new Color(.98f, .76f, .22f, 1f);
            Color goldDark = new Color(.62f, .38f, .06f, 1f);

            // Rich radial & sunburst background inside the badge
            float rays = .5f + .5f * Mathf.Cos(angle * 12f);
            Color bgDeep = Color.Lerp(new Color(.05f, .07f, .14f, 1f), item.primaryColor * .38f, Mathf.Clamp01(1f - badgeR * .85f));
            Color bgCore = Color.Lerp(bgDeep, item.primaryColor * .78f, Mathf.Clamp01((1f - badgeR) * 1.15f));
            Color col = Color.Lerp(bgCore, Color.Lerp(item.primaryColor, item.accentColor, .45f), rays * .16f * Mathf.Clamp01(1f - badgeR));

            // Inner metallic foil ring at badgeR ~ 0.81
            float foilRing = SmoothBand(badgeR, .81f, .035f, 34f);
            Color foilCol = Color.Lerp(goldDark, goldLight, Mathf.Clamp01((-x + y) * .6f + .5f));
            col = Color.Lerp(col, foilCol, foilRing);

            // Die-cut white vinyl sticker border outside badgeR ~ 0.84
            float vinylMask = Mathf.Clamp01((badgeR - .845f) * 32f);
            float innerOutline = SmoothBand(badgeR, .845f, .016f, 45f);
            col = Color.Lerp(col, vinylWhite, vinylMask);
            col = Color.Lerp(col, darkInk, innerOutline * .85f);

            // Compose the specific high-detail gift sticker artwork inside the badge
            switch (id)
            {
                case 0:
                    ComposeBroSticker(ref col, x, y, darkInk, goldLight, goldMid, goldDark);
                    break;
                case 1:
                    ComposeHeartWithRingSticker(ref col, x, y, darkInk, goldLight, goldMid, goldDark);
                    break;
                case 2:
                    ComposeGiftBoxSticker(ref col, x, y, darkInk, goldLight, goldMid, goldDark);
                    break;
                case 3:
                    ComposeKingCrownSticker(ref col, x, y, darkInk, goldLight, goldMid, goldDark);
                    break;
                case 4:
                    ComposeWingedShardSticker(ref col, x, y, darkInk, goldLight, goldMid, goldDark);
                    break;
                default:
                    ComposeGoatTrophySticker(ref col, x, y, darkInk, goldLight, goldMid, goldDark);
                    break;
            }

            // Glossy diagonal sticker sheen across upper-left
            float sheen = Mathf.Clamp01(1f - Mathf.Abs((x + y - .38f) * 3.8f)) * Mathf.Clamp01((.80f - badgeR) * 4f);
            col = Color.Lerp(col, Color.white, sheen * .14f);

            col.a = outerAlpha;
            return col;
        }

        private static void ComposeBroSticker(ref Color col, float x, float y, Color darkInk, Color goldLight, Color goldMid, Color goldDark)
        {
            // Electric cyan crest shield behind the BRO banner
            float sx = Mathf.Abs(x) / .56f;
            float sy = (y - .12f) / .52f;
            float shieldDist = Mathf.Max(sx + Mathf.Max(0f, -sy * .55f), Mathf.Abs(sy)) - .82f;
            ApplyLayeredShape(ref col, shieldDist,
                Color.Lerp(new Color(.04f, .44f, .88f, 1f), new Color(.24f, .94f, 1f, 1f), Mathf.Clamp01(y + .5f)),
                goldLight, darkInk, .04f, .05f);

            // Golden crown-star above the shield
            float starD = StarSdf(x, y - .42f, .19f, .085f, 5);
            ApplyLayeredShape(ref col, starD, Color.Lerp(goldMid, goldLight, Mathf.Clamp01((y - .3f) * 4f)), Color.white, darkInk, .018f, .04f);

            // Twin lightning accents left and right
            float sparkL = StarSdf(x + .52f, y - .34f, .11f, .04f, 4);
            float sparkR = StarSdf(x - .52f, y - .34f, .11f, .04f, 4);
            ApplyLayeredShape(ref col, Mathf.Min(sparkL, sparkR), Color.white, goldLight, darkInk, .01f, .03f);

            // Bold tilted/curved arcade ribbon plate for "BRO"
            float by = y + .12f - x * .05f;
            float bannerDist = RoundedBoxSdf(x, by, .64f, .24f, .09f);
            Color bannerFill = Color.Lerp(new Color(.10f, .14f, .28f, 1f), new Color(.20f, .28f, .52f, 1f), Mathf.Clamp01((.24f - by) * 2f));
            ApplyLayeredShape(ref col, bannerDist, bannerFill, goldMid, darkInk, .035f, .055f);

            // Crisp chunky "BRO" lettering in bright gold-white gradient with heavy comic outline
            float wordDist = SampleWord("BRO", x, by, .96f, .32f);
            Color textFill = Color.Lerp(new Color(1f, .72f, .14f, 1f), new Color(1f, .98f, .82f, 1f), Mathf.Clamp01((by + .16f) * 3.2f));
            ApplyTextLayer(ref col, wordDist, textFill, darkInk, .052f);
        }

        private static void ComposeHeartWithRingSticker(ref Color col, float x, float y, Color darkInk, Color goldLight, Color goldMid, Color goldDark)
        {
            // Tilted golden halo ring coordinates (20 deg tilt, positioned around the upper heart)
            float cosA = .9397f, sinA = -.3420f;
            float rx = x * cosA - (y - .16f) * sinA;
            float ry = (x * sinA + (y - .16f) * cosA) * 2.35f;
            float ringEllipseR = Mathf.Sqrt(rx * rx + ry * ry);
            float ringTubeSdf = Mathf.Abs(ringEllipseR - .62f) - .078f;

            Color ringFill = Color.Lerp(goldDark, goldLight, Mathf.Clamp01(.5f + rx * .6f + (1f - Mathf.Abs(ringEllipseR - .62f) * 10f) * .35f));

            // 1) Back arc of the golden ring (ry > 0 is behind the heart's upper lobes)
            if (ry > 0f)
                ApplyLayeredShape(ref col, ringTubeSdf, ringFill, goldLight, darkInk, .015f, .045f);

            // 2) Plump 3D Ruby Heart in the center
            float heartSdf = HeartSdf(x, y + .08f, .74f);
            if (heartSdf < .065f)
            {
                Color rubyDeep = new Color(.52f, .03f, .18f, 1f);
                Color rubyMid = new Color(.96f, .14f, .38f, 1f);
                Color rubyBright = new Color(1f, .44f, .66f, 1f);
                float heartGrad = Mathf.Clamp01((y + .45f) * 1.1f - x * .25f);
                Color heartCol = Color.Lerp(rubyDeep, rubyMid, heartGrad);
                // Inner rim bevel & plump center glow
                float innerGlow = Mathf.Clamp01(-heartSdf * 3.5f);
                heartCol = Color.Lerp(heartCol, rubyBright, innerGlow * .55f);
                // Glossy white-pink specular highlight on upper-left lobe
                float spec = CircleSdf(x + .21f, y - .14f, .11f);
                heartCol = Color.Lerp(heartCol, new Color(1f, .92f, .96f, 1f), Mathf.Clamp01((.04f - spec) * 14f) * .85f);

                ApplyLayeredShape(ref col, heartSdf, heartCol, new Color(1f, .62f, .78f, 1f), darkInk, .022f, .055f);
            }

            // 3) Front arc of the golden ring (passes in front of the heart!)
            if (ry <= .05f)
            {
                float frontBlend = Mathf.Clamp01((.05f - ry) * 14f);
                Color before = col;
                ApplyLayeredShape(ref col, ringTubeSdf, ringFill, goldLight, darkInk, .018f, .048f);
                col = Color.Lerp(before, col, frontBlend);
            }

            // 4) Sparkling Cyan Diamond Solitaire mounted on the front-right of the golden ring
            float gx = x - .44f, gy = y - .34f;
            float diamondSdf = (Mathf.Abs(gx) * 1.15f + Mathf.Abs(gy) * .95f) - .17f;
            Color gemCol = Color.Lerp(new Color(.12f, .76f, .98f, 1f), new Color(.84f, .98f, 1f, 1f), Mathf.Clamp01((gy + .15f) * 3.5f));
            ApplyLayeredShape(ref col, diamondSdf, gemCol, Color.white, darkInk, .02f, .045f);

            // 4-point star glint on the diamond and bottom-left sparkle
            float glint1 = StarSdf(x - .44f, y - .34f, .18f, .035f, 4);
            float glint2 = StarSdf(x + .50f, y + .32f, .13f, .03f, 4);
            col = Color.Lerp(col, Color.white, Mathf.Clamp01(-Mathf.Min(glint1, glint2) * 35f));
        }

        private static void ComposeGiftBoxSticker(ref Color col, float x, float y, Color darkInk, Color goldLight, Color goldMid, Color goldDark)
        {
            // Golden Double-Loop Bow on top of the gift box (y ~ 0.34)
            float bowL = Mathf.Abs(RotatedOvalSdf(x + .19f, y - .35f, .19f, .10f, .42f)) - .038f;
            float bowR = Mathf.Abs(RotatedOvalSdf(x - .19f, y - .35f, .19f, .10f, -.42f)) - .038f;
            float bowKnot = CircleSdf(x, y - .31f, .085f);
            float bowSdf = Mathf.Min(Mathf.Min(bowL, bowR), bowKnot);
            ApplyLayeredShape(ref col, bowSdf, Color.Lerp(goldMid, goldLight, Mathf.Clamp01((y - .25f) * 4f)), Color.white, darkInk, .015f, .045f);

            // Lower Gift Box Body
            float boxSdf = RoundedBoxSdf(x, y + .11f, .46f, .34f, .06f);
            if (boxSdf < .06f)
            {
                Color boxCol = Color.Lerp(new Color(.58f, .10f, .48f, 1f), new Color(.94f, .24f, .72f, 1f), Mathf.Clamp01((y + .4f) * 1.4f - x * .3f));
                // Vertical & horizontal golden satin ribbons across the box body
                float ribV = Mathf.Clamp01((.105f - Mathf.Abs(x)) * 32f);
                float ribH = Mathf.Clamp01((.085f - Mathf.Abs(y + .11f)) * 32f);
                float rib = Mathf.Max(ribV, ribH);
                Color ribCol = Color.Lerp(goldDark, goldLight, Mathf.Clamp01(.5f + y + x * .4f));
                boxCol = Color.Lerp(boxCol, ribCol, rib);
                ApplyLayeredShape(ref col, boxSdf, boxCol, new Color(1f, .64f, .88f, 1f), darkInk, .02f, .05f);
            }

            // Overhanging Gift Box Lid
            float lidSdf = RoundedBoxSdf(x, y - .17f, .52f, .11f, .045f);
            if (lidSdf < .06f)
            {
                Color lidCol = Color.Lerp(new Color(.74f, .16f, .58f, 1f), new Color(1f, .38f, .82f, 1f), Mathf.Clamp01((y - .08f) * 4f));
                float lidRib = Mathf.Clamp01((.11f - Mathf.Abs(x)) * 32f);
                lidCol = Color.Lerp(lidCol, Color.Lerp(goldMid, goldLight, .5f), lidRib);
                ApplyLayeredShape(ref col, lidSdf, lidCol, goldLight, darkInk, .018f, .048f);
            }

            // Sparkling cyan shard gems flanking the bow
            float s1 = StarSdf(x + .48f, y - .38f, .13f, .035f, 4);
            float s2 = StarSdf(x - .48f, y - .38f, .13f, .035f, 4);
            ApplyLayeredShape(ref col, Mathf.Min(s1, s2), new Color(.45f, .96f, 1f, 1f), Color.white, darkInk, .01f, .03f);

            // Crisp "GIFT" tag plate at the bottom of the box
            float tagY = y + .42f;
            float tagSdf = RoundedBoxSdf(x, tagY, .48f, .15f, .06f);
            ApplyLayeredShape(ref col, tagSdf, new Color(.08f, .12f, .24f, 1f), goldMid, darkInk, .025f, .045f);
            float giftWord = SampleWord("GIFT", x, tagY, .80f, .21f);
            ApplyTextLayer(ref col, giftWord, goldLight, darkInk, .038f);
        }

        private static void ComposeKingCrownSticker(ref Color col, float x, float y, Color darkInk, Color goldLight, Color goldMid, Color goldDark)
        {
            // 3-Peak Royal Gold Crown Body
            float cy = y - .04f;
            float crownW = .56f - (cy + .22f) * -.14f;
            float topWave = .16f + .24f * Mathf.Cos(x * 5.6f);
            float crownBox = Mathf.Max(Mathf.Abs(x) - crownW, Mathf.Max(-cy - .22f, cy - topWave));
            Color crownCol = Color.Lerp(goldDark, goldLight, Mathf.Clamp01((cy + .25f) * 1.4f + (.3f - Mathf.Abs(x))));
            // Crimson velvet base band on the crown
            if (cy < -.07f && cy > -.20f)
                crownCol = Color.Lerp(crownCol, new Color(.78f, .10f, .24f, 1f), Mathf.Clamp01((-.07f - cy) * 28f));

            ApplyLayeredShape(ref col, crownBox, crownCol, goldLight, darkInk, .022f, .052f);

            // 3 Golden Pearls on the 3 Crown Tips (center x=0, left/right x=±0.54)
            float pearlC = CircleSdf(x, cy - .43f, .075f);
            float pearlL = CircleSdf(x + .53f, cy - .35f, .065f);
            float pearlR = CircleSdf(x - .53f, cy - .35f, .065f);
            ApplyLayeredShape(ref col, Mathf.Min(pearlC, Mathf.Min(pearlL, pearlR)), goldLight, Color.white, darkInk, .015f, .04f);

            // Center Cyan Diamond & Side Emerald/Ruby Gems inset in the crown
            float gemCenter = (Mathf.Abs(x) * 1.2f + Mathf.Abs(cy - .05f)) - .12f;
            ApplyLayeredShape(ref col, gemCenter, new Color(.22f, .96f, 1f, 1f), Color.white, darkInk, .015f, .035f);
            float gemSide = (Mathf.Abs(Mathf.Abs(x) - .31f) * 1.25f + Mathf.Abs(cy - .02f)) - .085f;
            ApplyLayeredShape(ref col, gemSide, new Color(1f, .18f, .44f, 1f), new Color(1f, .75f, .85f, 1f), darkInk, .012f, .03f);

            // "VIP" royal plate at bottom
            float plateY = y + .42f;
            float plateSdf = RoundedBoxSdf(x, plateY, .44f, .16f, .06f);
            ApplyLayeredShape(ref col, plateSdf, new Color(.14f, .08f, .26f, 1f), goldLight, darkInk, .025f, .045f);
            float vipWord = SampleWord("VIP", x, plateY, .66f, .22f);
            ApplyTextLayer(ref col, vipWord, goldLight, darkInk, .042f);
        }

        private static void ComposeWingedShardSticker(ref Color col, float x, float y, Color darkInk, Color goldLight, Color goldMid, Color goldDark)
        {
            // Golden Halo above the crystal
            float haloR = Mathf.Sqrt(x * x + (y - .46f) * (y - .46f) * 6.5f);
            float haloSdf = Mathf.Abs(haloR - .28f) - .038f;
            ApplyLayeredShape(ref col, haloSdf, goldLight, Color.white, darkInk, .01f, .035f);

            // Spread Feathered Angel Wings on Left & Right
            float wx = Mathf.Abs(x) - .18f;
            float wy = y - .04f - wx * .32f;
            float wingOuter = RoundedBoxSdf(wx - .20f, wy, .24f, .24f - wx * .25f, .12f);
            // Feather notches along outer edge
            float featherWave = Mathf.Sin((y + .3f) * 18f) * .028f;
            float wingSdf = wingOuter + featherWave;
            Color wingCol = Color.Lerp(new Color(.78f, .88f, 1f, 1f), Color.white, Mathf.Clamp01((y + .3f) * 1.3f));
            wingCol = Color.Lerp(wingCol, goldLight, Mathf.Clamp01((wx - .22f) * 3f));
            ApplyLayeredShape(ref col, wingSdf, wingCol, Color.white, darkInk, .018f, .048f);

            // Center Faceted Dimension Shard Crystal
            float crystalSdf = (Mathf.Abs(x) * 1.45f + Mathf.Abs(y - .04f) * .92f) - .36f;
            if (crystalSdf < .06f)
            {
                Color cLeft = new Color(.18f, .86f, 1f, 1f);
                Color cRight = new Color(.54f, .32f, .98f, 1f);
                Color crystalCol = Color.Lerp(cLeft, cRight, Mathf.Clamp01(x * 3.5f + .5f));
                // Center vertical facet highlight
                crystalCol = Color.Lerp(crystalCol, Color.white, Mathf.Clamp01((.045f - Mathf.Abs(x)) * 20f) * .65f);
                ApplyLayeredShape(ref col, crystalSdf, crystalCol, new Color(.78f, .98f, 1f, 1f), darkInk, .022f, .052f);
            }

            // "MVP" plate at bottom
            float plateY = y + .43f;
            float plateSdf = RoundedBoxSdf(x, plateY, .46f, .15f, .06f);
            ApplyLayeredShape(ref col, plateSdf, new Color(.10f, .12f, .28f, 1f), new Color(.45f, .94f, 1f, 1f), darkInk, .025f, .045f);
            float mvpWord = SampleWord("MVP", x, plateY, .70f, .22f);
            ApplyTextLayer(ref col, mvpWord, goldLight, darkInk, .042f);
        }

        private static void ComposeGoatTrophySticker(ref Color col, float x, float y, Color darkInk, Color goldLight, Color goldMid, Color goldDark)
        {
            // Curved Golden Trophy Side Handles
            float handleR = Mathf.Sqrt((Mathf.Abs(x) - .32f) * (Mathf.Abs(x) - .32f) + (y - .16f) * (y - .16f) * .85f);
            float handleSdf = Mathf.Max(Mathf.Abs(handleR - .17f) - .042f, .22f - Mathf.Abs(x));
            ApplyLayeredShape(ref col, handleSdf, goldMid, goldLight, darkInk, .012f, .04f);

            // Trophy Cup Bowl + Stem + Base Pedestal
            float cupW = .16f + Mathf.Clamp01((y + .06f) * 1.8f) * .20f;
            float bowlSdf = RoundedBoxSdf(x, y - .16f, cupW, .25f, .10f);
            float stemSdf = RoundedBoxSdf(x, y + .16f, .085f, .12f, .03f);
            float baseSdf = RoundedBoxSdf(x, y + .28f, .26f, .065f, .03f);
            float trophySdf = Mathf.Min(bowlSdf, Mathf.Min(stemSdf, baseSdf));
            Color trophyCol = Color.Lerp(goldDark, goldLight, Mathf.Clamp01(.55f - x * .8f + (y + .2f) * .5f));
            ApplyLayeredShape(ref col, trophySdf, trophyCol, Color.white, darkInk, .02f, .052f);

            // Ruby Star Emblem on the center of the Trophy Bowl
            float cupStar = StarSdf(x, y - .18f, .16f, .068f, 5);
            ApplyLayeredShape(ref col, cupStar, new Color(.98f, .16f, .36f, 1f), new Color(1f, .72f, .82f, 1f), darkInk, .012f, .035f);

            // Bold "GOAT" championship banner at the bottom
            float plateY = y + .42f;
            float plateSdf = RoundedBoxSdf(x, plateY, .54f, .16f, .06f);
            ApplyLayeredShape(ref col, plateSdf, new Color(.18f, .08f, .06f, 1f), goldLight, darkInk, .025f, .048f);
            float goatWord = SampleWord("GOAT", x, plateY, .86f, .22f);
            ApplyTextLayer(ref col, goatWord, Color.Lerp(goldMid, Color.white, Mathf.Clamp01((plateY - y + .1f) * 4f)), darkInk, .04f);
        }

        private static void ApplyLayeredShape(ref Color dst, float sdf, Color fill, Color rim, Color outline, float rimWidth, float outlineWidth)
        {
            if (sdf > outlineWidth + .02f) return;
            float outerMask = Mathf.Clamp01((outlineWidth - sdf) * 38f);
            float fillMask = Mathf.Clamp01(-sdf * 38f);
            float rimMask = Mathf.Clamp01((rimWidth - Mathf.Abs(sdf + rimWidth * .5f)) * 38f) * fillMask;

            Color shapeCol = Color.Lerp(outline, fill, fillMask);
            shapeCol = Color.Lerp(shapeCol, rim, rimMask * .72f);
            dst = Color.Lerp(dst, shapeCol, outerMask);
        }

        private static void ApplyTextLayer(ref Color dst, float sdf, Color fill, Color outline, float outlineWidth)
        {
            if (sdf > outlineWidth + .02f) return;
            float outerMask = Mathf.Clamp01((outlineWidth - sdf) * 45f);
            float fillMask = Mathf.Clamp01(-sdf * 45f);
            Color c = Color.Lerp(outline, fill, fillMask);
            dst = Color.Lerp(dst, c, outerMask);
        }

        private static float SmoothBand(float value, float center, float halfWidth, float sharpness)
            => Mathf.Clamp01((halfWidth - Mathf.Abs(value - center)) * sharpness);

        private static float CircleSdf(float x, float y, float r)
            => Mathf.Sqrt(x * x + y * y) - r;

        private static float RoundedBoxSdf(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r;
            float qy = Mathf.Abs(y) - hy + r;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        private static float RotatedOvalSdf(float x, float y, float rx, float ry, float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float ux = (x * c - y * s) / rx;
            float uy = (x * s + y * c) / ry;
            return (Mathf.Sqrt(ux * ux + uy * uy) - 1f) * Mathf.Min(rx, ry);
        }

        private static float StarSdf(float x, float y, float outerR, float innerR, int points)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x) + Mathf.PI * .5f;
            float t = .5f + .5f * Mathf.Cos(a * points);
            float targetR = Mathf.Lerp(innerR, outerR, t * t);
            return r - targetR;
        }

        private static float HeartSdf(float x, float y, float scale)
        {
            // Exact 2D signed distance preference for a plump symmetrical heart (+y is UP)
            float px = Mathf.Abs(x) / scale;
            float py = y / scale + .38f;
            if (py + px > 1f)
            {
                float dx = px - .25f;
                float dy = py - .75f;
                return (Mathf.Sqrt(dx * dx + dy * dy) - .35355339f) * scale;
            }
            float d1x = px, d1y = py - 1f;
            float h = .5f * Mathf.Max(px + py, 0f);
            float d2x = px - h, d2y = py - h;
            float d = Mathf.Sqrt(Mathf.Min(d1x * d1x + d1y * d1y, d2x * d2x + d2y * d2y));
            float s = (px - py) < 0f ? -1f : 1f;
            return d * s * scale;
        }

        private static float SampleWord(string word, float x, float y, float totalWidth, float totalHeight)
        {
            int len = word.Length;
            int totalCols = len * 6 - 1;
            float cellW = totalWidth / totalCols;
            float cellH = totalHeight / 7f;
            float startX = -totalWidth * .5f;
            float startY = totalHeight * .5f; // Top of row 0 (+y is UP)

            float minDist = 1f;
            // Quick bounding box cull
            if (Mathf.Abs(x) > totalWidth * .5f + .1f || Mathf.Abs(y) > totalHeight * .5f + .1f)
                return minDist;

            float strokeR = Mathf.Min(cellW, cellH) * .56f;
            for (int i = 0; i < len; i++)
            {
                int[] rows = GetGlyphRows(word[i]);
                float charLeft = startX + i * 6 * cellW;
                if (x < charLeft - .09f || x > charLeft + 5 * cellW + .09f) continue;

                for (int r = 0; r < 7; r++)
                {
                    int mask = rows[r];
                    if (mask == 0) continue;
                    float cy = startY - (r + .5f) * cellH;
                    if (Mathf.Abs(y - cy) > cellH + .08f) continue;
                    for (int c = 0; c < 5; c++)
                    {
                        if (((mask >> (4 - c)) & 1) == 0) continue;
                        float cx = charLeft + (c + .5f) * cellW;
                        float dx = Mathf.Max(0f, Mathf.Abs(x - cx) - cellW * .22f);
                        float dy = Mathf.Max(0f, Mathf.Abs(y - cy) - cellH * .22f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy) - strokeR;
                        if (d < minDist) minDist = d;
                    }
                }
            }
            return minDist;
        }

        private static readonly int[] GlyphB = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 };
        private static readonly int[] GlyphR = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 };
        private static readonly int[] GlyphO = { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 };
        private static readonly int[] GlyphG = { 0b01111, 0b10000, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111 };
        private static readonly int[] GlyphI = { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111 };
        private static readonly int[] GlyphF = { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 };
        private static readonly int[] GlyphT = { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 };
        private static readonly int[] GlyphV = { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 };
        private static readonly int[] GlyphP = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 };
        private static readonly int[] GlyphM = { 0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001 };
        private static readonly int[] GlyphA = { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 };

        private static int[] GetGlyphRows(char ch)
        {
            return ch switch
            {
                'B' => GlyphB,
                'R' => GlyphR,
                'O' => GlyphO,
                'G' => GlyphG,
                'I' => GlyphI,
                'F' => GlyphF,
                'T' => GlyphT,
                'V' => GlyphV,
                'P' => GlyphP,
                'M' => GlyphM,
                'A' => GlyphA,
                _ => GlyphO
            };
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

