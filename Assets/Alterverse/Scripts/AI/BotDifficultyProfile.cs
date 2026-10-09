namespace Alterverse.AI
{
    // Preserve existing account/lobby tier ids while adding a tier below Easy.
    public enum BotDifficulty { VeryEasy = -1, Easy = 0, Normal = 1, Hard = 2, Expert = 3, Nightmare = 4 }

    // Difficulty is driven by decision quality. Easy through Expert receive normal damage.
    // Nightmare is the explicit top-end challenge with 4% less incoming damage.
    public readonly struct BotDifficultyProfile
    {
        public readonly float ReactionDelay, DecisionScale, ObjectiveInterval, TargetAccuracy;
        public readonly float RetreatAdjustment, Grouping, MemorySeconds, AbilityMistake, Positioning;
        public readonly float ThreatAwareness, TeamDiscipline, ObjectiveAwareness, DodgeAwareness;
        public readonly float ChaseDiscipline, AbilityDiscipline;
        public readonly float DamageTakenMultiplier;

        // MEDIUM is the default player-facing difficulty, so it must understand basic team play.
        // EASY remains deliberately individual. HARD/EXPERT still coordinate faster and more reliably.
        public bool Coordinated => TeamDiscipline >= .58f;
        public bool FullTeamCalls => TeamDiscipline >= .95f;
        public float TeamCallDelay => FullTeamCalls ? .18f : TeamDiscipline >= .8f ? .32f : .48f;
        public float FollowUpDelay => FullTeamCalls ? .12f : TeamDiscipline >= .8f ? .28f : .42f;

        private BotDifficultyProfile(
            float reaction,float decision,float objective,float accuracy,float retreat,float grouping,
            float memory,float mistake,float positioning,float threat,float team,float objectiveAwareness,
            float dodge,float chase,float ability,float damageTaken)
        {
            ReactionDelay=reaction;DecisionScale=decision;ObjectiveInterval=objective;TargetAccuracy=accuracy;
            RetreatAdjustment=retreat;Grouping=grouping;MemorySeconds=memory;AbilityMistake=mistake;Positioning=positioning;
            ThreatAwareness=threat;TeamDiscipline=team;ObjectiveAwareness=objectiveAwareness;DodgeAwareness=dodge;
            ChaseDiscipline=chase;AbilityDiscipline=ability;DamageTakenMultiplier=damageTaken;
        }

        public static BotDifficultyProfile For(BotDifficulty difficulty)=>difficulty switch
        {
            // Beginner tier reacts more slowly, predicts less accurately and wastes more
            // tactical/ability opportunities than Easy without changing combat damage.
            BotDifficulty.VeryEasy=>new(1.20f,2.60f,3.50f,.12f,-.06f,.16f,.65f,.58f,.12f,.15f,.15f,.18f,.15f,.15f,.18f,1f),
            // Easy remains functional, but reacts late, groups inconsistently and misses tactical windows.
            BotDifficulty.Easy=>new(.75f,1.80f,2.40f,.28f,-.035f,.28f,1.10f,.38f,.25f,.30f,.30f,.34f,.32f,.28f,.32f,1f),

            // Hard should feel like an experienced player without hidden information or stat advantages.
            BotDifficulty.Hard=>new(.20f,.72f,.50f,.90f,.045f,.86f,4.50f,.055f,.89f,.90f,.88f,.90f,.91f,.88f,.91f,1f),

            // Expert uses the same legal information as every other bot, but evaluates it quickly and consistently.
            BotDifficulty.Expert=>new(.11f,.50f,.28f,.98f,.075f,.98f,6.00f,.015f,.98f,1.00f,.98f,1.00f,.98f,.98f,.99f,1f),

            // Nightmare reacts faster and misses fewer tactical windows than Expert.
            BotDifficulty.Nightmare=>new(.08f,.42f,.22f,.995f,.075f,1f,6f,.005f,1f,1f,1f,1f,1f,1f,1f,.96f),

            // Internal enum stays Normal for save compatibility; player-facing label is MEDIUM.
            // MEDIUM now participates in shared tactical calls, but with slower reaction/follow-up timings.
            _=>new(.38f,1.00f,1.10f,.60f,0f,.58f,2.50f,.18f,.58f,.58f,.58f,.60f,.62f,.58f,.60f,1f)
        };
    }
}

