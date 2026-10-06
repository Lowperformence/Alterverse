namespace Alterverse.Content
{
    public readonly struct ChallengeDef
    {
        public readonly string Name,Description;
        public readonly int Shards;
        public ChallengeDef(string name,string description,int shards){Name=name;Description=description;Shards=shards;}
    }

    public static class PlayerChallengeContent
    {
        // Existing missions pay 45 shards; the distinct-map mission adds 15.
        public static readonly ChallengeDef[] Daily =
        {
            new ChallengeDef("HUNTER","Defeat 50 enemies.",20),
            new ChallengeDef("HOT ZONE","Win control of 20 Hot Zones.",15),
            new ChallengeDef("SOLDIER BREAKER","Defeat 60 enemy Soldiers.",10),
            new ChallengeDef("MAP EXPLORER","Win on 5 different maps today.",15)
        };
        public static readonly ChallengeDef[] Weekly =
        {
            new ChallengeDef("REALITY VETERAN","Complete 10 matches.",240),
            new ChallengeDef("COLLISION FORCE","Win 5 matches.",300),
            new ChallengeDef("SIX FACES OF WAR","Play 4 different heroes.",260)
        };
    }
}
