# Skillshots: enemies entering an initially empty target field

Source baseline: `18f378cb805c130297423d118a33e2b688b85b5f`, branch `dev/vertical-slice`.

## Confirmed failures

- An initially empty directed cone did not acquire an enemy entering its side during preparation. Projectiles and beams continued down the middle, although the enemy was inside the displayed selection field at launch. The original production delivery failed the new 30 FPS Veyr W edge-entry case: expected 35 damage, actual 0.
- Timmy Scratch retained only the victim selected at release. With no initial victim, a later entrant could not connect even when Timmy physically crossed that enemy during the leap. The original production Timmy execution failed the new 30 FPS warning-entry case: expected 54 damage, actual 0.
- Ordinary ground and self areas already checked current registry positions at impact. They do not need a target list captured at release. Their existing behavior is covered by the new matrix.

## Correction

`HeroAttackDelivery` stores the release origin for the existing directed cone. When a cone has no captured point, it checks that original field once at launch and freezes an entrant's current position. It then uses the existing rebase, range, wall, beam or swept projectile delivery. A previously captured point remains fixed. A projectile never acquires or follows enemies after its own launch. Empty volley shots each perform their check at their own launch; an already captured volley retains its original point.

Timmy's manually aimed empty field likewise accepts an entrant at the end of its warning, using the original world origin/direction. Movement taps keep their chosen direction. If a leap still launches without a victim, its actual path checks eligible enemy heroes during flight and on the landing frame. Relative-motion sweeps select the earliest physical contact, including long frames. A previously selected victim that escapes cannot be silently replaced. The original cone/range, line of sight, eligibility, damage gate, interruption, hold and four-tick damage rules remain in force.

Damage, cooldowns, windups, range, cone widths and projectile speed/width are unchanged. Ground areas keep their current impact-time checks. The V5 packet layout is unchanged; authority remains on the server.

## Verification

```bash
python3 Tools/Aiming/Regression/run_entry.py --dotnet-root /path/to/dotnet
```

Result: **1,245 assertions pass**, plus `BasicAttackDeliveryRegression.Run()` and `AttackDeliveryRegression.Run()` compiled and executed under the shared engine fixtures.

The entry matrix runs at 30, 60 and 120 FPS and covers:

- Every catalogued melee/projectile/beam skill: an initially empty cone receives a central or edge entrant during preparation; damage resolves with the existing catalog amount.
- Every damaging ground/self/conical area, including the unreleased Void Knight test catalog: impact-time entrants retain their current damage. Support-only areas use the existing support checks separately.
- Timmy entrants during warning and flight; edge entrants before launch; physical path contact for movement taps; first contact across a long landing frame.
- Rejection of off-path entrants after launch, allies, minions, walls, invulnerability, expired leaps, out-of-range victims and interrupted casts. A marked victim can still dodge without a replacement hit.
- The release field stays anchored when the caster moves; geometry-only overrides do not gain cone capture; already launched shots and captured release snapshots do not retarget.
- Actual V5 serialization/host command handlers for empty beams, projectiles and Scratch; existing captured-point network, reverse flick, independent aiming, Timmy contact/channel and Timmy pooled VFX checks.

The runner compiles unchanged production combat sources, including the appended Void Knight partial, against deterministic Unity/time/physics/transport fixtures. It adds only presentation/authority declarations missing from the older shared fixture. The historical full `run.py` suite was not reported as passing: it contains unrelated stale assertions for Sivra basic damage and Nerok's newer path overlay. The new runner executes the focused entry matrix and the applicable existing groups directly.

Native Unity compilation, CharacterController collision, Android rendering/input, real network latency and live deployment are not tested here. The corrected server and Android builds must be rebuilt and tested together; a GitHub push does not update the running Edgegap image.

## Cover-edge regression follow-up

The existing Nerok suite exposed a regression in the new launch-time empty-cone capture: an entrant beside cover was selected at its centre, steering a previously clear free shot into that cover. The original body could have been hit by the unchanged forward sweep. Late capture now checks the real launch-to-centre swept blocker path first. If obstructed, the committed free trajectory remains and existing physical body/wall contacts decide the hit. This does not allow damage through cover. Existing captured points are unchanged.

The pre-fix production delivery fails `GrazingContact` (actual HP 1000, expected 972). The correction passes all **284 Nerok checks** and all **1,245 entry/existing-combat assertions**, including the existing cover negatives, under controlled engine fixtures. The CI workflow now runs both suites and watches all shared aiming-regression files. Native Unity/device tests remain outstanding. This source correction is not included in the already uploaded v0.1.4 image.
