# DIMENSIONS FRONT — frontend and character presentation

## Source baseline

Reviewed branch `dev/vertical-slice` at `615cb6cc95581758225a1659219004f5d9d81590`.
The newer lunar-ground and objective work remains untouched. Hero balance, abilities,
respawn/AI fixes, progression, wallet, inventory and reward services are retained.
The separate unfinished combat-HUD working copy is not part of this change.

The previous frontend had duplicated home status/selection text, a moving full-screen
neon split, 38-unit navigation targets, global GUI.skin changes, an XP fill drawn at a
different y-coordinate from its track, no explicit native name keyboard, repeated
PlayerPrefs.Save calls while dragging audio, and an abstract 2D hero card unrelated to
the actual model. Its footer style was constructed before its source style existed.

## Implemented frontend

- Neutral slate surfaces, consistent text hierarchy and restrained selection accents.
  One dominant action per screen; no fake online queue or loading percentage.
- Home: selected in-game hero, profile, account XP, shards and PLAY. Navigation is
  shared across Home, Play, Heroes, Cosmetics, Profile and Settings.
- Deployment: accurately states one human plus four allied bots versus five enemy
  bots, local play and ZERO deployment (matching PrototypeBootstrap). START AI MATCH
  preserves the existing initial-start/restart and return-home contract.
- Six named hero rows with role and selected state, actual model presentation, role
  guidance and DEPLOY. Selection still writes PlayerHeroSelection.
- Cosmetics uses the existing validated AvatarShopService, affordability checks,
  owned/equipped state and wallet. No real-money purchasing is enabled. Existing
  founder/recall ownership is preserved and displayed.
- Profile retains saved name validation, lifetime record, XP, shards, six built-in
  avatars, owned founder avatar and access to purchased avatars through Cosmetics.
  Android uses the native text keyboard; desktop retains an editable text field.
- Audio has larger drag targets and +/- controls. Existing audio consumers see new
  values immediately; one preferences flush occurs on exit, pause or disable.
- Safe-area origin handles Unity's bottom-left coordinates. Content adapts to width;
  controls remain in a centered bounded layout on wider devices. Android autorotation
  allows either landscape side and excludes portrait, matching this two-thumb game.
- Escape returns home/cancels editing or deployment; losing focus during the short
  launch transition cancels it instead of starting a match in the background.

## Model presentation and cost

MenuHeroShowcase constructs one visual-only actor using HeroIdentity and the existing
StylizedHeroVisual. No Health, TeamMember, collider, ability or input component is added.
It reuses the gameplay geometry rather than drawing a different concept character.
A 640x720 RenderTexture, two unshadowed local lights and one orthographic camera render
on the previously unused layer 31, at an isolated location. No project lighting or
main-camera mask is changed. Preview camera/lights are inactive outside the three
hero pages and throughout gameplay. The texture is retained for returning to the
menu and released on destruction; changing hero disposes its previous visual actor.
Rifle-bearing Sivra uses a more oblique pose; mage prisms and support fins animate on
unscaled menu time. No per-frame mesh/material creation occurs.

Character surface changes are a separate commit: authored contour geometry receives
front/back bevels and outward normals. Cloth, armor and functional light strips use
separate material responses. Small emissive surfaces have their own palette key so
ZERO energy does not turn Zhaev's entire violet mantle emissive. Static batching and
existing animated-part names/silhouettes remain intact. No combat stats change.

## Checks actually run

- Tree-sitter C# syntax parsing: all 44 audited source files passed.
- `git diff --check`: passed.
- Numerical safe-area checks: 1280x720, 1920x1080, notched 2400x1080,
  notched 2992x1344, 2048x1536 tablet, and 1600x720. Navigation, roster and
  seven-founder-avatar action rectangles fit without mutual overlap.
- Generated bevel face directions: all 22 source contour outlines passed outward
  normal checks for front, back, side and bevel surfaces.

These are source/numerical checks, not rendered UI screenshots or Unity compilation.
No Unity editor or Android device is available in this environment. The procedural
characters are improved, but this does not certify final production art or 60 FPS.

## Required Unity/device acceptance

1. Compile in Unity 6000.6.1f1; launch Home, open every page and select all six heroes.
   Inspect portrait exposure, silhouette cropping, materials and lack of world leaks.
2. Start a first match; complete/leave it, return home, change hero and launch again.
   Verify correct hero/team and no preview camera or actor counted in the match.
3. Change/cancel/invalid-name/native-keyboard flows; suspend/resume during editing and
   deployment. Check persisted names, avatar, shards, XP and record after restart.
4. Purchase affordable/unaffordable/already-owned avatars with development balances;
   equip built-in, founder and purchased avatars without additional charges.
5. Drag/tap all audio controls; confirm immediate audible updates and retained levels.
6. Inspect both landscape orientations, notches and 4:3 tablets on real screens.
   Verify physical touch sizes and text readability; geometry checks cannot establish
   DPI comfort. Profile CPU/GPU time, allocations and repeated showcase lifecycle.
7. Run the existing Hero overhaul regression in Play mode; compare gameplay-camera
   screenshots of all six heroes on both teams. Material/renderer budgets require
   runtime verification, particularly after the separate emissive palette change.
