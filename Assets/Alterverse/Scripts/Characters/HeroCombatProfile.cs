using UnityEngine;
namespace Alterverse.Characters
{
    public sealed class HeroCombatProfile : MonoBehaviour
    {
        public HeroId Hero { get; private set; }
        public float Health { get; private set; }
        public float Attack { get; private set; }
        public float AttackRange { get; private set; }
        public float AttackInterval { get; private set; }
        public float MoveSpeed { get; private set; }
        public float AbilityPower => 1f; // AbilitySpec.Power is the actual baseline, no hidden role multiplier.
        public float Control { get; private set; }
        public float Sustain { get; private set; }
        public void Configure(HeroId id)
        {
            Hero = id;
            Health = HeroIdentity.BaseHealth(id); Attack = HeroIdentity.AttackDamage(id);
            switch (id)
            {
                case HeroId.Veyr: Set(2.25f, .85f, 7.1f, .2f, .1f); break;
                case HeroId.Kaelon: Set(2.3f, 1.05f, 5.6f, 1f, .8f); break;
                case HeroId.Nyra: Set(6.8f, .62f, 6.2f, .3f, .05f); break;
                case HeroId.Elyra: Set(6.1f, 1.10f, 5.9f, .85f, .05f); break;
                case HeroId.Mira: Set(6.0f, 1f, 6.1f, .5f, 1f); break;
                default: Set(2.5f, .9f, 6.15f, .45f, .7f); break;
            }
        }
        private void Set(float range, float interval, float speed, float control, float sustain)
        { AttackRange = range; AttackInterval = interval; MoveSpeed = speed; Control = control; Sustain = sustain; }
    }
}
