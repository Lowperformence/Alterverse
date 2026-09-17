using Alterverse.Characters;
namespace Alterverse.Content
{
public readonly struct HeroFlavor{public readonly HeroId Hero;public readonly string Origin,Tagline,CombatFantasy;public HeroFlavor(HeroId hero,string origin,string tagline,string combatFantasy){Hero=hero;Origin=origin;Tagline=tagline;CombatFantasy=combatFantasy;}}
public static class HeroFlavorContent
{
public static readonly HeroFlavor[] Heroes={new HeroFlavor(HeroId.Veyr,"DIMENSION ZERO","THE GAP MOVES WITH ME.","Fractured assassin who attacks from impossible gaps in occupied space."),new HeroFlavor(HeroId.Kaelon,"LUMEN","STAND WHERE REALITY HOLDS.","Geometric frontline anchor who turns controlled light into mass and protection."),new HeroFlavor(HeroId.Nyra,"LUMEN","ONE LINE. ONE OPENING.","Precision ranged hunter built around deliberate high-energy shots and clean spacing."),new HeroFlavor(HeroId.Elyra,"ASTRALIS","ANGLES ARE ONLY SUGGESTIONS.","Reality-bending caster who distorts geometry and punishes predictable movement."),new HeroFlavor(HeroId.Mira,"VERDANT","LIFE REMEMBERS EVERY WOUND.","Organic support who grows protection and restores allies through living resonance."),new HeroFlavor(HeroId.Rook,"WASTELAND-7","IF IT SURVIVED, I CAN USE IT.","Close-range salvager who converts brutal momentum and wreckage into pressure.")};
}
}
