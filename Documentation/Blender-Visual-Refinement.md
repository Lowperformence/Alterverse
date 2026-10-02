# Hero proportions, eleven planet surfaces and bunker detail

## Heroes

All seven heroes receive the reference-led geometry pass documented in `Tools/HeroRebuild/README.md`: compact head/neck and shoulder shapes, distinct torso tapers, fuller heavy-class legs and smoother arm transitions. Brakk remains the broadest silhouette. Existing skeleton joints, weapons, shop renderer names and movement are retained. Rebuild from the original anatomy with `build_heroes.py`, or run `refine_proportions.py` on the saved sources. The latter is versioned and idempotent.

## Planet surfaces

Every planet now has three 512px linear data textures: Ground and Stone contain signed slopes, roughness and height; Macro contains neutral value, cavity, roughness and sediment distribution. The new layer supplies broad geological patches visible from the gameplay camera while finer grains remain subordinate. Sand, regolith, organic soil, ice, basalt, wet stone and crystal retain separate patterns and their existing planet palettes.

`build_surface_details.py` authors the textures and the Blender material study. `SurfaceStudy.blend` references the committed textures using relative paths rather than duplicating 25 MB of packed images. The gameplay terrain height, collision, paths and cover are untouched. `DF_PlanetSurface` adds one geological texture sample; the transparent Diamond Core floor has the same separate layer, enabled only on its surface material. Android and PC import identical uncompressed RGBA data with mipmaps. A loaded planet uses about 4 MiB for the three detail textures including mips.

## Bunker

The Blender source and JSON author three replacement shells and five batched detail meshes. Chamfered concrete, recessed side panels, a framed rear blast door, access reader, roof hatch hardware, vent grilles and fasteners add depth. Turret hardware remains attached to the existing aiming pivot. The original roof plates were partly buried in the roof slab; the art pass raises them by 0.14 m and places their fasteners on the corrected tops.

The three shell meshes are fitted to their original local bounds. Added details stay within the existing footprint. Bunker placement, convex collision mesh, team, damage, range, aiming and muzzle are unchanged. Original rear panel meshes are hidden where the new door replaces them. All owned art meshes and the extra trim material are released on destruction. Blender previews include labeled preview-only approximations of retained live parts; only exported JSON parts are used by the runtime.

## Validation

Ran: seven FBX round trips, seven neutral guard/contact checks, unchanged hero rest-bone/weapon checks, 33 texture channel/import/seam checks, bunker topology/winding/UV/envelope checks, C# syntax checks and a byte comparison proving the bunker combat source differs only by its art hook.

Unity editor checks were extended for live geological bindings, all three texture kinds, bunker detail attachment and absence of enabled visual colliders. These checks, Unity C#/shader compilation and Android gameplay/performance review have not run because Unity is unavailable in the execution workspace.
