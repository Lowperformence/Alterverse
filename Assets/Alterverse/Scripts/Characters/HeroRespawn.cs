using System.Collections;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Characters
{
[RequireComponent(typeof(Health)),RequireComponent(typeof(CharacterController))]
public sealed class HeroRespawn:MonoBehaviour
{
[SerializeField]private float firstRespawnDelay=4f,addedDelayPerDeath=1f,maximumRespawnDelay=10f;private Health health;private CharacterController controller;private Vector3 spawnPoint;private bool respawning,respawnReady;private int deathCount;private float remainingSeconds;private Renderer[] renderers;
public int DeathCount=>deathCount;public bool IsRespawning=>respawning;public bool RespawnReady=>respawnReady;public float RemainingSeconds=>respawning&&!respawnReady?Mathf.Max(0,remainingSeconds):0;public float CurrentRespawnDelay=>Mathf.Min(maximumRespawnDelay,firstRespawnDelay+Mathf.Max(0,deathCount-1)*addedDelayPerDeath);
private void Awake(){health=GetComponent<Health>();controller=GetComponent<CharacterController>();renderers=GetComponentsInChildren<Renderer>(true);spawnPoint=transform.position;health.Died+=OnDied;}private void OnDestroy(){if(health!=null)health.Died-=OnDied;}
private void OnDied(){if(respawning)return;deathCount++;SetAlivePresentation(false);StartCoroutine(RespawnCountdown(CurrentRespawnDelay));}
private IEnumerator RespawnCountdown(float delay){respawning=true;respawnReady=false;remainingSeconds=delay;if(controller!=null)controller.enabled=false;while(remainingSeconds>0){yield return null;if(Time.timeScale>0)remainingSeconds-=Time.deltaTime;}remainingSeconds=0;respawnReady=true;}
public void ConfirmRespawn(){if(!respawning||!respawnReady)return;if(controller!=null)controller.enabled=false;transform.position=spawnPoint;health.RestoreFull();if(controller!=null)controller.enabled=true;SetAlivePresentation(true);remainingSeconds=0;respawnReady=false;respawning=false;}
private void SetAlivePresentation(bool alive){if(renderers==null)return;foreach(Renderer r in renderers)if(r!=null)r.enabled=alive;}
}
}
