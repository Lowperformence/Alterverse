# Ione and Traveler: selected lightning detonations

| Ability | Purchased source | Replacement trigger |
| --- | --- | --- |
| Ione Vaska / Elyra: Impossible Center | `Combat/Nova/Lightning/NovaLightningPurple.prefab` | Slot 3, resolved Area cue only |
| THE TRAVELER: Paradox Collapse | `Combat/Explosions/LightningExplosion/LightningExplosionYellow.prefab` | Slot 3, resolved Area cue only |

Both source paths are relative to `Assets/Epic Toon FX/Prefabs/`. The complete authored particle/mesh composition supplies each central detonation. No 2D Alternate variant is used. Source AudioSources, demo scripts and colliders are removed from the generated copies; the existing game audio remains the only sound path for these abilities.

The hook runs before the old central `PolishArea` and area-resolution effects. A successful native detonation replaces that old central stack. Warnings, Ione's falling cast visual, Traveler's travelling sphere, individual victim feedback and semantic network events retain their existing paths.

The runtime uses the radius received from the actual Area cue (`AimingSettings` / `areaAim.aoeRadius`), not a decorative fixed size. Both current defaults are 2.5 units; runtime overrides are respected. Native horizontal bounds are fitted to that radius, and visible fragments are clipped to the same world-space circle. Radius is not confused with diameter and no multiplier from the accepted Nuke, Small Bullet or Sivra effects is inherited. The ground height matches the existing warning/detonation ground plane.

Preparation is isolated to `Resources/CombatVfx/UltimateLightning`. It reuses the existing bounded visible-sampling and material adapter helpers. Autoplay emission stops at its measured schedule without clearing live particles or death-triggered sub-emitters. Delayed network playback seeks the effect instead of restarting a completed explosion. Missing resources retain the former central effect with an explicit warning; the importer reports any missing selected source path.

Damage, targeting, range, slow, cast/detonation timing, warning radius, audio routing and original audio assets are unchanged. Other attacks, accepted effect sizes, menu/shop and shield HUD are outside this edit scope.

Validation: 119 focused checks pass with controlled engine/editor fixtures for source selection, colour separation, source-audio exclusion, exact supplied radius, fragment clipping, one native composition per cue, late playback and cleanup. Existing combat/VFX and skill-entry checks also cover the unimported fallback. These checks do not render the actual licensed prefabs or establish Android appearance/performance; the native import and the two resolved blasts still require review in Unity.
