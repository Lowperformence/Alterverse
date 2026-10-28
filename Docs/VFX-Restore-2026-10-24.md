# Original VFX and sound restoration — 2026-10-24

The runtime presentation is restored to commit `4dbdca991d124e4cf0b73d7a5fc73dfddf6a5271`, immediately before the first Epic Toon replacement commit (`9ed4067fd3ac3e6293216a321d221375fdf67116`). This restores the previous hero attack effects, original shield effect, impacts, bomber explosions, healing markers, and hero sound selection. It does not reset the whole branch.

## Preserved

- Menu, shop, collection categories, cosmetics/headwear and commerce changes from `144b60ef3a2de07fa0d7408a0d5a5409a9ecfd4a`.
- Current match loading presentation.
- The blue shield-amount bar under hero health bars (`HeroTeamMarkers.cs`), explicitly requested by the user.
- All 337 original audio resource files; their Git blob hashes already match the pre-replacement baseline. No audio clip is edited or trimmed.
- The purchased asset import. The passive `EpicVfxLibrary` data type and `EpicParticles` shader remain so locally generated, unused library assets/materials retain valid references. Neither enables replacement effects.
- The corrected Void Knight test dependency on `HeroVfxEvents.cs`.

## Removed from active presentation

The Epic Toon runtime replacement backend, automatic replacement builder, visibility-review editor utility, added planetary weather and added healing-station effect component are removed. Experimental checklists/concepts and the replacement-only tests are removed with their implementation. The existing ranked workflow retains the loading checks and no longer runs the removed replacement-only suite.

The previous pushed VFX state is retained at branch `backup/vfx-before-original-restore-20261024` for comparison. Restoration is a new forward commit on `codex/epic-toon-replacement-20261024`; no branch history is rewritten.

## Validation

- 4,779 production online VFX/combat/shader assertions pass with controlled Unity/GPU/network fixtures.
- All ten hero basic-attack delivery checks pass; 1,332 skill-entry/combat assertions pass with deterministic engine fixtures.
- 103 Void Knight combat checks and 14 temporary future-release purchase checks pass.
- Before publication, the candidate Git tree is checked against the parent: every restored production file matches the baseline, every intended deletion is absent, and all other files (including shield HUD, menu/shop and original audio resources) keep their parent blob hashes except this note and the narrowly cleaned workflow.

These checks do not establish appearance, sound timing or performance in native Unity or on a phone. A native Play Mode review is still required.

## Reviewing in Unity

Stop Play Mode, pull `codex/epic-toon-replacement-20261024`, let Unity finish importing/compiling, then enter Play Mode again. Review the restored original attacks and audio first. Further purchased-effect work proceeds one attack at a time after that review; no automatic whole-roster conversion is enabled.
