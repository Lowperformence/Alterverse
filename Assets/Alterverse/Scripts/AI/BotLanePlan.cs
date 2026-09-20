using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.AI
{
    public sealed class BotLanePlan : MonoBehaviour
    {
        [SerializeField] private int team;
        [SerializeField] private float laneZ;
        private float nextObjectiveScan;
        private Health laneObjective;
        private bool exitAligned;
        public bool IsLeavingBase => transform.position.x * (team == 0 ? 1f : -1f) < -22.5f;

        public Vector3 Home => new(team == 0 ? -27.5f : 27.5f, transform.position.y, 0f);
        public Vector3 Staging => new(team == 0 ? 1.5f : -1.5f, transform.position.y, laneZ);
        public Vector3 Pressure => new(team == 0 ? 19f : -19f, transform.position.y, laneZ);
        public Vector3 EnemySide => new(team == 0 ? 29f : -29f, transform.position.y, laneZ);

        public void Initialize(int assignedTeam, float assignedLaneZ)
        {
            team = Mathf.Clamp(assignedTeam, 0, 1);
            laneZ = assignedLaneZ >= 0 ? 16f : -16f;
            ResetRoute();
        }

        public void ResetRoute() { laneObjective = null; nextObjectiveScan = 0; exitAligned = false; }

        public Vector3 GetDestination(MobaBotBrain.BotState state)
        {
            if (state == MobaBotBrain.BotState.Retreat || state == MobaBotBrain.BotState.Recover)
                return Home;

            // Leave the actual healing station through the assigned lane before advancing.
            float direction = team == 0 ? 1f : -1f;
            if (IsLeavingBase)
            {
                // Latch alignment: tactical calls must not flip the destination at the lane edge.
                if (Mathf.Abs(transform.position.z - laneZ) < .6f) exitAligned = true;
                if (Mathf.Abs(transform.position.z - laneZ) > 3f) exitAligned = false;
                return new Vector3((exitAligned ? -22f : -28f) * direction, transform.position.y, laneZ);
            }
            exitAligned = false;

            // No objective is a valid cached result too; do not allocate scene scans every frame.
            if (Time.time >= nextObjectiveScan || laneObjective != null && laneObjective.IsDead)
            {
                nextObjectiveScan = Time.time + 1f;
                laneObjective = FindLaneObjective();
            }
            return laneObjective != null ? laneObjective.transform.position : EnemySide;
        }

        private Health FindLaneObjective()
        {
            // Bot heroes never route toward towers. The lane plan keeps advancing naturally and
            // only converts to a combat objective when the enemy Reality Core is actually vulnerable.
            foreach (Health hp in Health.Active)
            {
                if (hp == null || hp.IsDead || !hp.isActiveAndEnabled) continue;
                RealityCore core = hp.GetComponent<RealityCore>();
                if (core != null && core.Team != team && core.IsVulnerable) return hp;
            }
            return null;
        }
    }
}

