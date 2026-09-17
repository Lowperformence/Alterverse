using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroRespawn : MonoBehaviour
    {
        [SerializeField] private float firstRespawnDelay = 4f;
        [SerializeField] private float addedDelayPerDeath = 1f;
        [SerializeField] private float maximumRespawnDelay = 10f;

        private Health health;
        private CharacterController controller;
        private Vector3 spawnPoint;
        private bool respawning;
        private bool respawnReady;
        private int deathCount;
        private float readyAt;

        public int DeathCount => deathCount;
        public bool IsRespawning => respawning;
        public bool RespawnReady => respawnReady;
        public float RemainingSeconds => respawning && !respawnReady ? Mathf.Max(0f,readyAt-Time.time) : 0f;
        public float CurrentRespawnDelay => Mathf.Min(maximumRespawnDelay,firstRespawnDelay+Mathf.Max(0,deathCount-1)*addedDelayPerDeath);

        private void Awake(){health=GetComponent<Health>();controller=GetComponent<CharacterController>();spawnPoint=transform.position;health.Died+=OnDied;}
        private void OnDestroy(){if(health!=null)health.Died-=OnDied;}
        private void OnDied(){if(respawning)return;deathCount++;StartCoroutine(RespawnCountdown(CurrentRespawnDelay));}

        private IEnumerator RespawnCountdown(float delay)
        {
            respawning=true;respawnReady=false;readyAt=Time.time+delay;
            if(controller!=null)controller.enabled=false;
            yield return new WaitForSeconds(delay);
            respawnReady=true;
        }

        public void ConfirmRespawn()
        {
            if(!respawning||!respawnReady)return;
            transform.position=spawnPoint;
            if(controller!=null)controller.enabled=true;
            health.RestoreFull();
            respawnReady=false;respawning=false;
        }
    }
}
