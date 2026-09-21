using UnityEngine;
namespace Alterverse.Characters
{
    public readonly struct HeroCombatStats
    {
        public readonly float AttackRange,AttackInterval,MoveSpeed,Control,Sustain;
        public HeroCombatStats(float range,float interval,float speed,float control,float sustain)
        {AttackRange=range;AttackInterval=interval;MoveSpeed=speed;Control=control;Sustain=sustain;}
    }

    public sealed class HeroCombatProfile : MonoBehaviour
    {
        public HeroId Hero { get; private set; }
        public float Health { get; private set; }
        public float Attack { get; private set; }
        public float AttackRange { get; private set; }
        public float AttackInterval { get; private set; }
        public float MoveSpeed { get; private set; }
        public const float GlobalMoveSpeedMultiplier = .855f;
        public float AbilityPower => 1f; // AbilitySpec.Power is the actual baseline, no hidden role multiplier.
        public float Control { get; private set; }
        public float Sustain { get; private set; }

        public static HeroCombatStats StatsFor(HeroId id)=>id switch
        {
            HeroId.Veyr=>new HeroCombatStats(2.25f,.85f,7.1f,.2f,.1f),
            HeroId.Kaelon=>new HeroCombatStats(2.3f,1.05f,5.6f,1f,.8f),
            HeroId.Nyra=>new HeroCombatStats(6.8f,.62f,6.2f,.3f,.05f),
            HeroId.Elyra=>new HeroCombatStats(6.1f,1.10f,5.9f,.85f,.05f),
            HeroId.Mira=>new HeroCombatStats(6.0f,1f,6.1f,.5f,1f),
            _=>new HeroCombatStats(2.5f,.9f,6.15f,.45f,.7f)
        };

        public void Configure(HeroId id)
        {
            Hero=id;Health=HeroIdentity.BaseHealth(id);Attack=HeroIdentity.AttackDamage(id);
            HeroCombatStats stats=StatsFor(id);
            AttackRange=stats.AttackRange;AttackInterval=stats.AttackInterval;
            // Global pacing pass: every hero, player and bot, now runs another 5% slower than the previous 0.90 multiplier while preserving
            // the authored speed differences between roles.
            MoveSpeed=stats.MoveSpeed*GlobalMoveSpeedMultiplier;
            Control=stats.Control;Sustain=stats.Sustain;
        }
    }
}
