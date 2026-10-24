# Standalone Node account-server clan restoration

The operator-supplied source reports revision 2026-10-22.2 and explicitly rejects
every social route except player levels. This restores the existing clan and
private-chat protocol in one self-contained index.js for the existing Google
Cloud Functions Framework Node editor. The earlier paired Node/Python proposal
is superseded. Python, Docker and a companion process are not runtime requirements.

## Preserved behavior

- Existing clan IDs, public player IDs, memberships, roles and chat documents.
- Server-generated clan names/tags and the same preset descriptions.
- A 500-Shard creation cost, earned Shards first, then paid balance. Debit,
  membership and the operation receipt commit in one Firestore transaction.
  Retrying creation, including after leaving, cannot debit or create again.
- Open joins, applications, acceptance/rejection, leaving, capacity limits,
  officer/member roles, leadership transfer and authorized kicking.
- Clan-level ranking sums member levels. Clan images use a leader-owned shop
  icon and survive leadership transfers.
- The existing creator moderation agreement requires active acceptance of its
  current version. Consents and timestamps use the same records. Chat consent
  and age-group rules, private-room authorization, warnings, mute/unmute,
  rejoining bans, blocking, reports and administration retain the existing
  protocol. No agreement is accepted on a player's behalf.
- Encrypted SMTP report delivery retains the configured recipient, durable
  outbox/lease/retry behavior and omission of private chat evidence.
- Existing disabled friend/discovery/legacy-message/identity routes stay disabled.

All **45 original Node functions** and the supplied catalog are preserved byte
for byte. Only the unsupported-social handler, authenticated suspension check,
post-save directory synchronization and revision marker are patched. Purchases,
shop/profile writes, authentication, receipt replay, subscriptions and Google
RTDN retain their implementation. Failed directory synchronization cannot roll
back or repeat a committed profile save.

## Source and persistence

The supplied services_dimensions-front-purchase-server_1792759012.950000 (1).zip
contains index.js with SHA-256:
162bbdc85e02192d5b30240cc9acc1656545eea7f8cc6bcf8774c221e1921c88.
The original package.json is unchanged (SHA-256:
a65aa9078d0f999d90f890f1896246624f605a3060f3d15b2f641fb9444ae9e1).
The builder accepts the reviewed compact/formatted exports, preserves later
catalog entries when supplied, and rejects unexpected source boundaries and
duplicate application. Rebuild from a fresh export if the live source changes.

The operator screenshot verifies project dimensions-front-play-games, database
(default), and prefix dimensions-front: clan/member/message, creation, consent,
chat-room and related collections are visible. Actual document contents and
account matching have not been inspected live.

Durable state stays in Firestore. Built-in node:sqlite materializes only the
request scope in memory inside each Firestore transaction, using the existing
SQL schema. All cloud reads finish before writes. Actor/clan document locks and
unique-name claims guard concurrent membership, capacity and identity allocation.
History is scoped by authoritative membership before message queries.
Node wallets remain in players; legacy dimensions-front-accounts balances and
purchase records are not copied, reset or replaced.

The embedded schema, identity catalog, moderation terms, storage keys and Unicode
case-fold data are generated from the existing Python implementation. Python is
a build/test input only and is not needed by the deployed index.

## Build and verification

    node Server/commerce/node/attach-standalone-clans.mjs CURRENT_INDEX.js NEW_INDEX.js

The generated file uses the existing ES-module package.json, built-in Node
modules and existing dependencies. Node 22.13 or later is required for built-in
SQLite without a flag; validation used official Node **22.23.3**, with the
distribution checksum verified. SQLite is imported lazily on clan requests.

Local validation:

- **53 standalone cases**: existing clan/chat scenarios against the actual
  generated Node implementation, full HTTP clan/purchase/profile paths,
  old membership/chat/ranking, consent, exact debit/retry, ledger isolation,
  metadata/receipt preservation, delivery leases/retries and failure paths.
- **102 integrity checks** across compact and formatted exports, including all
  original function bodies, catalogs, later additions and single-file scope.
- **23 SMTP checks** for TLS/STARTTLS, certificate/hostname verification,
  configured recipients, privacy, invalid settings and connection failures.
- Embedded-data parity, syntax checks, and startup/health checks with the actual
  unchanged Functions Framework/package dependencies under Node 22.

Firestore and authentication are substitutes. No real account, receipt, native
Android build or production load was used. Existing chat indexes, TTL,
administration/SMTP environment and service identity must be preserved.
The earlier working Cloud Run revision has not been identified; the agent's
browser still cannot access the Cloud Console.

## Release status

**Implementation prepared; no live deployment or AAB update performed.**
The operator asked to wait until the other ongoing game work is complete before
the joint server/AAB release. The game branch
codex/epic-toon-replacement-20261024 is not changed by this repair.

During the joint release, verify current source/catalog, runtime and settings,
retain the prior revision, and validate existing player/clan/chat, wallet,
receipt/subscription and memory behavior in a candidate before moving traffic.
No database migration is included.
