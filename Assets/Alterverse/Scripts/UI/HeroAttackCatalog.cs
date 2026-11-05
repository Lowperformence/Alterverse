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
            if(hero==HeroId.Bolt7)return new[]{
                new HeroAttackCard("BASIC","Gatling",$"2 DMG / SHOT | {stats.AttackInterval:0.###}s | RANGE {stats.AttackRange:0.#}","Three times Sivra's base fire rate. Short-to-medium range."),
                Ability(hero,"Q / MEDIUM",0,kit.Q),Ability(hero,"W / HEAVY",1,kit.W),Ability(hero,"E / GRENADE",2,kit.E),Ability(hero,"ULTIMATE",3,kit.Ultimate)};
            return new[]
            {
                new HeroAttackCard("BASIC",hero==HeroId.VoidKnight?"Greatsword":hero==HeroId.Timmy?"Claw":"Basic Attack",
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
            if(hero==HeroId.Bolt7&&slot==3)return "ALL CURRENT SHIELDS -> 0 / NO HP DAMAGE";
            if(hero==HeroId.VoidKnight&&slot==3)return $"{spec.Power:0} DMG + {VoidKnightBalance.UltimateLifeDrain:0} LIFE DRAIN / TARGET";
            if(hero==HeroId.VoidKnight&&slot==1)return "80% FRONTAL REDUCTION / 0.65s";
            if(hero==HeroId.Elyra&&slot==2)return $"{HeroAbilityKit.PhaseFoldShield:0} SHIELD / {HeroAbilityKit.PhaseFoldShieldSeconds:0.##}s";
            if(hero==HeroId.Timmy&&slot==2)return $"{TimmyBalance.JetShield:0} SHIELD / {TimmyBalance.JetShieldSeconds:0.#}s";
            if(hero==HeroId.Elyra&&slot==1)return $"{spec.Power:0} DMG + {HeroAbilityKit.IoneInvertShieldAbsorb:0} SHIELD ABSORB";
            if(hero==HeroId.Nyra&&slot==0)return $"{spec.Power:0} DMG + {HeroAbilityKit.SivraPrismShieldAbsorb:0} SHIELD ABSORB";
            if(hero==HeroId.Kaelon&&slot==1)return $"{spec.Power:0} SHIELD / 4s";
            if(hero==HeroId.Kaelon&&slot==3)return $"{spec.Power:0} DMG + 50 SELF HEAL";
            if(hero==HeroId.Mira&&slot==1)return $"{spec.Power:0} HEAL";
            if(hero==HeroId.Mira&&slot==2)return $"{spec.Power:0} SHIELD / 4s";
            if(hero==HeroId.Mira&&slot==3)return $"{spec.Power:0} HEAL + 25 SHIELD";
            if(hero==HeroId.Nerok&&slot==1)return $"{spec.Power:0} SHIELD / 3s";
            if(hero==HeroId.Rook&&slot==1)return $"{spec.Power:0} HEAL + 30 SHIELD";
            if(spec.Power<=0)return "";
            return $"{spec.Power:0} DMG";
        }

        private static string BasicEffect(HeroId hero)=>hero switch
        {
            HeroId.VoidKnight=>"Two-handed greatsword. Three confirmed basic hits prime a 30-point shield for 2s on the next damaging skill. Longer melee reach, slower swings and less health than a tank.",
            HeroId.Veyr=>"Fast melee pressure for finishing isolated targets.",
            HeroId.Timmy=>$"Short-range claw strike. Light Paws: base shield recharges {TimmyBalance.PassiveShieldRechargeMultiplier:0}x as fast. Damaging skill hits also grant {(TimmyBalance.PassiveSpeed-1f)*100:0}% speed for {TimmyBalance.PassiveSeconds:0.#}s, at most once every {TimmyBalance.PassiveCooldown:0.#}s.",
            HeroId.Kaelon=>$"Frontline melee. Extra passive: +{HeroAbilityKit.AurenPassiveRegenPerSecond:0} HP every second while alive (max HP cap).",
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
            HeroId.Bolt7=>slot switch
            {
                0=>$"Short grenade arc; {Bolt7Balance.GrenadeDamage:0} damage throughout a {Bolt7Balance.GrenadeRadius:0.#}m radius. {Bolt7Balance.GrenadeRange:0.#}m throw range; cover blocks it.",
                1=>"Brief windup and 2m rush; hit every enemy in the full marked 3m, 70 degree cone for 40 damage and knockback. Cover blocks it.",
                2=>$"Impulse grenade: {Bolt7Balance.ImpulseDamage:0} damage throughout a {Bolt7Balance.ImpulseRadius:0.#}m radius and {Bolt7Balance.ImpulseKnockback:0.#}m outward knockback. {Bolt7Balance.ImpulseRange:0.#}m throw range; cover blocks it.",
                _=>"After a 0.9s warning, remove every personal shield and overshield in the enemy area once. Lightning Orb and Lightning Nova, then Star Fog. New shields work immediately. No health damage."
            },
            HeroId.VoidKnight=>slot switch
            {
                0=>$"A wide greatsword cut; its direction stays fixed through the windup. At the swing, vanish in smoke for {VoidKnightBalance.SweepCloakSeconds:0}s, even on a miss: 80% transparent to yourself and allies, 98% to enemies. Enemy target locks and health markers disappear; area attacks can still hit.",
                1=>"Stand and guard for 0.65s. Reduce frontal damage 80%. A successful block triggers one short counter. Rear and flank hits bypass the guard.",
                2=>$"Tap to thrust in movement direction; drag to aim. A short preparation precedes the lunge. The first enemy stops it and is slowed by {(1f-VoidKnightBalance.DashSlow)*100:0}% for {VoidKnightBalance.DashSlowSeconds:0.#}s; walls block it. No invulnerability.",
                _=>$"After a 0.65s warning, strike an 80-degree front cone once per enemy. Each hit adds {VoidKnightBalance.UltimateLifeDrain:0} damage and restores up to {VoidKnightBalance.UltimateLifeDrain:0} HP from actual health lost. Slow movement 25% for 1.2s. Stuns interrupt preparation."
            },
            HeroId.Timmy=>slot switch
            {
                0=>"A stronger single-target claw swipe with short melee reach.",
                1=>$"Place a stardust burst; damages enemies in the circle and slows them by {(1f-TimmyBalance.DustSlow)*100:0}% for {TimmyBalance.DustSlowSeconds:0.#}s.",
                2=>$"Jet-assisted dash up to {TimmyBalance.JetRange:0.#}m in the chosen direction. Gain {TimmyBalance.JetShield:0} shield for {TimmyBalance.JetShieldSeconds:0.#}s.",
                _=>$"Leap toward one enemy selected inside the {TimmyBalance.ScratchRange:0.#}m aim field. Follow and root it for {TimmyBalance.ScratchHoldSeconds:0.#}s if it stays inside the original field; leaving the field avoids the hit. Deal {TimmyBalance.ScratchDamage:0} damage in {TimmyBalance.ScratchTicks} ticks. Timmy can move and attack; the target can counterattack."
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
                1=>"Grants Auren a 50-point shield for 4s.",
                2=>"Place a gravity field; damages and slows enemies 35% for 1.5s.",
                _=>"Area slam: damages and slows enemies 50% for 2s; allies gain 60 shield for 3s; Auren restores 50 HP."
            },
            HeroId.Nyra=>slot switch
            {
                0=>$"Fixed-direction blue laser. Steal up to {HeroAbilityKit.SivraPrismShieldAbsorb:0} enemy shield before damage. Refill your base shield first; excess becomes {HeroAbilityKit.SivraPrismOvershieldSeconds:0.#}s bonus shield within the shield cap.",
                1=>$"Reposition dash up to 3m. Passive: after the dash, gain +{(HeroAbilityKit.SivraDashSpeedMultiplier-1f)*100:0}% running speed for {HeroAbilityKit.SivraDashSpeedSeconds:0.#}s.",
                2=>"Damages and slows the target by 30% for 1.8s.",
                _=>$"Three fixed-direction shots through a very narrow {HeroAbilityKit.SivraUltimateConeAngle:0.#}-degree cone, up to {HeroAbilityKit.SivraUltimateRange:0.#}m (double range). {HeroAbilityKit.LoadoutFor(HeroId.Nyra).Ultimate.Power:0} total damage if all shots connect."
            },
            HeroId.Elyra=>slot switch
            {
                0=>"Place an area burst; enemies can escape during the warning.",
                1=>$"Area burst that slows enemies 35% for 2s. Absorb up to {HeroAbilityKit.IoneInvertShieldAbsorb:0} existing shield per enemy hit, refilling your base shield first; overflow lasts {HeroAbilityKit.IoneInvertOvershieldSeconds:0.#}s within the shield cap. Unshielded enemies grant no shield.",
                2=>$"Phase dash up to {HeroAbilityKit.LoadoutFor(HeroId.Elyra).E.Range:0.#}m; gain {HeroAbilityKit.PhaseFoldShield:0} shield for {HeroAbilityKit.PhaseFoldShieldSeconds:0.##}s.",
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
                1=>"Restores 60 HP and grants 30 shield for 3s.",
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
                0=>"The cone selects one position; the narrow anchor follows the shown path after a windup. First enemy hit takes 28 damage and mobile enemies are pulled up to 4.2m. Enemies can dodge; walls block flight and pull.",
                1=>"Gain 45 shield for 3s and 15% movement speed for 2s.",
                2=>"Short-range pressure wave; 26 damage and 25% slow for 1.2s.",
                _=>$"After a warning, a fixed {HeroAbilityKit.LoadoutFor(HeroId.Nerok).Ultimate.Radius:0.##}m vortex deals 60 total damage over 3s, draws mobile enemies inward at their running speed. Running directly outward cancels the pull."
            },
            _=>"Unknown effect."
        };
    }
}
