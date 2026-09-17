using UnityEngine;

namespace Alterverse.AI
{
    /// <summary>
    /// Provides simple, deterministic lane destinations for bot heroes.
    /// No NavMesh dependency: suitable for the current lightweight prototype.
    /// </summary>
    public sealed class BotLanePlan : MonoBehaviour
    {
        [SerializeField] private int team;
        [SerializeField] private float laneZ;
        [SerializeField] private float homeX = -22f;
        [SerializeField] private float stagingX = -14f;
        [SerializeField] private float pressureX = 10f;
        [SerializeField] private float enemySideX = 16f;

        public Vector3 Home => new(homeX, transform.position.y, laneZ);
        public Vector3 Staging => new(stagingX, transform.position.y, laneZ);
        public Vector3 Pressure => new(pressureX, transform.position.y, laneZ);
        public Vector3 EnemySide => new(enemySideX, transform.position.y, laneZ);

        public void Initialize(int assignedTeam, float assignedLaneZ)
        {
            team = Mathf.Clamp(assignedTeam, 0, 1);
            laneZ = Mathf.Clamp(assignedLaneZ, -12f, 12f);

            float direction = team == 0 ? 1f : -1f;
            homeX = -22f * direction;
            stagingX = -14f * direction;
            pressureX = 10f * direction;
            enemySideX = 16f * direction;
        }

        public Vector3 GetDestination(MobaBotBrain.BotState state)
        {
            switch (state)
            {
                case MobaBotBrain.BotState.Retreat:
                case MobaBotBrain.BotState.Recover:
                    return Home;
                case MobaBotBrain.BotState.Fight:
                    return Pressure;
                default:
                    return Staging;
            }
        }
    }
}
