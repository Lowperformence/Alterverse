# Shop, Play Games and monthly Shards

Implementation prepared on 2026-10-21. This changes source code, not the installed Android build or deployed account server. No live purchase or native sign-in has been performed by these tests.

## Offers to create in Play Console

| Type | Product ID | Delivery | German EUR price |
|---|---|---|---|
| Consumable | `dimensions_shards_12000` | 12,000 Shards per purchase | **€100.00** |
| Auto-renewing subscription | `dimensions_shards_monthly` | 1,200 Shards after every successful monthly payment | **€7.49/month** |

The subscription's only base plan must be `monthly`, recurring period **P1M**. Configure this plan as backwards compatible for the pinned Unity IAP 5.4.3 adapter. Do not add trials, introductory offers, annual/prepaid plans, plan changes or installment plans under this SKU: those require an explicit implementation and acceptance tests. The adapter ignores trial/annual product metadata and the server rejects unsupported offers and plans. Configure regional prices/availability in Google; the app uses Google's localized metadata. These products have not been created or activated by this source change.

The existing 500/1,200/5,500 packs remain consumables. The UI compares the subscription's price per Shard against the cheapest currently available same-currency one-time pack, rounds the saving down, and hides the claim when there is no verified price advantage. No fixed percentage or fabricated original price is displayed. The subscription is optional; its monthly billing, automatic renewal and Google cancellation link are visible before checkout.

## Server deployment before enabling subscription sales

Deploy the updated `Server/commerce` with the same durable Firestore namespace/database as the existing account service. Do not deploy a separate empty wallet database. Keep `DF_ENABLE_GOOGLE_PLAY=1` and the exact package `com.lowperformence.dimensionsfront`, using the existing server-only Android Publisher identity.

Set up Google Play RTDN to a Pub/Sub topic and an **authenticated push subscription** targeting `/google-play/rtdn`. Give Google's Play notification publisher the topic publishing role. Configure retries, a dead-letter queue, and alerts for delivery failures; verify the Play Console test notification reaches the service.

Only after the notification path is configured, set:

```text
DF_ENABLE_SHARD_SUBSCRIPTIONS=1
DF_RTDN_AUDIENCE=https://YOUR-ACCOUNT-SERVICE/google-play/rtdn
DF_RTDN_SERVICE_ACCOUNT=YOUR-PUSH-IDENTITY@YOUR-PROJECT.iam.gserviceaccount.com
```

The audience must exactly match the Pub/Sub push OIDC audience. The push identity must be dedicated to this trusted notification path. The endpoint checks Google's JWT signature, issuer/audience/expiry and exact verified email; it does not accept a player session or an arbitrary JSON user ID. Keep these service credentials on the server. Client snapshots advertise `subscriptionsReady=false` until the feature is configured, which keeps the subscription checkout disabled.

Both client receipt recovery and RTDN query `purchases.subscriptionsv2.get` using the actual purchase token. Account binding, product, base plan and expiry are checked. Missing Google order IDs are individually verified with `orders.get`, including historical renewals after a notification outage. Each processed order grants the fixed catalog amount once in the same durable transaction as the wallet. Initial purchase, duplicate notifications, restart and client recovery cannot grant twice. Long histories resume in bounded batches on retry. Unsettled orders retry; newly encountered cancelled/refunded orders and zero-cost reactivations do not grant currency. Tokens, Google order IDs and wallet writes stay account-bound. Acknowledgement follows durable delivery.

Monitor dead-letter messages and reconcile exceptions, unsupported legacy/token-replacement histories, failed acknowledgements and expired Google API token access. This is not a completed general refund/chargeback clawback system: previously spent or credited currency is not automatically reclaimed by this change. The existing public-payment release gates remain in force.

## Play Games corrections and outstanding setup

The published Most Wins leaderboard is `CgkI57GljPsdEAIQAA` (lowercase `l` after `G`), not the previous uppercase-`I` variant. The Android manifest now contains the real App ID `1029475752167`. Native ranking viewing no longer depends on loading the cloud profile. Uploading scores still requires the correct verified linked account and its cloud profile.

The repository's `PlayGamesSettings` Web Client ID is still empty. Complete the Google Game Server OAuth web-client setup in Unity Android Setup and the corresponding Unity Authentication Google Play Games provider before account linking can be accepted. No OAuth client secret belongs in this repository or the game. The Play Console “Ressourcen abrufen” action was blocked by automatic approval review because it could expose OAuth configuration; no alternate credential retrieval was attempted.

The store now bounds lost Connect/product/purchase-recovery callbacks, retries, lets RECONNECT cancel a hung startup, and ignores callbacks from retired controllers. This addresses a concrete hang path; the exact installed-build regression still needs device verification.

## Verification

```sh
python -m unittest discover -s Server/commerce -q
python Tools/Shop/GooglePlayConnectionRegression/run.py --dotnet-root /path/to/dotnet
python Tools/Shop/AccountRegression/run.py --dotnet-root /path/to/dotnet
python Tools/Shop/VisualRegression/run.py --dotnet-root /path/to/dotnet
```

Validated locally: 272 Python backend tests, 37 production Android store/ranking checks, 29 isolated internal-test checks and 15 delayed account/commerce scenarios. The layout checks pass at all three widths. The concurrency negative control fails as intended when serialization is deliberately removed.

The C# fixtures compile the actual store/session/ranking code in production Android, explicitly isolated billing-test and non-Android branches; the account fixture compiles actual wallet/cache/commerce coroutines. The layout fixture compiles the actual shop method/catalog/artwork and checks controls at 672/992/1360 virtual-pixel content widths. Optional rendered previews approximate fonts/panels and are not Unity/device screenshots.

Before release: resolve packages and compile in Unity, deploy the server, create/activate the products in the test track, install the signed Play build, test linked-account restore after restart, ranking open/upload, purchase/cancel/deferred states, each one-time pack, initial subscription, accelerated test renewal with the game closed, delayed notifications, cancellation, expired/on-hold recovery, acknowledgement, and a different-account attempt. The local billing-grant test build intentionally cannot sell subscriptions; use a production-path build with Google license testers and the real test backend.

References: [Google subscription lifecycle](https://developer.android.com/google/play/billing/lifecycle/subscriptions), [SubscriptionPurchaseV2](https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.subscriptionsv2), [Orders API](https://developers.google.com/android-publisher/api-ref/rest/v3/orders), [Authenticated Pub/Sub push](https://docs.cloud.google.com/pubsub/docs/authenticate-push-subscriptions).
