# Bomber detonation: Nuke Cone Explosion

Only the neutral aircraft's bomb detonation changes after the original VFX/audio restoration. Every arena uses the purchased **3D NukeConeExplosionFire** composition:

`Assets/Epic Toon FX/Prefabs/Combat/Explosions/NukeConeExplosion/NukeConeExplosionFire.prefab`

No 2D Alternate variant or old replacement-library entry is used. The authored particle/mesh layers, bursts, colours, curves, speeds, detail counts, trails and sub-emitters are retained. The dedicated URP material adapter keeps source textures, material colours/alpha and blend factors. The importer strips demo scripts, colliders and prefab audio so the existing bomber gameplay and original external explosion sound remain authoritative.

`BomberNukeBuilder` automatically prepares just this composition after script import, before Play Mode and before a player build. The purchased import must be present at the path above. Generated files stay under `Assets/Alterverse/Resources/CombatVfx/BomberNuke/`; the purchased source is not overwritten. A dependency signature prevents unnecessary rebuilding. Manual recovery menu: `DIMENSIONS FRONT > VFX > Prepare bomber Nuke Cone only`.

The complete animation is sampled to fit its horizontal extent to the actual **3.24-unit blast radius**. Height is excluded from that fit so a tall cone is not reduced to a tiny effect. A dedicated shader also clips visible fragments to the real circular footprint. Cleanup waits for the particle systems, including delayed layers and smoke tails, to finish. The old blast is not layered over a successful Nuke detonation. If the generated asset is unavailable in Editor, the previous explosion is a visible fallback with a warning; player builds fail preparation if the selected purchased source is missing.

Damage, hit radius, warnings, bomb flight, detonation timing and original explosion audio are unchanged. Other attacks, shield effects, the shield-amount HUD bar, menu and shop remain as restored in `a7a8b32`.

Validation: 121 focused import/playback/footprint/audio contracts pass across all 14 production `ArenaVariant` values, alongside the existing 4,779 VFX/combat assertions. Both use controlled engine/editor fixtures. They do not render the purchased prefab. Native Unity appearance, the complete cone/smoke sequence, camera readability and Android performance still require Play Mode/device review with the imported package.

Visual size adjustment: +150% means a 2.5 multiplier on the prepared Nuke composition. The fragment footprint remains clipped to the original 3.24-unit damage radius. Audio, emission timing and gameplay remain unchanged.
