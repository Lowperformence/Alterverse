# Account server and multiplayer fixes

The code now synchronizes the complete typed DIMENSIONS FRONT profile under the same Unity Authentication identity used by multiplayer. This includes account XP/derived level, lifetime match statistics, avatars, hero/map/weapon/cosmetic unlocks, battle items, Battle Pass and daily missions, onboarding, equipment and preferences.

## Behavior

- PlayerPrefs access in game profile code goes through an account-scoped cache. Switching identity does not inherit another account's cached progress.
- The first empty server account can import the existing offline profile once per installation. Legacy key types and common dynamic keys are enumerated in `Resources/Commerce/LegacyProfileKeys.json`; subsequent reads/writes register keys automatically.
- Dirty profiles save periodically and on application pause. Saved account state loads at authentication and survives backend restarts through SQLite.
- Every profile save includes its expected revision. A stale device receives HTTP 409 instead of overwriting a newer profile. The client loads the server version and preserves its conflicting offline version under a device-only conflict key for support. It does not silently merge rewards or claim masks.
- Purchased shards remain in the Google-verified ledger. Earned shards remain in `DF_Shards` within the cloud profile. The player sees a single summed balance. Rewards only increase the earned part, so remote paid shards cannot accidentally be copied into local currency.
- `/wallet/spend` derives prices and unlock writes from the shared catalog, computes the earned/paid split on the server, and commits the debit and inventory change atomically. Operation IDs persist before the request; retries, lost responses and consumable retries do not charge twice.
- Legacy `/wallet/claim` and `/wallet/claim/ack` are disabled because transferring paid currency to independent device saves is unsafe. Existing historical claim rows are retained for reconciliation. **If an older commerce server actually handled payments, reconcile already delivered/pending legacy claims before migrating those users.** Previously consumed or spent local value cannot safely be reconstructed by guessing.
- Google Play checkout stays disabled unless the account backend explicitly reports `billingReady`.
- The Dedicated Server roster freezes when its plan is sent. Late participants are rejected rather than enlarging the ACK target. After 20 seconds, peers missing the plan ACK are disconnected and a plan is rebuilt for survivors. Client bootstrap failure clears network match authority and returns to the menu.

The offline game continues to calculate XP, earned rewards and offline inventory on the client and syncs those values as player progress. This is account storage and conflict protection, **not server-validated anti-cheat for offline progress**. The verified paid ledger is never changed through profile-save input. Competitive progression would additionally require dedicated-server-issued result receipts and server validation of rewards.

## Start the account backend

This service needs persistent hosting and a database volume. Do not put it inside an ephemeral Edgegap match deployment: that process is for individual matches.

From the repository root:

```bash
docker build -f Server/commerce/Dockerfile -t dimensions-account .
docker run -d --name dimensions-account \
  -p 127.0.0.1:8000:8000 \
  -v /srv/dimensions-account:/data \
  -e DF_ACCOUNT_DB=/data/accounts.sqlite3 \
  -e DF_UNITY_PROJECT_ID=YOUR_UNITY_PROJECT_ID \
  dimensions-account
```

Use a TLS reverse proxy for the public HTTPS address. All workers must share the same durable database. Back it up before upgrades.

`Server/commerce/authentication.py` verifies Unity JWT signatures using the Unity JWKS, issuer, expiry/not-before, stable subject and exact project ID, then hashes the verified subject with SHA-256. The Unity client uses the same derivation for Google's obfuscated account ID. Passwords/tokens are not part of the saved profile.

In Unity, edit `Assets/Alterverse/Resources/Commerce/AccountServerConfig.json`:

```json
{"httpsEndpoint":"https://YOUR_ACCOUNT_SERVER"}
```

The endpoint is intentionally empty in source because no deployed account-service address was supplied. `AccountServerBootstrap` automatically binds an authorized Unity login to the backend. Account-only hosting does not require Google merchant credentials.

In Unity Dashboard → Authentication → Identity Providers, enable **Username & Password**. Profile → Account → **Secure Account** links a username/password to the existing guest identity, preserving its account ID and progress. **Sign In** loads the same identity on another device. An unlinked anonymous login is installation-bound and cannot restore the account on a different device after losing its Unity session token. Account changes are blocked during matches/parties and must flush the current cloud profile first.

## Enable paid shard packs

After the account service works:

