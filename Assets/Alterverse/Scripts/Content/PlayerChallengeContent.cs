namespace Alterverse.Content
{
    public readonly struct ChallengeDef
    {
        public readonly string Name, Description;
        public readonly int Shards;

        public ChallengeDef(string name, string description, int shards)
        {
            Name = name;
            Description = description;
            Shards = shards;
        }
    }

    public static class PlayerChallengeContent
    {
        public static readonly ChallengeDef[] Daily =
        {
            new ChallengeDef("ENEMY HUNTER", "Defeat 50 enemies today.", 20),
            new ChallengeDef("HOT ZONE CONTROL", "Capture 20 Hot Zones today.", 15),
            new ChallengeDef("SOLDIER SLAYER", "Defeat 60 enemy soldiers today.", 10)
        };

        public static readonly ChallengeDef[] Weekly =
        {
            new ChallengeDef("REALITY VETERAN", "Complete 10 matches.", 240),
            new ChallengeDef("COLLISION FORCE", "Win 5 matches.", 300),
            new ChallengeDef("SIX FACES OF WAR", "Play 4 different heroes.", 260)
        };
    }
}
