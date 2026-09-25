#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    /// <summary>
    /// Keeps optional phone hardware from becoming a Google Play device filter when
    /// Android libraries are merged into the final manifest.
    /// </summary>
    public sealed class AndroidDeviceCompatibilityPostprocessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string ToolsNamespace = "http://schemas.android.com/tools";

        // DIMENSIONS FRONT does not depend on these features to play. Some Android
        // permissions/plugins can otherwise make Play infer them as required hardware.
        private static readonly string[] OptionalHardwareFeatures =
        {
            "android.hardware.camera",
            "android.hardware.camera.any",
            "android.hardware.camera.autofocus",
            "android.hardware.camera.flash",
            "android.hardware.microphone",
            "android.hardware.location",
            "android.hardware.location.gps",
            "android.hardware.location.network",
            "android.hardware.telephony",
            "android.hardware.telephony.gsm",
            "android.hardware.telephony.cdma"
        };

        // Run late so this policy is applied after normal Unity/plugin Gradle generation.
        public int callbackOrder => 1000000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string unityLibraryManifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(unityLibraryManifest))
                throw new BuildFailedException("DIMENSIONS FRONT: generated AndroidManifest.xml was not found at " + unityLibraryManifest);

            PatchManifest(unityLibraryManifest);

            DirectoryInfo parent = Directory.GetParent(path);
            if (parent != null)
            {
                string launcherManifest = Path.Combine(parent.FullName, "launcher", "src", "main", "AndroidManifest.xml");
                if (File.Exists(launcherManifest))
                    PatchManifest(launcherManifest);
            }

            Debug.Log("DIMENSIONS FRONT: Android device-compatibility manifest guard applied.");
        }

        private static void PatchManifest(string manifestPath)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifestPath);

            XmlElement manifest = document.DocumentElement;
            if (manifest == null || manifest.Name != "manifest")
                throw new BuildFailedException("DIMENSIONS FRONT: invalid generated Android manifest: " + manifestPath);

            // tools:node=replace is intentional: android:required on <uses-feature> is
            // otherwise merged with OR semantics, so a library's required=true would win.
            if (string.IsNullOrEmpty(manifest.GetAttribute("tools", "http://www.w3.org/2000/xmlns/")))
                manifest.SetAttribute("xmlns:tools", ToolsNamespace);

            var byName = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
            foreach (XmlNode node in manifest.SelectNodes("uses-feature"))
            {
                if (!(node is XmlElement element))
                    continue;

                string name = element.GetAttribute("name", AndroidNamespace);
                if (!string.IsNullOrEmpty(name) && !byName.ContainsKey(name))
                    byName.Add(name, element);
            }

            XmlNode application = manifest.SelectSingleNode("application");
            foreach (string featureName in OptionalHardwareFeatures)
            {
                if (!byName.TryGetValue(featureName, out XmlElement feature))
                {
                    feature = document.CreateElement("uses-feature");
                    feature.SetAttribute("name", AndroidNamespace, featureName);
                    if (application != null)
                        manifest.InsertBefore(feature, application);
                    else
                        manifest.AppendChild(feature);
                    byName.Add(featureName, feature);
                }

                feature.SetAttribute("required", AndroidNamespace, "false");
                feature.SetAttribute("node", ToolsNamespace, "replace");
            }

            document.Save(manifestPath);
            WriteCompatibilitySnapshot(manifestPath, manifest);
        }

        private static void WriteCompatibilitySnapshot(string sourceManifestPath, XmlElement manifest)
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string outputDirectory = Path.Combine(projectRoot, "Builds", "Android");
                Directory.CreateDirectory(outputDirectory);

                string manifestCopy = Path.Combine(outputDirectory, "GeneratedManifest-device-compatibility.xml");
                File.Copy(sourceManifestPath, manifestCopy, true);

                var report = new StringBuilder();
                report.AppendLine("DIMENSIONS FRONT - generated Android uses-feature report");
                report.AppendLine(DateTime.UtcNow.ToString("u"));
                report.AppendLine();

                foreach (XmlNode node in manifest.SelectNodes("uses-feature"))
                {
                    if (!(node is XmlElement feature))
                        continue;

                    string name = feature.GetAttribute("name", AndroidNamespace);
                    string required = feature.GetAttribute("required", AndroidNamespace);
                    string glEsVersion = feature.GetAttribute("glEsVersion", AndroidNamespace);
                    if (string.IsNullOrEmpty(required))
                        required = "true (Android default)";

                    report.Append("name=").Append(string.IsNullOrEmpty(name) ? "<none>" : name)
                        .Append(" required=").Append(required);
                    if (!string.IsNullOrEmpty(glEsVersion))
                        report.Append(" glEsVersion=").Append(glEsVersion);
                    report.AppendLine();
                }

                File.WriteAllText(Path.Combine(outputDirectory, "GeneratedManifest-features.txt"), report.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("DIMENSIONS FRONT: could not write manifest compatibility snapshot: " + exception.Message);
            }
        }
    }
}
#endif
