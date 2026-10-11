using Alterverse.Characters;
using Alterverse.Combat;

namespace Alterverse.UI
{
    public readonly struct HeroAttackCard
    {
        public readonly string Key,Name,Stats,Effect;
        public HeroAttackCard(string key,string name,string stats,string effect)
        {Key=key;Name=name;Stats=stats;Effect=effect;}
    }

    /// <summary>
    /// Player-facing combat sheet for hero selection. Numeric values are sourced from the same
    /// HeroAbilityKit/HeroCombatProfile data used by runtime combat.
    /// </summary>
    public static class HeroAttackCatalog
    {
        private static readonly HeroAttackCard[][] cache=BuildAll();

        public static HeroAttackCard[] For(HeroId hero)=>cache[(int)hero];

        private static HeroAttackCard[][] BuildAll()
        {
            HeroAttackCard[][] all=new HeroAttackCard[HeroIdentity.CatalogHeroCount][];
            for(int i=0;i<all.Length;i++)all[i]=Build((HeroId)i);
            return all;
        }

        private static HeroAttackCard[] Build(HeroId hero)
        {
            HeroAbilityLoadout kit=HeroAbilityKit.LoadoutFor(hero);
            HeroCombatStats stats=HeroCombatProfile.StatsFor(hero);
            return new[]
            {
                new HeroAttackCard("BASIC",hero==HeroId.Timmy?"Claw":"Basic Attack",
                    $"{HeroIdentity.AttackDamage(hero):0} DMG  •  RANGE {stats.AttackRange:0.##}  •  {stats.AttackInterval:0.##}s / SHOT  •  WINDUP {HeroAttackDelivery.Windup(hero):0.##}s",
                    BasicEffect(hero)),
                Ability(hero,"SKILL 1",0,kit.Q),
                Ability(hero,"SKILL 2",1,kit.W),
                Ability(hero,"SKILL 3",2,kit.E),
                Ability(hero,"ULTIMATE",3,kit.Ultimate)
            };
        }

        private static HeroAttackCard Ability(HeroId hero,string key,int slot,AbilitySpec spec)
        {
            string primary=Primary(hero,slot,spec);
            string spatial=spec.Range>0?$"RANGE {spec.Range:0.##}":spec.Radius>0?"SELF":"SELF";
            if(spec.Radius>0)spatial+= $"  •  AOE {spec.Radius:0.##}";
            string timing=$"CD {spec.Cooldown:0.#}s";
            float windup=HeroAttackDelivery.AbilityWindup(hero,slot,spec);
            if(windup>0)timing+= $"  •  WINDUP {windup:0.##}s";
            if(slot==3)timing+= $"  •  START LOCK {HeroAbilityKit.UltimateInitialLock:0}s";
            string stats=string.IsNullOrEmpty(primary)?$"{spatial}  •  {timing}":$"{primary}  •  {spatial}  •  {timing}";
            return new HeroAttackCard(key,spec.Name,stats,Effect(hero,slot));
        }

        private static string Primary(HeroId hero,int slot,AbilitySpec spec)
        {
            if(hero==HeroId.Kaelon&&slot==1)return $"{spec.Power:0} SHIELD / 4s";
            if(hero==HeroId.Mira&&slot==1)return $"{spec.Power:0} HEAL";
            if(hero==HeroId.Mira&&slot==2)return $"{spec.Power:0} SHIELD / 4s";
            if(hero==HeroId.Mira&&slot==3)return $"{spec.Power:0} HEAL + 25 SHIELD";
            if(hero==HeroId.Nerok&&slot==1)return $"{spec.Power:0} SHIELD / 3s";
            if(hero==HeroId.Rook&&slot==1)return $"{spec.Power:0} HEAL + 20 SHIELD";
            if(spec.Power<=0)return "";
            return $"{spec.Power:0} DMG";
        }

        private static string BasicEffect(HeroId hero)=>hero switch
        {
            HeroId.Veyr=>"Fast melee pressure for finishing isolated targets.",
            HeroId.Timmy=>$"Short-range claw strike. Light Paws grants {(TimmyBalance.PassiveSpeed-1f)*100:0}% speed for {TimmyBalance.PassiveSeconds:0.#}s after a damaging skill hit, at most once every {TimmyBalance.PassiveCooldown:0.#}s.",
            HeroId.Kaelon=>"Heavy frontline strike with short melee reach.",
            HeroId.Nyra=>"Rapid long-range rifle fire.",
            HeroId.Elyra=>"Ranged dimensional basic attack.",
            HeroId.Mira=>"Ranged support basic attack.",
            HeroId.Rook=>"Melee strike. Below 60% HP, hero hits restore 12% of damage dealt.",
            HeroId.Traveler=>"Flux Bolt: Planet Flux fires compressed chronolight with a clean temporal trail.",
            HeroId.Nerok=>"Heavy anchor strike. A successful hook on a hero grants 12 shield for 2s.",
            _=>"Standard attack."
        };

