# Defect list review — 2026-10-01

Reviewed branch dev/vertical-slice at 37621e1e0155294cf926d3ec9c92b270fbc8a7bc. Static inspection only; Unity and an Android device are unavailable in this workspace.

## Findings and changes

- **Empty scene / build blocker: not confirmed.** EditorBuildSettings enables an existing SampleScene. MainMenuController.AutoCreate and PrototypeBootstrap.AutoCreate both use RuntimeInitializeOnLoadMethod(AfterSceneLoad). The menu is created automatically; the bootstrap waits for the menu to close before creating the arena, player, HUD and roster. An empty authored scene is intentional in this architecture. No scene replacement was made.
- **Touch stops when held: not confirmed.** PrototypeHUD runs at execution order -200, before HeroMotor (-25). HandleTouches processes active touches including stationary touches, calls SetMoveInput every held frame, resolves movement before combat release and clears input on touch release/cancel, missing touch, lost focus, pause and disabled HUD. The frame-based HeroMotor contract remains unchanged.
- **Duplicate ability preview: confirmed and fixed.** CreateLocalPlayer added AbilityRangePreviewOverlay twice. Removed the second addition; the HUD's existing conditional addition remains.
- **Mobile quality affects desktop: confirmed and fixed.** MobilePerformanceProfile previously applied on every platform. Apply now returns on non-mobile platforms.
- **Mobile runtime quality did not control URP shadows: confirmed and fixed.** The previous code changed Built-in QualitySettings.shadowDistance and antiAliasing. URP uses its pipeline asset for these properties. Mobile quality now clones the active URP asset once, changes its shadow distance/cascades/MSAA, and retains the source asset's color/HDR/lighting configuration. The clone avoids editing project assets in play mode.
- **Heavy Android defaults: confirmed and reduced.** Mobile URP had four cascades, 50m shadow distance, 2048px main/additional shadow atlases and high soft-shadow quality. New defaults: two cascades, 28m distance, 1024px atlases and medium soft shadows. Mobile QualitySettings LOD bias is .85 and streaming enabled with a 256MB budget. Desktop quality block is unchanged. Runtime tiers use 18/28/38m, 1/2/2 cascades and 128/256/384MB streaming budgets. Streaming applies only to textures imported with streaming mipmaps; no claim of measured RAM savings.
- **Packages: required integrations retained.** Koop scripts use Netcode/Unity services; CoopVoiceChatService uses Vivox; GooglePlayShardStore uses Purchasing; GooglePlayLevelLeaderboard resolves GooglePlayGames. URP and Input System are used throughout presentation/input. Package count alone is not evidence of an unused package.
- **Unity version: informational.** 6000.6.1f1 is not itself a demonstrated defect. No editor/package upgrades performed.

## Verification and remaining checks

C# syntax and targeted source/settings invariants checked locally. Seven-hero assets and movement from the preceding commit were not changed by this pass.

Still required in Unity: compile, launch from SampleScene into menu, start a match, hold joystick stationary while attacking, release/cancel and background/resume. On a device, switch quality tiers and measure GPU time/RAM/thermal behavior; compare HDR, colors and exposure with PC. Static settings changes are not proof of a successful Android build or identical rendered output.

References: Unity 6 quality-setting location documentation states URP shadow cascades/distance and MSAA belong to the URP asset; mipmap streaming remains in QualitySettings.
