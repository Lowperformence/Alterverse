# Menu startup transition candidate

The reported symptom is a brief contrast change and a slight shift toward the upper left on startup, followed by normal background animation. It has not been reproduced on a device here. The installed Android version is unknown.

The current source already loads a poster extracted from video frame zero. Commit `965c41e871667702df52271559db42bff68c0789` records a decoded-pixel comparison and matching 1918 x 1080 dimensions. This investigation did not independently repeat that binary comparison. The remaining startup draw path switches from a directly drawn Texture2D to a video RenderTexture and crossfades moving video over the still for 150 ms. That path is a candidate for the reported transition, not a proven root cause.

## Proposed change

- Seed the existing video RenderTexture once from the existing poster before preparing playback. Show that same render target throughout normal preparation and decoded playback.
- Set the poster blit's sRGB write state to the target's color-space flag, then restore the previous render target and write state even if the blit fails.
- Keep one opaque IMGUI draw with the existing full-screen crop. Remove the 150 ms two-image crossfade so moving video is never mixed with the static first frame.
- Preserve missing-resource/decoder-failure still fallback, timeout, pause/focus handling, lost-target recovery, silent looping playback, and server/batch exclusion. No assets, account code, gameplay or server deployment settings are changed.

This adds one GPU copy at menu initialization or lost-target recovery. It adds no render target and no per-frame copy. Resources.Load returns the existing shared poster; this code must not unload it while MenuCosmicBackdrop retains its fallback reference.

## Verification

`python Tools/UI/MenuBackgroundRegression/run.py --dotnet-root /path/to/dotnet`

The complete production classes compile against engine fixtures. 53 client lifecycle/draw-command checks and 3 UNITY_SERVER checks pass, covering texture identity across handover, one opaque draw without translation, caller tint, linear/gamma write-state selection and restoration, failed blits, fallback, timeout, focus/pause, lost targets, reopen, and cleanup. These fixtures do not decode a video or render pixels. The original sources are a negative control and must fail the first seeded-image check.

## Required native validation before merging

In a signed Android build, compare cold start with Google Play already signed in and with its sign-in overlay opening. Confirm the video target retains the seeded poster during Prepare; the native decoder must not clear the target to black before the first frame. Capture the seeded target and decoded frame zero on the actual graphics backend to check contrast, orientation and crop. Verify menu return, background/resume, and an actual lost render target. Confirm the existing animation, menu input, profile restoration and fallback remain intact.

If the decoded frame and seeded poster still differ, measure that difference before changing import/color settings. Do not compensate by altering project gamma, global contrast or game lighting. This candidate remains a draft until native validation passes; it does not establish that the reported problem is fixed.

This is a client rendering change. Once validated and merged it needs a new Android AAB; an Edgegap server build cannot deliver it to installed clients.

Relevant Unity documentation: [Graphics.Blit](https://docs.unity3d.com/ScriptReference/Graphics.Blit.html), [GL.sRGBWrite](https://docs.unity3d.com/ScriptReference/GL-sRGBWrite.html), [RenderTextureReadWrite](https://docs.unity3d.com/ScriptReference/RenderTextureReadWrite.html), [VideoClip.sRGB](https://docs.unity3d.com/ScriptReference/Video.VideoClip-sRGB.html).
