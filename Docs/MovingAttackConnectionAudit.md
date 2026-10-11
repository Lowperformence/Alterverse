# Attack connection while moving — 2026-10-11

Historical launch-time prediction pass. Automatic basic behavior is superseded by
`BasicAttackTargetCommitmentAudit.md`: all nine basics now retain their accepted
target through contact. Manual skill geometry remains unchanged.

## Reproduced failures

1. **Automatic ranged basics:** Sivra, Ione, Tahla and the Traveler retained the
   enemy's position from the button press. When caster and enemy ran sideways at
   the same speed with 4 m between them, all three held attacks missed at
   30/60/120 FPS. Relative distance stayed inside attack range. The expected
   damage was 66 / 45 / 48 / 51; actual damage was zero in all twelve cases.
2. **Zhaev's Vacant Echo:** a stationary enemy at 2 m could be outside the old
   melee cone axis after Zhaev strafed during its 320 ms preparation. This was
   reproduced at all three frame rates. The captured world point remained valid,
   but melee delivery did not rebase its direction from the new launch origin.
3. **Saturated obstacle query:** 40 overlapping actor colliders on the sight layer
   filled the 32-hit buffer. The old rule treated a full buffer as an immediate
   wall, even though its entries were ignored actors. An otherwise valid basic
   stopped before reaching the enemy. The test explicitly uses the default sight
   layer; normal unit-layer colliders are excluded from this query already.

## Corrections

- Ranged basics now default to `TargetingMode.AutoBasic` (appended enum value 4).
  Sticky selection and hero priority remain. The selected enemy is read **once at
  launch**, with bounded lead from the existing `HeroAimPrediction.Point` helper.
  The shot then keeps a fixed trajectory and uses the existing swept body contact.
  Sudden dodges after launch, walls and range limits can still cause misses.
- `basicLeadQuality` in `Combat/AimingSettings.asset` controls the lead from 0 to 1,
  default 1. The existing helper caps prediction at 0.24 seconds / 1.2 m and uses
  observed locomotion velocity. Flight targeting retains its range clamp but does
  not clamp to the arena rectangle used by ground-position prediction.
- Every captured direction, including melee, is rebased once from the current
  launch origin to its fixed world point. Manual skill targets remain frozen at
  release; they do not acquire automatic target tracking or prediction.
- On rare saturated queries, `SphereCastAll` supplies the complete obstacle list
  before the same owner/wall filters run. Ordinary queries retain the nonallocating
  buffer, following the existing locomotion fallback pattern.
- V5 basic commands carry the selected target using the existing target-identity
  fields. The host validates enemy eligibility and range; it refreshes that target
  at launch instead of selecting another hero. Manual commands retain fixed points.
  No packet-layout or channel-name change was needed. Update client and server
  together to receive the new basic behavior.

Damage, cooldowns, attack intervals, windups, projectile speeds/widths and attack
ranges remain unchanged. Melee basic targeting, AOE multi-hits, support, dash,
hook, vortex and volley mechanics retain their existing delivery classes.

## Verification

**1771 assertions pass**, plus the two existing editor delivery regression suites.
`MovingConnectionRegression.cs` adds:

- All eight basics and all 32 skill effects after caster strafe, at 30/60/120 FPS.
- Real HUD press/hold with two touch IDs and three attacks per hero, while caster
  and enemy co-move at `.88 * HeroCombatProfile.MoveSpeed`. All eight heroes
  produce their existing expected damage; melee samples use 1.8 m, ranged 4 m.
- Automatic ranged basics still miss post-launch dodges; explicit fixed-point
  basics do not silently reacquire. Invalid allied/out-of-range targets are
  rejected without spending host cooldown. The lead can be disabled centrally.
- V5 moving-target receipt/launch tests at all three frame rates, stable roster
  identity, a separate decoy, and rejection of a truncated basic packet.
- Saturated actor queries with and without a real wall; restoring the old
  full-buffer rule reproduces the false miss.

These are deterministic production-code fixtures with supplied movement paths.
Native CharacterController motion, Unity rendering/audio, network transport
latency and an Android device are not measured. Existing damage gates, shields,
interruptions, range/wall misses, all skill classes and connection timing checks
also pass. The original timing report remains in `AttackConnectionTiming.md`.

## Brakk's remaining arm details

The six-bracer and upper-arm tech-panel cleanup is retained. The latest branch
also excludes Brakk's generated hand/knuckle detail plates and raises the hero
FBX importer version to 13 so Unity rebuilds cached models with current Rook
detail exclusions and readable mesh settings. Those changes are preserved in
this audit's integration. Grey shoulders, gauntlets and Nerok's shared source
asset remain intact. See `BrakkArmPlateCleanup.md`; visual verification in the
user's Unity session is still required.
