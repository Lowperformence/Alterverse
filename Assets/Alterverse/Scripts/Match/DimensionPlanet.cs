using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Match
{
[RequireComponent(typeof(Health))]public sealed class DimensionPlanet:MonoBehaviour
{
[SerializeField]private PlanetBuffType buffType;[SerializeField]private float buffDuration=60f,frontInfluence=1.5f;private Health health;private DimensionsFront front;private Renderer[] visuals;private Collider[] colliders;private bool available=true;public PlanetBuffType BuffType=>buffType;public bool Available=>available;public float RespawnRemaining=>0;public void Initialize(PlanetBuffType type){buffType=type;RefreshVisualCache();}
private void Awake(){health=GetComponent<Health>();front=FindAnyObjectByType<DimensionsFront>();RefreshVisualCache();health.DiedWithSource+=OnDestroyed;}private void Update(){if(available&&Time.timeScale>0f)transform.Rotate(0,22*Time.deltaTime,0,Space.World);}public void RefreshVisualCache(){visuals=GetComponentsInChildren<Renderer>(true);colliders=GetComponentsInChildren<Collider>(true);}
private void OnDestroyed(Health victim,GameObject source){if(!available||source==null)return;TeamMember killer=source.GetComponentInParent<TeamMember>();if(killer==null||killer.Team<0||killer.Team>1){health.RestoreFull();return;}available=false;ApplyTeamBuff(killer.Team);if(front==null)front=FindAnyObjectByType<DimensionsFront>();front?.Push(killer.Team,frontInfluence);SetVisible(false);}
private void ApplyTeamBuff(int team){foreach(TeamMember member in FindObjectsByType<TeamMember>()){if(member.Team!=team)continue;Health h=member.GetComponent<Health>();if(h==null||h.IsDead)continue;PlanetBuffReceiver receiver=member.GetComponent<PlanetBuffReceiver>()??member.gameObject.AddComponent<PlanetBuffReceiver>();receiver.Apply(buffType,buffDuration);}}
private void SetVisible(bool value){if(visuals==null||colliders==null)RefreshVisualCache();foreach(Renderer r in visuals)if(r!=null)r.enabled=value;foreach(Collider c in colliders)if(c!=null)c.enabled=value;}private void OnDestroy(){if(health!=null)health.DiedWithSource-=OnDestroyed;}
}
}