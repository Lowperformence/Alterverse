# Ranked production-code regression

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
