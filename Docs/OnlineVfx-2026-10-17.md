# Online hero attack VFX and combat verification

## Observed evidence

The user reports attack VFX missing only online and suspects attacks may also do no
damage. The live v0.1.4 deployment `ab39476506cf` confirms an authoritative solo/bot
match and reports an unsupported HeroEnergy shader on its Null graphics device.
The captured log does not establish whether that player's commands or hits applied
damage. Do not describe the suspected live damage issue as proven or fixed.

Source inspection confirms that ordinary Basic/Spell/Impact/Area hero VFX were local
rendering calls. Client commands execute on the dedicated server; the server relayed
attack animations, sound and selected special effects, but not those ordinary cues.
The previous full facade fails the new production transport regression because a
normal spell publishes no VFX event.

## Changes

- Common hero VFX publish bounded semantic events before touching any material or
  graphics code. A reliable named message carries actor/target identity, original
  origin, effect parameters and server time. It is below 256 bytes even with the
  maximum fixed-string key. No gameplay state is inferred from a visual packet.
- The client checks server authority, round, enum, slots and finite/ranged fields,
  then invokes the real visual facade. Replay cannot publish another event. Nested
  common calls publish once; older specialized transports are suppressed only while
  inside a generic cue, so their independent paths remain available.
- Projectiles and warnings retain their original clock and expire instead of being
  restarted by late packets. Short impact flashes align with receipt. Origin and
  follow targets are preserved. Warning/projectile cancellation is replicated.
- Shields tolerate the VFX event arriving shortly before the health snapshot, then
  disappear immediately on a subsequently observed break. Offline behavior keeps
  its existing immediate shield binding.
- The pool does not initialize graphics on a Null device. The existing unsupported
  shader fallback already cleared its material safely; it was not proven to block
  live damage. Rendering failure remains independent of semantic publication.
- Server combat diagnostics report received, authorized and accepted commands,
  actual health-plus-shield removal, and human hit/removal counts separately from
  bot totals, at most once per ten seconds when combat occurs. Damage amounts,
  targeting, cooldowns and health mutation rules are unchanged.

## Verification

```sh
python Tools/Multiplayer/VfxRegression/run.py --dotnet-root /path/to/dotnet
python Tools/Multiplayer/Regression/run.py --dotnet-root /path/to/dotnet
python Tools/Ranked/Regression/run.py --dotnet-root /path/to/dotnet
python Tools/Combat/NerokRegression/run.py --dotnet-root /path/to/dotnet
python Tools/Aiming/Regression/run_entry.py --dotnet-root /path/to/dotnet
```

The new suite passes **1,128** assertions. It compiles the actual complete common
facade, Timmy/Void Knight partials, pool scheduler, semantic event scopes, new VFX
transport and combat command/delivery sources. All ten heroes' Basic and Q commands
are serialized, received by the real authoritative handlers and simulated at
30/60/120 FPS under Null and unsupported-shader conditions. Server health/shield
falls, human damage counters advance, client meshes appear, and visual replay does
not apply damage again. The fixture transport uses controlled byte buffers and the
graphics/physics/health objects are substitutes, not a Unity player or real UDP/GPU.

The ordinary facade baseline fails the publication test. The previous pool fails
the delayed-warning expiry assertion. The existing snapshot suite also asserts
that lower server HP/shield values reach the actual client snapshot reader.

Other local results: **166** movement/world/healing/scoreboard checks, **1,932**
lobby/identity/ranked checks, **284** Nerok checks and **1,245** entry/existing-combat
assertions pass. Native Unity compilation, Android rendering, CharacterController
collision, exact live command acceptance and device latency still need verification.

## Rollout

This adds `DF_COOP_HERO_VFX_V1`; the existing gameplay packet layouts are unchanged.
Both an updated Linux server and Android app are required to see the generic online
effects. An old client does not implement the new handler. Build both from the same
completed commit, upload the server using a fresh `v0.1.5-server` tag, create its
Edgegap version and update the matchmaker's application version before testing.
The currently running v0.1.4 deployment is not updated by this source commit.

Before releasing, verify Basic/Q/W/E/ultimate visuals, a visible hit and actual
health/shield loss, missed shots and cover, death/respawn cancellation, a second
search, and an offline match. Inspect `DIMENSIONS FRONT COMBAT` for accepted human
commands and human damage; do not infer human hits solely from bot damage totals.
