# Mixamo activation investigation and Nerok presentation

User report: the resting pose still looked unchanged. The screenshot shows Unity stopped,
an empty scene and previous menu-hero logs; it does not show the current hero pose, the
checked-out Git revision, or whether StandingIdle.json was loaded. The local reproduction
has not been confirmed because Unity is unavailable in the execution environment.

Code paths that could hide the new pose have been addressed:

- StandingIdle previously loaded only after the attack bank and silently fell back when
  missing. It now loads independently with the canonical bone mapping and reports errors.
- Static animation caches now reset at SubsystemRegistration, including Enter Play Mode
  with Domain Reload disabled. Re-enabled initialized actors refresh their clip references.
- The final combat/idle pass is called directly at the end of AuthoredHeroPresentation's
  bone writes, instead of depending on two independently scheduled LateUpdate methods.
  The component's fallback LateUpdate only runs for actors without that presentation owner.
- Each affected actor logs `MIXAMO Standing Idle v2 ACTIVE` once. Use
  `DIMENSIONS FRONT > Checks > Live Mixamo pose` in Play Mode to inspect resource/rig
  state and actual shoulder/elbow angular deviation. An idle actor should report about
  zero degrees; action poses intentionally differ. Sivra is excluded; Auren's entire
  left shield arm remains on its established guard.

Nerok changes:

- The supplied water vortex is the background of the existing five icon illustrations.
  All foreground objects, materials, cameras, slot mappings and ability behavior are
  unchanged. The Blender builder alpha-composites the exact supplied image, darkened
  for readability, behind its existing foreground render. The editable scene packs it.
- Only the actual 3D anchor and thrown anchor use the new silver/rust material. Nerok's
  body materials and the icon anchor illustrations retain their original appearance.
- Rust is localized procedural oxidation. The held anchor uses its existing rest-coordinate
  UV channel so the marks move with its skinned mesh. The rigid projectile uses local
  coordinates. VFX-pool reuse restores the normal material for subsequent non-anchor effects.

Validation performed: C# syntax parsing; five Blender icon renders and visual inspection;
foreground builder unchanged; shader/material binding, VFX-pool restoration and pose-owner
source review. No Unity compilation, Android shader compilation or live runtime test was
available. The new live check is intended to distinguish a stale checkout, missing resource,
inactive rig, active combat action and a later bone writer on the user's actual machine.
