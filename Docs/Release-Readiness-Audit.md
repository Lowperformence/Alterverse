# DIMENSIONS FRONT — release readiness, 2026-09-18

Status: **NOT A RELEASE CANDIDATE**. Source work is implemented; runtime quality and release gates below remain open. Baseline: `68d9f34120df1aa53a16e706f135b4bdb9fafa7c` on `dev/vertical-slice`.

## Implemented

- One runtime owner of `Time.timeScale`: independent menu, opening, result, transition and background pause reasons. Finishing hit-stop cannot release another pause. Repeated impact pulses restore normal speed rather than preserving the previous slowed speed.
- Persistent lifecycle handler pauses gameplay and audio on focus loss/background; opening countdown waits for foreground. Match rebuild releases its transition flag in `finally`.
- Match reward compatibility service delegates to the profile service, restoring lifetime statistics previously bypassed by namespace shadowing. XP, shards, statistics and a stable per-match receipt are written together through a replayable absolute-value PlayerPrefs journal. Save exceptions retry the same receipt without rolling another reward after successful recovery. This is local persistence, not a server authority or guarantee of filesystem durability.
- Avatar debit, ownership and equipped avatar share the same journal. Existing profile keys and economy are retained. Malformed pending journals are quarantined without clearing the profile. Wallet addition saturates rather than overflowing; loaded audio levels reject non-finite values.
- Siege state resets for each match. Planet ground-beacon scale is calculated from its rest scale, preventing cumulative size drift.
- One safe-area-aware match header for timer, kills and control replaces overlapping header implementations. Result/respawn panels use safe-area coordinates. Frontend actions trigger the existing UI audio policy; release builds no longer auto-create audio-test/performance-debug objects.
- Android application ID candidate `com.lowperformence.dimensionsfront`, IL2CPP/ARM64 retained, target SDK pinned to 36. A Unity editor release gate and signed AAB build entry point reject missing scenes, template ID, unsigned configuration, non-ARM64/IL2CPP and unsuitable target SDK.

## Verified here

- Tree-sitter C# syntax parse: 185 source files, zero syntax errors. This is **not a C# compilation** or Unity API compatibility check.
- `git diff --check` passed.
- Runtime scripts have exactly one time-scale assignment owner. Existing editor regression utilities may set time scale for their probes.
- Byte comparison against baseline preserved HeroCombatProfile, HeroAbilityKit, DimensionsFront rules, DimensionPlanetDirector spawn schedule, PlayerExperience progression, ProductionLunarGround and the parallel developer's MobaBotMover fix.
- Source review of reward call paths, journal replay, lifecycle pause ownership and per-match siege reset. No actual storage interruption was simulated on Android.

## NOT VERIFIED — mandatory runtime acceptance

| Area | Required evidence before release |
| --- | --- |
| Build | Unity 6000.6.1f1 import/compile with no errors; actual IL2CPP ARM64 build succeeds. Editor build gate itself must compile and run. |
| Movement | Run `DIMENSIONS FRONT/Checks/Movement collision and dash (Play mode)` and existing bot checks. Test all six heroes, diagonal analog input, wall sliding, dash collision/CC/death, respawn, 15/30/60 FPS. |
| Full match | Complete two consecutive matches in one session. Verify 1 player + 9 bots, bot respawn exit, opposing minion combat, towers and objectives. Check siege at 6:00 with 10-second warning and four mechs again in match two. |
| Win rules | Test 100% control, vulnerable-core destruction, timer lead, overtime and sudden death; each yields one result and one reward. Core stays protected at own control >=20%. |
| Objectives | Test 2:00, 5:30 and 8:30 planets, capture influence/buffs and readable defensive attacks; beacon never grows cumulatively. |
| Persistence | Record XP/shards/stats, finish match, return home, restart app. Buy avatar once; verify debit/ownership/equip. Inject save failures and process termination at journal-save/apply/delete boundaries. No double credit/debit on replay. Corrupt a test journal, confirm profile retained. Use disposable test profile. |
| Pause | Background during opening, dash, hit-stop, result and launch. Resume never bypasses remaining pauses. Repeated hit-stop recovers to normal speed. Test Android keyboard and focus callbacks together. |
| UI | Inspect actual 16:9, 20:9, tablet and cutout devices. Validate thumb targets, all six hero selections, name editing, cosmetics, settings, result and return-to-home. Remaining PrototypeHUD and onboarding overlays need a visual/mobile acceptance pass; no claim that all HUD is final. |
| Presentation | In-engine review of all heroes, run/attack animation, minions, objectives, lunar arena, materials and VFX. Procedural character/prop construction is not evidence of final art quality. Authored animation/asset needs remain subject to this review. |
| Audio/performance | Listen on device; measure overlapping cues, pause/resume and volume settings. Profile sustained matches for FPS, GC, draw calls, memory and thermals. No measured 60 FPS claim. |

The movement distance-driven procedural animation is an improvement to the existing presentation foundation, not a substitute for authored rigs/animation approval. The project still contains prototype-era HUD and procedural prop systems; this audit does not certify their visual quality.

## OWNER ACTION REQUIRED

1. Supply a Unity/Android execution environment and representative devices (or run the checks above and provide logs/captures). No Unity executable or device was available in this workspace.
2. Confirm the final Android package ID **before the first upload**; configure the upload keystore/alias/password locally. Keep keys and passwords out of git. Choose the release version and unused version code.
3. Use `DIMENSIONS FRONT/Release/Build signed Android bundle`. The output path is `Builds/Android/DIMENSIONS-FRONT-<versionCode>.aab`. Inspect its actual manifest, signing, native library compatibility including 16 KB page sizes, permissions and installed behavior. Static project settings do not prove the generated bundle's properties.
4. Complete Play Console account/testing requirements for the actual account, app signing, content rating, target audience and Data safety. Publish an accurate privacy policy with an owner-controlled URL after inspecting the final SDKs/binary and actual data behavior. Source review alone cannot certify no collection.
5. Provide final licensed app icon, feature graphic and screenshots of the actual game; verify rights for every shipped asset/audio/font. The listing must describe local AI 5v5 accurately and must not advertise active online multiplayer.

Policy reference checked during this pass: [Google Play target API requirements](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en). Target SDK 36 is preparation, not Play acceptance. Additional release checks: [Android page sizes](https://developer.android.com/guide/practices/page-sizes), [testing requirements for applicable personal accounts](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en).

No signing key, privacy-policy URL, store account declaration, screenshot evidence or runtime result has been invented. Release approval remains blocked by the evidence above.
