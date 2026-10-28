# Android R8: optional Google Ads Next-Gen references

The native AAB attempt on 2026-10-26 failed in R8 with 47 missing definitions
under com.google.android.libraries.ads.mobile.sdk. Every reported reference came
from the optional com.google.unity.ads.nextgen bridge. There was no reported
missing Standard Google Ads class in this error set.

The project pins Google Mobile Ads Unity 11.5.0 and explicitly selects Standard
(overrideDefaultGmaAndroidSdk: 1, selectedGmaAndroidSdk: 0). Upstream keeps both
bridge namespaces through its consumer rules. Its Android client factory probes
for the Next-Gen MobileAds class and otherwise uses the Standard client.

The existing late Gradle callback now configures a marked block in the generated
unityLibrary/proguard-user.txt only when build.gradle directly declares the
Standard SDK and does not declare Next-Gen. That block contains only:

    -dontwarn com.google.android.libraries.ads.mobile.sdk.**

This is an optional-dependency rule for the inactive architecture. It does not
suppress missing com.google.android.gms.ads or unrelated definitions, disable
R8, change SDK dependencies, replace consumer rules, edit the package cache, or
modify the versioned project rules. The block is removed when switching to
Next-Gen, declaring both SDKs, or removing Ads. Missing generated rules and
malformed managed blocks fail explicitly.

## Verification

36 checks compile and exercise the production Android release/R8 helpers with
Editor substitutes: settings, generated Gradle processing, dependency selection,
scope, SDK switching, idempotence and failure handling.

The optional real-R8 runner uses pinned R8 9.5.28 and representative Java fixtures.
It reproduces all 47 public type names from the native error: old rules fail,
the generated Standard-only rules pass, and a missing active Standard MobileAds
definition still fails. A Java smoke test invokes the retained Standard bridge
through reflection and receives its simulated reward callback. Existing Games,
Tasks and Billing JNI fixtures are retained while unrelated unused code shrinks.
These fixtures do not execute the actual Ads SDK or request a real ad.

Run:

    python Tools/AndroidR8/run.py --dotnet-root <dotnet-root> --r8-jar <r8.jar>

The Android R8 workflow runs this suite with a checksum-verified R8 jar. The
uploaded Editor.log is not stored in the repository; the fixture lists only the
47 public SDK type names.

## Native acceptance

Pull the fix, reopen Unity, and run DIMENSIONS FRONT > Release > Rebuild signed
Android bundle (clean). Confirm the generated-rules log and successful AAB output.
Then verify consent, a test rewarded-ad load/show and exactly one reward on the
Android device. This change is confined to Editor build processing; the already
uploaded Linux server image does not need to be rebuilt for it.

## Primary sources inspected

- [Google 11.5.0 Android factory](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Platforms/Android/GoogleMobileAdsClientFactory.cs)
- [Google 11.5.0 consumer rules](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/proguard.pgcfg)
- [Google 11.5.0 build preprocessor](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Editor/AndroidBuildPreProcessor.cs)
- [Google architecture selection](https://developers.google.com/admob/unity/android-next)
