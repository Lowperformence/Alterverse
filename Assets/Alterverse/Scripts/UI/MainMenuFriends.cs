using Alterverse.Characters;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        private int friendsSubTab;
        private int selectedFriendIndex;
        private string friendSearchInput = "";
        private int friendsListOffset;

        private void DrawFriends()
        {
            Heading("Friends & Stickers", "Add friends in matches or by name, invite them to your clan, and gift Shards Stickers.");

            var friends = FriendStickerService.Friends;
            var incoming = FriendStickerService.IncomingRequests;
            var outgoing = FriendStickerService.OutgoingRequests;
            var gifts = FriendStickerService.ReceivedStickers;
            var clanInvites = FriendStickerService.ClanInvites;

            string[] tabs =
            {
                $"MY FRIENDS ({friends.Count})",
                $"REQUESTS ({incoming.Count + outgoing.Count})",
                $"STICKER SHOP & INBOX ({gifts.Count})",
                $"CLAN INVITES ({clanInvites.Count})"
            };

            float tabW = (ContentWidth - 36f) / 4f;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (theme.Action(new Rect(Left + i * (tabW + 12f), 204, tabW, 40), tabs[i], selected: friendsSubTab == i))
                {
                    friendsSubTab = i;
                    friendsListOffset = 0;
                }
            }

            // Shard balance & status bar
            theme.StatusBadge(new Rect(Right - 220, 152, 220, 34), $"SHARDS  {PlayerWallet.Balance:N0}", FrontEndTheme.Accent);

            if (friendsSubTab == 0) DrawFriendsRoster(friends);
            else if (friendsSubTab == 1) DrawFriendRequestsAndAdd(incoming, outgoing);
            else if (friendsSubTab == 2) DrawStickerGiftCenter(friends, gifts);
            else DrawFriendClanInvites(clanInvites);

            theme.Panel(new Rect(Left, 666, ContentWidth, 38));
            theme.FitLabel(new Rect(Left + 16, 670, ContentWidth - 32, 30), FriendStickerService.Status, theme.Small, true);
        }

        private void DrawFriendsRoster(System.Collections.Generic.IReadOnlyList<FriendStickerService.FriendEntry> friends)
        {
            if (friends.Count == 0)
            {
                theme.EmptyState(new Rect(Left, 264, ContentWidth, 160), "No friends added yet",
                    "Open REQUESTS to add friends by name, or add teammates & opponents directly from the match scoreboard.");
                return;
            }

            selectedFriendIndex = Mathf.Clamp(selectedFriendIndex, 0, friends.Count - 1);
            const int pageSize = 5;
            friendsListOffset = Mathf.Clamp(friendsListOffset, 0, Mathf.Max(0, (friends.Count - 1) / pageSize * pageSize));
            int count = Mathf.Min(pageSize, friends.Count - friendsListOffset);

            for (int i = 0; i < count; i++)
            {
                int idx = friendsListOffset + i;
                var f = friends[idx];
                float y = 258 + i * 74;
                Rect card = new Rect(Left, y, ContentWidth, 66);
                theme.Panel(card);
                if (idx == selectedFriendIndex) theme.Frame(card, true);

                HeroId hero = (HeroId)Mathf.Clamp(f.hero, 0, HeroIdentity.HeroCount - 1);
                StylizedHeroCard.Draw(new Rect(Left + 12, y + 7, 46, 52), hero, FrontEndTheme.Pixel, idx == selectedFriendIndex);

                theme.FitLabel(new Rect(Left + 70, y + 8, 250, 26), f.name, theme.Heading);
                string clanLabel = string.IsNullOrEmpty(f.clanTag) ? "NO CLAN" : $"[{f.clanTag}] {f.clanName}";
                theme.FitLabel(new Rect(Left + 70, y + 36, 320, 20),
                    $"LEVEL {f.level}  â€¢  {HeroIdentity.NameFor(hero).ToUpperInvariant()}  â€¢  {clanLabel}", theme.Small, true);

                theme.StatusBadge(new Rect(Left + 390, y + 18, 96, 28), f.online ? "ONLINE" : "OFFLINE",
                    f.online ? FrontEndTheme.Positive : FrontEndTheme.Muted);

                if (theme.Action(new Rect(Right - 536, y + 14, 152, 38), "SEND STICKER", true))
                {
                    selectedFriendIndex = idx;
                    friendsSubTab = 2;
                }
                if (theme.Action(new Rect(Right - 374, y + 14, 152, 38), "CLAN INVITE"))
                {
                    selectedFriendIndex = idx;
                    FriendStickerService.InviteFriendToClan(f.playerId);
                }
                if (theme.Action(new Rect(Right - 212, y + 14, 110, 38), "PARTY", enabled: f.online))
                {
                    selectedFriendIndex = idx;
                    OnlinePartyOverlay.Show();
                }
                if (theme.Action(new Rect(Right - 92, y + 14, 80, 38), "REMOVE"))
                {
                    FriendStickerService.RemoveFriend(f.playerId);
                    return;
                }
            }

            if (friends.Count > pageSize)
            {
                if (theme.Action(new Rect(Left, 626, 110, 34), "PREVIOUS", enabled: friendsListOffset > 0))
                    friendsListOffset = Mathf.Max(0, friendsListOffset - pageSize);
                theme.Center(new Rect(Left + 120, 626, ContentWidth - 240, 34),
                    $"{friendsListOffset + 1}â€“{friendsListOffset + count} / {friends.Count}");
                if (theme.Action(new Rect(Right - 110, 626, 110, 34), "NEXT", enabled: friendsListOffset + count < friends.Count))
                    friendsListOffset += pageSize;
            }
        }

        private void DrawFriendRequestsAndAdd(
            System.Collections.Generic.IReadOnlyList<FriendStickerService.FriendEntry> incoming,
            System.Collections.Generic.IReadOnlyList<FriendStickerService.FriendEntry> outgoing)
        {
            Rect addBox = new Rect(Left, 258, ContentWidth, 88);
            theme.Panel(addBox);
            theme.Label(new Rect(Left + 20, 268, 360, 24), "SEND FRIEND REQUEST BY PLAYER NAME OR ID", theme.Small, true);
            friendSearchInput = GUI.TextField(new Rect(Left + 20, 296, ContentWidth - 260, 38), friendSearchInput ?? "", 32, theme.Field);
            if (theme.Action(new Rect(Right - 224, 296, 204, 38), "+ SEND REQUEST", true, enabled: !string.IsNullOrWhiteSpace(friendSearchInput)))
            {
                if (FriendStickerService.SendFriendRequest(friendSearchInput.Trim(), (HeroId)selectedHero, PlayerExperience.Level))
                    friendSearchInput = "";
            }

            float colW = (ContentWidth - 24f) * .5f;
            Rect inPanel = new Rect(Left, 358, colW, 296);
            Rect outPanel = new Rect(Left + colW + 24f, 358, colW, 296);
            theme.Panel(inPanel);
            theme.Panel(outPanel);

            theme.Label(new Rect(inPanel.x + 18, inPanel.y + 12, inPanel.width - 36, 28), $"INCOMING REQUESTS ({incoming.Count})", theme.Heading);
            if (incoming.Count == 0)
            {
                theme.Label(new Rect(inPanel.x + 18, inPanel.y + 56, inPanel.width - 36, 48), "No incoming friend requests right now.", theme.Body, true);
            }
            else
            {
                int inCount = Mathf.Min(4, incoming.Count);
                for (int i = 0; i < inCount; i++)
                {
                    var req = incoming[i];
                    float ry = inPanel.y + 48 + i * 58;
                    HeroId h = (HeroId)Mathf.Clamp(req.hero, 0, HeroIdentity.HeroCount - 1);
                    StylizedHeroCard.Draw(new Rect(inPanel.x + 16, ry + 4, 38, 44), h, FrontEndTheme.Pixel, 0f);
                    theme.FitLabel(new Rect(inPanel.x + 62, ry + 4, inPanel.width - 250, 24), req.name, theme.Body);
                    theme.FitLabel(new Rect(inPanel.x + 62, ry + 26, inPanel.width - 250, 18), $"LEVEL {req.level}  â€¢  {HeroIdentity.NameFor(h)}", theme.Small, true);
                    if (theme.Action(new Rect(inPanel.xMax - 182, ry + 8, 84, 36), "ACCEPT", true))
                    {
                        FriendStickerService.AcceptFriendRequest(req.playerId);
                        return;
                    }
                    if (theme.Action(new Rect(inPanel.xMax - 92, ry + 8, 76, 36), "DECLINE"))
                    {
                        FriendStickerService.DeclineFriendRequest(req.playerId);
                        return;
                    }
                }
            }

            theme.Label(new Rect(outPanel.x + 18, outPanel.y + 12, outPanel.width - 36, 28), $"SENT / IN-MATCH REQUESTS ({outgoing.Count})", theme.Heading);
            if (outgoing.Count == 0)
            {
                theme.Label(new Rect(outPanel.x + 18, outPanel.y + 56, outPanel.width - 36, 68),
                    "Tip: During any match, open Match Options (gear icon) or the Respawn Scoreboard to send friend requests to teammates and opponents.",
                    theme.Body, true);
            }
            else
            {
                int outCount = Mathf.Min(4, outgoing.Count);
                for (int i = 0; i < outCount; i++)
                {
                    var req = outgoing[i];
                    float ry = outPanel.y + 48 + i * 58;
                    HeroId h = (HeroId)Mathf.Clamp(req.hero, 0, HeroIdentity.HeroCount - 1);
                    StylizedHeroCard.Draw(new Rect(outPanel.x + 16, ry + 4, 38, 44), h, FrontEndTheme.Pixel, 0f);
                    theme.FitLabel(new Rect(outPanel.x + 62, ry + 4, outPanel.width - 250, 24), req.name, theme.Body);
                    theme.FitLabel(new Rect(outPanel.x + 62, ry + 26, outPanel.width - 250, 18), $"PENDING  â€¢  LEVEL {req.level}", theme.Small, true);
                    if (theme.Action(new Rect(outPanel.xMax - 182, ry + 8, 84, 36), "ACCEPT", true))
                    {
                        FriendStickerService.AcceptFriendRequest(req.playerId);
                        return;
                    }
                    if (theme.Action(new Rect(outPanel.xMax - 92, ry + 8, 76, 36), "CANCEL"))
                    {
                        FriendStickerService.DeclineFriendRequest(req.playerId);
                        return;
                    }
                }
            }
        }

        private void DrawStickerGiftCenter(
            System.Collections.Generic.IReadOnlyList<FriendStickerService.FriendEntry> friends,
            System.Collections.Generic.IReadOnlyList<FriendStickerService.ReceivedStickerGift> gifts)
        {
            float leftW = ContentWidth * .62f;
            float rightX = Left + leftW + 20f;
            float rightW = ContentWidth - leftW - 20f;

            // Recipient selector bar
            Rect recipientBar = new Rect(Left, 256, leftW, 56);
            theme.Panel(recipientBar);
            if (friends.Count > 0)
            {
                selectedFriendIndex = Mathf.Clamp(selectedFriendIndex, 0, friends.Count - 1);
                var target = friends[selectedFriendIndex];
                theme.FitLabel(new Rect(Left + 16, 268, leftW - 210, 32),
                    $"SEND TO FRIEND:  {target.name.ToUpperInvariant()}  (SENT: {target.stickersSent})", theme.Body);
                if (theme.Action(new Rect(Left + leftW - 184, 264, 80, 40), "< PREV", enabled: friends.Count > 1))
                    selectedFriendIndex = (selectedFriendIndex - 1 + friends.Count) % friends.Count;
                if (theme.Action(new Rect(Left + leftW - 96, 264, 80, 40), "NEXT >", enabled: friends.Count > 1))
                    selectedFriendIndex = (selectedFriendIndex + 1) % friends.Count;
            }
            else
            {
                theme.FitLabel(new Rect(Left + 16, 268, leftW - 32, 32), "Add a friend first to send Sticker Gifts.", theme.Body, true);
            }

            // 6 Multi-Tier Stickers Grid (2 rows x 3 columns)
            var catalog = FriendStickerService.Stickers;
            float cardW = (leftW - 24f) / 3f;
            float cardH = 162f;
            for (int i = 0; i < catalog.Length; i++)
            {
                var item = catalog[i];
                int col = i % 3;
                int row = i / 3;
                Rect card = new Rect(Left + col * (cardW + 12f), 322 + row * (cardH + 12f), cardW, cardH);
                theme.Panel(card);
                if (item.priceShards == 100) theme.Frame(card, true);

                Texture2D icon = FriendStickerService.GetStickerIcon(item.id);
                if (icon != null)
                    GUI.DrawTexture(new Rect(card.x + 12, card.y + 12, 54, 54), icon, ScaleMode.ScaleToFit, true);

                theme.FitLabel(new Rect(card.x + 72, card.y + 12, card.width - 80, 24), item.name, theme.Body);
                theme.FitLabel(new Rect(card.x + 72, card.y + 36, card.width - 80, 18), item.subtitle, theme.Small, true);

                theme.StatusBadge(new Rect(card.x + 12, card.y + 74, card.width - 24, 26),
                    $"FRIEND CONVERTS TO +{item.salvageShards:N0} SHARDS", FrontEndTheme.Positive);

                bool canSend = friends.Count > 0 && PlayerWallet.Balance >= item.priceShards;
                if (theme.Action(new Rect(card.x + 12, card.y + 108, card.width - 24, 42),
                    $"GIFT  â€¢  {item.priceShards:N0} SHARDS", true, enabled: canSend))
                {
                    FriendStickerService.SendStickerToFriend(friends[selectedFriendIndex].playerId, item.id);
                }
            }

            // Right column: Received Stickers Inbox (convert to 20% Shards!)
            Rect inboxPanel = new Rect(rightX, 256, rightW, 400);
            theme.Panel(inboxPanel);
            theme.FitLabel(new Rect(rightX + 18, 268, rightW - 36, 28), $"RECEIVED STICKERS ({gifts.Count})", theme.Heading);
            theme.FitLabel(new Rect(rightX + 18, 296, rightW - 36, 22), "Convert received friend stickers into Shards (20% value):", theme.Small, true);

            if (gifts.Count == 0)
            {
                theme.Label(new Rect(rightX + 18, 344, rightW - 36, 80),
                    "No uncollected stickers in your inbox.\nWhen friends send you a sticker (e.g. 100 Shards), you can convert it here for +20 Shards!",
                    theme.Body, true);
            }
            else
            {
                int showCount = Mathf.Min(4, gifts.Count);
                for (int i = 0; i < showCount; i++)
                {
                    var gift = gifts[i];
                    var sticker = FriendStickerService.GetSticker(gift.stickerId);
                    float gy = 328 + i * 78;
                    Rect rowRect = new Rect(rightX + 14, gy, rightW - 28, 70);
                    theme.Panel(rowRect);
                    Texture2D icon = FriendStickerService.GetStickerIcon(sticker.id);
                    if (icon != null)
                        GUI.DrawTexture(new Rect(rowRect.x + 10, rowRect.y + 10, 50, 50), icon, ScaleMode.ScaleToFit, true);

                    theme.FitLabel(new Rect(rowRect.x + 68, rowRect.y + 8, rowRect.width - 210, 24), sticker.name, theme.Body);
                    theme.FitLabel(new Rect(rowRect.x + 68, rowRect.y + 34, rowRect.width - 210, 20),
                        $"FROM {gift.senderName.ToUpperInvariant()}  ({sticker.priceShards:N0} SHARDS)", theme.Small, true);

                    if (theme.Action(new Rect(rowRect.xMax - 138, rowRect.y + 14, 126, 42),
                        $"+{sticker.salvageShards:N0} SHARDS", true))
                    {
                        FriendStickerService.ConvertReceivedStickerToShards(gift.giftId);
                        return;
                    }
                }
            }
        }

        private void DrawFriendClanInvites(System.Collections.Generic.IReadOnlyList<FriendStickerService.ClanInviteEntry> invites)
        {
            Rect panel = new Rect(Left, 258, ContentWidth, 396);
            theme.Panel(panel);
            theme.Label(new Rect(Left + 22, 274, ContentWidth - 44, 30), "CLAN INVITATIONS & SENT CLAN INVITES", theme.Heading);
            theme.Label(new Rect(Left + 22, 306, ContentWidth - 44, 22),
                "Invite friends from MY FRIENDS into your clan, or accept incoming clan invitations below.", theme.Small, true);

            if (invites.Count == 0)
            {
                theme.EmptyState(new Rect(Left + 22, 344, ContentWidth - 44, 150), "No pending clan invites",
                    "Use the CLAN INVITE button on any friend in MY FRIENDS to invite them to your clan.");
                return;
            }

            int count = Mathf.Min(5, invites.Count);
            for (int i = 0; i < count; i++)
            {
                var inv = invites[i];
                float y = 340 + i * 58;
                Rect row = new Rect(Left + 18, y, ContentWidth - 36, 52);
                theme.Panel(row);
                HeroId h = (HeroId)Mathf.Clamp(inv.inviterHero, 0, HeroIdentity.HeroCount - 1);
                StylizedHeroCard.Draw(new Rect(row.x + 10, row.y + 4, 38, 44), h, FrontEndTheme.Pixel, 0f);
                theme.FitLabel(new Rect(row.x + 58, row.y + 6, row.width - 320, 22), $"[{inv.clanTag}] {inv.clanName}", theme.Body);
                theme.FitLabel(new Rect(row.x + 58, row.y + 28, row.width - 320, 18),
                    inv.outgoing ? $"INVITED FRIEND: {inv.inviterName}" : $"INVITED BY: {inv.inviterName}", theme.Small, true);

                if (inv.outgoing)
                {
                    theme.StatusBadge(new Rect(row.xMax - 230, row.y + 12, 120, 28), "INVITE SENT", FrontEndTheme.Accent);
                    if (theme.Action(new Rect(row.xMax - 100, row.y + 8, 88, 36), "CANCEL"))
                    {
                        FriendStickerService.DeclineClanInvite(inv.inviteId);
                        return;
                    }
                }
                else
                {
                    if (theme.Action(new Rect(row.xMax - 208, row.y + 8, 100, 36), "JOIN CLAN", true))
                    {
                        FriendStickerService.AcceptClanInvite(inv.inviteId);
                        return;
                    }
                    if (theme.Action(new Rect(row.xMax - 98, row.y + 8, 86, 36), "DECLINE"))
                    {
                        FriendStickerService.DeclineClanInvite(inv.inviteId);
                        return;
                    }
                }
            }
        }
    }
}

