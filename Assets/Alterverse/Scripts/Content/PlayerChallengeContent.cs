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
        // Daily missions share the same daily reset and claim-once flow.
        public static readonly ChallengeDef[] Daily =
        {
            new ChallengeDef("HUNTER","Kill 50 enemies",20),
            new ChallengeDef("HOT ZONE","Capture 20 Hot Zones",15),
            new ChallengeDef("SOLDIER BREAKER","Kill 60 enemy soldiers",15),
            new ChallengeDef("MAP EXPLORER","Win on 5 different maps",15),
            new ChallengeDef("STORE ADS","Watch 5 ads in the Store",20),
            new ChallengeDef("FRIEND SQUAD","Complete 2 matches with an invited friend",40),
            new ChallengeDef("TURRET COMMANDER","Deploy 3 turrets",10),
            new ChallengeDef("MATCH MVP","Become match MVP 3 times",15),
            new ChallengeDef("HERO EXPLORER","Play 2 different heroes",5)
        };
        public static readonly ChallengeDef[] Weekly =
        {
            new ChallengeDef("REALITY VETERAN","Complete 10 matches.",240),
            new ChallengeDef("COLLISION FORCE","Win 5 matches.",300),
            new ChallengeDef("SIX FACES OF WAR","Play 4 different heroes.",260)
        };
    }
}
