#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
namespace Alterverse.Editor
{
    // Vivox's library requests a required microphone. Voice is optional in this game.
    public sealed class PartyVoiceAndroidManifest:IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>10000;
        public void OnPostGenerateGradleAndroidProject(string path)
            =>MakeMicrophoneOptional(Path.Combine(path,"src/main/AndroidManifest.xml"));
        public static void MakeMicrophoneOptional(string file)
        {
            var document=XDocument.Load(file);var root=document.Root;
            XNamespace android="http://schemas.android.com/apk/res/android",tools="http://schemas.android.com/tools";
            root.SetAttributeValue(XNamespace.Xmlns+"tools",tools.NamespaceName);
            var feature=root.Elements("uses-feature").FirstOrDefault(e=>(string)e.Attribute(android+"name")=="android.hardware.microphone");
            if(feature==null){feature=new XElement("uses-feature");root.Add(feature);}
            feature.SetAttributeValue(android+"name","android.hardware.microphone");
            feature.SetAttributeValue(android+"required","false");
            string existing=(string)feature.Attribute(tools+"replace")??"";
            if(!existing.Split(',').Select(x=>x.Trim()).Contains("android:required"))
                feature.SetAttributeValue(tools+"replace",existing.Length==0?"android:required":existing+",android:required");
            document.Save(file);
        }
    }
}
#endif
