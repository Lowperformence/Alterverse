using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class LaneMinion : MonoBehaviour
    {
        // Reuse one live registry for movement snapshots and one group-leader pass per frame.
        private static readonly List<LaneMinion> active = new();
        public static IReadOnlyList<LaneMinion> Active => active;
        private static readonly Dictionary<(int team, string group), LaneMinion> groupLeaders = new();
        private static int leaderFrame = -1;

        private enum UnitRole : byte { None, Minion, Tower, Core, Member }
        private struct CachedUnitInfo
        {
            public UnitRole Role;
            public int Team;
            public RealityCore Core;
            public LaneTower Tower;
        }
        private static readonly Dictionary<Health, CachedUnitInfo> unitInfoCache = new(64);
        private static int unitInfoCacheFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            active.Clear();
            groupLeaders.Clear();
            leaderFrame = -1;
            unitInfoCache.Clear();
            unitInfoCacheFrame = -1;
        }

        private static CachedUnitInfo ResolveUnitInfo(Health candidate)
        {
            if (candidate == null) return default;
            int frame = Time.frameCount;
            if (unitInfoCacheFrame != frame)
            {
                unitInfoCacheFrame = frame;
                unitInfoCache.Clear();
            }
            if (unitInfoCache.TryGetValue(candidate, out CachedUnitInfo info))
                return info;

            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            if (minion != null)
            {
                info = new CachedUnitInfo { Role = UnitRole.Minion, Team = minion.Team };
            }
            else
            {
                LaneTower tower = candidate.GetComponent<LaneTower>();
                if (tower != null)
                {
                    info = new CachedUnitInfo { Role = UnitRole.Tower, Team = tower.Team, Tower = tower };
                }
                else
                {
                    RealityCore core = candidate.GetComponent<RealityCore>();
                    if (core != null)
                    {
                        info = new CachedUnitInfo { Role = UnitRole.Core, Team = core.Team, Core = core };
                    }
                    else
                    {
                        TeamMember member = candidate.GetComponent<TeamMember>();
                        if (member != null)
                            info = new CachedUnitInfo { Role = UnitRole.Member, Team = member.Team };
                    }
                }
            }
            unitInfoCache[candidate] = info;
            return info;
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            leaderFrame = -1;
        }

        private static void RefreshGroupLeaders()
        {
            if (leaderFrame == Time.frameCount) return;
            leaderFrame = Time.frameCount;
            groupLeaders.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                LaneMinion unit = active[i];
                if (unit == null || unit.health == null || unit.health.IsDead || unit.siegeUnit || string.IsNullOrEmpty(unit.waveGroupKey)) continue;
                var key = (unit.team, unit.waveGroupKey);
                if (!groupLeaders.TryGetValue(key, out LaneMinion leader) ||
                    (unit.team == 0 ? unit.transform.position.x > leader.transform.position.x : unit.transform.position.x < leader.transform.position.x))
                    groupLeaders[key] = unit;
            }
        }

        [SerializeField] private float moveSpeed = 2.8f, detectionRange = 7.5f, attackRange = 1.85f, attackDamage = 3.25f, attackCooldown = 1.35f;
        private Health health, target, pendingTarget;
        private bool targetIsStructure;
        private float hitAt, pendingDamage, pendingRange;
        private int team;
        private Alterverse.Match.DimensionsFront front;
        private float structureDamageMultiplier = 1f, frontInfluenceOnDeath;
        private bool siegeUnit;
        public bool IsSiegeUnit => siegeUnit;
        public float RunningSpeed => moveSpeed;
        private Vector3 destination;
        public Vector3 LaneDestination => destination;
        private float nextAttackTime, nextScanTime, nextRouteScan;
        private string waveGroupKey;
        private int waveSlot;
        private float regroupGraceUntil;
        private RealityCore routeCore;
        private bool routeBlocked;
        private Alterverse.Visuals.MechMachineGunVisual cachedGun;
        private bool gunResolved;
        public int Team => team;

        public void ConfigureFrontInfluence(float percent) => frontInfluenceOnDeath = Mathf.Max(0f, percent);
        public void ConfigureSiegeRole(bool enabled = true) { siegeUnit = enabled; leaderFrame = -1; }
        public void ConfigureCombat(float speed, float detection, float range, float damage, float cooldown, float vsStructures = 1f)
        {
            moveSpeed = speed;
            detectionRange = detection;
            attackRange = range;
            attackDamage = damage;
            attackCooldown = cooldown;
            structureDamageMultiplier = Mathf.Max(1f, vsStructures);
        }

        private void Awake()
        {
            gameObject.layer = GameplayCollision.Units;
            health = GetComponent<Health>();
            front = FindAnyObjectByType<Alterverse.Match.DimensionsFront>();
            if (health != null) health.Died += OnDied;
        }

        private void OnDestroy()
        {
            active.Remove(this);
            leaderFrame = -1;
            if (health != null) health.Died -= OnDied;
        }

        private void OnDisable()
        {
            active.Remove(this);
            leaderFrame = -1;
            pendingTarget = null;
        }

        private void OnDied()
        {
            leaderFrame = -1;
            pendingTarget = null;
            if (frontInfluenceOnDeath <= 0f) return;
            front ??= FindAnyObjectByType<Alterverse.Match.DimensionsFront>();
            if (front != null) front.Push(team == 0 ? 1 : 0, frontInfluenceOnDeath);
        }

        public void Initialize(int teamId, Vector3 laneDestination)
        {
            leaderFrame = -1;
            team = teamId;
            destination = laneDestination;
            nextAttackTime = Time.time + Random.Range(.2f, .65f);
            float stagger = (teamId & 1) * .043f + Random.Range(0f, .08f);
            nextScanTime = Time.time + stagger;
            nextRouteScan = Time.time + stagger + .05f;
        }

        public void ConfigureWaveGroup(string unitKey, int slot)
        {
            if (string.IsNullOrEmpty(unitKey)) return;
            int marker = unitKey.LastIndexOf(":I");
            waveGroupKey = marker > 0 ? unitKey.Substring(0, marker) : unitKey;
            waveSlot = Mathf.Max(0, slot);
            leaderFrame = -1;
            regroupGraceUntil = Time.time + 2.5f;
            float stagger = (team & 1) * .043f + (waveSlot % 6) * .029f;
            nextScanTime = Time.time + stagger;
            nextRouteScan = Time.time + stagger + .04f;
        }

        private void Update()
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay) return;
            if (Time.timeScale <= 0f) return;
            if (health == null || health.IsDead || !HeroCombatStatus.CanAct(gameObject))
            {
                pendingTarget = null;
                return;
            }
            if (front != null && front.HasEnded)
            {
                pendingTarget = null;
                return;
            }
            if (pendingTarget != null)
            {
                Vector3 facing = pendingTarget.transform.position - transform.position;
                Face(facing);
                if (Time.time >= hitAt)
                {
                    Health victim = pendingTarget;
                    pendingTarget = null;
                    if (IsValidTarget(victim) && HeroCombatRules.TargetDistance(transform.position, victim) <= pendingRange + .25f)
                    {
                        if (!gunResolved)
                        {
                            gunResolved = true;
                            cachedGun = GetComponentInChildren<Alterverse.Visuals.MechMachineGunVisual>();
                        }
                        if (cachedGun != null && cachedGun.Muzzle != null)
                            Alterverse.Visuals.MonsterAttackVfx.Resolve(gameObject, cachedGun.Muzzle.position, victim.transform.position + Vector3.up * .55f);
                        victim.TakeDamage(pendingDamage, gameObject);
                    }
                }
                return;
            }
            if (TryRegroup())
            {
                target = null;
                targetIsStructure = false;
                return;
            }
            if (!IsValidTarget(target))
            {
                target = null;
                targetIsStructure = false;
            }
            if (Time.time >= nextScanTime)
            {
                SetTarget(FindTarget());
                nextScanTime = Time.time + .2f;
            }
            if (target != null)
            {
                Vector3 delta = target.transform.position - transform.position;
                delta.y = 0;
                float range = targetIsStructure ? 2.4f : attackRange;
                if (delta.sqrMagnitude <= range * range)
                {
                    Face(delta);
                    if (Time.time >= nextAttackTime)
                    {
                        nextAttackTime = Time.time + attackCooldown;
                        Alterverse.Visuals.CombatMotion.Begin(gameObject, .18f, target);
                        float dealt = targetIsStructure ? attackDamage * structureDamageMultiplier : attackDamage;
                        pendingTarget = target;
                        pendingDamage = dealt;
                        pendingRange = range;
                        hitAt = Time.time + .18f;
                    }
                    return;
                }
                MoveTowards(target.transform.position);
                return;
            }

            // Siege mechs must always advance when combat is not actively holding them in range.
            // They deliberately bypass the ordinary wave's bastion/core route gate: their lane destination
            // is authoritative, and FindPriorityStructure will still pick up a nearby tower/core to fight.
            if (siegeUnit)
            {
                AdvanceSiegeLane();
                return;
            }

            if (Time.time >= nextRouteScan)
            {
                nextRouteScan = Time.time + .2f;
                routeCore = FindEnemyCore();
                routeBlocked = HasEnemyBastion();
            }
            if (!routeBlocked && routeCore != null && routeCore.IsVulnerable)
            {
                MoveTowards(routeCore.transform.position);
                return;
            }
            MoveTowards(destination);
        }

        private void SetTarget(Health newTarget)
        {
            target = newTarget;
            if (newTarget == null)
            {
                targetIsStructure = false;
                return;
            }
            CachedUnitInfo info = ResolveUnitInfo(newTarget);
            targetIsStructure = info.Role == UnitRole.Tower || info.Role == UnitRole.Core;
        }

        private bool TryRegroup()
        {
            if (siegeUnit || string.IsNullOrEmpty(waveGroupKey)) return false;
            RefreshGroupLeaders();
            groupLeaders.TryGetValue((team, waveGroupKey), out LaneMinion leader);
            if (leader == null || leader == this) return false;
            float behind = team == 0 ? leader.transform.position.x - transform.position.x : transform.position.x - leader.transform.position.x;
            bool assembly = Time.time < regroupGraceUntil;
            if (!assembly && behind < 2.15f) return false;
            Vector3 slot = leader.transform.position;
            float rear = .85f + Mathf.Max(0, waveSlot - 1) * .55f;
            slot.x += team == 0 ? -rear : rear;
            slot.z = destination.z + (waveSlot - 1) * .72f;
            slot.y = transform.position.y;
            MoveTowards(slot, 1.55f);
            return true;
        }

        private void AdvanceSiegeLane()
        {
            Vector3 lanePoint = destination;
            lanePoint.y = transform.position.y;
            MoveTowards(lanePoint);
        }

        private bool IsValidTarget(Health candidate)
        {
            if (candidate == null || candidate.IsDead || !candidate.isActiveAndEnabled || CombatVisibility.HiddenFrom(gameObject, candidate)) return false;
            CachedUnitInfo info = ResolveUnitInfo(candidate);
            bool enemy = info.Role switch
            {
                UnitRole.Minion => info.Team != team,
                UnitRole.Tower => info.Team != team,
                UnitRole.Core => info.Team != team && info.Core != null && info.Core.IsVulnerable,
                UnitRole.Member => info.Team != team,
                _ => false
            };
            if (!enemy) return false;
            Vector3 d = candidate.transform.position - transform.position;
            d.y = 0;
            return d.sqrMagnitude <= detectionRange * detectionRange * 1.45f;
        }

        private Health FindTarget()
        {
            if (siegeUnit)
            {
                Health structure = FindPriorityStructure();
                if (structure != null) return structure;
            }
            Health waveOpponent = FindOpposingMinion();
            if (waveOpponent != null) return waveOpponent;
            Health best = null;
            float bestScore = float.MaxValue, maxSqr = detectionRange * detectionRange;
            var activeHealth = Health.Active;
            for (int index = 0; index < activeHealth.Count; index++)
            {
                Health candidate = activeHealth[index];
                if (candidate == null || candidate == health || candidate.IsDead || CombatVisibility.HiddenFrom(gameObject, candidate)) continue;
                CachedUnitInfo info = ResolveUnitInfo(candidate);
                bool enemy = false;
                float priority = 1f;
                switch (info.Role)
                {
                    case UnitRole.Minion:
                        enemy = info.Team != team;
                        priority = .42f;
                        break;
                    case UnitRole.Tower:
                        enemy = info.Team != team;
                        priority = siegeUnit ? .28f : .78f;
                        break;
                    case UnitRole.Core:
                        enemy = info.Team != team && info.Core != null && info.Core.IsVulnerable;
                        priority = .9f;
                        break;
                    case UnitRole.Member:
                        enemy = info.Team != team;
                        priority = 1.15f;
                        break;
                }
                if (!enemy) continue;
                Vector3 delta = candidate.transform.position - transform.position;
                delta.y = 0;
                float sqr = delta.sqrMagnitude;
                if (sqr > maxSqr) continue;
                float lanePenalty = 1f + Mathf.Abs(delta.z) * .12f;
                float score = sqr * priority * lanePenalty;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }

        private Health FindPriorityStructure()
        {
            Health best = null;
            float bestSqr = detectionRange * detectionRange;
            var activeHealth = Health.Active;
            for (int index = 0; index < activeHealth.Count; index++)
            {
                Health hp = activeHealth[index];
                if (hp == null || hp.IsDead) continue;
                CachedUnitInfo info = ResolveUnitInfo(hp);
                if (info.Role != UnitRole.Tower || info.Team == team || info.Tower == null) continue;
                Vector3 d = info.Tower.transform.position - transform.position;
                d.y = 0;
                if (Mathf.Abs(info.Tower.transform.position.z - destination.z) > 6f || d.sqrMagnitude > bestSqr) continue;
                bestSqr = d.sqrMagnitude;
                best = hp;
            }
            if (best != null) return best;
            RealityCore core = FindEnemyCore();
            if (core == null || !core.IsVulnerable) return null;
            Health coreHp = core.GetComponent<Health>();
            if (coreHp == null || coreHp.IsDead) return null;
            Vector3 delta = core.transform.position - transform.position;
            delta.y = 0;
            return delta.sqrMagnitude <= bestSqr ? coreHp : null;
        }

        private Health FindOpposingMinion()
        {
            // Keep a live wave opponent until it dies or leaves detection range.
            if (IsValidTarget(target))
            {
                CachedUnitInfo currentInfo = ResolveUnitInfo(target);
                if (currentInfo.Role == UnitRole.Minion && currentInfo.Team != team) return target;
            }
            Health closest = null;
            float nearest = detectionRange * detectionRange;
            for (int index = 0; index < active.Count; index++)
            {
                LaneMinion other = active[index];
                if (other == null || other == this || other.team == team) continue;
                Health hp = other.health;
                if (hp == null || hp.IsDead) continue;
                Vector3 delta = other.transform.position - transform.position;
                delta.y = 0;
                if (delta.sqrMagnitude < nearest)
                {
                    nearest = delta.sqrMagnitude;
                    closest = hp;
                }
            }
            return closest;
        }

        private bool HasEnemyBastion()
        {
            var activeHealth = Health.Active;
            for (int index = 0; index < activeHealth.Count; index++)
            {
                Health h = activeHealth[index];
                if (h == null || h.IsDead) continue;
                CachedUnitInfo info = ResolveUnitInfo(h);
                if (info.Role != UnitRole.Tower || info.Team == team || info.Tower == null) continue;
                /* Only the bastion on this minion's lane blocks its route. The other lane must not hold this wave hostage. */
                if (Mathf.Abs(info.Tower.transform.position.z - transform.position.z) < 6f) return true;
            }
            return false;
        }

        private RealityCore FindEnemyCore()
        {
            var activeHealth = Health.Active;
            for (int index = 0; index < activeHealth.Count; index++)
            {
                Health h = activeHealth[index];
                if (h == null) continue;
                CachedUnitInfo info = ResolveUnitInfo(h);
                if (info.Role == UnitRole.Core && info.Team != team && info.Core != null)
                    return info.Core;
            }
            return null;
        }

        private void MoveTowards(Vector3 point, float speedMultiplier = 1f)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .16f) return;
            float distance = direction.magnitude;
            direction.Normalize();
            transform.position += direction * Mathf.Min(moveSpeed * Mathf.Max(.1f, speedMultiplier) * HeroCombatStatus.Speed(gameObject) * Time.deltaTime, distance);
            Face(direction);
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10 * Time.deltaTime);
        }
    }
}
