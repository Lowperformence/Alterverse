using System;
using System.Collections.Generic;

namespace Alterverse.Match
{
    /// <summary>Match-time cadence and single-claim ownership; independent of presentation/transport.</summary>
    public sealed class HalloweenPumpkinRules
    {
        public const float SpawnInterval = 120f;
        public const int ShardsPerPumpkin = 1;
        public const float PickupRadius = 1.35f;
        public const float PickupHeight = 2f;
        readonly HashSet<int> available = new();
        readonly Dictionary<int,int> collected = new();
        float remaining = SpawnInterval;
        int serial;

        public int SpawnCount => serial;
        public float Remaining => remaining;
        public bool Advance(float matchDelta)
        {
            if (float.IsNaN(matchDelta) || float.IsInfinity(matchDelta) || matchDelta <= 0) return false;
            remaining -= matchDelta;
            return remaining <= .0001f;
        }
        public int Spawned()
        {
            remaining = SpawnInterval; // No burst of catch-up pickups after suspension/a stalled frame.
            available.Add(++serial);
            return serial;
        }
        public int CollectedBy(int slot) => collected.TryGetValue(slot,out int count) ? count : 0;
        public bool TryCollect(int id,int slot,bool eligible,out int total)
        {
            total = CollectedBy(slot);
            if (!eligible || slot < 0 || !available.Remove(id)) return false;
            collected[slot] = total = checked(total + 1);
            return true;
        }
        public static bool Eligible(bool human,bool alive,bool connected,float flatDistanceSquared,float height)
        {
            return human && alive && connected &&
                !float.IsNaN(flatDistanceSquared) && !float.IsInfinity(flatDistanceSquared) &&
                !float.IsNaN(height) && !float.IsInfinity(height) &&
                flatDistanceSquared >= 0 && flatDistanceSquared <= PickupRadius * PickupRadius &&
                Math.Abs(height) <= PickupHeight;
        }
    }
}
