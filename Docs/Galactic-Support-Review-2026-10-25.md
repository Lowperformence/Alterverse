# Galactic Support rating invitation

Completed matches 2, 4, 6, ... queue Galactic Support in the existing match-reward
transaction. The menu consumes each invitation once, after the account profile is
ready. Duplicate receipts and abandoned matches do not advance it. NOT NOW, Escape,
reopening the menu and restarting do not permanently suppress future invitations.
The old first-match/offered flag is not evidence of a submitted review.

The existing message and Galactic Support styling remain, with five geometric gold
stars, RATE 5 STARS, NOT NOW and ALREADY RATED. Confirmation is saved in
AccountProfilePrefs and therefore participates in account caching/cloud sync.

RATE 5 STARS requests Google's native In-App Review flow through the official
com.google.play.review 1.8.4 package. Its dependencies and Android review 2.0.2 are
pinned, including the existing Gradle template and JNI keep rules for R8 releases.
Repeated taps are blocked while the request is pending; a failed request returns
control after 15 seconds. A launched dialog has no timeout while a player writes.

Google does not report whether a dialog was displayed, dismissed or submitted.
Consequently, a successful API callback does not mark the player as rated. After
returning, the player can confirm ALREADY RATED or explicitly open Google Play.
There is no automatic shop redirect after a native callback. Devices without the
Play Store use a browser for the explicit store link.

An existing rating cannot be detected automatically, including one left before
this update. Such players must confirm ALREADY RATED once. Native presentation is
also controlled by Google quota/account/device eligibility; opening it on every
tap cannot be guaranteed. No unsupported review URL parameter is used.

## Validation

Run:

```sh
python Tools/UI/ReviewPromptRegression/run.py --dotnet-root /path/to/dotnet
```

The harness compiles production reward receipts, review progress, account
persistence and review UI with controlled engine/SDK boundaries for Android,
Editor and server. It covers the interval, legacy flags, duplicate receipts,
menu recreation, delayed presentation, account cache/cloud restoration, native
success/failure/exception/timeout, repeated taps, explicit store fallback and
account changes while a request is pending. It checks package/Gradle/R8 wiring.

Unity, UPM resolution, Gradle/R8 and a real Play dialog were not executable in this
workspace. Before release, install from the Google Play internal test track with
an eligible account and no existing rating. Verify matches 1 through 6, dismissal,
native submission/cancellation, ALREADY RATED persistence and an R8 release build.
Internal app sharing cannot submit real ratings.

## References

- https://developer.android.com/guide/playcore/in-app-review/unity
- https://developer.android.com/guide/playcore/in-app-review
- https://developer.android.com/guide/playcore/in-app-review/test
- https://github.com/google/play-in-app-reviews-unity/releases/tag/v1.8.4
