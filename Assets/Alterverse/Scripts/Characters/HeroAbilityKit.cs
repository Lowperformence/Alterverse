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

    public readonly struct HeroAbilityLoadout
    {
        public readonly AbilitySpec Passive,Q,W,E,Ultimate;
        public HeroAbilityLoadout(AbilitySpec passive,AbilitySpec q,AbilitySpec w,AbilitySpec e,AbilitySpec ultimate)
        {Passive=passive;Q=q;W=w;E=e;Ultimate=ultimate;}
        public AbilitySpec Spec(int slot)=>slot==0?Q:slot==1?W:slot==2?E:Ultimate;
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

        /// <summary>
        /// Single source of truth for hero ability numbers. Runtime combat and menu presentation
        /// both read this catalog so displayed cooldown/range/power cannot silently drift.
        /// </summary>
        public static HeroAbilityLoadout LoadoutFor(HeroId id)
        {
            return id switch
            {
                HeroId.Veyr=>new HeroAbilityLoadout(
                    new("Fractured edge",0,0,0),
                    new("Null Cut",7,38,3.1f),
                    new("Rift Scar",10,28,3.3f),
                    new("Gapfold",12,0,4.2f),
                    new("Vacant Echo",48,58,3.5f,0,.4f)),
                HeroId.Kaelon=>new HeroAbilityLoadout(
                    new("Frontline",0,0,0),
                    new("Anchor Strike",9,24,3.4f),
                    new("Lumen Wall",14,75,0),
                    new("Gravity Anchor",12,20,5f,2.6f,.5f),
                    new("Kest Citadel",55,38,0,3.8f,.65f)),
                HeroId.Nyra=>new HeroAbilityLoadout(
                    new("Calibrated edge",0,0,0),
                    new("Prism Shot",7,32,7.8f),
                    new("Vector Shift",14,0,3f),
                    new("Light Snare",12,20,6.8f),
                    new("Sightline Break",50,64,7.8f)),
                HeroId.Elyra=>new HeroAbilityLoadout(
                    new("Field geometry",0,0,0),
                    new("Orbit Fracture",8,45,7.3f,2f,.45f),
                    new("Invert Field",12,26,6.5f,2.6f,.55f),
                    new("Phase Fold",16,0,1.8f),
                    new("Impossible Center",58,72,7f,3.5f,.85f)),
                HeroId.Mira=>new HeroAbilityLoadout(
                    new("Living memory",0,0,0),
                    new("Resonant Seed",7,18,6.5f),
                    new("Growth Veil",9,42,6.5f),
                    new("Venn Shelter",13,50,6.5f),
                    new("Wildsurge",58,58,0,5f,.4f)),
                _=>new HeroAbilityLoadout(
                    new("Scrap recovery",0,.12f,0),
                    new("Wreck Swing",7,34,2.9f),
                    new("Armor Grip",13,30,0),
                    new("Wreckstorm",10,28,0,2.8f,.35f),
                    new("Orun Overdrive",52,48,0,3.2f,.6f))
            };
        }

        public void Configure(HeroId id)
        {
            Hero=id;
            HeroAbilityLoadout loadout=LoadoutFor(id);
            Passive=loadout.Passive;Q=loadout.Q;W=loadout.W;E=loadout.E;Ultimate=loadout.Ultimate;
        }
    }
}
