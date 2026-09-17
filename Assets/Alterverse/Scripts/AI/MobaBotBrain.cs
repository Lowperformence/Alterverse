using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    /// <summary>
    /// Lightweight decision layer for ALTERVERSE bot heroes.
    /// Kept independent from the current prototype bot so the tested runtime stays unchanged.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class MobaBotBrain : MonoBehaviour
    {
        public enum BotState { Lane, Fight, Retreat, Recover }

        [SerializeField] private int team;
        [SerializeField] private float laneZ;
        [SerializeField, Range(0.1f, 1f)] private float aggression = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float caution = 0.55f;
        [SerializeField] private float decisionInterval = 0.35f;
        [SerializeField] private float lowHealthThreshold = 0.30f;

        private Health health;
        private BotState state = BotState.Lane;
        private float nextDecisionTime;

        public int Team => team;
        public float LaneZ => laneZ;
        public BotState State => state;
        public float Aggression => aggression;
        public float Caution => caution;

        private void Awake()
        {
            health = GetComponent<Health>();
            // Small per-bot variation prevents synchronized, robotic decisions.
            aggression = Mathf.Clamp01(aggression + Random.Range(-0.12f, 0.12f));
            caution = Mathf.Clamp01(caution + Random.Range(-0.12f, 0.12f));
            decisionInterval += Random.Range(-0.08f, 0.12f);
        }

        private void Update()
        {
            if (health == null || health.IsDead || Time.time < nextDecisionTime) return;
            nextDecisionTime = Time.time + Mathf.Max(0.15f, decisionInterval);
            Decide();
        }

        public void Initialize(int assignedTeam, float assignedLaneZ, float personalityAggression = 0.55f, float personalityCaution = 0.55f)
        {
            team = assignedTeam;
            laneZ = assignedLaneZ;
            aggression = Mathf.Clamp01(personalityAggression);
            caution = Mathf.Clamp01(personalityCaution);
        }

        private void Decide()
        {
            float hp = health.Normalized;

            if (hp <= lowHealthThreshold)
            {
                state = BotState.Retreat;
                return;
            }

            if (state == BotState.Retreat && hp < 0.80f)
            {
                state = BotState.Recover;
                return;
            }

            if (hp >= 0.80f && (state == BotState.Retreat || state == BotState.Recover))
            {
                state = BotState.Lane;
                return;
            }

            // Actual target evaluation and movement are deliberately connected later,
            // after the current map/minion/tower build passes its Unity stability test.
            state = BotState.Lane;
        }
    }
}
