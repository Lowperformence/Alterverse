using UnityEngine;

namespace Alterverse.Combat
{
    public sealed class SpawnHealingZone : MonoBehaviour
    {
        [SerializeField] private float radius = 4.5f;
        [SerializeField] private float healPerSecond = 28f;
        [SerializeField] private int team;
        [SerializeField] private float refreshInterval = 0.5f;
        private Health legacyTarget;
        private TeamMember[] cachedMembers = System.Array.Empty<TeamMember>();
        private float nextRefresh;

        public void Initialize(Health playerHealth) => legacyTarget = playerHealth;
        public void InitializeTeam(int assignedTeam) => team = Mathf.Clamp(assignedTeam, 0, 1);

        private void Update()
        {
            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + Mathf.Max(0.1f, refreshInterval);
                cachedMembers = Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
            }

            if (legacyTarget != null && !legacyTarget.IsDead)
            {
                TeamMember member = legacyTarget.GetComponent<TeamMember>();
                if (member == null || member.Team == team) HealIfInside(legacyTarget);
            }

            for (int i = 0; i < cachedMembers.Length; i++)
            {
                TeamMember member = cachedMembers[i];
                if (member == null || member.Team != team) continue;
                Health health = member.GetComponent<Health>();
                if (health == null || health == legacyTarget || health.IsDead) continue;
                HealIfInside(health);
            }
        }

        private void HealIfInside(Health health)
        {
            Vector3 delta = health.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radius * radius) health.Heal(healPerSecond * Time.deltaTime);
        }
    }
}
