using System;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

partial class TimmyMovementRegression
{
    static void PhaseFoldCases()
    {
        foreach(int fps in new[]{30,60,120})foreach(Vector3 direction in new[]{Vector3.forward,Vector3.right,-Vector3.forward})
        {
            Reset();var actor=Hero(HeroId.Elyra,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*8);
            var abilities=actor.GetComponent<PlayerHeroAbilities>();var health=actor.GetComponent<Health>();
            Near(abilities.PreviewRange(2),2.3f,"Phase Fold preview "+fps);
            Near(actor.GetComponent<HeroMotor>().MoveSpeed,5.9f*.93f*HeroCombatProfile.GlobalMoveSpeedMultiplier,"Ione seven percent speed reduction");
            Vector3 start=actor.transform.position;
            Check(abilities.CastAimed(2,new AimPose(direction,start+direction*99)),"Phase Fold accepts directional dash "+fps);
            Near(health.TemporaryShield,20,"Phase Fold grants twenty shield once");
            Near(abilities.RemainingFor(2),16,"Phase Fold cooldown");
            Check(abilities.Describe(2).Contains("20 shield")&&abilities.Describe(2).Contains("2.3m"),"HUD describes range and shield");
            Check(Alterverse.UI.HeroAttackCatalog.For(HeroId.Elyra)[3].Stats.Contains("20 SHIELD"),"menu presents shield as protection");
            health.TakeDamage(7);Near(health.Current,1000,"Phase Fold shield absorbs damage");Near(health.TemporaryShield,13,"only absorbed damage consumes shield");
            Check(!abilities.CastAimed(2,new AimPose(direction,start+direction)),"cooldown blocks repeat Phase Fold");
            Near(health.TemporaryShield,13,"rejected cast cannot refill shield");
            Run(.3f,fps,actor,enemy);
            Near(Vector3.Dot(actor.transform.position-start,direction),2.3f,"production Phase Fold travels exactly 2.3m "+fps);
            Near(Total(enemy),1000,"Phase Fold causes no enemy damage");
        }
        Reset();var stunned=Hero(HeroId.Elyra,0,Vector3.zero);
        stunned.GetComponent<HeroCombatStatus>().Stun(.5f);
        Check(!stunned.GetComponent<PlayerHeroAbilities>().CastE(),"stun blocks Phase Fold");
        Near(stunned.GetComponent<Health>().TemporaryShield,0,"stunned Phase Fold grants no shield");
        Reset();var client=Hero(HeroId.Elyra,0,Vector3.zero);
        CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;
        Check(!client.GetComponent<HeroAbilityExecution>().TryCastAimed(2,new AimPose(Vector3.forward,Vector3.forward*2.3f)),"client cannot grant authoritative Phase Fold shield");
        Near(client.GetComponent<Health>().TemporaryShield,0,"client never duplicates the host shield");
        Reset();Near(HeroCombatProfile.StatsFor(HeroId.Kaelon).MoveSpeed,6f,"Auren speed preserved");
        Near(HeroCombatProfile.StatsFor(HeroId.Nyra).MoveSpeed,5.58f,"Sivras speed preserved");
    }

