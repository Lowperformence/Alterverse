# Ione redesign and seven-hero hand/weapon fitting

Ione Vaska now follows the latest reference direction: a visible adult female elven face, long blonde hair, pointed ears, a fine silver diadem, emerald eyes and fitted violet-gold garments. The sealed demon helmet, horns and floating shoulder relics are removed. Her boots are slimmer. Existing ability behavior and violet attack effects are retained.

All seven imported heroes receive class-specific neutral arm targets. Forearms retain a neutral wrist; the established gait, body movement and action delivery still run through HeroVisualMotion and HeroProceduralRig. The previous independent world-space weapon rotation is removed: weapons inherit the anatomical hand's movement, with their actual handle centers fitted to palm anchors. Modeled curled fingers and opposing thumbs replace the folded base-mesh fingers on held weapons; these hands belong to the body renderer, so weapon-shop colorways cannot recolor them. Brakk's armor gauntlets are fitted around his fists.

Sivra's rifle is angled toward her shoulder and has a reachable foregrip socket. The supporting hand uses the socket's live position after the main hand and rifle have moved, with two palm-offset corrections.

Validation completed:

- Seven FBX round trips: 19 bones, normalized skinning, body UVs, expected separate shop weapon renderers, muzzle sockets and all nine authored clips.
- Sampled exported clip frames: each weapon remains rigid relative to its hand; handle/palm anchor error remains below 0.002 m. The measured maximum is approximately 0.000000382 m.
- Seven Blender simulations of the runtime neutral guard: handle/palm contact passes; Sivra's supporting palm is approximately 0.004614 m from the foregrip after two corrections.
- Python compilation and C# syntax parsing pass.
- Preview/*.png are Blender renders. *-GripCheck.png and Lineup.png show the simulated neutral guards, not Unity screenshots.

Unity is unavailable in this environment. Unity compilation, live running/casting/attack transitions, weapon-shop preview/equip and Android rendering still require in-engine verification. This change does not claim those checks passed.

The publish uses the latest remote tree as its base and preserves concurrent changes outside the explicit hero files.
