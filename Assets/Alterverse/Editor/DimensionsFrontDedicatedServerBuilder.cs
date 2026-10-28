using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alterverse.EditorTools
{
    public static class DimensionsFrontDedicatedServerBuilder
    {
        private const string OutputDirectory="Builds/DedicatedServer/Linux";
        private const string ExecutableName="DimensionsFrontServer.x86_64";
        private const string DockerfileName="Dockerfile";
        private const string PlayGamesSettingsPath="Assets/GooglePlayGames/Resources/PlayGamesSettings.asset";
        private const string HeldPlayGamesSettingsPath="Assets/Alterverse/Editor/PlayGamesSettings.server-build.asset";

        [InitializeOnLoadMethod]
        private static void RestoreAfterInterruptedBuild()
        {
            EditorApplication.delayCall+=()=>
            {
                if(!BuildPipeline.isBuildingPlayer) RestorePlayGamesSettings();
            };
        }

        [MenuItem("Tools/Dimensions Front/Build Linux Dedicated Server")]
        public static void BuildLinuxDedicatedServer()
        {
            RestorePlayGamesSettings();
            string[] scenes=EditorBuildSettings.scenes
                .Where(scene=>scene.enabled&&!string.IsNullOrEmpty(scene.path))
                .Select(scene=>scene.path)
                .ToArray();

            if(scenes.Length==0)
                throw new InvalidOperationException("No enabled scenes are configured in Build Profiles/Build Settings.");

            string projectRoot=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string outputDirectory=Path.Combine(projectRoot,OutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            string executablePath=Path.Combine(outputDirectory,ExecutableName);

            BuildPlayerOptions options=new()
            {
                scenes=scenes,
                locationPathName=executablePath,
                target=BuildTarget.StandaloneLinux64,
                subtarget=(int)StandaloneBuildSubtarget.Server,
                options=BuildOptions.CleanBuildCache
            };

            Debug.Log($"DIMENSIONS FRONT SERVER BUILD: building {scenes.Length} scene(s) to {executablePath}");
            // Arena builders currently create materials alongside authoritative colliders.
            // Optional server optimizations strip every shader, making new Material(null)
            // throw before bots spawn and the confirmed match-start packet is sent.
            bool previousOptimizations=PlayerSettings.dedicatedServerOptimizations;
            BuildReport report;
            try
            {
                PlayerSettings.dedicatedServerOptimizations=false;
                Alterverse.EditorChecks.AndroidRuntimeShaderPreserver.EnsureForDedicatedServer();
                // Google.Play.Games only compiles for Android/Editor. Resources assets are
                // otherwise bundled into Linux too, leaving this ScriptableObject with no class.
                // Hold it under Editor for this build; MoveAsset preserves the asset and GUID.
                if(AssetFileExists(PlayGamesSettingsPath))
                    MoveSettingsAsset(PlayGamesSettingsPath,HeldPlayGamesSettingsPath);
                report=BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.dedicatedServerOptimizations=previousOptimizations;
                RestorePlayGamesSettings();
            }
            BuildSummary summary=report.summary;
            if(summary.result!=BuildResult.Succeeded)
                throw new InvalidOperationException($"Dedicated server build failed: {summary.result} ({summary.totalErrors} errors).");

            WriteEdgegapDockerFiles(outputDirectory);
            Debug.Log($"DIMENSIONS FRONT SERVER BUILD READY: {executablePath} • {summary.totalSize} bytes • {summary.totalTime}");
            Debug.Log($"DIMENSIONS FRONT EDGEGAP: Dockerfile ready at {Path.Combine(outputDirectory,DockerfileName)}");
            if(!Application.isBatchMode) EditorUtility.RevealInFinder(outputDirectory);
        }

        private static bool AssetFileExists(string path)=>
            File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath,"..",path)));

        private static void RestorePlayGamesSettings()
        {
            if(!AssetFileExists(HeldPlayGamesSettingsPath)) return;
            if(!AssetFileExists(PlayGamesSettingsPath))
            {
                MoveSettingsAsset(HeldPlayGamesSettingsPath,PlayGamesSettingsPath);
                return;
            }

            // An Editor/plugin import can recreate the Resources asset while the local
            // original is held for the server build. Archive BOTH versions and GUIDs
            // before touching either. The held file is the pre-build configuration.
            string recovery=Path.GetFullPath(Path.Combine(Application.dataPath,"..",
                "Builds/ServerBuildRecovery",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(recovery);
            BackupSettingsAsset(PlayGamesSettingsPath,Path.Combine(recovery,"ResourceCopy.asset"));
            BackupSettingsAsset(HeldPlayGamesSettingsPath,Path.Combine(recovery,"HeldOriginal.asset"));
            string quarantined="Assets/Alterverse/Editor/PlayGamesSettings.regenerated-"+Guid.NewGuid().ToString("N")+".asset";
            // Import callbacks must not recreate Resources settings between the moves.
            AssetDatabase.StartAssetEditing();
            try
            {
                MoveSettingsAsset(PlayGamesSettingsPath,quarantined);
                try
                {
                    MoveSettingsAsset(HeldPlayGamesSettingsPath,PlayGamesSettingsPath);
                }
                catch(Exception restoreError)
                {
                    // Put the displaced copy back if restoration failed. Never
                    // overwrite a file that a plugin may have recreated again.
                    if(!AssetFileExists(PlayGamesSettingsPath))
                    {
                        try { MoveSettingsAsset(quarantined,PlayGamesSettingsPath); }
                        catch(Exception rollbackError)
                        {
                            throw new AggregateException("Settings recovery failed; both copies and metadata are backed up at "+recovery,
                                restoreError,rollbackError);
                        }
                    }
                    throw new InvalidOperationException("Settings recovery failed; both copies and metadata are backed up at "+recovery+": "+restoreError.Message,restoreError);
                }
                // Only the displaced duplicate is removed after both byte-for-byte
                // backups exist and the original has been restored with its GUID.
                if(!AssetDatabase.DeleteAsset(quarantined))
                    Debug.LogWarning("DIMENSIONS FRONT: restored Play Games settings; could not remove quarantined duplicate at "+quarantined);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.LogWarning("DIMENSIONS FRONT: restored the pre-build Play Games settings. Both versions and metadata were archived at "+recovery);
        }

        private static void BackupSettingsAsset(string assetPath,string destination)
        {
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"..",assetPath));
            foreach(string suffix in new[]{"",".meta"})
            {
                if(!File.Exists(source+suffix)) continue;
                File.Copy(source+suffix,destination+suffix);
                if(!File.ReadAllBytes(source+suffix).SequenceEqual(File.ReadAllBytes(destination+suffix)))
                    throw new IOException("Settings backup verification failed: "+destination+suffix);
            }
        }

        private static void MoveSettingsAsset(string from,string to)
        {
            string error=AssetDatabase.MoveAsset(from,to);
            if(!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Could not move PlayGamesSettings from {from} to {to}: {error}");
        }

        private static void WriteEdgegapDockerFiles(string outputDirectory)
        {
            string dockerfile=string.Join("\n",new[]
            {
                "FROM ubuntu:22.04",
                "ENV DEBIAN_FRONTEND=noninteractive",
                "RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libatomic1 libstdc++6 libgcc-s1 libc6 libuuid1 zlib1g && rm -rf /var/lib/apt/lists/*",
                "WORKDIR /server",
                "COPY . /server",
                "RUN chmod +x /server/DimensionsFrontServer.x86_64",
                "EXPOSE 7777/udp",
                "ENTRYPOINT [\"./DimensionsFrontServer.x86_64\",\"-batchmode\",\"-nographics\",\"-logFile\",\"/dev/stdout\",\"-port\",\"7777\"]",
                ""
            });
            const string dockerIgnore="DimensionsFrontServer_BackUpThisFolder_ButDontShipItWithYourGame/\n";
            File.WriteAllText(Path.Combine(outputDirectory,DockerfileName),dockerfile);
            File.WriteAllText(Path.Combine(outputDirectory,".dockerignore"),dockerIgnore);
        }

        // CI/command-line entry point: -executeMethod Alterverse.EditorTools.DimensionsFrontDedicatedServerBuilder.BuildLinuxFromCommandLine
        public static void BuildLinuxFromCommandLine()
        {
            BuildLinuxDedicatedServer();
        }
    }
}
