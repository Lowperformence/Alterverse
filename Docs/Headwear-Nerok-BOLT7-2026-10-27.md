# Nerok and BOLT-7 headwear attachment

Nerok's hat measurement previously treated the uppermost cranial fin as his
crown. The fin shares his Head weights and skin material, so excluding only
ornament/halo materials did not help. The source geometry has a skull apex at
1.95 and a fin apex at 2.075; the old upper-20-percent threshold (1.9728) contains
no skull points. These are authoring coordinates, not a Unity world measurement.

`HeadwearPresentation` now measures the largest connected, head-weighted surface
of the generated `DF_Nerok_Siege_0` mesh. Normal/material seams are joined by
identical positions for this measurement. Surface area chooses the component,
so adding subdivisions to the thin fin cannot make it win over the skull.
Existing SkinOrnaments/HeadAdornment mesh suffixes are accepted. All other
hero meshes keep their previous measurement path. The source mesh, materials,
weights, triangles and visible fin are not changed.

BOLT-7 builds its own rig and returns before the shared authored-hero setup.
`Bolt7Visual.Awake` now configures the existing headwear renderer after the
model, materials and initial grounded pose are ready. Preview selections and
per-hero local selection use the same renderer; later network cosmetics find
the initialized component. Hats are attached to the existing Head bone using
its mesh bind frame. No animation, wings, combat, account or audio behavior is
added or changed by this correction.

## Checks

From the repository root:

```sh
python Tools/Headwear/Regression/run.py --dotnet-root /path/to/dotnet
python Tools/Visuals/Bolt7Regression/run.py --dotnet-root /path/to/dotnet --presentation-only
```

The Headwear suite passes 15,455 controlled checks, including all five hats,
flat-shaded duplicate vertices, a fin with more subdivisions than the skull,
unchanged anatomy buffers, the unchanged measurement path for other models,
late equip under a changed Head transform and BOLT-style bind-frame scaling.
The BOLT presentation suite passes 11 controlled checks, including its actual
visual initialization path for preview, local and remote actors, plus the
existing gait, owner and relocation behavior. Its headwear boundary records
the production setup call; the Headwear suite separately runs the production
fitting code and the real five serialized hat meshes.

Negative controls with the previous production files fail respectively at
the fin-apex assertion and the missing BOLT headwear initialization assertion.
The fixtures do not emulate native Unity rendering or certify visual clipping.
The full BOLT resource runner still requires a complete checkout of the shipped
model/texture resources.

Before a client release, use **DIMENSIONS FRONT > Headwear > Fitting studio**
and the all-hero attachment check. Inspect Nerok and BOLT-7 with each hat, then
test idle, run, attack, death/respawn and equip/unequip. In an online match verify
a local BOLT-7 and a remote BOLT-7, including a late equipment snapshot. Repeat
on Android to check head/fin intersection and the final apparent sizes. No
Unity build, device run or live multiplayer session was performed here.
