# Rewarded shard ads

The Shards shop has a voluntary `WATCH AD` play button beside the monthly
subscription. A completed Google rewarded ad grants exactly **2 earned shards**.
The subscription purchase and management controls remain in the left-hand panel.

## AdMob configuration

- Android app ID: `ca-app-pub-1716027263297702~9368994561`
- Rewarded ad unit: `ca-app-pub-1716027263297702/1813938170`
- Placement name: `Store_Shards_Rewarded_2`
- AdMob reward: amount `2`, item `Shards`
- Format: ordinary rewarded ads (voluntary player action).

The IDs are public application identifiers, not credentials.

Google Mobile Ads **11.5.0** is pinned via OpenUPM. It uses the existing
`com.google.external-dependency-manager` resolver, whose direct project pin stays
at **1.2.188**. Version 11.6.0 introduces a different Unity resolver; this change
does not install both resolver implementations side by side.

Unity resolves the package when opening the project. The committed Google settings
asset already contains the Android app ID. Do not import a second `.unitypackage`
copy of the SDK. If Android dependencies have not resolved, use the existing
Android Resolver's Force Resolve action, then build an Android development player.

Development builds use Google's rewarded **test** unit from the pinned SDK sample.
Release builds use the real placement above. The Unity Editor and dedicated server
never show ads or grant simulated rewards. This is a client change; it does not
require a dedicated game-server deployment.

## Before a production rollout

1. Finish AdMob account/app verification. Confirm that the published Play listing's
   developer website hosts the exact publisher entry at `/app-ads.txt`.
2. In AdMob Privacy & messaging, configure and publish the applicable user messages
   for this Android app, including the European regulations message where required.
   The client calls UMP at every launch, waits for consent before requesting ads,
   and exposes `AD PRIVACY` in the Shards shop when UMP requires that entry point.
3. In the ad unit's advanced settings, review the ad types. If this placement should
   serve video only, select Video and disable other selectable types. The screenshot
   that supplied the IDs showed creation success, not these advanced settings.
4. Update the existing Google Play data-safety and advertising declarations and
   privacy policy to reflect the actual Google ads integration before release.
5. Test on Android: loading, completion (+2), early close (+0), unavailable ads,
   reopening, background/resume, consent choices, account switching and cloud sync.

## Reward storage and validation boundary

Only the SDK's earned-reward callback can enqueue a reward. Clicking, closing,
load failure and presentation failure never grant currency. Unity/profile calls
are marshalled to Unity's main thread. Duplicate callbacks for an impression are
ignored. The earned balance and a receipt marker are committed together through
the existing `ProfileTransaction` journal and account cache, so normal profile
synchronization includes the reward.

Pending earned callbacks are persisted locally and tied to the account that
started the ad; another account cannot collect them. The original account can
collect the pending reward after signing back in on the same device. Removal is
persisted after each receipt, so interruption/replay of a pending receipt does
not mint a second reward.

This follows the game's existing client-earned currency model. It **does not**
implement cryptographic AdMob server-side verification, and must not be described
as server-verified anti-cheat. Leave the optional AdMob SSV callback URL unset
until a separate authenticated, signed-callback server integration exists.

## Automated checks

`python Tools/Shop/RewardedAdsRegression/run.py --dotnet-root "$DOTNET_ROOT"`

This compiles the real service, payout, wallet, profile cache and transaction
journal against controlled SDK/Unity fixtures for Android release, Android
development, Editor and server defines. It exercises double taps, duplicate and
late callbacks, early close, presentation failure, cross-account delivery,
pending-receipt replay, privacy invalidation, ad expiry and bounded retries.
The GitHub Actions workflow also runs the existing account regression harness.
These fixture checks do not replace a Unity build or Android-device validation.

Official references:
- https://developers.google.com/admob/unity/quick-start
- https://developers.google.com/admob/unity/rewarded
- https://developers.google.com/admob/unity/privacy
- https://github.com/googleads/googleads-mobile-unity/tree/v11.5.0
