# Visual and audio modernization — 9 October 2026

Base: `9762bd4907838da8404754aadb4f06645bfadf56`, `dev/vertical-slice`.

## Scope and preservation

Inventoried all 455 existing C# files, the rendering configuration and relevant surface/effect shaders. This is a source audit, not an exhaustive game/device test. The existing project already uses Unity 6000.6.1f1, URP 17.6.0, linear colour, HDR, SRP Batcher, Vulkan with a GLES fallback and authored roughness/normal detail. Modernization therefore targets concrete presentation and runtime weaknesses rather than changing engine/package versions merely to increase their numbers.

All user-selected MP3s and their import settings remain untouched. All other audio assets, selected-recording mappings (`HeroCombatAudio`), music, selected bomber recording, model meshes, textures, map geometry, weapon fits and the previous grounded/upper-body movement changes remain unchanged. Only the tower's visual `SpawnShot` method and its now-unused visual block fields change; targeting, range, damage, cooldown, sounds and authority stay identical. Existing prototype beam callers retain their public entry points and durations.

## Implemented

| Area | Change | Boundary |
| --- | --- | --- |
| Energy surfaces | Curved filtered plasma filaments and radial rim depth; stationary shield hex cells with an activation band; botanical veins; brief coloured hot release. | Original inherited opacity is preserved exactly. Dark portal hue is not replaced with white. Warning detail/polish remains zero. |
| Device detail | Battery/overload uses the cheap surface branch; balanced/performance enables differentiated detail. | No new textures, scene colour/depth reads, screen distortion, dynamic lights, material copies per attack or render passes. Actual GPU cost still needs measurement. |
| World attack traces | Tower shots and the old prototype Beam route use the existing 192-slot pool instead of per-shot cube/collider/TransientScaleVfx objects. | Bolt local Z −1.4…+0.55 is fitted to muzzle/contact. Width compensates the pool's 1.5 cross-section scale; length bypasses that enlargement. |
| Pool priority | Core effects can reclaim a decorative slot when saturated. | 32 warning slots are never reclaimed; decorative emissions cannot displace cores. The 128/32/32 capacity partitions remain. |
| Effect rotation | Detect the all-zero optional quaternion by components. | Unity's rotation equality operator uses a near-one dot product and cannot recognize an all-zero sentinel. Ordinary authored rotations remain intact. |
| Quality stability | Two seconds of consecutive overload below 82% of target; four seconds of consecutive recovery at 94% or more. Reset on focus/enable/target changes. | Controls presentation only. Monitor ignores gameplay pause; desktop uses its own requested frame rate rather than a mobile preference. |
| Audio mix | Smooth 24 ms attack / 180 ms release toward the existing 0.55 duck gain. Validate PlayAt cue index. | Critical voices remain unattenuated; clip assets, routing, pitch variation, timing, gain targets and music are preserved. No new layers or synthetic sounds. |
| Bomber audio | Retain the old component identity but disable its obsolete auto-install/scene-scan/private-field reflection bridge. | Director already loads `Audio/BombJetFlyby`; flyover already owns the selected recording, loop, mix and pause behavior. No recording or playback rule is replaced. |
| Scenery | Leaf finish receives bounded 0.065 main-light backlighting with real shadow/distance attenuation. | All non-leaf materials use zero transmission; no map geometry, palette or collision changes. Additional leaf fragment work requires device profiling. |
| Legacy API | Veyr fallback grip refresh uses unsorted FindObjectsByType with inactive objects included. | Discovery set, refresh cadence and all offsets remain the same; authored rigs still bypass the fallback. |

## Executed verification

- Portable Mono: **9,274 assertions**. Production VFX scheduler, Show/Draw/Trace, cancellation and lifetime methods compile and run; only the startup local-function mesh factory is replaced. Unity objects, renderers/Resources and engine transforms are substitutes. Tests cover saturation/preemption, warning partitions/detail/size, zero quaternion, pause/delay/expiry, 1,000 repeated traces with fixed pool child count, 500 arbitrary 3D endpoints/width fits, quality thresholds at 30/60/120/144 Hz, actual monitor target/pause/reset behavior and audio-envelope monotonicity/frame-rate stability. This is not native Unity allocation or render validation.
- HeroEnergy HLSL: original vertex/fragment source compiled to SPIR-V, validated, converted to GLSL and rendered with EGL/Mesa llvmpipe. **336 render cases** across four controlled mesh fixtures, ages, detail levels and two sizes. Output finite, maximum warning RGBA difference **0**, maximum alpha difference **0**. Only Unity Core matrix/view helpers are replaced; fixture omissions and engine limits are recorded in the test README. The comparison PNG is an isolated material study, not a game screenshot.
- Scenery forward HLSL: **four compiled/validated stages**, regular and screen-space shadow branches, with substitute Unity lighting types. The bounded leaf response is also checked at 300 parameter combinations; this mathematical check is not a rendered plant/real shadow test.
- Changed/new C# syntax checked. Repository-wide parser inventory found one unchanged contextual-keyword parser issue at `AndroidDeviceCompatibilityPostprocessor` (`required` local variable); that construct is valid C# and is not altered by this pass.
- Git tree delta must contain only the documented presentation/test files. All audio assets and import metadata, selected-recording bank, gameplay ability code, model/map assets and prior movement code must match the parent blob SHAs.

