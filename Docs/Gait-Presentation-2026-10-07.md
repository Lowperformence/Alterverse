# Directional gait and contact continuity

Applies to the shared HeroVisualMotion used by authored and procedural hero rigs.
The existing class guards, weapon grips, slower distance-driven step frequency,
combat pose ownership and relocation reset remain in place.

## Changes

- Blend the world-space direction of actual travel, then express it relative to
  the actor's current facing. Backpedalling reverses hip swing and strafing adds
  bounded lateral hip movement with ankle compensation. Knees retain their flexion axis.
- Fade the gait faster when actual travel stops, including blocked wall movement.
- Use mostly linear ground-contact hip travel to reduce easing-related foot sliding.
- Remove duplicate weight multiplication from ankle compensation during blended movement.
- Match knee and ankle endpoints at toe-off and landing. Normalize SmoothStep inputs
  at the late contact and landing transitions to eliminate internal angle jumps.

Only HeroVisualMotion runtime code changes. HeroMotor, HeroLocomotion, damage, ability
timings, collision shapes, movement values, input, camera and network state code are
unchanged. No new physics queries, per-frame allocations, components, root motion or
animation assets are introduced. Direction is derived from observed displacement,
including remote replicas; network packets and authority are unchanged.

This reduces gait artifacts; it does not provide exact world-space foot locking or
terrain IK. Existing body bob and weapon-release presentation continue to work as before.

## Validation performed

- C# syntax parsing of the eight inspected source files, including the new editor check.
- Translated the actual EvaluateRunLeg syntax tree into an executable numerical evaluator:
  90,000 samples across both rig conventions, five weights, three contact fractions and
  three run blends. Checked all eight piecewise seams, joint bounds, zero-weight rest
  and loaded sole compensation. Largest seam delta at +/- 0.000001 cycle: 0.000872 degrees.
- Direction convergence and stop-weight fading at 15, 30, 60 and 120 FPS.
- Re-read the current branch before publishing; the modified source file still matches
  its reviewed baseline. Commit is based on the latest branch tree to preserve other work.

## Unity acceptance still required

No Unity editor/compiler or Android device is available in this environment. Numerical
and syntax checks are not a Unity build, live physics test, visual acceptance or profiler run.

Run `DIMENSIONS FRONT > Checks > Gait contact continuity` in Unity to execute the actual
C# evaluator. Run the existing movement collision/dash check in a running match.
Check all eight hero identities in forward/diagonal/side/backward movement, sharp reversal,
wall stop, idle recovery, simultaneous attacks/casts, dash, hit reaction, death/respawn,
recall and pause. Inspect Auren's shield/hammer, Zhaev's blade grips, Brakk's fists,
Nerok's anchor, and Sivra's two-hand rifle hold. Test a remote client and profile Android
frame time/GC. Full visual and performance acceptance has not yet been established.
