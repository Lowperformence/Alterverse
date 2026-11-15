using System;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Performance;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine
{
    public class Object { public string name; public static T Instantiate<T>(T o) where T:Object => o; public static void Destroy(Object o) {} }
    public enum RuntimeInitializeLoadType { SubsystemRegistration, AfterSceneLoad }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
    public static class Application { public static bool isMobilePlatform = true; public static int targetFrameRate; }
    public static class Time { public static int frameCount; }
    public static class Mathf { public static int Max(int a,int b)=>Math.Max(a,b); public static int Clamp(int a,int b,int c)=>Math.Max(b,Math.Min(c,a)); }
    public static class SystemInfo
    {
        public static int systemMemorySize {get;set;} = 8000;
        public static int graphicsMemorySize {get;set;} = 512;
        public static int processorCount {get;set;} = 8;
        public static int processorFrequency {get;set;} = 900;
        public static string graphicsDeviceName {get;set;} = "Mali-G710";
    }
    public static class QualitySettings
    {
        public static RenderPipelineAsset renderPipeline; public static int vSyncCount;
        public static float lodBias,shadowDistance,streamingMipmapsMemoryBudget; public static bool streamingMipmapsActive;
    }
}
namespace UnityEngine.Rendering { public class RenderPipelineAsset:UnityEngine.Object {} public static class GraphicsSettings {public static RenderPipelineAsset currentRenderPipeline;} }
namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineAsset:RenderPipelineAsset
    {
        public float shadowDistance,renderScale; public int shadowCascadeCount,msaaSampleCount; public bool supportsHDR;
    }
}
namespace Alterverse.Profile
{
    public static class AccountProfilePrefs
    {
        static readonly Dictionary<string,int> values=new(); public static string Account {get;set;}="a";
        public static int GetInt(string key,int fallback=0)=>values.TryGetValue(Account+key,out int v)?v:fallback;
        public static void SetInt(string key,int value)=>values[Account+key]=value;
        public static void Save(){}
    }
}
namespace Alterverse.Performance { public static class RuntimePerformanceMonitor {public static bool UnderBudget {get;set;}} }
static class Regression
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Reset()=>typeof(MobilePerformanceProfile).GetMethod("ResetRuntimeState",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    public static void Main()
    {
        var pipeline=new UniversalRenderPipelineAsset {name="test"};GraphicsSettings.currentRenderPipeline=pipeline;
        Reset();Check(MobilePerformanceProfile.RecommendedQuality==MobileQuality.Performance,"Mali G710 must not match old low G71 rule; low reported GPU memory and frequency must not demote it");
        MobilePerformanceProfile.Current=MobileQuality.Performance;
        Check(!MobilePerformanceProfile.AutoModeEnabled,"explicitly choosing the recommended level is manual");
        Reset();Check(!MobilePerformanceProfile.AutoModeEnabled && MobilePerformanceProfile.Current==MobileQuality.Performance,"manual selection survives restart");
        RuntimePerformanceMonitor.UnderBudget=true;MobilePerformanceProfile.Apply(MobileQuality.Performance);
        Check(Math.Abs(pipeline.renderScale-1f)<.001 && pipeline.supportsHDR && pipeline.shadowDistance==38f && pipeline.msaaSampleCount==2,"FPS alone must not lower graphics on manual high");
        MobilePerformanceProfile.EnableAutoHardwareProfile();
        Check(MobilePerformanceProfile.AutoModeEnabled && MobilePerformanceProfile.Current==MobileQuality.Performance,"auto is explicitly enabled");
        MobilePerformanceProfile.Apply(MobileQuality.Performance);
        Check(Math.Abs(pipeline.renderScale-1f)<.001 && pipeline.supportsHDR && pipeline.shadowDistance==38f,"FPS alone must not lower GPU image settings in auto");
        Alterverse.Profile.AccountProfilePrefs.Account="b";SystemInfo.graphicsDeviceName="Adreno (TM) 740";Reset();
        Check(MobilePerformanceProfile.AutoModeEnabled && MobilePerformanceProfile.RecommendedQuality==MobileQuality.Performance,"auto and classification are account/device safe");
        SystemInfo.graphicsDeviceName="Mali-G52";Reset();Check(MobilePerformanceProfile.RecommendedQuality==MobileQuality.Battery,"recognized low GPU retains battery profile");
        Console.WriteLine("PASS: GPU classification, manual persistence, account switching and unverified FPS-pressure graphics.");
    }
}
