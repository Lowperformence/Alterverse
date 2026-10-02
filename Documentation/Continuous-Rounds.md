# Continuous rounds

Offline, private co-op and online rounds remain in the result screen and continue automatically after a 12-second countdown. The local result receipt must be fully committed first. Multiplayer additionally waits for every connected human's receipt confirmation and next-plan acknowledgement; only the server launches the new round. The session, teams, selected heroes, map and difficulty are retained. Each round gets a fresh match/receipt ID.

Players leaving during the multiplayer result screen are removed from the next plan and replaced with bots in their original slots. A changed roster cancels the old countdown and triggers a new plan handshake. Delayed round-state/result messages cannot end the new match. BACK TO MENU exits the online party/co-op room and shuts down the local network; the same exit path is used in match settings.

Relay-host departure still closes the room, as the existing networking stack has no host migration. Loss of connection during live co-op retains the existing abort behavior.

## Validation

Run `DIMENSIONS FRONT > Checks > Round Loop Roster` in Unity. This validates three consecutive private/online plans, fresh IDs, unchanged team/hero assignments, departing-player replacement, original-plan immutability and invalid empty/duplicate-ID rejection.

Live checks (require Unity and multiple clients):

- Offline: finish three consecutive rounds; confirm the 12-second countdown restarts each time and BACK TO MENU cancels it.
- Co-op/online: finish a round with two players; delay one client's reward save. Confirm no launch until both receipts and plans are acknowledged.
- Co-op/online: leave as a guest during the countdown. Confirm the host keeps the session and the remaining roster gets a fresh countdown with a replacement bot.
- Deliver an old match-state/result packet after rebuilding. Confirm it is ignored.
- Leave as the Relay host. Confirm guests receive the existing disconnect flow, not an orphaned next round.
- Exit from in-match settings and from results. Confirm the player is removed from the party/room and can subsequently start an offline match.

Live Unity/network checks were not executed in the remote editing environment.
