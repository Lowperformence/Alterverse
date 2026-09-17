using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Match
{
    public sealed class DimensionsFrontCombatBridge : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float refreshInterval = .5f;
        private readonly HashSet<Health> tracked = new();
        private DimensionsFront front;
        private float nextRefresh;

        private void Start()
        {
            front = Object.FindAnyObjectByType<DimensionsFront>();
            RefreshHeroes();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            RefreshHeroes();
        }

        private void RefreshHeroes()
        {
            TeamMember[] members = Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
            foreach (TeamMember member in members)
            {
                if (member == null) continue;
                Health health = member.GetComponent<Health>();
                if (health == null || !tracked.Add(health)) continue;
                health.DiedWithSource += OnHeroDied;
            }
        }

        private void OnHeroDied(Health victim, GameObject source)
        {
            if (front == null || victim == null || source == null) return;
            TeamMember victimTeam = victim.GetComponent<TeamMember>();
            TeamMember killerTeam = source.GetComponentInParent<TeamMember>();
            if (victimTeam == null || killerTeam == null || victimTeam.Team == killerTeam.Team) return;
            front.RegisterHeroKill(killerTeam.Team);
        }

        private void OnDestroy()
        {
            foreach (Health health in tracked)
                if (health != null) health.DiedWithSource -= OnHeroDied;
            tracked.Clear();
        }
    }
}
