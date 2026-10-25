# BOLT-7 playable test hero

Pull `codex/epic-toon-replacement-20261024`, let Unity finish importing, and choose **BOLT-7** in HEROES. Unlock him in STORE for **1,800 shards**. Preview remains available while locked; human selection/deployment requires ownership. Bots can draw BOLT-7 regardless of human ownership. The exact authored mesh, rig and poses are loaded from `Assets/Alterverse/Resources/Bolt7/Model.bytes`. Textures, portrait and five ability icons are loaded from PNG byte resources in `Bolt7/Raw`. Original PNG/FBX authoring files are preserved under `ArtSource/Heroes/Bolt7`.

## Controls and baseline

The existing desktop bindings are preserved: **Q = Frag Arc**, **E = Piston Ram**, **R = Impulse Grenade**, **F = ultimate**. These correspond to ability slots 0, 1, 2 and 3.  Basic fire uses the existing basic-attack control. On mobile, use the illustrated buttons and drag to aim.

| Attack | Result | Range / area | Cooldown |
| --- | --- | --- | --- |
| Gatling | 5 per shot; 0.2067s interval, three times Sivra's base fire rate | 5.5m | Basic cadence |
| Frag Arc | 35 centre damage, falling to 15 at the edge; 0.45s warning and lob | 5m cast, 1.4m radius | 5s |
| Piston Ram | 0.3s preparation, 2m dash, 40 damage to every enemy in the marked cone, 0.8m knockback | 3m / 70 degree cone anchored at dash start | 8s |
| Impulse Grenade | 25 damage and 0.8m outward knockback after a 0.55s grenade arc | 4m cast, 1.1m radius | 9s |
| Null Field | 0.9s warning, removes **current enemy personal base shields and overshields** once; **zero HP damage** | 6m cast, 3m radius | 75s; initial match lock 45s |

280 HP, 40 rechargeable base shield, 4.5m/s movement. These are starting playtest values, not a claim of final competitive balance. Existing damage buffs and mitigation still apply to normal attacks. Enemies can escape the marked area; cover blocks all four skill resolutions.

**Null Field does not disable future shields.** A new shield ability works immediately after impact. Base shields recharge after their usual 11.25 damage-free seconds. Star Fog is visual only and never removes a shield again. Allies and the caster keep their shields.

## Imported effects

Your licensed Epic Toon FX package must be present at `Assets/Epic Toon FX`, as for the other effect replacements on this branch. The editor automatically prepares Grenade muzzle/projectile/impact stages, Lightning Orb and Star Fog. The manual command is **DIMENSIONS FRONT > BOLT-7 > Prepare imported effects**. Missing stage references produce a clear editor error; the runtime logs when it is using a simple visual fallback.

Native effect roots use **2.5x** base visual scale, once. Frag Arc muzzle, flight and detonation have the requested **0.70** attack multiplier (30% smaller). Impulse Grenade additionally follows its smaller 1.1m radius. Cartoony Punch Heavy is fitted to the short ram contact. Gameplay AOE sizes are independent of particle scale. Null Field plays Lightning Orb and native Lightning Nova at impact and starts Star Fog 0.18s later. The Nova uses the circular-footprint shader clipped to the actual 3m ultimate radius. Damage is resolved on game authority, never by particle collision or the fog.

The runtime model contains the complete 19-bone rig, named muzzle sockets and 15 authored clips, sampled from Blender at 30 FPS. Its explicit Unity coordinates and bind matrices avoid FBX import scale and generic clip sampling. BOLT-7 does not pass through humanoid retargeting. LOD0 has 76,642 triangles; lower LOD authoring assets remain in the delivered model package. Material appearance, native PNG decoding, GPU skinning and the locally imported licensed effects still need native Unity playtesting.

## Checks to perform in Unity

