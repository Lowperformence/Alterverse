using Alterverse.Characters;
namespace Alterverse.Audio
{
public readonly struct CombatSoundIdentity{public readonly float Pitch,Gain;public readonly string Character;public CombatSoundIdentity(float pitch,float gain,string character){Pitch=pitch;Gain=gain;Character=character;}}
public static class CombatSoundPalette
{
public static CombatSoundIdentity For(HeroId hero)=>hero switch{HeroId.Kaelon=>new CombatSoundIdentity(1f,1f,"massive geometric impact"),HeroId.Nyra=>new CombatSoundIdentity(1f,1f,"dry recorded rifle report"),HeroId.Elyra=>new CombatSoundIdentity(1f,1f,"impossible harmonic fracture"),HeroId.Mira=>new CombatSoundIdentity(1f,1f,"soft crystalline healing"),HeroId.Rook=>new CombatSoundIdentity(1f,1f,"salvaged brutal strike"),HeroId.Traveler=>new CombatSoundIdentity(1f,1f,"suction, fracture and short temporal echo"),_=>new CombatSoundIdentity(1f,1f,"fast fractured cut")};
}
}