        private static string Effect(HeroId hero,int slot)=>hero switch
        {
            HeroId.Timmy=>slot switch
            {
                0=>"A stronger single-target claw swipe with short melee reach.",
                1=>$"Place a stardust burst; damages enemies in the circle and slows them by {(1f-TimmyBalance.DustSlow)*100:0}% for {TimmyBalance.DustSlowSeconds:0.#}s.",
                2=>$"Jet-assisted dash up to {TimmyBalance.JetRange:0.#}m in the chosen direction.",
                _=>$"Leap up to {TimmyBalance.ScratchRange:0.#}m toward one aimed enemy. On contact, root only that enemy for {TimmyBalance.ScratchHoldSeconds:0.#}s and deal {TimmyBalance.ScratchDamage:0} damage in {TimmyBalance.ScratchTicks} ticks. Timmy can move and attack; the target can counterattack."
            },
            HeroId.Veyr=>slot switch
            {
                0=>"Single-target slash.",
                1=>"Damages and slows movement by 20% for 1.2s.",
                2=>"Dash up to 4.2m in the chosen direction.",
                _=>"Delayed execute strike; damage rises by up to 35% as the target loses HP."
            },
            HeroId.Kaelon=>slot switch
            {
                0=>"Strike that slows 35% for 1.5s and stuns for 0.55s.",
                1=>"Grants Auren a 75-point shield for 4s.",
                2=>"Place a gravity field; damages and slows enemies 35% for 1.5s.",
                _=>"Area slam: damages and slows enemies 50% for 2s; allies gain 60 shield for 3s."
            },
            HeroId.Nyra=>slot switch
            {
                0=>"Aim a blue laser along a fixed direction.",
                1=>"Reposition dash up to 3m.",
                2=>"Damages and slows the target by 30% for 1.8s.",
                _=>"Three fixed-direction shots; 64 total damage if all shots connect."
            },
            HeroId.Elyra=>slot switch
            {
                0=>"Place an area burst; enemies can escape during the warning.",
                1=>"Area burst that slows enemies 35% for 2s.",
                2=>"Short phase dash up to 1.8m.",
                _=>"Large delayed field: 72 damage and 45% slow for 2s."
            },
            HeroId.Mira=>slot switch
            {
                0=>"Damages and slows the target by 25% for 1.5s.",
                1=>"Heal the aimed allied hero; heal yourself when no teammate is aimed at.",
                2=>"Shield the aimed allied hero for 4s; shield yourself when no teammate is aimed at.",
                _=>"Large support field: heals nearby allies and grants 25 shield for 3s."
            },
            HeroId.Rook=>slot switch
            {
                0=>"Heavy melee hit that slows 20% for 1s.",
                1=>"Restores 30 HP and grants 20 shield for 3s.",
                2=>"Short-windup 2.8m area smash.",
                _=>"3.2m area burst; Brakk also gains 65 shield for 4s."
            },
            HeroId.Traveler=>slot switch
            {
                0=>"Temporal Beam: precise long-range chronolight strike.",
                1=>"Rift Step: time-skip 3.6m through an entry and exit portal.",
                2=>"Chrono Snare: delayed 2.2m field that slows enemies for 1.5s.",
                _=>"Paradox Collapse: large delayed time-rift burst that slows enemies for 1.8s."
            },
            HeroId.Nerok=>slot switch
            {
                0=>"Throw the anchor after a readable windup; 28 damage and pull mobile enemies up to 4.2m. Walls block the hook and pull.",
                1=>"Gain 45 shield for 3s and 15% movement speed for 2s.",
                2=>"Short-range pressure wave; 26 damage and 25% slow for 1.2s.",
                _=>"After a warning, a fixed 3.2m vortex deals 60 total damage over 3s, draws mobile enemies inward at their running speed. Running directly outward cancels the pull."
            },
            _=>"Unknown effect."
        };
    }
}


