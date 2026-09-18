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

        public Vector3 Home => new(team == 0 ? -34f : 34f, transform.position.y, team == 0 ? -4f : 4f);
        public Vector3 Staging => new(team == 0 ? 1.5f : -1.5f, transform.position.y, laneZ);
        public Vector3 Pressure => new(team == 0 ? 19f : -19f, transform.position.y, laneZ);
        public Vector3 EnemySide => new(team == 0 ? 29f : -29f, transform.position.y, laneZ);

        public void Initialize(int assignedTeam, float assignedLaneZ)
        {
            team = Mathf.Clamp(assignedTeam, 0, 1);
            laneZ = assignedLaneZ >= 0 ? 16f : -16f;
            ResetRoute();
        }

        public void ResetRoute() { laneObjective = null; nextObjectiveScan = 0; }

        public Vector3 GetDestination(MobaBotBrain.BotState state)
        {
            if (state == MobaBotBrain.BotState.Retreat || state == MobaBotBrain.BotState.Recover)
                return Home;

            // Leave the healing area through the assigned lane before advancing.
            float direction = team == 0 ? 1f : -1f;
            if (transform.position.x * direction < -23f && Mathf.Abs(transform.position.z - laneZ) > 1f)
            {
                // Route outward before turning into lane: this keeps spawn traffic clear of the core at x +/-36.
                float laneEntryX = team == 0 ? -29.5f : 29.5f;
                return new Vector3(laneEntryX, transform.position.y, laneZ);
            }

            if (laneObjective == null || laneObjective.IsDead || Time.time >= nextObjectiveScan)
            {
                nextObjectiveScan = Time.time + 1f;
                laneObjective = FindLaneObjective();
            }
            return laneObjective != null ? laneObjective.transform.position : EnemySide;
        }

        private Health FindLaneObjective()
        {
            Health best = null;
            float bestScore = float.MaxValue;
            foreach (LaneTower tower in FindObjectsByType<LaneTower>())
            {
                Health hp = tower.GetComponent<Health>();
                if (tower.Team == team || hp == null || hp.IsDead) continue;
                // Clear our lane first, then help the other lane unlock the core.
                float score = Mathf.Abs(tower.transform.position.z - laneZ) * 100f
                    + (tower.transform.position - transform.position).sqrMagnitude;
                if (score < bestScore) { bestScore = score; best = hp; }
            }
            if (best != null) return best;
            foreach (RealityCore core in FindObjectsByType<RealityCore>())
            {
                Health hp = core.GetComponent<Health>();
                if (core.Team != team && core.IsVulnerable && hp != null && !hp.IsDead) return hp;
            }
            return null;
        }
    }
}
