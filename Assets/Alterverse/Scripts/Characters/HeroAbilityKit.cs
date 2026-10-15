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
        public const float PhaseFoldShield = 20f;
        public const float PhaseFoldShieldSeconds = 2.25f;
        // Supplemental passive: Auren restores health each second in addition to Frontline.
        public const float AurenPassiveRegenPerSecond = 2f;
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
                HeroId.VoidKnight=>new HeroAbilityLoadout(
                    new("Void Resonance",0,30,0),
                    new("Rift Sweep",7,24,3.4f,0,.28f),
                    new("Null Guard",12,0,0),
                    new("Rift Lunge",9,16,4f,0,.18f),
                    new("Eventide Cleave",40,42,3.8f,0,.65f)),
                HeroId.Timmy=>new HeroAbilityLoadout(
                    new("Light Paws",0,0,0),
                    new("Claw Swipe",TimmyBalance.ClawCooldown,TimmyBalance.ClawDamage,TimmyBalance.ClawRange),
                    new("Stardust",TimmyBalance.DustCooldown,TimmyBalance.DustDamage,TimmyBalance.DustRange,TimmyBalance.DustRadius,TimmyBalance.DustWindup),
                    new("Jet Pounce",TimmyBalance.JetCooldown,0,TimmyBalance.JetRange),
                    new("Scratch",TimmyBalance.ScratchCooldown,TimmyBalance.ScratchDamage,TimmyBalance.ScratchRange,0,TimmyBalance.ScratchWindup)),
                HeroId.Veyr=>new HeroAbilityLoadout(
                    new("Fractured edge",0,0,0),
                    new("Null Cut",7,38,3.1f),
                    new("Rift Scar",10,35,3.3f),
                    // Zhaev depends on Gapfold to enter ranged matchups at all. Eleven seconds keeps
                    // a real punish window while avoiding long periods where a 2.25m assassin has
                    // no viable way to cross 6-7m ranged pressure.
                    new("Gapfold",11,0,4.2f),
                    new("Vacant Echo",48,58,3.5f,0,.32f)),
                HeroId.Kaelon=>new HeroAbilityLoadout(
                    new("Frontline",0,0,0),
                    new("Anchor Strike",9,24,3.4f),
                    new("Lumen Wall",14,50,0),
                    new("Gravity Anchor",12,20,5f,2.6f,.45f),
                    new("Kest Citadel",55,38,0,3.8f,.55f)),
                HeroId.Nyra=>new HeroAbilityLoadout(
                    new("Calibrated edge",0,0,0),
                    new("Prism Shot",7,32,7.8f),
                    new("Vector Shift",14,0,3f),
                    new("Light Snare",12,20,6.8f),
                    new("Sightline Break",50,64,7.8f)),
                HeroId.Elyra=>new HeroAbilityLoadout(
                    new("Field geometry",0,0,0),
                    new("Orbit Fracture",8,55,7.3f,2f,.38f),
                    new("Invert Field",12,26,6.5f,2.6f,.46f),
                    new("Phase Fold",16,0,2.3f),
                    new("Impossible Center",58,72,7f,2.5f,.68f)),
                HeroId.Mira=>new HeroAbilityLoadout(
                    new("Living memory",0,0,0),
                    new("Resonant Seed",7,30,6.5f),
                    new("Growth Veil",9,42*1.08f,6.5f),
                    new("Venn Shelter",13,50,6.5f),
                    new("Wildsurge",58,58*1.08f,0,5f,.4f)),
                HeroId.Rook=>new HeroAbilityLoadout(
                    new("Scrap recovery",0,.12f,0),
                    new("Wreck Swing",7,39,2.9f),
                    new("Armor Grip",13,60,0),
                    new("Wreckstorm",10,35,0,2.8f,.30f),
                    new("Orun Overdrive",52,48,0,3.2f,.50f)),
                HeroId.Traveler=>new HeroAbilityLoadout(
                    new("Between seconds",0,0,0),
                    new("Temporal Beam",8,35,7.2f),
                    new("Rift Step",13,0,3.6f),
                    new("Chrono Snare",12,35,6.5f,2.2f,.38f),
                    new("Paradox Collapse",56,60,6.8f,2.5f,.68f)),
                HeroId.Nerok=>new HeroAbilityLoadout(
                    new("Salvage Tide",0,12,0),
                    new("Anchor Recall",12,28,6.2f,0,.30f),
                    new("Pressure Hull",14,45,0),
                    new("Breaker Wave",9,26,0,2.8f,.30f),
                    new("Abyssal Maelstrom",58,60,6.0f,2.3f,.65f)),
                _=>new HeroAbilityLoadout(
                    new("Unknown",0,0,0),new("Unknown",8,20,5f),new("Unknown",12,0,0),new("Unknown",12,20,5f),new("Unknown",55,50,5f))
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
