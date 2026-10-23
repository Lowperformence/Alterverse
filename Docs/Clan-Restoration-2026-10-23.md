# Clan restoration investigation — 23 October 2026

## Requested outcome

Restore the clan feature the user reports worked in an older version, preserving its existing data and behavior. Prepare the repair now; perform server and Android AAB updates together with the user on 24 October. The other conversation owns cap/UFO-hat work. No live deployment or database change is authorized for this preparation step.

## Confirmed findings

The app's configured account endpoint is `https://dimensions-front-purchase-server-1029475752167.europe-west1.run.app`. A read-only root request on 23 October returned service `dimensions-front-purchase-server`, revision `2026-10-22.2`.

The retained `index.js` version 3 (53,924 bytes, dated 22 October) declares that same revision. It implements `/social/levels`, then rejects every other `/social/*` request with HTTP 503, code `social_feature_not_deployed` and the exact text shown in the user's screenshot. This establishes the current failure mechanism. It does not establish that clans never worked earlier, that stored clan data was deleted, or which earlier Cloud Run revision supported them.

Older retained Cloud Run source exports were inspected as well:

| Source | index.js SHA-256 | Result |
| --- | --- | --- |
| `services_dimensions-front-purchase-server_1791322930.249000.zip`, uploaded 9 October | `a38122694118c2743fe765bd89a0601fdd8a6e5f88eae4147acb4995f569f7d7` | Node purchase/profile backend; no clan/social routes |
| Same export with `(1)`, uploaded 21 October | same hash | Identical source |
| `dimensions-front-server-update.zip`, 21 October | `98b5d7f7b119062615b0583909cafbcb027d6f4f980752b8a6fcd8c7cf2a3dd6` | Node purchase/profile/subscription backend; no clan/social routes |

These are only the available exports, not an exhaustive history of Cloud Run deployments. The pasted text dated 8 October is a Unity build log, not an older clan service.

## Existing implementation recovered from Git history

The actual clan implementation remains in the Python account backend on `dev/vertical-slice`:

- `36df7b4d8903a57142ee35c525fd452197481603` (7 October): `social.py`, `firestore_social.py` and Unity `SocialService` introduced clans, joining, applications, member/officer/leader permissions and summed-level ranking.
- `0e681b034938ad2f28a45c32f4dca7dc4136c890` (7 October): real membership IDs, atomic 500-Shard creation debit, idempotent retries, owned clan icons and member chat.
- `836b91c3c3d291c9beb07b2d32fb0ecee7d3223d` (12 October): generated immutable identities and communication restrictions.
- `2c1acb57d8ffa5d185800ffa1e63482412878d3f` (14 October): private clan chat, consent and moderation.
- `e2970f2bf2efc80d2f90cb90fa63c6f558cd4b16` (17 October): verified Google Play display aliases.

The current implementation was materialized from repository commit `61b731dab3fe1e76ef4c8db03afe05f52f3213bb`. Its behavior is intact in source; it is not the Node code currently running at the configured endpoint. No replacement clan implementation has been written in this investigation.

## Validation completed

The existing unmodified Python source passed **120 tests**:

```
PYTHONPATH=Server/commerce python -m unittest \
  test_social test_firestore_social test_clan_chat test_authentication \
  test_application test_safe_identity test_google_display_name -q
```

This checks the shared clan rules, strict Firestore substitute, persistence, permissions, creation debit/retries, authentication and aliases. It is not a test against the live database, the earlier deployed service, or an Android device. No claim is made that the live clan problem has been repaired.

## Concrete blocker and continuation

The Cloud Run console and its service revisions page both return `Site Unavailable` in this browser. Local `gcloud` is unavailable. No previous live clan revision, its source/configuration, or the actual clan collections could be inspected.

When access is available with the user, inspect Cloud Run's retained revisions and export the last known clan-enabled source plus the current source. Identify the existing Firestore project, namespace and clan collections before preparing a restoration patch. Preserve the current Google Play verification, purchase/subscription receipts, balances, profiles and hat catalog additions. Restore the existing clan handler/data mapping rather than creating an empty parallel clan store.

The storage distinction matters: the current Node source uses `players` and Node receipt collections; the Python ledger defaults to `<namespace>-accounts` and `<namespace>-purchase-tokens`, with `<namespace>-social-*` clan collections. Their account document fields look similar, but replacing the runtime blindly is not a verified migration and can select different saves or receipt ledgers. Likewise, reverting an entire old revision can discard newer billing fixes.

Before traffic is changed, validate existing membership and ranking with real accounts, one idempotent 500-Shard creation operation, and current purchase/profile behavior in a non-live revision. The user has deferred deployment to the joint update. The Edgegap dedicated match server is separate from this account/clan service.

## Other prepared work

- Draft PR #48: dedicated-server hazard presentation optimization; no live deployment.
- Draft PR #49: menu startup candidate; 53 client plus 3 server lifecycle/draw checks passed, native Android contrast/crop validation still pending.
