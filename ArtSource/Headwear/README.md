# Headwear collection

Only headwear models, attachment and preview tools are changed.

| Model | Availability |
| --- | --- |
| BETA Trucker Cap | Existing premium Battle Pass level 50 reward; same ownership key and cosmetic ID 7 |
| UFO Test Hat | Existing 1-shard catalog item; same SKU, ownership key and cosmetic ID 8 |
| Wizard Hat | New prototype in the Unity fitting studio |
| Propeller Cap | New prototype in the Unity fitting studio |
| Mushroom Hat | New prototype in the Unity fitting studio |

The three new prototypes have editable Blender files, GLB exports, runtime-format mesh JSON and transparent 768 × 640 thumbnails. They have no purchase price or reward assignment yet. Previewing does not unlock them or write a profile. Network IDs are unchanged.

The BETA cap now has a structured cream front, embroidered BETA patch, an actual open mesh rear, cyan piping and a curved stitched visor. Its preview is compiled from the production C# mesh. All other thumbnails are rendered from the exact serialized geometry Unity loads.

## Attachment

The seat plane is centered over the upper ten percent of the measured head contour and aligned to its top. This replaces the old fixed 18% inset. The mesh is transformed into the real head bone's space and parented to that bone. No colliders, gameplay stats or animation loops are added.

## Unity validation

Open **DIMENSIONS FRONT → Headwear → Fitting studio**. Select each hero and each of the five hats. Rotate the view, tilt the head and enable wings. The camera frames the full hat and head, including the tall wizard hat.

**Check all 5 hats on all 10 heroes** checks 50 combinations for head attachment, seat alignment, mesh budgets, repeated equip, removal, colliders and parent stability under a head pose.

This structural check is not a visual clipping review. All ten real hero shapes, idle/running/attack poses and the Android build must still be inspected in Unity/on device. The isolated .NET fixture does not load real hero meshes.

## Rebuild and checks

From the repository root:

    python Tools/Headwear/Regression/run.py --dotnet-root /path/to/dotnet
    python Tools/Headwear/validate_assets.py
    blender --background --python ArtSource/Headwear/create_collection.py -- --root .
    blender --background --python ArtSource/Headwear/BetaCap/render_preview.py -- --source-root . --dotnet-root /path/to/dotnet --output Assets/Alterverse/Resources/Headwear/BetaCapIcon.png

Use Blender 5.0.1 and .NET 8. The collection script exports the three prototypes, then renders the UFO and prototype previews. The separate BETA baker uses the actual production mesh.

