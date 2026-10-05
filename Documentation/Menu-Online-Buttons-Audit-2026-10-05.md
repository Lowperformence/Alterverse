# Menu, online buttons and leaderboard audit — 2026-10-05

Base: dev/vertical-slice at 16f0d94980e1de6994b0af03339e750eda08760b.

## Changes
- Play Online now calls EdgegapOnlineFlow.QuickSearchAsync for solo search and BeginSearchAsync for an existing ready leader party. Existing searches are reopened rather than restarted; guests and unready parties stay in the party surface.
- Main menu displays live Edgegap status/errors rather than declaring legacy Unity public-lobby search to be dedicated matchmaking.
- Offline launch leaves through EdgegapOnlineFlow so assignment polling and remote group membership are cleaned up before entering offline gameplay.
- Online overlay has explicit foreground GUI depth. Main menu, settings exit and voice controls do not receive input while it is open.
- Leave Party is available while searching; the existing flow cancels/leaves the Edgegap group before leaving the Unity party.
- Online and co-op Unready remain available when a hero conflict exists; becoming Ready still requires a valid roster.

## Code review coverage
| Surface | Reviewed wiring | Result |
| --- | --- | --- |
| Navigation/home | Page selection, profile card, Play, Heroes, Store, Inventory, Battle Pass, Profile, Daily, Ranking, Settings | Correct destinations found |
| Deployment | Online/offline, private co-op, hero choice, owned arena selection, arena arrows, shuffle | Online routing corrected |
| Online party | Create, join/normalize friend code, copy code, hero arrows, ready/unready, start/cancel search, leave, close | Overlay, leave and unready corrected |
| Private co-op | Create/join/copy/leave, microphone, hero choice, ready, arena/shuffle, stage/start/cancel preparation | Unready corrected; host and preparation guards reviewed |
| Store/inventory | Avatar/cosmetic/weapon/hero/arena/battle item actions, shard store/reconnect, equip/unequip, tabs and paging | Handlers and ownership/affordability gates reviewed; no transactions executed |
| Rewards/profile | Battle Pass preview/claim/premium, daily claim, profile name save/cancel, avatar equip | Handlers reviewed |
| Settings/results/combat | Volume, resume/leave confirmation, exit confirmation, result page/back-to-menu gate, combat touch/mouse action dispatch | Handlers reviewed |
| Ranking | CONNECT & SYNC/SYNC WINS and OPEN MOST WINS, authentication retry, lifetime-win reporting, native UI callbacks | Correct service calls; configuration blocker below |

## Ranking blocker
Assets/GooglePlayGames/Resources/PlayGamesSettings.asset has an empty mAppId in this repository snapshot. GooglePlayLevelLeaderboard.IsConfigured therefore returns false and the ranking buttons are disabled. MostWinsLeaderboardId is present. The numeric game App ID must come from this game's Play Games Services configuration; it has not been guessed or replaced. Local setup may differ from GitHub.

Configure via Play Console → Play Games Services → Setup and management → Configuration → Get resources; import Android resources in Unity → Window → Google Play Games → Setup → Android Setup. Save generated changes, build Android, and test using the Play Games tester account and the correctly signed build.

## Validation and limitations
- Reviewed all UI C# button call sites and the relevant online flow and leaderboard service paths.
- Checked balanced delimiters in the four changed C# files and reviewed the final routing/guard changes. This is a structural check, not C# compilation.
- Unity/.NET compilers and an authenticated Android runtime are unavailable in this environment. No Unity compilation, physical clicks, paid purchases, live two-player match, Edgegap deployment or Google Play native leaderboard test was performed.

## Runtime acceptance checks
1. Solo Play Online opens the foreground party view; current preparing/search/error status appears. Search cancellation and leaving return to a usable party/menu.
2. A ready host starts Edgegap for an existing friend party. A guest cannot start. Unready/hero-conflict parties remain editable. Rapid repeated clicks do not start duplicate searches.
3. Closing the online view does not stop an active search; reopening shows it. While open, main menu/settings buttons cannot receive clicks.
4. Switching to offline after closing the overlay cleans up the online search before starting offline gameplay.
5. Co-op Unready still works after a conflicting hero choice; staging and launching remain host gated.
6. After Play Games setup, CONNECT & SYNC authenticates in Android; a recorded victory changes the submitted lifetime-win count; OPEN MOST WINS opens the configured All Time leaderboard. Failed sign-in can be retried. Editor execution only reports Android support.

## Follow-up: remaining button actions
Reviewed the backing services in addition to UI call sites: PlayerProfile/AvatarInventory, PlayerWallet/PaidShardCommerce/GooglePlayShardStore, arena/hero/weapon/cosmetic ownership and purchases, BattleItemInventory, BattlePassProgress, DailyQuestProgress, AccountServerBootstrap, co-op lobby/voice and MatchExit.

Additional corrections:
- Account login keeps authentication status visible while the panel is open. Empty credentials disable SIGN IN/SECURE ACCOUNT with an input hint instead of silently calling a service that immediately rejects them.
- Cosmetic purchase controls use PlayerWallet.CanAfford, which includes the ongoing-purchase lock. The backing commerce service already rejects duplicate pending purchases; the UI now follows the same lock.
- Cosmetic purchase failures distinguish insufficient shards from other failed requests.
- Battle-item EQUIP/UNEQUIP reports the actual service result rather than showing success after a rejected equip from stale inventory.

Reviewed behavior, without execution:
- Paid purchase recovery keeps an operation ID and retries the same debit; purchase UI activation is guarded. Google Play credits are verified before confirmation.
- Ownership and affordability are checked again by arena/hero/weapon purchases; equip requires ownership.
- Daily claims commit currency and claim state together; repeat claims are rejected.
- Battle Pass claims use claim masks; locked/premium/unreached rewards are rejected and repeated claims do not grant again.
- Display name validation and save/cancel paths, avatar ownership, and volume clamping/persistence are connected to their intended profile operations.
- Result menu return is receipt gated. MatchExit routes cleanup through Edgegap and restores the home menu; co-op voice controls request permission/join or toggle mute.
- Ranking behavior remains as documented above. No additional setup changes were made because the user's local setup may already be complete.

Validation: inspected final changed files and checked balanced delimiters. No C# compiler/Unity runtime is present. This is a code-level functional audit; device clicks, integration tests and external-service behavior remain unverified. No purchases, account sign-ins or rewards were issued during the audit.
