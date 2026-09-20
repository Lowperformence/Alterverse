#if UNITY_EDITOR
using System;
using Alterverse.Match;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class HotZoneScoringRegression
    {
        private static void Check(bool value,string reason)
        {
            if(!value)throw new InvalidOperationException("Hot Zone scoring regression: "+reason);
        }

        private static void Expect(int zero,int lumen,float seconds,float expectedZero,float expectedLumen)
        {
            Vector2 delta=HotZoneDirector.ScoreDelta(zero,lumen,seconds);
            Check(Mathf.Abs(delta.x-expectedZero)<.001f,
                $"ZERO delta for {zero}v{lumen} over {seconds}s was {delta.x}, expected {expectedZero}");
            Check(Mathf.Abs(delta.y-expectedLumen)<.001f,
                $"LUMEN delta for {zero}v{lumen} over {seconds}s was {delta.y}, expected {expectedLumen}");
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Hot Zone simultaneous scoring")]
        public static void Run()
        {
            Expect(0,0,1f,0f,0f);
            Expect(1,0,1f,1f,0f);
            Expect(5,0,1f,5f,0f);
            Expect(0,3,1f,0f,3f);

            // Both teams continue scoring while sharing the zone.
            Expect(1,1,1f,1f,1f);
            Expect(3,1,1f,3f,1f);
            Expect(2,5,1f,2f,5f);
            Expect(4,2,.5f,2f,1f);

            // Defensive input behavior.
            Expect(-2,3,1f,0f,3f);
            Expect(2,3,-1f,0f,0f);

            Debug.Log("Hot Zone scoring regression passed: every hero scores independently, including simultaneous ZERO/LUMEN occupancy.");
        }
    }
}
#endif
