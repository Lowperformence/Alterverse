#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Alterverse.EditorChecks
{
    /// <summary>Current game uses Internet/Relay and the share sheet, not LAN or device contacts.</summary>
    public sealed class AndroidPermissionPolicy : IPostGenerateGradleAndroidProject
    {
        internal static readonly string[] UnusedPermissions =
        {
            "android.permission.ACCESS_COARSE_LOCATION",
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.ACCESS_BACKGROUND_LOCATION",
            "android.permission.READ_CONTACTS",
            "android.permission.WRITE_CONTACTS",
            "android.permission.MANAGE_CONTACTS",
            "android.permission.MANAGE_EXTERNAL_STORAGE",
            "android.permission.READ_CALL_LOG",
            "android.permission.WRITE_CALL_LOG",
            "android.permission.PROCESS_OUTGOING_CALLS",
            "android.permission.READ_SMS",
            "android.permission.SEND_SMS",
            "android.permission.RECEIVE_SMS",
            "android.permission.RECEIVE_MMS",
            "android.permission.RECEIVE_WAP_PUSH",
            "android.permission.ACCESS_LOCAL_NETWORK"
        };

        public int callbackOrder => 1000200;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            PatchManifest(Path.Combine(path, "src/main/AndroidManifest.xml"));
            string parent = Directory.GetParent(path)?.FullName;
            if (parent == null) throw new BuildFailedException("Android permission policy: missing Gradle project root.");
            // Launcher has higher merge priority than unityLibrary and dependency AARs.
            PatchManifest(Path.Combine(parent, "launcher/src/main/AndroidManifest.xml"));
        }

        internal static void PatchManifest(string path)
        {
            if (!File.Exists(path)) throw new BuildFailedException("Android permission policy: manifest missing: " + path);
            var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var manifest = document.Root;
            if (manifest == null || manifest.Name != "manifest")
                throw new BuildFailedException("Android permission policy: invalid manifest: " + path);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            XNamespace tools = "http://schemas.android.com/tools";
            bool changed = (string)manifest.Attribute(XNamespace.Xmlns + "tools") != tools.NamespaceName;
            manifest.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
            foreach (string permission in UnusedPermissions)
            {
                // SDK-23 declarations have a distinct merger key; block both forms.
                foreach (string tag in new[] { "uses-permission", "uses-permission-sdk-23" })
                {
                    var existing = manifest.Elements(tag).Where(e => (string)e.Attribute(android + "name") == permission).ToArray();
                    if (existing.Length == 1 && existing[0].Attributes().Count() == 2 &&
                        (string)existing[0].Attribute(tools + "node") == "remove") continue;
                    existing.Remove();
                    manifest.Add(new XElement(tag, new XAttribute(android + "name", permission),
                        new XAttribute(tools + "node", "remove")));
                    changed = true;
                }
            }
            if (changed) document.Save(path);
        }
    }
}
#endif
