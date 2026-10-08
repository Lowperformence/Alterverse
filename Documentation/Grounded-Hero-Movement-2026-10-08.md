# Grounded hero movement — 2026-10-08

## Confirmed causes

`HeroVisualMotion` multiplied every distance-driven gait cycle by `0.39`. The actor covered a full stride while its animation covered only 39% of a cycle. Its leg rotations also had no persistent world-space foot contact. Changing hip/knee angles alone could not prevent the support foot sliding with the moving actor.

On co-op clients, remote gameplay roots are assigned host snapshots at 12 Hz. Measuring those raw transforms at render frequency produced speed spikes followed by zero-speed frames. Host and local-owner motion have different authority and must keep their existing control behavior.

The visible imported skeleton receives its final combat pose in `AuthoredHeroPresentation`, after the renderer-free procedural controls. A correction applied only to the controls or before the combat pose would be overwritten.

## Implementation

- Remove the `0.39` factor. Measure each visible leg and use its length to choose a walk/run cycle stride. Cycle phase still advances with traveled distance, so slows and buffs change cadence naturally.
- Add `HeroGroundedLocomotion` automatically when the existing motion controller starts. Solve the actual `Thigh/Shin/Foot` chains; use the procedural chains only for legacy visuals. During stance the ankle target remains at a world-space anchor. During swing it travels toward the next contact with positive clearance, knee flexion and ankle roll. Start/stop retain the current contact state and ease stride amplitude/landing.
- Run this solve synchronously after the final authored combat pose. Permit a limited visual pelvis lowering for reachable ground targets. Restore that correction before the next pose; never alter the actor transform, controller, movement speed or damage/cooldown code.
- Capture sole points once from baked, foot-weighted mesh vertices across all skins. Compensate ankle height from the rotated sole extent so toe-off cannot rotate the boot through the floor; remove that correction before the next target plan to avoid accumulation. Ground rays ignore triggers, the actor's own colliders and other characters. Preserve the model's initial ground plane when no ground collider is available.
- Buffer remote presentation snapshots and interpolate the visual root with a 100 ms delay. The co-op actor, collision root, ownership logic and network payload stay unchanged. Gait measurements use the interpolated pose. Packet loss holds the last known pose; it does not extrapolate movement.
- Release contacts for death, relocation, respawn and forced movement/dash. Remote unusually fast displacement also releases contacts because the existing snapshot payload does not transmit a dedicated dash flag. Forced-motion presentation expires after a 250 ms heartbeat, including the render delay, so lost packets cannot disable grounding indefinitely.

## External animation research

The requested research agent inspected these primary sources:

- [Quaternius Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html): CC0, free standard edition, Unity-compatible humanoid animation, directional locomotion. [Version 3 update](https://quaternius.itch.io/universal-animation-library/devlog/1555893/added-root-motion-and-other-fixes) describes root-motion `_RM` variants and synchronized left-foot starts for locomotion.
- [KayKit Character Animations](https://kaylousberg.itch.io/kaykit-character-animations): free CC0 FBX/glTF locomotion, including walk/run, for the provided medium/large rigs.
- [Unity Blend Trees](https://docs.unity3d.com/6000.0/Documentation/Manual/class-BlendTree.html), [Root Motion](https://docs.unity3d.com/6000.0/Documentation/Manual/RootMotion.html) and [BakeMesh](https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.BakeMesh.html).

No external animation clip was imported in this change. The active imported Animator is deliberately disabled by the project's final pose system. A future clip import should retarget into that system, use in-place locomotion with distance-matched playback and preserve the final foot-contact layer. Enabling an additional Animator would introduce a second writer and would not fix contact drift by itself.

## Verification and remaining acceptance work

Completed here: C# syntax parsing of changed scripts; independent agent review of active writers, visible chain binding, scale, start/stop, death/dash and snapshot lifecycle. A synthetic transform simulation of the existing two-bone solve under imported scale covered 180 cases with a maximum 3.757 mm endpoint error after two passes. These are source/math checks, not a Unity build or a visual acceptance test.

Added editor checks under `DIMENSIONS FRONT/Checks`:

1. `Movement snapshot interpolation` exercises the production buffer with 12 Hz packets at 60/120 fps, yaw, dropped packets, respawn and duplicate timestamps.
2. `Movement live foot contacts` checks active visible chains, stance endpoint error below 35 mm and unchanged actor transform in Play Mode. Run it during movement and while stopped.

Unity 6000.6.1f1 is not installed in the execution environment. Neither editor check, a Unity compile nor an Android build has been executed here. Before accepting the appearance, inspect every playable hero from the side and at normal gameplay distance: forward, backward, strafing, turning, start/stop, walking into a wall, speed changes, movement while attacking, dash, death and respawn. On a co-op client inspect another human and a bot as well as the local owner. Confirm the standing foot stays fixed during stance, the swing foot lifts, knees bend without inversion, soles clear the ground and weapons/grips remain correct. Measure phone performance because the new ground queries run once per foot per visible hero per frame.
