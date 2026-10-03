> Updated account/profile and wallet architecture: [Account-Server-Setup.md](Account-Server-Setup.md). Historical notes below must not be used to enable the disabled local claim protocol.

# DIMENSIONS FRONT — Google Play shard integration

Status: integration code and reference ledger delivered; **EXTERNAL GOOGLE PLAY CONFIGURATION REQUIRED**. Live payment, Unity compilation, Android build, store callbacks and device purchase recovery: **NOT VERIFIED**. The store deliberately stays unavailable without authenticated backend configuration. No simulated payment grants run in Android builds.

## Products and authoritative prices

Create three consumable one-time products in the Play Console for the actual signed Android application:

| Product ID | Shards |
|---|---:|
| dimensions_shards_500 | 500 |
| dimensions_shards_1200 | 1200 |
| dimensions_shards_5500 | 5500 |

Configure local prices, regional availability, merchant/payment setup and activate products in Play Console. The UI displays Google Play metadata prices; no hardcoded currency amount or pretend checkout. Publish a signed internal-test build and configure license testers before testing. Unity must resolve `com.unity.purchasing` 5.4.3 and its dependencies from Packages/manifest.json; package resolution and generated lockfile changes require the Unity editor.

The shared `Resources/Commerce/ShardCatalog.json` supplies quantities, item prices and unlock writes to client and server. Earned progress is part of the account profile; purchased shards remain in the verified ledger. The UI shows one summed balance, and server purchases can use both earned and purchased shards atomically. See [Account-Server-Setup.md](Account-Server-Setup.md) for the current bootstrap, profile storage and deployment flow.

## External service and identity setup

`Server/commerce/application.py` is a WSGI reference integration, not an already deployed service. Supply:

1. A real account login service and an `authenticate` function validating bearer signature, issuer, audience and expiry. Its result must be the stable opaque account hash also passed to Google Play as the obfuscated account ID. Never trust a client-supplied account name.
2. A server-only Google authenticated session with Android Publisher permissions for the exact application package. Never put service-account credentials in the Unity client.
3. An HTTPS host, production database storage/backups and operational monitoring. The SQLite reference requires one shared durable database; do not deploy separate writable copies behind multiple replicas.
4. A production refund/chargeback and revoked-entitlement reconciliation flow (including Google notifications), account deletion policy. These are not implemented in this pass and block public paid release. Paid cosmetic entitlements now stay in the authenticated session snapshot, separate from offline earned ownership. Logout clears them; reconnect retrieves them from the server. Offline use of paid cosmetics after restart is intentionally unavailable until a secure account-scoped cache exists.
5. Configure `Resources/Commerce/AccountServerConfig.json`; `AccountServerBootstrap` now calls `PaidShardCommerce.Connect` automatically after Unity Authentication. No hardcoded endpoint or unauthenticated fallback exists. The token provider must return a valid access token without throwing. Refresh tokens through the identity provider. Call `PaidShardCommerce.Disconnect()` on logout, before changing identity; call `Connect` for the new session. Both services survive scene changes. Switching sessions cancels outstanding client requests, and stale responses cannot equip cosmetics or acknowledge purchases for a new session. Store recovery gates new purchases until its initial query succeeds; all pending tokens remain blocked until confirmed. Device verification of these guards is still required.

The server queries Google for package/product/token, accepts only completed purchases tied to the authenticated account, and atomically journals tokens before crediting. Repeated callbacks and process restarts return the same wallet without another grant. The client confirms/consumes only after that durable response. Lost responses leave orders available for recovery. Pending, cancelled and rejected purchases grant nothing. Failed server verification requires reconnect/retry; customer support/reconciliation is necessary for permanently invalid or already-consumed unknown tokens.

## Verification performed and remaining gates

Run isolated ledger tests (temporary wallets, no real currency):

`python -m unittest discover -s Server/commerce -v`

Covered: concurrent duplicate callbacks, persistence/restart, pending/cancelled states, account mismatch, token reuse, idempotent cosmetic debit, insufficient balance and unknown consumed tokens. HTTP boundary tests additionally cover required authentication, malformed payload rejection before Google access, ignored client-supplied amounts/accounts, upstream outages, retry after a lost fulfillment response, and cosmetic purchase plus wallet refresh. **The current server test count and scope are documented in Account-Server-Setup.md.** These are isolated server tests with a fake Google transport, not live Google or Unity Editor checkout tests.

The main Editor shop intentionally reports store unavailable. `DIMENSIONS FRONT > Development > Shard Shop Sandbox` provides an isolated, explicitly labelled simulation of pending/confirmed/cancelled purchases, duplicate callbacks, cosmetic debit and equip. It never changes the real wallet/profile and is excluded from player builds. This is a test harness, not proof of store integration. Native Android SDK calls are excluded in Editor builds; use a Play internal-test device for the real purchase flow. Still required: Unity compile, all four localized prices, cancellation, deferred payment, interrupted verification, relaunch recovery, multiple pending purchases, account changes, refund reconciliation, resume after Play overlay, and purchase-to-equip verification. Do not enable public payments before those gates pass.

References: [Unity purchase lifecycle](https://docs.unity.com/en-us/iap/purchases), [Google billing security](https://developer.android.com/google/play/billing/security).


## Abschlussaudit 2026-09-19

Aktuelle Freigabegrenzen und Data-Safety-Befunde: [GooglePlayReleaseChecklist.md](GooglePlayReleaseChecklist.md). Kontowechsel während Store-Connect wird erneut geprüft; fehlerhafte Credential-Provider werden abgefangen und 401/403 sperrt die Session. Alte Founder-/Recall-Entwicklungs-Grants sind Editor-only. Das ersetzt keine echten Login-, Pending-Order-, Accountwechsel- oder Geräteprüfungen. Refund-/Chargeback-Reconciliation fehlt weiterhin.

Der Microsoft-GDK-Dialog aus Purchasing 5.4.3 erscheint für Windows/Xbox-Buildziele; für Android kein GDK installieren und in Unity das Android-Build-Profil aktivieren. Der Projektadapter erzeugt explizit `StoreController("GooglePlay")` ausschließlich in `UNITY_ANDROID && !UNITY_EDITOR`.
