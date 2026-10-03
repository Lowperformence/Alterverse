using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Audio
{
    public sealed class AudioRuntimeReset : MonoBehaviour
    {
        private DimensionsFront front;
        private float nextResolve;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if(FindAnyObjectByType<AudioRuntimeReset>()==null)
                new GameObject("DF_AudioRuntime").AddComponent<AudioRuntimeReset>();
        }
        private void Start()=>ResetState();
        private void Update()
        {
            if(Time.unscaledTime<nextResolve)return;
            nextResolve=Time.unscaledTime+.25f;
            DimensionsFront current=FindAnyObjectByType<DimensionsFront>();
            if(current==front)return;
            Unsubscribe();front=current;
            ResetState();
            if(front!=null)front.MatchEnded+=OnMatchEnded;
        }
        private static void ResetState(){AudioPriorityLimiter.Reset();AudioMixState.Reset();}
        private void OnMatchEnded(int team)=>ResetState();
        private void Unsubscribe(){if(front!=null)front.MatchEnded-=OnMatchEnded;}
        private void OnDestroy()=>Unsubscribe();
    }
}
