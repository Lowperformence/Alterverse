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
        [SerializeField, Range(.1f, 1f)] private float aggression = .55f, caution = .55f;
        [SerializeField] private float decisionInterval = .35f, lowHealthThreshold = .30f;
        private Health health;
        private MobaBotRespawn respawn;
        private BotPersonality personality;
        private BotTargetSensor targetSensor;
        private BotState state = BotState.Lane;
        private float nextDecisionTime;
        private bool wasDead;
        private System.Random decisions;
        private float lastHealth, threatUntil;
        public BotDifficulty DifficultyLevel { get; private set; }
        public BotDifficultyProfile Difficulty { get; private set; }
        public bool GroupWithAllies { get; private set; }
        public float TeamFocus=>personality!=null?personality.TeamFocus:.6f;
        public float ObjectiveFocus=>personality!=null?personality.ObjectiveFocus:.6f;
        public bool Chance(float probability)=>decisions.NextDouble()<Mathf.Clamp01(probability);
        public int Team => team;
        public float LaneZ => laneZ;
        public BotState State => state;
        public float Aggression => aggression;
        public float Caution => caution;

        private void Awake()
        {
            DifficultyLevel=BotDifficultySettings.Selected;Difficulty=BotDifficultyProfile.For(DifficultyLevel);
            // We only need per-bot entropy for the local RNG; an object hash is sufficient and avoids obsolete InstanceID APIs in Unity 6.6+.
            decisions=new System.Random(unchecked(GetHashCode()*397^System.Environment.TickCount));
            health = GetComponent<Health>();lastHealth=health.Normalized; respawn = GetComponent<MobaBotRespawn>();
            personality = GetComponent<BotPersonality>(); targetSensor = GetComponent<BotTargetSensor>();
            ApplyPersonality();
        }
        private void Update()
        {
            if (Time.timeScale <= 0f || health == null) return;
            // The roster adds respawn after the brain, so Awake alone cannot resolve it.
            if (respawn == null) respawn = GetComponent<MobaBotRespawn>();
            if (targetSensor == null) targetSensor = GetComponent<BotTargetSensor>();
            if (health.IsDead) { wasDead = true; return; }
            if (respawn != null && respawn.IsRespawning) return;
            if (wasDead) ResumeLaneAfterRespawn();
            if (Time.time < nextDecisionTime) return;
            nextDecisionTime = Time.time + Mathf.Max(.15f, decisionInterval*Difficulty.DecisionScale);
            Decide();
        }
        public void Initialize(int assignedTeam, float assignedLaneZ, float personalityAggression = .55f, float personalityCaution = .55f)
        {
            team = assignedTeam; laneZ = assignedLaneZ;
            aggression = Mathf.Clamp01(personalityAggression); caution = Mathf.Clamp01(personalityCaution);
            lowHealthThreshold = Mathf.Lerp(.22f, .42f, caution);
            state = BotState.Lane; nextDecisionTime = 0;
        }
        public void ResumeLaneAfterRespawn()
        {
            wasDead = false;threatUntil=0;lastHealth=health.Normalized;GroupWithAllies=false; state = BotState.Lane; nextDecisionTime = Time.time + .1f;
            GetComponent<BotTargetSensor>()?.ClearTarget();
            GetComponent<MobaBotMover>()?.ResetMotion();
            GetComponent<BotLanePlan>()?.ResetRoute();
        }
        public void RefreshPersonality()
        {
            personality = GetComponent<BotPersonality>(); targetSensor = GetComponent<BotTargetSensor>();
            ApplyPersonality();
        }
        private void ApplyPersonality()
        {
            if (personality == null) return;
            aggression = personality.Aggression; caution = personality.Caution;
            decisionInterval = personality.ReactionDelay;
            lowHealthThreshold = Mathf.Lerp(.22f, .42f, caution);
        }
        private void Decide()
        {
            float hp = health.Normalized;
            if(lastHealth-hp>.08f)threatUntil=Time.time+Difficulty.MemorySeconds;
            lastHealth=hp;
            int pressure=targetSensor!=null?Mathf.Max(0,targetSensor.NearbyEnemies-targetSensor.NearbyAllies-1):0;
            float danger=pressure*.035f*Difficulty.TargetAccuracy;
            if(Time.time<threatUntil)danger+=.035f*Difficulty.TargetAccuracy;
            GroupWithAllies=Chance(TeamFocus*Difficulty.Grouping);
            float retreat=Mathf.Clamp(lowHealthThreshold+Difficulty.RetreatAdjustment+danger,.18f,.48f);
            if (hp <= retreat) { state = BotState.Retreat; return; }
            if (state == BotState.Retreat || state == BotState.Recover)
            {
                state = hp < .8f ? BotState.Recover : BotState.Lane;
                return;
            }
            // No second HP gate: otherwise wounded bots keep advancing but refuse to fight.
            Health enemy = targetSensor != null ? targetSensor.CurrentTarget : null;
            state = enemy != null && !enemy.IsDead ? BotState.Fight : BotState.Lane;
        }
    }
}