1. Inspect the hero preview and game model: feet on the floor, ivory/gold armour, dark mechanisms, amber eyes, Gatling and launcher pointing forward.
2. Fire continuously: confirm faster cadence than Sivra and 5 baseline damage per hit; verify the shorter reach.
3. Test both grenades at full range, very close and directly under BOLT-7 (drag out then back to the button). Preview, flight endpoint, explosion and damage must share the released point. Move BOLT after release: the point must stay fixed. Test several enemies at centre/rim and one outside; entering/leaving enemies are checked when the explosion resolves. Cover and ally protection still apply.
4. Hold Piston Ram to see the normal melee cone. Hit several enemies across its full 3m / 70 degree area; confirm the amber sweep follows that cone. The damage origin stays at the dash start, including when a wall shortens the dash.
5. Wait for the initial 45s ultimate lock. Hit enemies with base shields and ability shields together. HP must remain unchanged and both current shield pools must become zero.
6. Immediately activate a new shield on a struck hero: it must work normally, including while Star Fog is visible. Check base recharge after 11.25s and protection for allies/outside targets.
7. Test cancellation on death/stun, respawn, then host/client replication with both peers on this revision. Dedicated servers also need rebuilding from this branch for the new hero ID.

## Automated verification

`python Tools/Combat/ShieldRegression/run.py --dotnet-root /path/to/dotnet8`

`python Tools/Combat/Bolt7Regression/run.py --dotnet-root /path/to/dotnet8`

These compile production Health, BOLT-7 combat, balance and kit code against deterministic Unity fixtures. They exercise the production manual aiming, tap placement and grenade scheduling at 30/60/120 FPS, including shields of arbitrary current size, immediate new shield activation, recharge, authority, cover, falloff and ram direction. They are not Unity rendering, asset-import or network-transport tests. A separate C# 9 syntax parse included the editor paths; a Unity editor/build is unavailable in the integration environment.


### BOLT-7 presentation regression

`python Tools/Visuals/Bolt7Regression/run.py --dotnet-root /path/to/dotnet8`

This compiles the production mesh, pose and texture loaders against controlled engine fixtures and reads the actual shipped data. Checks cover all clip endpoints/midpoints, the full geometry, named sockets, the five correct icons, portrait, texture maps, caching and construction without an FBX resource. It is not a native Unity render test.

`Tools/Bolt7/validate_runtime.py` compares the shipped rig skinning with Blender across 45 poses: 2,795,085 vertex comparisons passed, maximum positional error under 0.000002m. The geometry retains all 76,642 triangles; 62,113 runtime vertices include normal/UV seams. Original authoring assets remain available under ArtSource. The legacy imported Resources files are retained during this migration so local Unity-generated metadata does not block a pull; rendering uses Model.bytes and Raw PNG resources. Retaining those legacy resources temporarily increases player-build content.


### Pull compatibility

If GitHub Desktop reports that the nine legacy BOLT-7 PNG/FBX `.meta` files would be overwritten, close Unity and the error dialog, fetch origin, then pull the current branch head. The compatibility commit restores the original tracked files and metadata unchanged, so locally generated importer settings do not need to be discarded or stashed. The new runtime model and raw icons remain in use. Avoid stashing/discarding the project's thousands of unrelated local imports to resolve this migration.


### Complete native effects and audio

The importer retains native AudioSources, including a projectile flight loop when supplied, and reads original clip references from the native grenade chain before removing demo scripts. Missing sources are resolved only within Epic Toon FX and logged; no unrelated hero recording is substituted. Every stage owns its sound, including detonations that miss an enemy, so area hits do not duplicate sounds per victim. Voices are stopped on pooled return; flight stops at contact; delayed native sub-emitters and impact tails are allowed to finish. Native stage gain is raised to 1.4 and multiplied by the player's Master/SFX settings. Global mute still works. Verify perceived volume with the imported package on the target device.

The importer selects **Cartoony Punch Heavy** (Fight Action fallback if that family is unavailable), and adds **NovaLightningPurple** to Null Field while preserving Lightning Orb and Star Fog. The Nova uses a separate converted material folder so its footprint shader cannot change another stage's shared native material.

The common match-insertion transport captures and reveals all HeroIdentity renderers, including BOLT-7 and Sivra, and both use the same local-player creation and respawn path. BOLT-7 keeps the universal range-preview UI and now fills all five standard CombatHudLayout action slots. Its exceptional roster thumbnail is removed so hero list labels align consistently. These are source/fixture comparisons; native multiplayer and device acceptance remain separate.

`python Tools/Visuals/Bolt7VfxRegression/run.py --dotnet-root /path/to/dotnet8`

This checks native source selection, audio preservation and volume/mute, projectile loop cleanup, 30% grenade scaling, ram charge/trails/impact, Nova radius clipping, shader isolation and Orb-to-Fog timing.

