using System;
using UnityEngine;
namespace Alterverse.Match
{
    [Flags]
    public enum PauseReason { Menu=1, Opening=2, Result=4, Transition=8, Background=16, Settings=32 }
    /// <summary>One owner for scaled match time. Releasing one pause cannot release another.</summary>
    public static class MatchClock
    {
        private static PauseReason reasons;
        private static float impactScale=1;
        public static bool Paused=>reasons!=0;
        public static bool InBackground=>(reasons&PauseReason.Background)!=0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){reasons=0;impactScale=1;Apply();}
        public static void Set(PauseReason reason,bool value)
        {if(value)reasons|=reason;else reasons&=~reason;Apply();}
        public static void SetImpact(float scale){impactScale=Mathf.Clamp(scale,.05f,1);Apply();}
        private static void Apply(){Time.timeScale=Paused?0:impactScale;}
    }
}
