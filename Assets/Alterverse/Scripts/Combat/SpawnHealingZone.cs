using UnityEngine;

namespace Alterverse.Combat
{
    public sealed class SpawnHealingZone : MonoBehaviour
    {
        [SerializeField] private float radius = 4.5f;
        [SerializeField] private float healPerSecond = 28f;
        [SerializeField] private int team;
        private Health legacyTarget;

        public void Initialize(Health playerHealth) => legacyTarget = playerHealth;
        public void InitializeTeam(int assignedTeam) => team = Mathf.Clamp(assignedTeam, 0, 1);

        private void Update()
        {
            if (legacyTarget != null && !legacyTarget.IsDead)
            {
                TeamMember member = legacyTarget.GetComponent<TeamMember>();
                if (member == null || member.Team == team) HealIfInside(legacyTarget);
            }

            TeamMember[] members = Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i] == null || members[i].Team != team) continue;
                Health health = members[i].GetComponent<Health>();
                if (health == null || health == legacyTarget || health.IsDead) continue;
                HealIfInside(health);
            }
        }

        private void HealIfInside(Health health)
        {
            Vector3 delta = health.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radius * radius)
                health.Heal(healPerSecond * Time.deltaTime);
        }
    }
}
