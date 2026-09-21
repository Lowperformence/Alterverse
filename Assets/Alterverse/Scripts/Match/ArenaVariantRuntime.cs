using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Match
{
    public enum ArenaVariant { Moon=0, Mars=1, GreenPlanet=2, Noxara=3, Kryon=4 }

    /// <summary>Persistent player-selected arena. Locked premium arenas always fall back to Moon.</summary>
    public static class ArenaVariantRuntime
    {
        internal const string SelectedKey="DF_SelectedArenaVariant";
        public static ArenaVariant Current { get; private set; }=ArenaVariant.Moon;
        public static bool IsMars=>Current==ArenaVariant.Mars;
        public static bool IsGreenPlanet=>Current==ArenaVariant.GreenPlanet;
        public static bool IsNoxara=>Current==ArenaVariant.Noxara;
        public static bool IsKryon=>Current==ArenaVariant.Kryon;

        public static ArenaVariant Selected
        {
            get
            {
                int raw=PlayerPrefs.GetInt(SelectedKey,(int)ArenaVariant.Moon);
                ArenaVariant selected=raw>=0&&raw<=4?(ArenaVariant)raw:ArenaVariant.Moon;
                if(!ArenaUnlockInventory.Owned(selected)||raw<0||raw>4)
                {
                    selected=ArenaVariant.Moon;
                    PlayerPrefs.SetInt(SelectedKey,(int)selected);
                    PlayerPrefs.Save();
                }
                return selected;
            }
        }

        public static bool CanSelect(ArenaVariant variant)=>variant==ArenaVariant.Moon||ArenaUnlockInventory.Owned(variant);

        public static bool Select(ArenaVariant variant)
        {
            if(!CanSelect(variant))return false;
            PlayerPrefs.SetInt(SelectedKey,(int)variant);
            PlayerPrefs.Save();
            return true;
        }

        public static void EnsureValidSelection()
        {
            _=Selected;
        }

        public static void BeginMatch()
        {
            // There is deliberately no automatic rotation here. A match can only use the
            // player's persisted, ownership-validated selection.
            Current=Selected;
        }
    }
}