# DIMENSIONS FRONT — ingame hero geometry pass

Uses the existing runtime hero builder; no concept image or external franchise asset is substituted for an ingame model.

| Hero | Changed silhouette and materials | Combat signature |
|---|---|---|
| Zhaev Null | Asymmetric shoulder mantle, two unequal tapered coat panels, angular pale mask, swept forearm blades; charcoal with limited violet | Short slash strokes, narrow dash trace, delayed marked finisher |
| Auren Kest | Broad pale shoulder wedges, tapered chest panel, full-height irregular polygon shield with inset color; no solid cyan shield slab | Hexagonal ground construction, real temporary shields, short stun |
| Sivra Quell | Narrow torso, sloped visor/brow, single side cape; long dark rifle with distinct stock, rail and pale barrel | Thin straight shots and four-shot ultimate volley |
| Ione Vaska | Tall split robe, angular floating prisms instead of round orbs, wide collar, restrained pale head | Diamond glyph inside an exact circular damage boundary |
| Tahla Venn | Pale split coat and upright paired projector fins; pentagonal staff emitter instead of floating crown/ball | Petal-like twelve-point glyph, ranged healing/shield links |
| Brakk Orun | Low broad chest, oversized right gauntlet with separate knuckles, smaller left arm, off-center exhaust | Heavy strike pose and octagonal ground impact |

Hero palette remains the same across teams. ZERO/LUMEN use a consistent shoulder insignia and small functional light details, rather than repainting the whole body. Neutral dark cloth, light armor and one hero accent separate major shapes. Existing hero-team HUD markers remain.

## Mobile cost / lifecycle

- Convex extruded contour meshes for capes, shields, mantles, blades and armor panels; no primitive collider or placeholder host renderer is created.
- Materials are reused by exact palette color within each hero, rather than allocated per part. Static body surfaces combine by material; animated limbs and signature parts remain separate. No skinned-mesh requirement or imported rig is added.
- Shared ability line material; thin geometry and limited strokes replace hero ability particle clouds. Exact damage-radius warnings remain visible until resolution. Hero attack poses respond to actual basic/skill execution for players and bots.
- Founder cosmetic is a small attached chest insignia. Its floating cylinder/cubes over the hero are removed. No entitlement or purchase logic changes.
- Per-hero meshes and owned materials are destroyed with the hero. Existing renderer-state respawn fixes remain untouched.

## Checks and limits

Twenty authored contour outlines passed convexity checks, allowing the fan triangulation to remain valid. C# syntax and diff checks pass. The Unity regression runner now checks all six models for nonempty geometry, absence of host renderers, at most 24 mesh renderers and at most 6 palette materials after batching.

**Unity compilation, these runtime mesh-budget checks, visual inspection from the actual game camera and Android profiling have not run here.** The numeric budgets are assertions to run, not measured device results. This remains an authored procedural geometry pass, not final production character art or a full skeletal animation set. A camera-distance screenshot comparison is required before claiming a visual quality target was met.
