# Menu startup and Google Play identity — 2026-10-28

This is a production-source audit and controlled regression run, not a Unity Profiler capture or an Android timing measurement. The reported two-second delay was not reproduced on a native device here.

## Changes

- Rewarded videos previously started loading only when the Shards shop called `Prepare`. The persistent ad service now prepares one video in the background after the current launch's consent check, initializes the SDK once, and honors the existing retry backoff. Repeated UI events never request multiple videos or open one automatically.
- Closing an ad no longer adds a fixed one-second wait before requesting its replacement. A brief app switch retains a valid cached ad; both wall-clock age and the Unity clock invalidate it after 55 minutes. Presentation still requires an explicit tap and a verified, loaded Google cloud profile.
- The menu background video previously used synchronous `Resources.Load<VideoClip>` during menu creation. It now uses `Resources.LoadAsync<VideoClip>`, leaving the existing poster visible while loading. Reopening during a pending request shares that request. Late completion after disable/destroy releases the asset without allocating a render target. Decoder preparation remains asynchronous and the same seeded render target is used once playback begins.
- Android account/profile, wallet and social initialization now waits for a verified match between the current Google player and the Unity Authentication provider. An SDK `SignedIn` event alone is insufficient. Failed native Google authorization cannot restore an anonymous cached session in the background. Legacy token recovery is only allowed after Google authorization and links the same identity in place before connecting its profile.
- Android's displayed player name uses the verified Google alias. If the alias has not yet been persisted on the first cloud frame, it uses the verified SDK name; it never displays the generated server name. The server's canonical name is an internal field on the same profile, not an additional account. Existing cloud identity keys, balances, purchases and progress are retained. Username compatibility calls cannot select another Android login.

## Other reviewed paths

- Menu navigation has a 0.16-second opacity reveal, not a two-second input lock.
- The match start intentionally waits 0.55 seconds for its transition; this is separate from main-menu/ad readiness.
- Hero preview selection already reuses the current model and a bounded three-variant LRU cache. The portrait target is 1536 × 1728 with 4× MSAA, and new models are built synchronously on their first selection. Native CPU/GPU profiling is needed to decide whether model construction, render-target allocation or drawing is a significant remaining bottleneck; no quality reduction was made based on an unmeasured guess.
- Imported hero backdrops, avatar artwork, fonts and shop artwork already have caches. The menu card atlas and first-frame poster still have synchronous first-use texture loads; profile these on an Android cold start before choosing a larger async-art migration.
- Play Games authentication, cloud profile synchronization and network ad availability remain asynchronous dependencies. Background preloading improves readiness but cannot guarantee an instant button on a cold start or a slow/unavailable connection.

## Validation

Production C# 9 is compiled against controlled engine/SDK fixtures for Android release/development, desktop/Editor and server boundaries. The suites cover ad prefetch/cache/resume/expiry and account gating; menu video asset delay, lifecycle, fallback, GPU-state restoration and decoder errors; Google-only startup, concurrent sign-in, provider/owner races, no anonymous creation on failure, safe legacy linking, and unchanged currency/XP.

Run:
```sh
python Tools/Shop/RewardedAdsRegression/run.py --dotnet-root "$DOTNET_ROOT"
python Tools/Shop/AccountRegression/run.py --dotnet-root "$DOTNET_ROOT"
python Tools/Profile/GoogleNameRegression/run.py --dotnet-root "$DOTNET_ROOT"
python Tools/UI/MenuBackgroundRegression/run.py --dotnet-root "$DOTNET_ROOT"
```

These are client changes. Rebuild the Android player; no account-server `index.js`, `package.json` or schema change is required. Native Android cold start, first shop opening, ad close/resume and Google login still need a device check.

References:
- https://developers.google.com/admob/unity/rewarded
- https://developers.google.com/admob/unity/privacy
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.LoadAsync.html
