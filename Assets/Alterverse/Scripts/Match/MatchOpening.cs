using System.Collections;using UnityEngine;
namespace Alterverse.Match
{
public sealed class MatchOpening:MonoBehaviour
{
[SerializeField]private float messageSeconds=2.2f;private string display="";private bool visible,ownsPause;private float previousScale=1f;private GUIStyle title,sub;
private IEnumerator Start(){while(GameObject.Find("DF_MainMenu")!=null)yield return null;previousScale=Time.timeScale;Time.timeScale=0f;ownsPause=true;visible=true;display="REALITIES HAVE COLLIDED.|Fight for your reality. Drive your universe across the Front.";yield return new WaitForSecondsRealtime(messageSeconds);for(int i=3;i>=1;i--){display=i.ToString();yield return new WaitForSecondsRealtime(.72f);}display="FIGHT!";yield return new WaitForSecondsRealtime(.55f);visible=false;RestoreTimeScale();}
private void RestoreTimeScale(){if(!ownsPause)return;Time.timeScale=previousScale;ownsPause=false;}private void OnDisable(){RestoreTimeScale();}private void OnDestroy(){RestoreTimeScale();}
private void OnGUI(){if(!visible)return;float scale=Mathf.Clamp(Screen.height/900f,.72f,1.25f);EnsureStyles(scale);string[] parts=display.Split('|');Rect panel=new(Screen.width*.15f,Screen.height*.32f,Screen.width*.70f,Screen.height*.30f);GUI.Box(panel,GUIContent.none);if(parts.Length==1)GUI.Label(panel,parts[0],title);else{GUI.Label(new Rect(panel.x+20*scale,panel.y+25*scale,panel.width-40*scale,80*scale),parts[0],title);GUI.Label(new Rect(panel.x+30*scale,panel.y+112*scale,panel.width-60*scale,60*scale),parts[1],sub);}}
private void EnsureStyles(float scale){int titleSize=Mathf.RoundToInt(34*scale);if(title!=null&&title.fontSize==titleSize)return;title=new GUIStyle(GUI.skin.label){fontSize=titleSize,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true};sub=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(20*scale),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true};}
}
}
