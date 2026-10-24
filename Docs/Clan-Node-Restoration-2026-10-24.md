# Clan restoration for the current Node account service

The live service reports `2026-10-22.2`, implements player levels, and rejects the other social routes. The earlier clan implementation still exists in Python. This change reconnects that existing implementation to the Node service's current account documents instead of rewriting clan rules or replacing purchase/subscription processing.

## Prepared behavior

The primary Node entry point remains `helloHttp` on the existing HTTPS endpoint. Verified social requests are forwarded to the existing Python WSGI rules on **127.0.0.1:8081 inside the same container**. That process exposes social routes only. The existing Node handler retains Google Play purchases, receipt idempotency, subscriptions, RTDN, profile saves, catalog and ownership writes.

`NodeAccountLedger` reads the same `players/<account hash>` documents and wallet/profile revision fields used by Node. It never creates an alternative wallet or reallocates the canonical name. Clan creation uses the existing shared Firestore transaction for membership, its 500-Shard earned/paid debit and its operation receipt, while preserving additional Node account metadata. The original Node decorator supplies billing/subscription flags on returned clan wallet snapshots. Account suspension checks also guard the Node account routes; Google-authenticated RTDN retains its original handling.

The **actual existing social namespace must be supplied explicitly**. Its existing public player IDs, clan IDs, membership/roles, applications, icons, creation receipts, chat/moderation and outbox collections are reused. No namespace/data migration is performed. Startup refuses an empty clan namespace unless an operator has separately verified that an empty installation is intended and explicitly sets `DF_SOCIAL_ALLOW_EMPTY_NAMESPACE=1`.

Committed Node profile saves synchronize the public clan directory through the existing profile-sync implementation. A delayed directory refresh does not invalidate or repeat a committed save. Private credentials, purchase tokens and receipt data are not forwarded to the clan process; it receives the player's original authorization plus social request data. Both authentication implementations remain in place.

The account and clan collections must also be in the **same actual Firestore project**. The adapter resolves the default project used by the inspected `new Firestore()` Node source and rejects a conflicting project override. Cross-project migration is not implemented and must not be approximated by pointing the clan service at a second wallet.

## Preparing the actual release

Export **the current** Cloud Run Node `index.js` and `package.json`, including any hat additions made in the other conversation. Do not use the historical test fixture as the live source.

```
python Tools/ClanRestore/build_release.py \
  --node-index /path/to/current/index.js \
  --node-package /path/to/current/package.json \
  --social-namespace THE_VERIFIED_EXISTING_NAMESPACE \
  --output /path/to/new-release-directory
```

The script creates a new directory, retains the supplied package and catalog code, inserts the loopback transport, copies the existing Python rules/assets and emits a paired Dockerfile/start script plus source hashes in `RELEASE.json`. Unexpected source layout, a duplicate bridge or an existing output directory is rejected. The input files remain unchanged. No cloud API, deployment or database write is performed by preparation.

**This cannot be deployed by pasting only index.js into the old Node source editor.** The prepared Docker image contains Node and Python. The launcher waits for the clan process to pass namespace/configuration checks before starting the public Node process, and stops the instance if either process exits. Only port 8080 is exposed. This replaces the application image on the same service, not its IAM, runtime identity, endpoint or existing secrets.

Verify the existing Cloud Run/Firestore project and namespace before selecting values. Preserve all current environment settings and secret references. Set `DF_SOCIAL_NAMESPACE` explicitly on the Cloud Run revision too, so index tooling and maintenance see the same value as the image. Preserve/configure the existing clan-chat admin, consent, retention and SMTP settings according to `Clan-Chat-Setup.md`.

Before sending traffic, inspect/create the namespace's existing chat indexes and TTL using the operator's authenticated environment:

```
python Tools/ClanChat/setup_firestore.py --project ACTUAL_RUN_PROJECT --social-namespace THE_VERIFIED_EXISTING_NAMESPACE
python Tools/ClanChat/setup_firestore.py --project ACTUAL_RUN_PROJECT --social-namespace THE_VERIFIED_EXISTING_NAMESPACE --execute
```

This helper now recognizes the paired runtime. Its Python-only `--deploy` path is blocked for this layout so it cannot overwrite the Node purchase service. Existing indexes are retained. Maintenance detects `DF_SOCIAL_NAMESPACE` and uses the same Node account/social adapter; the existing TTL/outbox worker instructions continue to apply.

## Validation completed

