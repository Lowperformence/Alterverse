using System;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>
    /// Core tug-of-war state for ALTERVERSE. PvP is the primary source of momentum;
    /// map systems support fights instead of replacing them.
    /// </summary>
    public sealed class DimensionsFront : MonoBehaviour
    {
        [Header("Match")]
        [SerializeField, Min(60f)] private float matchDurationSeconds = 600f;
        [SerializeField, Range(0f, 100f)] private float lumenControl = 50f;

        [Header("Prototype influence")]
        [SerializeField, Min(0f)] private float heroKillInfluence = 2f;
        [SerializeField, Min(0f)] private float minionInfluence = 0.1f;
        [SerializeField, Min(0f)] private float towerInfluence = 5f;
        [SerializeField, Min(0f)] private float objectiveInfluence = 3f;

        private float elapsed;
        private bool ended;
        private bool overtime;

        public float ZeroControl => 100f - lumenControl;
        public float LumenControl => lumenControl;
        public float TimeRemaining => Mathf.Max(0f, matchDurationSeconds - elapsed);
        public bool HasEnded => ended;
        public bool IsOvertime => overtime;

        public event Action<float, float> ControlChanged;
        public event Action<int> MatchEnded;

        private void Update()
        {
            if (ended || overtime) return;
            elapsed += Time.deltaTime;
            if (elapsed >= matchDurationSeconds) ResolveTimerEnd();
        }

        public void RegisterHeroKill(int scoringTeam) => Push(scoringTeam, heroKillInfluence);
        public void RegisterMinionKill(int scoringTeam) => Push(scoringTeam, minionInfluence);
        public void RegisterTowerDestroyed(int scoringTeam) => Push(scoringTeam, towerInfluence);
        public void RegisterObjectiveCaptured(int scoringTeam) => Push(scoringTeam, objectiveInfluence);

        public void Push(int scoringTeam, float influence)
        {
            if (ended || influence <= 0f) return;
            lumenControl = Mathf.Clamp(lumenControl + (scoringTeam == 1 ? influence : -influence), 0f, 100f);
            ControlChanged?.Invoke(ZeroControl, LumenControl);

            // At a 50/50 timer tie, the next meaningful shift ends Sudden Convergence.
            if (overtime && !Mathf.Approximately(lumenControl, 50f))
                EndMatch(lumenControl > 50f ? 1 : 0);
        }

        public void RegisterCoreDestroyed(int destroyedCoreTeam)
        {
            if (!ended) EndMatch(destroyedCoreTeam == 0 ? 1 : 0);
        }

        private void ResolveTimerEnd()
        {
            elapsed = matchDurationSeconds;
            if (Mathf.Approximately(lumenControl, 50f))
            {
                overtime = true;
                return;
            }
            EndMatch(lumenControl > 50f ? 1 : 0);
        }

        private void EndMatch(int winningTeam)
        {
            ended = true;
            overtime = false;
            MatchEnded?.Invoke(winningTeam);
        }
    }
}
