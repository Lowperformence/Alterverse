# Individual hero bodies, original movement, authored weapon shop

This revision supersedes the first all-in-one mesh pass.

- Each hero has one body renderer and one or two separately weighted weapon renderers (`DF_WeaponL/R`, or `DF_GauntletL/R` for Brakk).
- Weapon-shop colorways are applied only to those renderer references. Selecting Standard restores the original materials. Existing purchase ownership, prices and equipped-variant storage are unchanged.
- The existing HeroVisualMotion and HeroProceduralRig movement calculations are retained, including gait, attacks, casts, recoil, dash deformation and body turn/weight shifts. Renderer-free control pivots retarget their rotation deltas into the imported bones. Imported Animators are disabled so they cannot compete. The FBX clips remain source-authoring assets, not the active runtime movement.
- Kaelon has a wider torso/hips and a closed helmet; Brakk has the broadest chest, reinforced gauntlets and industrial shoulder armor. Other heroes have distinct proportions and headgear. Veyr has a full-face mask and paired crescent blades.
- All heroes have shaped boots with flat soles, heel blocks and toe caps. Anatomy and costume share normalized nearest-bone skin weights. The anatomical hands follow the forearms. Separate WeaponGripL/WeaponGripR child bones remain rigid relative to the hand; actual handle centers are fitted to the palms in the neutral guard. Independent world-space weapon rotation is removed. Named palm and handle-contact sockets verify contact during exported clips. Sivra has a rifle foregrip socket driving her supporting hand. Brakk's armor gauntlets continue to follow his hands. All rigs have 19 bones.

## Files

`Assets/Alterverse/Resources/AuthoredHeroes/*.fbx` are the Unity models. `ArtSource/Heroes/*.blend` are editable Blender 4.5 LTS sources. Preview images are Blender studio renders; they are not Unity screenshots and their authoring poses do not prove runtime retargeting.

## Rebuild

Download the free Human Base Meshes 1.4.1 bundle from https://www.blender.org/download/demo-files/ and set DF_HUMAN_BASE_MESH_SOURCE to its human_base_meshes_bundle.blend file.

    blender --background --python-exit-code 1 --python Tools/HeroRebuild/build_heroes.py
    blender --background --python-exit-code 1 --python Tools/HeroRebuild/validate_fbx.py

## Validation

Seven FBX round trips check mesh counts, named shop weapon renderers, armature bindings, normalized skin weights, UVs and animation clips; they also verify independent weapon weights, neutral idle wrists, coincident hand/grip anchors, and sampled handle/palm contact across all nine exported clips. C# syntax checks pass. The existing gait and attack calculations are preserved; the imported-rig connection and class-specific neutral guards are corrected. Unity compilation, runtime posing/grips, shop preview/equip and Android rendering still require in-engine verification; Unity is unavailable in the execution environment.

## License

Blender Human Base Meshes 1.4.1 README declares the provided anatomy assets CC0. Costumes, weapons, rigs and generator modifications are original project work. No Rain Rig is used.

Ione now follows the latest open-face elf reference: blonde layered hair, pointed ears, silver diadem, emerald eyes and fitted violet-gold garments. The sealed demon helm and horns are removed.

Held weapons now use modeled curled fingers and an opposing thumb instead of folded base-mesh fingers. These hand meshes are skinned only to anatomical Hand bones and remain in the body renderer, so weapon colorways never recolor the hands. `validate_guard_poses.py` renders the seven neutral guard simulations and checks rifle support contact; it does not run Unity.

## Anatomical arm and reference update

Arm joints now use separate measured male/female base-mesh coordinates. Torso shaping no longer pinches the forearms, and armor follows the revised joints. Held weapons use uniform scaling, so broad bodies do not bend barrels or staffs. The existing locomotion path and 19-bone contract remain intact. Rifle support uses four correction passes.

Ione's latest reference is reflected in blue-grey skin, dark auburn hair, luminous pale eyes, a tall split crown, layered gold breast armor and a pale center panel over violet robes. This is an original stylized interpretation, not an exact replica.

FBX checks cover arm ratios and symmetry, normalized skin weights, bones, clips, weapon attachment and grip contact. The guard-pose check evaluates hand contact and rifle support. These Blender checks do not replace a Unity play-mode test.

## Distinct arm and leg volume

`LIMB_VOLUME` controls radial thickness around upper-arm, forearm, thigh and shin axes independently of torso width. Tank and Brakk use the strongest muscle volume, assassin and Traveler are athletic, and the three women use slimmer arms and calves. A smooth envelope tapers the volume at joints. The same transformation fits bone-bound arm and leg armor; hands, feet and rigid weapons retain their dimensions.

`validate_limb_volume.py` compares actual median mesh radii with the previous `.blend1` backups generated by Blender. It checks thicker male limbs, slimmer female limbs and unchanged joint coordinates. Run immediately after rebuilding when those backups exist. FBX and guard-pose validation remain necessary for export and grip verification.

## Tahla Venn latest reference

The healer now uses pale blue skin and exposed slender arms, blue gripping hands, swept white hair with a small pink flower, long elven ears, ivory vestments and pointed engraved gold shoulders. The healing staff remains attached to the original grip and retains green restoration effects. This is a stylized interpretation of the new priestess reference.

## Assassin scout helmet reference

Zhaev Null now wears an original closed dark gunmetal scout helmet with rounded crown, recessed black eye apertures, angular cheek and chin panels, layered jaw rims and a hinged side antenna. The previous masks, glowing eye strips and exposed hair are removed. Helmet geometry is rigidly weighted to the existing Head bone; weapon grips, body proportions and locomotion are unchanged.

## Final thickness, shoulders and rounded feet

All seven anatomical arm cross-sections receive an additional 1.30 multiplier in their middle regions, tapering into shoulders and wrists. Bone lengths and grip positions are preserved. Brakk's shoulder plates, exosuit shells, cages and fins are reduced to half size around their mounting centers. Boots now use continuous rounded elliptical profiles with flat soles, curved toe caps and tapered ankles.
