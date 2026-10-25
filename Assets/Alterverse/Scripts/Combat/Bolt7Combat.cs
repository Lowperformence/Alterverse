using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.Combat
{
    // The effect scheduler never changes combat state. Every hit is resolved here on authority.
    public static class Bolt7Combat
    {
        private static readonly RaycastHit[] cover=new RaycastHit[32];
        public static float GrenadeDamage(float distance)
            =>Mathf.Lerp(Bolt7Balance.GrenadeDamage,Bolt7Balance.GrenadeEdgeDamage,
                Mathf.InverseLerp(Bolt7Balance.GrenadeCore,Bolt7Balance.GrenadeRadius,distance));
        public static bool Visible(GameObject actor,Health target,Vector3 origin)
        {
            Vector3 from=origin+Vector3.up*.45f,to=target.transform.position+Vector3.up*.45f;
            Vector3 ray=to-from;if(ray.sqrMagnitude<.0001f)return true;
            int count=Physics.RaycastNonAlloc(from,ray.normalized,cover,ray.magnitude,
                GameplayCollision.SightMask,QueryTriggerInteraction.Ignore);
            if(count==cover.Length)return false;
            for(int i=0;i<count;i++)
            {
                Health body=cover[i].collider.GetComponentInParent<Health>();
                if(body!=target&&(body==null||body.gameObject!=actor))return false;
            }
            return true;
        }
        private static bool CanResolve(GameObject actor)=>actor!=null&&CoopAuthority.CanSimulateHostGameplay&&
            actor.GetComponent<HeroIdentity>()?.Hero==HeroId.Bolt7&&
            actor.GetComponent<Health>() is Health hp&&!hp.IsDead&&HeroCombatStatus.CanAct(actor);
        public static int Grenade(GameObject actor,Vector3 center)
        {
            if(!CanResolve(actor))return 0;int hits=0;
            for(int i=Health.Active.Count-1;i>=0;i--)
            {
                Health target=Health.Active[i];
                if(!HeroCombatRules.Enemy(actor,target))continue;
                float d=HeroCombatRules.Distance(center,target.transform.position);
                if(d>Bolt7Balance.GrenadeRadius||!Visible(actor,target,center))continue;
                float before=target.Current+target.Shield;
                HeroCombatRules.Damage(actor,target,GrenadeDamage(d));
                if(target.Current+target.Shield<before)hits++;
            }
            return hits;
        }
        public static int Nullfield(GameObject actor,Vector3 center)
        {
            if(!CanResolve(actor))return 0;int hits=0;
            for(int i=Health.Active.Count-1;i>=0;i--)
            {
                Health target=Health.Active[i];
                if(!HeroCombatRules.Enemy(actor,target)||HeroCombatRules.Distance(center,target.transform.position)>Bolt7Balance.UltimateRadius||!Visible(actor,target,center))continue;
                float removed=target.RemoveAllShields(actor);
                if(removed>0){CoopGameplayCommands.RecordAuthoritativeDamage(actor,removed);hits++;}
            }
            return hits;
        }
        public static int Impulse(GameObject actor,Vector3 center)
        {
            if(!CanResolve(actor))return 0;int hits=0;
            for(int i=Health.Active.Count-1;i>=0;i--)
            {
                Health target=Health.Active[i];
                if(!HeroCombatRules.Enemy(actor,target)||HeroCombatRules.Distance(center,target.transform.position)>Bolt7Balance.ImpulseRadius||!Visible(actor,target,center))continue;
                float before=target.Current+target.Shield;
                HeroCombatRules.Damage(actor,target,Bolt7Balance.ImpulseDamage);
                if(target.Current+target.Shield>=before)continue;
                hits++;
                Vector3 outward=target.transform.position-center;outward.y=0;
                if(outward.sqrMagnitude<.0001f)outward=actor.transform.forward;
                target.GetComponent<HeroLocomotion>()?.BeginForcedMove(target.transform.position+
                    AimGeometry.Direction(outward)*Bolt7Balance.ImpulseKnockback,.14f);
            }
            return hits;
        }
        public static int Ram(GameObject actor,Vector3 direction)
        {
            if(!CanResolve(actor))return 0;int hits=0;
            for(int i=Health.Active.Count-1;i>=0;i--)
            {
                Health target=Health.Active[i];
                if(!HeroCombatRules.Enemy(actor,target)||!AimGeometry.Cone(actor.transform.position,direction,
                    target.transform.position,Bolt7Balance.RamReach,Bolt7Balance.RamAngle)||!Visible(actor,target,actor.transform.position))continue;
                float before=target.Current+target.Shield;
                HeroCombatRules.Damage(actor,target,Bolt7Balance.RamDamage);
                if(target.Current+target.Shield>=before)continue;
                hits++;
                target.GetComponent<HeroLocomotion>()?.BeginForcedMove(target.transform.position+
                    AimGeometry.Direction(direction)*Bolt7Balance.RamKnockback,.12f);
            }
            return hits;
        }
    }
}
