using System;using UnityEngine;
namespace Alterverse.Match
{
public sealed class DimensionsFront:MonoBehaviour
{
[Header("Match")][SerializeField,Min(60f)]private float matchDurationSeconds=600f;[Header("PvP-first influence")][SerializeField,Min(0f)]private float heroKillInfluence=3.5f;[SerializeField,Min(0f)]private float minionInfluence=.05f;[SerializeField,Min(0f)]private float towerInfluence=1.25f;[SerializeField,Min(0f)]private float objectiveInfluence=2.5f;[SerializeField,Range(0f,100f)]private float lumenControl=50f;private float elapsed;private bool ended,overtime;
public float ZeroControl=>100f-lumenControl;public float LumenControl=>lumenControl;public float TimeRemaining=>Mathf.Max(0,matchDurationSeconds-elapsed);public bool HasEnded=>ended;public bool IsOvertime=>overtime;public float HeroKillInfluence=>heroKillInfluence;public event Action<float,float> ControlChanged;public event Action<int> MatchEnded;
private void Update(){if(ended||overtime)return;elapsed+=Time.deltaTime;if(elapsed>=matchDurationSeconds)ResolveTimerEnd();}public void RegisterHeroKill(int team)=>Push(team,heroKillInfluence);public void RegisterHeroKill(int team,float worldZ)=>Push(team,heroKillInfluence);public void RegisterMinionKill(int team)=>Push(team,minionInfluence);public void RegisterTowerDestroyed(int team)=>Push(team,towerInfluence);public void RegisterObjectiveCaptured(int team)=>Push(team,objectiveInfluence);
public void Push(int scoringTeam,float influence){if(ended||influence<=0||(scoringTeam!=0&&scoringTeam!=1))return;lumenControl=Mathf.Clamp(lumenControl+(scoringTeam==1?influence:-influence),0,100);ControlChanged?.Invoke(ZeroControl,LumenControl);if(overtime&&!Mathf.Approximately(lumenControl,50))EndMatch(lumenControl>50?1:0);}public void RegisterCoreDestroyed(int destroyedCoreTeam){if(!ended&&(destroyedCoreTeam==0||destroyedCoreTeam==1))EndMatch(destroyedCoreTeam==0?1:0);}private void ResolveTimerEnd(){elapsed=matchDurationSeconds;if(Mathf.Approximately(lumenControl,50)){overtime=true;return;}EndMatch(lumenControl>50?1:0);}private void EndMatch(int winningTeam){ended=true;overtime=false;MatchEnded?.Invoke(winningTeam);}
}
}
