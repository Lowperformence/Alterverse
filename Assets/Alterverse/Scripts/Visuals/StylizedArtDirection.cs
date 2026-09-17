using UnityEngine;
namespace Alterverse.Visuals
{
public static class StylizedArtDirection
{
public static readonly Color Ink=new(.018f,.014f,.035f);public static readonly Color ZeroDark=new(.075f,.025f,.12f);public static readonly Color ZeroMid=new(.31f,.075f,.48f);public static readonly Color ZeroEnergy=new(.76f,.24f,1f);public static readonly Color LumenDark=new(.025f,.13f,.16f);public static readonly Color LumenMid=new(.035f,.48f,.55f);public static readonly Color LumenEnergy=new(.18f,.92f,1f);public static readonly Color WarmAccent=new(1f,.62f,.16f);
public static Color HeroAccent(Characters.HeroId hero)=>hero switch{Characters.HeroId.Kaelon=>new Color(.16f,.64f,.92f),Characters.HeroId.Nyra=>new Color(1f,.64f,.14f),Characters.HeroId.Elyra=>new Color(.63f,.25f,.95f),Characters.HeroId.Mira=>new Color(.2f,.86f,.42f),Characters.HeroId.Rook=>new Color(.88f,.4f,.14f),_=>ZeroEnergy};
}
}
