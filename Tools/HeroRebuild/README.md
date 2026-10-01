# Individual hero bodies, original movement, authored weapon shop

This revision supersedes the first all-in-one mesh pass.

- Each hero has one body renderer and one or two separately weighted weapon renderers (`DF_WeaponL/R`, or `DF_GauntletL/R` for Brakk).
- Weapon-shop colorways are applied only to those renderer references. Selecting Standard restores the original materials. Existing purchase ownership, prices and equipped-variant storage are unchanged.
- The existing HeroVisualMotion and HeroProceduralRig movement calculations are retained, including gait, guard, attacks, casts, recoil, dash deformation and body turn/weight shifts. Renderer-free control pivots retarget their rotation deltas into the imported bones. Imported Animators are disabled so they cannot compete. The FBX clips remain source-authoring assets, not the active runtime movement.
- Kaelon has a wider torso/hips and a closed helmet; Brakk has the broadest chest, reinforced gauntlets and industrial shoulder armor. Other heroes have distinct proportions and headgear. Veyr has a full-face mask and paired crescent blades.
- All heroes have shaped boots with flat soles, heel blocks and toe caps. Anatomy and costume share normalized nearest-bone skin weights. The anatomical hands follow the forearms. Separate WeaponGripL/WeaponGripR child bones carry weapon angles; weapon rotations never deform the body. Brakk's armor gauntlets continue to follow his hands. All rigs have 19 bones.

## Files

`Assets/Alterverse/Resources/AuthoredHeroes/*.fbx` are the Unity models. `ArtSource/Heroes/*.blend` are editable Blender 4.5 LTS sources. Preview images are Blender studio renders; they are not Unity screenshots and their authoring poses do not prove runtime retargeting.

## Rebuild

Download the free Human Base Meshes 1.4.1 bundle from https://www.blender.org/download/demo-files/ and set DF_HUMAN_BASE_MESH_SOURCE to its human_base_meshes_bundle.blend file.

    blender --background --python-exit-code 1 --python Tools/HeroRebuild/build_heroes.py
    blender --background --python-exit-code 1 --python Tools/HeroRebuild/validate_fbx.py

## Validation

Seven FBX round trips check mesh counts, named shop weapon renderers, armature bindings, normalized skin weights, UVs and animation clips; they also verify independent weapon weights, neutral idle wrists and coincident hand/grip anchors. C# syntax checks pass. The existing gait and attack calculations are preserved; only the imported-rig connection changes. Unity compilation, runtime posing/grips, shop preview/equip and Android rendering still require in-engine verification; Unity is unavailable in the execution environment.

## License

Blender Human Base Meshes 1.4.1 README declares the provided anatomy assets CC0. Costumes, weapons, rigs and generator modifications are original project work. No Rain Rig is used.
