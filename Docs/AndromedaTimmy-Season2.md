# Andromeda Timmy — Season 2 character

Small orange tabby cat in a pearl astronaut suit, with visible ears/tail, cyan suit claws, jet backpack and a round light-gray glass visor over his face. Assassin: short melee reach, Zhaev's running speed, lower health, one-target interruptible ultimate. Existing eight hero IDs and their combat numbers are preserved; Timmy is appended as ID 8.

## Availability and trying the hero

Open the project in Unity and let assets import. Select Andromeda Timmy directly in the existing Heroes menu and start a match. He is included in the regular nine-hero roster in both the editor and player builds; no preview switch is required. Run **DIMENSIONS FRONT → Checks → Andromeda Timmy integration (non-destructive)** to validate resources/catalogs in Unity.

Timmy is currently selectable through the existing default free-hero ownership path. A future purchase restriction is intentionally left to the owner; no shard price, purchase SKU or currency debit is introduced. Timmy has no purchasable cosmetics yet. Rebuild clients and the authoritative server from the same version. `HeroIdentity.HeroCount` remains a compile-time constant, as required by the existing audio bank.

## Kit and balancing starting values

All Timmy numbers live in `Assets/Alterverse/Scripts/Characters/TimmyBalance.cs`. The existing global health/movement multipliers, ordinary universal hero shield, ability cooldown modifiers and 45-second initial ultimate lock still apply.

| Attack | Aiming and effect | Damage | Range | Cooldown |
| --- | --- | ---: | ---: | ---: |
| Claw (basic) | Existing automatic melee soft target; one physical melee hit | 22 | 1.6 m | 0.78 s interval |
| Claw Swipe (Q) | Manual melee cone; one enemy | 28 | 2.2 m | 6 s |
| Stardust (W) | Placed circle; 0.3 s warning; 20% slow for 1.5 s | 14 per enemy | 4.5 m cast / 1.6 m radius | 12 s |
| Jet Pounce (E) | Directional dash using the common collision sweep | 0 | 3.4 m | 13 s |
| Scratch (ultimate) | Aim cone selects one enemy hero; fixed-end leap; 2 s grip with four damage ticks | 54 total | 9 m acquisition / 1.35 m contact | 52 s |

Light Paws passive: a successful damaging skill hit grants 12% speed for 1.2 seconds, at most once per 4 seconds. Basic hits and missed/rejected skill hits do not trigger it. AOE damage to several enemies triggers it once.

Raw base health is 185; the existing global multiplier produces **223.3875 HP** before the separate ordinary shield. Speed is **6.374025 m/s**, exactly the current Zhaev reference. Basic DPS is approximately 28.21 versus Zhaev's 28.24; Timmy trades reach/durability for targeted mobility/control. Runtime model scale is 70%, collision radius 0.28 m and capsule height 1.4 m. The shorter capsule is offset downward to preserve the existing hero-origin/ground convention.

The requested earlier 100-point conversion could not be found in the repository or retrieved history. The user approved a **provisional design allocation using Zhaev as reference**: damage 30, mobility 28, control 24, durability 10, utility 8, total 100. This is a design budget, not a recovered stat conversion formula or proof of measured competitive balance. Matchup/win-rate tuning remains necessary.

## Scratch contact, grip and counterplay

Selection is synchronous on hold/tap/release; only enemy heroes qualify. Soldiers/objectives cannot hide a hero or receive the ultimate. The normal melee cone default is read from the existing `AimingSettings` (currently 70 degrees), including per-attack overrides. The shared preview displays that cone without adding a trajectory line; basic targeting remains unchanged.

Scratch captures the enemy's current position once. After a 0.2-second warning, Timmy traverses toward that fixed endpoint over 0.4 seconds. A 0.75 m stand-off keeps the actors from occupying the same spot. The leap does not home. On landing, range, living enemy eligibility, damage gate, authority and sight are checked again. A dodged/blocked leap gives no grip or damage and keeps its committed cooldown. An invalid initial cast consumes no cooldown.

Successful contact roots the victim's movement for two seconds and locks Timmy's movement/actions during the channel. **The victim can still attack Timmy**, allowing a counterattack or stun. Damage is four 13.5 ticks at 0.5-second intervals, not an immediate 54-damage soft-target hit. Each tick rechecks contact and authority. Caster stun, death, disable, authority loss, forced displacement, victim death/disable, damage immunity, obstruction or separation stops remaining ticks and releases both roots. Roots are source-owned; cleanup does not remove other slows/stuns/boosts or another source's grip. Existing short-stun immunity is unchanged.

NGO command aiming intent is reused. The host rejects owner-position updates during a root/leap. Movement-only victim roots are represented by zero movement with action permission; the channel uses the existing action-lock/stun snapshot field. Timmy's owner receives the host's channel position. Existing timestamped combat-motion events relay scratch ticks and claw VFX/audio, including to the casting client, without client-side damage simulation. Packet layout is unchanged. This code path still requires a real two-peer latency test.

## Art, animation and effects

