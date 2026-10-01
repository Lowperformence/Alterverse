# Hero pose and design correction — 2026-10-01

- Preserve preceding 35% reduction of gait speed (phase multiplier .39); no further global movement-speed change.
- Correct imported skeleton thigh direction during stance/recovery, recomputing ankle counter-rotation using the corrected hip angle. Legacy geometry keeps its existing sign. The imported thigh now places the foot forward at heel contact and sweeps it backward during stance.
- Add analytic two-bone guard fitting for Traveler, Veyr and Nyra using real shoulder/elbow/hand lengths. Remove Nyra's old fixed shoulder/wrist offsets. Nyra keeps a two-hand rifle hold through firing; Traveler/Veyr blend out of the guard during existing attack/cast/hit/dash delivery.
- Sivra Quell (Nyra): base move speed 6.2 -> 5.58 (exactly -10%). Range, attack interval and other combat values unchanged. New precision rifle silhouette (long barrel, brake, shoulder stock, scope, folded bipod); cyborg half-face armor and sensor eye.
- Veyr and Nyra: boot geometry width/length reduced 20%, height reduced 10%, ankle locations and leg lengths unchanged.
- Ione Vaska (Elyra): sealed elongated demon helm, great curved glowing horns, dark layered plate armor, pointed shoulders and blue armor accents following the later supplied reference. Existing violet attack VFX unchanged.
- Traveler: remove both old mask layers/visor. Open hood with visible face, dark armored coat and chainmail, silver detailing, violet chronolight following the supplied reference's costume direction.
- Tahla Venn (Mira): more defined waist/hip silhouette, softer lip detail, long blonde locks, white-gold priestess armor, flowing ivory panels and ornate staff. The optional hat was superseded by the user's later priestess reference and removed. Gameplay healing VFX remain green.
- Weapons remain separate skinned meshes for the current weapon shop.

Validation: C# syntax; FBX roundtrips for seven heroes with weighted/normalized vertices, body UVs, weapon meshes, muzzle and nine source clips. Blender guard solver checks for the three corrected heroes verify hand targets and visually inspect limb poses. These are studio simulations, not Unity screenshots.

Preview Veyr/Nyra/Traveler images use the guard solver simulation; other portraits use source-authoring poses. Source clips remain authoring aids: gameplay keeps the established motion retarget path. Unity compile/playmode/device checks remain pending; particularly verify rightward gait, rifle grip while firing, melee/cast blending and shop variants after import.
