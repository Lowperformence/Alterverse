using Alterverse.AI;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))] public sealed class BasicAttack:MonoBehaviour
    {
        [SerializeField,Min(.1f)]private float range=2.2f;[SerializeField,Min(0f)]private float damage=20f;[SerializeField,Min(.05f)]private float cooldown=.7f;[SerializeField]private LayerMask targetMask=~0;private Health selfHealth;private TeamMember selfTeam;private float nextAttackTime;
        private void Awake(){selfHealth=GetComponent<Health>();selfTeam=GetComponent<TeamMember>();}
        private void Update(){if(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame)TryAttack();}
        public bool TryAttack(){if(selfHealth.IsDead||Time.time<nextAttackTime)return false;Collider[] hits=Physics.OverlapSphere(transform.position,range,targetMask,QueryTriggerInteraction.Ignore);Health closest=null;float closestSqr=float.MaxValue;foreach(Collider hit in hits){Health h=hit.GetComponentInParent<Health>();if(h==null||h==selfHealth||h.IsDead||!IsEnemy(h))continue;float sqr=(h.transform.position-transform.position).sqrMagnitude;if(sqr<closestSqr){closestSqr=sqr;closest=h;}}if(closest==null)return false;nextAttackTime=Time.time+cooldown;PrototypeVfx.Slash(transform,new Color(.44f,.12f,.75f),1.5f);Vector3 look=closest.transform.position-transform.position;look.y=0;if(look.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(look.normalized,Vector3.up);closest.TakeDamage(damage,gameObject);PrototypeVfx.Burst(closest.transform.position,new Color(.58f,.16f,.90f),.85f,6);return true;}
        private bool IsEnemy(Health h){TeamMember m=h.GetComponent<TeamMember>();if(m!=null&&selfTeam!=null)return m.Team!=selfTeam.Team;LaneMinion minion=h.GetComponent<LaneMinion>();if(minion!=null)return minion.Team!=(selfTeam!=null?selfTeam.Team:0);LaneTower tower=h.GetComponent<LaneTower>();if(tower!=null)return tower.Team!=(selfTeam!=null?selfTeam.Team:0);return h.GetComponent<PrototypeEnemyBot>()!=null;}
        private void OnDrawGizmosSelected(){Gizmos.DrawWireSphere(transform.position,range);}
    }
}
