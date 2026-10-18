namespace Alterverse.Multiplayer
{
    /// <summary>Frame-independent send deadlines without catch-up packet bursts.</summary>
    public static class SnapshotCadence
    {
        public static bool IsDue(float now,float interval,ref float next)
        {
            const float tolerance=.00001f;
            if(now+tolerance<next)return false;
            next+=interval;
            // A stall sends the latest state once, never the missed intermediate states.
            if(next<=now+tolerance)next=now+interval;
            return true;
        }
    }
}
