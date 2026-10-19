# Angel and mechanical RGB Wings

Both wings are wired into the existing collection, ownership, per-hero equip, 3D menu preview, gameplay visual builder and match-reward snapshot. Existing IDs 0–4 remain unchanged; Angel is 5 and RGB is 6. The existing internal `RgbRiftWings` ID/SKU is retained, while the player-facing name is **RGB WINGS**.

| Wing | Shards | Match XP bonus | Match shard bonus |
| --- | ---: | ---: | ---: |
| Angel Wings | 11,000 | 27.5% | 27.5% |
| RGB Wings | 15,000 | 37.5% | 37.5% |

The existing ratio is 400 shards per percentage point. Fractional percentages remain intact until the final whole reward is rounded. Purchases use the existing atomic journal/account-ledger path, do not equip automatically, and cannot charge twice. Receipt retries retain their original wing ID and reward totals.

## Model source and runtime

`ArtSource/Wings/ApprovedWings.blend` contains the editable native scenes, five-bone rigid rigs, idle clips and explosion controls. `AngelWings.fbx` and `RGBWings.fbx` are exported source assets. The Angel parts match the previously approved model exactly.

The new RGB model has eight layered anthracite metal blades per side, gunmetal bevels, spars, hinge plates, bolts and inset light strips. Only the narrow channels cycle through RGB hues. Metal remains dark. The six-second cycle is continuous, synchronized across the pair and uses no lights or particles. Gameplay follows scaled time; menu previews use unscaled time.

`PremiumWingParts.json` is the shared geometry source. `build_mechanical.py` rebuilds only RGB and retains Angel; `build_meshes.py` bakes both models to the Resources mesh asset. Runtime loads that exact baked geometry without requiring Blender or FBX import. Left/right share one wing mesh; the fixed mount uses another. Each equipped pair uses three renderers and three owned materials, no colliders, and releases meshes/materials when destroyed. Rendered geometry, including opposite-facing triangles, is 5,432 triangles for Angel and 6,296 for RGB. Shop thumbnails come from the actual Blender models.

Build source with Blender 4.x:

```
python3 ArtSource/Wings/build_mechanical.py
python3 ArtSource/Wings/build_meshes.py
blender --background --python-exit-code 1 --python ArtSource/Wings/create_blender.py -- --write-blend --export-game --render
```

## Validation and limits

- Native `.blend` reopened and checked: both source meshes, five-bone rigs, weights, idle clips and explosion drivers pass. Models were rendered and visually inspected.
- Mesh checks pass: finite coordinates, valid indices, no degenerate triangles and 16-bit index budgets.
- Production presentation compiled under deterministic engine fixtures for all ten heroes in game/menu modes: construction, shared side meshes, material separation, RGB cycle and resource destruction pass.
- Production collection/equip checks and 16,018 bonus assertions pass, including exact balance, insufficient funds, duplicate purchase, saved ownership, all ten hero slots and match snapshots.
- Real reward-service/journal receipt tests pass for Deluxe, Angel and RGB, including half-percent totals and idempotent retries.
- Four SQLite premium-wing ledger tests pass, including earned/paid splits, persistence and concurrent retry. A broader old ledger/profile run is blocked by missing Google Firestore Python dependencies in this environment.

Unity Editor, GPU shader compilation, native attachment/clipping on all heroes, live commerce and Android device acceptance are not verified here. The new commerce SKUs require deployment of the updated shared `ShardCatalog.json` with the account service; a client rebuild alone cannot add SKUs to an already-running service. The existing multiplayer cosmetic-replication behavior is unchanged.
