# Respawn scoreboard work — 2026-10-17

Reported trigger: online play stutters while the local hero is dead and waiting to respawn.

The respawn-only IMGUI table rebuilt and sorted two lists during every GUI event, allocated headings/value arrays and score strings, recalculated performance repeatedly, and called a global local-player scene search separately for each team. GUI layout and repaint events repeated that work within a frame.

Cache the local human for its active plan, retry missing ownership at a bounded interval and refresh the sorted presentation at four Hz. Reuse the team lists and row labels when painting. The countdown updates when its displayed second changes. Online respawn remains server-owned; offline confirmation still calls the existing respawn path. The match result and rewards paths are unchanged.

Validation uses the production discovery, view building and drawing methods compiled verbatim against controlled GUI and scene-discovery fixtures. For 1200 repeated draws, the old paint/local-lookup methods performed 2400 scene searches, 6000 performance reads and allocated 1699200 bytes in this fixture. The changed methods performed none of those repeated operations and allocated zero bytes in the fixture's no-allocation GUI. Seventeen behavioral assertions cover ownership, ranking refresh, countdown, visibility and online/offline respawn behavior. The negative control fails with the original paint methods. The existing transport/scoreboard regression also passes 225 checks.

These figures measure the production C# methods with substitutes; Unity's native IMGUI still has its own cost. No Android FPS improvement or full native Unity build is claimed. In the live Frankfurt match `cd6c4320db5d`, the server remained close to 60 FPS in the sampled intervals, with some individual frame spikes (up to 67.4 ms in the captured initial samples). That does not measure the phone's frame time and does not establish a sole cause for the reported stutter.

This UI change needs an updated client/AAB. It does not require extra server capacity or a protocol change. The previous roster/VFX allocation improvements also remain pending in client/server builds.
