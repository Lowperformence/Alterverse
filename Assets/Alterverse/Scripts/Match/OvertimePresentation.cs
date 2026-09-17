using System.Collections;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.Match
{
public sealed class OvertimePresentation:MonoBehaviour
{
private DimensionsFront front;private string message;private float visibleUntil;private GUIStyle title,sub;private Texture2D pixel;
private void Awake(){pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();}private void Start(){front=FindAnyObjectByType<DimensionsFront>();if(front!=null)front.OvertimeStarted+=OnOvertime;}private void OnOvertime(){message="REALITY DEADLOCK";visibleUntil=Time.unscaledTime+3.2f;StartCoroutine(SwapMessage());}private IEnumerator SwapMessage(){yield return new WaitForSecondsRealtime(1.35f);message="+2:00  OVERTIME";}
private void OnGUI(){if(string.IsNullOrEmpty(message)||Time.unscaledTime>visibleUntil||pixel==null)return;float scale=Mathf.Clamp(Screen.height/900f,.72f,1.25f),w=Mathf.Min(Screen.width*.72f,720*scale),h=118*scale,x=(Screen.width-w)*.5f,y=Screen.height*.29f;Color old=GUI.color;GUI.color=new Color(.015f,.01f,.025f,.88f);GUI.DrawTexture(new Rect(x,y,w,h),pixel);float pulse=.55f+.35f*Mathf.Sin(Time.unscaledTime*8f);GUI.color=new Color(StylizedArtDirection.WarmAccent.r,StylizedArtDirection.WarmAccent.g,StylizedArtDirection.WarmAccent.b,pulse);GUI.DrawTexture(new Rect(x,y,w,3*scale),pixel);GUI.DrawTexture(new Rect(x,y+h-3*scale,w,3*scale),pixel);GUI.color=old;EnsureStyles(scale);GUI.Label(new Rect(x+12*scale,y+12*scale,w-24*scale,54*scale),message,title);GUI.Label(new Rect(x+12*scale,y+68*scale,w-24*scale,30*scale),"THE FRONT REMAINS UNRESOLVED",sub);}
private void EnsureStyles(float scale){int size=Mathf.RoundToInt(29*scale);if(title!=null&&title.fontSize==size)return;title=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};sub=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(13*scale),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};}private void OnDestroy(){if(front!=null)front.OvertimeStarted-=OnOvertime;if(pixel!=null)Destroy(pixel);}
}
}
