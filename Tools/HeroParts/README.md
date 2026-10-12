# Authored hero part redesign

This change replaces Sivra Quell's and Ione Vaska's complete heads, Tahla Venn's
shoulder armor and Nerok Abyss's jacket and trousers. The new geometry follows the
existing models' proportions, materials and bone contracts.

`build_parts.py` always reads the original assets from commit
`bd7756321a7ac0ac9e32275c67bc03461547d00e`; rebuilds do not stack replacements.
It removes only the requested parts and preserves every remaining runtime
vertex, normal, binding, weapon group and marker. Tahla's original skeleton and
animation curves are preserved in `Mira.blend` and exported to the game's
existing `Mira.fbx` resource. Nerok's new garment indices use the existing brown
fabric shader, while his arm accessories continue to use the existing renderer.

## Build and inspect

Use Python 3.11 with `bpy==4.5.14`, NumPy and Pillow:

```bash
python Tools/HeroParts/build_parts.py
python Tools/HeroParts/validate_exports.py
```

The GitHub Actions workflow performs both steps and commits the generated assets
to `art/hero-parts-redesign-20261011`. `ArtSource/Heroes/PartsRedesign` contains
editable Blender scenes, skinned FBX models, isolated OBJ/MTL parts, native
Blender geometry renders and the validation report. The three runtime geometry
resources retain their original material slot counts and existing animations.
The additional source FBX models use the unchanged authored rest-pose anchors;
the game continues to animate Sivra, Ione and Nerok with its existing hero rigs.

The renders inspect actual mesh geometry in a neutral Blender studio. They are
not Unity screenshots; Unity's shader details and procedural arm accessories
are applied by the existing appearance components at runtime. Validation checks
asset structure, untouched geometry, bindings and FBX imports. Run the Unity
menu and combat scenes before merging to check the final game lighting and
animated garment clearances.
