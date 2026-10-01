# Authored heroes — first Blender production pass

Seven original DIMENSIONS FRONT hero costumes over Blender's CC0 Human Base Meshes 1.4.1 anatomy. The models are a first authored pass; they have not been certified as matching the reference's final art quality.

## Contents

- `Assets/Alterverse/Resources/AuthoredHeroes/*.fbx`: one weighted mesh per hero, 17 bones, nine baked animation clips (Idle, Run, Attack, Skill0–2, Ultimate, Hit, Death).
- `ArtSource/Heroes/*.blend`: editable Blender 4.5 LTS sources, materials, rig, actions and reproducible studio scene.
- `ArtSource/Heroes/Preview/`: actual Blender renders, not in-game screenshots.
- `Tools/HeroRebuild/`: reproducible generation and FBX round-trip validation.

Unity's `AuthoredHeroImporter` imports Generic rigs, looping Idle/Run clips and URP materials, then generates resource controllers in `AuthoredHeroes/Controllers`. Open the project and let imports finish before making a player build. The presentation uses the existing HeroVisualMotion API as a bridge; the procedural pose controller does not run when an authored rig is loaded. Existing movement, combat, buttons and collision shapes are unchanged.

The existing procedural assets remain a fallback if a model or controller is missing. The established visual-root coordinate frame is preserved for owned wings and founder cosmetics.

## VFX

Adds feathered projectile streaks, twin arc ribbons and rising green healing leaves through the shared pool and decorative budget. Uses actual authoritative hit callbacks for impacts. Rift Scar's purple traveling X remains unchanged. Attack readability cues use the authored weapon socket. Optional details never consume the pool's warning/core reserve.

## Rebuild

Install free Blender 4.5 LTS. Download the Human Base Meshes 1.4.1 bundle from https://www.blender.org/download/demo-files/ and point `DF_HUMAN_BASE_MESH_SOURCE` at `human_base_meshes_bundle.blend`.

```
blender --background --python-exit-code 1 --python Tools/HeroRebuild/build_heroes.py
blender --background --python-exit-code 1 --python Tools/HeroRebuild/validate_fbx.py
```

## Validation and remaining review

All seven FBXs were re-imported in Blender: one armature and mesh, 17 bones, nine animation clips, UV layer and all vertices weighted. C# syntax was checked with tree-sitter. Unity compilation, player rendering, controller import and Android GPU validation have not been run in this environment. Review animation posing, VFX timing, cosmetics, skin palettes and material colors in Unity before a release build. No claim of a Unity-tested or final AAA result is made.

## Source license

The Human Base Meshes bundle's included README states: “All provided assets are public domain under the CC0 license.” Anatomy contributors include Blender Studio/community; outfits, accessories, weapons, rig and generator modifications are original to this project. The bundle also contains an unrelated legacy Rain Rig attribution text; no Rain Rig is used here.
