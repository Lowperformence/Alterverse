namespace Alterverse.Content
{
public readonly struct ChallengeDef{public readonly string Name,Description;public readonly int Shards;public ChallengeDef(string name,string description,int shards){Name=name;Description=description;Shards=shards;}}
public static class PlayerChallengeContent
{
public static readonly ChallengeDef[] Daily={new ChallengeDef("FRONTLINE","Complete 2 matches today.",60),new ChallengeDef("HUNTER","Defeat 10 enemies today.",70),new ChallengeDef("OBJECTIVE FORCE","Score 5 objectives today.",55)};public static readonly ChallengeDef[] Weekly={new ChallengeDef("REALITY VETERAN","Complete 10 matches.",240),new ChallengeDef("COLLISION FORCE","Win 5 matches.",300),new ChallengeDef("SIX FACES OF WAR","Play 4 different heroes.",260)};
}
}
