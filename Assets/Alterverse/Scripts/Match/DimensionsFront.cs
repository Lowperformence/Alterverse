using System;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>Three-lane tug-of-war. Overall map control is the average of all local fronts.</summary>
    public sealed class DimensionsFront : MonoBehaviour
    {
        [Header("Match")]
        [SerializeField, Min(60f)] private float matchDurationSeconds=600f;
        [Header("PvP-first influence")]
        [SerializeField, Min(0f)] private float heroKillInfluence=3.5f;
        [SerializeField, Min(0f)] private float minionInfluence=.05f;
        [SerializeField, Min(0f)] private float towerInfluence=1.25f;
        [SerializeField, Min(0f)] private float objectiveInfluence=2.5f;
        private readonly float[] lumenLaneControl={50f,50f,50f}; // bot, mid, top
        private float elapsed;private bool ended;private bool overtime;
        public float ZeroControl=>100f-LumenControl;
        public float LumenControl=>(lumenLaneControl[0]+lumenLaneControl[1]+lumenLaneControl[2])/3f;
        public float BotLumenControl=>lumenLaneControl[0];public float MidLumenControl=>lumenLaneControl[1];public float TopLumenControl=>lumenLaneControl[2];
        public float TimeRemaining=>Mathf.Max(0f,matchDurationSeconds-elapsed);public bool HasEnded=>ended;public bool IsOvertime=>overtime;
        public event Action<float,float> ControlChanged;public event Action<int,float> LaneControlChanged;public event Action<int> MatchEnded;
        private void Update(){if(ended||overtime)return;elapsed+=Time.deltaTime;if(elapsed>=matchDurationSeconds)ResolveTimerEnd();}
        public void RegisterHeroKill(int team)=>Push(team,heroKillInfluence);
        public void RegisterHeroKill(int team,float worldZ)=>PushLane(team,heroKillInfluence,worldZ);
        public void RegisterMinionKill(int team)=>Push(team,minionInfluence);
        public void RegisterTowerDestroyed(int team)=>Push(team,towerInfluence);
        public void RegisterObjectiveCaptured(int team)=>Push(team,objectiveInfluence);
        public void Push(int scoringTeam,float influence){if(!CanPush(scoringTeam,influence))return;for(int i=0;i<3;i++)lumenLaneControl[i]=Shift(lumenLaneControl[i],scoringTeam,influence);NotifyAll();ResolveOvertime();}
        public void PushLane(int scoringTeam,float influence,float worldZ){if(!CanPush(scoringTeam,influence))return;int lane=LaneIndex(worldZ);lumenLaneControl[lane]=Shift(lumenLaneControl[lane],scoringTeam,influence);LaneControlChanged?.Invoke(lane,lumenLaneControl[lane]);ControlChanged?.Invoke(ZeroControl,LumenControl);ResolveOvertime();}
        public float GetLaneLumenControl(int lane)=>lane>=0&&lane<3?lumenLaneControl[lane]:50f;
        public void RegisterCoreDestroyed(int destroyedCoreTeam){if(!ended&&(destroyedCoreTeam==0||destroyedCoreTeam==1))EndMatch(destroyedCoreTeam==0?1:0);}
        private bool CanPush(int team,float influence)=>!ended&&influence>0f&&(team==0||team==1);
        private static float Shift(float value,int team,float influence)=>Mathf.Clamp(value+(team==1?influence:-influence),0f,100f);
        private static int LaneIndex(float z)=>z>6f?2:z<-6f?0:1;
        private void NotifyAll(){for(int i=0;i<3;i++)LaneControlChanged?.Invoke(i,lumenLaneControl[i]);ControlChanged?.Invoke(ZeroControl,LumenControl);}
        private void ResolveOvertime(){if(overtime&&!Mathf.Approximately(LumenControl,50f))EndMatch(LumenControl>50f?1:0);}
        private void ResolveTimerEnd(){elapsed=matchDurationSeconds;if(Mathf.Approximately(LumenControl,50f)){overtime=true;return;}EndMatch(LumenControl>50f?1:0);}
        private void EndMatch(int winningTeam){ended=true;overtime=false;MatchEnded?.Invoke(winningTeam);}
    }
}
