# Portable VFX and audio envelope regression

Run from the repository with Python 3, tree-sitter/tree-sitter-c-sharp and Mono:

```sh
python Tools/Visuals/Regression/run.py --mono-root /path/to/extracted-mono
```

The runner compiles the production `HeroVfxPool` scheduler, `Show`, `Draw`, `Trace`, cancellation and lifetime code, plus the unchanged production performance monitor, VFX budget and audio envelope. Only `BuildShape`, which uses local functions unsupported by the portable Mono compiler, is replaced with a named empty mesh fixture. Substitute engine transforms use System.Numerics; renderers, Resources, object lifecycle and clips are stand-ins. This is not full Unity compilation or proof of rendering/native allocations.

The checks cover the 128 decorative / 32 core / 32 warning partitions; core preemption of decoration; warning preservation and owner cancellation; warning size/detail; default rotation; pause, delayed start and expiry; 1,000 traces without new pool children; 500 arbitrary 3D muzzle/contact/width fits; 30–144 Hz quality hysteresis, actual monitor pause/target/reset behavior, and audio attack/release stability. No MP3 or other audio file is loaded or changed.

For the material regression, install NumPy, Pillow and ModernGL and provide glslangValidator, spirv-cross and spirv-val. Obtain the previous shader from the parent commit:

```sh
git show 9762bd4907838da8404754aadb4f06645bfadf56:Assets/Alterverse/Resources/CombatVfx/HeroEnergy.shader > /tmp/HeroEnergy-before.shader
python Tools/Visuals/render_energy_regression.py --native-bin /path/to/bin --baseline /tmp/HeroEnergy-before.shader --output /tmp/df-vfx-check
python Tools/Visuals/compile_scenery_regression.py --native-bin /path/to/bin --output /tmp/df-vfx-check/scenery.json
```

The original HeroEnergy HLSL is compiled to SPIR-V, validated, translated to GLSL and rendered with EGL. Only Unity Core matrices/view helpers are replaced. Controlled orb body, shield sides, ring and seed fixtures do not include every runtime mesh feature. 336 render cases test finite output, inherited alpha and exact warning output; the montage is an isolated material comparison, not a screenshot of the game. Scenery forward stages compile regular and screen-space shadow paths using substitute Unity lighting types. Real URP includes, stripping, Vulkan/GLES drivers, sockets, audio listening and Android GPU timings require the Unity/device checks described in the accompanying documentation.
