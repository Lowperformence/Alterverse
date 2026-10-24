# Selected Epic Toon FX work checklist

Requested by Radisch, 25 October 2026. Progress is implementation / controlled verification / native Unity review, tracked separately. Default for these selected new effects: +250% linear size (3.5x); attack footprints and travel endpoints stay at their actual gameplay limits. No 2D Alternate variants or cartoon text. Existing sounds stay except Traveler's two explicitly selected complete lightning chains.

| Task | Selected presentation | Implementation | Controlled checks | Native Unity review |
| --- | --- | --- | --- | --- |
| Thala Growth Veil | Small rising healing crosses, HealOnceLoop | Implemented | 64 focused checks passed | Pending |
| Thala Wildsurge | Marked green ring healing field, HealField instead of HealNova | Implemented | 64 focused checks passed | Pending |
| Timmy Jet Pounce | Add native smoke from both jetpack nozzles; preserve thruster | Implemented | 86 focused helper checks passed | Pending |
| Timmy Scratch | Add native smoke from both jetpack nozzles; preserve thruster | Implemented | 86 focused helper checks passed | Pending |
| Timmy Scratch confirmed hold | Stunning Circling Stars on victim during actual hold, with native sound and 3.5x size | Implemented | 86 focused helper checks passed | Pending |
| Zhaev Vacant Echo | Spiky Blood Explosion | Implemented | 86 focused helper checks passed | Pending |
| Zhaev Rift Scar | Lightning Strike instead of purple X; existing sound | Implemented | 86 focused helper checks passed | Pending |
| Auren Kest Citadel | Magic Circle; existing sound | Implemented | 86 focused helper checks passed | Pending |
| Brakk Orun Overdrive | Fire Nova; existing sound | Implemented | 86 focused helper checks passed | Pending |
| Traveler Temporal Beam | Full native lightning shot chain including package audio | Pending | Pending | Pending |
| Traveler Chrono Snare | Full native lightning shot chain including package audio | Pending | Pending | Pending |

Protect all unrelated restored attacks, Nerok, menu/shop, shield bar, gameplay authority, timing, damage/healing/shields, cooldowns and targeting. Only the selected VFX and specifically authorized Traveler audio are in scope. A passed fixture check does not establish native import, camera appearance or Android performance.

The new TimmyHold semantic cue is appended after existing cue indices. It starts only after Scratch contact and TryRoot succeed, and explicitly ends on release/cancellation. Its package sound uses one appended audio-bank cue and is not restarted for damage ticks. No stun or action lock is added. Zhaev retains the travel silhouette of Vacant Echo, but its old release/contact bursts are replaced by a confirmed native blood explosion clipped to the actual release cone. Enclosing Magic Circle rims fit the entire live AoE; burst interiors use 3.5x geometry within their unchanged clip. Native source resolution logs actual selected paths and excludes 2D Alternate.
