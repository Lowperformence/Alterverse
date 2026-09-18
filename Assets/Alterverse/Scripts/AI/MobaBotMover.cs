using System.Collections.Generic;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health), typeof(MobaBotBrain), typeof(BotLanePlan))]
    [RequireComponent(typeof(BotTargetSensor))]
    public sealed class MobaBotMover : MonoBehaviour
    {
        [SerializeField] private float fallbackMoveSpeed = 3.35f, laneStopDistance = .35f, chaseLimit = 12f, turnSpeed = 7f;
        private const float ArenaHalfWidth = 40f, ArenaHalfHeight = 33f, TowerMargin = .75f;
        private static readonly List<MobaBotMover> ActiveBots = new();
        private static long nextSeparationOrder;
        private long separationOrder;
        private Health health;
        private MobaBotBrain brain;
        private BotLanePlan lanePlan;
        private BotTargetSensor sensor;
        private HeroCombatProfile profile;
        private PlanetBuffReceiver planetBuff;
        private float nextWorldScan;
        private DimensionPlanet objective;
        private DimensionPlanet[] planets = System.Array.Empty<DimensionPlanet>();
        private LaneTower[] towers = System.Array.Empty<LaneTower>();
        private PlanetPulseAttack[] pulses = System.Array.Empty<PlanetPulseAttack>();
        private Vector3 velocity;
        private int rotationSlot;
        public bool IsAvoidingHazard { get; private set; }
        private float MoveSpeed => (profile != null ? profile.MoveSpeed : fallbackMoveSpeed) * (planetBuff != null ? planetBuff.MoveSpeedMultiplier : 1f);
        private float CombatStop => profile != null ? Mathf.Max(1.15f, profile.AttackRange * .78f) : 1.4f;

        private void Awake()
        {
            separationOrder = ++nextSeparationOrder;
            health = GetComponent<Health>(); brain = GetComponent<MobaBotBrain>();
            lanePlan = GetComponent<BotLanePlan>(); sensor = GetComponent<BotTargetSensor>();
            profile = GetComponent<HeroCombatProfile>(); planetBuff = GetComponent<PlanetBuffReceiver>();
            // Stable across sessions; string.GetHashCode does not promise this.
            uint hash = 0;
            foreach (char c in gameObject.name) hash = unchecked(hash * 31u + c);
            rotationSlot = (int)(hash % 5u);
        }
        private void OnEnable() { if (!ActiveBots.Contains(this)) ActiveBots.Add(this); }
        private void OnDisable() { ActiveBots.Remove(this); ResetMotion(); }

        private void Update()
        {
            IsAvoidingHazard = false;
            if (Time.timeScale <= 0f || health == null || health.IsDead || brain == null || lanePlan == null)
            { velocity = Vector3.zero; return; }
            if (planetBuff == null) planetBuff = GetComponent<PlanetBuffReceiver>();
            ScanWorld();
            Vector3 destination = lanePlan.GetDestination(brain.State);
            float stopDistance = laneStopDistance;
            Health target = sensor != null ? sensor.CurrentTarget : null;
            if (target != null && !target.IsDead && brain.State == MobaBotBrain.BotState.Fight)
            {
                Vector3 delta = target.transform.position - transform.position; delta.y = 0;
                if (delta.sqrMagnitude <= chaseLimit * chaseLimit)
                { destination = target.transform.position; stopDistance = CombatStop; }
            }
            else if (brain.State == MobaBotBrain.BotState.Lane && objective != null && objective.Available && ShouldRotate())
            { destination = objective.transform.position; stopDistance = 2.2f; }

            if (TryGetPulseEscape(out Vector3 escape))
            {
                IsAvoidingHazard = true;
                destination = escape; stopDistance = .1f;
            }
            MoveToward(destination, stopDistance);
        }

        private void ScanWorld()
        {
            if (Time.time < nextWorldScan) return;
            nextWorldScan = Time.time + .5f;
            planets = FindObjectsByType<DimensionPlanet>();
            pulses = FindObjectsByType<PlanetPulseAttack>();
            towers = FindObjectsByType<LaneTower>();
            objective = null;
            float best = float.MaxValue;
            foreach (DimensionPlanet planet in planets)
            {
                if (planet == null || !planet.Available) continue;
                float distance = (planet.transform.position - transform.position).sqrMagnitude;
                if (distance < best) { best = distance; objective = planet; }
            }
        }
        private bool ShouldRotate()
        {
            if (objective == null) return false;
            float sqr = (objective.transform.position - transform.position).sqrMagnitude;
            return sqr <= 26f * 26f && (rotationSlot < 2 || sqr < 10f * 10f);
        }

        private bool TryGetPulseEscape(out Vector3 escape)
        {
            escape = transform.position;
            float deepest = 0f;
            foreach (PlanetPulseAttack pulse in pulses)
            {
                if (pulse == null || !pulse.IsTelegraphing) continue;
                Vector3 away = transform.position - pulse.transform.position; away.y = 0;
                float distance = away.magnitude, safeRadius = pulse.BlastRadius + .7f;
                if (distance >= safeRadius || safeRadius - distance <= deepest) continue;
                deepest = safeRadius - distance;
                if (distance < .01f) away = brain.Team == 0 ? Vector3.left : Vector3.right;
                escape = pulse.transform.position + away.normalized * safeRadius;
                escape.y = transform.position.y;
            }
            return deepest > 0f;
        }

        public void ResetMotion()
        {
            velocity = Vector3.zero; objective = null; nextWorldScan = 0; IsAvoidingHazard = false;
        }

        public bool CanRepositionTo(Vector3 destination)
        {
            ScanWorld();
            foreach (LaneTower tower in towers)
            {
                if (tower == null || !tower.isActiveAndEnabled || brain == null || tower.Team == brain.Team || tower.HasMinionCoverFor(brain.Team)) continue;
                Health hp = tower.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                Vector3 from = transform.position - tower.transform.position; from.y = 0;
                Vector3 to = destination - tower.transform.position; to.y = 0;
                float safeRadius = tower.AttackRange + TowerMargin;
                // Allow an escape from existing danger, never a new dive or a crossing dash.
                if (from.sqrMagnitude < safeRadius * safeRadius)
                {
                    if (to.sqrMagnitude <= from.sqrMagnitude) return false;
                    continue;
                }
                Vector3 step = to - from;
                float t = step.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector3.Dot(from, step) / step.sqrMagnitude) : 0;
                if ((from + step * t).sqrMagnitude < safeRadius * safeRadius) return false;
            }
            foreach (PlanetPulseAttack pulse in pulses)
            {
                if (pulse == null || !pulse.IsTelegraphing) continue;
                Vector3 from = transform.position - pulse.transform.position; from.y = 0;
                Vector3 to = destination - pulse.transform.position; to.y = 0;
                float safeRadius = pulse.BlastRadius + .7f;
                if (to.sqrMagnitude < safeRadius * safeRadius && to.sqrMagnitude <= from.sqrMagnitude) return false;
            }
            return true;
        }

        private Vector3 SeparationVelocity()
        {
            Vector3 push = Vector3.zero;
            const float spacing = 1.15f;
            foreach (MobaBotMover other in ActiveBots)
            {
                if (other == null || other == this || other.health == null || other.health.IsDead) continue;
                Vector3 away = transform.position - other.transform.position; away.y = 0;
                float distance = away.magnitude;
                if (distance >= spacing) continue;
                if (distance < .001f) away = separationOrder < other.separationOrder ? Vector3.left : Vector3.right;
                push += away.normalized * (1f - distance / spacing);
            }
            return Vector3.ClampMagnitude(push, 1f) * 1.5f;
        }

        private Vector3 GuardAgainstTowers(Vector3 next)
        {
            if (brain == null) return next;
            foreach (LaneTower tower in towers)
            {
                if (tower == null || !tower.isActiveAndEnabled || tower.Team == brain.Team || tower.HasMinionCoverFor(brain.Team)) continue;
                Health towerHealth = tower.GetComponent<Health>();
                if (towerHealth == null || towerHealth.IsDead) continue;
                Vector3 away = transform.position - tower.transform.position; away.y = 0;
                float safeRadius = tower.AttackRange + TowerMargin;
                if (away.sqrMagnitude < safeRadius * safeRadius)
                {
                    // If cover dies, leave the danger zone instead of continuing to dive.
                    if (away.sqrMagnitude < .001f) away = brain.Team == 0 ? Vector3.left : Vector3.right;
                    Vector3 escape = tower.transform.position + away.normalized * (safeRadius + .1f);
                    escape.y = transform.position.y;
                    next = Vector3.MoveTowards(transform.position, escape, MoveSpeed * Time.deltaTime);
                    velocity = Vector3.zero;
                }
                else
                {
                    // Test the whole step, so a long frame cannot cross the tower circle.
                    Vector3 step = next - transform.position; step.y = 0;
                    float along = step.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector3.Dot(away, step) / step.sqrMagnitude) : 0f;
                    Vector3 closest = away + step * along;
                    if (closest.sqrMagnitude < safeRadius * safeRadius)
                    { next = transform.position; velocity = Vector3.zero; }
                }
            }
            return next;
        }

        private void MoveToward(Vector3 destination, float stopDistance)
        {
            Vector3 delta = destination - transform.position; delta.y = 0;
            Vector3 advance = Vector3.zero;
            if (delta.sqrMagnitude <= stopDistance * stopDistance) velocity = Vector3.zero;
            else
            {
                if (Vector3.Dot(velocity, delta) < 0f) velocity = Vector3.zero;
                velocity = Vector3.Lerp(velocity, delta.normalized * MoveSpeed, Mathf.Clamp01(6f * Time.deltaTime));
                advance = Vector3.ClampMagnitude(velocity * Time.deltaTime, Mathf.Max(0f, delta.magnitude - stopDistance));
            }
            Vector3 step = Vector3.ClampMagnitude(advance + SeparationVelocity() * Time.deltaTime, MoveSpeed * Time.deltaTime);
            Vector3 next = GuardAgainstTowers(transform.position + step);
            next.x = Mathf.Clamp(next.x, -ArenaHalfWidth, ArenaHalfWidth);
            next.z = Mathf.Clamp(next.z, -ArenaHalfHeight, ArenaHalfHeight);
            Vector3 movement = next - transform.position;
            transform.position = next;
            if (movement.sqrMagnitude > .00001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement.normalized), turnSpeed * Time.deltaTime);
        }
    }
}
