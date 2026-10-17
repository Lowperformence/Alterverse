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
            // Melee movement is role-specific rather than a blanket buff. Zhaev already owns a long
            // gap-close, Auren has ranged control and tank durability, while Brakk has no true dash
            // and therefore needs enough foot speed to eventually punish a perfectly kiting carry.
            // Zhaev receives an additional 5% hero-specific movement-speed increase.
            HeroId.VoidKnight=>new HeroCombatStats(VoidKnightBalance.BasicRange,VoidKnightBalance.BasicInterval,6.7f,.5f,.35f),
            HeroId.Veyr=>new HeroCombatStats(2.25f,.85f,7.1f*1.05f,.2f,.1f),
            HeroId.Timmy=>new HeroCombatStats(TimmyBalance.BasicRange,TimmyBalance.BasicInterval,TimmyBalance.RawMoveSpeed,.8f,0f),
            HeroId.Kaelon=>new HeroCombatStats(2.3f,1.05f,6.0f,1f,.8f),
            HeroId.Nyra=>new HeroCombatStats(6.8f,.62f,5.58f,.3f,.05f),
            HeroId.Elyra=>new HeroCombatStats(6.1f,1.10f,5.9f*.93f,.85f,.05f),
            HeroId.Mira=>new HeroCombatStats(6.0f,1f,6.1f,.5f,1f),
            HeroId.Rook=>new HeroCombatStats(2.5f,.9f,6.9f,.45f,.7f),
            HeroId.Traveler=>new HeroCombatStats(6.2f,.92f,5.67f,.62f,.10f),
            HeroId.Nerok=>new HeroCombatStats(2.6f,.98f,6.5f,.85f,.3f),
            _=>new HeroCombatStats(5.5f,1f,6f,.4f,.1f)
        };

        public void Configure(HeroId id)
        {
            Hero=id;Health=HeroIdentity.BaseHealth(id);Attack=HeroIdentity.AttackDamage(id);
            HeroCombatStats stats=StatsFor(id);
            AttackRange=stats.AttackRange;AttackInterval=stats.AttackInterval;
            // Keep the existing global pace. Slowing everyone further would lengthen rotations and,
            // more importantly, increase the time melee heroes spend exposed before reaching ranged
            // opponents. Duel duration is tuned through durability instead of sluggish movement.
            MoveSpeed=stats.MoveSpeed*GlobalMoveSpeedMultiplier;
            Control=stats.Control;Sustain=stats.Sustain;
        }
    }
}
