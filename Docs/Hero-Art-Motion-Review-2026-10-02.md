# Hero art and movement review — 2 October 2026

Base: `dev/vertical-slice`, commit `f4009fefab0d407ed1540682628caedf1f920538`.

## Implemented in this draft

- Weapon variants now retain the original opaque materials and add a separate translucent fluorescent layer using the same skinned mesh and skeleton. Surface opacity is 22–28%; edges 32–40%. Death, respawn, preview switching and material disposal follow the existing colorway lifecycle.
- The model importer performs a conservative, bone-weighted limb cross-section pass on all seven heroes. Existing head proportions, skeleton lengths, weapon grips and animation curves remain intact. Heavy classes gain 4% peak thigh volume and lose 3% peak forearm volume; other classes receive smaller adjustments.
- Chamfered hand, forearm and shin plates are generated from each model's actual weighted surface and use that model's skeleton. One additional skinned renderer/material per hero. Clothing, skin and metal receive distinct smoothness values.
- Traveler's isolated cylindrical handle is removed on model import. The Blender generator also omits the handle on future full rebuilds. The projectile socket is centered on the chronosphere's energy submesh. Its weapon grip floats subtly without moving the hand.
- Existing authored guard targets now follow the chest's pose. Elbow hints are closer to the body and the solver avoids full extension. Left/right guard adjustments remain editable on AuthoredHeroPresentation. Rifle support keeps its explicit weapon socket target.

The Unity importer version is incremented so imported hero assets receive the new pass. The Blender `.blend` and FBX source binaries have **not** been regenerated in this environment; these are reproducible Unity import changes plus a Blender generator fix, not replacement sculpted source files.

## Movement findings

The current visible heroes use HeroVisualMotion -> HeroProceduralRig -> AuthoredHeroPresentation. Imported Animators are deliberately disabled by the existing implementation. Consequently, buying/downloading animation clips alone will not change the visible movement.

The authored hero gait advances at 0.39 times its distance-based phase rate. This was left as configured by the user. Slower phase progression can contribute to foot sliding: evaluation requires an in-game recording at known travel speed and a foot-contact check.

Noxara spider already has separate foot planting, swing phases and leg IK. Kryon bear rotates complete legs with a sine cycle; the lower legs are visually part of that rigid motion. Realistic bear gait requires articulated knee/ankle pivots and planted-foot targets before a new animation source can help. No speculative monster animation replacement was made in this draft.

## External animation help checked

- Unity Two Bone IK: hand targets plus elbow hints; relevant technique for the existing guard solver. https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.1/manual/constraints/TwoBoneIKConstraint.html
- Cascadeur AutoPosing: humanoids and quadrupeds; quadruped support requires Pro or higher according to current documentation. Suitable candidate for a single-model test, not a confirmed automated connection in this session. https://cascadeur.com/help/tools/animation_tools/autoposing
- Cascadeur non-humanoid rigging: other creatures need a suitable custom rig. https://cascadeur.com/help/rig/advanced_rigging/other_topics/non_humanoid_characters
- Rokoko: video motion capture and FBX export/retargeting for human performances. https://support.rokoko.com/hc/en-us/articles/28312013117841-How-to-Get-Started-With-Rokoko-Vision
- Mixamo was checked as an alternative source; no old Mixamo clips were reintroduced. https://www.mixamo.com/

No project models were uploaded to external animation services and no paid service was purchased.

## Planet surface shortlist

| Map | Candidate | Proposed use |
| --- | --- | --- |
| Mars | https://polyhaven.com/a/red_laterite_soil_stones | Red soil and small rock breakup; reduce saturation to fit scene lighting. |
| Moon | https://polyhaven.com/a/rock_ground | Desaturated rocky detail mixed with fine regolith; artistic base, not a lunar scan. |
| Ice map / Kryon | https://ambientcg.com/view?id=Ice002 and https://ambientcg.com/view?id=Snow006 | Ice patches plus snow; restrain reflectivity for gameplay readability. |
| Pyraxis | https://ambientcg.com/view?id=Lava001 | Dark crust with locally emissive fissures. |
| Green planet | https://polyhaven.com/a/rocky_terrain | Stone/soil variation with selectively recolored growth. |

Poly Haven assets are CC0: https://docs.polyhaven.com/en/faq
ambientCG assets are CC0: https://docs.ambientcg.com/license/
Solar System Scope provides whole-planet maps under CC BY 4.0: https://www.solarsystemscope.com/textures/ — useful for distant planet spheres, not interchangeable with tileable close-up ground textures.

Proposed mobile starting budget: 1K material maps, 2K only where visibly necessary, tiled base color + normal plus packed roughness/masks. Profile on the target Android device before increasing resolution. This is a proposed budget, not a measured performance claim. No map textures were downloaded or installed as part of this research request.

## Validation and remaining work

This environment has no working Unity or Blender executable. GitHub read/write is available; an authenticated local clone and binary FBX retrieval were unavailable. Therefore:

- Python generator syntax and mathematical handle-classification assumptions can be checked locally.
- Actual Unity C# compilation, shader compilation, importer execution, renderer bounds, clip playback, weapon readability and Android performance are **not yet validated**.
- Run `Alterverse > Validation > Modern Hero Art` in Unity after import, then check every class front/side/back, idle/run/turn/attack/cast, all shop variants and death/respawn.
- Confirm Traveler has only the orb and orbit rings, the muzzle is centered, and the closed existing hand pose remains acceptable beneath the floating orb.
- Check finger plate placement under all grips and attachment/shadow behavior. This detail pass is not a substitute for a reviewed Blender sculpt.
- Verify a current local user-tuned project matches the GitHub base before merging. Unsynced local adjustments are not visible to this review.

Keep this PR as draft until the engine and visual checks pass. Do not represent the full visual/movement overhaul as finished based only on this source review.
