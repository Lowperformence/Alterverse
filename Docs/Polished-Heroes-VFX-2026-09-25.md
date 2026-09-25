# Polished heroes and combat VFX — first implementation

Base: `dev/vertical-slice`, commit `cec5f9b2bb678863b09a4153df1d3245e2ca5e8b`.

## Included

- Sivra Quell / Nyra: a new procedural scout model, narrower torso and limbs, layered pearl/graphite armor, compact sensor helmet, red energy accents, swept rifle shells and a short rear scarf. Used by the actual match model and menu preview. The rifle and existing cosmetic hooks retain their names, right-arm parent and muzzle coordinates.
- All seven heroes: shared rings and analytic smooth normals on lofted body parts; crisp end caps. Cloth/undersuit and metal response is stored in UV2 and survives mesh batching. Existing palettes and energy flags remain in vertex colors.
- Hero shader: distinct matte and metal highlights, restrained rim light and an isolated camera-space studio rig for menu portraits. Portraits use 2x MSAA. No full-screen bloom/SSAO was added in this pass.
- Shared attack effects: brighter authored cores for slashes, bolts, orbs and impact bursts; soft energy coronas within the existing pooled meshes; higher-resolution orb geometry. Warning effects use separate unembellished meshes and opt out of the core whitening. Warning rings retain their radius and geometry.
- The pool capacity, warning reserve, event triggers, target tracking and combat timing are unchanged. No per-effect lights, particles or Update-time mesh generation were introduced.

## Verification completed outside Unity

- C# syntax parsed with tree-sitter; no syntax errors in the inspected files.
- 86 literal loft profiles checked for positive radii, strictly increasing heights and finite outward side normals. One dynamically parameterized profile was not evaluated by this check.
- Reviewed weapon attachment, muzzle coordinates, cosmetic part names, property-block preservation and warning-pool separation.

## Required Unity/device verification

Unity is not installed in the editing environment. Shader compilation, Play Mode, screenshots and Android FPS have **not** been verified here.

1. Switch to `art/polished-heroes-vfx`, let Unity import, and check the Console for compilation errors.
2. In Play Mode, run `DIMENSIONS FRONT > Checks > Polished heroes (Play Mode)`. This creates temporary base hero rigs and checks materials, finite mesh data, UV2, draw budgets and Sivra's weapon attachment. Run the existing hero presentation checks as well for the equipped cosmetics.
3. Inspect all seven menu portraits, especially Sivra's rifle, armor overlap and framing. Review Original, Voidfall and weapon-overdrive variants.
4. Check normal attacks and all four abilities for every hero, including target death, cast cancellation and overlapping warning zones.
5. Compare Android frame time in a 5v5 fight with the previous build. The current changes retain draw batching and pooling, but have not been device-profiled.

## Scope

This is the first implemented style pass, not a claim that the complete reference quality has been reached. Sivra is the first new silhouette; the other six heroes receive the common surface/material upgrade and still need individual art review. Hand-authored texture/normal maps, additional model redesigns, shop artwork and full post-processing are future passes after the Unity visual review.
