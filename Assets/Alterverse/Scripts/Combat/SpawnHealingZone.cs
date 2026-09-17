using UnityEngine;

namespace Alterverse.Combat
{
    public sealed class SpawnHealingZone : MonoBehaviour
    {
        [SerializeField] private float radius = 4.5f;
        [SerializeField] private float healPerSecond = 28f;
        private Health target;

        public void Initialize(Health playerHealth) => target = playerHealth;

        private void Update()
        {
            if (target == null || target.IsDead) return;
            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radius * radius)
                target.Heal(healPerSecond * Time.deltaTime);
        }
    }
}
