using System;using UnityEngine;
namespace Alterverse.Match
{
public sealed class DimensionsFront:MonoBehaviour
{
[Header("Match")][SerializeField,Min(60f)]private float matchDurationSeconds=600f;[SerializeField,Range(0f,10f)]private float overtimeThresholdPercent=3f;[SerializeField,Min(30f)]private float overtimeDurationSeconds=120f;[Header("PvP-first influence")][SerializeField,Min(0f)]private float heroKillInfluence=3.5f;[SerializeField,Min(0f)]private float objectiveInfluence=2.5f;[SerializeField,Range(0f,100f)]private float lumenControl=50f;private float elapsed,overtimeElapsed;private bool ended,overtime,suddenDeath;private int winner=-1;
private void Awake(){if(GetComponent<Alterverse.Combat.BattleItemAirdropSchedule>()==null)gameObject.AddComponent<Alterverse.Combat.BattleItemAirdropSchedule>();}
public float ElapsedSeconds=>elapsed+overtimeElapsed;
public float ZeroControl=>100f-lumenControl;public float LumenControl=>lumenControl;public float TimeRemaining=>overtime?Mathf.Max(0,overtimeDurationSeconds-overtimeElapsed):Mathf.Max(0,matchDurationSeconds-elapsed);public bool HasEnded=>ended;public bool IsOvertime=>overtime;public bool IsSuddenDeath=>suddenDeath;public int Winner=>winner;public float HeroKillInfluence=>heroKillInfluence;public event Action<float,float> ControlChanged;public event Action<int> MatchEnded;public event Action OvertimeStarted;public event Action SuddenDeathStarted;
private void OnEnable(){if(ended)ResetMatch();}private void Update(){if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;if(ended||Time.timeScale<=0f)return;if(overtime){if(suddenDeath)return;overtimeElapsed+=Time.deltaTime;if(overtimeElapsed>=overtimeDurationSeconds)ResolveOvertimeEnd();return;}elapsed+=Time.deltaTime;if(elapsed>=matchDurationSeconds)ResolveTimerEnd();}public void ResetMatch(){GetComponent<Alterverse.Combat.BattleItemAirdropSchedule>()?.ResetSchedule();elapsed=0f;overtimeElapsed=0f;ended=false;overtime=false;suddenDeath=false;winner=-1;lumenControl=50f;ControlChanged?.Invoke(ZeroControl,LumenControl);}
public void ApplyNetworkState(float remaining,float zero,float lumen,bool networkOvertime,bool networkSuddenDeath,bool networkEnded,int networkWinner)
{
bool wasOvertime=overtime,wasSudden=suddenDeath,wasEnded=ended;
lumenControl=Mathf.Clamp(lumen,0,100);
overtime=networkOvertime;suddenDeath=networkSuddenDeath;ended=networkEnded;winner=networkWinner;
if(overtime)overtimeElapsed=Mathf.Clamp(overtimeDurationSeconds-Mathf.Max(0,remaining),0,overtimeDurationSeconds);
else elapsed=Mathf.Clamp(matchDurationSeconds-Mathf.Max(0,remaining),0,matchDurationSeconds);
ControlChanged?.Invoke(ZeroControl,LumenControl);
if(!wasOvertime&&overtime)OvertimeStarted?.Invoke();
if(!wasSudden&&suddenDeath)SuddenDeathStarted?.Invoke();
if(!wasEnded&&ended&&winner>=0)MatchEnded?.Invoke(winner);
}public void RegisterHeroKill(int team)=>Push(team,heroKillInfluence);public void RegisterHeroKill(int team,float worldZ)=>Push(team,heroKillInfluence);/* Legacy hooks stay callable, but lane minions and towers no longer create match progress. */public void RegisterMinionKill(int team){}public void RegisterTowerDestroyed(int team){}public void RegisterObjectiveCaptured(int team)=>Push(team,objectiveInfluence);
public void Push(int scoringTeam,float influence){if(ended||influence<=0||(scoringTeam!=0&&scoringTeam!=1))return;lumenControl=Mathf.Clamp(lumenControl+(scoringTeam==1?influence:-influence),0,100);ControlChanged?.Invoke(ZeroControl,LumenControl);if(lumenControl<=0f){EndMatch(0);return;}if(lumenControl>=100f){EndMatch(1);return;}if(suddenDeath&&!Mathf.Approximately(lumenControl,50f))EndMatch(lumenControl>50?1:0);}public void RegisterCoreDestroyed(int destroyedCoreTeam){if(!ended&&(destroyedCoreTeam==0||destroyedCoreTeam==1))EndMatch(destroyedCoreTeam==0?1:0);}private void ResolveTimerEnd(){elapsed=matchDurationSeconds;float gap=Mathf.Abs(LumenControl-ZeroControl);if(gap<=overtimeThresholdPercent){overtime=true;overtimeElapsed=0f;OvertimeStarted?.Invoke();return;}EndMatch(lumenControl>50?1:0);}private void ResolveOvertimeEnd(){overtimeElapsed=overtimeDurationSeconds;if(Mathf.Approximately(lumenControl,50f)){suddenDeath=true;SuddenDeathStarted?.Invoke();return;}EndMatch(lumenControl>50?1:0);}private void EndMatch(int winningTeam){if(ended)return;ended=true;overtime=false;suddenDeath=false;winner=winningTeam;MatchEnded?.Invoke(winningTeam);}
}
}
