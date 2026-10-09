# Upper-body movement — 2026-10-09

## Cause and change

The final visible arm pose is owned by `MeleeBasicAttackAnimation`, including outside attacks. `AuthoredHeroPresentation` therefore skips the procedural arm rotations and rebases the arm bones; `ApplyGuard` then resets them to imported rest again. The earlier slow, time-driven shoulder swing never reaches the final visible arms. In idle, `ApplyGuard` also erased the supplied pelvis/spine/chest/head pose.

Add bounded locomotion offsets **after** that guard reset and **before** GuardWorld capture and attack blending. The existing fitted rest pose remains the base. Left and right shoulders follow opposite halves of the foot-contact cycle, elbows add forward flex, and chest/pelvis yaw counter-rotate. The head compensates torso yaw. Idle breathing stays small, fades during running and is retained by the final guard stage.

The body and final arm layers use the same smoothed travel direction and distance phase. A continuous forward/side blend avoids an arm flip at the transition into backpedalling. Keeping the last smoothed travel direction lets stop weighting settle the arms instead of snapping them to rest when instantaneous velocity becomes zero.

Arm gait fades for attacks, abilities, interruptions and forced movement. Sivra/Nyra's two rifle arms and Auren/Kaelon's left shield arm receive no new arm gait. Heavy anchor/hammer/staff hands use smaller swings. Existing grip transforms, attack clips, actor transforms, colliders, combat timing and movement values are not edited. The existing final ground-contact solve remains after the final combat pose.

## Executed checks

The portable runner in `Tools/Movement/UpperBodyRegression` compiled and executed the unchanged production upper-body sampler, full final combat-pose component and original full CombatMotion with Mono 6.8.0.105. It uses substitute engine components and System.Numerics math, not Unity.

- 1,536 final-pose evaluations: eight profiles × two rest fixtures × three travel directions × walk/run × sixteen phases.
- 2,880 one-degree steering samples across a full turn, checking bounded continuous shoulder changes.
- Repeated authored/direct frames, pause, zero locomotion weight, preserved idle body rotations, ability early return, action fade/recovery, fitted grip local transforms and shield/rifle exclusions.
- Actual final shoulder quaternions match the sampler. GuardWorld captures the new final gait. No direct actor/model or moving-leg writes occur in those evaluations.
- C# syntax parsing of all edited/new Unity scripts and inspection of the final writer order.

Final result: **PASS**, 261,846 assertions. The assertion count includes repeated bone checks; it does not represent that many independent gameplay scenarios.

The fixtures are the 19-bone native Nerok Blender source-rest hierarchy and a synthetic hierarchy with different local axes, with rotated parents and the existing imported nonuniform scale. Eight profiles were tested on those fixtures, not eight real imported FBX rigs. Actual rifle socket/aim solving, final weapon clipping, animation-bank strike curves and the full HeroVisualMotion/retarget writer are outside this isolated test. Its math/component substitutes may differ from Unity native behavior.

## Unity acceptance

Unity 6000.6.1f1 is unavailable here. No Unity project build, Play Mode, Android build or rendered in-game acceptance was executed.

Two editor checks were added under `DIMENSIONS FRONT/Checks`: `Upper body gait curves` and `Record selected hero upper body for 10 seconds`. For the recording, select a gameplay hero in Play Mode and run continuously without attacking. It measures the final visible Chest/UpperArm bones through a full gait cycle and fails if another writer makes the chest/eligible arms rigid.

Then inspect every hero forward/backward/sideways, starting/stopping, changing direction and moving during attacks/abilities. Verify head balance, hand/weapon clearance, Sivra's two contacts, Auren's left shield and the existing planted foot targets. Repeat for local and remote co-op heroes. These engine checks are still required to judge visual quality and phone performance.