Gatling now uses the native Small Bullet muzzle/projectile/impact stages and their package sounds as well. Imported normal/ultimate source materials remain isolated from the Nova clipping shader.

Null Field uses the user-selected `Audio/Heroes/Bolt7/Nullfield_Selected.mp3` (3.72s), played once at detonation. Orb/Nova/Fog retain their particles while their native sounds are muted for this selected cue; the chosen recording can finish after particle emission ends. Other attacks retain their native package chains.

Gatling presentation: muzzle, projectile and confirmed-hit effect use **one third of their previous visual size** (base 2.5x multiplied by 1/3). The fallback trace width follows the same factor. Shot endpoints, flight timing, damage, attack rate, range and audio gain remain unchanged.


### Grounded entry, aiming and full-catalog bots

Match entry and respawn sample the grounded Idle pose immediately. The authored airborne Spawn clip is retained in the model but is not played by match insertion. Death, fire and walk action state is reset on respawn.

Manual and tap AOE poses carry an explicit world point through the existing host/client command format. This fixes BOLT-7 discarding close preview points and throwing to maximum range. The projectile and authoritative damage use the same point captured on release. A 0.1mm numerical boundary tolerance prevents float rounding from excluding exact circle-edge targets; it does not add meaningful attack reach. Normal damage buffs/mitigation and shield absorption still apply.

`python Tools/AI/BotRosterRegression/run.py --dotnet-root /path/to/dotnet8`

Offline, co-op, online and dedicated bot slots draw from every HeroId, including paid heroes, without consulting human ownership or release gates. New rounds draw again while preserving remaining humans' chosen heroes. Bots avoid teammates' already selected heroes. Online draws use the match id and team as a deterministic seed; clients keep the authoritative roster. Human purchase/selection rules and ranked full-human requirements remain unchanged.


### Death-body rig compatibility

BOLT-7's authored 19-bone mech rig uses Pelvis, a rigid Chest and two weapon arms. The death-body builder now maps it to 13 independent passive bodies and 12 joints rather than requiring the humanoid Hips/Spine/Forearm/Hand layout. All original skin bones, Gatling barrel/ram details and grenade drum stay on private copied transforms. The gameplay skeleton is never driven by corpse physics. Existing humanoid/procedural ragdolls keep 16 bodies/15 joints, isolated physics, renderer-local fade and bounded lifetime/scene cleanup.

`python Tools/Visuals/HeroDeathRegression/run.py --dotnet-root /path/to/dotnet8`

The fixture compiles the real corpse builder with the shipped BOLT model and checks complete skin/weapon copies, joints, physics-scene routing, live-skeleton isolation, fade and cleanup. Native PhysX collapse must be checked in Unity using **DIMENSIONS FRONT > Checks > Hero death bodies (Play Mode)**. That check now understands the mech layout too.

### BOLT-7 purchase and server rollout

BOLT-7 is a released shard hero at **1,800 shards**, SKU `hero_bolt7`, permanent account key `DF_HeroOwned_Bolt7`. The Heroes screen and the dedicated BOLT-7 store tab use the normal price/owned/pending/insufficient-funds paths. The unlock is atomic with the earned-shard debit offline, or uses the existing durable server purchase operation online. Selection rechecks ownership after account changes. Bots do not consult this gate.

`python Tools/Shop/Bolt7PurchaseRegression/run.py --dotnet-root /path/to/dotnet8`

`cd Server/commerce` then `python -m unittest test_bolt7 test_ledger test_profile test_void_knight test_purchase_restoration`

Client checks exercise the real wallet, inventory, journal, selection, account cache and commerce coroutines. Server checks cover the actual SQLite and Firestore adapters (strict transactional Firestore fixture), earned-only/paid-only/mixed purchases, retries, parallel attempts, account isolation, ownership restoration and server-controlled pricing through HTTP.

**Rollout requirement:** rebuild/redeploy the existing account/commerce service with the updated shared `Assets/Alterverse/Resources/Commerce/ShardCatalog.json`. Its Dockerfile copies this catalog into the server image; an older running image does not know `hero_bolt7`. Preserve the existing authentication, Firestore storage and billing configuration. This is the persistent account service, separate from the ephemeral Edgegap match server. No production account deployment or real shard payment was performed by these source/fixture checks.
