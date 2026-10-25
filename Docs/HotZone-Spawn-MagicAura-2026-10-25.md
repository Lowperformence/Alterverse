# Hot Zone spawn preparation and Magic Aura

The reward previously constructed its complete procedural body, loaded creature
resources and created materials in `SpawnReward`. Match startup now builds two
complete actors for each possible winning team behind the loading screen. Health,
AI and replication are disabled during preparation. Planet detail managers get
half a second of unscaled loading time before the actors are parked inactive.

Spawn activates a prepared actor with full health and the existing occurrence/team
network identity. Dead actors and client removal messages return prepared actors
to the match-owned reserve. The existing host limit of two living rewards remains.
The separate Diamond Core presentation also stays cached while its actor is parked.
Custom entry paths without match preparation retain the original construction fallback.

`HotZoneSpawnAuraBuilder` selects a real `MagicAura` prefab from Epic Toon FX,
preferring the blue variant, and prepares its original particle composition for
the existing URP particle shader. The source package is not modified. Demo scripts,
colliders and source audio are excluded. A looping aura becomes a brief spawn pulse.
Two aura instances are loaded at match startup; spawn performs no resource loading
or instantiation. Its horizontal size follows the visible monster bounds and it
stays at the spawn point. Clients wait for the first authoritative position sample.

The shared material converter preserves optional empty particle/trail slots and
non-rendering particle drivers. Required particle, enabled-trail and mesh materials
still fail with the real source path, renderer hierarchy and slot. All dependent
import signatures include the converter version so existing prepared resources rebuild.

## Unity acceptance

Pull `codex/epic-toon-replacement-20261024`, let Unity finish compiling and importing,
then clear the Console to distinguish old messages. The Magic Aura importer runs
automatically and can also be invoked through
`DIMENSIONS FRONT > VFX > Prepare Hot Zone Magic Aura only`.

Verify the first and later reward spawns, both winning teams, repeated deaths,
host/client placement and return to menu. Review all planet bodies, especially
Diamond Core, Neptune and Pyraxis. Capture Unity Profiler CPU/GPU frame timing on
the target Android device around the first visible spawn: this environment cannot
verify native particle appearance, first GPU shader use or actual device frame time.

## Automated verification

`Tools/HotZone/SpawnRegression/run.py --dotnet-root <dotnet-sdk>` executes production
pooling, spawn orchestration, network identity, first-position aura placement and
Magic Aura import logic with controlled Unity substitutes; it also compiles the
server configuration. The Nuke, Small Bullet, Sivra Nova, Ultimate Lightning,
Thala, Selected Ability and Traveler suites cover the shared material converter.
These checks do not replace native Unity and Android acceptance.
