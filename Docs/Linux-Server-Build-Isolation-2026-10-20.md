# Linux dedicated server build isolation — 2026-10-20

The v0.1.15 startup test reached Ready in 17.74 seconds and listened on UDP 7777.
It also reported an unknown script and a serialization layout mismatch (52 bytes read, 156 expected).
The test deployment b933451d110e was terminated after collecting its logs. This was an idle startup test, not a complete multiplayer match.

## Confirmed incompatible asset

Assets/GooglePlayGames/Resources/PlayGamesSettings.asset references script GUID a17b07049887e4804980e9016e49ad41.
That GUID matches PlayGamesSettings.cs in the pinned Google Play Games v2.2.1 package.
The package's Runtime/Google.Play.Games.asmdef includes only Android and Editor.
Resources assets are included in Linux builds even though this script assembly is absent there.

The dedicated server builder now holds this asset under Assets/Alterverse/Editor only while building,
then restores its original path using AssetDatabase.MoveAsset, preserving its local contents and GUID.
Restoration also runs after build failure and at Editor startup to recover an interrupted build.
A collision aborts without overwriting either asset. Android settings are not edited.
The server build uses CleanBuildCache so scripts and player data are rebuilt.

## Verification

99 dedicated server build regression assertions pass using editor substitutes, compiling the production builder with Roslyn/.NET 8.
Cases include success, failed reports, native exceptions, move failure, restore failure and recovery, duplicate-file protection,
Editor startup recovery, shader preservation and dedicated runtime suppression.

Unity's native asset pipeline and the corrected Linux startup still need validation on the build PC.
The exclusion fixes a confirmed incompatible asset; it does not yet prove that both startup warnings are resolved.
The live matchmaker remains on v0.1.13-server until the corrected image passes validation.

