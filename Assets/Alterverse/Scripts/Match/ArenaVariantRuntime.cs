using UnityEngine;
namespace Alterverse.Match
{
    public enum ArenaVariant { Moon=0, Mars=1 }

    /// <summary>Persistent alternating visual arena choice. Gameplay geometry remains shared.</summary>
    public static class ArenaVariantRuntime
    {
        private const string NextKey="DF_NextArenaVariant";
        public static ArenaVariant Current { get; private set; }=ArenaVariant.Moon;
        public static bool IsMars=>Current==ArenaVariant.Mars;

        public static void BeginMatch()
        {
            int next=Mathf.Clamp(PlayerPrefs.GetInt(NextKey,0),0,1);
            Current=(ArenaVariant)next;
            PlayerPrefs.SetInt(NextKey,next==0?1:0);
            PlayerPrefs.Save();
        }
    }
}