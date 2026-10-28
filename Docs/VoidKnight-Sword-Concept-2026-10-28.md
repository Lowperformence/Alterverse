# Void Knight sword — supplied concept silhouette, V6

The supplied screenshot replaces the V5 design direction. Its visible weapon features are a long broad dark blade, violet outer cutting edges, a dark longitudinal spine, a compact angular technical guard and round violet energy sockets. The reference cuts off the point; the distal point in this model is an inferred continuation, not a claimed exact match to unseen concept geometry. Armor and character proportions are outside this change.

## Current sources

- Full rigged source: `ArtSource/Heroes/VoidKnight/VoidKnight.blend`
- Rigged sword module: `ArtSource/Heroes/VoidKnight/Modules/Greatsword.blend`
- Standalone editable studio: `ArtSource/Heroes/VoidKnight/Sword-V6/VoidKnight-Conceptschwert.blend`
- Standalone GLB: `ArtSource/Heroes/VoidKnight/Sword-V6/VoidKnight-Conceptschwert.glb`
- Measured construction/material drawing: `ArtSource/Heroes/VoidKnight/Sword-V6/VoidKnight-Konzeptschwert-Zeichnung.png`
- Runtime: existing `Assets/Alterverse/Resources/AuthoredHeroes/VoidKnight.fbx`, preserving the importer/GUID.

V5 source, GLB and drawing remain versioned as history. V6 supersedes V5 for the current full hero and sword module. Use `rebuild_concept_sword.py` for the current design. The older `rebuild_greatsword.py` restores the superseded V5 style.

## Construction

The 1.633 m sword retains the original 0.38 m hand span and original rig-space frame. Broad near-parallel blade flats carry a layered dark spine and small fitted hardware. Physical violet bands track both outer cutting facets on both faces, rather than the central fuller. A 0.388 m compact technical guard has matching round energy sockets with separate recessed seats, bevel rings and faceted crystals. A segmented black two-handed grip terminates in an angular counterweight.

All 69 sword components stay rigidly weighted to the original `WeaponGripR` bone. The source replacement validates the coordinates and material indices of all 250 non-weapon body meshes, original rest bones and all twelve action keyframe data blocks before saving. Shared character materials and textures are reused.

## Reproduction and validation

With Blender 5.0.1, run from the hero source directory:

1. `rebuild_concept_sword.py`
2. `export_void_knight.py`
3. `export_modules.py`
4. `Sword-V6/render_sword.py`
5. `Sword-V6/make_drawing.py` with Pillow after rendering.

The current V6 builder also accepts its own output. Triangle totals are 3,652 for the sword and 30,910 for the hero (below 35,000). Reimporting the runtime FBX confirms two meshes (`DF_Anatomy`, `DF_WeaponR`), five materials, 19 bones, all twelve expected clips and the exact 30,910 triangles. Validation reports are alongside the standalone source. The construction sheet uses actual Blender renders.

This change modifies only sword assets, their generation sources, drawing and documentation. Hero release/price, Sivra/Ione abilities, armor, animation data, combat reach and other gameplay remain as in the preceding commit. For this additional visual revision only, import in Unity and rebuild the Android client; the earlier combat changes still require the previously documented matching dedicated server build. Native Unity/device rendering and hand-contact appearance still require inspection in the actual game.
