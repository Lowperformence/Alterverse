using System.Collections.Generic;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Match
{
    public enum ArenaVariant { Moon=0, Mars=1, GreenPlanet=2, Noxara=3, Kryon=4, Earth=5, Pyraxis=6, Neptune=7, Dark00=8, DiamondCore=9, GJ504b=10, Wasp76b=11, Trappist1e=12, Toi715b=13 }

    /// <summary>Persistent player-selected arena. Locked premium arenas always fall back to Moon.</summary>
    public static class ArenaVariantRuntime
    {
        internal const string SelectedKey="DF_SelectedArenaVariant";
        internal const string ShuffleKey="DF_ArenaShuffle";
        internal const string LastShuffleKey="DF_LastShuffledArena";
        public static ArenaVariant Current { get; private set; }=ArenaVariant.Moon;
        // DARK 00 intentionally inherits Moon gameplay/geology defaults; IsDark00 remains available for its visual overrides.
        public static bool IsMoon=>Current==ArenaVariant.Moon||Current==ArenaVariant.Dark00;
        public static bool IsMars=>Current==ArenaVariant.Mars;
        public static bool IsGreenPlanet=>Current==ArenaVariant.GreenPlanet;
        public static bool IsNoxara=>Current==ArenaVariant.Noxara;
        public static bool IsKryon=>Current==ArenaVariant.Kryon;
        public static bool IsEarth=>Current==ArenaVariant.Earth;
        public static bool IsPyraxis=>Current==ArenaVariant.Pyraxis;
        public static bool IsNeptune=>Current==ArenaVariant.Neptune;
        public static bool IsDark00=>Current==ArenaVariant.Dark00;
        public static bool IsGJ504b=>Current==ArenaVariant.GJ504b;
        public static bool IsDiamondCore=>Current==ArenaVariant.DiamondCore;
        public static bool IsWasp76b=>Current==ArenaVariant.Wasp76b;
        public static bool IsTrappist1e=>Current==ArenaVariant.Trappist1e;
        public static bool IsToi715b=>Current==ArenaVariant.Toi715b;
        public static bool IsExoplanet=>IsWasp76b||IsTrappist1e||IsToi715b;

        public static ArenaVariant Selected
        {
            get
            {
                int raw=Alterverse.Profile.AccountProfilePrefs.GetInt(SelectedKey,(int)ArenaVariant.Moon);
                bool valid=System.Enum.IsDefined(typeof(ArenaVariant),raw);
                ArenaVariant selected=valid?(ArenaVariant)raw:ArenaVariant.Moon;
                if(!valid||!ArenaUnlockInventory.Owned(selected))
                {
                    selected=ArenaVariant.Moon;
                    Alterverse.Profile.AccountProfilePrefs.SetInt(SelectedKey,(int)selected);
                    Alterverse.Profile.AccountProfilePrefs.Save();
                }
                return selected;
            }
        }

        public static bool ShuffleEnabled=>Alterverse.Profile.AccountProfilePrefs.GetInt(ShuffleKey,1)==1;
        public static bool CanSelect(ArenaVariant variant)=>System.Enum.IsDefined(typeof(ArenaVariant),variant)&&
            (variant==ArenaVariant.Moon||ArenaUnlockInventory.Owned(variant));

        public static bool Select(ArenaVariant variant)
        {
            if(!CanSelect(variant))return false;
            Alterverse.Profile.AccountProfilePrefs.SetInt(SelectedKey,(int)variant);
            Alterverse.Profile.AccountProfilePrefs.SetInt(ShuffleKey,0);
            Alterverse.Profile.AccountProfilePrefs.Save();
            return true;
        }

        public static void SetShuffleEnabled(bool enabled)
        {
            Alterverse.Profile.AccountProfilePrefs.SetInt(ShuffleKey,enabled?1:0);
            Alterverse.Profile.AccountProfilePrefs.Save();
        }

        public static ArenaVariant RandomOwnedArena(ArenaVariant avoid)
        {
            List<ArenaVariant> owned=new();
            for(int i=0;i<ArenaShopCatalog.Items.Length;i++)
            {
                ArenaVariant candidate=ArenaShopCatalog.Items[i].Variant;
                if(ArenaUnlockInventory.Owned(candidate))owned.Add(candidate);
            }
            if(owned.Count==0)return ArenaVariant.Moon;
            if(owned.Count>1)owned.Remove(avoid);
            ArenaVariant picked=owned[Random.Range(0,owned.Count)];
            Alterverse.Profile.AccountProfilePrefs.SetInt(LastShuffleKey,(int)picked);
            Alterverse.Profile.AccountProfilePrefs.Save();
            return picked;
        }

        public static void EnsureValidSelection()
        {
            _=Selected;
        }

        public static void BeginMatch()
        {
            if(!ShuffleEnabled)
            {
                Current=Selected;
                return;
            }

            int rawLast=Alterverse.Profile.AccountProfilePrefs.GetInt(LastShuffleKey,int.MinValue);
            ArenaVariant avoid=Selected;
            for(int i=0;i<ArenaShopCatalog.Items.Length;i++)
                if((int)ArenaShopCatalog.Items[i].Variant==rawLast){avoid=ArenaShopCatalog.Items[i].Variant;break;}
            Current=RandomOwnedArena(avoid);
        }

        public static void BeginCoopMatch(ArenaVariant arena)
        {
            // A guest may temporarily play the host-selected arena without receiving ownership.
            // Never persist this override to Alterverse.Profile.AccountProfilePrefs.
            Current=System.Enum.IsDefined(typeof(ArenaVariant),arena)?arena:ArenaVariant.Moon;
        }
    }
}