- Configure signed Android internal testing and the three active consumables in Google Play: `dimensions_shards_500`, `dimensions_shards_1200`, `dimensions_shards_5500`.
- Set `DF_ENABLE_GOOGLE_PLAY=1`, the exact `DF_ANDROID_PACKAGE_NAME`, and server-only Google Application Default Credentials with Android Publisher permissions. Mount credentials read-only; never put them in the Unity project or Git.
- Keep `ShardCatalog.json` in the backend image; client/server catalog versions must agree.
- Run real license-tester purchases, cancellations, delayed payment, lost-response recovery, relaunch, account switching, refund/revocation handling and two-device restoration before enabling public payments. Refund/chargeback reconciliation is still separate work.

### Google Cloud Run: resolve "account server billing is not enabled"

The shipped Android app points to the Cloud Run account endpoint
`https://dimensions-front-purchase-server-1029475752167.europe-west1.run.app`.
This is **server-side Google Play Billing**, not the Google Cloud project's billing account.

1. **Deploy the repaired `Server/commerce/wsgi.py` first.** The earlier code
   assigned `google=None` inside `create_application()`, shadowed the imported
   `google.auth` module, and would crash once `DF_ENABLE_GOOGLE_PLAY=1` was set.
   Updating environment variables on an old deployment is not a fix.
2. In **Google Cloud Console → Cloud Run → dimensions-front-purchase-server →
   Edit & deploy new revision → Variables & Secrets**, configure:
   - `DF_ENABLE_GOOGLE_PLAY=1`
   - `DF_ANDROID_PACKAGE_NAME=com.lowperformence.dimensionsfront`
   Preserve the existing `DF_UNITY_PROJECT_ID`, `DF_ACCOUNT_STORAGE`, Firestore
   namespace/project, identity and data storage settings.
3. Enable the **Google Play Android Developer API** for the appropriate Google
   Cloud project, and make the Cloud Run runtime service account authorized to
   access the app in the **Google Play Console** (API access / app permissions).
   Use Application Default Credentials from that service account; do not embed
   JSON keys or tokens in the repository or Android build.
4. Deploy the new revision and check Cloud Run logs for billing initialization
   errors. The authenticated `POST /wallet` response should have
   `wallet.billingReady: true` (this confirms configuration, **not** the
   Play Console product activation or a successful live purchase).
5. Confirm that `dimensions_shards_500`, `dimensions_shards_1200`, and
   `dimensions_shards_5500` are active one-time products for
   `com.lowperformence.dimensionsfront`. Install the test build **through Google
   Play** and use a licensed tester to check prices and an actual test checkout.

If credentials are unavailable or the Android package name is blank, the
patched backend stays up for account/profile access but reports
`billingReady: false`. It never grants shards without Google token verification.
A Google Publisher permission error might still occur at fulfillment even when
`billingReady` is true: verify the Cloud Run service-account permissions and
use a real licensed Play test purchase before public launch. Never turn on
`DF_GOOGLE_PLAY_SHARD_TEST` for production as a workaround.

Backend regression tests include `Server/commerce/test_wsgi_billing.py`.
These tests do not check the deployed Google Cloud service or Play billing.

## Verification

Run:

```bash
python -m pip install -r Server/commerce/requirements.txt
python -m unittest discover -s Server/commerce -v
```

Tests cover authentication forgery/expiry/wrong project, durable profiles/XP/settings after restart, account isolation, concurrent revision conflicts, mixed earned/paid spending, catalog prices, duplicate unlocks, concurrent consumable retries, rollback on insufficient funds, malformed payloads, no client credit to the paid ledger, legacy-claim rejection, purchase replay and Google outages. These use temporary databases and fake Google/JWKS transports; they do not prove live Google/Unity integration.

The Unity editor/compiler and live two-client, Android, login and production-hosting checks must be run in the actual project environment. C# syntax parsing alone is not Unity type checking.

Official references:

- https://docs.unity.com/en-us/authentication/platform-signin/username-password
- https://docs.unity.com/en-us/services-web-apis/client-auth
- https://docs.unity.com/en-us/oas-player-auth-jwks/1.0.0


## Friends, levels and clans

The same persistent account backend now serves social routes, player-level rankings and clans. See [Social-System.md](Social-System.md) for behavior, limits, deployment and verification. Rebuild the backend image and configure the existing HTTPS endpoint to activate them.
