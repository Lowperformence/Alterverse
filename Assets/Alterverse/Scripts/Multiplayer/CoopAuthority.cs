using Alterverse.AI;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Single source of truth for whether the current match is a network co-op match.
    /// Offline play always simulates locally. In co-op, only the Relay host may advance
    /// authoritative AI, random gameplay directors, damage and match state.
    /// </summary>
    public static class CoopAuthority
    {
        public static CoopMatchPlan PreparedPlan { get; private set; }
        public static CoopMatchPlan ActivePlan { get; private set; }
        public static bool Active=>ActivePlan!=null;
        public static bool LocalIsHost { get; private set; }
        public static bool CanSimulateHostGameplay=>!Active||LocalIsHost;

        public static void Prepare(CoopMatchPlan plan)
        {
            PreparedPlan=plan;
        }

        public static bool BeginPreparedMatch(bool localIsHost)
        {
            if(PreparedPlan==null)return false;
            ActivePlan=PreparedPlan;
            PreparedPlan=null;
            LocalIsHost=localIsHost;

            ArenaVariantRuntime.BeginCoopMatch(ActivePlan.Arena);
            BotDifficultySettings.BeginMatchOverride(ActivePlan.Difficulty);
            return true;
        }

        public static void EndMatch()
        {
            PreparedPlan=null;
            ActivePlan=null;
            LocalIsHost=false;
            BotDifficultySettings.EndMatchOverride();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            PreparedPlan=null;
            ActivePlan=null;
            LocalIsHost=false;
            BotDifficultySettings.EndMatchOverride();
        }
    }
}
