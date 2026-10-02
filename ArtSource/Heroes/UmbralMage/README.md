# Ione Vaska / Pearl Mech

Rebuilt from the new user reference: pearl ceramic armor, a violet mechanical frame, compact multi-sensor helmet and antenna, broad segmented shoulders, luminous violet hip reactors and angular mechanical legs. Heavy proportions widen the chest by 20%, thicken the abdominal core by 38%, and increase arm/leg cross-sections by 25%/23% around unchanged joint axes. The former robe, ritual crossbar and staff are removed. A four-petal deployed arcane focus replaces the staff; its geometry is fixed in the deployed pose, not animated deployment.

Build with `python Tools/UmbralMage/build_umbral_mage.py` (NumPy, Pillow). Body and weapon retain separate mesh groups, existing renderer identities and 18 rig bones. Legacy Staff marker names remain for compatibility but now mark the focus grip, aperture and orientation.

Generator validates finite vertices/normals, triangle and bone indices, geometry budgets and separate body/weapon triangles. Front/back images are offline geometry previews. Unity compilation, live animation clipping, actual materials and performance remain unverified.

Additional front/rear shoulder shields and overlapping outer shoulder plates follow each upper-arm joint. Runtime mirrors the source focus onto the left hand, rotates its barrel forward and corrects reflected winding; source geometry previews still show the canonical weapon orientation.
