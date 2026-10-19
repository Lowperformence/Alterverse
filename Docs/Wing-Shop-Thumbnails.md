# Model-rendered thumbnails for every wing

VOIDFALL, Plague, Beta Supporter and Deluxe now use rendered model thumbnails in the collection cards, matching the existing Angel and RGB treatment. Shop and inventory share the same cached image lookup. Original and unknown cosmetics retain the existing fallback icon.

| Cosmetic | Resource |
| --- | --- |
| VOIDFALL | `Wings/VoidfallIcon` |
| Plague Wings | `Wings/PlagueIcon` |
| Beta Supporter Wings | `Wings/BetaSupporterIcon` |
| Deluxe Wings | `Wings/DeluxeIcon` |
| Angel Wings | `Wings/AngelIcon` |
| RGB Wings | `Wings/RgbIcon` |

Each image is 384 × 240 with the same charcoal studio background, model framing, material lighting and bloom treatment as the new wing thumbnails. Locked items use the existing dimmed tint. Textures are loaded once per cosmetic and reused across IMGUI events; no per-card 3D camera or render texture is introduced.

The four legacy renders come from the **unchanged production mesh builders**, including their real material colors, energy lines, mirrored transforms and Plague toxin nodes. `LegacyIconExport.cs` captures the neutral built pose through the existing deterministic engine boundary. `render_legacy_shop.py` reconstructs that snapshot in Blender. The source snapshot and editable studio scenes are kept under `ArtSource/Wings`.

Regenerate from the repository root:

```
python3 Tools/Shop/PremiumWingsRegression/run_visuals.py --dotnet-root /path/to/dotnet --export-icons ArtSource/Wings/LegacyShopGeometry.json
blender --background --python-exit-code 1 --python ArtSource/Wings/render_legacy_shop.py
```

The render script also writes the four 384 × 240 Resources icons. Existing Unity GUIDs must be retained when replacing them.

Validation: renders inspected; all six production resource mappings, image existence, distinct paths, repeated-draw texture caching and fallback behavior checked with the existing collection fixture. The same purchase/equip/reward suite remains passing. Runtime wing geometry, 3D character preview behavior, IDs, prices and reward percentages are untouched. Unity/Android screenshot acceptance remains a device check.
