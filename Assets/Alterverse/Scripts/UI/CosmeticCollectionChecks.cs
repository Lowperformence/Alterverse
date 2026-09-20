#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Shop;
using UnityEditor;
using UnityEngine;

namespace Alterverse.UI
{
    public static class CosmeticCollectionChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Cosmetic collection (non-destructive)")]
        public static void Run()
        {
            HeroId previousHero=PlayerHeroSelection.Selected;
            var previousData=CosmeticProfile.TestData;
            var previousWrites=CosmeticProfile.LastTestTransaction;
            // Preserve the live match snapshot; tests must not change its reward.
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            var snapshot=typeof(WingMatchRewardBonus).GetField("matchCosmetic",flags);
            var hasSnapshot=typeof(WingMatchRewardBonus).GetField("hasSnapshot",flags);
            object previousSnapshot=snapshot.GetValue(null),previousHasSnapshot=hasSnapshot.GetValue(null);
            try
            {
                CheckMigration();CheckPurchaseAndEquip();CheckGrid();
                Debug.Log("DIMENSIONS FRONT: cosmetic migration, purchases, per-hero equip, match snapshot and grid geometry checks passed. Touch/render validation remains a device check.");
            }
            finally
            {
                CosmeticProfile.TestData=previousData;CosmeticProfile.LastTestTransaction=previousWrites;
                PlayerHeroSelection.Select(previousHero);
                snapshot.SetValue(null,previousSnapshot);hasSnapshot.SetValue(null,previousHasSnapshot);
            }
        }
        private static void Assert(bool ok,string message){if(!ok)throw new InvalidOperationException("Cosmetic regression: "+message);}
        private static void CheckMigration()
        {
            foreach(var legacy in new[]{UniversalHeroCosmetic.Voidfall,UniversalHeroCosmetic.PlagueWings,UniversalHeroCosmetic.BetaSupporterWings})
            {
                var data=new Dictionary<string,int>{{"DF_Shards",4321},{UniversalCosmeticSelection.EquippedKey,(int)legacy}};
                data[UniversalSkin.OwnedKey]=1;data[PlagueWings.OwnedKey]=1;data[BetaSupporterWings.OwnedKey]=1;
                CosmeticProfile.TestData=data;PlayerHeroSelection.Select(HeroId.Elyra);
                UniversalCosmeticSelection.EnsureMigrated(HeroId.Elyra);
                Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Elyra)==legacy,"Legacy selection must migrate to the selected hero.");
                Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.Original,"Other heroes start with Original.");
                Assert(data["DF_Shards"]==4321&&data[UniversalSkin.OwnedKey]==1&&data[PlagueWings.OwnedKey]==1&&data[BetaSupporterWings.OwnedKey]==1,"Migration preserves currency and all ownership keys.");
                Assert(CosmeticProfile.LastTestTransaction.Length==7,"Six slots and migration marker form one journal transaction.");
                UniversalCosmeticSelection.EquipFor(HeroId.Elyra,UniversalHeroCosmetic.Original);
                UniversalCosmeticSelection.EnsureMigrated(HeroId.Veyr);
                Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Elyra)==UniversalHeroCosmetic.Original,"Migration must never reapply a legacy loadout.");
            }
            CosmeticProfile.TestData=new Dictionary<string,int>{{UniversalSkin.EquippedKey,1},{UniversalSkin.OwnedKey,1},{UniversalCosmeticSelection.HeroKey(HeroId.Rook),(int)UniversalHeroCosmetic.Voidfall}};
            PlayerHeroSelection.Select(HeroId.Veyr);UniversalCosmeticSelection.EnsureMigrated(HeroId.Veyr);
            Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Voidfall,"Legacy bool-only equip migrates.");
            Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.Voidfall,"Existing per-hero slots survive partial migration.");
            CosmeticProfile.TestData=new Dictionary<string,int>{{UniversalCosmeticSelection.EquippedKey,(int)UniversalHeroCosmetic.Voidfall}};
            UniversalCosmeticSelection.EnsureMigrated(HeroId.Veyr);
            Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Original,"Pending entitlement must not grant ownership.");
            CosmeticProfile.TestData[UniversalSkin.OwnedKey]=1;
            Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Voidfall,"Restored entitlement must recover legacy equipped intent.");
            CosmeticProfile.TestData=new Dictionary<string,int>{{UniversalCosmeticSelection.EquippedKey,999},{"DF_Shards",17}};
            UniversalCosmeticSelection.EnsureMigrated(HeroId.Veyr);
            Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Original&&CosmeticProfile.Balance==17,"Invalid legacy values fall back without a wallet reset.");
        }
        private static void CheckPurchaseAndEquip()
        {
            CosmeticProfile.TestData=new Dictionary<string,int>{{"DF_Shards",14000}};
            PlayerHeroSelection.Select(HeroId.Veyr);UniversalCosmeticSelection.EnsureMigrated(HeroId.Veyr);
            Assert(CosmeticCatalog.All.Count>=4&&CosmeticCatalog.IsOwned(UniversalHeroCosmetic.Original),"Original is always available.");
            Assert(CosmeticCatalog.Find(UniversalHeroCosmetic.Voidfall).Price==2000&&CosmeticCatalog.Find(UniversalHeroCosmetic.PlagueWings).Price==4000&&CosmeticCatalog.Find(UniversalHeroCosmetic.BetaSupporterWings).Price==8000,"Existing prices must not change.");
            foreach(var id in new[]{UniversalHeroCosmetic.Voidfall,UniversalHeroCosmetic.PlagueWings,UniversalHeroCosmetic.BetaSupporterWings})
            {
                int before=CosmeticProfile.Balance;
                Assert(CosmeticCatalog.Purchase(id)==AvatarPurchaseResult.Purchased,"First purchase succeeds.");
                Assert(CosmeticProfile.Balance==before-CosmeticCatalog.Find(id).Price&&CosmeticCatalog.IsOwned(id),"Correct deduction and immediate account ownership.");
                Assert(CosmeticProfile.LastTestTransaction.Length==2,"Ownership and deduction share one transaction.");
                int after=CosmeticProfile.Balance;
                Assert(CosmeticCatalog.Purchase(id)==AvatarPurchaseResult.AlreadyOwned&&CosmeticProfile.Balance==after,"Duplicate purchase cannot deduct shards.");
            }
            Assert(CosmeticProfile.Balance==0,"All existing cosmetics cost 14000 total.");
            Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Original,"Purchase does not silently equip.");
            UniversalCosmeticSelection.EquipFor(HeroId.Veyr,UniversalHeroCosmetic.Voidfall);
            UniversalCosmeticSelection.EquipFor(HeroId.Elyra,UniversalHeroCosmetic.PlagueWings);
            Assert(UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Voidfall&&UniversalCosmeticSelection.EquippedFor(HeroId.Elyra)==UniversalHeroCosmetic.PlagueWings&&UniversalCosmeticSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.Original,"Hero slots remain independent.");
            WingMatchRewardBonus.BeginMatch(HeroId.Elyra);
            UniversalCosmeticSelection.EquipFor(HeroId.Elyra,UniversalHeroCosmetic.BetaSupporterWings);
            Assert(WingMatchRewardBonus.MatchPercent==10,"A live match keeps its initial bonus snapshot.");
            WingMatchRewardBonus.BeginMatch(HeroId.Elyra);Assert(WingMatchRewardBonus.MatchPercent==20,"Next match uses that hero's new selection.");
            Assert(WingMatchRewardBonus.Percent(UniversalHeroCosmetic.Original)==0&&WingMatchRewardBonus.Percent(UniversalHeroCosmetic.Voidfall)==5,"Original and Voidfall bonus unchanged.");
            Assert(UniversalCosmeticSelection.EquipFor(HeroId.Elyra,UniversalHeroCosmetic.Original),"Original can always be equipped.");
            CosmeticProfile.TestData=new Dictionary<string,int>{{"DF_Shards",1999}};
            Assert(CosmeticCatalog.Purchase(UniversalHeroCosmetic.Voidfall)==AvatarPurchaseResult.InsufficientShards&&CosmeticProfile.Balance==1999&&!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.Voidfall),"Insufficient shards cannot grant or charge.");
            Assert(!UniversalCosmeticSelection.EquipFor(HeroId.Veyr,UniversalHeroCosmetic.Voidfall),"Unowned cosmetics cannot be equipped.");
        }
        private static void CheckGrid()
        {
            foreach(float screenWidth in new[]{1920f,2160f,2400f,800f})
            {
                float safeWidth=screenWidth-80f,scale=Mathf.Min(1080f/720f,safeWidth/1200f);
                float width=Mathf.Min(1360,safeWidth/scale-64);
                bool compact=safeWidth<1000;
                float viewport=(compact?width:Mathf.Floor(width*.54f))-24;
                int columns=CollectionGridLayout.Columns(viewport,170,compact?3:4);
                float cell=CollectionGridLayout.CellWidth(viewport,columns);
                Assert(cell>=150&&columns>=2&&columns<=4,"Readable cosmetic cards across safe landscape widths.");
                Assert(columns*cell+(columns-1)*12<=viewport-19,"No horizontal overflow.");
                Assert(CollectionGridLayout.ContentHeight(100,columns,170)>410,"100 cosmetics produce scrollable content.");
                int bagColumns=CollectionGridLayout.Columns(viewport,100,6);
                float size=CollectionGridLayout.CellWidth(viewport,bagColumns);
                Assert(size>=90&&CollectionGridLayout.ContentHeight(30,bagColumns,size)>300,"30 battle entries fit a compact scrollable grid.");
            }
        }
    }
}
#endif
