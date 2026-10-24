// Compile-only stand-ins for the two real Unity headwear editor tools.
// They are never used as evidence of real-hero fitting, rendering or Unity/device validation.
using System;
using UnityEngine;
namespace UnityEngine
{
    public enum HideFlags {HideAndDontSave}
    public enum CameraClearFlags {SolidColor}
    public enum EventType {MouseDrag,Repaint}
    public struct Vector2 {public float x,y;}
    public sealed class Event {public static Event current=new();public EventType type;public Vector2 mousePosition,delta;public void Use(){}}
    public class Collider:Component {}
    public class Camera:Component {public float nearClipPlane,farClipPlane,fieldOfView;public CameraClearFlags clearFlags;public Color backgroundColor;}
    public class Light:Component {public float intensity;}
    public class GUIStyle {public static GUIStyle none=new();}
    public class GUILayoutOption {}
    public static class GUILayout
    {
        public static bool Button(string value)=>false;
        public static void Label(Texture2D texture,params GUILayoutOption[] options){}
        public static GUILayoutOption Height(float value)=>new();
    }
    public static class GUILayoutUtility {public static Rect GetRect(float a,float b,float c,float d)=>default;}
}
namespace UnityEngine.SceneManagement
{
    public struct Scene {}
    public static class SceneManager {public static void MoveGameObjectToScene(GameObject go,Scene scene){}}
}
namespace UnityEditor
{
    public enum MessageType {Info,None}
    public class EditorWindow:UnityEngine.Object
    {public static T GetWindow<T>(string title) where T:EditorWindow,new()=>new();public void Repaint(){}}
    public static class EditorGUI
    {public static void BeginChangeCheck(){}public static bool EndChangeCheck()=>false;}
    public static class EditorGUILayout
    {
        public static void HelpBox(string value,MessageType kind){}
        public static Enum EnumPopup(string label,Enum value)=>value;
        public static bool Toggle(string label,bool value)=>value;
        public static float Slider(string label,float value,float min,float max)=>value;
    }
    public class PreviewRenderUtility
    {
        public readonly Camera camera=new GameObject().AddComponent<Camera>();
        public readonly Light[] lights={new GameObject().AddComponent<Light>(),new GameObject().AddComponent<Light>()};
        public void Cleanup(){}public void AddSingleGO(GameObject actor){}public void BeginPreview(Rect rect,GUIStyle style){}
        public void Render(bool allowScriptableRenderPipeline){}public Texture2D EndPreview()=>new();
    }
}
namespace UnityEditor.SceneManagement
{
    public static class EditorSceneManager
    {public static UnityEngine.SceneManagement.Scene NewPreviewScene()=>default;public static void ClosePreviewScene(UnityEngine.SceneManagement.Scene scene){}}
}
namespace Alterverse.Visuals {public class StylizedHeroVisual:MonoBehaviour {public void Build(){}}}

