# Shared universe background

All arena variants load the user-provided Universe.png star-field/nebula photograph through PhotoArenaPanorama. It is rendered only behind the gameplay camera via its Skybox component. The 999x666 reference is displayed with an aspect-preserving cover crop, not spherical panorama coordinates. No lighting/exposure multiplier is applied. PC and Android use the same sRGB texture and shader. Portrait/menu cameras remain isolated.

The old planet-specific textures are retained as source assets but are no longer selected by this loader. They can be removed after visual acceptance if no longer needed.

Source inspection and viewport crop calculations checked. Unity shader compilation, gameplay screenshot review and an Android build remain pending because no Unity/device runner is available in this workspace.
