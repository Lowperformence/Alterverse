# Ranked production-code regression

`LifecycleRegression.cs` also executes the production online result exit, auto-return,
round-loop guards and world-entity registry. Hotzone open/resolve/progress methods and
their message handlers are extracted verbatim from production; physics scoring, terrain
meshes and monster rendering are controlled boundaries. It verifies dedicated-server
authority, client objective/monster presentation, stale-event rejection, reward-commit
gating, retained host/guest parties and repeated 2–4 friend searches without the former
Relay connection. Guest join receipts and cancellation use the surviving Unity session.
The full online-party and Edgegap-flow classes are compiled unmodified.

The world registry case verifies one scan across 19 chunks in the same frame, discovering
a newly spawned entity in that frame and rejecting stale round packets before scanning.
These checks run in the existing ranked CI job; they do not prove native frame time,
Unity package compatibility, real service propagation, or Android/Edgegap end-to-end play.

The invitation/lobby audit adds production co-op/online service and Edgegap-flow execution, plus the actual online menu-entry method extracted verbatim. `LobbyServiceStubs.cs` supplies controlled Unity Services sessions, and `LobbyRegression.cs` tests invitation codes, room capacity, readiness/staging, guest departure, cancellation, late HTTP responses, roster changes and Relay/dedicated transitions. Local result: **1,839 checks passed on .NET 8**. See `Docs/Invitations-Matchmaking-Audit-2026-10-16.md` for scope and live-test limits.

The runner compiles production offline, co-op, Ranked, roster, handshake and UI classes against
controlled engine, HTTP and NGO substitutes. No live service is accessed.

CI / .NET 8:

```sh
python Tools/Ranked/Regression/run.py --dotnet-root /path/to/dotnet
```

Mono with C# 9 Roslyn:

```sh
python Tools/Ranked/Regression/run.py \
  --mono-root /path/to/extracted-mono \
  --roslyn /path/to/roslyn/tasks/net472/csc.exe
```

Mono needs `usr/bin/mono-sgen`, its runtime configuration and library layout,
`System.dll`, `System.Core.dll`, `System.Xml.dll`,
`System.Runtime.Serialization.dll` and `System.ServiceModel.Internals.dll`.
Dependencies are supplied separately; the runner does not install/download them.
Its JSON reader defaults missing fields as Unity does, through Mono's XML bridge;
.NET uses System.Text.Json. Neither substitutes a native Unity parser/build test.

The 4v4 cases execute the production offline spawn loops for every fixture hero, co-op parties of one to four humans, online parties of one to four across one to eight humans, dedicated team allocations, deterministic client rosters, rematches and overflow rejection. The dedicated tests exercise successful eight-player Ranked registration and launch as well as missing ACKs and disconnects. These use .NET 8 and engine/service substitutes. `FailureRegression.cs`
holds/reorders HTTP completions to exercise cancellation, account changes, aborted
registration, missing ACKs and pre-start disconnects. It also verifies dedicated
credentials, immutable result retry, the 60-second leaver boundary and retained
Casual fallback. Restoring the old three production classes reproduces six failure
scenarios. NGO byte transport and native scene transitions are not exercised.
Recovery checks also cover linked Google/existing password accounts, rejecting
unlinked guests before queue creation, verification failures, delayed verification
after cancellation/account changes, and a new client with an empty rank cache
loading the saved status/leaderboard with a new valid session. Native Google login
and actual Android uninstall/reinstall are not substituted by these fixtures.

Backend / deployment tools:

```sh
python -m unittest discover -s Server/commerce -q
python -m unittest discover -s Tools/Ranked -p 'test_*.py' -q
```

These pass **168** and **4** tests respectively. SQLite is real; Firestore uses
strict transactional fixtures. Install `Server/commerce/requirements.txt` and
use a complete checkout including the Commerce JSON catalogs.
See `Docs/RankedOnlineAudit-2026-10-11.md` for findings and outstanding live checks.
