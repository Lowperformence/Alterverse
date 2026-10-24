# Clan restoration for the current Node account service

The live service reports `2026-10-22.2`, implements player levels, and rejects the other social routes. The earlier clan implementation still exists in Python. This change reconnects that existing implementation to the Node service's current account documents instead of rewriting clan rules or replacing purchase/subscription processing.

## Prepared behavior

The primary Node entry point remains `helloHttp` on the existing HTTPS endpoint. Verified social requests are forwarded to the existing Python WSGI rules on **127.0.0.1:8081 inside the same container**. That process exposes social routes only. The existing Node handler retains Google Play purchases, receipt idempotency, subscriptions, RTDN, profile saves, catalog and ownership writes.

`NodeAccountLedger` reads the same `players/<account hash>` documents and wallet/profile revision fields used by Node. It never creates an alternative wallet or reallocates the canonical name. Clan creation uses the existing shared Firestore transaction for membership, its 500-Shard earned/paid debit and its operation receipt, while preserving additional Node account metadata. The original Node decorator supplies billing/subscription flags on returned clan wallet snapshots. Account suspension checks also guard the Node account routes; Google-authenticated RTDN retains its original handling.

The **actual existing social namespace must be supplied explicitly**. Its existing public player IDs, clan IDs, membership/roles, applications, icons, creation receipts, chat/moderation and outbox collections are reused. No namespace/data migration is performed. Startup refuses an empty clan namespace unless an operator has separately verified that an empty installation is intended and explicitly sets `DF_SOCIAL_ALLOW_EMPTY_NAMESPACE=1`.

Committed Node profile saves synchronize the public clan directory through the existing profile-sync implementation. A delayed directory refresh does not invalidate or repeat a committed save. Private credentials, purchase tokens and receipt data are not forwarded to the clan process; it receives the player's original authorization plus social request data. Both authentication implementations remain in place.

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
- **58 Node-adapter scenarios passed** across the shared clan/chat scenarios and targeted compatibility checks: retained old membership/public IDs, exact earned/paid debit and retry, stale saves, forged account fields, retained Node metadata/purchase records, closed billing routes, suspension and namespace/maintenance configuration.
- **62 JavaScript checks passed** using the actual retained Node functions against network/Firestore fixtures: unchanged authentication/profile/purchase/subscription functions and catalog text, loopback routing, errors, scope, original Google grant/replay and shop debit/retry.
- **15 two-process HTTP checks passed** between the patched Node handler and actual Python rules over loopback, using Firestore/authentication fixtures: old clan discovery, explicit empty membership, creation/debit/retry, leave/join and summed-level ranking.
- **5 release-builder tests and 10 Firestore setup tests passed**; Python/JavaScript syntax and shell syntax checks passed.

The historical Node source and package under `Tools/ClanRestore/fixtures` are test inputs only. No current live export, real database, signed Google login or Android device was used by these tests. Docker is unavailable locally. The compatibility workflow builds an image from the historical fixture, without cloud credentials or deployment. **The actual release image from the latest live export has not been built or load-tested.** Two processes have a different memory footprint from the previous Node-only image; measure it in the candidate revision before choosing its resources or traffic.

## Deployment status and remaining verification

On 24 October the Google Cloud revisions page still returns `Site Unavailable` in this browser, and local `gcloud`/Docker are unavailable. The real earlier clan revision, existing namespace and latest live source/catalog therefore remain unverified. No image, traffic, environment, database or AAB has been updated. The code is prepared for review; it is not a verified live fix.

With operator access, build the paired image from a fresh source export, create a candidate revision without moving production traffic, and test with the user's existing accounts and clans. Confirm the old clan ID and membership, clan ranking, chat/consent/moderation and a retry of one creation operation. Verify normal profile saves, a shop ownership restore, Google receipt replay and subscription/RTDN behavior without re-crediting receipts. Then move traffic using the existing service's normal release process.

The clan repair uses existing client routes and does not itself change Unity gameplay/UI code. The separately prepared menu-background change requires its own native Android validation and AAB. Edgegap match-server optimization is also separate from this account-service release.
