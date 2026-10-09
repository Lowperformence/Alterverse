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
        private float nextAttackTime, nextTargetScan, nextCoverScan;
        private int cachedCoverTeam=-1;
        private bool cachedCover, deathHandled;
        public int Team => team;
        public float AttackRange => attackRange;
        public Health CurrentTarget => IsValidTarget(currentTarget) ? currentTarget : null;
        private void Awake()
        {
            health = GetComponent<Health>();
            if (health != null) health.Died += OnDied;
        }
        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }
        public void Initialize(int teamId) { Configure(teamId,attackRange,attackDamage,attackCooldown); } public void Configure(int teamId,float range,float damage,float cooldown){team=teamId;attackRange=Mathf.Max(1f,range);attackDamage=Mathf.Max(1f,damage);attackCooldown=Mathf.Max(.2f,cooldown);currentTarget=null;nextTargetScan=0;nextCoverScan=0;cachedCoverTeam=-1;cachedCover=false;}

        public bool HasMinionCoverFor(int attackingTeam)
        {
            if (health == null || health.IsDead || attackingTeam == team) return false;

            // If the tower is already shooting a lane minion, hero cover is unquestionably active.
            Health target = CurrentTarget;
            LaneMinion lockedMinion = target != null ? target.GetComponent<LaneMinion>() : null;
            if (lockedMinion != null && lockedMinion.Team == attackingTeam) return true;

            // Do not make heroes freeze at the exact tower-radius boundary while their wave is
            // already entering range. Cache briefly because several bots query this every frame.
            if (cachedCoverTeam == attackingTeam && Time.time < nextCoverScan) return cachedCover;
            cachedCoverTeam = attackingTeam;
            nextCoverScan = Time.time + .12f;
            cachedCover = false;
            float coverRadius = Mathf.Max(1f, attackRange - .12f);
            float coverSqr = coverRadius * coverRadius;
            for (int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(candidate==null||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                LaneMinion minion=candidate.GetComponent<LaneMinion>();
                if(minion==null||minion.Team!=attackingTeam)continue;
                Vector3 delta=candidate.transform.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude<=coverSqr){cachedCover=true;break;}
            }
            return cachedCover;
        }

        private void OnDied()
        {
            if (deathHandled) return;
            deathHandled = true;
            currentTarget = null;
            cachedCover = false;

            // A tower is always a destructible structure. Health normally destroys it because
            // bootstrap configures destroyOnDeath=true; this explicit cleanup is a safety net for
            // scene/prefab towers that were configured differently.
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;

            Color color = team == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            PrototypeVfx.RadialBurst(transform.position, color, 2.5f);
            PrototypeVfx.Burst(transform.position, color, .85f, 8);
            enabled = false;
            Destroy(gameObject);
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
                (candidate.GetComponent<HeroIdentity>() != null || candidate.GetComponent<HeroRespawn>() != null ||
                 candidate.GetComponent<MobaBotRespawn>() != null || candidate.GetComponent<HotZoneTank>() != null);
        }

        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if (Time.timeScale <= 0f || health == null || health.IsDead) return;
            if (Time.time >= nextTargetScan || (currentTarget != null && CurrentTarget == null))
            {
                nextTargetScan = Time.time + .2f;
                Health previous=currentTarget;currentTarget = FindEnemyTarget();
                if(currentTarget!=previous&&currentTarget!=null&&currentTarget.GetComponent<HeroRespawn>()!=null)Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.TowerTarget,.8f);
            }
            Health target = CurrentTarget;
            if (target == null) return;
            DimensionTowerShell shell = GetComponent<DimensionTowerShell>();
            if (shell != null) shell.AimAt(target.transform.position);
            if (Time.time < nextAttackTime) return;
            if (shell != null) shell.Fire();
            nextAttackTime = Time.time + attackCooldown;
            Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.TowerShot,transform.position,.55f);
            float before=target.Current+target.Shield;
            target.TakeDamage(attackDamage, gameObject);
            if(target.Current+target.Shield<before)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.TowerImpact,target.transform.position,.5f);
            SpawnShot(target.transform.position);
        }

        private Health FindEnemyTarget()
        {
            Health hero = null, minion = null;
            float heroScore = float.MaxValue, minionScore = float.MaxValue;
            // Scan units, not colliders: armor colliders cannot crowd units out of a fixed buffer.
            foreach (Health candidate in Health.Active)
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

        private void SpawnShot(Vector3 target)
        {
            Color color=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            DimensionTowerShell shell=GetComponent<DimensionTowerShell>();
            Vector3 origin=shell!=null?shell.MuzzlePosition:transform.position+Vector3.up*1.7f;
            if((target-origin).sqrMagnitude<.0001f)return;
            HeroVfxPool.Trace(origin,target,color,.11f,.1f);
            PrototypeVfx.Ring(target,color,.65f,.18f);
        }
    }
}

