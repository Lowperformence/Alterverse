# Map concept graphics pass — 20 October 2026

All 14 arena variants receive more grounded peripheral scenery, geological surface depth and explicit biome lighting. This pass targets the user's concept reference while keeping the combat court readable. It is a source update; it has not been built into an Android release.

## Production changes

- `PlanetLandscapeDetail`: layered rock shelves anchor each scenery pocket to the terrain; sparse mineral seams, larger mineral rubble, forest buttress roots and industrial service plates are combined into the existing four material groups per pocket. Earth and TOI-715 b rubble uses a mineral material instead of bark. Every new placement passes through the existing building-clearance check. There are no new gameplay components, lights, colliders or per-frame landscape updates.
- `PlanetArenaThemeRuntime`: Pyraxis basalt masses and spires use shared fractured meshes; Neptune coral uses a shared tapered profile. Owned meshes are released together with the theme's materials. Boss presentation and combat logic are unchanged.
- `DF_PlanetSurface.shader`: existing geological data contributes bounded, footprint-filtered surface relief; cliffs use triplanar geology; recessed mineral seams subtly vary roughness. This adds two geological texture reads for the stone/cliff mode and no additional ground texture reads.
- `DF_ScenerySurface.shader`: cavity depth and roughness follow the existing relief data, with no extra texture reads.
- `LunarLighting`: explicit profiles cover Moon, Mars, GreenPlanet, Noxara, Kryon, Earth, Pyraxis, Neptune, Dark00, DiamondCore, GJ504b, Wasp76b, Trappist1e and Toi715b. Warm/cool key and ambient fill distinguish biomes with one directional light. Dark00 retains its blackout. Disabled shadow quality explicitly requests no key shadows. Previous global settings and existing key-light state are restored on teardown.

The lighting uses Unity's three ambient colours: [RenderSettings](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rendersettings). No dependency or external asset licence was added.

## Validation

Run with a .NET 8 SDK:

```sh
python3 Tools/Visuals/ConceptLightingRegression/run.py --dotnet /path/to/dotnet
python3 Tools/Visuals/ConceptMapRegression/run.py --dotnet-root /path/to/dotnet-sdk-root
python3 Tools/Visuals/ConceptMapRegression/check_geology.py
```

The lighting fixture compiles the actual production component with engine substitutes: 14 profiles, 336 lifecycle scenarios and 11,426 assertions pass. It covers reused lights, created fallback lights, quality modes, disabled lights, settings restoration, repeated enable/disable and owned-resource cleanup.

The landscape fixture compiles the actual production landscape, arena-theme and fractured-rock components with other geometry/engine substitutes: 219 assertions pass. It checks all 14 variants, deterministic placement, clearance rejection, repeated builds, resource cleanup and at most 32 regional material batches. Theme checks cover shared meshes, normalized basalt bounds, tilted coral endpoint alignment, partial failure cleanup, retry suppression and world changes. Batch vertex checks include actual fractured rocks; other mesh factories are substitutes. It does not measure native GPU draw calls or frame time.

Numerical geological-gradient checks cover 4,096 tangent orientations/scales and 20,480 footprint cases, neutral data and grazing/mirrored triangles. The reconstructed broad gradient is capped at 0.28. The check reads constants from the production shader sources. Shader property names, material buffer layout and existing shadow/depth passes are preserved.

## Required native acceptance before release

Unity/URP shader compilation, Unity scene rendering and Android performance have not been tested in this environment. Open the current development branch in Unity, allow imports and verify the Console and the project's shader validation. Inspect every map from the gameplay camera, especially Pyraxis lava contrast, Neptune coral silhouettes, Dark00 darkness, DiamondCore transparency and both teams' combat telegraphs. Then run a full 4v4 match on representative Android hardware and compare frame time, memory and temperature against the previous build. A new client build is required to deliver these graphics to players.
