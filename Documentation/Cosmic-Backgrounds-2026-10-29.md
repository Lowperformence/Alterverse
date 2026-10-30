# Main-menu cosmic background; arena rollback

On 2026-10-30 all match-background changes from the 2026-10-29 cosmic-background update were reverted at the user's request. The entire `Assets/Alterverse/Resources/MapPanoramas` subtree, `PhotoArenaPanorama.cs`, `CosmicArenaBackdrop.cs` and the previous map-background document are restored byte-for-byte from commit `359d045047f812a7cfc8a8ac63f7f70af25d0cbc`.

Matches again load the original `MapPanoramas/Universe` image through the original single-pass `MapPanoramas/PhotoSkybox` shader. Replacement map compositions, map-specific effects and map import overrides are removed. Combat, balance, matchmaking and server configuration are untouched.

The main menu retains its five planets, moonlets, dense stars, dust ribbon, glints and shooting stars. Its image is unchanged. Menu effects now use the separate single-pass shader `FrontEnd/CosmicMenuEffect` and linear noise data `FrontEnd/CosmicNoise`, rendered only through `MenuUniverseBackground` into the existing 1280x640 target at up to 30 fps. Texture leases, exposure, pause/focus handling, graphics-state restoration and lost-target recovery remain intact. Import overrides apply only to the two menu textures. Hero pedestal and sole-placement files are unchanged.

The rejected shared shader contained both a skybox pass and a `ZTest Always` menu pass. A skybox must not receive a menu draw that bypasses depth; reported Android symptoms included stretched bands and hidden opaque world geometry. The menu shader is now separated from arena rendering; the arena directory is exactly the pre-change subtree.

Run `python3 Tools/Visuals/CosmicBackdropRegression/run.py --dotnet-root /path/to/dotnet` and `python3 Tools/UI/MenuBackgroundRegression/run.py --dotnet-root /path/to/dotnet`. Checks cover shader separation, restored arena resource selection and menu lifecycle behavior. Git tree/blob identities verify the rollback and preservation of the menu image and pedestal.

The earlier shader-math tests did not exercise complete Unity skybox draw order and failed to catch this regression. Their map-art previews/results have been removed. Current API fixtures do not constitute an Android render test. Stop/restart Play mode after updating and build a new AAB for the phone. A server rebuild is unnecessary for this client-only rollback.