Unity is unavailable in this environment. Full project import/compilation, Unity shader variants/stripping, real in-game sight/listening tests, Android Vulkan/GLES drivers and GPU/frame-time measurements are **not** claimed.

## Unity and device verification

Run `DIMENSIONS FRONT/Checks/Visual modernization materials and budgets`, `Selected hero sound recordings`, existing `Planet scenery`/`AllMapAssetValidation` checks and `Release/Ensure Android runtime shaders`. Then inspect all hero casts/basic hits, dark portals, shields, healing colours, crowded tower/bunker/trench/boss shots and pause/resume on a representative Vulkan device plus a GLES device. Warning outlines must still match gameplay bounds; munitions should end at contact. Listen to warning duck/release while the original selected recordings play. Check foliage under direct light and shadow.

For before/after GPU/CPU comparisons use the same saved match, camera and quality setting: warm up, record 60-second combat segments including a crowded fight, and repeat after a thermal soak. Compare median/95th-percentile frame time, transparent overdraw, SetPass/draw calls, GC allocation and memory. No FPS gain is asserted from the isolated rendering or substitute-engine tests.

## Further techniques, with project-specific gates

| Technique | Current assessment | Concrete next verification |
| --- | --- | --- |
| GPU Resident Drawer / BRG | Existing Forward path and per-renderer VFX property blocks are not an eligible automatic conversion. Vulkan-only eligible opaque scenery is the useful candidate. | Profile an isolated scenery branch with Forward+, compatible DOTS-instanced shaders and shader variants retained. Keep a measured GLES fallback. Do not convert the combat pool blindly. |
| TAA / STP upscaling | Requires trustworthy motion vectors, history and small-character/UI clarity. Current custom skinned surfaces/visual offsets need motion-vector validation first. | Compare moving heroes, weapon edges, transparencies, warning rings and camera cuts at native versus reconstructed resolution; reject ghosting or loss of readability. |
| More pooling | Prototype rings/bursts and certain static/generated scene details still create objects. Startup scenery construction alone is not proof of a frame hotspot. | Profile allocation traces; migrate frequent bursts/rings with their exact radius/sequence and warning priority, rather than replacing all scene construction. |
| UI migration | Several live interfaces use IMGUI and reflection helpers. A wholesale rewrite could change touch areas, safe-area layout and settings. | Measure first; retain the existing control flow and layout in a separately tested Canvas/UI Toolkit migration. |
| Frame pacing / thermal adaptation | Android Frame Pacing, AGI/Perfetto and ADPF are relevant; frame-pacing state is not established from the current serialized settings. | Read the actual Unity player setting, capture device traces and introduce only device-supported, measured policy. Keep selected recordings intact. |
| Audio middleware/spatial plugins | Current pooled sources already preserve production/selected assets, player priority and camera-relative panning. Replacing this would not automatically improve the mix. | Listen/profile voice saturation and routing first; add native mixer tooling only if it solves an observed issue without altering selected recordings. |

Official references checked on 9 October 2026:

- Unity 6.6 [GPU Resident Drawer requirements](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer): Forward+/Deferred+, compatible compute APIs, no OpenGL ES or MaterialPropertyBlocks.
- Unity 6.6 [URP rendering path comparison](https://docs.unity.com/en-us/engine/6000.6/manual/render-pipelines/universal-render-pipeline/introduction/urp-concepts/rendering-paths/comparison) and [performance settings](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance).
- Unity 6.6 [Quaternion equality](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/quaternion/op-equality).
- Android [game optimization tools](https://developer.android.com/games/tools): frame pacing, AGI and Perfetto.
