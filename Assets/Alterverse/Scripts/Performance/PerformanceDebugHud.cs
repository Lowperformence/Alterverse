using UnityEngine;
namespace Alterverse.Performance
{
public sealed class PerformanceDebugHud:MonoBehaviour
{
public static bool Visible{get;set;}private GUIStyle style;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(!Application.isEditor&&!Debug.isDebugBuild)return;if(FindAnyObjectByType<PerformanceDebugHud>()==null)new GameObject("DF_PerfDebug").AddComponent<PerformanceDebugHud>();}private void OnGUI(){if(!Visible)return;style??=new GUIStyle(GUI.skin.label){fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};string state=RuntimePerformanceMonitor.UnderBudget?"UNDER BUDGET":"STABLE";GUI.Box(new Rect(10,10,190,62),GUIContent.none);GUI.Label(new Rect(18,14,174,20),$"FPS {RuntimePerformanceMonitor.SmoothedFps:0} • {state}",style);GUI.Label(new Rect(18,34,174,20),$"{MobilePerformanceProfile.Current} • VFX {AdaptiveVfxBudget.Intensity*100f:0}%",style);}
}
}
