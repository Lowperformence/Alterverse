# Brakk Orun: bare upper arms, forearms and shoulders

All additional geometry on Brakk's upper arms, forearms and shoulders is removed.
This includes bronze/enamel bracers, silver shoulder plates, grey fabric-material
shoulder cages, heat fins, generated armor panels, bearings, elbow pistons,
stripes and fasteners. There is no fallback that lifts or preserves arm plates.

`BrakkForearmTrim` runs on Brakk's instance in the shared menu/gameplay creation
path. The connected anatomical body retains its vertices, UVs, skin weights and
existing skin/fabric materials and color treatment. Separate rigid arm components
that share a fabric material are classified before removal. Other added surfaces
are removed independently of their color or renderer name. Complete attachment
components are matched across FBX normal/UV seams, including blended shoulder and
wrist weights, so residual sides and back faces do not survive.

Rigid renderers attached directly to UpperArm/Forearm bones are disabled without
removing bones or pose controls. The hand/weapon-grip branches remain functional.
Nerok shares the Rook source model: cleanup is restricted to `HeroId.Rook` and
clones meshes/materials per instance. Source assets and other heroes are unchanged.

The procedural Brakk builder no longer creates shoulder armor, bearings, elbow
pistons or shoulder stripes. The Blender source builder also excludes every
non-anatomical UpperArm/Forearm object for Rook before skin binding and export.
Existing import/runtime limb-detail generators already skip Brakk's arms.

Pull outside Play mode and restart Play mode after compilation to recreate the
hero instances. Runtime filtering works on readable imported models without a
new FBX export. Unity/device rendered verification remains pending.
Validation: the production cleanup compiles and passes fixture checks for silver,
grey, bronze/enamel, dark and fabric-material attachments on both upper/lower arms;
merged anatomical/accessory meshes; normal/UV seams; blended accessory weights;
source mesh and body/material preservation; repeated application; and Nerok/hand
isolation. The pre-change cleanup fails on retained silver shoulder plates. C#
syntax and Blender-generator Python syntax/filter checks also pass. The fixture
uses Unity stubs; it is not a rendered Unity acceptance test.
