using System.Collections;using UnityEngine;using Alterverse.Combat;
namespace Alterverse.AI
{
[RequireComponent(typeof(Health))]public sealed class MobaBotRespawn:MonoBehaviour
{
[SerializeField]private float firstRespawnDelay=4f,addedDelayPerDeath=1f,maximumRespawnDelay=10f;private Health health;private Vector3 spawnPoint;private bool respawning;public event System.Action Respawned;private int deathCount;public int DeathCount=>deathCount;public bool IsRespawning=>respawning;public float CurrentRespawnDelay=>1.2f*Mathf.Min(maximumRespawnDelay,firstRespawnDelay+Mathf.Max(0,deathCount-1)*addedDelayPerDeath);
private void Awake(){health=GetComponent<Health>();spawnPoint=transform.position;}private void OnEnable(){if(health!=null)health.Died+=OnDied;}private void OnDisable(){if(health!=null)health.Died-=OnDied;}private void OnDied(){if(respawning)return;deathCount++;StartCoroutine(RespawnRoutine(CurrentRespawnDelay));}
private IEnumerator RespawnRoutine(float delay){respawning=true;Renderer[] renderers=GetComponentsInChildren<Renderer>(true);Collider[] colliders=GetComponentsInChildren<Collider>(true);bool[] rendererStates=new bool[renderers.Length];bool[] colliderStates=new bool[colliders.Length];
for(int i=0;i<renderers.Length;i++){if(renderers[i]==null)continue;rendererStates[i]=renderers[i].enabled;renderers[i].enabled=false;}
for(int i=0;i<colliders.Length;i++){if(colliders[i]==null)continue;colliderStates[i]=colliders[i].enabled;colliders[i].enabled=false;}float remaining=delay;while(remaining>0){yield return null;if(Time.timeScale>0)remaining-=Time.deltaTime;}while(Time.timeScale<=0f)yield return null;transform.SetPositionAndRotation(spawnPoint,Quaternion.LookRotation(spawnPoint.x<0?Vector3.right:Vector3.left));health.RestoreFull();for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=rendererStates[i];
for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=colliderStates[i];respawning=false;MobaBotBrain brain=GetComponent<MobaBotBrain>();if(brain!=null)brain.ResumeRoamAfterRespawn();Respawned?.Invoke();}
}
}
