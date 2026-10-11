# Guard mech hand rotation

The production guard pose repeatedly reset the upper arm and elbow before solving IK,
but retained the previous local wrist rotation. `CombatMotion.Solve` preserves the
incoming world wrist orientation and therefore writes a local compensation after
rotating the arm. Reusing that compensation on the next frame accumulated rotation
even while the guard stood still. Green Planet claws and Mars fists are the actual
visible hand transforms in this chain. The existing gun-specific wrist reset only
covered the machine-gun branch.

`LaneUnitVisualMotion` now captures each authored wrist rotation during binding and
restores it before every arm solve. The machine-gun branch retains its existing
actor-relative holding orientation. Relocation also restores both authored wrist
rotations. Arm articulation, rigid shoulder armor, IK lengths, barrel rotation,
gameplay transforms and combat behavior remain unchanged.

## Validation

```sh
python Tools/Movement/GuardArmRegression/run.py --mono-root /path/to/mono-root
```

The regression now compiles the actual Green/Mars arm builders as well as the Moon
guard builder and the production binding, rest capture, pose update and IK methods.
Geometry/material creation and Unity transforms are substitutes. The previously
missing machine-gun fixture type is supplied so the existing test compiles again.

Before the fix, the new stability check failed on the Green Planet left claw at
24 FPS: **68.306 degrees of drift on the second stationary pose**. Observation mode
across the cases measured up to **179.897 degrees of orientation drift** and an
**88.150 degree frame jump**. These results reproduced the accumulation in the
production methods rather than merely inspecting the source.

After the fix, **239,865 assertions passed** across 16 existing shoulder/articulation
scenarios and 24 new wrist scenarios. Moon, Green Planet and Mars wrists are checked
at 24/30/60/120 FPS with and without a held gun, during idle, after walking/turning
and repeated attacks, relocation, and pause. Stationary wrist drift and frame jumps
are **0.000 degrees**. Existing rigid shoulder checks and arm articulation still pass.
Native Unity playback, GPU rendering and Android visual verification were not run.
