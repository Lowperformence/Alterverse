using System;
using UnityEngine;
using Alterverse.Match;

namespace Alterverse.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath;
        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsDead { get; private set; }
        public float Normalized => maxHealth <= 0f ? 0f : Current / maxHealth;
        public GameObject LastDamageSource { get; private set; }
        public event Action<float,float> Changed;
        public event Action Died;
        public event Action<Health,GameObject> DiedWithSource;
        private void Awake(){Current=maxHealth;}
        public void Configure(float newMaxHealth,bool shouldDestroyOnDeath=false){maxHealth=Mathf.Max(1f,newMaxHealth);destroyOnDeath=shouldDestroyOnDeath;IsDead=false;Current=maxHealth;LastDamageSource=null;Changed?.Invoke(Current,maxHealth);}
        public void TakeDamage(float amount)=>TakeDamage(amount,null);
        public void TakeDamage(float amount,GameObject source)
        {
            if(IsDead||amount<=0f)return;
            if(source!=null){LastDamageSource=source;MatchScoreboard scoreboard=Object.FindAnyObjectByType<MatchScoreboard>();if(scoreboard!=null)scoreboard.RecordDamage(this,source);}
            Current=Mathf.Max(0f,Current-amount);Changed?.Invoke(Current,maxHealth);
            Debug.Log($"{name} took {amount:0.#} damage. HP: {Current:0.#}/{maxHealth:0.#}");
            if(Current<=0f){IsDead=true;Died?.Invoke();DiedWithSource?.Invoke(this,LastDamageSource);if(destroyOnDeath)Destroy(gameObject);}
        }
        public void Heal(float amount){if(IsDead||amount<=0f)return;Current=Mathf.Min(maxHealth,Current+amount);Changed?.Invoke(Current,maxHealth);}
        public void RestoreFull(){IsDead=false;Current=maxHealth;LastDamageSource=null;Changed?.Invoke(Current,maxHealth);}
    }
}
