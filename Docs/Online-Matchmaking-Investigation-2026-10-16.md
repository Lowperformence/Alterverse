# Online matchmaking investigation — 2026-10-16

Follow-up: [authenticated live diagnosis and Solo rollout](Online-Live-Diagnosis-2026-10-16.md). The initial investigation below predates the single-team fallback and live hosting inspection.

The reported symptom was two devices searching simultaneously on 15 October, with no match and no visible error. Source inspection identifies a reproducible client monitoring gap, but does not establish which stage those devices reached. No authenticated live Edgegap state, device logs, deployed game-server image, or installed Android version was available.

## When two players are sufficient

`Server/matchmaking/edgegap-matchmaker-v1.json` defines the Casual profile `dimensions-front-online` with two teams, initially four humans per team. At ten seconds, its expansion permits one to four humans per team. Therefore two independently queued solo players can form opposing teams; the dedicated server then fills six of the eight slots with bots. The production dedicated handshake was exercised with two assigned tickets, two different team IDs, and two plan acknowledgements: it launches without requiring eight humans.

Two players in the **same friend group** remain on the same team. They still need at least one opposing human group under this configuration. Supporting one friend group alone against an entirely bot opposition would require a separate matchmaking rule/design change. The server's ability to build a one-human roster does not mean the current cloud rules will assign a server to one human.

Ranked remains a separate queue, requiring eight humans and four per team. Its release lock is unchanged.

## Confirmed client problems and corrections

1. **Monitoring ended before match start.** `EdgegapOnlineFlow.PollAssignmentAndConnectAsync` returned once the transport connected. It left `searching` set until `MarkMatchStarted`, without a timeout or failure continuation during identity/plan/launch verification. The existing disconnect watchdog only handled an active online match or co-op lobby, leaving this pre-launch interval uncovered. A server rejecting an old protocol or closing before launch could therefore leave an apparently active search. The new regression fails on the original flow at `connected transport is still monitored until launch`. The correction monitors until launch/cancel, reports a pre-launch disconnect, and cancels after 95 seconds without a launch. This allows the server's 60-second human arrival window and 20-second plan acknowledgement window, plus margin.
2. **Actual search phases were hidden.** The flow/UI kept showing generic opponent-search text despite membership updates such as `TEAM_FOUND`, `MATCH_FOUND`, or `HOST_ASSIGNED`. The flow now exposes readable live phases, and the overlay switches to a match connection label after assignment. Phase changes are also logged for device diagnostics.
3. **Malformed assignments could stall or fail indirectly.** An assigned host without a usable address/port previously kept polling; a valid address without a player ticket/team could connect but never send the required identity hello. Both are now rejected with a specific assignment error. Unexpected polling exceptions now clean up and produce a visible error instead of faulting an unobserved task.

Failure cleanup retains the existing membership deletion and invitation-room unlock behavior. Successful launch leaves the game connection intact. These changes do not modify queue profiles, bot balancing, game protocols, cloud configuration, or installed releases.

## Deployment checks still needed for the reported session

- Inspect the **active** Edgegap Casual profile, especially the ten-second expansion. A JSON file in GitHub is not evidence that the running matchmaker imported it.
- Check whether both devices searched independently or were in one friend group, and whether both used the same updated client release.
- If tickets remained `SEARCHING`/`TEAM_FOUND`, inspect readiness and active queue rules. `MATCH_FOUND` followed by a return to searching points to allocation/startup failure and calls for deployment logs. `HOST_ASSIGNED` followed by failed connection calls for address/UDP mapping, server startup, and protocol checks.
- Complete the coordinated 4v4 rollout in `4v4-Migration.md`: updated client, matching Linux dedicated build, Edgegap application versions `v0.1.2-server` / `v0.1.2-ranked-server`, imported profile configuration, and the Ranked account backend changes when deploying that migration. The new dedicated protocol is 4; an old server rejects it. Current cloud settings and deployed image versions have not been inferred from GitHub alone.
- Repeat an actual two-device Casual search, save each phase and the corresponding Edgegap ticket/deployment logs, and verify entry into the same game. Test friend invitations separately.

The Edgegap documentation confirms that groups stay together and that partial matches cannot fall below the configured minimum per team: https://docs.edgegap.com/learn/matchmaking/matchmaker-in-depth#group-up and https://docs.edgegap.com/learn/matchmaking/matchmaker-in-depth#partial-matches.

## Verification

Relevant production and test files were matched by Git blob hash against `dev/vertical-slice` at `bab893aa435d3a58d44e68869f946326c36fd504` before edits. The local source checkout includes the merged 4v4 and invitation changes; unrelated later hero-art/combat changes are not modified by this patch.

- Original C# regression suite: 1,839 checks passed.
- Added coverage exercises the complete assignment-to-launch continuation: timeout, disconnect, successful launch, cancellation, phase visibility, malformed endpoint/ticket/team, unexpected polling exceptions, and a two-human/six-bot dedicated launch.
- Updated suite: 1,877 C# checks passed using controlled Unity, transport, service and HTTP substitutes. The five changed C# files also passed syntax parsing.
- 243 account/backend tests passed with the existing test dependencies.

Commands:

```sh
python Tools/Ranked/Regression/run.py --dotnet-root /path/to/dotnet
python -m unittest discover -s Server/commerce -q
git diff --check
```

These checks are not a Unity/Android build or a live two-device/cloud test. The reproduced gap can explain a silent wait after connection; it does not prove that this was the cause on 15 October.
