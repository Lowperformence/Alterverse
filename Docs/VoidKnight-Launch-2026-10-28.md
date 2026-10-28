# Void Knight / Riftkeeper V5 and ability updates

Void Knight is purchasable for 1,600 Shards using `hero_void_knight` and the existing `DF_HeroOwned_VoidKnight` profile key. Timmy remains unreleased. The two optional Void Knight weapon colorways remain preview-only; their independent purchase guard and shop button retain that state when the hero launches.

The rebuilt greatsword retains the original 0.38 m hand span and rig frame. It has faceted cutting edges, a physically recessed two-sided energy channel, fitted chevron inlays, separate swept guard shells, a faceted crystal/bezel, diagonal grip wrapping, ferrules and a counterweight pommel. The editable Blender source has 53 named sword components. The hero FBX still uses two skinned meshes, five shared materials, 19 bones and all twelve original clips. No combat reach or animation data changed.

- Full hero: `ArtSource/Heroes/VoidKnight/VoidKnight.blend`
- Rigged sword module: `ArtSource/Heroes/VoidKnight/Modules/Greatsword.blend`
- Standalone studio source and GLB: `ArtSource/Heroes/VoidKnight/Sword-V5/VoidKnight-Riftkeeper.*`
- Detailed construction/material drawing: `ArtSource/Heroes/VoidKnight/Sword-V5/VoidKnight-Schwert-Zeichnung.png`
- Shipped model: `Assets/Alterverse/Resources/AuthoredHeroes/VoidKnight.fbx` (existing importer/GUID retained)

Sivra's Sightline Break now deals 80 TOTAL base damage, divided across the existing three shots. It does not deal 80 per shot. Timing, range, cooldown and shot count are unchanged. Its ability description reads the production damage value.

Ione Vaska's Invert Field transfers up to 20 EXISTING shield points per enemy hit, before its unchanged 26 normal damage. No enemy shield means no transfer. The existing authoritative Health transfer refills the caster's base shield first; overflow lasts three seconds within the existing shield cap. Actual transferred amounts are counted once in damage acknowledgements. Friendly, invulnerable, dead and non-authoritative targets cannot grant shield.

## Deployment

1. Pull `codex/epic-toon-replacement-20261024`, resolve/import the existing Unity project, and build the Android AAB.
2. Rebuild/publish the dedicated server from the same commit and update the Edgegap game-server version. The server must share the client ability rules.
3. Python commerce deployment uses the updated shared ShardCatalog automatically. The CURRENT standalone Cloud Run Node service instead needs its SKU added explicitly:

   `node Server/commerce/node/enable-void-knight.mjs CURRENT-index.js NEW-index.js`

   Deploy NEW-index.js as index.js with the existing package.json, entry point, service identity, environment and Firestore database. This helper only adds the single hero product at its existing price, refuses a conflicting existing definition, and does not deploy. Do not replace current server changes with an older export. No database migration, balance reset or Google Play Billing product is needed for an in-game Shard hero unlock.

Live Cloud Run deployment and native Unity/Android verification have NOT been performed here.

## Rebuild artwork

From the hero source directory, run `rebuild_greatsword.py` with Blender 5.0.1 against the current source. Then run `export_void_knight.py` and `export_modules.py`. The V5 builder accepts V3/V4/V5 sword geometry. It verifies unchanged body meshes, bones and all original action keyframes before saving. The standalone studio script is `Sword-V5/render_sword.py`; run that before `Sword-V5/make_drawing.py` (Pillow).

## Validation

- 250 body meshes, 19 original bones and 12 action data blocks unchanged during sword replacement.
- Sword 2,208 triangles; hero 29,466 triangles (under 35,000).
- FBX reimport: exactly two runtime meshes, five materials, 19 bones, 12 expected clips and 29,466 triangles.
- 76 production shield/ability/card checks, 170 existing Health checks.
- 129 Void Knight combat/integration checks, 14 actual released-catalog purchase checks and 16 cloak checks.
- Nine commerce tests cover release price, ownership, atomic debit, retry, mixed funds, restoration, insufficient funds and unchanged locked products.
- Node launch patch tests cover price/key, idempotence, preservation and rejection of conflicting/unknown source layouts.

Controlled engine/services are fixtures. These checks do not prove native rendering, live Google purchases or network transport.
