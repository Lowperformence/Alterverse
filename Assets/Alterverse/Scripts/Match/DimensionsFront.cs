using System;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>
    /// Core tug-of-war state for ALTERVERSE: Dimensions Front.
    /// 50 = equal map control. Values below 50 favour Zero, above 50 favour Lumen.
    /// The system deliberately allows the front to move in both directions until match end.
    /// </summary>
    public sealed class DimensionsFront : MonoBehaviour
    {
        [Header("Match")]
        [SerializeField, Min(60f)] private float matchDurationSeconds = 600f;
        [SerializeField, Range(0f, 100f)] private float lumenControl = 50f;

        [Header("Prototype influence")]
        [SerializeField, Min(0f)] private float heroKillInfluence = 4f;
        [SerializeField, Min(0f)] private float minionInfluence = 0.12f;
        [SerializeField, Min(0f)] private float towerInfluence = 1.5f;
        [SerializeField, Min(0f)] private float objectiveInfluence = 3f;

        private float elapsed;
        private bool ended;

        public float ZeroControl => 100f - lumenControl;
        public float LumenControl => lumenControl;
        public float TimeRemaining => Mathf.Max(0f, matchDurationSeconds - elapsed);
        public bool HasEnded => ended;

        public event Action<float, float> ControlChanged;
        public event Action<int> MatchEnded;

        private void Update()
        {
            if (ended) return;

            elapsed += Time.deltaTime;
            if (elapsed >= matchDurationSeconds)
                ResolveTimerEnd();
        }

        public void RegisterHeroKill(int scoringTeam) => Push(scoringTeam, heroKillInfluence);
        public void RegisterMinionKill(int scoringTeam) => Push(scoringTeam, minionInfluence);
        public void RegisterTowerDestroyed(int scoringTeam) => Push(scoringTeam, towerInfluence);
        public void RegisterObjectiveCaptured(int scoringTeam) => Push(scoringTeam, objectiveInfluence);

        public void Push(int scoringTeam, float influence)
        {
            if (ended || influence <= 0f) return;

            float signed = scoringTeam == 1 ? influence : -influence;
            lumenControl = Mathf.Clamp(lumenControl + signed, 0f, 100f);
            ControlChanged?.Invoke(ZeroControl, LumenControl);
        }

        /// <summary>
        /// Call when a Reality Core is destroyed. The opposing team wins immediately.
        /// </summary>
        public void RegisterCoreDestroyed(int destroyedCoreTeam)
        {
            if (ended) return;
            EndMatch(destroyedCoreTeam == 0 ? 1 : 0);
        }

        private void ResolveTimerEnd()
        {
            if (Mathf.Approximately(lumenControl, 50f))
            {
                // Tie: keep playing until the front moves. No arbitrary kill-count tiebreaker.
                elapsed = matchDurationSeconds;
                return;
            }

            EndMatch(lumenControl > 50f ? 1 : 0);
        }

        private void EndMatch(int winningTeam)
        {
            ended = true;
            MatchEnded?.Invoke(winningTeam);
        }
    }
}
