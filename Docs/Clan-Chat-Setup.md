# Private clan chat

This extends the existing Unity `SocialService`, IMGUI frontend, Python account server and SQLite/Firestore social adapters. All new player-facing text is English. Generated identities remain in use; public player search and friend messaging remain disabled.

## Behavior

Only current clan members with current chat consent may read, write or report messages. The room comes from authoritative membership; injected clan/message IDs are rejected. Leave, kick, clan ban and closure revoke subsequent requests immediately. The visible UI detects changes on its next poll, normally within two seconds; already delivered information cannot be recalled from a malicious client.

Only the visible, focused chat polls. Unchanged Firestore rooms read membership, suspension, consent and room revision without loading history. Updated rooms return up to 50 visible messages from a bounded 100-message candidate window; SQLite returns up to 50 directly. This is near-real-time polling, not WebSockets. Message bodies display as plain text with server-issued names and local display times. Private caches clear on disconnect, focus loss, membership changes and failed authorization. Failed sends retain the draft and retry operation ID.

Founders must actively check the moderation agreement before creation. Consent version/time, clan creation and its debit commit atomically. Existing leaders must accept before using chat moderation; recipients of a leadership transfer must also accept first. All members accept the chat rules before first use.

The age-group selection stores no birthday and refuses under-13 selections. This is self-declaration, **not verified age assurance**. All age groups receive strict contact/link filtering, plain text, reporting and blocking; no direct messages or attachments are introduced. Decide whether verified age or parental consent is needed for the intended audience before release.

Leaders may warn, mute (UI: one hour; API: 1 minute–7 days), unmute, remove and ban from rejoining. Existing officer roles and clan icon controls remain available. Warnings and mute expiry appear in the affected player's chat. Blocks hide messages in both directions and can be reversed from the blocked list.

Message/player reports store IDs, reason, reporter/target public IDs, server time and private evidence. Duplicate retries are idempotent. Evidence survives message removal. Only configured admins can inspect reports, remove messages, suspend/restore account API access, resolve reports or close clans. Admin leadership reassignment is available through `/social/chat/admin/action` with `action=transfer`, `clanId` and the new leader's `playerId`, after that member accepts the agreement. Account suspension blocks subsequent account/social/ranked API requests; it does not forcibly disconnect an already running match.

Messages are limited to 500 Unicode characters. Server limits: 12 sends/minute, at least 1.5 seconds between sends, repeated-text suppression for 30 seconds, 5 new reports/minute, bounded moderation actions. The configurable word filter is a basic filter, not a complete detector of harassment or personal data. Links, email/phone-like contact details and control/bidirectional characters are rejected. Clients cannot grant themselves roles or admin permissions.

## Required configuration

Keep the existing HTTPS endpoint, Unity project ID, authentication and durable storage. No new runtime dependencies were added.

| Server setting | Purpose |
| --- | --- |
| `DF_ENABLE_CLAN_CHAT` | Defaults to `1`; `0` preserves the previous disabled-communication behavior. |
| `DF_CHAT_ADMIN_ACCOUNTS` | Comma-separated server account hashes, **not public IDs or names**. Derive each using SHA-256 of the Unity player subject, exactly as `authentication.py` does. Configure your admin account before enabling chat for players. |
| `DF_CHAT_ADMIN_EMAIL` | Optional fixed email recipient. Empty disables notifications, not report storage. |
| `DF_CHAT_SMTP_HOST` | Your SMTP hostname. |
| `DF_CHAT_SMTP_PORT` | Default 587 for STARTTLS, 465 for TLS. |
| `DF_CHAT_SMTP_MODE` | `starttls` (default) or `tls`; plaintext SMTP is refused. |
| `DF_CHAT_SMTP_FROM` | Sender authorized by the mail provider. |
| `DF_CHAT_SMTP_USER`, `DF_CHAT_SMTP_PASSWORD` | Optional mail authentication; deployment secrets only. Never add these to Unity assets. |
| `DF_CHAT_FILTER_WORDS` | Optional comma-separated blocked phrases; a small English/German default list is included. |
| `DF_CHAT_MESSAGE_DAYS` | Default 30; permitted range 1–365. |
| `DF_CHAT_REPORT_DAYS` | Default 180 for reports, evidence, audit and outbox; permitted range 1–365. |

Consent version/time stays for the account's lifetime; blocks and sanctions remain until removal or account erasure. Process erasure through server/database administration, including these new tables/collections. Restrict service-account and backup access. Avoid logging bodies, evidence, tokens or SMTP secrets. Choose final retention periods for the published privacy policy; defaults do not constitute legal approval.

## Deployment, indexes and deletion

If the UI shows `Connecting to clan chat…` and `Social server unavailable`, an unconfigured Firestore composite index is one possible cause. After the diagnostic backend update, an actual missing-index error displays `Clan chat needs a server database index` instead of the generic outage message. Other outages remain generic; do not infer the cause from the screenshot alone.

