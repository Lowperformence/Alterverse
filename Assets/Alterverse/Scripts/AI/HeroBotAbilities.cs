using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;

namespace Alterverse.AI
{
[RequireComponent(typeof(Health),typeof(BotTargetSensor),typeof(HeroIdentity),typeof(HeroAbilityKit))]
public sealed class HeroBotAbilities:MonoBehaviour
{
private Health self;private BotTargetSensor sensor;private HeroIdentity identity;private HeroAbilityKit kit;private float qReady,wReady,eReady,ultReady;
private void Awake(){self=GetComponent<Health>();sensor=GetComponent<BotTargetSensor>();identity=GetComponent<HeroIdentity>();kit=GetComponent<HeroAbilityKit>();}
private void Update(){if(self==null||self.IsDead||sensor==null||identity==null||kit==null)return;Health target=sensor.CurrentTarget;if(target==null||target.IsDead)return;float d=FlatDistance(target.transform.position,transform.position);if(Time.time>=ultReady&&d<=kit.Ultimate.Range&&ShouldUltimate(target)){ultReady=Time.time+kit.Ultimate.Cooldown;Deal(target,kit.Ultimate.Power);return;}if(Time.time>=qReady&&d<=kit.Q.Range){qReady=Time.time+kit.Q.Cooldown;Deal(target,kit.Q.Power);return;}if(Time.time>=wReady&&d<=kit.W.Range&&kit.W.Power>0f){wReady=Time.time+kit.W.Cooldown;Deal(target,kit.W.Power);return;}if(Time.time>=eReady&&d<=kit.E.Range&&kit.E.Power>0f){eReady=Time.time+kit.E.Cooldown;Deal(target,kit.E.Power);}}
private bool ShouldUltimate(Health target){return target.Normalized<=.55f||self.Normalized<=.38f;}
private void Deal(Health target,float basePower){if(basePower<=0f)return;HeroCombatProfile profile=GetComponent<HeroCombatProfile>();float scale=profile!=null?profile.AbilityPower:1f;target.TakeDamage(basePower*scale,gameObject);if(identity.Hero==HeroId.Veyr)self.Heal(basePower*.12f);if(identity.Hero==HeroId.Mira)self.Heal(basePower*.35f);}
private static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0f;return Vector3.Distance(a,b);}
}
}
