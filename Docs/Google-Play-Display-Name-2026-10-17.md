# Google Play Games display name

After Google Play Games is successfully linked to the current Unity Authentication
account, the client adopts `PlayGamesPlatform.Instance.GetUserDisplayName()` as its
display name. Verification rechecks both native Google player ID and Unity player ID
after the asynchronous provider lookup. Merely opening or cancelling Google sign-in
does not rename the profile.

The name is stored as account-scoped `DF_PlayGamesDisplayName`. `DF_PlayerName`
remains the immutable server-assigned canonical name; account hashes, public social
IDs, purchase ownership, XP and currency retain their existing identifiers. Adoption
waits for the matching cloud profile, handles either order of link/profile callbacks,
and schedules a profile save only when the alias actually changes. A failed or empty
SDK result keeps the previous valid account name. Normal cloud save/retry logic owns
persistence. Changing the Google nickname refreshes it at the next verified sync.

The alias is a cosmetic client-reported display value. The account backend authenticates
the profile owner and validates the alias format; it does not independently attest that
the text came from Google. Display names must never authorize purchases or gameplay.
There is no new free-text naming UI, and clan naming rules remain unchanged.

Names are trimmed, NFC-normalized and bounded to 32 UTF-16 units and 60 UTF-8 bytes,
within NGO's FixedString64Bytes capacity. Truncation respects text elements. Control,
format, unpaired surrogate, private-use/unassigned characters, line separators and
angle-bracket markup are rejected. Display validation does not allocate text-element
enumerators on every HUD/profile read. Profile headings fit the available label width.

The shared display sanitizer preserves aliases in profile, social views, lobby and
scoreboard. Social projection reads the alias from the saved profile. Ranked search
also reads the saved profile, ignoring the request's claimed display name; historical
ranked rows refresh their displayed name when that account next searches Ranked.

## Compatibility and rollout

- Build a new Android AAB from this commit for the native name getter and UI adoption.
- Deploy the account backend from the same commit for alias validation and social/
  Ranked projection. Both SQLite and Firestore use the shared validation/projection.
- Build and deploy the Unity dedicated server from the same commit for online match
  labels. Use `Builds/DedicatedServer/Linux/Dockerfile`, not the repository-root Dockerfile.
  This change does not alter server CPU/RAM allocations or deployment configuration.

The dedicated hello appends an optional `DFN1` capability marker, retaining protocol 6
and existing plan/ACK formats. Existing servers ignore the trailing marker and still
connect new clients, using their existing generated match labels. New servers accept
hellos with or without the marker. If any human lacks alias support, the entire roster
uses the canonical-format sanitizer before fingerprinting. Bot labels retain their
existing generated format. This prevents mixed versions from hashing different names.

Co-op and online lobby player properties similarly advertise `displayNameFormat=gpg-v1`.
If any lobby member lacks support, current clients use the same generated name format
as older clients. With all members updated, Google aliases are retained. Profile
display remains the account's Google alias independently of this compatibility fallback.

Code publication alone does not build an AAB, deploy the account service, replace the
active dedicated image, or verify a real Google Play login.

## Validation

- `Tools/Profile/GoogleNameRegression/run.py --dotnet-root <dotnet>`: production
  bootstrap/session/profile code compiled in Android and desktop configurations;
  21 Android and 3 desktop checks with controlled native SDK/service boundaries.
  Covers wrong provider, account changes during verification, delayed cloud load,
  direct adoption, SDK errors, empty reply, sign-out, progress preservation and
  absence of repeated local/cloud writes. Original bootstrap fails the delayed
  cloud-load name-adoption case as a negative control.
- `Tools/Ranked/Regression/run.py --dotnet-root <dotnet>`: 10,029 checks, including
  alias persistence, Unicode limits, account isolation, social sanitization, new/mixed
  lobby clients, old/new dedicated hello shapes and real production plan writer/reader
  fingerprint round trips. Engine, session and NGO byte transport are substituted.
- `python -m unittest discover -s Server/commerce -q`: 248 tests passed, including
  alias save/restore, identity/currency/XP preservation, social output and Ranked
  naming on SQLite and transactional Firestore fixtures.

No Unity editor/IL2CPP build, native Android sign-in, font rendering, live Firestore,
or real network interoperability test was performed in this environment. Device
acceptance: connect Google in Profile, verify the same nickname in the menu and lobby,
start a match with updated peers/server, inspect scoreboard, restart and reconnect;
also try cancelled login and a second account and confirm unchanged progress/ownership.

SDK reference: https://developer.android.com/games/services/unity/v2/api/class/google-play-games/play-games-platform
