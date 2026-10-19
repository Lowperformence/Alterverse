#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEditor;
using UnityEngine;

namespace Alterverse.UI
{
    public static class MatchResultScreenChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Result presentation (no save writes)")]
        public static void Run()
        {
            foreach(var cosmetic in new[]{UniversalHeroCosmetic.Original,UniversalHeroCosmetic.Voidfall,UniversalHeroCosmetic.PlagueWings,UniversalHeroCosmetic.BetaSupporterWings,UniversalHeroCosmetic.DeluxeWings,UniversalHeroCosmetic.AngelWings,UniversalHeroCosmetic.RgbRiftWings})
            {
                var data=Fixture(0);data.BonusPercent=WingMatchRewardBonus.Percent(cosmetic);data.BonusName=WingMatchRewardBonus.DisplayName(cosmetic);
                data.Xp=123;data.Shards=321;data.BaseXp=100;data.XpBonus=23;data.PassXpGained=100;
                data.Prepare();
                Assert(data.XpText=="+123"&&data.ShardsText=="+321","Receipt totals must be displayed directly, not recomputed from cosmetic percent.");
                Assert(data.PassXpGained==100,"Preparing a wing badge must not multiply Battle Pass XP.");
                Assert(data.BonusPercent>0?data.BonusBadge.Contains(WingMatchRewardBonus.PercentLabel(data.BonusPercent)+"%"):data.BonusBadge=="BONUS REWARD","Non-wing reward bonuses stay generic so hidden difficulty is not exposed.");
            }
            var defeat=Fixture(4);defeat.Prepare();Assert(defeat.Result=="DEFEAT","Result is from the player's perspective.");
            var capped=Fixture(7);capped.Prepare();Assert(capped.AccountText=="MAX LEVEL"&&capped.XpText=="+1","Cap-limited actual XP must not be replaced with an estimated bonus.");
            var retry=Fixture(10);retry.Prepare();Assert(!retry.Ready&&retry.XpText=="—"&&retry.Status.Contains("RETRYING"),"Retry stays pending and does not claim rewards were received.");
            var pending=Fixture(9);pending.Prepare();Assert(!pending.Ready&&pending.Status=="FINALIZING REWARDS…","Pending state is explicit.");
            var large=Fixture(8);large.Prepare();Assert(large.XpText.Length<=7&&large.Xp==int.MaxValue,"Large total abbreviation must not change the underlying receipt.");
            var drop=Fixture(5);drop.Prepare();Assert(drop.DropName==BattleItemInventory.Name(BattleItemType.TeamHeal),"Drop identity comes from the existing inventory catalog.");
            var noDrop=Fixture(6);noDrop.Prepare();Assert(noDrop.DropName=="NO BATTLE ITEM DROP","No drop must not appear as a granted item.");
            foreach(var size in new[]{new Vector2(1920,1080),new Vector2(2160,1080),new Vector2(2340,1080),new Vector2(2400,1080),new Vector2(800,360),new Vector2(640,360),new Vector2(480,270),new Vector2(360,640)})
            {
                CheckLayout(size.x,size.y);
                CheckLayout(size.x-40,size.y-20);
            }
            Debug.Log("DIMENSIONS FRONT: result snapshot and layout assertions passed. No rewards or PlayerPrefs were written. Device rendering and retry integration still require Play mode.");
        }
        private static void CheckLayout(float width,float height)
        {
            float scale=MatchResultLayout.Scale(new Rect(0,0,width,height));float w=width/scale,h=height/scale;
            Assert(h>=250,"Fixtures include down to 250 usable pixels of landscape height.");
            var layout=new MatchResultLayout(w,h);
            Assert(scale>=1,"Small displays must not shrink below logical pixel size.");
            Assert(layout.Header.y>=0&&layout.Footer.yMax<=h&&layout.Footer.height>=48,"Result and touch footer stay inside safe bounds.");
            Assert(layout.Kda.y>=layout.Header.yMax&&layout.Xp.y>=layout.Kda.yMax,"Top hierarchy cannot overlap.");
            Assert(layout.Shards.yMax<=layout.Footer.y,"Main rewards must remain above Continue.");
            Assert(layout.Xp.x>=0&&layout.Shards.xMax<=w,"Reward cards stay horizontally inside the safe area.");
            Assert(layout.Stacked?layout.Shards.y>=layout.Xp.yMax:layout.Shards.x>=layout.Xp.xMax,"Reward cards must wrap or separate.");
        }
        private static void Assert(bool value,string message){if(!value)throw new InvalidOperationException("Result UI regression: "+message);}
        internal static MatchResultScreenData Fixture(int scenario)
        {
            var cosmetic=scenario==1?UniversalHeroCosmetic.Voidfall:scenario==2?UniversalHeroCosmetic.PlagueWings:scenario==3?UniversalHeroCosmetic.BetaSupporterWings:UniversalHeroCosmetic.Original;
            var data=new MatchResultScreenData
            {
                Won=scenario!=4,Ready=scenario!=9&&scenario!=10,Retrying=scenario==10,Difficulty="EXPERT",Hero="Ione Vaska",
                Kills=12,Deaths=3,Assists=8,Objectives=4,Xp=120,Shards=144,BaseXp=100,XpBonus=20,BaseShards=120,ShardBonus=24,
                BonusPercent=WingMatchRewardBonus.Percent(cosmetic),BonusName=WingMatchRewardBonus.DisplayName(cosmetic),
                Drop=scenario==5?BattleItemType.TeamHeal:BattleItemType.None,
                AccountLevel=22,AccountInto=1840,AccountRequired=2200,AccountProgress=.836f,
                PassLevel=14,PassInto=120,PassRequired=342,PassProgress=.351f,PassXpGained=100,PassLevelUp=true
            };
            // Fixtures model granted receipts; they do not call any grant/drop/transaction function.
            int percent=WingMatchRewardBonus.CalculateBonus(100,cosmetic);
            data.XpBonus=percent;data.Xp=100+percent;data.ShardBonus=percent;data.BaseShards=100;data.Shards=100+percent;
            if(scenario==7){data.AccountMax=true;data.AccountLevel=PlayerExperience.MaxLevel;data.Xp=1;data.BaseXp=1;data.XpBonus=0;data.AccountProgress=1;data.PassXpGained=1;}
            if(scenario==8){data.Xp=int.MaxValue;data.Shards=int.MaxValue;data.BaseXp=int.MaxValue;data.BaseShards=int.MaxValue;data.Kills=123456;data.Deaths=10000;data.Assists=999999;}
            if(scenario==11)data.Drops=new[]{new MatchResultScreenData.DropReward(BattleItemType.Heal,1),new MatchResultScreenData.DropReward(BattleItemType.Shield,2),new MatchResultScreenData.DropReward(BattleItemType.TeamHeal,1)};
            data.Details=new[]{new MatchResultScreenData.Metric("PLAYED HERO",data.Hero),new MatchResultScreenData.Metric("EXACT ACCOUNT XP",data.Xp.ToString("N0")),new MatchResultScreenData.Metric("EXACT SHARDS",data.Shards.ToString("N0")),new MatchResultScreenData.Metric("DAMAGE DEALT","12,450"),new MatchResultScreenData.Metric("MATCH MVP","A very long example player name")};
            return data;
        }
    }
    public sealed class MatchResultPreviewWindow : EditorWindow
    {
        private static readonly string[] Names={"Victory / Original","Voidfall +5%","Plague +10%","Beta +20%","Defeat","Team Heal drop","No drop","Account cap","Large numbers","Finalizing","Retry","Multiple drops (fixture)"};
        private static readonly Vector2[] Sizes={new(800,360),new(640,360),new(1920,1080),new(2160,1080),new(2340,1080),new(2400,1080),new(360,640)};
        private static readonly string[] SizeNames={"800×360","640×360","16:9","18:9","19.5:9","20:9","360×640"};
        private MatchResultScreen screen;
        private int scenario,size;
        private bool inset;
        [MenuItem("DIMENSIONS FRONT/Checks/Result screen visual preview")]
        private static void Open(){var window=GetWindow<MatchResultPreviewWindow>();window.titleContent=new GUIContent("Result preview");window.minSize=new Vector2(660,450);window.Show();}
        private void OnEnable(){screen=new MatchResultScreen();screen.SetData(MatchResultScreenChecks.Fixture(scenario));}
        private void OnGUI()
        {
            int next=EditorGUI.Popup(new Rect(10,8,230,24),scenario,Names);
            size=EditorGUI.Popup(new Rect(250,8,130,24),size,SizeNames);
            inset=EditorGUI.ToggleLeft(new Rect(390,8,170,24),"Safe-area insets",inset);
            if(next!=scenario){scenario=next;screen.SetData(MatchResultScreenChecks.Fixture(scenario));}
            Vector2 pixels=Sizes[size];if(inset)pixels-=new Vector2(40,20);
            float scale=MatchResultLayout.Scale(new Rect(Vector2.zero,pixels));Vector2 logical=pixels/scale;
            float fit=Mathf.Min((position.width-20)/logical.x,(position.height-48)/logical.y);
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(new Vector3(10,42,0),Quaternion.identity,Vector3.one*fit);
            try{screen.Draw(logical.x,logical.y);}finally{GUI.matrix=old;}
            // Continue has no navigation or reward callback in this presentation-only fixture.
        }
    }
}
#endif
