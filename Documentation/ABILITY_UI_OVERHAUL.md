# DIMENSIONS FRONT — ability button art pass

Implemented from branch `dev/vertical-slice`, starting at `b649ac5c5079fe3c951f5e62e1eaacf4b03ba96e`.

## Changes

- `Assets/Alterverse/Scripts/UI/PrototypeHUD.cs`: replaces the five immediate-mode glyph buttons with a retained UI presentation. Input, hitboxes, release-to-cast, drag-out cancellation and targeting preview remain intact. Canvas visibility follows HUD lifecycle, death, pause, focus and suspension.
- `Assets/Alterverse/Scripts/UI/AbilityButtonView.cs`: five cached RawImages, five cached cooldown labels, one ultimate caption and one screen-space Canvas. No raycaster, Button, EventSystem or input interception. Explicit mapping of the six existing internal hero IDs to the 30 atlas regions. Pixel-calibrated UVs account for nonuniform source spacing.
- `Assets/Alterverse/Shaders/AbilityButton.shader`: circular alpha containment, grayscale unavailable state, clockwise remaining-cooldown sector, pressed flash and held pulse. Ultimate has a complete separate double/segmented frame and four bounded energy accents; neighboring atlas ornaments cannot bleed into its frame.
- `Assets/Alterverse/Resources/CombatUI/AbilityAtlas.png`: generated illustrated texture asset, 1145 × 1374, 30 distinct motifs; not runtime line art. Source pixels are unmodified after image generation. Cropping happens through UV sampling.
- `Assets/Alterverse/Resources/CombatUI/AbilityButton.mat`: serialized shader/texture references keep these assets included in builds. Unity metadata accompanies every new asset and folder.
- `Assets/Alterverse/Scripts/Combat/BasicAttack.cs`: adds only `RemainingCooldown` and `AttackCooldown` read-only accessors. `TryAttack` is unchanged.
- `Documentation/ability-ui-preview.png`: CPU reference rendering of the actual atlas crops and shader formulas at HUD sizes. This is **not** a Unity screenshot or proof of shader compilation.

No edits to balance, abilities, movement, AI, targeting rules, match rules, hit areas or `CombatGlyphs.cs`. The latter remains available for technical lines and the joystick, but is no longer used to draw the five ability icons.

## 30-icon map

Atlas column order is Basic, Q, W, E, Ultimate. HUD slots 0/1/2/3 map to columns 1/2/3/4; attack slot 4 maps to column 0.

| Hero / internal ID | Basic | Q | W | E | Ultimate |
| --- | --- | --- | --- | --- | --- |
| Zhaev / Veyr | Dimensional blade | Forceful slash | Jagged rift | Portal dash | Blade/core finisher |
| Auren / Kaelon | Hammer | Ground strike | Shield | Gravity anchor | Team citadel |
| Sivra / Nyra | Rifle projectile | Crosshair shot | Directional dash | Bound energy target | Four shots |
| Ione / Elyra | Orb | Fractured orbit | Inward vortex | Paired portals | Singularity |
| Tahla / Mira | Support projectile | Seed | Healing plus | Protective shield | Team heal |
| Brakk / Rook | Gauntlet | Swing | Armored gauntlet | Ground slam | Charged twin gauntlets |

Hero accents: violet/magenta, amber, cyan, dimensional violet/blue, mint green, orange/red respectively.

## States

Ready retains full illustrated color. Pressed briefly shrinks and flashes over 160 ms. Held/targeting adds a pulsing ring. Cooldown leaves the art recognizable beneath a dark clockwise sector and a bold, outlined remaining-seconds label. The number rounds upward so a live cooldown never reads zero. Unavailable desaturates the icon when stunned or when its gameplay component is absent. Target absence is not predicted; existing gameplay feedback still reports an unsuccessful cast.

The basic attack uses its real cooldown, not a guessed animation timer. The cooldown sector denominator comes from the existing gameplay accessor. No cooldown values are changed. Ultimate receives 84 layout units instead of the previous reduced 64-unit face; the 84-unit hit rectangle remains unchanged. Regular abilities use 76 units; attack uses 98.

## Performance

One shared atlas and template, five material instances created once per HUD, cached shader property IDs, cached labels and 0–600 second strings. No runtime texture generation, RenderTextures, per-frame material creation or frame-string formatting in the new view. RectTransforms resize only when the safe area changes; text meshes change only when the displayed second changes. Materials and the canvas are disposed with their HUD. Import settings disable mipmaps/CPU readback and use Android ASTC 4×4. Verify import and GPU support on actual target devices; 60 FPS has not been measured here.

## Validation actually performed

- Parsed all five local C# files with a C# syntax parser: no syntax errors. This is **not** a Unity compiler check.
- Compared existing touch, mouse, release/cancel, preview and basic-attack execution methods against their original sources: unchanged.
- Verified `CombatHudLayout.cs` is unchanged and the HUD no longer calls `CombatGlyphs.Draw`.
- Verified 30 unique in-bounds atlas UV regions and inspected the reference rendering at 76/84/98-pixel HUD sizes and all five states.
- Checked five resolution/safe-area fixtures, including 16:9, a wide landscape with side insets, and portrait: action rectangles contained and nonoverlapping.
- Checked unique asset GUIDs and material shader/texture references.

## Still required in Unity / on Android

No Unity editor or Android runtime was available in this environment. Unity C# compilation, shader compilation, actual URP color-space appearance, import results and runtime exceptions are **unverified**. So are multi-touch execution, font rendering, pause/resume/respawn, focus changes, screen rotation and on-device performance.

Pull the branch, allow assets to import, and run one match with each hero. Confirm Basic/Q/W/E/Ultimate, press/release, drag-to-cancel, held targeting, cooldown completion, stunned/unavailable, death/respawn, pause/resume and safe-area placement. Check the Unity console and Android profiler. The visual definition of done requires this live-game acceptance pass; the source-level and asset work alone does not establish release readiness.

## Art provenance / generation instructions

Created with the built-in Imagegen tool using the user's original “Dimensions Front: Heldenfähigkeiten im HUD” concept as the style reference. A first atlas generation specified the complete 30-motif map above, five columns/six rows, dark circular faces, hero-colored rims, large readable illustrated silhouettes, no text and no portraits. A second edit preserved all motifs/colors and requested full frame margins and corrected Brakk W to a mechanical gauntlet in front of armor plates with a small plus. Generated spacing was still nonuniform, so the runtime uses inspected pixel crops; the ultimate frame is supplied independently in the shader to avoid source-edge clipping. No external stock icon pack was used.
