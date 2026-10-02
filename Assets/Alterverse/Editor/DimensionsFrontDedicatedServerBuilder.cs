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

            WriteEdgegapDockerFiles(outputDirectory);
            Debug.Log($"DIMENSIONS FRONT SERVER BUILD READY: {executablePath} • {summary.totalSize} bytes • {summary.totalTime}");
            Debug.Log($"DIMENSIONS FRONT EDGEGAP: Dockerfile ready at {Path.Combine(outputDirectory,DockerfileName)}");
            EditorUtility.RevealInFinder(outputDirectory);
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
