# Arena space backgrounds

The current implementation gives all fourteen arenas their own authored composition and a separate main-menu background. It replaces the formerly hardcoded shared `MapPanoramas/Universe` selection.

See [Cosmic backgrounds](Cosmic-Backgrounds-2026-10-29.md) for map identities, rendering, client resource budgets, import settings and validation. See `ArtSource/CosmicSkies/manifest.json` for artwork provenance and prompts/briefs.

The images are 2:1 camera-facing matte paintings, not seamless equirectangular maps. `CosmicSky.shader` preserves aspect ratio and crops to the viewport. `PhotoArenaPanorama` owns only the gameplay-camera skybox and restores its former state. Lighting, hero materials and gameplay do not change.
