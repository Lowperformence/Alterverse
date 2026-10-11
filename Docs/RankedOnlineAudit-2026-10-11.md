# Ranked online audit — 11 October 2026

Base: `a63601c928ff7e6cffa73a93932baa77654f94f0`, `dev/vertical-slice`.
No production account, queue, deployment or rating was changed by this audit.

## Reproduced and corrected

| Failure | Correction | Regression evidence |
| --- | --- | --- |
| Matchmaker deletion fails during cancellation; the old ticket remains eligible for registration. | Revoke the account's reservation before external deletion. A failed deletion is still reported, but cannot authorize a cancelled player. | Backend scenario fails before correction in SQLite and Firestore fixtures. A fresh search can create another owned ticket. |
| Admission is accepted at its exact expiry, even though queue polling considers it expired. | Expiry is exclusive: reject at `exp <= now`. | Boundary registration fails before correction, passes afterwards. |
| Missing ACK or pre-launch disconnect uses Casual's reduced-roster rebuild. Ranked can loop on invalid plans instead of terminating promptly. | Abort Ranked immediately after an ACK timeout or a disconnect after plan publication. Keep the match identity until a backend tombstone is issued; reject invalid Ranked plans through the same abort path. | Controlled ten-client handshake fixtures; old code fails both cases. Casual still evicts only the stalled peer and rebuilds with bots. |
| A delayed successful registration response reactivates a locally voided reporter; repeated aborts can create overlapping void operations. | Guard registration with a receipt generation and share one immutable void task. Backend tombstones continue to reject either ordering of register/void. | Held register HTTP response completed after two void calls: no reactivation, exactly one void request. |
| After an account switch, an old rank refresh can clear a new search's Busy flag/status; a delayed cancellation can wipe its assignment; result polling continues for the new account. | Guard asynchronous completion by generation and account. Invalidate result polling on account change/new search and all pending work when the service is destroyed. | Held HTTP completions across account changes. Old code fails refresh/cancel/result cases; corrected code preserves the new operation. |

The six C# failure scenarios and the two backend scenarios were run against the
pre-fix production sources as negative controls. No rating formula, tier boundary,
placement count, cooldown, disconnect threshold or matchmaker expansion was changed.

## Verified behavior

- **163 account/backend tests pass**, including SQLite and strict Firestore
  transaction fixtures: authenticated queue ownership, five placements, tier
  boundaries, persistent results, concurrent/idempotent settlement, invalid and
  replayed admissions, server-only result routes, leaver deductions/search lock,
  void/late-register ordering and unchanged commerce/social/profile behavior.
- **86 C# checks pass** against nine production classes. Existing full 5v5 roster
  and mode fingerprints remain covered. New cases include delayed queue creation,
  cancellation, valid and invalid port assignments, account switches, ACK timeout,
  pre-start disconnect, delayed registration, Casual fallback and result retry.
- Reporter receipts preserve match, winner and sorted leavers across a simulated
  503 and retry. 59.9 seconds absent does not add a leaver; exactly 60 seconds on
  the final frame does. Repeating local completion sends no second result.
- **4 account deployment-tool tests pass.** Ranked matchmaking configuration stays
  at two teams of five through every expansion; no Ranked bot-fill relaxation.
- Edited C# sources pass syntax parsing. Python modules compile.

The C# runner can use .NET 8 (the unchanged CI path) or Mono/Roslyn. HTTP completion,
engine objects, JSON parsing, NGO messaging, clock and transport are substitutes.
Native Unity scene transitions, Android input, real sockets, packet serialization,
Firestore service/emulator behavior and CPU/frame performance are not certified.
The local executed C# path is Mono; no native Unity or .NET SDK build was run here.

## Remaining live checks and limitations

1. Confirm that the current account deployment actually enables Ranked, uses
   durable storage and has matching server credentials; import/validate the
   Ranked profile and update both client and Dedicated builds to protocol 3.
   Source configuration alone does not prove live deployment readiness.
2. Run a real ten-account 5v5 and a nine-account non-start. Repeat ACK timeout,
   pre-start disconnect, queue cancellation, winner settlement and a backend
   outage in a staging deployment. Native builds/transport were unavailable here.
3. There is still **no mid-match reconnect**. A client disconnect returns to the
   menu. An absence of at least 60 seconds receives the existing leaver penalty.
4. There is still **no external durable result outbox**. Ordinary result retries
   block the Dedicated self-stop, but a hard container kill can lose an unaccepted
   receipt. An unresolved active match blocks search for up to two hours; operational
   recovery remains necessary. No live receipt was fabricated or voided here.
5. If external group deletion fails after cancellation, its old matchmaking group
   can survive until provider expiry. Its admission is revoked, so it cannot start
   a valid Ranked match; fresh searches do not reuse it.
6. The Ranked profile has no ping/region rule. `EdgegapRankedGateway.create` does
   not pass a player IP, so provider inference sees the account backend's request
   address. Verify deployment location/ping for players outside that region and
   decide whether to forward a trusted client IP or add measured latency rules.
   Edgegap documents automatic IP inference and the server-to-server alternative:
   <https://docs.edgegap.com/learn/matchmaking>
   <https://docs.edgegap.com/learn/matchmaking/matchmaker-in-depth#server-to-server-api>

Run commands and release prerequisites remain in `RankedMode.md` and
`Tools/Ranked/Regression/README.md`.