    static float BasicDamage(HeroId hero)=>HeroIdentity.AttackDamage(hero)*(hero==HeroId.Rook?1.05f:1f);
    static void BasicMovementCases()
    {
        foreach(int fps in new[]{30,60,120})foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
        {
            foreach(Vector3 offset in new[]{Vector3.forward,Vector3.right,-Vector3.forward})
            {
                Reset();var actor=Hero(hero,0,Vector3.zero);
                float range=actor.GetComponent<HeroCombatProfile>().AttackRange;
                var target=Hero(HeroId.Kaelon,1,offset*(range-.02f));
                var basic=actor.GetComponent<BasicAttack>();
                Check(basic.TryAttack(),hero+" edge-range acquisition "+fps+" "+offset);
                Vector3 start=actor.transform.position;bool facedAtRelease=false;
                float releaseAt=Time.time+HeroAttackDelivery.Windup(hero);
                for(int frame=0;frame<Mathf.CeilToInt(.55f*fps);frame++)
                {
                    float frameTime=Time.time;
                    Step(1f/fps,actor,new Vector2(-offset.x,-offset.z),target,Vector2.zero);
                    if(Time.time>=releaseAt&&frameTime<releaseAt)
                        facedAtRelease=Vector3.Angle(actor.transform.forward,target.transform.position-actor.transform.position)<.05f;
                }
                Near(Total(target),1000-BasicDamage(hero),hero+" retreat connects exactly once "+fps+" "+offset);
                Check(Vector3.Dot(actor.transform.position-start,-offset)>.5f,hero+" still walks away during attack "+fps);
                Check(facedAtRelease,hero+" strike/release faces the live target "+fps+" "+offset);
                float settledHealth=Total(target);Run(.2f,fps,actor,target,new Vector2(0,-1));
                Near(Total(target),settledHealth,hero+" held-facing heartbeat adds no duplicate hit "+fps);
            }

            // Both production motors backpedal with the button held. The enemy is always
            // eligible, and the attacker must face it throughout the cooldown as well.
            Reset();var a=Hero(hero,0,Vector3.zero);var b=Hero(hero,1,Vector3.forward*1.5f);
            var attack=a.GetComponent<BasicAttack>();int accepted=0;
            for(int frame=0;frame<Mathf.CeilToInt((attack.AttackCooldown*2+.55f)*fps);frame++)
            {
                if(attack.TryAttack())accepted++;
                Step(1f/fps,a,new Vector2(0,-1),b,new Vector2(0,-1));
                Check(Vector3.Angle(a.transform.forward,b.transform.position-a.transform.position)<.05f,
                    hero+" sustained hold faces target every movement frame "+fps+" frame="+frame);
            }
            Check(accepted==3,hero+" cooldown permits three held attacks "+fps);
            Near(Total(b),1000-BasicDamage(hero)*3,hero+" three backpedaling hits "+fps);

            // Change direction after launch/contact; the accepted target is kept even if
            // another enemy is closer to the old aim point or crosses the projectile.
            Reset();a=Hero(hero,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*(HeroAttackDelivery.IsRanged(hero)?4f:1.4f));
            var decoy=Hero(HeroId.Kaelon,1,Vector3.forward*.7f);
            Check(a.GetComponent<BasicAttack>().TryAttackNetworkAuthoritative(b.GetComponent<Health>()),hero+" explicitly accepted target "+fps);
            Run(HeroAttackDelivery.Windup(hero)+1f/fps,fps,a,b,new Vector2(0,-1));
            b.transform.position+=Vector3.right*2;
            Run(.5f,fps,a,b,new Vector2(0,-1),new Vector2(-1,0));
            Near(Total(b),1000-BasicDamage(hero),hero+" target keeps its accepted hit after turning "+fps);
            Near(Total(decoy),1000,hero+" another enemy cannot steal targeted basic "+fps);

            Reset();a=Hero(hero,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*(HeroCombatProfile.StatsFor(hero).AttackRange+.01f));
            Check(!a.GetComponent<BasicAttack>().TryAttackNetworkAuthoritative(b.GetComponent<Health>()),hero+" outside range rejected "+fps);
            Near(a.GetComponent<BasicAttack>().RemainingCooldown,0,hero+" invalid range retains cooldown "+fps);
            b.transform.position=Vector3.up+Vector3.forward; b.GetComponent<TeamMember>().Initialize(0);
            Check(!a.GetComponent<BasicAttack>().TryAttackNetworkAuthoritative(b.GetComponent<Health>()),hero+" ally rejected "+fps);

            // Keyboard hold uses the same cooldown-bound repetition as touch/mouse hold.
            Reset();a=Hero(hero,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*1.3f);
            Keyboard.current=new Keyboard();Keyboard.current.spaceKey.isPressed=true;
            attack=a.GetComponent<BasicAttack>();
            for(int frame=0;frame<Mathf.CeilToInt((attack.AttackCooldown*2+.55f)*fps);frame++)
            {Call(attack,"Update");Step(1f/fps,a,Vector2.zero,b,Vector2.zero);}
            Near(Total(b),1000-BasicDamage(hero)*3,hero+" space-key hold repeats "+fps);
        }
    }
}
