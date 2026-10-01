# Straight forearms and independent weapon grips — 2026-10-01

The imported hand retarget binding previously combined the WristJoint control and Weapon control. Veyr's blade guard angles therefore twisted the anatomical hands and neighboring skin. The analytic guard solver also forced a world-space hand orientation even when the forearm pointed elsewhere. Source clips repeated that forced wrist orientation.

Changes:
- All seven FBX/Blender rigs now have 19 bones: 17 anatomical bones plus WeaponGripL/WeaponGripR child bones anchored at the hands.
- Hand bindings receive wrist motion only. Weapon guard/cast angles target the independent grip bones after arm pose solving.
- The guard solver restores the neutral local wrist, so hands follow forearms. Source-authoring clips use the same neutral wrist rule.
- Body skin cannot be influenced by weapon-grip bones. Weapons skin to their corresponding grip; Brakk's gauntlets stay attached to the anatomical hands.
- Muzzle sockets follow the weapon grip (Brakk uses the hand), and the named separate weapon renderers remain compatible with the shop.
- Existing hero designs, gait speed reduction, Sivra's movement speed and gameplay/VFX are retained.

Verification:
Seven FBX round trips check 19 bones, parenting, normalized weights, shop meshes, clips, neutral idle wrist angles (<5.2 degrees from rest relative to the forearm), and coincident hand/grip anchors (<5mm in Blender source units). They also reject any body influence from weapon-grip bones. C# syntax is checked. Three guard poses are simulated and rendered in Blender for visual inspection.

Unity compile/playmode validation remains pending. Test all seven heroes in menu and gameplay, especially weapon grip during attacks/casts, rifle aim, and weapon-shop colorways. Studio portraits are Blender pose simulations, not Unity screenshots.
