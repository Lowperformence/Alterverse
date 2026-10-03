using Unity.Netcode;

namespace Alterverse.Multiplayer
{
    /// <summary>Shared sender binding and round isolation for every gameplay transport.</summary>
    public static class MatchNetworkProtocol
    {
        public static bool Authorized(ulong clientId, string playerId)
        {
            if (CoopAuthority.ActivePlan is OnlineMatchPlan)
                return DedicatedOnlineMatchHandshake.IsAuthorizedClient(clientId, playerId) ||
                    OnlinePlanHandshake.IsAuthorizedClient(clientId, playerId);
            return CoopPlanHandshake.IsAuthorizedClient(clientId, playerId);
        }
        public static bool ReadRound(ref FastBufferReader reader)
        {
            if (!CoopAuthority.Active || !reader.TryBeginRead(sizeof(uint))) return false;
            reader.ReadValueSafe(out uint round);
            return round == CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan);
        }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
