# BOLT-7 playable test hero

Pull `codex/epic-toon-replacement-20261024`, let Unity finish importing, and choose **BOLT-7** in HEROES. He is available without a purchase. The exact authored mesh, rig and poses are loaded from `Assets/Alterverse/Resources/Bolt7/Model.bytes`. Textures, portrait and five ability icons are loaded from PNG byte resources in `Bolt7/Raw`. Original PNG/FBX authoring files are preserved under `ArtSource/Heroes/Bolt7`.

## Controls and baseline

The existing desktop bindings are preserved: **Q = Splitterbogen**, **E = ram**, **R = Impulsgranate**, **F = ultimate**. These correspond to the HUD's Q/skill-1, W/skill-2 and ultimate slots.  Basic fire uses the existing basic-attack control. On mobile, use the illustrated buttons and drag to aim.

| Attack | Result | Range / area | Cooldown |
| --- | --- | --- | --- |
| Gatling | 5 per shot; 0.2067s interval, three times Sivra's base fire rate | 5.5m | Basic cadence |
| Splitterbogen | 35 centre damage, falling to 15 at the edge; 0.45s warning and lob | 5m cast, 1.4m radius | 5s |
| Kolbenramme | 0.3s preparation, 2m dash, 1m front strike; 40 damage, 0.8m knockback | 70 degree front cone after movement | 8s |
| Impulsgranate | 25 damage and 0.8m outward knockback after a 0.55s grenade arc | 4m cast, 1.1m radius | 9s |
| Nullfeld | 0.9s warning, removes **current enemy personal base shields and overshields** once; **zero HP damage** | 6m cast, 3m radius | 75s; initial match lock 45s |

280 HP, 40 rechargeable base shield, 4.5m/s movement. These are starting playtest values, not a claim of final competitive balance. Existing damage buffs and mitigation still apply to normal attacks. Enemies can escape the marked area; cover blocks all four skill resolutions.

**Nullfeld does not disable future shields.** A new shield ability works immediately after impact. Base shields recharge after their usual 11.25 damage-free seconds. Star Fog is visual only and never removes a shield again. Allies and the caster keep their shields.

## Imported effects

Your licensed Epic Toon FX package must be present at `Assets/Epic Toon FX`, as for the other effect replacements on this branch. The editor automatically prepares Grenade muzzle/projectile/impact stages, Lightning Orb and Star Fog. The manual command is **DIMENSIONS FRONT > BOLT-7 > Prepare imported effects**. Missing stage references produce a clear editor error; the runtime logs when it is using a simple visual fallback.

Native effect roots use **2.5x** base visual scale, once. Splitterbogen muzzle, flight and detonation have the requested **0.70** attack multiplier (30% smaller). Impulsgranate additionally follows its smaller 1.1m radius. Cartoony Punch Heavy is fitted to the short ram contact. Gameplay AOE sizes are independent of particle scale. Nullfeld plays Lightning Orb and native Lightning Nova at impact and starts Star Fog 0.18s later. The Nova uses the circular-footprint shader clipped to the actual 3m ultimate radius. Damage is resolved on game authority, never by particle collision or the fog.

The runtime model contains the complete 19-bone rig, named muzzle sockets and 15 authored clips, sampled from Blender at 30 FPS. Its explicit Unity coordinates and bind matrices avoid FBX import scale and generic clip sampling. BOLT-7 does not pass through humanoid retargeting. LOD0 has 76,642 triangles; lower LOD authoring assets remain in the delivered model package. Material appearance, native PNG decoding, GPU skinning and the locally imported licensed effects still need native Unity playtesting.

## Checks to perform in Unity

1. Inspect the hero preview and game model: feet on the floor, ivory/gold armour, dark mechanisms, amber eyes, Gatling and launcher pointing forward.
2. Fire continuously: confirm faster cadence than Sivra and 5 baseline damage per hit; verify the shorter reach.
3. Aim grenades at the centre and boundary, and place cover between the explosion and a target. Confirm the small warning circle matches the hit area.
4. Ram forward and into a wall; check that enemies behind BOLT-7 are not hit.
5. Wait for the initial 45s ultimate lock. Hit enemies with base shields and ability shields together. HP must remain unchanged and both current shield pools must become zero.
6. Immediately activate a new shield on a struck hero: it must work normally, including while Star Fog is visible. Check base recharge after 11.25s and protection for allies/outside targets.
7. Test cancellation on death/stun, respawn, then host/client replication with both peers on this revision. Dedicated servers also need rebuilding from this branch for the new hero ID.

