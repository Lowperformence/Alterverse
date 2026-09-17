using System.Collections;using UnityEngine;
namespace Alterverse.Combat
{
public sealed class HitStopController:MonoBehaviour
{
private static HitStopController instance;private Coroutine active;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<HitStopController>()==null)new GameObject("DF_HitStop").AddComponent<HitStopController>();}private void Awake(){instance=this;}public static void Pulse(float seconds=.035f,float scale=.16f){if(instance==null||Time.timeScale<=0f)return;if(instance.active!=null)instance.StopCoroutine(instance.active);instance.active=instance.StartCoroutine(instance.Run(seconds,scale));}private IEnumerator Run(float seconds,float scale){float previous=Time.timeScale;Time.timeScale=Mathf.Clamp(scale,.05f,1f);yield return new WaitForSecondsRealtime(Mathf.Clamp(seconds,.01f,.08f));if(Time.timeScale>0f)Time.timeScale=previous;active=null;}private void OnDestroy(){if(instance==this)instance=null;}
}
}