On the operator's machine or Cloud Shell, with authenticated `gcloud`, run:

```bash
python3 Tools/ClanChat/setup_firestore.py
python3 Tools/ClanChat/setup_firestore.py --execute
```

The first command inspects only. The script resolves the Cloud Run project from the configured game endpoint (or accepts `--project PROJECT_ID`). The second command creates missing chat/outbox indexes and enables TTL, deriving the actual Firestore project and namespace from the existing service. Existing indexes are retained. Index creation may take several minutes. To also rebuild the existing backend with the diagnostic update, use `--execute --deploy`; deployment is blocked until the required indexes are READY and preserves environment variables and secret references. Operator permissions for indexes/TTL and Cloud Run deployment are required. This script does not configure the administrator account or SMTP credentials.

Rebuild/deploy the existing account image and updated Unity build together. Old clients cannot create new clans on an enabled-chat server without active agreement. Completed creation receipts remain idempotent when retried with consent. Back up durable storage before upgrading.

SQLite creates tables in the existing account database. Firestore creates namespace-prefixed collections through server credentials. `Server/commerce/firestore-chat.rules` is a deny-all reference for a database used exclusively by this account backend; review existing rules before applying it to a shared database. Player SDKs must not access these collections directly.

Merge `Server/commerce/firestore-chat.indexes.json` into existing index configuration. Replace `dimensions-front` with your `DF_FIRESTORE_NAMESPACE` when different. Required composite indexes: `(clan ASC, created_at DESC)` for messages and `(sent_at ASC, next_at ASC)` for outbox. Enable timestamp TTL on `expiresAt` for message, report, audit and outbox collections. Expired messages/reports are excluded from reads even before physical TTL deletion. Maintenance also handles legacy records without TTL fields.

Use the same server environment and credentials for the worker:

```bash
PYTHONPATH=Server/commerce python3 Server/commerce/chat_maintenance.py
PYTHONPATH=Server/commerce python3 Server/commerce/chat_maintenance.py --cleanup
```

Schedule the first command approximately every minute for emails/retries and the second daily for deletion using the existing host scheduler or a Cloud Run job from the same image. No public route/key is required. Firestore cleanup deletes up to 100 expired rows per collection/run; repeat for a backlog or rely on TTL. SQLite deletes all expired rows. The ADMIN tab's `RETRY EMAILS` handles a small batch manually.

Reports are acknowledged after durable storage; notification delivery uses the worker. Failed attempts persist with backoff. Leases exclude simultaneous workers and stale acknowledgements. SMTP delivery is at-least-once: a crash after SMTP acceptance but before acknowledgement can cause a duplicate. Each report uses a deterministic Message-ID. Emails contain metadata only; evidence stays in the authenticated admin view.

## Verification and remaining release checks

```bash
PYTHONPATH=Server/commerce python3 -m unittest test_clan_chat test_social test_firestore_social test_authentication test_application test_safe_identity test_wsgi_billing -q
python3 Tools/ClanChat/Regression/run.py --dotnet-root /path/to/dotnet
```

119 Python tests pass, including 36 clan-chat scenarios across SQLite and a strict transactional Firestore fake. Coverage includes foreign IDs, forged identity/role/admin fields, consent, rejected creation without debit, revocation, warnings/mutes/bans, transfer consent, blocks, filtering/flood/concurrency, duplicate retries, durable evidence, suspensions, unchanged polling, physical retention cleanup, SMTP failure/retry, worker leases and encrypted mail construction. Tests do not access production or send real mail.

C# regression compiles the changed production service/menu partials against engine substitutes and checks consent/cache behavior. This is not a Unity import, visual layout review or Android build. Before release, test two member accounts and a third account in another clan on a test backend, confirm actual Firestore indexes/TTL, receive a real SMTP notification and check the checkbox, keyboard, scrolling and moderation/admin actions in Unity and on Android. No live deployment, live SMTP or device validation was performed here.

The broader existing backend suite has five pre-existing failures involving zero-price shop items and old shard-price expectations. These were reproduced with unmodified backend sources and the current catalog; this change preserves shop prices and those assertions.

## Changed files

- `Assets/Alterverse/Scripts/Profile/SocialService.cs`
- `Assets/Alterverse/Scripts/UI/MainMenuSocial.cs`
- `Assets/Alterverse/Scripts/UI/MainMenuClanChat.cs` and `.meta`
- `Server/commerce/application.py`, `social.py`, `firestore_social.py`
- `Server/commerce/clan_chat.py`, `chat_delivery.py`, `chat_maintenance.py`
- `Server/commerce/firestore-chat.indexes.json`, `firestore-chat.rules`
- `Server/commerce/test_clan_chat.py`, `test_social.py`, `test_firestore_social.py`
- `Tools/ClanChat/Regression/EngineStubs.cs`, `Regression.cs`, `run.py`
- `Docs/Clan-Chat-Setup.md`, `Docs/Social-System.md`

