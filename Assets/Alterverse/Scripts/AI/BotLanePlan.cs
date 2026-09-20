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
        private float towerApproachSpread;
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
            uint hash=0;foreach(char ch in gameObject.name)hash=unchecked(hash*31u+ch);
            towerApproachSpread=((int)(hash%5u)-2)*.28f;
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
            return laneObjective != null ? ObjectiveApproach(laneObjective) : EnemySide;
        }

        private Vector3 ObjectiveApproach(Health objective)
        {
            Vector3 target=objective.transform.position;
            LaneTower tower=objective.GetComponent<LaneTower>();
            if(tower==null)return target;

            // Never route a lane bot into the centre of a solid tower. Approach from the
            // attacking side and spread the roster slightly across Z so melee heroes do not
            // stack on one collider point.
            Collider body=tower.GetComponent<Collider>();
            float bodyRadius=body!=null&&body.enabled?Mathf.Max(body.bounds.extents.x,body.bounds.extents.z):.70f;
            float direction=team==0?1f:-1f;
            float standOff=bodyRadius+.72f;
            target.x-=direction*standOff;
            target.z+=towerApproachSpread;
            target.y=transform.position.y;
            return target;
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

