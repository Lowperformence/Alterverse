using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Shop;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    public static class AndromedaTimmyIntegration
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Andromeda Timmy integration (non-destructive)")]
        private static void Check()
        {
            if(TimmyBalance.TotalPoints!=100)throw new System.InvalidOperationException("Timmy's design budget must sum to 100.");
            if((int)HeroId.Timmy!=8)throw new System.InvalidOperationException("Existing serialized hero IDs must remain stable.");
            if(HeroIdentity.HeroCount<9)throw new System.InvalidOperationException("Timmy must be included in the regular roster.");
            if(HeroUnlockCatalog.Price(HeroId.Timmy)!=1900||HeroUnlockCatalog.PurchaseSku(HeroId.Timmy)!="hero_timmy")
                throw new System.InvalidOperationException("Timmy shop listing must cost 1,900 shards.");
            if(!HeroUnlockCatalog.Released(HeroId.Timmy)&&HeroUnlockInventory.Owned(HeroId.Timmy))
                throw new System.InvalidOperationException("Unreleased Timmy must remain unavailable for deployment.");
            if(HeroAbilityKit.LoadoutFor(HeroId.Timmy).Ultimate.Name!="Scratch")throw new System.InvalidOperationException("Timmy catalog incomplete.");
            if(Resources.Load<GameObject>("AuthoredHeroes/Timmy")==null)throw new System.InvalidOperationException("Rebuild/import Timmy.fbx.");
            for(int i=0;i<5;i++)if(Resources.Load<Texture2D>("AndromedaTimmy/Icons/"+i)==null)throw new System.InvalidOperationException("Timmy icon missing: "+i);
            if(AimingSettings.Current.For(HeroId.Timmy,1).aimType!=AimType.Area||AimingSettings.Current.For(HeroId.Timmy,3).aimType!=AimType.Dash)
                throw new System.InvalidOperationException("Timmy targeting classification incorrect.");
            Debug.Log("DF: Timmy regular roster, 100-point draft budget, model, icons and aiming types verified.");
        }
    }
}

