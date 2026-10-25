# BOLT-7 playable test hero

Pull `codex/epic-toon-replacement-20261024`, let Unity finish importing, and choose **BOLT-7** in HEROES. He is available without a purchase. The rigged FBX, textures, portrait and four ability icons are included under `Assets/Alterverse/Resources/Bolt7`.

## Controls and baseline

The existing desktop bindings are preserved: **Q = grenade**, **E = ram**, **F = ultimate**. These correspond to the HUD's Q/skill-1, W/skill-2 and ultimate slots. The third normal skill slot is intentionally unused. Basic fire uses the existing basic-attack control. On mobile, use the illustrated buttons and drag to aim.

| Attack | Result | Range / area | Cooldown |
| --- | --- | --- | --- |
| Gatling | 5 per shot; 0.2067s interval, three times Sivra's base fire rate | 5.5m | Basic cadence |
| Splitterbogen | 35 centre damage, falling to 15 at the edge; 0.45s warning and lob | 5m cast, 1.4m radius | 5s |
| Kolbenramme | 0.3s preparation, 2m dash, 1m front strike; 40 damage, 0.8m knockback | 70 degree front cone after movement | 8s |
| Nullfeld | 0.9s warning, removes **current enemy personal base shields and overshields** once; **zero HP damage** | 6m cast, 3m radius | 75s; initial match lock 45s |

360 HP, 60 base shield, 4.5m/s movement. These are starting playtest values, not a claim of final competitive balance. Existing damage buffs and mitigation still apply to normal attacks. Enemies can escape the marked area; cover blocks all three skill resolutions.

**Nullfeld does not disable future shields.** A new shield ability works immediately after impact. Base shields recharge after their usual 11.25 damage-free seconds. Star Fog is visual only and never removes a shield again. Allies and the caster keep their shields.

## Imported effects

Your licensed Epic Toon FX package must be present at `Assets/Epic Toon FX`, as for the other effect replacements on this branch. The editor automatically prepares Grenade muzzle/projectile/impact stages, Lightning Orb and Star Fog. The manual command is **DIMENSIONS FRONT > BOLT-7 > Prepare imported effects**. Missing stage references produce a clear editor error; the runtime logs when it is using a simple visual fallback.

Native effect roots use exactly **2.5x** visual scale, once. Gameplay AOE sizes are independent of particle scale. Nullfeld plays Lightning Orb at impact and starts Star Fog 0.18s later. Damage is resolved on game authority, never by particle collision or the fog.

The FBX contains a 19-bone rig, named muzzle sockets and 15 authored clips. BOLT-7 samples its own clips and does not pass through humanoid retargeting. LOD0 has 76,642 triangles; lower LOD authoring assets remain in the delivered model package. FBX import, material appearance, clip axes and the locally imported licensed effects still need native Unity playtesting.

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

These compile production Health, BOLT-7 combat, balance and kit code against deterministic Unity fixtures. They cover 207 assertions in total, including shields of arbitrary current size, immediate new shield activation, recharge, authority, cover, falloff and ram direction. They are not Unity rendering, asset-import or network-transport tests. A separate C# 9 syntax parse included the editor paths; a Unity editor/build is unavailable in the integration environment.
