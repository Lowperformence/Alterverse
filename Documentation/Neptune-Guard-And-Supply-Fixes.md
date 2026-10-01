# Neptune guard and supply drop review

The Neptune base healing guard now wears a rounded deep-sea pressure suit with a circular brass helmet, viewport, twin air tanks, respirator hoses, weighted boots and team beacons. Its existing health, attacks, collider and single locomotion controller remain in use. Other maps and lane mechs retain their models.

## Supply drop findings

- The crate and parachute relied on Shader.Find rather than the Resources-backed runtime shader. Missing build shaders could prevent crate creation or leave invisible/error visuals. Both now use RuntimeShaderLibrary.Lit.
- A failed crate creation previously set issued=true forever even when no inventory item was consumed. It now retries once per second while the selected item remains available.
- All blocked landing candidates already caused retries, but the HUD offered no explanation. A waiting-for-clear-landing status is now shown.
- Private co-op explicitly disables drops because inventory consumption is not synchronized. This remains unsupported and is now shown in the HUD.

Expected solo flow: own AND equip an item before the match; drop begins at 120 seconds of match time, descends for 4 seconds, then any living allied hero within 1.35 units may collect it. Allied AI heroes are intentionally eligible. Buying without equipping does not schedule a drop. Inventory is consumed once on successful spawn, not on pickup. Victory rewards are a separate 30% roll.

Validation: changed C# files pass tree-sitter syntax parsing. Source flow reviewed for spawn failure, inventory cancellation, blocked landing, pause and co-op exclusion. Unity compilation, Android build and play-mode checks are still required; unavailable in this workspace.
