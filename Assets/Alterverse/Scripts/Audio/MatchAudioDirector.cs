using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Match;using UnityEngine;
namespace Alterverse.Audio
{
public sealed class MatchAudioDirector:MonoBehaviour
{
private DimensionsFront front;private int playerTeam=-1;private float nextResolve;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<MatchAudioDirector>()==null)new GameObject("DF_MatchAudio").AddComponent<MatchAudioDirector>();}private void Update(){if(front!=null||Time.unscaledTime<nextResolve)return;nextResolve=Time.unscaledTime+.5f;front=FindAnyObjectByType<DimensionsFront>();TeamMember member=LocalHeroContext.Member;playerTeam=member!=null?member.Team:-1;if(front==null)return;front.OvertimeStarted+=OnOvertime;front.SuddenDeathStarted+=OnSuddenDeath;front.MatchEnded+=OnMatchEnded;}private void OnOvertime(){if(AudioCuePolicy.Allow("overtime",AudioCuePriority.Critical))DimensionAudio.Play(DimensionSound.Overtime,.7f*AudioMixState.CriticalGain);}private void OnSuddenDeath(){if(AudioCuePolicy.Allow("sudden-death",AudioCuePriority.Critical))DimensionAudio.Play(DimensionSound.SuddenDeath,.82f*AudioMixState.CriticalGain);}private void OnMatchEnded(int winner){AudioMixState.Reset();TeamMember member=LocalHeroContext.Member;playerTeam=member!=null?member.Team:-1;DimensionAudio.Play(playerTeam>=0&&winner==playerTeam?DimensionSound.Victory:DimensionSound.Defeat,.82f);}private void OnDestroy(){if(front==null)return;front.OvertimeStarted-=OnOvertime;front.SuddenDeathStarted-=OnSuddenDeath;front.MatchEnded-=OnMatchEnded;}
}
}
