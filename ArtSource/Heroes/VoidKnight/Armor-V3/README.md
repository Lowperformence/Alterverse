# Void Knight – Armor V3 implementation

The 29 October 2026 V3 armor/helmet concepts are implemented as editable Blender geometry on the existing animated hero. This is geometry revision 7 of the production source; the concept's **V3** name is retained separately from earlier source revision numbers.

![Actual Blender geometry](Renders/armor-angle.png)

## Changes

- Rebuilt closed helmet with a narrow V visor and central illuminated slit, overlapping crown/cheek/nape plates, temple service caps, seated hardware and surface markings.
- Layered chest plates and core bezel, segmented shoulders, abdominal lames, split hip guards, thigh shells, knee pivots, shin inserts and instep plates.
- Secondary panel islands provide real inset borders. Main plates and luminous inserts remain separate editable objects. Small hardware and markings are batched by assembly, material and bone.
- Existing five material definitions and packed surface images are retained. Runtime shaders and gameplay code are unchanged.
- Original sword, hands, undersuit and 19-bone rest skeleton are preserved. The requested carry correction rebakes only the eight arm/hand/grip channels: the blade clears the right upper arm and chest in Idle/Run. Strike contacts, non-arm keys, all 12 clip identities, frame ranges, FBX path and asset GUID remain unchanged.

The production source is [../VoidKnight.blend](../VoidKnight.blend). Individual rest-aligned assemblies are in [../Modules](../Modules). The runtime file remains `Assets/Alterverse/Resources/AuthoredHeroes/VoidKnight.fbx`.

## Verified artifact

| Item | Result |
| --- | --- |
| Editable source mesh objects | 360 |
| Newly built armor/detail objects | 211 |
| Protected original mesh objects compared | 149 |
| Runtime mesh objects | 2: `DF_Anatomy`, `DF_WeaponR` |
| Runtime triangles | 28,722; previous model 30,910 |
| Materials | 5 |
| Original skeleton | 19 bones |
| Animation clips | 12; 384 evaluated frames |
| Individual assembly files opened | 17 |
| Sword-to-eye visibility checks | 180 rays |

The independent [source validation](source-validation.json) compares protected vertex coordinates, topology, material slots, UVs, weights, bone rest matrices and non-arm action keys/handles to the pre-change source. [Build validation](build-validation.json) lists every new module and its bone binding; its action-immutability check applies to the armor construction step. [Carry validation](carry-clearance-validation.json) checks the deliberately adjusted pose, blade/armor intersections and strike-contact preservation. Existing refinement, module and FBX checks also pass.

## Rebuild and inspect

Use Blender 5.0.1, from the repository root. The builder is repeatable: it replaces the armor while retaining protected meshes and actions from the input production source.

```bash
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/Armor-V3/build_armor_v3.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/Armor-V3/fix_carry_clearance.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/Armor-V3/validate_armor_v3.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/export_void_knight.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/validate_fbx.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/export_modules.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/validate_modules.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/validate_refinement.py
blender --background --threads 4 --python-exit-code 1 --python ArtSource/Heroes/VoidKnight/Armor-V3/render_armor_v3.py
```

To compare against an independently retained pre-change file, add `-- --before /absolute/path/to/baseline.blend --allow-carry-fix` to `validate_armor_v3.py`. The flag permits only the requested arm-channel changes. Without a baseline it checks the saved artifact's structure, weights, finite coordinates, module coverage, closed new shells, material budget and packed images.

## References and presentation

- [Armor concept](Concept-Armor.png), [helmet concept](Concept-Helmet.png), [German Blender plan](Blender-Bauplan.md).
- [Helmet close-up](Renders/helmet-angle.png), [front](Renders/armor-front.png), [rear](Renders/armor-back.png).
- Actual action-pose studio renders: [carry](Renders/carry.png), [attack contact](Renders/attack.png), [guard](Renders/guard.png), [ultimate contact](Renders/ultimate.png).

The illustrated concepts are adapted to the existing proportions, skeleton and real-time mesh budget. The studio images show the actual Blender geometry. Native Unity import/rendering and an Android device build have not been run in this environment; those remain the final presentation check in the client project.
