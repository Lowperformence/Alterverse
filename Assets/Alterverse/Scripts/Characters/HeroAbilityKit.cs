using UnityEngine;
namespace Alterverse.Characters
{
    public readonly struct AbilitySpec
    {
        public readonly string Name;
        public readonly float Cooldown, Power, Range, Radius, Windup;
        public AbilitySpec(string name, float cooldown, float power, float range, float radius = 0f, float windup = 0f)
        { Name = name; Cooldown = cooldown; Power = power; Range = range; Radius = radius; Windup = windup; }
    }
    public sealed class HeroAbilityKit : MonoBehaviour
    {
        public const float UltimateInitialLock = 45f;
        public HeroId Hero { get; private set; }
        public AbilitySpec Passive { get; private set; }
        public AbilitySpec Q { get; private set; }
        public AbilitySpec W { get; private set; }
        public AbilitySpec E { get; private set; }
        public AbilitySpec Ultimate { get; private set; }
        public AbilitySpec Spec(int slot) => slot == 0 ? Q : slot == 1 ? W : slot == 2 ? E : Ultimate;
        public void Configure(HeroId id)
        {
            Hero = id;
            switch (id)
            {
                case HeroId.Veyr:
                    Passive = new("Fractured edge", 0, 0, 0);
                    Q = new("Null Cut", 7, 38, 3.1f); W = new("Rift Scar", 10, 28, 3.3f);
                    E = new("Gapfold", 12, 0, 4.2f); Ultimate = new("Vacant Echo", 48, 58, 3.5f, 0, .4f); break;
                case HeroId.Kaelon:
                    Passive = new("Frontline", 0, 0, 0);
                    Q = new("Anchor Strike", 9, 24, 3.4f); W = new("Lumen Wall", 14, 75, 0);
                    E = new("Gravity Anchor", 12, 20, 5f, 2.6f, .5f); Ultimate = new("Kest Citadel", 55, 38, 0, 3.8f, .65f); break;
                case HeroId.Nyra:
                    Passive = new("Calibrated edge", 0, 0, 0);
                    Q = new("Prism Shot", 7, 32, 7.8f); W = new("Vector Shift", 14, 0, 3f);
                    E = new("Light Snare", 12, 20, 6.8f); Ultimate = new("Sightline Break", 50, 64, 7.8f); break;
                case HeroId.Elyra:
                    Passive = new("Field geometry", 0, 0, 0);
                    Q = new("Orbit Fracture", 8, 45, 7.3f, 2f, .45f); W = new("Invert Field", 12, 26, 6.5f, 2.6f, .55f);
                    E = new("Phase Fold", 16, 0, 1.8f); Ultimate = new("Impossible Center", 58, 72, 7f, 3.5f, .85f); break;
                case HeroId.Mira:
                    Passive = new("Living memory", 0, 0, 0);
                    Q = new("Resonant Seed", 7, 18, 6.5f); W = new("Growth Veil", 9, 42, 6.5f);
                    E = new("Venn Shelter", 13, 50, 6.5f); Ultimate = new("Wildsurge", 58, 58, 0, 5f, .4f); break;
                default:
                    Passive = new("Scrap recovery", 0, .12f, 0);
                    Q = new("Wreck Swing", 7, 34, 2.9f); W = new("Armor Grip", 13, 30, 0);
                    E = new("Wreckstorm", 10, 28, 0, 2.8f, .35f); Ultimate = new("Orun Overdrive", 52, 48, 0, 3.2f, .6f); break;
            }
        }
    }
}