## Automated verification

`python Tools/Combat/ShieldRegression/run.py --dotnet-root /path/to/dotnet8`

`python Tools/Combat/Bolt7Regression/run.py --dotnet-root /path/to/dotnet8`

These compile production Health, BOLT-7 combat, balance and kit code against deterministic Unity fixtures. They cover 214 assertions in total, including shields of arbitrary current size, immediate new shield activation, recharge, authority, cover, falloff and ram direction. They are not Unity rendering, asset-import or network-transport tests. A separate C# 9 syntax parse included the editor paths; a Unity editor/build is unavailable in the integration environment.


### BOLT-7 presentation regression

`python Tools/Visuals/Bolt7Regression/run.py --dotnet-root /path/to/dotnet8`

This compiles the production mesh, pose and texture loaders against controlled engine fixtures and reads the actual shipped data. 305 checks cover all clip endpoints/midpoints, the full geometry, named sockets, the five correct icons, portrait, texture maps, caching and construction without an FBX resource. It is not a native Unity render test.

`Tools/Bolt7/validate_runtime.py` compares the shipped rig skinning with Blender across 45 poses: 2,795,085 vertex comparisons passed, maximum positional error under 0.000002m. The geometry retains all 76,642 triangles; 62,113 runtime vertices include normal/UV seams. Original authoring assets remain available under ArtSource. The legacy imported Resources files are retained during this migration so local Unity-generated metadata does not block a pull; rendering uses Model.bytes and Raw PNG resources. Retaining those legacy resources temporarily increases player-build content.


### Pull compatibility

If GitHub Desktop reports that the nine legacy BOLT-7 PNG/FBX `.meta` files would be overwritten, close Unity and the error dialog, fetch origin, then pull the current branch head. The compatibility commit restores the original tracked files and metadata unchanged, so locally generated importer settings do not need to be discarded or stashed. The new runtime model and raw icons remain in use. Avoid stashing/discarding the project's thousands of unrelated local imports to resolve this migration.


### Complete native effects and audio

The importer retains native AudioSources, including a projectile flight loop when supplied, and reads original clip references from the native grenade chain before removing demo scripts. Missing sources are resolved only within Epic Toon FX and logged; no unrelated hero recording is substituted. Every stage owns its sound, including detonations that miss an enemy, so area hits do not duplicate sounds per victim. Voices are stopped on pooled return; flight stops at contact; delayed native sub-emitters and impact tails are allowed to finish. Native stage gain is raised to 1.4 and multiplied by the player's Master/SFX settings. Global mute still works. Verify perceived volume with the imported package on the target device.

The importer selects **Cartoony Punch Heavy** (Fight Action fallback if that family is unavailable), and adds **NovaLightningPurple** to Nullfeld while preserving Lightning Orb and Star Fog. The Nova uses a separate converted material folder so its footprint shader cannot change another stage's shared native material.

The common match-insertion transport captures and reveals all HeroIdentity renderers, including BOLT-7 and Sivra, and both use the same local-player creation and respawn path. BOLT-7 keeps the universal range-preview UI and now fills all five standard CombatHudLayout action slots. Its exceptional roster thumbnail is removed so hero list labels align consistently. These are source/fixture comparisons; native multiplayer and device acceptance remain separate.

`python Tools/Visuals/Bolt7VfxRegression/run.py --dotnet-root /path/to/dotnet8`

This checks native source selection, audio preservation and volume/mute, projectile loop cleanup, 30% grenade scaling, ram charge/trails/impact, Nova radius clipping, shader isolation and Orb-to-Fog timing.

Gatling now uses the native Small Bullet muzzle/projectile/impact stages and their package sounds as well. Imported normal/ultimate source materials remain isolated from the Nova clipping shader.

Nullfeld uses the user-selected `Audio/Heroes/Bolt7/Nullfield_Selected.mp3` (3.72s), played once at detonation. Orb/Nova/Fog retain their particles while their native sounds are muted for this selected cue; the chosen recording can finish after particle emission ends. Other attacks retain their native package chains.
