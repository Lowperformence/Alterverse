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

Validation: C# syntax parsing and an executable fixture of the production triangle
filter against the imported six-bracer material layout, generated detail layers and
preserved anatomy/shoulder/hand/leg cases. Unity visual verification remains pending.
Reload the hero preview or restart Play mode to recreate existing model instances.
