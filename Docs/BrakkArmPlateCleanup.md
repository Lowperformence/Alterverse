# Brakk Orun: embedded arm plates

The highlighted arm plates came from two layers that survived the earlier cleanup:

- `DF_Anatomy` contains three imported bracer shells per arm. Only the first uses
  `Champagne metal`; the other two use `Signature enamel`. The old filter removed
  only the metal shell and recolored the four surviving enamel shells gold.
- `HeroLimbTechDetails` generated upper-arm panels after import. The old runtime
  cleanup removed its forearm panels but retained its upper-arm panels and fasteners.

`BrakkForearmTrim` now removes rigid forearm triangles in both imported bracer
materials and cleans both upper-arm and forearm triangles from the tech-detail layer.
`HeroLimbTechDetails` skips generating those arm panels for `HeroId.Rook` while
retaining its leg details. Existing articulated-detail cleanup remains in place.

The material and bone checks preserve the fabric anatomy, grey shoulder cages,
gauntlets, leg armor, skeleton and animation bindings. Meshes are cloned per Brakk
instance, so Nerok and other heroes retain their own appearance. Gameplay values
are unchanged. The fix applies to both the menu model and gameplay through the
shared `AuthoredHeroPresentation.TryCreate` path; no FBX regeneration is required.

The follow-up fixes a separate import-cache gap: the upper-arm generation change
landed after importer version 12, without increasing `AuthoredHeroImporter.GetVersion`.
Unity can therefore retain previously generated bronze plates in the imported Rook
asset. Version 13 forces the authored FBX assets to reimport after compilation.
The import pass also skips Brakk's additional hand/knuckle plates, whose fitting
bounds can overlap the forearm. The original black gauntlet chassis and red knuckles
are separate authored geometry and remain intact. Leg detail generation is preserved.
Unreadable meshes now report the skipped runtime cleanup instead of silently leaving
the old arm overlays visible.

## Remaining embedded plates

The follow-up also handles imported bronze/enamel shells assigned to `UpperArm`,
not just `Forearm`, and cached detail renderers with name suffixes. Bronze overlays
are recognized by material family or bronze color; fabric, skin, grey shoulder cages
and silver shoulder plates are excluded from removal.

For any remaining recognizable bronze arm overlay (including renamed renderers and
blended wrist weights), a runtime fallback fits it outside the actual fabric anatomy.
Triangle-connected components are welded in the lookup across FBX normal/UV seams.
Each whole plate translates along its outward radial direction until its innermost
vertex clears the fabric support plane, with a small margin. Sparse fabric rings use
the conservative whole-limb envelope. Plate topology, thickness and bone weights stay
unchanged; already clear plates do not move. All mesh changes belong only to Brakk's
instance, and the source mesh shared with Nerok remains unchanged.

Validation: the complete production `BrakkForearmTrim` compiles and passes 343
executable fixture checks, including both arms, FBX seam duplication, blended wrist
weights, renamed bronze materials, sparse fabric rings, rigid imported upper-arm
shells, suffixed cached detail renderers, unchanged fabric/gauntlets/grey shoulders,
and source-mesh isolation from Nerok. The previous production version fails the
embedded-plate clearance case. Native Unity import/skinning and visual verification
remain pending. Pull the change outside
Play mode, allow the model reimport to finish, then restart Play mode to recreate
existing model instances. A Play-mode restart alone does not invalidate FBX cache.

