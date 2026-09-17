using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
namespace Alterverse.AI
{
[RequireComponent(typeof(Health),typeof(BotTargetSensor),typeof(HeroIdentity),typeof(HeroAbilityKit))]
public sealed class HeroBotAbilities:MonoBehaviour
{
private Health self;private BotTargetSensor sensor;private HeroIdentity identity;private HeroAbilityKit kit;private HeroCombatProfile profile;private float qReady,wReady,eReady,ultReady;
private void Awake(){self=GetComponent<Health>();sensor=GetComponent<BotTargetSensor>();identity=GetComponent<HeroIdentity>();kit=GetComponent<HeroAbilityKit>();profile=GetComponent<HeroCombatProfile>();}
private void Update(){if(self==null||self.IsDead||sensor==null||identity==null||kit==null)return;Health target=sensor.CurrentTarget;if(target==null||target.IsDead)return;float d=FlatDistance(target.transform.position,transform.position);if(Time.time>=ultReady&&d<=kit.Ultimate.Range&&ShouldUltimate(target)){ultReady=Time.time+kit.Ultimate.Cooldown;Cast(target,kit.Ultimate.Power,true);return;}if(Time.time>=qReady&&d<=kit.Q.Range){qReady=Time.time+kit.Q.Cooldown;Cast(target,kit.Q.Power,false);return;}if(Time.time>=wReady&&d<=kit.W.Range){wReady=Time.time+kit.W.Cooldown;CastUtilityOrDamage(target,kit.W.Power);return;}if(Time.time>=eReady&&d<=kit.E.Range){eReady=Time.time+kit.E.Cooldown;CastUtilityOrDamage(target,kit.E.Power);}}
private bool ShouldUltimate(Health target)=>target.Normalized<=.48f||self.Normalized<=.32f;
private void CastUtilityOrDamage(Health target,float power){switch(identity.Hero){case HeroId.Kaelon:self.Heal(self.Max*.10f);break;case HeroId.Mira:HealWeakestAlly(self.Max*.14f);break;case HeroId.Nyra:RepositionAway(target,2.5f);break;case HeroId.Elyra:RepositionAway(target,1.4f);break;case HeroId.Rook:self.Heal(self.Max*.08f);break;default:Cast(target,power,false);break;}if(power>0f)Cast(target,power,false);}
private void Cast(Health target,float basePower,bool ultimate){if(basePower<=0f)return;float scale=profile!=null?profile.AbilityPower:1f;float damage=basePower*scale;if(identity.Hero==HeroId.Veyr&&ultimate)damage*=1f+(1f-target.Normalized)*.55f;target.TakeDamage(damage,gameObject);if(identity.Hero==HeroId.Veyr)self.Heal(damage*.12f);if(identity.Hero==HeroId.Rook&&self.Normalized<.45f)self.Heal(damage*.10f);if(identity.Hero==HeroId.Mira)HealWeakestAlly(damage*.35f);}
private void HealWeakestAlly(float amount){TeamMember mine=GetComponent<TeamMember>();if(mine==null)return;Health[] all=FindObjectsByType<Health>(FindObjectsSortMode.None);Health best=null;float lowest=2f;foreach(Health h in all){if(h==null||h.IsDead)continue;TeamMember tm=h.GetComponent<TeamMember>();if(tm!=null&&tm.Team==mine.Team&&h.Normalized<lowest){lowest=h.Normalized;best=h;}}best?.Heal(amount);}
private void RepositionAway(Health target,float distance){Vector3 away=transform.position-target.transform.position;away.y=0;if(away.sqrMagnitude<.01f)return;Vector3 p=transform.position+away.normalized*distance;p.x=Mathf.Clamp(p.x,-41f,41f);p.z=Mathf.Clamp(p.z,-35f,35f);transform.position=p;}
private static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0f;return Vector3.Distance(a,b);}
}
}
