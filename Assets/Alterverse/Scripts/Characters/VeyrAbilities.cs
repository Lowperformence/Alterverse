using System.Collections;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(CharacterController)), RequireComponent(typeof(Health))]
    public sealed class VeyrAbilities : MonoBehaviour
    {
        [Header("Cooldowns")]
        [SerializeField] private float shadowStrikeCooldown=5f,soulRendCooldown=6f,nullStepCooldown=7f,endFutureCooldown=14f;
        private const float ArenaHalfSize=29f;
        private static readonly Color ShadowColor=new(.42f,.10f,.72f),SoulColor=new(.72f,.08f,.25f),FutureColor=new(.90f,.12f,.45f);
        private CharacterController controller; private Health selfHealth; private TeamMember selfTeam; private float qReady,eReady,rReady,fReady;
        public float QRemaining=>Mathf.Max(0,qReady-Time.time); public float ERemaining=>Mathf.Max(0,eReady-Time.time); public float RRemaining=>Mathf.Max(0,rReady-Time.time); public float FRemaining=>Mathf.Max(0,fReady-Time.time);
        private void Awake(){controller=GetComponent<CharacterController>();selfHealth=GetComponent<Health>();selfTeam=GetComponent<TeamMember>();}
        private void Update(){if(Keyboard.current==null||selfHealth.IsDead)return;if(Keyboard.current.qKey.wasPressedThisFrame)ShadowStrike();if(Keyboard.current.eKey.wasPressedThisFrame)SoulRend();if(Keyboard.current.rKey.wasPressedThisFrame)NullStep();if(Keyboard.current.fKey.wasPressedThisFrame)EndOfFuture();}

        public bool ShadowStrike(){if(Time.time<qReady)return false;Health target=FindNearestEnemy(5.5f);if(target==null)return false;qReady=Time.time+shadowStrikeCooldown;Vector3 start=transform.position;Vector3 behind=-target.transform.forward;behind.y=0;if(behind.sqrMagnitude<.01f)behind=(transform.position-target.transform.position).normalized;Vector3 destination=ClampToArena(target.transform.position+behind.normalized*1.25f);PrototypeVfx.ShadowTrail(start,destination,ShadowColor);PrototypeVfx.Ring(start,ShadowColor,1.25f,.18f);Teleport(destination);Face(target.transform.position);PrototypeVfx.Ring(transform.position,ShadowColor,1.45f,.20f);PrototypeVfx.Slash(transform,ShadowColor,2.2f);target.TakeDamage(26f);PrototypeVfx.Burst(target.transform.position,ShadowColor,1.05f,9);return true;}
        public bool SoulRend(){if(Time.time<eReady)return false;eReady=Time.time+soulRendCooldown;PrototypeVfx.Ring(transform.position,SoulColor,2.8f,.38f);PrototypeVfx.Burst(transform.position,SoulColor,1.25f,10);Collider[] hits=Physics.OverlapSphere(transform.position,2.8f,~0,QueryTriggerInteraction.Ignore);HashSet<Health> damaged=new();float totalDamage=0;foreach(Collider hit in hits){Health health=hit.GetComponentInParent<Health>();if(!IsEnemy(health)||!damaged.Add(health))continue;health.TakeDamage(30f);PrototypeVfx.SoulLink(health.transform.position,transform.position,SoulColor);PrototypeVfx.Burst(health.transform.position,SoulColor,.9f,7);totalDamage+=30f;}if(totalDamage>0){selfHealth.Heal(totalDamage*.30f);PrototypeVfx.Burst(transform.position,new Color(.18f,.85f,.42f),.65f,6);}return true;}
        public bool NullStep(){if(Time.time<rReady)return false;rReady=Time.time+nullStepCooldown;Vector3 start=transform.position,destination=ClampToArena(transform.position+transform.forward*4.2f);PrototypeVfx.ShadowTrail(start,destination,ShadowColor);PrototypeVfx.Ring(start,ShadowColor,.9f,.16f);Teleport(destination);PrototypeVfx.Ring(transform.position,ShadowColor,1.5f,.24f);Health target=FindNearestEnemy(2.2f);if(target!=null){target.TakeDamage(16f);PrototypeVfx.Burst(target.transform.position,ShadowColor,.75f,6);}return true;}
        public bool EndOfFuture(){if(Time.time<fReady)return false;Health target=FindNearestEnemy(7.5f);if(target==null)return false;fReady=Time.time+endFutureCooldown;PrototypeVfx.FutureMark(target.transform,FutureColor,1.15f);StartCoroutine(ResolveUltimate(target));return true;}
        private IEnumerator ResolveUltimate(Health target){yield return new WaitForSeconds(1.15f);if(target==null||target.IsDead||!IsEnemy(target))yield break;float damage=30f+target.Current*.28f;PrototypeVfx.Ring(target.transform.position,FutureColor,3.1f,.42f);PrototypeVfx.Burst(target.transform.position,FutureColor,1.7f,14);target.TakeDamage(damage);}
        private Health FindNearestEnemy(float radius){Collider[] hits=Physics.OverlapSphere(transform.position,radius,~0,QueryTriggerInteraction.Ignore);Health closest=null;float closestSqr=float.MaxValue;HashSet<Health> seen=new();foreach(Collider hit in hits){Health health=hit.GetComponentInParent<Health>();if(!seen.Add(health)||!IsEnemy(health))continue;float sqr=(health.transform.position-transform.position).sqrMagnitude;if(sqr<closestSqr){closestSqr=sqr;closest=health;}}return closest;}
        private bool IsEnemy(Health health){if(health==null||health==selfHealth||health.IsDead)return false;TeamMember member=health.GetComponent<TeamMember>();if(member!=null&&selfTeam!=null)return member.Team!=selfTeam.Team;int team=selfTeam!=null?selfTeam.Team:0;LaneMinion minion=health.GetComponent<LaneMinion>();if(minion!=null)return minion.Team!=team;LaneTower tower=health.GetComponent<LaneTower>();if(tower!=null)return tower.Team!=team;return health.GetComponent<PrototypeEnemyBot>()!=null;}
        private static Vector3 ClampToArena(Vector3 p){p.x=Mathf.Clamp(p.x,-ArenaHalfSize,ArenaHalfSize);p.z=Mathf.Clamp(p.z,-ArenaHalfSize,ArenaHalfSize);return p;}private void Face(Vector3 p){p.y=transform.position.y;transform.LookAt(p);}private void Teleport(Vector3 p){controller.enabled=false;transform.position=p;controller.enabled=true;}
    }
}
