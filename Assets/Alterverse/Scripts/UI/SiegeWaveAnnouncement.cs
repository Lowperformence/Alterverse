using Alterverse.Combat;using UnityEngine;
namespace Alterverse.UI
{
public sealed class SiegeWaveAnnouncement:MonoBehaviour
{
private string message="";private float until;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<SiegeWaveAnnouncement>()==null)new GameObject("DF_SiegeWaveAnnouncement").AddComponent<SiegeWaveAnnouncement>();}
private void OnEnable(){LaneWaveSpawner.SiegeWaveWarning+=OnWarning;LaneWaveSpawner.SiegeWaveSpawned+=OnSpawned;}
private void OnDisable(){LaneWaveSpawner.SiegeWaveWarning-=OnWarning;LaneWaveSpawner.SiegeWaveSpawned-=OnSpawned;}
private void OnWarning(float seconds){message=$"SIEGE MECHS IN {Mathf.CeilToInt(seconds)} SECONDS";until=Time.unscaledTime+3.5f;}
private void OnSpawned(){message="SIEGE MECHS DEPLOYED • BOTH LANES";until=Time.unscaledTime+4f;}
private void OnGUI(){if(Time.unscaledTime>=until||string.IsNullOrEmpty(message))return;GUIStyle s=new GUIStyle(GUI.skin.box){fontSize=Mathf.Clamp(Screen.width/42,16,28),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};float w=Mathf.Min(Screen.width*.72f,720f),h=54f;GUI.Box(new Rect((Screen.width-w)*.5f,Screen.safeArea.y+Screen.height*.09f,w,h),message,s);}
}
}