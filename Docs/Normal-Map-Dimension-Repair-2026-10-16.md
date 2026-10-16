# Normal-map dimension repair

The Unity screenshot showed `Error assigning CUBE texture to 2D texture property '_BumpMap': Dimensions must match` while displaying the hero menu. The screenshot does not identify the specific source material. Code inspection found unchecked texture copying in the runtime material fallback, no explicit 2D shape in the Void Knight texture importer, and retained normal bindings during the model importer's URP shader conversion.

The runtime fallback now copies only 2D-compatible base and normal textures. Valid normals retain their strength, UV transforms and imported texture encoding. Supported shaders with a mismatched 2D normal binding receive an owned runtime copy with that invalid binding removed; shared imported materials, other material properties and legitimate cubemap shader properties remain unchanged. Repeated checks reuse the repaired copy.

The Void Knight texture importer pins Texture2D and increments its version to invalidate older imports. The hero model importer increments its version and explicitly resets the normal binding during URP conversion, preserving only 2D-compatible inherited normals before applying the existing authored textures. Unity will reimport the affected assets after pulling.

Run `python Tools/Materials/Regression/run.py --dotnet-root <dotnet-sdk-directory>`. The production runtime guard and texture importer pass 63 checks against engine fixtures enforcing texture dimensionality, including cubemaps, arrays, 3D textures, 2D render textures, shared-source preservation, repeated repair, valid normal maps, normal encoding, base-map fallback and unrelated imports. The original runtime guard fails the stale normal-map regression. No native Unity render or Android build was available; the user's Console and hero preview still need confirmation after reimport.

This change is based on `dev/vertical-slice` commit `08255e8dab7689b809477d80ed34b527ac6d5ac9`; existing R8 and launcher fixes are preserved.

API references: [Shader texture dimensions](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/shader/getpropertytexturedimension), [Texture dimensionality](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/texture/dimension).
