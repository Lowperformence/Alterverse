# Menu Canvas upgrade

The main menu now renders its shared panels, navigation, labels, progress bars, card artwork,
avatars, hero previews and inventory grids with a pooled screen-space Canvas. Existing IMGUI
controls continue to process purchases, navigation, mouse/touch drag capture and text editing.
Canvas graphics do not receive input or require a second EventSystem. Gameplay HUD controls
retain their existing input path.

## Installed tools

- DOTween 1.3.030, the official free distribution in `Assets/Plugins/Demigiant/DOTween`.
  Unscaled page fades and action feedback keep working while the menu pauses the match.
  Original files, copyright notices and `readme.txt` are included.
- UIEffect 5.11.7: glass panel gradients and slow light sweeps on selected/primary controls.
- UI Particle 4.14.1: a single bounded emitter around the selected hero, at most 40 particles.
  A private fixed seed leaves gameplay randomness untouched. It stops when the menu hides.
- SoftMaskForUGUI 3.6.5: rounded hero previews and home-card clipping in AntiAliasing mode.
  Inventory scroll views use rectangular clipping. No render-texture soft masks are allocated.

The three Coffee packages use verified release tags and commit hashes in both the manifest
and lock file. Their MIT licenses are delivered by their packages. Archive SHA-256 digests
are recorded in `UIAddonDownloads.json`.

## Rendering and lifecycle

Only Repaint updates the Canvas. Layout/input passes retain the original controls and do
not allocate duplicate graphics. The same GUI-to-screen transform positions both visual
and input rectangles, including safe areas, scaled layouts, nested groups and scrolling.
Page transitions change opacity only; control positions stay fixed. Canvas slots are reused
on stable pages and retired safely when their primitive type changes. Hidden pages release
active emitters and menu shutdown kills its tweens. Runtime-created textures and materials
are destroyed with the renderer.

The Canvas uses pixel-perfect alignment. Labels round their absolute screen bounds and
render glyphs at integer screen sizes with an 11-pixel readability floor. The existing
theme handles fitting; the Canvas no longer performs a second automatic best-fit shrink.
Rajdhani body/display imports use HintedSmooth for clearer small glyph strokes.
All three particle velocity axes explicitly use TwoConstants, including a zero Z range,
preventing Unity's "Particle Velocity curves must all be in the same mode" simulation error.

`Resources/FrontEnd/MenuUiVariants.shadervariants` retains the 20 required gradient/shiny,
clipping and particle shader combinations in Android players. `MenuCanvasChecks` validates
the shader assets before builds and is available in **DIMENSIONS FRONT → Checks → Menu
Canvas assets**.

## Unity Console

Rig initialization and standing-pose success traces now use `HeroVisualDiagnostics`. They
are compiled out by default, including their string formatting. Define
`DF_VERBOSE_HERO_DIAGNOSTICS` only when investigating hero rigs. Existing warnings and
errors keep their normal output. This suppresses the informational animation spam seen
in the Unity Console without hiding failed imports, missing bones or shader errors.

## Verification scope

The isolated C# regressions exercise production aiming/network code, motor/locomotion,
Health shield rules, fitted shield orientation and Canvas geometry/pooling/lifecycle with
substitute engine components. They are not a Unity build, rendered preview or Android test.
The Canvas suite now checks fractional screen positions, the small-text floor, and the
velocity curve-mode invariant. Its substitutes do not rasterize fonts or run native particles.

The remaining native visual check is to open the project in Unity, run the menu asset check,
then exercise Home, Play, Heroes, Store, Inventory, Profile, Settings and Co-op at 16:9 and
wide Android safe areas. Check the profile-name and lobby-code keyboards, inventory swipes,
disabled/locked purchases, page return after a match and Auren's actual skinned shield.
