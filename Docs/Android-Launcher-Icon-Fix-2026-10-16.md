# Android launcher icon: circular portal

The approved icon uses the Dimensions Front stone portal and blue-violet crystal, without the title or surrounding landscape. The new source artwork is `Assets/Alterverse/Branding/DimensionsFrontAppIcon.png`; its exterior is transparent. The adaptive Android background is a full-bleed navy-to-violet field. The transparent foreground contains a 300px portal on a 512px layer, centered at (106, 106). Android's typical round visible mask has a 342px diameter at that scale, so the portal fills about 88% of the visible diameter. It is much larger than the previous 216px source layout.

`AndroidLauncherIcons` already assigns both adaptive layers before Android builds and verifies every icon slot. The Unity importer GUIDs and ProjectSettings references remain unchanged. Run `node Tools/Branding/export-launcher-icons.cjs` with `sharp` available to regenerate both layers and the unused legacy preview from the new source artwork. Do not restore the old 216px layout.

The round launcher preview was checked at 512px and at 96px. No Unity or Android build was run here; install a newly built APK/AAB on the device to verify the launcher rendering.
