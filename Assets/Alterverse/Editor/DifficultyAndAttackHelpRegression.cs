#if UNITY_EDITOR
using System;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class DifficultyAndAttackHelpRegression
    {
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("Difficulty/help: "+message);}
        [MenuItem("DIMENSIONS FRONT/Checks/Difficulty and attack help (non-destructive)")]
        public static void Run()
        {
            Check((int)BotDifficulty.VeryEasy==-1&&(int)BotDifficulty.Easy==0&&(int)BotDifficulty.Normal==1&&
                (int)BotDifficulty.Hard==2&&(int)BotDifficulty.Expert==3&&(int)BotDifficulty.Nightmare==4,"saved tier ids changed");
            Check(BotDifficultySettings.InitialDifficulty==BotDifficulty.Normal,"new profile does not start on Normal");
            var beginner=BotDifficultyProfile.For(BotDifficulty.VeryEasy);
            var easy=BotDifficultyProfile.For(BotDifficulty.Easy);
            Check(beginner.ReactionDelay>easy.ReactionDelay&&beginner.DecisionScale>easy.DecisionScale&&
                beginner.ObjectiveInterval>easy.ObjectiveInterval,"beginner timing is not slower than Easy");
            Check(beginner.TargetAccuracy<easy.TargetAccuracy&&beginner.Positioning<easy.Positioning&&
                beginner.AbilityMistake>easy.AbilityMistake&&beginner.MemorySeconds<easy.MemorySeconds,"beginner combat is not weaker than Easy");
            Check(beginner.ThreatAwareness<easy.ThreatAwareness&&beginner.TeamDiscipline<easy.TeamDiscipline&&
                beginner.ObjectiveAwareness<easy.ObjectiveAwareness&&beginner.DodgeAwareness<easy.DodgeAwareness&&
                beginner.ChaseDiscipline<easy.ChaseDiscipline&&beginner.AbilityDiscipline<easy.AbilityDiscipline&&
                beginner.Grouping<easy.Grouping&&!beginner.Coordinated,"beginner tactics are not weaker than Easy");
            Check(beginner.DamageTakenMultiplier==easy.DamageTakenMultiplier,"beginner silently changed damage");
            Check(BotDifficultySettings.Cycle(BotDifficulty.VeryEasy,-1)==BotDifficulty.Nightmare&&
                BotDifficultySettings.Cycle(BotDifficulty.Nightmare,1)==BotDifficulty.VeryEasy&&
                BotDifficultySettings.Cycle(BotDifficulty.Easy,-1)==BotDifficulty.VeryEasy,"tier cycling skips beginner");
            Check(BotDifficultySettings.Clamp((BotDifficulty)(-10))==BotDifficulty.VeryEasy&&
                BotDifficultySettings.Clamp((BotDifficulty)99)==BotDifficulty.Nightmare,"tier boundaries incorrect");
            Check(!BotDifficultySettings.IsValid((BotDifficulty)(-2))&&!BotDifficultySettings.IsValid((BotDifficulty)5),"invalid tier accepted");
            Check(BotDifficultySettings.RewardBonusPercent(BotDifficulty.VeryEasy)==0&&
                BotDifficultySettings.RewardBonusPercent(BotDifficulty.Easy)==0&&
                BotDifficultySettings.RewardBonusPercent(BotDifficulty.Normal)==10,"existing reward bonuses changed");
            for(int tier=(int)BotDifficultySettings.Lowest;tier<=(int)BotDifficultySettings.Highest;tier++)
            {
                BotDifficulty value=(BotDifficulty)tier;
                Check(BotDifficultySettings.IsValid(value),"valid tier rejected");
                Check(BotDifficultySettings.HoldAttackHelpAllowed(value,false)==(value==BotDifficulty.VeryEasy),"solo help leaked to another tier");
                Check(!BotDifficultySettings.HoldAttackHelpAllowed(value,true),"multiplayer showed hold help");
                var members=new[]{new OnlineMatchPlan.DedicatedMember
                {PlayerId="difficulty-check-player",DisplayName="CHECK",TeamId="check-team",Hero=HeroId.Veyr}};
                var plan=OnlineMatchPlan.BuildDedicated("difficulty-check-"+tier,ArenaVariant.Moon,value,members);
                Check(plan.Difficulty==BotDifficulty.Hard,"online accepted non-Hard bots");
                var replica=OnlineMatchPlan.FromAuthoritativeSlots(plan.MatchId,plan.Arena,value,plan.Session.Slots,members[0].PlayerId);
                Check(replica.Difficulty==BotDifficulty.Hard,"online replica accepted non-Hard bots");
                // Even a stale/mutated old plan must use Hard in the next online round.
                plan.Difficulty=value;
                var next=CoopMatchPlan.NextRound(plan,plan.MatchId+"-next",1);
                Check(next.Difficulty==BotDifficulty.Hard,"online rematch accepted non-Hard bots");
            }
            Debug.Log("DF: beginner ordering/profile, save ids, cycling, rewards, solo-only hold help and online Hard plan/replica/rematch checks passed.");
        }
    }
}
#endif

