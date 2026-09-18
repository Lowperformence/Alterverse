using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class LaneTower : MonoBehaviour
    {
        [SerializeField] private float attackRange = 5.25f, attackDamage = 18f, attackCooldown = 1.2f;
        private Health health, currentTarget;
        private int team;
        private float nextAttackTime, nextTargetScan;
        public int Team => team;
        public float AttackRange => attackRange;
        public Health CurrentTarget => IsValidTarget(currentTarget) ? currentTarget : null;
        private void Awake() => health = GetComponent<Health>();
        public void Initialize(int teamId) { Configure(teamId,attackRange,attackDamage,attackCooldown); } public void Configure(int teamId,float range,float damage,float cooldown){team=teamId;attackRange=Mathf.Max(1f,range);attackDamage=Mathf.Max(1f,damage);attackCooldown=Mathf.Max(.2f,cooldown);currentTarget=null;nextTargetScan=0;}

        public bool HasMinionCoverFor(int attackingTeam)
        {
            if (health == null || health.IsDead || attackingTeam == team) return false;
            Health target = CurrentTarget;
            LaneMinion minion = target != null ? target.GetComponent<LaneMinion>() : null;
            return minion != null && minion.Team == attackingTeam;
        }

        private bool IsValidTarget(Health candidate)
        {
            if (candidate == null || candidate == health || candidate.IsDead || !candidate.isActiveAndEnabled) return false;
            Vector3 delta = candidate.transform.position - transform.position; delta.y = 0;
            if (delta.sqrMagnitude > attackRange * attackRange) return false;
            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            if (minion != null) return minion.Team != team;
            TeamMember member = candidate.GetComponent<TeamMember>();
            return member != null && member.Team != team &&
                (candidate.GetComponent<HeroIdentity>() != null || candidate.GetComponent<HeroRespawn>() != null || candidate.GetComponent<MobaBotRespawn>() != null);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || health == null || health.IsDead) return;
            if (Time.time >= nextTargetScan || (currentTarget != null && CurrentTarget == null))
            {
                nextTargetScan = Time.time + .2f;
                currentTarget = FindEnemyTarget();
            }
            Health target = CurrentTarget;
            if (target == null) return;
            DimensionTowerShell shell = GetComponent<DimensionTowerShell>();
            if (shell != null) shell.AimAt(target.transform.position);
            if (Time.time < nextAttackTime) return;
            if (shell != null) shell.Fire();
            nextAttackTime = Time.time + attackCooldown;
            target.TakeDamage(attackDamage, gameObject);
            SpawnShot(target.transform.position);
        }

        private Health FindEnemyTarget()
        {
            Health hero = null, minion = null;
            float heroScore = float.MaxValue, minionScore = float.MaxValue;
            // Scan units, not colliders: armor colliders cannot crowd units out of a fixed buffer.
            foreach (Health candidate in FindObjectsByType<Health>())
            {
                if (!IsValidTarget(candidate)) continue;
                Vector3 delta = candidate.transform.position - transform.position; delta.y = 0;
                float score = delta.sqrMagnitude * (candidate == currentTarget ? .65f : 1f);
                if (candidate.GetComponent<LaneMinion>() != null)
                {
                    if (score < minionScore) { minionScore = score; minion = candidate; }
                }
                else if (score < heroScore) { heroScore = score; hero = candidate; }
            }
            return minion != null ? minion : hero;
        }

        private void SpawnShot(Vector3 target){Color c=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;DimensionTowerShell shell=GetComponent<DimensionTowerShell>();Vector3 origin=shell!=null?shell.MuzzlePosition:transform.position+Vector3.up*1.7f,direction=target-origin;float distance=direction.magnitude;if(distance<.01f)return;GameObject beam=GameObject.CreatePrimitive(PrimitiveType.Cube);beam.name="DF_TowerShot";beam.transform.position=origin+direction*.5f;beam.transform.rotation=Quaternion.LookRotation(direction);beam.transform.localScale=new Vector3(.11f,.11f,distance);Collider col=beam.GetComponent<Collider>();if(col!=null)col.enabled=false;Renderer r=beam.GetComponent<Renderer>();if(r!=null)r.material.color=c;PrototypeVfx.Ring(target,c,.65f,.18f);Destroy(beam,.1f);}
    }
}
