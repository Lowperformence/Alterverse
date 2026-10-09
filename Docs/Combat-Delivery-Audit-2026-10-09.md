# Combat delivery and difficulty review — 2026-10-09

## Scope and findings

Reviewed the active shared player/bot implementation for all eight heroes: eight basic attacks and all 32 Q/W/E/ultimate slots. The runtime roster uses PlayerHeroAbilities/HeroAbilityExecution, not the old VeyrAbilities prototype.

| Execution path | Review result |
| --- | --- |
| Eight basic attacks | Strict initial range remains. Melee contact retains 0.25 m release grace. Ranged shots now finish after release when caster/target moves or the caster is stunned. |
| Targeted Q/W/E skills | Use the same release/arrival scheduler. Legal target, authority, death, disable and damage gates still apply. A stun cancels preparation, not a shot already flying. |
| Nerok Anchor Recall | Replaced separate coroutine with shared delivery. Validates sight at release and from launch origin at arrival; a real wall still blocks damage. Anchor presentation now follows the selected target on host/client. Pull stays capped at 4.2 m and uses collision-safe forced movement. |
| Nerok pull under victim stun | Interrupting the victim's own ability preparation no longer resets externally owned forced movement. |
| Nyra four-shot ultimate | Range loss/stun still stops future shots. Already released shots finish. Four 0.18 s releases and total damage unchanged. |
| Veyr delayed ultimate | Still resolves after its explicit windup with range validation; leaving range can avoid it by design. |
| Area attacks and support ultimate | Fixed cast centres, windup, radius and Health.Active target registry reviewed. A target leaving the displayed area can avoid it; no new area/range/damage balance changes. |
| Nerok vortex | Reviewed six half-second damage ticks, continuous inward displacement, authority and interruption rules. Requires Unity playtest for real frame/physics timing. |
| Mobility, heals and shields | Reviewed eligibility, collision-safe dash, local/server routing and cooldown paths. No execution changes required. |

The old generic delivery regression treated every Q as shared delivery while the hook used a coroutine; it also compared restored heroes' HP without their regenerated base shield and ignored Brakk's existing 5% damage multiplier. The regression now accounts for those cases and includes hook release, moving targets, source/victim stun, real walls and rejected damage.

## Difficulty and held attack help

- New VeryEasy = -1 / VERY EASY is below Easy; old serialized ids 0–4 are preserved.
- Very Easy has slower reaction/decision/objective intervals, weaker prediction/positioning/team awareness and more ability mistakes than Easy. Damage multipliers are unchanged.
- Existing solo adaptive progression can now demote Easy to Very Easy and promote back after the existing three-result streak.
- A fresh profile starts on Normal. Saved personal tier, win/loss streaks and last result receipt are persisted through AccountProfilePrefs/PlayerPrefs and included in the account cache/cloud profile; exiting an online override does not reset them. Existing legacy import keys already include all four values.
- Co-op difficulty cycling/validation covers all six tiers.
- Held descriptions and range overlays appear only in solo Very Easy. Other solo tiers, online and co-op hide them. Press/release/cancel input still executes independently.
- All online plan factories, authoritative replicas, handshake validation and subsequent rounds enforce Hard. Runtime difficulty also enforces Hard for online plans, including later spawned/replacement bots. Private co-op retains its host difficulty.

## Validation

C# syntax parsing and source contract checks were run in the workspace. Unity and a C# compiler are unavailable here; the editor/play-mode regressions were added/reviewed but could not be executed.

The server profile suite passed 25 tests, including a new account-isolated restart test for all six tiers (including -1), pending two-loss streak and duplicate-result receipt.

Unity checks to run:
- DIMENSIONS FRONT/Checks/Attack delivery (Play mode) — disposable offline unpaused match.
- DIMENSIONS FRONT/Checks/All hero basic delivery (Play Mode).
- DIMENSIONS FRONT/Checks/Difficulty and attack help (non-destructive).
- DIMENSIONS FRONT/Checks/Bot priority: planet buffs before Hot Zone.

Device/network acceptance remains: hook near maximum range against a moving hero; stun before and after release; wall during flight; tablet low-FPS pull; held buttons across all six solo tiers and co-op/online; online bot replacement/rematch remains Hard. Deploy matching updated clients/server: shark target-follow effect uses DF_COOP_SHARK_FX_V2.

