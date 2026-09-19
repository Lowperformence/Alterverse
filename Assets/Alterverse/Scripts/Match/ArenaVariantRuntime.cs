using UnityEngine;
namespace Alterverse.Match
{
    public enum ArenaVariant { Moon=0, Mars=1, GreenPlanet=2 }

    /// <summary>Persistent rotating visual arena choice. Gameplay geometry remains shared.</summary>
    public static class ArenaVariantRuntime
    {
        private const string NextKey="DF_NextArenaVariant";
        public static ArenaVariant Current { get; private set; }=ArenaVariant.Moon;
        public static bool IsMars=>Current==ArenaVariant.Mars;
        public static bool IsGreenPlanet=>Current==ArenaVariant.GreenPlanet;

        public static void BeginMatch()
        {
            int next=Mathf.Clamp(PlayerPrefs.GetInt(NextKey,0),0,2);
            Current=(ArenaVariant)next;
            PlayerPrefs.SetInt(NextKey,(next+1)%3);
            PlayerPrefs.Save();
        }
    }
}