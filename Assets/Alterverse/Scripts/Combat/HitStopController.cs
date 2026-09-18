using System.Collections;
using Alterverse.Match;
using UnityEngine;
namespace Alterverse.Combat
{
    public sealed class HitStopController : MonoBehaviour
    {
        private static HitStopController instance;
        private Coroutine active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate(){if(FindAnyObjectByType<HitStopController>()==null)new GameObject("DF_HitStop").AddComponent<HitStopController>();}
        private void Awake(){instance=this;}
        public static void Pulse(float seconds=.035f,float scale=.16f)
        {
            if(instance==null||MatchClock.Paused)return;
            if(instance.active!=null)instance.StopCoroutine(instance.active);
            instance.active=instance.StartCoroutine(instance.Run(seconds,scale));
        }
        private IEnumerator Run(float seconds,float scale)
        {
            MatchClock.SetImpact(scale);
            yield return new WaitForSecondsRealtime(Mathf.Clamp(seconds,.01f,.08f));
            MatchClock.SetImpact(1);active=null;
        }
        private void OnDisable(){if(active!=null)StopCoroutine(active);active=null;MatchClock.SetImpact(1);}
        private void OnDestroy(){if(instance==this)instance=null;}
    }
}
