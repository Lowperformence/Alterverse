using Alterverse.Characters;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class SoftTargeting : MonoBehaviour
    {
        [SerializeField] private float stickSeconds = 1.25f;

        private Health current;
        private Health self;
        private float stickyUntil;
        private TeamMember team;

        public Health Current => Valid(current) ? current : null;

        private void Awake()
        {
            team = GetComponent<TeamMember>();
            self = GetComponent<Health>();
        }

        public Health Acquire(float range, Vector3 aimDirection, bool preferHeroes = true)
        {
            var candidates = Health.Active;
            int count = candidates.Count;
            bool heroAvailable = false;

            if (preferHeroes)
            {
                for (int i = 0; i < count; i++)
                {
                    Health candidate = candidates[i];
                    if (!Valid(candidate) || candidate.GetComponent<HeroIdentity>() == null) continue;
                    if (HeroCombatRules.BasicDistance(transform.position, candidate) <= range)
                    {
                        heroAvailable = true;
                        break;
                    }
                }
            }

            if (Valid(current) &&
                Time.time < stickyUntil &&
                HeroCombatRules.BasicDistance(transform.position, current) <= range &&
                (!heroAvailable || current.GetComponent<HeroIdentity>() != null))
                return current;

            Health best = null;
            float bestScore = float.MinValue;
            Vector3 aim = aimDirection;
            aim.y = 0;
            if (aim.sqrMagnitude < .05f) aim = transform.forward;
            aim.Normalize();

            for (int i = 0; i < count; i++)
            {
                Health h = candidates[i];
                if (!Valid(h)) continue;

                bool hero = h.GetComponent<HeroIdentity>() != null;
                if (heroAvailable && !hero) continue;

                float dist = HeroCombatRules.BasicDistance(transform.position, h);
                if (dist > range) continue;

                Vector3 d = h.transform.position - transform.position;
                d.y = 0;
                float centerDistance = Mathf.Max(.01f, d.magnitude);
                float alignment = Vector3.Dot(aim, d / centerDistance);
                bool objective = h.GetComponent<DimensionPlanet>() != null;
                float score = (range - dist) * 1.2f + alignment * 3.5f + (objective ? 1.2f : 0f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = h;
                }
            }

            current = best;
            stickyUntil = Time.time + stickSeconds;
            return current;
        }

        public void Release()
        {
            current = null;
            stickyUntil = 0;
        }

        public void RegisterOffensiveAction()
        {
            if (Valid(current)) stickyUntil = Time.time + stickSeconds;
        }

        private bool Valid(Health h)
        {
            if (h == null || h.IsDead || !h.isActiveAndEnabled || h == self) return false;

            RealityCore core = h.GetComponent<RealityCore>();
            if (core != null) return core.IsVulnerable && core.Team != (team != null ? team.Team : 0);

            DimensionPlanet planet = h.GetComponent<DimensionPlanet>();
            if (planet != null) return planet.Available;

            TeamMember other = h.GetComponent<TeamMember>();
            if (other != null && team != null) return other.Team != team.Team;

            LaneMinion minion = h.GetComponent<LaneMinion>();
            if (minion != null) return minion.Team != (team != null ? team.Team : 0);

            LaneTower tower = h.GetComponent<LaneTower>();
            if (tower != null) return tower.Team != (team != null ? team.Team : 0);

            return false;
        }
    }
}
