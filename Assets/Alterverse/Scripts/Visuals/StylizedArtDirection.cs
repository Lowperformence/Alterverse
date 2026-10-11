using Alterverse.Characters;using UnityEngine;
namespace Alterverse.Visuals
{
public static class StylizedArtDirection
{
public static readonly Color Ink=new(.018f,.014f,.035f);public static readonly Color ArenaVoid=new(.055f,.07f,.11f);public static readonly Color ArenaStone=new(.22f,.27f,.34f);public static readonly Color ArenaStoneLight=new(.34f,.40f,.47f);public static readonly Color ArenaCenter=new(.12f,.18f,.24f);public static readonly Color ZeroGround=new(.30f,.16f,.42f);public static readonly Color LumenGround=new(.10f,.39f,.43f);public static readonly Color ObjectiveGold=new(1f,.58f,.12f);public static readonly Color ZeroDark=new(.075f,.025f,.12f);public static readonly Color ZeroMid=new(.31f,.075f,.48f);public static readonly Color ZeroEnergy=new(.76f,.24f,1f);public static readonly Color LumenDark=new(.025f,.13f,.16f);public static readonly Color LumenMid=new(.035f,.48f,.55f);public static readonly Color LumenEnergy=new(.18f,.92f,1f);public static readonly Color WarmAccent=new(1f,.62f,.16f);public static Color HeroAccent(HeroId hero)=>hero switch{HeroId.Kaelon=>new Color(1f,.66f,.19f),HeroId.Nyra=>new Color(.12f,.78f,1f),HeroId.Elyra=>new Color(.63f,.25f,.95f),HeroId.Mira=>new Color(.42f,.92f,1f),HeroId.Rook=>new Color(.88f,.4f,.14f),HeroId.Traveler=>new Color(.94f,.82f,.54f),HeroId.Nerok=>new Color(.04f,.72f,1f),HeroId.Timmy=>new Color(.30f,.85f,1f),_=>ZeroEnergy};
}
}

