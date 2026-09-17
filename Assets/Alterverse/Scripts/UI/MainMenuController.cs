using UnityEngine;
namespace Alterverse.UI
{
public sealed class MainMenuController:MonoBehaviour
{
private static MainMenuController instance;private bool visible=true;private GUIStyle title,subtitle,button,small;private Texture2D dimTexture;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<MainMenuController>()==null)new GameObject("AV_MainMenu").AddComponent<MainMenuController>();}
private void Awake(){if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;Time.timeScale=0f;dimTexture=new Texture2D(1,1);dimTexture.SetPixel(0,0,new Color(.015f,.01f,.035f,.94f));dimTexture.Apply();}
private void OnDestroy(){if(instance==this)instance=null;if(dimTexture!=null)Destroy(dimTexture);}
private void OnGUI(){if(!visible)return;EnsureStyles();GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),dimTexture);float w=Mathf.Min(560f,Screen.width*.82f),x=(Screen.width-w)*.5f;float center=Screen.height*.26f;GUI.Label(new Rect(x,center,w,80),"ALTERVERSE",title);GUI.Label(new Rect(x,center+72,w,38),"FIGHT FOR YOUR REALITY",subtitle);float bw=Mathf.Min(360f,w*.78f),bx=(Screen.width-bw)*.5f,by=Mathf.Max(center+155f,Screen.height*.52f);if(GUI.Button(new Rect(bx,by,bw,58),"SPIELEN",button))Play();GUI.enabled=false;GUI.Button(new Rect(bx,by+72,bw,50),"HELDEN",button);GUI.Button(new Rect(bx,by+134,bw,50),"RANGLISTE",button);GUI.enabled=true;GUI.Label(new Rect(x,Screen.height-62,w,30),"EARLY PROTOTYPE  •  DIMENSIONS FRONT",small);}
private void Play(){visible=false;Time.timeScale=1f;gameObject.SetActive(false);}
private void EnsureStyles(){if(title!=null)return;title=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Screen.width/18,36,64),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};subtitle=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Screen.width/45,15,24),alignment=TextAnchor.MiddleCenter};button=new GUIStyle(GUI.skin.button){fontSize=Mathf.Clamp(Screen.width/40,18,28),fontStyle=FontStyle.Bold};small=new GUIStyle(GUI.skin.label){fontSize=13,alignment=TextAnchor.MiddleCenter};}
}
}
