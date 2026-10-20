using System.Collections.Generic;

namespace Alterverse.Multiplayer
{
    /// <summary>A bounded distance reserve absorbs receive bursts without granting slack per packet.</summary>
    internal sealed class OwnerMovementBudget
    {
        private struct State { public float At, Distance; }
        private readonly Dictionary<ulong,State> owners=new();
        private const float ReserveSeconds=.25f;

        public float Limit(ulong owner,float now,float speed,float requested,float slack)
        {
            if(!MatchNetworkProtocol.Finite(now)||!MatchNetworkProtocol.Finite(speed)||
               !MatchNetworkProtocol.Finite(requested)||speed<=0f||requested<0f)return 0f;
            float capacity=speed*ReserveSeconds+slack;
            if(!owners.TryGetValue(owner,out State state))state=new State{At=now,Distance=capacity};
            float elapsed=System.Math.Max(0f,now-state.At);
            float available=System.Math.Min(capacity,state.Distance+speed*elapsed);
            float accepted=System.Math.Min(requested,available);
            owners[owner]=new State{At=now,Distance=available-accepted};
            return accepted;
        }

        // Server-controlled movement/status cannot bank a later owner teleport.
        public void Hold(ulong owner,float now)=>owners[owner]=new State{At=now,Distance=0f};
        public void Clear()=>owners.Clear();
    }
}
