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
        // Combat/map missions pay 60 shards; five completed store ads add a 20-shard daily bonus.
        public static readonly ChallengeDef[] Daily =
        {
            new ChallengeDef("HUNTER","Kill 50 enemies",20),
            new ChallengeDef("HOT ZONE","Capture 20 Hot Zones",15),
            new ChallengeDef("SOLDIER BREAKER","Kill 60 enemy soldiers",10),
            new ChallengeDef("MAP EXPLORER","Win on 5 different maps",15),
            new ChallengeDef("STORE ADS","Watch 5 ads in the Store",20)
        };
        public static readonly ChallengeDef[] Weekly =
        {
            new ChallengeDef("REALITY VETERAN","Complete 10 matches.",240),
            new ChallengeDef("COLLISION FORCE","Win 5 matches.",300),
            new ChallengeDef("SIX FACES OF WAR","Play 4 different heroes.",260)
        };
    }
}
