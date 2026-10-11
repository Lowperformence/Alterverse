# Shader loading and cyan first-use flashes

The reported turquoise surface on arena start or first attack matches Unity's cyan placeholder for asynchronous shader compilation in Game/Scene view. `ProjectSettings/EditorSettings.asset` previously enabled that behavior (`m_AsyncShaderCompilation: 1`). The project now disables it. This is an Editor setting; it does not change player rendering, shader colors, alpha, damage, or targeting.

`ProjectShaderValidation` runs before entering Play Mode. It keeps asynchronous compilation disabled, enumerates project shader assets, synchronously compiles their default material passes on the current Editor graphics device, and checks for compiler errors. Additional keyword variants compile synchronously on first use. Shader failures stop Play Mode and identify the affected asset. Temporary preflight materials are disposed, and serialized runtime templates are preserved.

A build preprocessor also checks project shader errors and the HeroEnergy, AbilityButton, and HeroSurface Resources material references. It runs after the existing Android runtime shader preservation step. Builds with a null graphics device skip device-dependent support/compilation checks while still validating asset references and reported import errors. Default Editor pass compilation is not a complete Android/Vulkan variant validation or a runtime device warmup.

## Verification

- Audited the 22 project shader files, 58 render programs, local HLSL includes, unique names/GUIDs, and the three serialized material shader references. No missing local include or mismatched material reference was found.
- The previous asynchronous compilation setting reproduces the regression audit failure; the updated setting passes.
- The production preflight compiles in the portable C# harness and passes 23 behavior checks, including synchronous compilation, failed imports, compiler errors/exceptions, absent or incorrect materials, unsupported shaders, headless builds, event subscription, Play Mode cancellation, and temporary material cleanup.
- Native Unity shader import/GPU compilation and Android visual testing are not available in this environment. The preflight will perform the native default-pass checks on the next Play Mode entry. Android build and device checks remain required to establish behavior on a phone.

Run the portable checks with:

```text
python Tools/Shaders/Regression/run.py --mono-root <extracted-mono-root> --roslyn <path-to-csc.exe>
```

The manual Unity check is available at `DIMENSIONS FRONT/Validation/Validate and precompile project shaders`. After updating the project, enter Play Mode, start an arena, and use each attack once to exercise any additional keyword variants. The first compilation can briefly delay Editor rendering instead of displaying a cyan placeholder.

## Unity documentation

- https://docs.unity3d.com/6000.0/ScriptReference/EditorSettings-asyncShaderCompilation.html
- https://docs.unity3d.com/6000.0/ScriptReference/ShaderUtil.CompilePass.html
- https://docs.unity3d.com/6000.0/ScriptReference/ShaderUtil.ShaderHasError.html
