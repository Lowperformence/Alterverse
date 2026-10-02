using Alterverse.AI;
using Alterverse.Match;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Single source of truth for whether the current match is a network co-op match.
    /// Offline play always simulates locally. In network play, the NGO server owns
    /// authoritative AI, random gameplay directors, damage and match state. Relay host
    /// mode remains supported because the host is also the NGO server.
    /// </summary>
    public static class CoopAuthority
    {
        public static CoopMatchPlan PreparedPlan { get; private set; }
        public static CoopMatchPlan ActivePlan { get; private set; }
        public static bool Active=>ActivePlan!=null;
        public static bool LocalIsHost { get; private set; }

        public static bool LocalHasServerAuthority
        {
            get
            {
                if(!Active)return true;
                NetworkManager manager=NetworkManager.Singleton;
                if(manager!=null&&manager.IsListening)return manager.IsServer;
                // Preserve the existing Relay-host behavior while the transport is starting/stopping.
                return LocalIsHost;
            }
        }

        public static bool CanSimulateHostGameplay=>LocalHasServerAuthority;
        public static bool CanMutateAuthoritativeState=>LocalHasServerAuthority;

        public static void Prepare(CoopMatchPlan plan)
        {
            PreparedPlan=plan;
        }

        public static void ClearPrepared()
        {
            if(!Active)PreparedPlan=null;
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
