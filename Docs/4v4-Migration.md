# Dimensions Front: 4v4 migration

All newly created matches use two teams of four heroes, eight total slots. Offline has one local human, three allied bots and four enemy bots. Private co-op admits one to four humans on the same team and fills remaining slots with bots. Online friend parties admit one to four players; Casual supports one to eight humans with bot fill. Ranked requires eight authenticated humans, four per team, and all eight start acknowledgements.

`MatchSession.TeamSize` is the client/game-server authority; `MaxPlayers` is derived from it. Co-op and party capacities, team offsets, full rosters, rematches, spawn rows, supplies and Hot Zone capacity follow that format. Menus, lobby cards, queue text and match descriptions show 4v4. The existing `FiveVsFiveTestRoster` component/file name is retained to preserve Unity serialization references; its production spawn loops now create 4v4.

Private co-op and online Unity session types advance to `df-private-coop-v3` and `df-online-party-v3`. Join-code paths reject incompatible room capacities. Co-op and Relay-online plan protocols advance to 2; the dedicated protocol advances to 4. Old clients must update before joining new matches. Friend invite codes and the existing invitation controls remain available.

The account backend accepts only balanced eight-human Ranked registrations. Settlement computes team ratings from the registered roster, allowing a match already registered by the old five-player server to finish without changing the season, rank history, placements or leaver rules.

## Deployment

These source changes do not update an installed Android build or an existing cloud deployment. Roll out the matching components together:

1. Pause new matchmaking and let active matches finish. Do not clear accounts, ranks, Firestore or purchase data.
2. Build the updated Linux dedicated server using **Tools → Dimensions Front → Build Linux Dedicated Server**. Build its container and create Edgegap application versions `v0.1.2-server` and `v0.1.2-ranked-server` from that image, preserving the existing UDP port mapping. Preserve the current Casual settings and the separate Ranked environment/secret settings documented in `RankedMode.md`.
3. Deploy the updated account backend to the existing account service, preserving its project, storage, namespace, secrets and billing configuration. New Ranked registrations then require eight humans. Keep Ranked's existing release lock unchanged until its separate release is intended.
4. Import and validate `Server/matchmaking/edgegap-matchmaker-v1.json` in the existing Edgegap matchmaker. Both profiles initially require 2 × 4 humans. Casual still expands after ten seconds to 1–4 humans per team and lets the new game server fill bots; Ranked never expands below four per team. The configuration references the new application versions above.
5. Build and distribute the updated signed Android/client release with the existing Unity project, authentication and endpoint configuration. Re-create invitation rooms with the updated client. Resume matchmaking after the checks below.

## Verification

Local result: **243 backend tests, 4 deployment-tool tests and 1,724 C# checks passed.** All 29 changed C# files parsed without syntax errors.

Local automated checks:

```sh
python -m unittest discover -s Server/commerce -q
python -m unittest discover -s Tools/Ranked -p 'test_*.py' -q
python Tools/Ranked/Regression/run.py --dotnet-root /path/to/dotnet
```

The backend suite covers both SQLite and strict Firestore transaction fixtures: 4v4 admission and Elo, fifth-teammate and legacy ten-human rejection, existing-match settlement, account isolation, purchases, restoration and social features. The C# runner compiles production offline/co-op/online roster code and dedicated Ranked handshakes with engine, service and transport substitutes. It exercises exact team sizes, all supported party/human counts, bot fill, stable fingerprints, rematches, old protocol rejection, handshake overflow, successful eight-player launch and incomplete-start cancellation.

Five pre-existing backend failures were reproduced on the untouched baseline: avatar tests still assumed the previous 450/500-Shard prices although the current catalog charges 400. Their expectations now use the current prices; the zero-paid-balance restoration fixture spends the remaining 100 on a shield. No production catalog or purchase code was changed.

Unity compilation, signed Android build, live Edgegap import/deployment and real multi-device matches require the actual build/hosting environment and were not performed by these headless fixtures. Before releasing, verify:

- Offline: local player plus three allies versus four enemies on each arena.
- Co-op: invite codes, one/two/four humans, readiness, fifth invitee rejection, disconnect, rematch and return to lobby.
- Casual: solo and a four-player friend party, eight-human matches, sparse matches with bot fill, no fifth teammate or ninth player.
- Ranked: eight humans start after all eight acknowledgements; seven humans or one missing acknowledgement must cancel without points; settlement, relogin, leaderboard and leaver rules still work.
- Both teams: four correctly placed heroes, scoreboard/HUD, four separate supply lanes, objectives, respawn and round reset.

Historical audit documents describe earlier 5v5 revisions and are retained as history.
