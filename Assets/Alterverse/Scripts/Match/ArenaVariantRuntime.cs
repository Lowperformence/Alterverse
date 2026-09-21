using System.Collections.Generic;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Match
{
    public enum ArenaVariant { Moon=0, Mars=1, GreenPlanet=2, Noxara=3, Kryon=4 }

    /// <summary>Persistent player-selected arena. Locked premium arenas always fall back to Moon.</summary>
    public static class ArenaVariantRuntime
    {
        internal const string SelectedKey="DF_SelectedArenaVariant";
        internal const string ShuffleKey="DF_ArenaShuffle";
        internal const string LastShuffleKey="DF_LastShuffledArena";
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

        public static bool ShuffleEnabled=>PlayerPrefs.GetInt(ShuffleKey,0)==1;
        public static bool CanSelect(ArenaVariant variant)=>variant==ArenaVariant.Moon||ArenaUnlockInventory.Owned(variant);

        public static bool Select(ArenaVariant variant)
        {
            if(!CanSelect(variant))return false;
            PlayerPrefs.SetInt(SelectedKey,(int)variant);
            PlayerPrefs.SetInt(ShuffleKey,0);
            PlayerPrefs.Save();
            return true;
        }

        public static void SetShuffleEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(ShuffleKey,enabled?1:0);
            PlayerPrefs.Save();
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
            PlayerPrefs.SetInt(LastShuffleKey,(int)picked);
            PlayerPrefs.Save();
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

            int rawLast=PlayerPrefs.GetInt(LastShuffleKey,int.MinValue);
            ArenaVariant avoid=Selected;
            for(int i=0;i<ArenaShopCatalog.Items.Length;i++)
                if((int)ArenaShopCatalog.Items[i].Variant==rawLast){avoid=ArenaShopCatalog.Items[i].Variant;break;}
            Current=RandomOwnedArena(avoid);
        }

        public static void BeginCoopMatch(ArenaVariant arena)
        {
            // A guest may temporarily play the host-selected arena without receiving ownership.
            // Never persist this override to PlayerPrefs.
            Current=System.Enum.IsDefined(typeof(ArenaVariant),arena)?arena:ArenaVariant.Moon;
        }
    }
}