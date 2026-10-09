# Movement video audit — 2026-10-09

## Observed result

The supplied 18.13-second Unity recording shows Tahla Venn changing world position while her visible legs remain nearly in their standing pose. This is particularly clear at 3.3–5.8 seconds and 9.75–10.5 seconds. Turning changes the full body's heading without a readable alternating step. Other units do move; this recording alone does not establish that a particular other player is unable to move.

## Confirmed collision defect and repair

`HeroLocomotion.Sweep` previously returned zero allowed movement whenever its 32-entry query buffer filled, before filtering the results. Thirty-two self, unit, or upward-facing ground hits could therefore stop movement even without a blocking structure. A truncated result set could also omit the nearest real blocker.

The saturated path now calls `Physics.CapsuleCastAll` and applies the same self/unit/ground/direction filters to the complete results. The nearest real blocker still limits travel. Queries below capacity retain the non-allocating path. The complete query allocates only on saturation; persistent saturation in a dense scene still needs phone profiling. This is a concrete possible movement blockade, not a demonstrated cause of Tahla's rigid legs in the video.

The portable collision runner compiles the unmodified production `Sweep` method against deterministic query doubles. It covers an ignored crowd exceeding capacity, a real blocker beyond the initial buffer, normal queries without fallback, and movement away from a contact. It does not simulate Unity Physics or compile the complete Unity project.

## Animation diagnosis

Source inspection confirms input ordering, death/respawn reset, generated leg chains, Tahla plate bone assignments, and the intended visual writer order. Imported skeleton conversion calls the final combat pose and grounded foot solve synchronously. The console message `imported rest pose RESET v5` alone does not establish an animation bug. No speculative pose reset or additional Animator was enabled.

The existing live foot check only visited existing active grounded components and could miss a hero whose motion controller was missing or disabled. The new `DIMENSIONS FRONT/Checks/Movement record all heroes for 15 seconds` check visits active gameplay hero identities, samples after `LateUpdate`, and reports controller/chain readiness and moved heroes with rigid final visible leg bones. Move the local hero, turn, stop, and move again during the capture; exercise remote heroes on a co-op client as well. A hero remaining stationary is reported as unexercised, not automatically broken. Check the Console report after capture.

## Verification limits

Unity 6000.6.1f1 is unavailable in this environment. No Play Mode acceptance, rendered gameplay, imported FBX verification, Android build, or live multiplayer test was run here. A successful portable or syntax test does not establish that the visible movement is repaired. The original upper-body regression passes 261,846 assertions with substitute engine math/components. A separate synthetic leg solve check at the existing imported scale also passed its contact tolerance; strongly asymmetric scales did not and were not treated as evidence about this video's model.

The rigid-leg problem remains open until the new final-bone capture runs in Unity and identifies the active failure. Required acceptance: every playable hero, forward/backward/sideways, start/stop, turning, walls, slow/buff, attacks, abilities, dash, death/respawn, local/remote co-op, and Android performance. Preserve Auren's shield, Sivra's rifle contacts, weapon grips, chosen MP3 assets, and existing movement/combat balance.
