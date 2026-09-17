using UnityEngine;
namespace Alterverse.Match
{
public sealed class DimensionsFrontHUD:MonoBehaviour
{
private DimensionsFront front;private GUIStyle label,timer,overtime;private float nextFind;private void Update(){if(front==null&&Time.unscaledTime>=nextFind){nextFind=Time.unscaledTime+.5f;front=FindAnyObjectByType<DimensionsFront>();}}
private void OnGUI(){if(front==null)return;label??=new GUIStyle(GUI.skin.label){fontSize=14,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};timer??=new GUIStyle(label){fontSize=18};overtime??=new GUIStyle(label){fontSize=20};float width=Mathf.Min(430,Screen.width*.56f),x=(Screen.width-width)*.5f,y=96;GUI.Box(new Rect(x,y,width,52),GUIContent.none);float zero=front.ZeroControl/100f;GUI.Box(new Rect(x+8,y+28,(width-16)*zero,13),GUIContent.none);GUI.Label(new Rect(x+8,y+3,width-16,23),front.IsOvertime?"OVERTIME":Format(front.TimeRemaining),front.IsOvertime?overtime:timer);GUI.Label(new Rect(x+8,y+28,90,18),$"ZERO {front.ZeroControl:0}%",label);GUI.Label(new Rect(x+width-98,y+28,90,18),$"{front.LumenControl:0}% LUMEN",label);}
private static string Format(float seconds){int total=Mathf.CeilToInt(seconds);return $"{total/60:00}:{total%60:00}";}
}
}
