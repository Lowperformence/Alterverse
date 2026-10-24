# Selected Epic Toon FX work checklist

Requested by Radisch, 25 October 2026. Progress is implementation / controlled verification / native Unity review, tracked separately. Default for these selected new effects: +250% linear size (3.5x); attack footprints and travel endpoints stay at their actual gameplay limits. No 2D Alternate variants or cartoon text. Existing sounds stay except Traveler's two explicitly selected shot chains and Timmy's explicitly requested native Stunning Circling Stars cue.

| Task | Selected presentation | Implementation | Controlled checks | Native Unity review |
| --- | --- | --- | --- | --- |
| Thala Growth Veil | Small rising healing crosses, HealOnceLoop | Implemented | 64 focused checks passed | Pending |
| Thala Wildsurge | Marked green ring healing field, HealField instead of HealNova | Implemented | 64 focused checks passed | Pending |
| Timmy Jet Pounce | Add native smoke from both jetpack nozzles; preserve thruster | Implemented | 88 focused helper checks passed | Pending |
| Timmy Scratch | Add native smoke from both jetpack nozzles; preserve thruster | Implemented | 88 focused helper checks passed | Pending |
| Timmy Scratch confirmed hold | Stunning Circling Stars on victim during actual hold, with native sound and 3.5x size | Implemented | 88 focused helper checks passed | Pending |
| Zhaev Vacant Echo | Spiky Blood Explosion | Implemented | 88 focused helper checks passed | Pending |
| Zhaev Rift Scar | Lightning Strike instead of purple X; existing sound | Implemented | 88 focused helper checks passed | Pending |
| Auren Kest Citadel | Magic Circle; existing sound | Implemented | 88 focused helper checks passed | Pending |
| Brakk Orun Overdrive | Fire Nova; existing sound | Implemented | 88 focused helper checks passed | Pending |
| Traveler Temporal Beam | Full native lightning shot chain including package audio | Implemented | 43 chain + 275 audio checks passed | Pending |
| Traveler Chrono Snare | Lightning shot, then full Nova on actual AoE after arrival; native package audio | Implemented | 43 chain + 275 audio checks passed | Pending |

Protect all unrelated restored attacks, Nerok, menu/shop, shield bar, gameplay authority, timing, damage/healing/shields, cooldowns and targeting. Only the selected VFX and specifically authorized Traveler/stars audio are in scope. A passed fixture check does not establish native import, camera appearance or Android performance.

The new TimmyHold semantic cue is appended after existing cue indices. It starts only after Scratch contact and TryRoot succeed, and explicitly ends on release/cancellation. Its package sound uses one appended audio-bank cue and is not restarted for damage ticks. No stun or action lock is added. Zhaev retains the travel silhouette of Vacant Echo, but its old release/contact bursts are replaced by a confirmed native blood explosion clipped to the actual release cone. Enclosing Magic Circle rims fit the entire live AoE; burst interiors use 3.5x geometry within their unchanged clip. Native source resolution logs actual selected paths and excludes 2D Alternate.

Final controlled regression results: 64 Thala source/field/follow contracts, 88 selected ability source/scale/hold/cancellation contracts, 43 Traveler chain/controller/miss/arrival-Nova/AoE contracts, 275 selected Traveler/stars full-clip audio routing contracts, 4,808 production common-facade/network/combat checks, 1,335 skill-entry/combat assertions, and 278 existing Sivra native-audio contracts. Unity/GPU/physics/time/voices are controlled substitutes. Actual paid-source import, matching the referenced preview, crowded combat and Android performance remain pending local acceptance.

Source mapping:

- Thala: `Interactive/Healing/HealOnceLoop.prefab` for rising crosses and `Interactive/Healing/HealField.prefab` for the ringed field. Enclosing rims fit the entire real area.
- Timmy smoke: `Environment/Smoke/White/SmokeBurstWhiteSoft.prefab`, added at both existing nozzle sockets during Jet Pounce and Scratch. Existing flames/claws stay.
- Timmy hold: discover the native Stunning/Circling Stars family including category-based file names, and its actual referenced or stun/dizzy/confusion/twinkle/sparkle package sound. Missing audio is reported rather than synthesized or trimmed.
- Zhaev: discover native Spiky Blood Explosion and Lightning Strike (purple preferred); blood starts only at a confirmed hit and respects the cached release cone.
- Auren: discover native Magic Circle (yellow preferred); retain its complete rim inside the actual Kest Citadel radius.
- Brakk: `Combat/Explosions/NovaExplosion/ExplosionNovaFire.prefab`, clipped to actual Orun Overdrive area.
- Traveler: `Combat/Missiles/Lightning/LightningMissileYellow.prefab`, its controller-referenced muzzle/contact stages first, otherwise native Lightning Muzzle / Lightning Explosion Yellow for Temporal Beam. Chrono Snare instead uses a full native Nova on its real area after that shot arrives. Preserve native projectile loops and natural tails. Sounds use complete source AudioClips with the selected `etfx_shoot_lightning.wav` launch fallback and explicit package impact-family selection.

Temporal Beam remains a direct shot: only confirmed contact may create a contact burst. Chrono Snare remains an area ability: one lightning shot arrives at its existing deadline, then one full native Nova detonation/sound occurs, including legitimate empty-ground resolution. Its existing area-warning cue now starts the selected shot sound at actual flight launch; the later combat Cast cue does not repeat it. Other Traveler attacks and ultimate audio remain unchanged. The existing explicit size overrides on previously accepted effects are retained; 3.5x is the default for this newly selected work, subject to true attack footprint limits.

Latest user correction: Chrono Snare uses a full native Nova at ground contact instead of Lightning Explosion. The Nova must wait for projectile arrival, use +250% interior geometry, stay clipped to the live AoE, and replace rather than stack with the former central impact. Temporal Beam and selected package sounds retain their established mapping. Preferred native source is Combat/Magic/Nova/MagicNovaYellow.prefab; actual full Nova alternatives are discovered only outside projectile, Small, Lightning, Fire, Blood and 2D Alternate families.
