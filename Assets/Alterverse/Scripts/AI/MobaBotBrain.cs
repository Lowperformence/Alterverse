using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
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
        private BotPersonality personality;
        private BotTargetSensor targetSensor;
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
            personality = GetComponent<BotPersonality>();
            targetSensor = GetComponent<BotTargetSensor>();
            ApplyPersonality();

            aggression = Mathf.Clamp01(aggression + Random.Range(-0.06f, 0.06f));
            caution = Mathf.Clamp01(caution + Random.Range(-0.06f, 0.06f));
            decisionInterval += Random.Range(-0.05f, 0.08f);
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

        public void RefreshPersonality()
        {
            personality = GetComponent<BotPersonality>();
            targetSensor = GetComponent<BotTargetSensor>();
            ApplyPersonality();
        }

        private void ApplyPersonality()
        {
            if (personality == null) return;
            aggression = personality.Aggression;
            caution = personality.Caution;
            decisionInterval = personality.ReactionDelay;
            lowHealthThreshold = Mathf.Lerp(0.22f, 0.42f, personality.Caution);
        }

        private void Decide()
        {
            float hp = health.Normalized;

            if (hp <= lowHealthThreshold)
            {
                state = BotState.Retreat;
                return;
            }

            if ((state == BotState.Retreat || state == BotState.Recover) && hp < 0.80f)
            {
                state = BotState.Recover;
                return;
            }

            if (hp >= 0.80f && (state == BotState.Retreat || state == BotState.Recover))
            {
                state = BotState.Lane;
                return;
            }

            Health enemy = targetSensor != null ? targetSensor.CurrentTarget : null;
            if (enemy != null && !enemy.IsDead)
            {
                // Aggressive profiles accept fights more readily; cautious bots
                // need more health before committing.
                float requiredHealth = Mathf.Lerp(0.72f, 0.42f, aggression);
                if (hp >= requiredHealth)
                {
                    state = BotState.Fight;
                    return;
                }
            }

            state = BotState.Lane;
        }
    }
}
