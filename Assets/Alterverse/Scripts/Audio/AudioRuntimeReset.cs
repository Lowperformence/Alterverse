using Alterverse.Match;using UnityEngine;
namespace Alterverse.Audio
{
public sealed class AudioRuntimeReset:MonoBehaviour
{
private DimensionsFront front;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<AudioRuntimeReset>()==null)new GameObject("DF_AudioRuntime").AddComponent<AudioRuntimeReset>();}private void Start(){AudioPriorityLimiter.Reset();front=FindAnyObjectByType<DimensionsFront>();if(front!=null)front.MatchEnded+=OnMatchEnded;}private void OnMatchEnded(int team)=>AudioPriorityLimiter.Reset();private void OnDestroy(){if(front!=null)front.MatchEnded-=OnMatchEnded;}
}
}
