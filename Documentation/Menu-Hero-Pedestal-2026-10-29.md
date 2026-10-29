# Menu hero pedestal and sole placement

The menu previously placed every preview actor at its model origin with no visible support underneath. Imported model scales, foot geometry and idle poses differ, so the heroes appeared suspended over the cosmic background.

`MenuHeroShowcase` now owns one reusable sci-fi pedestal on portrait layer 31. A graphite beveled shell, slate deck, fine cyan rim strips and restrained panel seams provide a visible stand. Two small contact patches sit directly beneath the boots. The geometry uses three deck materials and one shared contact material, no colliders, extra camera, extra lights, image textures or gameplay registration. The top is a single flat plane at stage-local Y = -1.05. The diameter adapts to the selected actor's measured feet.

Each cached preview retains a `MenuHeroGrounding` instance. It identifies the actual left and right feet, including imported `FootL/FootR`, underscore/dot variants, Mixamo names and procedural ankle/boot fallbacks. It measures only visible, referenced triangles weighted to those feet or rigid footwear beneath them. Weapons, capes, wings and unused skin vertices cannot determine the floor. Skinning is reconstructed from bind matrices and current bones; `BakeMesh` and whole-body renderer bounds are deliberately avoided because imported scales and low-hanging accessories can give inappropriate stand heights.

The lower footprint envelope is cached in at most 64 support points per foot. Subsequent frames reuse those points without reading mesh arrays or generating textures. The preview's existing arena grounding is disabled in Update, before pose evaluation, so arena raycasts and persistent world contacts do not compete with the menu's flat deck. After the idle pose, the menu aligns the body and uses the existing two-bone solver to bring both soles onto the same surface while retaining the foot orientation. All changes apply to the isolated preview clones.

The showcase executes at order 1200, after BOLT-7's authored pose at 1100. Camera fitting then includes the complete pedestal bounds, including its base and outer rim. A slightly higher camera angle reveals the top surface. The same pedestal is used on Home, hero selection, inventory and shop previews, and is hidden with the stage. Owned meshes and materials are released on showcase destruction; cached heroes retain their measured soles when revisited.

## Verification

```sh
python3 Tools/UI/MenuPedestalRegression/run.py --dotnet-root /path/to/dotnet
python3 Tools/UI/MenuPreviewRegression/run.py --dotnet-root /path/to/dotnet
```

The pedestal regression compiles the complete production showcase, grounding and deck classes against affine Unity API fixtures and the production two-bone solver. Synthetic cases cover eleven roster entries, different scales and naming conventions, asymmetric foot heights, nonuniform imported scales, the remote portrait stage at 12000 units, low weapon outliers, unused foot-weighted vertices, repeated turning, stable mesh reads, mesh winding, deck coverage, full camera framing, cache reuse and cleanup. Geometry checks include indices and triangle orientation; the count is not a measure of native model coverage.

The existing preview regression additionally checks real imported origin-background PNGs and production selection/cache behavior. Native Unity import, actual FBX/runtime-model sole placement and Android rendering remain to be visually checked. In Unity, cycle through all eleven heroes and cosmetics, check heel/toe contact and the whole rim, switch pages, hide/reopen the menu, and confirm that the pedestal is absent from gameplay. These are client presentation changes and require a new client build, without a dedicated-server image update.
