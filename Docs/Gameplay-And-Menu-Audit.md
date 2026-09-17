# DIMENSIONS FRONT — gameplay and menu code review
Base: e3a2d41a04cc89ed13548a30a992a70f1f76c4fc

## Evidence and limits
Loaded all 156 gameplay C# files and examined the menu, bootstrap, input, targeting, damage, bots, minions, objectives, VFX, profile, match result and mobile configuration paths. This is a source-level review, not a visual acceptance test. The uploaded MP4 could not be decoded with the tools exposed in this session. No Unity editor, compiler or Android device runner was available. No build or playtest is claimed.

## Changes
- Defer match construction until menu launch so hero identity, model, stats and ability kit use the chosen hero.
- Own generated match objects under DF_MatchWorld and rebuild that world on the next match. Bots, waves and planets spawned later join the same hierarchy. Existing scene-authored objects are preserved.
- Rebind match audio when a delayed or replacement match becomes available.
- HUD ability tap opens a preview: full name, actual effect, range, cooldown, CAST and CANCEL. Keyboard shortcuts retain immediate casting.
- Preview circle shows maximum range, not area damage. Movement abilities show distance budget; there is no drag-to-aim yet.
- Preserve exact combat range for sticky targets and collider-based acquisition.
- Limit player support healing to the ability's configured range.
- Allow the player to acquire enemy vulnerable cores, and bots to acquire planets and vulnerable cores.
- Show bot basic attacks with slash or projectile effects; no attacks while paused.
- Route menu portraits through the existing hero-specific illustration renderer.
- Set Unity product name to DIMENSIONS FRONT.

## Checks executed
- Prototype/PrototypeBootstrap.cs: balanced structure
- UI/MainMenuController.cs: balanced structure
- Characters/PlayerHeroAbilities.cs: balanced structure
- Combat/SoftTargeting.cs: balanced structure
- UI/PrototypeHUD.cs: balanced structure
- AI/BotTargetSensor.cs: balanced structure
- UI/StylizedHeroCard.cs: balanced structure
- Prototype/FiveVsFiveTestRoster.cs: balanced structure
- Combat/LaneWaveSpawner.cs: balanced structure
- Match/DimensionPlanetDirector.cs: balanced structure
- AI/MobaBotCombat.cs: balanced structure
- Audio/MatchAudioDirector.cs: balanced structure
- Match construction waits for menu selection
- Replay destroys old match before rebuilding
- All four HUD skill buttons arm a preview
- Preview has explicit cast and cancel
- Damage target radius no longer has 25% squared-distance allowance
- Player heal selection applies range
- Core selection requires vulnerability
- Bot sensor can select available planets

These assertions check source integration only.

## Unity acceptance checks still required
1. From a cold scene choose each of the six heroes. Check identity, model, HP, skill names and movement speed.
2. End a match, return to menu, choose another hero, start again twice. Check one player, nine bots, four towers, two cores, one timer; old waves/planets must be gone.
3. Preview each skill. CANCEL must spend no cooldown or health. CAST must apply the displayed action; no target must not spend the cooldown.
4. During preview move, die, pause and respawn; previews must not remain active through death or menus.
5. Test enemy and ally just inside/outside ability range. Large colliders and sticky targeting must not extend damage range.
6. Attack enemy core above and below its vulnerability threshold. Bots must attack available planets when in range.
7. Check bot attack visibility and draw cost on a crowded lane.
8. Verify audio binds on first match and subsequent matches.
9. Repeat the existing minion combat probe and a full wave-versus-wave match.
10. Inspect mobile safe areas, portrait clipping, description wrapping, two-finger movement+skill use and stable framerate on Android.

## Remaining release blockers and design problems
- Hero art is still procedural low-poly geometry; no final authored characters, skinning, attack animation or visual acceptance.
- Skills largely use auto-target single-target damage; several names/VFX suggest control, shields or areas that gameplay does not implement. Do not advertise those missing mechanics.
- Bot utility skills still diverge from player skills and need a shared execution design; support AI healing is not yet range-limited.
- Full skill icons, drag-to-aim, target-specific preview and direction indicators remain unfinished.
- Procedural combat effects instantiate many objects/materials; mobile performance needs profiling and pooling based on measured cost.
- Two mobile quality policies overlap; consolidate after selecting the intended settings flow.
- Shop and profile still use IMGUI; touch keyboard, saved names, purchased avatar presentation and layout require device tests.
- Product name fixed, but Android application identifier remains Unity's template identifier; release identity/signing/billing remain unconfigured.
- Multiplayer provider is absent; current playable mode is local bots.
- No green Unity compilation, full playtest, Android build or MP4-based visual review exists for this change.
