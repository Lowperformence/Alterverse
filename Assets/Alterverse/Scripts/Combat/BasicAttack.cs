using Alterverse.AI;using Alterverse.Characters;using Alterverse.Match;using Alterverse.Onboarding;using Alterverse.Visuals;using UnityEngine;using UnityEngine.InputSystem;
namespace Alterverse.Combat
{
[RequireComponent(typeof(Health))]public sealed class BasicAttack:MonoBehaviour
{
[SerializeField,Min(.1f)]private float fallbackRange=2.2f;[SerializeField,Min(0f)]private float fallbackDamage=20f;[SerializeField,Min(.05f)]private float fallbackCooldown=.78f;private HeroLocomotion locomotion;private Health selfHealth;private HeroCombatProfile profile;private HeroIdentity identity;private SoftTargeting targeting;private float nextAttackTime;private HeroCombatProfile Profile{get{if(profile==null)profile=GetComponent<HeroCombatProfile>();return profile;}}private float Range=>Profile!=null?Profile.AttackRange:fallbackRange;private float Damage=>Profile!=null?Profile.Attack:fallbackDamage;private float Cooldown=>HeroCombatRules.Interval(gameObject,Profile!=null?Profile.AttackInterval:fallbackCooldown);
// Read-only HUD values; attack timing and execution are unchanged.
public float RemainingCooldown=>Mathf.Max(0,nextAttackTime-Time.time);
public float AttackCooldown=>Cooldown;
private void Awake(){locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();selfHealth=GetComponent<Health>();profile=GetComponent<HeroCombatProfile>();identity=GetComponent<HeroIdentity>();targeting=GetComponent<SoftTargeting>()??gameObject.AddComponent<SoftTargeting>();}private void Update(){if(Time.timeScale<=0f)return;if(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame)TryAttack();}
public bool TryAttack(){if(Time.timeScale<=0f||selfHealth==null||selfHealth.IsDead||!HeroCombatStatus.CanAct(gameObject)||Time.time<nextAttackTime)return false;Health target=targeting.Acquire(Range,transform.forward,true);if(target==null)return false;nextAttackTime=Time.time+Cooldown;targeting.RegisterOffensiveAction();Color attackColor=HeroColor();Vector3 look=target.transform.position-transform.position;look.y=0;locomotion.FaceAction(look);HeroCombatRules.BasicHit(gameObject,target,Damage);OnboardingSessionGuard.Attack(this);return true;}
private Color HeroColor()=>identity!=null?StylizedArtDirection.HeroAccent(identity.Hero):StylizedArtDirection.ZeroEnergy;private void OnDrawGizmosSelected(){Gizmos.DrawWireSphere(transform.position,Application.isPlaying?Range:fallbackRange);}
}
}


