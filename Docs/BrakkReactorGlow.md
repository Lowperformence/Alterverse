# Brakk: fluorescent red spinal reactor

The elongated rear object is the authored **Spinal reactor**. The existing Rook
FBX merges it into `DF_Anatomy`, sharing the `Energy` material with the front chest
relic and cybernetic eye. Raising that shared material's emission would also
change those other details and the shared source used by Nerok.

`BrakkForearmTrim` now isolates only rear Energy triangles rigidly attached to
`Chest`, using renderer-to-model rest coordinates (`z < -0.04`). It clones the
instance mesh, appends a reactor-only material slot, and keeps all original
vertex/UV/normal/bone/bind-pose data. This runs once in the existing authored
presentation path for `HeroId.Rook`, including menu previews. The existing owner
tracks the new mesh and material for destruction. No source FBX reimport is
required and no extra update loop or dynamic light is added.

The material uses the included `DF_RuntimeLit` shader with active red emission.
This shader adds emission directly to the lit surface, so the reactor remains
bright in shadow without depending on the imported URP emission variant.

Tuning in `BrakkForearmTrim.cs`:

- `ReactorRed`: `(1, 0.025, 0.012)`.
- `ReactorEmissionStrength`: `6` (HDR).
- Metallic: `0.1`; smoothness: `0.4`.

The Blender source builder assigns the same values to a dedicated
`Brakk spinal reactor energy` material for future asset regeneration. Existing
arm cleanup, suits, weapons, animations and gameplay values are preserved.

Validation on 2026-10-11: the production mesh/material method compiles under
Mono/Roslyn against controlled substitutes; **146 checks** cover rear/front
separation, head/skin/blended-weight exclusion, all four skin-weight slots,
transformed mesh coordinates, absent material slots, clone/source preservation,
material alignment, tracked lifetime, repeat application and incompatible meshes.
C# syntax and Python builder syntax also pass. These checks do not render the
actual imported FBX in Unity. Android/Unity inspection of the default hero and
shop preview remains pending. Global bloom settings are not changed.
