# DIMENSIONS FRONT — movement and locomotion pass

Baseline: `8b340bc35cf736b2ae3c0bf7ae5c0c48e433f8c6`, dev/vertical-slice.

## Audit and ownership

Current player uses CharacterController, camera-relative WASD and EnhancedTouch input
from PrototypeHUD. Bots use BotLanePlan/target sensing/brain state and transform-driven
movement; there is no NavMesh/Rigidbody character locomotion. HeroAbilityExecution is
the live player/bot ability path. Its three movement skills previously disabled the
controller and teleported to an endpoint. BaseRecall intentionally remains a teleport.
VeyrAbilities and PrototypeEnemyBot are legacy paths, not instantiated by the current
5v5 bootstrap; their unused teleport behavior was not redesigned.

Observed issues: no input/motor execution ordering, full-left-screen joystick capture,
no radial dead zone, hard attack-facing snaps and competing bot rotation writers,
frame-dependent bot interpolation, no bot static-world collision, abrupt dash endpoint
teleports, camera follow yaw feeding back into movement, and time-driven gait cycles
with repeated Transform.Find calls and displaced blade poses that did not reset.

## Input and movement

- Touch processing runs at -200, dash body at -50, motors at -25, ordinary combat at 0.
  Frame-stamped input cannot persist after its producer stops. Keyboard remains a fallback.
- Lower-left safe-area capture, stable finger ownership, 8% radial dead zone with
  continuous remapping, unit magnitude cap, separately bounded visual joystick center.
  Ending/canceling/missing touch, resize, death, pause and focus loss release input.
- Player direction uses an orthonormal planar camera basis. No acceleration delay or
  release inertia is added; visible weight belongs to presentation, not position.
- HeroLocomotion owns facing, using a bounded 1080 degrees/second turn. Moving/dashing
  direction wins; attack facing applies at rest and expires. Damage/target acquisition
  is not delayed until the visual turn completes. No movement locks added to attacks.
- Existing HeroCombatStatus supplies stun/slow, and PlanetBuffReceiver supplies the
  existing speed modifier. Receivers are initialized once instead of repeatedly
  searching for an absent component. Hero stats/specs remain byte-identical.
- HeroLocomotion provides bounded displacement for both player and bots. Nonalloc
  capsule sweeps ignore triggers/self/floor faces; wall tangent motion uses at most two
  contact planes. Players retain CharacterController collision; bots retain their
  crowd-separation rules instead of introducing a new dynamic-body pushing system.
  Query saturation stops conservatively. Controller step-climbing is disabled.
- Existing player bounds (41/35), bot bounds (40/33) and dash bounds (40/33) are retained.
  No lunar geometry, map art, damage, speed, role, cooldown or attack range changes.

## Dashes: explicit behavior change

Zhaev's dash and Sivra/Ione's evades now traverse their existing range over **0.16 s**
in open space. This replaces instantaneous displacement; it is a deliberate movement
quality change and requires matchup/playtesting. No invulnerability is introduced.
Geometry can shorten the distance; a fully blocked start is rejected without consuming
cooldown. The initial VFX endpoint is limited by the same sweep. Each moving frame
rechecks collision, and stun/death/disable/pause/focus loss cancel remaining travel.
Slows continue to affect walking, not the dash's defined range/time. A dash owns its
completion frame to prevent an extra walking step. Normal attacks and existing ability
windups retain their previous movement permissions. No root/leap/pull/knockback system
exists in the live kit; none was invented here.

## Bots and camera

Existing lane assignments, respawn reset, tower-cover checks, retreat logic and pulse
escape remain. Bots have a 0.22m arrival hysteresis, exponential acceleration, low-noise
separation at rest and a short committed wall-tangent detour when physical travel stalls.
Hazard escape cancels a detour; tower safety still gates requested movement. This is
local obstacle steering, not a global pathfinder for arbitrary maze maps. World scans
are shared across bots at 0.5s cadence instead of allocating one scan set per bot.

Camera position still follows exponentially. Its angle is now determined by its fixed
view offset, so following does not change input axes. Dash tracking tightens and large
recall/respawn relocation snaps the camera instead of sweeping across the map.

## Presentation (procedural replacement)

HeroVisualMotion is the only hero movement controller. It advances gait from actual
planar displacement, with separate start/stop thresholds and smoothed weight. The helper
HeroProceduralRig splits each existing leg mesh at its knee and applies hip, knee, ankle,
elbow and wrist rotations from one procedural gait. There are no Mixamo clips, PlayableGraph,
or alternate locomotion fallback. AllFigureMotionRealism continues to animate non-hero
figures and refuses to run on HeroIdentity actors. Combat attack poses and cast timing remain
in HeroVisualMotion and the gameplay combat systems; visual motion never moves the gameplay
root or hitboxes. Large relocation and respawn reset the gait without a burst of steps.

This is a lightweight game-style gait, not motion capture or full terrain foot IK. It does
not claim exact foot locking while turning. If procedural joint construction fails, a clear
runtime error is logged and the controller does not switch to another gait.

## Validation

Run here: C# syntax parsing across 44 audited files, diff whitespace checks, byte-exact
hero-stat/spec preservation, radial input symmetry/cap/dead-zone checks, ideal open-space
dash integration at 15/30/60/120 FPS plus uneven frame intervals, and distance-phase /
straight-stance mathematical checks. These are source/numerical checks, not physics or
visual validation in Unity.

Added, **not run here**: `DIMENSIONS FRONT > Checks > Movement collision and dash
(Play mode)`. Start a running match first. The isolated test actor checks solid wall
blocking, wall sliding, dash wall blocking, absence of an immediate teleport, dash
distance, stun cancellation and death cancellation, then removes its objects.

No Unity compiler/editor or Android device is available in this environment. Required
acceptance: all six heroes at several frame rates; joystick plus attack multitouch;
release/focus loss; narrow corners and both arena edges; existing towers/cores/objectives;
retreat and all nine bot respawns; dash into solids/through crowds/during CC; recall and
match restart; actual foot/leg poses from gameplay camera; CPU/GPU/GC profiling.
Collider-free decorative geometry is intentionally not made solid by this patch.
