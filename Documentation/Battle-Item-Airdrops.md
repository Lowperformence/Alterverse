# Battle item team supplies

The equipped item is captured with the local player team when the match starts. At 120 seconds on the DimensionsFront gameplay clock, one item is consumed and one supply crate starts a four-second parachute descent. There is no ultimate activation hook. Pausing the match pauses both the clock and descent.

Landing candidates are mirrored for teams 0/1 at x +/-8, +/-10 or +/-12 and z +/-7 or +/-9. Bases are at x +/-36. Obstruction checks select a clear location in the allied half of the central third; if every candidate is obstructed, the scheduler retries without consuming inventory.

An airborne crate cannot be collected. After landing it persists without a lifetime limit until a living hero of the owner team comes within 1.35m. Enemy heroes, dead heroes, minions and monsters cannot claim it. Any allied hero, including an allied bot, can claim the single crate. Effects affect the collector. Leaving/resetting/ending the match cleans up the supply. Inventory is consumed when the drop successfully starts, not on pickup; matches ending before two minutes do not consume it.

Regen restores 20% maximum health; Aegis grants a 20% maximum-health shield for five seconds; Velocity grants 10% movement speed for five seconds. Meteor arms a strike on the collecting hero until a valid enemy is available. It retains the existing 40-damage, telegraphed, avoidable hit, with additional falling rock fragments. The canopy, cords, landing marker, beveled crate and colored item symbols are built procedurally. Menu and collection descriptions and the match HUD use the new rules.

Private co-op keeps the previous disabled-item restriction. Device-local wallets are not an authoritative multiplayer inventory; this change does not introduce a co-op inventory/pickup synchronization protocol.

## Validation

C# syntax, source rule checks, mirrored placement bounds and parachute triangle geometry are checked here. Unity is unavailable, so compilation, runtime mesh/material imports, timing, inventory persistence and touch/gameplay behavior require a Unity run.

Unity acceptance: equip each item; start a match; cast an ultimate before two minutes and verify no consumption/drop; pause around 1:59; resume and observe a single descent at 2:00; verify no pickup in the air; leave the crate for several minutes; try collecting with an enemy, then an allied bot/player; verify one effect and one inventory decrement. Test a dead player at 2:00, both teams, blocked landing candidates, Meteor collection without enemies, match end before 2:00, and reset/return-to-menu cleanup.
