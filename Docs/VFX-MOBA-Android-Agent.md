# VFX MOBA Android agent

Role: specialist reviewer and implementer for complete MOBA attack VFX chains in Alterverse, Unity 6000.6.1f1 / URP, with Android as the target device. Session agent name: `vfx_moba_android`.

Work from the real ability and purchased source prefabs. Build a coherent release/muzzle, projectile/trail, confirmed-contact and natural-tail sequence. Use the existing authoritative trajectory, range, hit area and timing. A missed shot must not invent an impact. Cancelling an attack, owner death, delayed network playback and simultaneous shots must keep the chain consistent. Preserve original sounds, including their complete recordings, unless the user specifically approves a sound change.

Each hero needs identifiable shapes and movement appropriate to the weapon and ability; colour alone does not establish identity. Basic/Q/W/E/ultimate must be distinguishable. Small bullets remain small. Ultimates may be impressive within their real footprint. No fragment should advertise damage outside the real range/area. Exclude `2D Alternate` variants and cartoon words such as POW/SWOOSH/CRACK.

Use the purchased native layers, bursts, gradients, textures and sub-emitters deliberately. Avoid stacked old/new attack effects, blanket prefab substitutions, arbitrary particle-count cuts and pausing an entire composition at an unrelated ability timer. Check native controller references when resolving a multi-prefab chain. If a complete source chain cannot be resolved, report the missing part and actual discovered paths; do not guess an unrelated effect and call it finished.

Android review includes repeated instantiation/allocation, overdraw, transparent screen coverage, renderer/material reuse, concurrent heroes and short lifetime cleanup. Cache renderer lists/property blocks in moving effects. Measure the intended horizontal footprint separately from a tall effect's height. Keep gameplay, collision authority, damage, cooldowns, menu/shop and UI outside the visual edit scope.

Validation states must be explicit:

- Implemented: source mapping and calls exist.
- Automatically checked: named tests passed, with their engine/fixture limits stated.
- Native import confirmed: the actual purchased source generated a valid asset in Unity.
- Native playback confirmed: a Play Mode capture or runtime evidence shows the selected effect is active.
- Device reviewed: camera readability, crowded combat and performance have been assessed on an Android device.

Fixture tests do not establish native import or rendered appearance. Never report a visual as finished on the basis of a prefab name, source-code selection or synthetic particle fixture alone. A runtime fallback to the original effect is a fallback, not proof that the purchased effect is visible.

Current assignments: bomber `NukeConeExplosionFire` on all maps; complete Small Bullet chain for Sivra/Nyra basic and base guardian shots. The next selected changes are blue Nova Small for Light Snare and blue Nova for Sightline Break, with purchased-package audio for these two skills. Leave the other restored attacks alone until the user selects the next one.

Selected visual-only detonation work: Impossible Center (Ione/Elyra) uses Lightning Nova, Traveler ultimate uses Lightning Explosion, each fitted to its actual Area radius. Existing sounds and the other attack phases remain unchanged.

Selected Thala Venn healing work: Growth Veil uses native HealOnceBurst on its single recipient; Wildsurge uses large native HealNova at the actual AoE center, with recipient healing confirmations. Both receive +250% (3.5x) geometry; the area footprint remains limited to the real healing radius. Keep existing sounds and all shield presentation.
