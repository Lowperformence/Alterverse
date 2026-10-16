# Android launcher icon repair

The Android adaptive icon slots previously contained only the original logo in layer zero. Android's second layer was not assigned, allowing the Unity foreground to appear over the game's logo. The release helper used the older application-icon API and accepted any nonempty slot, so it did not repair the missing foreground.

`AndroidLauncherIcons` assigns all adaptive backgrounds and foregrounds with `SetPlatformIcons`, plus round and legacy fallbacks. The existing build callback applies the repair before every Android build, including development builds and builds started through Unity's standard window. Release validation checks every slot and its expected textures. The repair is also available under `DIMENSIONS FRONT > Release > Repair Android launcher icons`.

The original `DimensionsFrontAppIcon.png` remains unchanged. A mechanical SVG layout export adds safe padding and masks the light outer corners of its existing rounded frame. The adaptive foreground's 216px square fits inside the central 66/108 safe circle on a 512px canvas; the opaque background fills the complete canvas. Round and legacy fallbacks use the equivalent visible scale. This preserves the existing artwork; the original source is only 128px and exporting at 512px does not add artwork detail.

Regenerate derived images with Node.js and `sharp`: `node Tools/Branding/export-launcher-icons.cjs`.

Validation completed: all 18 serialized slots, texture GUID references, PNG dimensions, alpha coverage, circular safe area, unchanged source hash, round/rounded-square preview and whitespace checks. The production repair helper compiled and passed behavioral checks with C# API fixtures for incomplete layers, stale Unity textures, all slots, layer order, validation, idempotence and missing assets. These fixtures are not a Unity integration test. Unity and the Android toolchain are unavailable here, so no APK/AAB or device test was performed.

The relevant upstream source files were read from `Lowperformence/Alterverse`, `dev/vertical-slice`, commit `bab893aa435d3a58d44e68869f946326c36fd504`. This change is limited to Android launcher icons and their build configuration. Existing local matchmaking edits were not modified.

For manual installation, import `DimensionsFront-Icon-Fix.unitypackage` through `Assets > Import Package > Custom Package`, then build Android again. The package contains only the three derived icon textures, the repair helper and the updated Android release build helper. It does not replace ProjectSettings or gameplay code. Installing a new game build is required before the phone's launcher can show the repaired icon.

API references: [Unity platform icons](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.SetPlatformIcons.html), [Android adaptive icon design](https://developer.android.com/develop/ui/compose/system/icon_design_adaptive).
