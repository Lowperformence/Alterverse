using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Visuals;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health), typeof(BotTargetSensor), typeof(HeroIdentity))]
    [RequireComponent(typeof(HeroAbilityKit))]
    public sealed class HeroBotAbilities : MonoBehaviour
    {
        private Health self;
        private BotTargetSensor sensor;
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private HeroCombatProfile profile;
        private MobaBotBrain brain;
        private TeamMember team;
        private PlanetBuffReceiver buff;
        private readonly float[] ready = new float[4];
        private float nextDecision;

        private void Awake()
        {
            self = GetComponent<Health>(); sensor = GetComponent<BotTargetSensor>();
            identity = GetComponent<HeroIdentity>(); kit = GetComponent<HeroAbilityKit>();
            profile = GetComponent<HeroCombatProfile>(); brain = GetComponent<MobaBotBrain>();
            team = GetComponent<TeamMember>();
            float stagger = Random.Range(.7f, 1.8f);
            for (int slot = 0; slot < 4; slot++) ready[slot] = Time.time + stagger + slot * .55f;
            ready[3] = Time.time + 45f;
            nextDecision = Time.time + stagger;
        }
        private AbilitySpec Spec(int slot) => slot == 0 ? kit.Q : slot == 1 ? kit.W : slot == 2 ? kit.E : kit.Ultimate;

        private void Update()
        {
            if (Time.timeScale <= 0f || self == null || self.IsDead || identity == null || kit == null || Time.time < nextDecision) return;
            nextDecision = Time.time + Random.Range(.28f, .48f);
            if (buff == null) buff = GetComponent<PlanetBuffReceiver>();
            Health target = sensor != null ? sensor.CurrentTarget : null;
            // Defensive utility is useful while retreating too, and does not require an enemy.
            if (identity.Hero == HeroId.Mira)
            {
                Health ally = FindWoundedAlly(kit.Ultimate.Range);
                if (ally != null && ally.Normalized < .4f && TryCast(3, target)) return;
                if (TryCast(1, target) || TryCast(2, target)) return;
            }
            else if ((identity.Hero == HeroId.Kaelon || identity.Hero == HeroId.Rook) && self.Normalized < .8f)
            {
                if (TryCast(1, target)) return;
            }
            if (target == null || target.IsDead) return;
            float distance = FlatDistance(target.transform.position, transform.position);
            if (distance < 3.5f)
            {
                if (identity.Hero == HeroId.Nyra && TryCast(1, target)) return;
                if (identity.Hero == HeroId.Elyra && TryCast(2, target)) return;
            }
            bool fighting = brain == null || brain.State == MobaBotBrain.BotState.Fight;
            if (identity.Hero == HeroId.Veyr &&
                ((!fighting && self.Normalized < .5f) || (fighting && distance > 3f && distance < 7f)) && TryCast(2, target)) return;
            if (!fighting) return;
            if (identity.Hero != HeroId.Mira && (target.Normalized < .42f || self.Normalized < .28f) && TryCast(3, target)) return;
            if (TryCast(0, target)) return;
            if (identity.Hero != HeroId.Kaelon && identity.Hero != HeroId.Rook && identity.Hero != HeroId.Nyra && identity.Hero != HeroId.Mira && TryCast(1, target)) return;
            if (identity.Hero == HeroId.Kaelon || identity.Hero == HeroId.Nyra || identity.Hero == HeroId.Rook) TryCast(2, target);
        }

        private bool TryCast(int slot, Health target)
        {
            if (Time.time < ready[slot]) return false;
            AbilitySpec spec = Spec(slot);
            HeroId hero = identity.Hero;
            Color accent = StylizedArtDirection.HeroAccent(hero);
            Vector3 origin = transform.position, end = origin;
            float scale = profile != null ? profile.AbilityPower : 1f;
            if (hero == HeroId.Mira && slot != 0)
            {
                Health ally = FindWoundedAlly(spec.Range);
                if (ally == null) return false;
                ally.Heal(spec.Power > 0 ? spec.Power * scale : self.Max * .12f);
                end = ally.transform.position;
                PrototypeVfx.Bloom(end, accent, slot == 3 ? 3f : 1.8f);
            }
            else if ((hero == HeroId.Kaelon || hero == HeroId.Rook) && slot == 1)
            {
                if (self.Normalized >= .8f) return false;
                self.Heal(self.Max * (hero == HeroId.Kaelon ? .12f : .09f));
                PrototypeVfx.Guard(origin, accent, 1.8f);
            }
            else if ((hero == HeroId.Nyra && slot == 1) || (hero == HeroId.Elyra && slot == 2) || (hero == HeroId.Veyr && slot == 2))
            {
                if (target == null || target.IsDead) return false;
                Vector3 direction = origin - target.transform.position;
                float distance = hero == HeroId.Nyra ? 3f : hero == HeroId.Elyra ? 2f : 4.2f;
                if (hero == HeroId.Veyr && brain != null && brain.State == MobaBotBrain.BotState.Fight)
                {
                    direction = -direction;
                    distance = Mathf.Min(distance, Mathf.Max(0f, FlatDistance(origin, target.transform.position) - 1.5f));
                }
                direction.y = 0;
                if (direction.sqrMagnitude < .01f || distance < .1f) return false;
                end = origin + direction.normalized * distance;
                end.x = Mathf.Clamp(end.x, -40f, 40f); end.z = Mathf.Clamp(end.z, -33f, 33f);
                MobaBotMover mover = GetComponent<MobaBotMover>();
                if (mover != null && !mover.CanRepositionTo(end)) return false;
                transform.position = end;
                mover?.ResetMotion();
                PrototypeVfx.ShadowTrail(origin, end, accent);
            }
            else
            {
                if (target == null || target.IsDead || spec.Power <= 0 || FlatDistance(origin, target.transform.position) > spec.Range) return false;
                float damage = spec.Power * scale;
                if (hero == HeroId.Veyr && slot == 3) damage *= 1f + (1f - target.Normalized) * .55f;
                if (target.GetComponent<LaneMinion>() != null && buff != null) damage *= buff.MinionDamageMultiplier;
                target.TakeDamage(damage, gameObject);
                if (hero == HeroId.Veyr) self.Heal(damage * .12f);
                if (hero == HeroId.Rook && self.Normalized < .45f) self.Heal(damage * .1f);
                end = target.transform.position;
                PrototypeVfx.ProjectileLine(origin, end, accent, .1f);
            }
            ready[slot] = Time.time + spec.Cooldown * (buff != null ? buff.CooldownMultiplier : 1f);
            FounderAbilityVfx.CastSignature(identity, slot, origin, end);
            return true;
        }

        private Health FindWoundedAlly(float range)
        {
            Health best = null;
            float lowest = .9f;
            foreach (Health candidate in FindObjectsByType<Health>())
            {
                if (candidate == null || candidate.IsDead || candidate.Normalized >= lowest) continue;
                TeamMember member = candidate.GetComponent<TeamMember>();
                if (member == null || team == null || member.Team != team.Team || FlatDistance(candidate.transform.position, transform.position) > range) continue;
                lowest = candidate.Normalized; best = candidate;
            }
            return best;
        }
        private static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    }
}
