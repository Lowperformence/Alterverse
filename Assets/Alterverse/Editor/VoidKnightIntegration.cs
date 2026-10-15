using System;
using System.Linq;
using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Shop;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorTools
{
 public static class VoidKnightIntegration
 {
  [MenuItem("DIMENSIONS FRONT/Checks/Void Knight integration (non-destructive)")]
  public static void Check()
  {
   Require((int)HeroId.VoidKnight==9&&HeroIdentity.HeroCount==10,"Serialized roster IDs changed.");
   Require(HeroIdentity.AttackDamage(HeroId.VoidKnight)==18,"Greatsword must deal 18 basic damage.");
   Require(HeroIdentity.BaseHealth(HeroId.VoidKnight)<HeroIdentity.BaseHealth(HeroId.Kaelon),"Knight must have less health than tank.");
   Require(!HeroAttackDelivery.IsRanged(HeroId.VoidKnight),"Greatsword must remain melee.");
   foreach(var opponent in new[]{HeroId.Veyr,HeroId.Timmy,HeroId.Kaelon})Require(HeroCombatProfile.StatsFor(HeroId.VoidKnight).AttackRange>HeroCombatProfile.StatsFor(opponent).AttackRange,"Insufficient greatsword reach.");
   Require(!HeroUnlockCatalog.Released(HeroId.VoidKnight)&&!HeroUnlockInventory.Owned(HeroId.VoidKnight),"Knight must remain preview-only until release.");
   Require(HeroUnlockCatalog.Purchasable(HeroId.VoidKnight)&&HeroUnlockCatalog.Price(HeroId.VoidKnight)==1600&&HeroUnlockCatalog.PurchaseSku(HeroId.VoidKnight)=="hero_void_knight"&&HeroUnlockCatalog.OwnedKey(HeroId.VoidKnight)=="DF_HeroOwned_VoidKnight","Knight's 1600-shard unlock mapping is incomplete.");
   var model=Resources.Load<GameObject>("AuthoredHeroes/VoidKnight");Require(model!=null,"Missing imported VoidKnight.fbx.");
   Require(model.GetComponentsInChildren<Renderer>(true).Any(r=>r.name=="DF_WeaponR"),"Greatsword renderer missing.");
   var clips=Resources.LoadAll<AnimationClip>("AuthoredHeroes/VoidKnight");
   foreach(string name in new[]{"Idle","Run","Attack1","Attack2","Attack3","Skill0","Skill1","Skill2","Ultimate","Counter","Hit","Death"})Require(clips.Any(c=>c.name.Split('|').Last()==name),"Missing animation "+name);
   for(int i=0;i<5;i++)Require(Resources.Load<Texture2D>("VoidKnight/Icons/"+i)!=null,"Missing attack icon "+i);
   foreach(string action in new[]{"Basic","Q","W","E","R"})
   {
    Require(Resources.Load<AudioClip>("Audio/Heroes/VoidKnight/"+action+"_Cast")!=null,"Missing cast sound "+action);
    Require(Resources.Load<AudioClip>("Audio/Heroes/VoidKnight/"+action+"_Impact")!=null,"Missing impact sound "+action);
    if(action!="W")Require(Resources.Load<AudioClip>("Audio/Heroes/VoidKnight/"+action+"_Windup")!=null,"Missing preparation sound "+action);
   }
   Require(HeroCombatAudio.CueCount==150,"Audio cue bank incomplete.");
   Require(AimingSettings.Current.For(HeroId.VoidKnight,2).aimType==AimType.Dash,"Lunge aiming incorrect.");
   Require(AimingSettings.Current.For(HeroId.VoidKnight,3).aimType==AimType.AreaCone,"Cleave aiming incorrect.");
   Debug.Log("DF: Void Knight model, roster, balance, twelve animations, five icons and sounds verified. Play-mode and device tests are separate.");
  }
  private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
 }
}
