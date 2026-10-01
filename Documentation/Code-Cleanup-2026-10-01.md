# Runtime code review and cleanup

Base: `565e1d5d8fd17f2a7f29360e0499f45d09847131` on `dev/vertical-slice`.

## Confirmed defects and changes

- Two automatically installed material repair systems scanned the scene. `RuntimeShaderRescue` also mutated shared materials in place, did not migrate legacy texture properties, and did not own missing-slot replacement materials. Removed it and retained one `AndroidMaterialCompatibilityGuard` for all URP platforms. The retained guard now also handles unsupported shaders, preserves texture tiling/offset and legacy glossiness, and owns its replacement copies through `RuntimeMaterialPalette`. Valid materials are untouched.
- `AllFigureMotionRealism` still installed a whole-scene movement scanner. Production minions and standard guards already use `LaneUnitVisualMotion`; heroes use `HeroVisualMotion`; new exoplanet guards use `PlanetSentinelVisual`. Removed the obsolete scanner/controller and its obsolete hero conflict check. Diagnostic primitive minions no longer receive its decorative fallback motion.
- Five retired Epic/Legendary weapon systems still installed scene scanners. The legendary systems interpreted colorway 1 as a replacement-weapon selection, created extra models, and relied on a later colorway pass to hide them. Removed those five systems, the signature-weapon partial builder, and the obsolete overdrive helper chain in `StylizedHeroVisual`. Standard weapons are now built directly for both shop variants. Removed recurring replacement-geometry suppression and obsolete signature-grip adjustments. Inventory keys and purchase migration logic are unchanged.
- `HeroSurface` uses `_BaseColor.a` as a hit-flash override, while the shop colorway code preserved its default zero alpha. That made its persistent tint ineffective on vertex-painted weapon surfaces. Added a separate `_WeaponTint` channel, preserving vertex brightness detail and the energy mask. Hit flashes still override the result independently. Other weapon shaders retain their existing material tint path.

## Verification executed

- Loaded all 363 original C# sources for source/reference review. This is not a claim that every gameplay path was exercised.
- After removal of eight C# files and addition of one regression helper, all 356 remaining C# sources pass tree-sitter syntax parsing. The pre-existing contextual identifier `required` in `AndroidDeviceCompatibilityPostprocessor` is normalized only in parser input.
- No remaining C# references to the removed runtime types or removed weapon-builder entry points.
- Checked all 16 tracked serialized scene/material/asset files under Assets for the eight removed script GUIDs: no references.
- Numeric shader checks: zero tint preserves authored vertex colors; active tint changes them; full hit flash still overrides tint.

## Unity verification still required

Unity Editor, C# semantic compilation, shader compilation and an Android device are unavailable here. Added `DIMENSIONS FRONT/Checks/Runtime cleanup (Play Mode)` for material migration/idempotence and seven-hero colorway/geometry/restoration checks; it has not been executed here.

In Unity, run that check, switch all seven heroes between Original and both shop colors, inspect attack flashes, and verify both exoplanet guards. Repeat a match/menu cycle and profile materials, meshes and frame time on Android. This cleanup does not establish the cause of the reported Android darkness or guarantee that the whole game is bug-free.

Backup scenes, ProjectSettings.zip, save compatibility APIs, and editor diagnostics were not removed merely because they looked old.
