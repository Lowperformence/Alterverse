# Per-map cosmic backgrounds

Every arena now selects its own authored space composition through `CosmicSkyProfile.ForArena(ArenaVariantRuntime.Current)`. The former implementation always loaded `MapPanoramas/Universe`. The main menu has its own fuller navy/cyan/violet composition: five distinct planets, additional moonlets, dense fine stars and a diagonal galactic dust ribbon.

| Arena | Visual identity |
| --- | --- |
| Moon | Silver lunar crescent, cold blue dust |
| Mars | Copper terrain, rust-colored nebula |
| GreenPlanet | Emerald/turquoise clouds and green world |
| Noxara | Violet dust and dark alien world |
| Kryon | Ice-blue frozen world |
| Earth | Blue oceans and cool white atmospheric rim |
| Pyraxis | Black volcanic rock and restrained molten fissures |
| Neptune | Cobalt gas giant, fine rings and small moons |
| Dark00 | Almost-black indigo, dim violet eclipse |
| DiamondCore | Silver/lilac crystalline world and dust |
| GJ504b | Rose gas giant and magenta dust curls |
| Wasp76b | Amber gas giant and golden iron-vapor haze |
| Trappist1e | Three rocky worlds, cool teal dust, dim red dwarf |
| Toi715b | Ochre super-Earth and muted brown/maroon dust |

## Rendering and ownership

The PNGs are 1774×887 camera-facing matte paintings, not 360° equirectangular sky maps. The URP shader preserves image aspect and center-crops to the camera viewport. Large shapes sit toward the outer image so the floating arena remains readable. No new third-party package is required. AI artwork provenance and available generation prompts/briefs are recorded in `ArtSource/CosmicSkies/manifest.json`; the original generated pixels are preserved in the runtime PNGs.

`PhotoArenaPanorama` installs a material only on the main camera carrying `MobaCamera`, excludes cameras rendering to textures, and restores the former camera material, enable state and clear flags when ownership ends. It never writes global `RenderSettings`, ambient light, scene lights, hero materials, exposure volumes or gameplay state. Existing arena meshes and gameplay remain unchanged.

One selected map texture and one shared 256×256 periodic noise-data texture are loaded at a time by the backdrop renderer. Textures use explicit shared leases so a menu fallback or another renderer cannot lose an image while still using it. Android imports use ASTC 6×6, mipmaps, non-readable textures and a 2048 maximum dimension. Art uses sRGB/clamp; numerical noise uses linear/repeat.

The shader adds a very faint drifting dust veil, sparse small glints and at most two short shooting stars. Their timing is deterministic, uses an unscaled clock, does not consume gameplay random state, and keeps fixed exposure. Map streaks last 0.95 seconds at intervals of 23–43 seconds for the primary streak and a longer secondary interval; the menu primary interval is 13 seconds. There are no raymarch loops, particle objects or additional scene lights.

The menu renders one opaque 1280×640 target at no more than 30 updates per second, with no video decoder or extra camera. Its first frame is completed before exposure to OnGUI. The same texture, crop and caller tint are retained across menu pages. Rendering pauses on app pause/focus loss, recreates a lost Android render target before displaying it, restores previous graphics state, and releases owned resources on disable/destroy. If the shader is unavailable, the new authored menu still remains available. Historical video/poster assets remain in the repository but the menu controller now uses `MenuUniverseBackground`.

`UNITY_SERVER` and batch mode skip graphics resources and render work. These are client visuals: create a new Android AAB from this branch; no new dedicated-server image or Edgegap configuration is required for these changes.

## Validation

Run with an installed .NET 8 SDK:

```sh
python3 Tools/Visuals/CosmicBackdropRegression/run.py --dotnet-root /path/to/dotnet
python3 Tools/UI/MenuBackgroundRegression/run.py --dotnet-root /path/to/dotnet
```

The production C# is compiled against Unity API fixtures for client and `UNITY_SERVER`. Checks cover all fourteen image selections, limited loading, shared-resource lifetime, camera ownership/restoration, deterministic brief effects, menu opacity/graphics-state preservation, frame cap, pause/focus handling, render-target loss and failures. Artwork validation checks the actual arena enum, distinct hashes, image dimensions and Unity GUIDs. The new suite passes 107 client and 3 server checks; the existing menu suite passes 60 client and 3 server checks.

`Tools/Visuals/CosmicBackdropRegression/shader_check.py` additionally compiles eight production HLSL entry-point/platform variants using minimal Unity helper shims and checks the compiled fragment math with Mesa. Its 90 checks cover all fifteen artworks, opacity, finite output, stable exposure, localized shooting stars, aspect crop and agreement between sky/menu fragments. `ArtSource/CosmicSkies/shader-preview.jpg` is an isolated shader preview, not a Unity screenshot. `shader-validation.json` records the renderer and limitations. This optional check requires glslangValidator, spirv-cross, moderngl, numpy and Pillow.

These checks do not substitute for a Unity import/build or Android device rendering/profiling. Before release, preview the menu and all maps at 16:9 and a wide phone aspect, pause/resume the app, and check texture compression, shader compilation and frame time on a target device.
