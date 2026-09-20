using Alterverse.Characters;
using Alterverse.Match;
using UnityEngine;
namespace Alterverse.Combat
{
    public static class HeroCombatRules
    {
        public static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        // Basic attacks measure solid objectives at their surface. Hero/minion duels keep
        // the authored centre-to-centre range. Reuse this at windup and impact, too.
        public static float BasicDistance(Vector3 origin,Health target)
        {
            if(target==null)return float.MaxValue;
            if(target.GetComponent<HeroIdentity>()!=null||target.GetComponent<LaneMinion>()!=null)
                return Distance(origin,target.transform.position);
            Collider body=target.GetComponent<Collider>();
            if(body==null||!body.enabled||body.isTrigger)return Distance(origin,target.transform.position);
            Vector3 point=origin;point.y=body.bounds.center.y;
            return Distance(origin,body.ClosestPoint(point));
        }
        public static bool Enemy(GameObject actor, Health target)
        {
            if (target == null || target.IsDead || !target.isActiveAndEnabled || target.gameObject == actor) return false;
            TeamMember member = actor.GetComponent<TeamMember>(); if (member == null) return false;
            RealityCore core = target.GetComponent<RealityCore>();
            if (core != null) return core.Team != member.Team && core.IsVulnerable;
            DimensionPlanet planet = target.GetComponent<DimensionPlanet>(); if (planet != null) return planet.Available;
            LaneMinion minion = target.GetComponent<LaneMinion>(); if (minion != null) return minion.Team != member.Team;
            LaneTower tower = target.GetComponent<LaneTower>(); if (tower != null) return tower.Team != member.Team;
            TeamMember other = target.GetComponent<TeamMember>(); return other != null && other.Team != member.Team;
        }
        public static float Damage(GameObject actor, Health target, float amount)
        {
            if (!Enemy(actor, target)) return 0;
            PlanetBuffReceiver buff = actor.GetComponent<PlanetBuffReceiver>();
            if (target.GetComponent<LaneMinion>() != null && buff != null) amount *= buff.MinionDamageMultiplier;
            float before = target.Current;
            target.TakeDamage(amount, actor);
            return Mathf.Max(0, before - target.Current);
        }
        public static float Interval(GameObject actor, float baseline)
        { return baseline * (actor.GetComponent<PlanetBuffReceiver>()?.CooldownMultiplier ?? 1f); }
        public static void BasicHit(GameObject actor, Health target, float amount)
        {
            HeroCombatProfile profile=actor.GetComponent<HeroCombatProfile>();
            HeroAttackDelivery.For(actor).Basic(target,amount,profile!=null?profile.AttackRange:Distance(actor.transform.position,target.transform.position)+.05f);
        }
        internal static void ResolveBasicHit(GameObject actor,Health target,float amount)
        {
            float before=target.Current+target.Shield;
            float dealt = Damage(actor, target, amount);
            if(target.Current+target.Shield<before){Alterverse.Audio.HeroCombatAudio.Play(actor,0,true,target.transform.position);Alterverse.Visuals.HeroSkillVfx.Impact(actor,target,-1);}
            HeroIdentity identity = actor.GetComponent<HeroIdentity>();
            Health hp = actor.GetComponent<Health>();
            if (identity != null && identity.Hero == HeroId.Rook && hp != null && hp.Normalized < .6f && target.GetComponent<HeroIdentity>() != null)
                hp.Heal(dealt * .12f,false);

        }
    }
}


