namespace Alterverse.Content
{
public readonly struct ChallengeDef{public readonly string Name,Description;public readonly int Shards;public ChallengeDef(string name,string description,int shards){Name=name;Description=description;Shards=shards;}}
public static class PlayerChallengeContent
{
public static readonly ChallengeDef[] Daily={new ChallengeDef("FRONTLINE","Influence the Dimensions Front 5 times.",60),new ChallengeDef("PLANET HUNTER","Help secure 2 dimension planets.",70),new ChallengeDef("STAY REAL","Complete a match with 3 or fewer deaths.",55)};public static readonly ChallengeDef[] Weekly={new ChallengeDef("REALITY VETERAN","Complete 10 matches.",240),new ChallengeDef("COLLISION FORCE","Win 5 matches.",300),new ChallengeDef("SIX FACES OF WAR","Play 4 different heroes.",260)};
}
}
