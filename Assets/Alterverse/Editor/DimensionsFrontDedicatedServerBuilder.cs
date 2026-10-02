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

        [MenuItem("Tools/Dimensions Front/Build Linux Dedicated Server")]
        public static void BuildLinuxDedicatedServer()
        {
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
                options=BuildOptions.None
            };

            Debug.Log($"DIMENSIONS FRONT SERVER BUILD: building {scenes.Length} scene(s) to {executablePath}");
            BuildReport report=BuildPipeline.BuildPlayer(options);
            BuildSummary summary=report.summary;
            if(summary.result!=BuildResult.Succeeded)
                throw new InvalidOperationException($"Dedicated server build failed: {summary.result} ({summary.totalErrors} errors).");

            Debug.Log($"DIMENSIONS FRONT SERVER BUILD READY: {executablePath} • {summary.totalSize} bytes • {summary.totalTime}");
            EditorUtility.RevealInFinder(outputDirectory);
        }

        // CI/command-line entry point: -executeMethod Alterverse.EditorTools.DimensionsFrontDedicatedServerBuilder.BuildLinuxFromCommandLine
        public static void BuildLinuxFromCommandLine()
        {
            BuildLinuxDedicatedServer();
        }
    }
}
