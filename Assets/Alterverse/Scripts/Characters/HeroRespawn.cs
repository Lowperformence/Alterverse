using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health))][RequireComponent(typeof(CharacterController))]
    public sealed class HeroRespawn:MonoBehaviour
    {
        [SerializeField]private float firstRespawnDelay=4f;[SerializeField]private float addedDelayPerDeath=1f;[SerializeField]private float maximumRespawnDelay=10f;
        private Health health;private CharacterController controller;private Vector3 spawnPoint;private bool respawning;private bool respawnReady;private int deathCount;private float remainingSeconds;
        public int DeathCount=>deathCount;public bool IsRespawning=>respawning;public bool RespawnReady=>respawnReady;public float RemainingSeconds=>respawning&&!respawnReady?Mathf.Max(0f,remainingSeconds):0f;public float CurrentRespawnDelay=>Mathf.Min(maximumRespawnDelay,firstRespawnDelay+Mathf.Max(0,deathCount-1)*addedDelayPerDeath);
        private void Awake(){health=GetComponent<Health>();controller=GetComponent<CharacterController>();spawnPoint=transform.position;health.Died+=OnDied;}
        private void OnDestroy(){if(health!=null)health.Died-=OnDied;}
        private void OnDied(){if(respawning)return;deathCount++;StartCoroutine(RespawnCountdown(CurrentRespawnDelay));}
        private IEnumerator RespawnCountdown(float delay){respawning=true;respawnReady=false;remainingSeconds=delay;if(controller!=null)controller.enabled=false;while(remainingSeconds>0f){yield return null;if(Time.timeScale>0f)remainingSeconds-=Time.deltaTime;}remainingSeconds=0f;respawnReady=true;}
        public void ConfirmRespawn(){if(!respawning||!respawnReady)return;transform.position=spawnPoint;if(controller!=null)controller.enabled=true;health.RestoreFull();remainingSeconds=0f;respawnReady=false;respawning=false;}
    }
}
