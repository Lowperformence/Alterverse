using Alterverse.Characters;
using Alterverse.Match;
using UnityEngine;
namespace Alterverse.Combat
{
    public static class HeroCombatRules
    {
        // Combined with the 1.08 hero-health pass this raises typical hero-vs-hero TTK by roughly
        // 17% while leaving planet/minion clear speed untouched. Movement stays responsive.
        public const float HeroVsHeroDamageMultiplier=.92f;
        public static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

        // Heroes and minions intentionally use centre-to-centre combat ranges. Large objectives
        // use their physical surface instead, including trigger colliders such as dimension planets.
        // This prevents short-range heroes from having to walk inside a planet/objective model
        // before an otherwise valid melee attack or targeted ability can connect.
        public static float TargetDistance(Vector3 origin,Health target)
        {
            if(target==null)return float.MaxValue;
            if(target.GetComponent<HeroIdentity>()!=null||target.GetComponent<LaneMinion>()!=null)
                return Distance(origin,target.transform.position);
            Collider body=target.GetComponent<Collider>();
            if(body==null||!body.enabled)body=target.GetComponentInChildren<Collider>();
            if(body==null||!body.enabled)return Distance(origin,target.transform.position);
            Vector3 point=origin;point.y=body.bounds.center.y;
            return Distance(origin,body.ClosestPoint(point));
        }

        public static float BasicDistance(Vector3 origin,Health target)=>TargetDistance(origin,target);

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
            // PvP only: slightly longer hero duels create room for counterplay and a second decision
            // without slowing movement/rotations or making planets and lane clearing take longer.
            if(actor.GetComponent<HeroIdentity>()!=null&&target.GetComponent<HeroIdentity>()!=null)
                amount*=HeroVsHeroDamageMultiplier;
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
            HeroAttackDelivery.For(actor).Basic(target,amount,profile!=null?profile.AttackRange:TargetDistance(actor.transform.position,target)+.05f);
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