- **120 existing Python tests passed** for clan rules, Firestore transactions, chat/moderation, authentication, identities, HTTP application behavior and aliases.
- **59 Node-adapter scenarios passed** across the shared clan/chat scenarios and targeted compatibility checks: retained old membership/public IDs, exact earned/paid debit and retry, stale saves, forged account fields, retained Node metadata/purchase records, closed billing routes, suspension and namespace/project/maintenance configuration.
- **62 JavaScript checks passed** using the actual retained Node functions against network/Firestore fixtures: unchanged authentication/profile/purchase/subscription functions and catalog text, loopback routing, errors, scope, original Google grant/replay and shop debit/retry.
- **15 two-process HTTP checks passed** between the patched Node handler and actual Python rules over loopback, using Firestore/authentication fixtures: old clan discovery, explicit empty membership, creation/debit/retry, leave/join and summed-level ranking.
- **5 release-builder tests and 10 Firestore setup tests passed**; Python/JavaScript syntax and shell syntax checks passed.

The Node source and package under `Tools/ClanRestore/fixtures` are fixed regression inputs only. The operator subsequently supplied the current Cloud Console export; the compact export is now included and tested alongside the formatted source. No real database, signed Google login or Android device was used by these tests. Docker is unavailable locally. The compatibility workflow builds an image from the supplied export fixture, without cloud credentials or deployment. **The release image with the production namespace has not been load-tested.** Two processes have a different memory footprint from the previous Node-only image; measure it in the candidate revision before choosing its resources or traffic.

## Operator evidence supplied later on 24 October

The operator's Firestore screenshot shows project `dimensions-front-play-games`, database `(default)`, and the existing collections `dimensions-front-accounts`, `dimensions-front-social-clan`, `dimensions-front-social-member`, `dimensions-front-social-creation`, `dimensions-front-social-message`, `dimensions-front-chat-consent`, `dimensions-front-chat-rate` and `dimensions-front-chat-room`. This verifies the existing collection prefix **`dimensions-front`**. Collection contents, counts, account matching and active Cloud Run environment settings have not yet been inspected.

The supplied `services_dimensions-front-purchase-server_1792759012.950000 (1).zip` contains revision `2026-10-22.2`. Its `index.js` SHA-256 is `162bbdc85e02192d5b30240cc9acc1656545eea7f8cc6bcf8774c221e1921c88`; `package.json` SHA-256 is `a65aa9078d0f999d90f890f1896246624f605a3060f3d15b2f641fb9444ae9e1`. The `1792692442.840000` export differs only by a line break. The `1792688413.400000` export reports `2026-10-22.1` and already rejects non-level social routes. The `1792607591.91000` export has no social implementation. These exports do not identify the previously working clan deployment.

Preparation now accepts the reviewed compact Cloud Console formatting without reformatting or replacing the supplied account code. Firestore project/database overrides and changed profile-save arguments are still rejected. The original account functions and whole catalog are compared byte for byte using Node's own parsed functions, and real Node purchase/replay behavior is exercised for **both** source formats.

Additional local validation: **179 Python cases**, **66 Node checks plus 15 two-process HTTP checks for each source format**, **7 release-builder checks**, and **10 Firestore setup checks** passed. A local release directory was prepared from the supplied export with `DF_SOCIAL_NAMESPACE=dimensions-front`, while rejecting an empty namespace at startup. This is preparation, not a live rollout.

`dimensions-front-accounts` is an existing legacy account collection. The active uploaded Node code uses `players`. The adapter deliberately preserves the current Node wallet and purchase ledgers and reads the old social collections; it does **not** copy old balances, replace Node receipts or charge existing members for restoration. Verify the old membership account key against the current authenticated account before rollout.

## Deployment status and remaining verification

On 24 October the Google Cloud revisions page still returns `Site Unavailable` in this browser, and local `gcloud`/Docker are unavailable. The operator has provided source archives and verified the collection prefix through a screenshot; the real earlier clan revision, actual document contents and active environment settings remain unverified. No image, traffic, environment, database or AAB has been updated. The code is prepared for review; it is not a verified live fix.

With operator access, build the paired image from a fresh source export, create a candidate revision without moving production traffic, and test with the user's existing accounts and clans. Confirm the old clan ID and membership, clan ranking, chat/consent/moderation and a retry of one creation operation. Verify normal profile saves, a shop ownership restore, Google receipt replay and subscription/RTDN behavior without re-crediting receipts. Then move traffic using the existing service's normal release process.

The clan repair uses existing client routes and does not itself change Unity gameplay/UI code. The separately prepared menu-background change requires its own native Android validation and AAB. Edgegap match-server optimization is also separate from this account-service release.
