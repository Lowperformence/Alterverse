# Hot Zone spawn: geometry preparation and reuse

The match builds two complete rewards for each possible winning team under the loading
surface and reuses them for later objectives. The real Epic Toon Magic Aura and its
first-authoritative-position client placement remain intact. See
`HotZone-Spawn-MagicAura-2026-10-25.md` for the complete actor/aura lifecycle.

Matching reference monster rigs now share completed immutable mesh geometry instead of
rebuilding/retaining a full copy per actor. Sharing is opt-in for the reference rigs, frost
bear and mechanical scorpion. Resource identity plus matching rest frames and bind poses
are required; different poses get separate geometry. Bones, animation and materials remain
independent, including damage tint. The last owner frees the shared mesh. This reduces the
loading work and extra mesh memory needed by the four prepared actors.

Before the loading surface is removed, all prepared mesh-filter and skinned-renderer geometry
is explicitly uploaded, including the separately parented diamond guardian. A match-local set
avoids duplicate uploads of shared meshes. Meshes remain CPU-readable. Dedicated server
builds skip the graphics upload. The prepared rigs also reset their movement samples on
activation, so a changed spawn position is not interpreted as a large animation step.

Snapshot reception rejects a cached old occurrence whose pooled actor already carries a
different key. A late pose/health packet for the former occurrence must never move or damage
the new monster; existing removal-message protection and tombstones remain intact. No network
format, spawn timing, health, damage, attack ranges, cooldowns, visible model detail or warnings
are changed by these supplemental optimizations.

## Validation

- `Tools/HotZone/SpawnRegression/run.py`: production reserve/activation/aura import checks,
  including uploads before reveal, no extra upload/build at warm spawn, repeated reuse, host
  authority and first authoritative aura position. It also compiles the server configuration.
- `Tools/Multiplayer/HotZoneResultRegression/run.py`: production spawn/snapshot/result tests,
  including rejection of an old occurrence's pose/health after actor reassignment.
- `Tools/Performance/HotZoneArtRegression/run.py`: compiles the complete production resource
  mesh and authored-art classes; verifies shared ownership, different-pose isolation,
  independent materials, authored triangle/UV preservation, readable upload policy and cleanup.

All executed checks use controlled Unity graphics/physics/NGO substitutes. They cannot prove
Android frame time, shader first-use cost, native appearance or actual phone RAM consumption.
Profile the first and later rewards on the target phone after the next AAB and server build,
for both teams and all arenas, including diamond, Noxara, Neptune and Pyraxis. No release was
deployed as part of these code changes.
