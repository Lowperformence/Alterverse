# Modern combat VFX pass

The shared HeroEnergy material now renders moving object-space energy filaments, a brighter volumetric center for orbs/seeds, and a distinct surface response for shields/runes. Each pooled effect supplies its normalized age; paused gameplay freezes the surface. Hero hues and dark portal cores are preserved. Warning slots disable the new shading and retain their old circle/hex meshes.

Combat contacts use a brief camera-facing feathered star, followed by each hero's existing class silhouette. Physical attacks scatter directional tapered sparks instead of tumbling shards. Ring/hex rims and rifts have feathered bright edges. All meshes are cached at pool startup; no attack builds a new mesh or material. Optional sparks continue to obey the adaptive VFX budget and reserved warning/core capacity.

Mira's healing cast and beneficial area palette are green, while offensive seeds retain their own palette. Veyr's Rift Scar remains a purple flying X. Instant bolt traces no longer receive the pool's global length enlargement, so their visual ends match muzzle/contact rather than overshooting the target.

Damage, range, hit times, cooldowns, target selection and controls are unchanged. The presentation still uses the existing 192-slot pool and shared Resources material. No new textures, dynamic lights, fullscreen distortion or additional render passes were introduced. Shader fragment work increases modestly; Android GPU measurement is required.

Validation: C# tree-sitter syntax; contact-ray triangle area; hitscan endpoint formula at five distances; spark trajectory/scale at 101 samples. Unity, shader compilation and player/device visual tests were unavailable. Check all seven heroes' basic attacks and four slots, moving-target projectiles, pause/resume, crowded combat, green heals and exact warning boundaries on PC/Android.