- Editable model: `ArtSource/Heroes/AndromedaTimmy/AndromedaTimmy.blend`.
- Unity model: `Assets/Alterverse/Resources/AuthoredHeroes/Timmy.fbx`; 10,392 triangles, one anatomy, two claw renderers and a separate glass visor, existing 19-bone rig contract. Claws are fully weighted to their matching WeaponGrip bone; palm/contact sockets coincide.
- Visor: a smooth circular spherical cap fitted to the existing helmet rim, with enough depth to cover the muzzle and whiskers. `DF_Visor` is fully weighted to Head, so it follows the existing head animation. Light-gray RGB is (0.78, 0.80, 0.82), opacity 0.30 = **70% transparency**. Blender source and FBX contain the same glass material; the existing hero importer configures transparent URP Lit alpha blending, front faces, no depth writing and no opaque shadow casting. The single surface avoids applying the gray tint twice. Importer version 14 refreshes cached hero imports. Native Unity/device rendering still requires visual confirmation.
- Editable modeled icon illustrations: `AndromedaTimmy-icons.blend`. Five distinct 512px PNGs are loaded by AbilityButtonView: 0 Claw Swipe, 1 Stardust, 2 Jet Pounce, 3 Scratch, 4 Claw. The basic paw has no skill sweep; Scratch has an amber capture ring.
- Studio reference: `AndromedaTimmy-preview.png`. This is a Blender render, not a screenshot of Unity gameplay.
- Common animation/retarget pipeline is reused. Basic claws alternate the existing slash clip. Scratch uses four contact-synchronous channel pulses; its action lock cannot suppress its own pose. Stun/death still interrupt it.
- Common pooled VFX are reused: cyan triple claw sweeps, stardust warning/impact circle, amber/cyan jet trail and landing ring, target warning and scratch impacts. No persistent new particles, duplicate attack scheduler, extra targeting loop or new animation controller is required. Basic/E reuse Zhaev's authored fast-cut recordings. Claw Swipe (Q) now uses the supplied `universfield-sword-blade-slicing-flesh-352708.mp3`, copied unchanged to `Assets/Alterverse/Resources/Audio/Heroes/Timmy/Q_Cast.mp3`. It plays once at Q release without added preparation/impact recordings. Stardust (W) uses the supplied `gregorquendel-sci-fi-sound-effect-designed-circuits-sfx-29-202030.mp3`, copied unchanged to `Assets/Alterverse/Resources/Audio/Heroes/Timmy/W_Cast.mp3` (3.984 seconds, 48 kHz stereo source). It starts once when the area resolves after its existing 0.3-second warning, including on a miss, and does not restart for additional enemies or layer preparation/impact sounds. Scratch now uses the supplied `yodguard-a-massive-axe-impact-hitting-metal-2-450253.mp3`, copied unchanged to `Assets/Alterverse/Resources/Audio/Heroes/Timmy/Ultimate_Selected.mp3` (3.024 seconds, 48 kHz stereo source). It plays once when the leap begins after the existing 0.2-second warning; the four damage ticks do not restart it or add an extra impact/preparation layer. The audio importer uses the existing mobile mono/preload settings. All previous cue indices remain stable; Claw Swipe occupies appended cue 123, Stardust cue 126 and Scratch cue 132, and network sound events retain Timmy's actor identity.

Rebuild assets from the repository root with Blender 4.5 LTS:

```bash
blender --background --python-exit-code 1 --python Tools/AndromedaTimmy/build_timmy.py
blender --background --python-exit-code 1 --python Tools/AndromedaTimmy/validate_timmy.py
```

For model-only updates, append `-- --model-only` to the build command; this regenerates the Blender model, FBX and studio preview while preserving the five attack icons and their editable source.

The generator uses original geometry/materials and CPU rendering. It writes model, FBX, previews, icon source and five icons. The validator checks both Blender source and reimported FBX: bone names, normalized weights, rigid claws, head-bound visor, 70% glass transparency, circular silhouette, outward normals, actual face clearance, nondegenerate finite geometry, contact sockets, physical scale, triangle budget and icon dimensions. Reports are committed alongside the art source.

## Verification and release checks

The production combat/input code is compiled against deterministic engine fixtures by `Tools/Aiming/Regression/run.py`, including the existing editor regressions. Timmy tests cover regular nine-hero availability, production audio compilation/dispatch, selected Q/W/R resource loading, one Stardust recording after its warning across multiple enemies, one Scratch recording across all four ticks and unchanged legacy cue indices, point sum, Zhaev speed reference, short basic range, single-target Q, multi-target/dodgeable W, reverse E, first-frame hero selection despite a closer soldier, tap/cancel, fixed-end dodge, two-second grip/four ticks, counterattack permission, caster/victim death, stun, disable, forced movement, wall rejection, snapshot semantics and source-owned root cleanup. Gameplay-clock first-damage samples: basic ~167 ms, Q ~175–200 ms, W 300 ms, Scratch ~1.1 s from cast (warning + leap + first tick). Zero dash timing in the fixture measures acceptance, not native traversal.

Final deterministic combat/input/audio run: **2,268 assertions passed**. The upper-body/attack-pose regression also passes for the existing eight heroes (**294,488 assertions**). Blender source/FBX round-trip checks and five icon renders were completed. These checks **do not constitute a Unity editor compile, native physics/rendering test, real multiplayer session or Android device test**.

Before live release, run the Unity checks and playtest: short/reverse flick, moving basic/skills, each collision/dodge, grip for a full two seconds, immunity and all interruptions, repeated combos/passive cooldown, paused match, respawn, walls/arena edge, all icon sizes/aspect ratios, skin/paw contacts through clips, readable ground height, two-peer owner/caster/victim synchronization at latency, and matchups against every Season 1 hero. Rebalance only central Timmy values from those results.
