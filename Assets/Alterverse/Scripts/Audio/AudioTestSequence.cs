using System.Collections;using UnityEngine;
namespace Alterverse.Audio
{
public sealed class AudioTestSequence:MonoBehaviour
{
public static bool Enabled;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(!Application.isEditor&&!Debug.isDebugBuild)return;if(FindAnyObjectByType<AudioTestSequence>()==null)new GameObject("DF_AudioTest").AddComponent<AudioTestSequence>();}private void Update(){if(!Enabled)return;Enabled=false;StartCoroutine(Run());}private IEnumerator Run(){DimensionAudio.Play(DimensionSound.UiConfirm,.5f);yield return new WaitForSecondsRealtime(.18f);DimensionAudio.Play(DimensionSound.HitLight,.55f);yield return new WaitForSecondsRealtime(.18f);DimensionAudio.Play(DimensionSound.HitHeavy,.65f);yield return new WaitForSecondsRealtime(.22f);DimensionAudio.Play(DimensionSound.RecallStart,.55f);yield return new WaitForSecondsRealtime(.35f);DimensionAudio.Play(DimensionSound.RecallComplete,.62f);yield return new WaitForSecondsRealtime(.35f);DimensionAudio.Play(DimensionSound.RealityShatter,.72f);}
}
}
