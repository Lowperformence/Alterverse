using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    public static class AndromedaTimmyPreview
    {
        private const string Key="DF.Season2.Timmy.EditorPreview";
        [InitializeOnLoadMethod]
        private static void Restore()=>Season2Content.EditorPreviewEnabled=EditorPrefs.GetBool(Key,false);
        [MenuItem("DIMENSIONS FRONT/Season 2/Enable Timmy preview")]
        private static void Enable()
        {EditorPrefs.SetBool(Key,true);Restore();Debug.Log("DF: Andromeda Timmy is available in the editor hero menu. Player builds retain the Season 1 roster.");}
        [MenuItem("DIMENSIONS FRONT/Season 2/Disable Timmy preview")]
        private static void Disable()
        {EditorPrefs.SetBool(Key,false);Restore();PlayerHeroSelection.Selected=HeroId.Veyr;}
        [MenuItem("DIMENSIONS FRONT/Checks/Andromeda Timmy integration (non-destructive)")]
        private static void Check()
        {
            if(TimmyBalance.TotalPoints!=100)throw new System.InvalidOperationException("Timmy's design budget must sum to 100.");
            if((int)HeroId.Timmy!=8)throw new System.InvalidOperationException("Existing serialized hero IDs must remain stable.");
            if(HeroAbilityKit.LoadoutFor(HeroId.Timmy).Ultimate.Name!="Scratch")throw new System.InvalidOperationException("Timmy catalog incomplete.");
            if(Resources.Load<GameObject>("AuthoredHeroes/Timmy")==null)throw new System.InvalidOperationException("Rebuild/import Timmy.fbx.");
            for(int i=0;i<5;i++)if(Resources.Load<Texture2D>("AndromedaTimmy/Icons/"+i)==null)throw new System.InvalidOperationException("Timmy icon missing: "+i);
            if(AimingSettings.Current.For(HeroId.Timmy,1).aimType!=AimType.Area||AimingSettings.Current.For(HeroId.Timmy,3).aimType!=AimType.Dash)
                throw new System.InvalidOperationException("Timmy targeting classification incorrect.");
            Debug.Log("DF: Timmy catalog, 100-point draft budget, model, icons and aiming types verified. Run contact/interrupt playtests before Season 2 release.");
        }
    }
}
