using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Match
{
[RequireComponent(typeof(Health))]public sealed class DimensionPlanet:MonoBehaviour
{
[SerializeField]private PlanetBuffType buffType;[SerializeField]private float buffDuration=60f,respawnSeconds=75f,frontInfluence=1.5f;private Health health;private DimensionsFront front;private Renderer[] visuals;private Collider[] colliders;private bool available=true;private float respawnAt;public PlanetBuffType BuffType=>buffType;public bool Available=>available;public float RespawnRemaining=>available?0:Mathf.Max(0,respawnAt-Time.time);public void Initialize(PlanetBuffType type){buffType=type;}
private void Awake(){health=GetComponent<Health>();front=FindAnyObjectByType<DimensionsFront>();visuals=GetComponentsInChildren<Renderer>();colliders=GetComponentsInChildren<Collider>();health.DiedWithSource+=OnDestroyed;}private void Update(){if(!available&&Time.time>=respawnAt)Respawn();if(available)transform.Rotate(0,22*Time.deltaTime,0,Space.World);}
private void OnDestroyed(Health victim,GameObject source){if(!available||source==null)return;TeamMember killer=source.GetComponentInParent<TeamMember>();if(killer==null){health.RestoreFull();return;}available=false;ApplyTeamBuff(killer.Team);if(front==null)front=FindAnyObjectByType<DimensionsFront>();front?.Push(killer.Team,frontInfluence);respawnAt=Time.time+respawnSeconds;SetVisible(false);}
private void ApplyTeamBuff(int team){foreach(TeamMember member in FindObjectsByType<TeamMember>(FindObjectsSortMode.None)){if(member.Team!=team)continue;Health h=member.GetComponent<Health>();if(h==null||h.IsDead)continue;PlanetBuffReceiver receiver=member.GetComponent<PlanetBuffReceiver>()??member.gameObject.AddComponent<PlanetBuffReceiver>();receiver.Apply(buffType,buffDuration);}}
private void Respawn(){available=true;health.RestoreFull();SetVisible(true);}private void SetVisible(bool value){foreach(Renderer r in visuals)if(r!=null)r.enabled=value;foreach(Collider c in colliders)if(c!=null)c.enabled=value;}private void OnDestroy(){if(health!=null)health.DiedWithSource-=OnDestroyed;}
}
}
