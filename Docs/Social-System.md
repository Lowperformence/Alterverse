# Friends, player levels and clans

The account server now supplies account-authenticated social features using the same persistent SQLite database or Firestore namespace as cloud profiles. The Unity menu has FRIENDS and CLANS entry points and ranking tabs for MOST WINS, PLAYER LEVELS and CLAN LEVELS. The existing Google Play Most Wins leaderboard is retained.

## First release

- Search players by saved display name or a unique public player ID. Duplicate display names are allowed; IDs identify the recipient. Only accounts known to this backend are searchable.
- Send, accept, decline, cancel and remove friend requests; accepted friends appear on both accounts. Crossing requests still require explicit acceptance. Block removes the relationship, hides players from each other's search and prevents new friend requests. Unblock is available separately.
- Maximum 100 friends, 50 pending friend requests and 200 blocked players per account. Successful social mutations are limited to 30 per minute per account.
- Player ranking sorts saved level descending, saved total XP descending, then stable public ID ascending. The XP curve and level-1000 cap match `PlayerExperience`. Lists return eight entries per page and include the caller's global rank.
- Create a free clan with a unique name (3–24 characters), unique uppercase tag (2–5 ASCII letters/digits), description (160 characters) and open/application policy. Maximum 50 members; one clan per player. Clan applications are capped at five per player and 100 per clan.
- Clan ranking sums current levels of current members. Equal totals use the stable clan ID as a deterministic tie breaker. Changes to saved XP or membership change the score on the next load.
- Leaders can promote/demote officers, remove members and transfer leadership. Officers can manage applications and remove ordinary members. Members can leave. A leader with other members must transfer leadership before leaving; the last departing member deletes the empty clan and its applications.
- Public responses expose random player IDs, names, avatars, levels and XP. They never expose Unity subjects, account hashes, private profile entries or purchase data. Caller identity is derived from the verified Unity token, never request JSON. Sign-out clears client social caches and pending coroutines.

This is **saved progression**, including offline XP, rather than a server-verified competitive XP ladder. The existing profile backend permits client-calculated offline progress. Competitive anti-cheat requires dedicated-server result receipts and reward validation separately.

Online presence, party invitations from the friend list, chat, clan wars, seasonal clan rewards and clan name/policy editing are not part of this release. Friends and social snapshots refresh when opening a page or tapping REFRESH.

## Hosting and activation

`Server/commerce` is backend source, not a running server. Rebuild and deploy the account Docker image described in [Account-Server-Setup.md](Account-Server-Setup.md), retain the database volume, configure `DF_UNITY_PROJECT_ID`, and expose it through HTTPS. Google merchant credentials are unnecessary for friends/clans/account storage. For the existing Cloud Run/Firestore backend, keep `DF_ACCOUNT_STORAGE=firestore` and the existing project/namespace configuration; no SQLite volume is needed for durable Firestore data.

SQLite social tables are created automatically and existing cloud profiles are indexed when the service starts. Firestore creates namespace-prefixed social collections lazily and indexes accounts on wallet/profile loads. To index profiles that predate this release, run `PYTHONPATH=Server/commerce python Server/commerce/firestore_social.py` once with the same Firestore project/namespace and server credentials. Back up the database before upgrading. New profile saves update the public player name/level and therefore clan totals. Public IDs remain stable across restarts and devices.

Set `Assets/Alterverse/Resources/Commerce/AccountServerConfig.json` to the actual HTTPS endpoint. That file remains empty in source because no deployed account-service address was supplied. Until configured, the menu explains that an account connection is required; local gameplay remains available.

All routes use POST with `Authorization: Bearer <Unity access token>` and a JSON object. Successful replies are `{ "fulfilled": true, "social": { ... } }` with `Cache-Control: no-store`.

| Route | Input |
| --- | --- |
| `/social/state` | `{}` |
| `/social/players` | `query`, `offset` (optional) |
| `/social/levels` | `offset` (optional) |
| `/social/clans`, `/social/clan-levels` | `query`, `offset` (optional) |
| `/social/friend/request`, `/social/friend/accept`, `/social/friend/remove` | `playerId` |
| `/social/block`, `/social/unblock` | `playerId` |
| `/social/clan/create` | `name`, `tag`, `description`, `open` |
| `/social/clan/join`, `/social/clan/cancel` | `clanId` |
| `/social/clan/accept`, `/social/clan/reject`, `/social/clan/kick`, `/social/clan/transfer` | `playerId`; clan is derived from caller membership |
| `/social/clan/role` | `playerId`, `role` (`member` or `officer`) |
| `/social/clan/leave` | `{}` |

Mutations return the updated account social snapshot. Invalid input returns 400, invalid authentication returns 401, forbidden clan management returns 403, and unexpected server outages return 503. Friendship/join/accept retries do not duplicate rows. Clan creation or leadership transfers with a lost response require refreshing state before retrying; the client does not automatically replay mutations.

Firestore applies the shared SQL rule engine to a disposable local representation of the relevant relationships inside a Firestore transaction. The durable state remains in Firestore. Actor/target/clan document writes serialize quota and membership decisions, and deterministic name/tag documents serialize unique clan creation. Search and ranking currently scan public directory metadata; large populations will need indexed search/ranking and materialized clan totals to reduce read costs. Private account profiles are not scanned by ranking queries.

## Verification

```bash
python -m unittest discover -s Server/commerce -v
```

Tests cover migrated accounts, stable public IDs, no private profile leaks, the complete level curve, ranking ties/pagination, profile-driven score updates, duplicate names, SQL search literals, friendship acceptance/retries, blocks, persistence, role authorization, applications, leadership transfer, clan deletion, concurrent capacity checks and one-clan membership, bounds and rate limits, and authenticated HTTP behavior. The same social scenarios run through a strict fake Firestore transport that rejects reads after queued writes; it does not replace testing against the Firestore service/emulator. Existing account/purchase tests must also remain green.

Unity Editor compilation and actual Android layout, account switching, two-device friendship/clan actions, cloud-save refresh and production HTTPS authentication remain required before release. A C# syntax parser is not a Unity compiler.
