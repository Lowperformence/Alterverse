# Online arena pool — 2026-10-18

## Intended rule

Edgegap public matches use a **union** of the eligible arena ownership of all joined human participants, not the dedicated server's own selected arena. Every zero-price arena is eligible even if no participant made a purchase. If **any one** participant owns a paid arena, that arena enters the common pool. Each eligible distinct arena gets one equal draw entry. The dedicated server draws **once per newly created match**, sends the chosen `ArenaVariant` inside the existing authoritative online plan, and clients verify the plan fingerprint before joining.

The pool derives from all accepted, still-connected human Edgegap tickets when the roster is frozen; bots do not add owned maps. This logic has no hard-coded human count. The **current production match format is 4v4**, with up to eight human slots, despite a previously discussed ten-player performance reserve.

## Implementation

- `OnlineArenaPool` in `Assets/Alterverse/Scripts/Multiplayer/OnlineMatchPlan.cs` enumerates the existing shop catalog. `LocalOwnedMask` reports owned and free arenas; `Normalize` adds free arenas and strips unknown enum bits; `Draw` selects from the deduplicated merged pool.
- `DedicatedOnlineMatchHandshake` appends an optional uint ownership mask after the existing name-capability field in each client's hello. The server collects masks from authenticated Edgegap-ticket participants, merges them with OR, draws an arena, and puts that arena in `OnlineMatchPlan.BuildDedicated` instead of `ArenaVariantRuntime.Selected`.
- The trailing hello field is backwards-compatible in its transport shape: an older client contributes only free arenas; an older server ignores the field and can still choose Moon. **Both Android and Linux server must be built from the updated source** to enable pooling in a live match.
- Private host/Relay lobbies still use their existing host-selected arena; this change targets Edgegap public matchmaking only.
- Regression checks in `Tools/Ranked/Regression/Regression.cs` exercise free fallback, a premium arena contributed by one player, unknown mask bits, local unlock inclusion, and candidate selection.

## Outstanding security and validation

**A client's reported paid-map bitmask is not purchase authorization.** Edgegap authenticates its match ticket but does not currently verify that the client's reported arena entitlements were bought on the account backend. Before treating pool contributions as tamper-proof, the dedicated server must validate premium ownership against server-side account entitlements or a short-lived signed entitlement receipt. Never use these bits for item grants, purchase fulfillment, or account balances.

Run `python Tools/Ranked/Regression/run.py --dotnet-root <dotnet root>` and native Unity Android/Linux builds before deployment. Verify on two real client accounts (one owning a premium map, one not) that the premium map enters the shared draw and both clients load the same arena. Code updates alone are not evidence that the current Edgegap deployment has been updated.
