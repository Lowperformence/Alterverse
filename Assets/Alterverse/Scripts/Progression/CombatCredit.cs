using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Progression
{
    [RequireComponent(typeof(Health))]
    public sealed class CombatCredit : MonoBehaviour
    {
        [SerializeField, Min(0)] private int xpReward = 25;
        [SerializeField, Min(0.1f)] private float creditWindow = 8f;

        private Health health;
        private readonly Dictionary<HeroLevel, float> contributors = new();

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Died += AwardXp;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= AwardXp;
        }

        public void RecordDamage(GameObject attacker)
        {
            if (attacker == null || health == null || health.IsDead) return;
            HeroLevel hero = attacker.GetComponentInParent<HeroLevel>();
            if (hero == null) return;
            contributors[hero] = Time.time;
        }

        public void ConfigureReward(int reward)
        {
            xpReward = Mathf.Max(0, reward);
        }

        private void AwardXp()
        {
            if (xpReward <= 0 || contributors.Count == 0)
            {
                contributors.Clear();
                return;
            }

            List<HeroLevel> eligible = new();
            foreach (KeyValuePair<HeroLevel, float> entry in contributors)
            {
                if (entry.Key != null && Time.time - entry.Value <= creditWindow)
                    eligible.Add(entry.Key);
            }

            if (eligible.Count > 0)
            {
                int share = Mathf.Max(1, xpReward / eligible.Count);
                foreach (HeroLevel hero in eligible)
                    hero.AddXp(share);
            }

            contributors.Clear();
        }
    }
}
