using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    /// <summary>
    /// Finds nearby living enemy heroes using explicit team identity.
    /// Designed for a small offline roster; no global search is performed every frame.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    public sealed class BotTargetSensor : MonoBehaviour
    {
        [SerializeField] private float scanRadius = 7f;
        [SerializeField] private float scanInterval = 0.35f;

        private TeamMember teamMember;
        private Health currentTarget;
        private float nextScanTime;

        public Health CurrentTarget => currentTarget;

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
        }

        private void Update()
        {
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + Mathf.Max(0.15f, scanInterval);
            Scan();
        }

        private void Scan()
        {
            currentTarget = null;
            float bestScore = float.MaxValue;
            Collider[] hits = Physics.OverlapSphere(transform.position, scanRadius);

            for (int i = 0; i < hits.Length; i++)
            {
                TeamMember candidateTeam = hits[i].GetComponentInParent<TeamMember>();
                if (candidateTeam == null || candidateTeam == teamMember || !teamMember.IsEnemy(candidateTeam)) continue;

                Health candidateHealth = candidateTeam.GetComponent<Health>();
                if (candidateHealth == null || candidateHealth.IsDead) continue;

                Vector3 delta = candidateHealth.transform.position - transform.position;
                delta.y = 0f;

                // Prefer closer enemies, with a small bonus for wounded targets.
                float score = delta.sqrMagnitude * Mathf.Lerp(0.75f, 1f, candidateHealth.Normalized);
                if (score >= bestScore) continue;

                bestScore = score;
                currentTarget = candidateHealth;
            }
        }
    }
}
