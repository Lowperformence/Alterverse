namespace Alterverse.AI
{
    public enum BotDifficulty { Easy, Normal, Hard, Expert }

    // Decision parameters only. No combat stats, vision radius or ability cooldowns.
    public readonly struct BotDifficultyProfile
    {
        public readonly float ReactionDelay, DecisionScale, ObjectiveInterval, TargetAccuracy;
        public readonly float RetreatAdjustment, Grouping, MemorySeconds, AbilityMistake, Positioning;
        private BotDifficultyProfile(float reaction,float decision,float objective,float accuracy,float retreat,float grouping,float memory,float mistake,float positioning)
        {ReactionDelay=reaction;DecisionScale=decision;ObjectiveInterval=objective;TargetAccuracy=accuracy;RetreatAdjustment=retreat;Grouping=grouping;MemorySeconds=memory;AbilityMistake=mistake;Positioning=positioning;}
        public static BotDifficultyProfile For(BotDifficulty difficulty)=>difficulty switch
        {
            BotDifficulty.Easy=>new(.60f,1.65f,2.0f,.35f,-.04f,.35f,1.5f,.28f,.30f),
            BotDifficulty.Hard=>new(.22f,.80f,.55f,.90f,.03f,.85f,5f,.07f,.90f),
            BotDifficulty.Expert=>new(.13f,.62f,.38f,.97f,.05f,.96f,6.5f,.025f,.98f),
            _=>new(.35f,1f,1f,.65f,0f,.60f,3f,.15f,.60f)
        };
    }
}
