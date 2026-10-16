# Dedicated server arena startup and search cleanup

## Observed failure

The live v0.1.2-server deployment accepted the phone's UDP connection and verified its
matchmaking ticket. Match startup then failed while constructing the base cores:

```text
Trying to access a shader but no shaders were included in the build because Dedicated Server Optimizations is enabled.
DIMENSIONS FRONT: Moon startup failed during base cores and healing zones
ArgumentNullException: Value cannot be null. Parameter name: shader
UnityEngine.Material..ctor
Alterverse.Visuals.RealityCoreShell.Mat
Alterverse.Visuals.RealityCoreShell.Build
Alterverse.Prototype.PrototypeBootstrap.CreateBaseCore
```

The failure occurs before arena creation completes and the server confirms match launch.
The procedural arena builders currently create materials alongside gameplay colliders.
Removing whole builders would risk changing authoritative collision geometry.

## Changes

- The Linux dedicated server build menu temporarily disables Unity's optional
  `PlayerSettings.dedicatedServerOptimizations` asset stripping during `BuildPlayer`.
  It restores the previous setting in `finally`, including failed builds.
- The builder also preserves URP Lit/Unlit and HeroEnergy through the existing shader
  preservation helper. Missing shaders fail the build with a useful error.
- The container still runs with `-batchmode -nographics` and UDP 7777.
- Group DELETE cleanup accepts Edgegap's documented 409 response for a group that
  already found a match. Edgegap removes those groups automatically; the client
  releases the old local membership so it can create a new search. This also covers
  cleanup of a late group creation response. Other request failures still fail.

The phone's exact HTTP cleanup response was not captured. The 409 behavior is the
documented matched-group case consistent with cleanup after a successful connection;
it is covered explicitly rather than suppressing arbitrary cleanup errors.

## Verification

```sh
python Tools/Ranked/Regression/run.py --dotnet-root /path/to/dotnet
python Tools/DedicatedServer/Regression/run.py --dotnet-root /path/to/dotnet
```

Results: 1,916 lobby/identity/ranked checks and 37 dedicated build checks passed.
The added regressions fail against the previous production implementations.
These compile production C# against controlled Unity/HTTP/editor substitutes. They do
not replace a native Unity build or a phone-to-server match test.

## Required rollout

1. Update the Unity project from `dev/vertical-slice`.
2. Run **Tools > Dimensions Front > Build Linux Dedicated Server** in Unity 6000.6.1f1.
   Use this project menu so shader preservation and temporary build settings apply.
3. From `Builds/DedicatedServer/Linux`, build and push a new immutable image tag:

   ```powershell
   docker build -t registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server:v0.1.3-server .
   docker push registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server:v0.1.3-server
   ```

4. Create the Edgegap application version using that image and switch the live
   matchmaker application reference to it. Keep the approved solo expansion rules.
5. Rebuild and install the Android client as well, so the phone receives the cleanup fix.
6. Test one phone searching alone: assignment, verified dedicated connection, completed
   arena startup, seven bots filling the 4v4 roster, and confirmed match launch. Then
   return to the menu and search again. Check server logs for startup exceptions.

The source fix does not change the already uploaded v0.1.2 image or installed phone app.

References:
- [Unity dedicatedServerOptimizations](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/playersettings/dedicatedserveroptimizations)
- [Edgegap Abandon Queue contract](https://docs.edgegap.com/learn/matchmaking/matchmaker-in-depth#abandon-queue)
