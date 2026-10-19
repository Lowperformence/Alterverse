# Held aiming at the outer radius

2026-10-19. Base: `dev/vertical-slice`, `ee1ce1b3083c60b73c35c873421fb7de01d06f64`.

## Reproduced code behavior

`ManualAiming.Rebuild` applied automatic assistance even after a ground-area drag. An enemy crossing assist eligibility changed the circle direction between the raw thumb direction and the enemy direction. At full range this produces the largest lateral displacement.

`BasicAttack.TryAttack` refreshed facing for eligible targets but returned immediately when the same target moved just outside range. Movement facing could then take over between successful refreshes. Also, `HeroMotor.TrackBasicTarget` replaced an accepted windup's longer facing deadline with the per-frame 0.08-second hold deadline.

## Correction

- A dragged ground-area gesture uses its thumb direction and distance directly. Tap assistance remains available before dragging. Manual aiming rebuilds at execution order 100, after default-order locomotion and before the preview at 200, keeping the point and origin in the same movement frame.
- For a held basic with no eligible target, only the last valid basic target's presentation facing can be refreshed: within 0.2 units beyond the normal range, for 0.12 seconds. This does not acquire a damage target, start an attack, restart animation, spend cooldown or change movement speed. Refresh stops on release; dead, invalid or distant targets cannot renew it.
- Same-target hold updates extend rather than shorten the accepted facing deadline. Switching to another eligible target still updates immediately.

No damage, cooldown, cast range, projectile geometry, network protocol or server authority values changed.

## Validation

`run_entry.py --dotnet-root /path/to/dotnet --aim-jitter`: **12,006 assertions pass** at 30/60/120 FPS, compiling the production aiming, basic attack and motor sources. Every ground-area skill is tested during movement while an enemy alternates across assist boundaries. All ten hero basics are checked for facing retention outside exact attack range without a new attack/cooldown, deadline preservation, release expiry, dead and distant target rejection.

Negative controls independently restore the original area-assist branch, facing-deadline assignment and missing no-target facing refresh. Each fails its corresponding regression assertion; corrected sources pass.

Existing suites: **1,310** skill-entry/combat assertions plus both editor delivery suites under fixtures; **699** ranged-cone assertions pass.

These are deterministic C# fixtures, not a Unity build or Android visual test. The jitter suite uses the actual motor's target-facing decisions, but native locomotion and quaternion interpolation/rendering are substituted. Device acceptance still requires holding a basic while strafing at maximum range, dragging an AOE around its outer edge while moving, releasing/cancelling, and checking online and offline with the rebuilt client.
