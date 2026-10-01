# Runtime audit and Android rendering parity

## Confirmed fixes
- Owned and shared runtime material copies for lane units, recall effects, landmarks and prototype primitives; cleanup with their owner.
- One shared Veyr trail material per hero with explicit destruction.
- Diamond Core avoids building hidden legacy guardian and floor visuals when the replacement systems are active. Floor resources are released when the match disappears; guardian renderer discovery no longer runs every frame.
- Added six missing asset meta files with stable, unique GUIDs.
- PC and Android pipeline assets now share visual settings, including render scale, HDR, color grading, shadows and light limits. Both renderer assets use the same Forward renderer and ambient occlusion feature, avoiding the previous PC Deferred / mobile Forward split. GPU resident drawer is disabled on both. Mobile quality now uses the same skin weights, anisotropy and base LOD bias as PC.
- The legacy material repair runs on both platforms. Unlit materials stay unlit, legacy textures are preserved and replacement materials are released with their renderer. Valid custom/URP materials remain untouched.

## Verification performed
355 C# sources passed tree-sitter syntax validation. The existing local variable named `required` in AndroidDeviceCompatibilityPostprocessor was normalized only in parser input to avoid a contextual-keyword parser false positive; its source is unchanged. This is not Unity compilation.

545 meta GUIDs checked: no duplicates. Literal Resources.Load references were checked against tracked resources. Pipeline and renderer assets compare identically after normalizing asset names and their own renderer GUIDs. Project color space was already Linear, with HDR display output disabled.

## Remaining verification and data candidates
Unity Editor, player builds and an Android device were unavailable. A matching PC/Android screenshot comparison, shader compilation, repeated-match memory profiling and Android GPU/frame-time measurements remain required. This change aligns rendering configuration; it does not establish pixel identity across displays or prove the reported darkness was solely configuration-related. Matching PC visual quality increases Android rendering cost, particularly ambient occlusion and shadows.

ProjectSettings.zip (about 7.2 MB) is outside Assets, and Assets/2222222.unity and Assets/_Recovery/0.unity are byte-identical tracked scenes. Only Assets/Scenes/SampleScene.unity is enabled in build settings. These backup candidates were retained because their recovery purpose cannot be excluded. No gameplay assets or backups were deleted speculatively.

Device check: use the same map, hero, camera and graphics option with automatic screen brightness disabled; capture lossless screenshots and compare UI colors, hero highlights, map fill light and effects. Run several matches and return to the menu, then check retained material/mesh counts and Android frame time.
