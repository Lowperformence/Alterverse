using System.Collections;using UnityEngine;using Alterverse.Combat;
namespace Alterverse.AI
{
[RequireComponent(typeof(Health))]public sealed class MobaBotRespawn:MonoBehaviour
{
[SerializeField]private float firstRespawnDelay=4f,addedDelayPerDeath=1f,maximumRespawnDelay=10f;private Health health;private Vector3 spawnPoint;private bool respawning;private int deathCount;public int DeathCount=>deathCount;public float CurrentRespawnDelay=>Mathf.Min(maximumRespawnDelay,firstRespawnDelay+Mathf.Max(0,deathCount-1)*addedDelayPerDeath);
private void Awake(){health=GetComponent<Health>();spawnPoint=transform.position;}private void OnEnable(){if(health!=null)health.Died+=OnDied;}private void OnDisable(){if(health!=null)health.Died-=OnDied;}private void OnDied(){if(respawning)return;deathCount++;StartCoroutine(RespawnRoutine(CurrentRespawnDelay));}
private IEnumerator RespawnRoutine(float delay){respawning=true;Renderer[] renderers=GetComponentsInChildren<Renderer>(true);Collider[] colliders=GetComponentsInChildren<Collider>(true);foreach(Renderer r in renderers)if(r!=null)r.enabled=false;foreach(Collider c in colliders)if(c!=null)c.enabled=false;float remaining=delay;while(remaining>0){yield return null;if(Time.timeScale>0)remaining-=Time.deltaTime;}transform.position=spawnPoint;health.RestoreFull();foreach(Renderer r in renderers)if(r!=null)r.enabled=true;foreach(Collider c in colliders)if(c!=null)c.enabled=true;respawning=false;}
}
}
