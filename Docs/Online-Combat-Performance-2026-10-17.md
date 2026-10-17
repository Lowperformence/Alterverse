# Online combat performance follow-up — 2026-10-17

Report: intermittent online stutter and enemy attacks appearing stronger or duplicated.

## Findings and changes

- Combat intentions are sequenced and acknowledged by the authoritative server. Health rejects client-side damage mutations. Reviewed basic delivery, common ability resolution, Timmy, Void Knight and Nerok paths have no established online-only double-damage defect. Do not reduce damage speculatively.
- Online bots use Hard difficulty independently of the local solo adaptive tier. That changes decisions/accuracy, not a general outgoing damage multiplier. Multiple hits arriving between health snapshots can look like one larger hit; this remains a hypothesis requiring a reproduction or live damage trace.
- Hero state synchronization previously rediscovered every hero through a global `FindObjectsByType` search and allocated a new array every 0.5 seconds on all peers. Cache a complete roster for its active plan. Continue discovery during partial construction; invalidate on missing/changed slots, a new plan, or explicit refresh. Death and respawn retain the actor object.
- Create renderer-caching replica presentation components only on clients. Server gameplay does not consume them.
- Convert the received fixed-string world target key once per VFX message instead of once for each candidate in the Health registry.

## Validation

- Production `RefreshSlots` method compiled verbatim against controlled scene discovery fixtures: 1223 assertions passed. Stable ten-minute roster: one scene search instead of 1201. Also covered incomplete construction, missing/replaced actors, slot reassignment, plan change, explicit refresh, local ownership and server/client presentation ownership.
- Negative control with the original repeated discovery fails the stable-roster case.
- Existing production VFX/combat suite: 4306 checks passed with controlled Unity/GPU/network substitutes.
- No native Unity build, Android FPS benchmark, real network latency test or confirmed duplicate-hit reproduction was performed here.

## Live deployment and next measurements

The last observed real deployment `256221dfbb39` used v0.1.11-server, 0.50 vCPU and 1 GiB in Paris. It ended normally through the container delete request at 21:29:44 UTC. Edgegap's historical metrics returned retrieval errors and container logs are not retained after termination, so CPU saturation cannot be established from that round.

Hobbyist is the matchmaker cluster tier; changing it does not increase a game deployment's CPU budget. A controlled comparison at 1 vCPU / 2 GiB using the same image isolates resource pressure. It does not require a new AAB. Applying the code changes above does require rebuilding the Unity dedicated server and client; do not confuse that with the resource-only comparison.

During the next live round inspect container combat `serverFps`, `frameP95Ms`, `frameMaxMs` along with CPU and memory graphs before the server stops. Record the attacking hero, ability, defender, shields/buffs and actual HP delta for suspected duplicate damage. No higher-resource deployment was activated by this code commit.
