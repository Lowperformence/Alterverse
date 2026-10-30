using System;
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType {SubsystemRegistration,AfterSceneLoad}
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
    public static class Application {public static bool isMobilePlatform;public static int targetFrameRate;}
    public static class QualitySettings
    {
        public static Rendering.RenderPipelineAsset renderPipeline;
        public static int vSyncCount;public static float lodBias,shadowDistance,streamingMipmapsMemoryBudget;
        public static bool streamingMipmapsActive;
    }
}
namespace UnityEngine.Rendering
{
    public class RenderPipelineAsset:UnityEngine.Object{}
    public static class GraphicsSettings {public static RenderPipelineAsset currentRenderPipeline=>UnityEngine.QualitySettings.renderPipeline??Source;public static RenderPipelineAsset Source=new Universal.UniversalRenderPipelineAsset{name="Mobile"};}
}
namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineAsset:UnityEngine.Rendering.RenderPipelineAsset
    {public float shadowDistance;public int shadowCascadeCount,msaaSampleCount;}
}
namespace Alterverse.Profile
{
    public static class AccountProfilePrefs
    {
        public static string Account;public static int Reads,Value=1,Saves;
        public static int GetInt(string key,int fallback=0){Reads++;return Value;}
        public static void SetInt(string key,int value){Value=value;}
        public static void Save(){Saves++;}
    }
}
