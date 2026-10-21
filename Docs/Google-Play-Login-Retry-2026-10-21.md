# Google Play sign-in regression and retry

## Root cause in the published Android build

The uploaded Play Console artifacts were compared directly on 2026-10-21:

| Build | Version | Android runtime `com.google.android.gms.games.APP_ID` |
|---|---|---|
| 59 | 0.6.9 | `1029475752167` |
| 61 | 0.7.1 | ` 1029475752167` (leading U+0020 space) |

Commit `00dba3d506aac664f85e29450285b9a054798c94` replaced the plugin's first-digit Unicode escape with a space escape. In build 61, `\u00201029475752167` becomes a string beginning with a space. The previous `\u0031029475752167` becomes the digit-only string: `\u0031` encodes the first digit `1`.

This is an invalid Play Games application ID. Google's troubleshooting documentation requires digits only. The actual bundled Google Games SDK also reads this metadata with `Long.parseLong` without trimming in `com.google.android.gms.games.internal.v2.appshortcuts.zzt.zza`; the new value is not parseable by that API.

The original AABs were converted into locally signed analysis APKs using Google's bundletool 1.18.3, and their binary Android manifests were independently decoded. This distinguishes the final runtime string from bundletool's escaped XML representation. The two AABs have byte-identical DEX files and byte-identical bundled `PlayGamesSettings` objects, including the same numeric project ID and public Web Client ID. The published Play signing SHA-1 matches a published Android OAuth credential. The account endpoint also responds with the expected authentication-required result to an anonymous wallet request.

The source now restores the prior first-digit encoding for the App ID, and applies it to the plugin-version metadata too. The manifest check validates the decoded runtime value instead of blessing the erroneous literal escape spelling.

## Retry and diagnostics

The shop could show a Google sign-in failure while RECONNECT only refreshed commerce and reset billing. RECONNECT now invokes `AccountServerBootstrap.ConnectGoogleAsync()` when Google identity is not ready, before refreshing commerce or reconnecting billing. Concurrent taps and nested store reconnect calls share the same recovery. A failed login preserves its account status and does not manufacture an outstanding purchase. Existing identity, purchase-verification and account-switch guards remain authoritative.

`GooglePlayAccountSession.LastError` distinguishes native SDK rejection (enum and numeric value), missing native configuration, a callback timeout and an exception starting authentication. Initial sign-in and manual recovery preserve this diagnostic. Exception messages, authorization codes and credentials are not logged.

## Validation and deployment

- 47 production and 34 isolated billing-test connection/ranking checks pass, plus non-Android compilation and the decoded digits-only manifest App ID check.
- The actual account bootstrap/session compile together and pass 21 Android and 3 desktop account checks.
- Negative controls reject the old shop retry behavior and the leading-space manifest.
- The repaired analysis artifact is converted through bundletool and its binary manifest is checked for the exact digit-only, string-typed App ID.

These are source, SDK-boundary and Android packaging checks. An authenticated Google sign-in on the user's device has not been performed. The analysis APKs use a disposable local signing key and are not release deliverables. Build a new signed Unity Android version with a version code above 61, deliver it through the existing Play track, and verify sign-in and cloud-profile restoration on a device. The already published build 61 is unchanged by this source correction.

References: [Google Play Games authentication troubleshooting](https://developer.android.com/games/pgs/android/troubleshooting), [Java Long.parseLong](https://docs.oracle.com/en/java/javase/17/docs/api/java.base/java/lang/Long.html#parseLong(java.lang.String)).
