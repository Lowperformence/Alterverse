using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Shared team identity for heroes and future team-aware systems.
    /// Team 0 = Zero side, Team 1 = Lumen side.
    /// </summary>
    public sealed class TeamMember : MonoBehaviour
    {
        [SerializeField] private int team;

        public int Team => team;

        public void Initialize(int assignedTeam)
        {
            team = Mathf.Clamp(assignedTeam, 0, 1);
        }

        public bool IsEnemy(TeamMember other)
        {
            return other != null && other.team != team;
        }
    }
}
