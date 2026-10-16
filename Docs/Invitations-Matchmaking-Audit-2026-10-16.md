# Invitations and matchmaking audit — 2026-10-16

## Player-visible behavior

PLAY now has an explicit **INVITE FRIENDS** action. Both it and **Play Online** open the online party choices without creating a room, readying a player or starting matchmaking. A player can create a private friend party, copy its invitation code, or enter a friend's code and join. Only **FIND MATCH** starts the search. Private co-op remains available separately through **PRIVATE CO-OP**.

Online parties still contain at most four friends, with 4v4 matches and the existing Casual bot fill. The leader starts matchmaking after every friend is ready. The server-side Unity room is locked during matchmaking; hero/readiness edits are frozen, and a stable roster snapshot protects against in-flight changes. Cancel unlocks the existing room for further invitations. Guests can leave without deleting the leader's entire Edgegap group.

## Corrected errors

- The PLAY entry previously bypassed the invitation choices by immediately starting quick search. Reopening a ready party could also start searching without a separate confirmation.
- Search cancellation previously refused to run while group creation was busy. Cancellation now invalidates polling immediately, waits for a pending create/join response, deletes the resulting membership, and blocks a replacement search until cleanup finishes.
- Poll failures/expiration and preparation failures now clean up the Edgegap group/member and unlock the invitation room. DELETE 404/410 is treated as already cleaned up; failed deletions remain retryable instead of silently discarding membership.
- Late create/join/ready/poll responses cannot overwrite a newer membership or trigger a cancelled connection. A late abandoned create/join response attempts cleanup of that exact old group/member, without deleting a newer group. An obsolete connection waiter also cannot shut down a newer search's transport.
- Group size alone was not a sufficient snapshot check. Player IDs, hero selections, battle items and readiness now participate in the check. Disconnected Relay client IDs cannot supply a guest acknowledgement.
- Relay room removal during an already assigned dedicated-server handoff no longer aborts the valid connection attempt.
- Co-op settings and player choices are frozen during staging. Invalid staged rosters clear the prepared plan instead of retaining an older plan.
- The legacy Relay-online plan handshake aborts after 20 seconds without confirmation or when a player leaves. It cannot run concurrently with the Edgegap search.
- Ranked cannot start while an invitation room is being created. An old Ranked queue response cannot clear a newer search's busy state after an account change. Existing Ranked release availability and 4v4 rules are retained.
- Remaining obsolete “Five versus five” / “TWO TEAMS OF FIVE” UI descriptions now say four.

## Automated verification

Local checks passed:

- **243 backend tests** covering account, authentication, commerce, restoration, social, clan chat and SQLite/Firestore Ranked behavior.
- **4 deployment-tool tests**.
- **1,839 C# checks** on .NET 8. The runner now compiles the actual `CoopLobbyService`, `OnlinePartyService`, `EdgegapOnlineFlow`, `OnlinePlanHandshake`, `EdgegapMatchmakerClient`, Ranked, roster and handshake classes against controlled Unity/Services/HTTP/NGO substitutes. The actual online menu-entry method is extracted verbatim and exercised too.
- All **13 modified/new C# files** passed syntax parsing; whitespace checks passed.

The new cases cover invitation navigation without automatic network requests, code normalization, capacity/version rejection, failed leave/retry, one-to-four-player co-op staging, phase edit locks, two-to-four-friend group acknowledgement ordering, pending creation cancellation, guest departure during a pending join, expired groups, late membership/assignment responses, changed roster snapshots, dedicated handoff, Relay plan timeout/disconnect and cross-mode/account-switch races.

Run the same checks with:

```sh
python -m unittest discover -s Server/commerce -q
python -m unittest discover -s Tools/Ranked -p 'test_*.py' -q
python Tools/Ranked/Regression/run.py --dotnet-root /path/to/dotnet
```

## Build and live acceptance

These results verify production source behavior with substitutes. They do not constitute a Unity/Android build or authenticated live Unity/Edgegap testing. Install the updated client build and check two real devices, then four: create/copy/join an invite code, choose distinct heroes, ready up, cancel while preparing/searching, invite again, leave as guest, reconnect through a fresh invitation, and enter a Casual match. Repeat private co-op staging/cancel and return to lobby; verify a disconnected player cannot leave a stale start plan.

These repairs do not change the dedicated wire protocol, cloud profile names, or the previous 4v4 capacity settings. If the earlier 4v4 release has not yet been deployed, its coordinated client/game-server/account-backend/Edgegap rollout is still required; see `4v4-Migration.md`. This audit did not deploy cloud services or alter production accounts.
