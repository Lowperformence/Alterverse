using UnityEngine;
namespace Alterverse.Onboarding
{
public sealed class OnboardingHud:MonoBehaviour
{
private GUIStyle title,body,skip;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<OnboardingHud>()==null)new GameObject("DF_Onboarding").AddComponent<OnboardingHud>();}private void OnGUI(){if(OnboardingProgress.Complete||Time.timeScale<=0f)return;Ensure();OnboardingStep step=OnboardingProgress.Current;float w=Mathf.Min(440f,Screen.width*.76f),h=96f,x=(Screen.width-w)*.5f,y=Screen.height*.12f;GUI.Box(new Rect(x,y,w,h),GUIContent.none);GUI.Label(new Rect(x+16,y+8,w-32,26),OnboardingCopy.Title(step),title);GUI.Label(new Rect(x+18,y+35,w-36,42),OnboardingCopy.Body(step),body);if(GUI.Button(new Rect(x+w-70,y+h-25,60,20),"SKIP",skip))OnboardingProgress.Skip();}private void Ensure(){title??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=16};body??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.UpperCenter,wordWrap=true,fontSize=12};skip??=new GUIStyle(GUI.skin.button){fontSize=10};}
}
}
