using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;

namespace Alterverse.AI
{
    [RequireComponent(typeof(TeamMember))]
    public sealed class BotTargetSensor : MonoBehaviour
    {
        [SerializeField] private float fallbackScanRadius = 10f, scanInterval = .22f;
        private TeamMember teamMember;
        private Health selfHealth, currentTarget;
        private HeroCombatProfile profile;
        private float nextScanTime;
        public Health CurrentTarget => IsEnemy(currentTarget) ? currentTarget : null;

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
            selfHealth = GetComponent<Health>();
            profile = GetComponent<HeroCombatProfile>();
        }
        public void ClearTarget() { currentTarget = null; nextScanTime = 0; }
        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (selfHealth != null && selfHealth.IsDead) { ClearTarget(); return; }
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + Mathf.Max(.12f, scanInterval);
            Scan();
        }
        private bool IsEnemy(Health candidate)
        {
            if (candidate == null || candidate == selfHealth || candidate.IsDead ||
                !candidate.isActiveAndEnabled || teamMember == null) return false;
            DimensionPlanet planet = candidate.GetComponent<DimensionPlanet>();
            if (planet != null) return planet.Available;
            RealityCore core = candidate.GetComponent<RealityCore>();
            if (core != null) return core.Team != teamMember.Team && core.IsVulnerable;
            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            if (minion != null) return minion.Team != teamMember.Team;
            LaneTower tower = candidate.GetComponent<LaneTower>();
            if (tower != null) return tower.Team != teamMember.Team;
            TeamMember other = candidate.GetComponent<TeamMember>();
            return other != null && teamMember.IsEnemy(other);
        }
        private float Score(Health candidate)
        {
            Vector3 delta = candidate.transform.position - transform.position;
            delta.y = 0;
            float priority = candidate.GetComponent<HeroIdentity>() != null ? .72f : 1f;
            if (candidate.GetComponent<LaneMinion>() != null) priority = .9f;
            if (candidate.GetComponent<LaneTower>() != null) priority = 1.15f;
            if (candidate.GetComponent<RealityCore>() != null) priority = 1.2f;
            if (candidate.GetComponent<DimensionPlanet>() != null) priority = 1.35f;
            return delta.sqrMagnitude * Mathf.Lerp(.7f, 1f, candidate.Normalized) * priority;
        }
        private void Scan()
        {
            float radius = profile != null ? Mathf.Max(fallbackScanRadius, profile.AttackRange + 4f) : fallbackScanRadius;
            Health best = null;
            float bestScore = float.MaxValue;
            foreach (Health candidate in FindObjectsByType<Health>())
            {
                if (!IsEnemy(candidate)) continue;
                Vector3 delta = candidate.transform.position - transform.position;
                delta.y = 0;
                if (delta.sqrMagnitude > radius * radius) continue;
                float score = Score(candidate);
                // A challenger must be substantially better; small movements cannot flip targets.
                if (candidate == currentTarget) score *= .65f;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            currentTarget = best;
        }
    }
}
