using System;
using System.Globalization;
using Alterverse.Profile;
using Alterverse.Multiplayer;
using Rewards=Alterverse.Match.MatchRewardService;

static class Regression
{
    static void Check(bool valid,string scenario)
    { if(!valid)throw new InvalidOperationException(scenario); }
    static void Match(string id,int kills,int deaths,bool network)
    {
        CoopAuthority.Active=network;
        Rewards.GrantMatchReward(true,kills,0,0,id,deaths:deaths);
    }
    static void Main()
    {
        AccountProfilePrefs.Account="all-modes";
        Check(ProgressionSummary.KillDeathRatioLabel=="—","No match statistics yet");
        // Very different match ratios verify that totals are divided, never averaged.
        Match("solo",4,1,false);
        Match("coop",3,8,true);
        Match("online-casual",1,1,true);
        Match("online-ranked",0,0,true);
        Check(ProgressionSummary.Kills==8&&ProgressionSummary.Deaths==10,"All modes aggregate");
        Check(Alterverse.Profile.MatchRewardService.KillDeathKills==8&&Alterverse.Profile.MatchRewardService.KillDeathDeaths==10,"Paired counters include all modes");
        Check(ProgressionSummary.Matches==4,"All match results counted");
        Check(Math.Abs(ProgressionSummary.KillDeathRatio-.8)<.000001,"Total kills / total deaths");
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
        Check(ProgressionSummary.KillDeathRatioLabel=="0.8","One decimal with stable separator");
        Match("online-ranked",100,100,true);
        Check(ProgressionSummary.Kills==8&&ProgressionSummary.Deaths==10,"Receipt retry does not double count");
        Check(ProgressionSummary.KillDeathRatioLabel=="0.8","Receipt retry preserves ratio");

        AccountProfilePrefs.Account="interrupted";
        AccountProfilePrefs.FailAfterSaves=2;
        try { Match("recovered",6,3,true); throw new Exception("Save did not interrupt"); }
        catch(InvalidOperationException) { }
        AccountProfilePrefs.FailAfterSaves=-1;
        Match("recovered",6,3,true);
        Check(ProgressionSummary.Kills==6&&ProgressionSummary.Deaths==3,"Journal replay restores absolute totals once");
        Check(ProgressionSummary.KillDeathRatioLabel=="2.0","Journal replay restores paired ratio once");
        Check(ProgressionSummary.Matches==1,"Interrupted match counted once");

        AccountProfilePrefs.Account="deathless";
        Match("perfect",4,0,false);
        Check(ProgressionSummary.KillDeathRatioLabel=="∞"&&ProgressionSummary.Deaths==0,"Actual zero deaths have no fictional denominator");
        AccountProfilePrefs.Account="negative";
        Match("invalid",-4,-3,false);
        Check(ProgressionSummary.Kills==0&&ProgressionSummary.Deaths==0,"Negative inputs clamped");
        Check(ProgressionSummary.KillDeathRatioLabel=="0.0","Zero kills and zero deaths");

        AccountProfilePrefs.Account="overflow";
        AccountProfilePrefs.SetInt("DF_LifetimeKills",int.MaxValue-1);
        AccountProfilePrefs.SetInt("DF_LifetimeDeaths",int.MaxValue-1);
        AccountProfilePrefs.SetInt("DF_KDKills_v1",int.MaxValue-1);
        AccountProfilePrefs.SetInt("DF_KDDeaths_v1",int.MaxValue-1);
        Match("cap",4,8,true);
        Check(ProgressionSummary.Kills==int.MaxValue&&ProgressionSummary.Deaths==int.MaxValue,"Counters saturate without overflow");
        Check(ProgressionSummary.KillDeathRatioLabel=="1.0","Paired counters saturate without overflow");

        AccountProfilePrefs.Account="legacy";
        AccountProfilePrefs.SetInt("DF_LifetimeKills",26);
        Check(ProgressionSummary.Kills==26&&ProgressionSummary.Deaths==0,"Existing kills preserved without inventing historical deaths");
        Check(ProgressionSummary.KillDeathRatioLabel=="—","Legacy 26 kills are not shown as a 26 K/D");
        Match("first-tracked",2,4,false);
        Check(ProgressionSummary.Kills==28&&ProgressionSummary.Deaths==4,"Legacy profile gains death counter");
        Check(ProgressionSummary.KillDeathRatioLabel=="0.5","Unknown historic kills cannot inflate the tracked ratio");

        AccountProfilePrefs.Account="unpaired-history";
        AccountProfilePrefs.SetInt("DF_LifetimeKills",26);
        AccountProfilePrefs.SetInt("DF_LifetimeDeaths",5);
        Check(ProgressionSummary.KillDeathRatioLabel=="—","Counters with different start dates are not divided");
        Match("same-period",4,5,true);
        Check(ProgressionSummary.KillDeathRatioLabel=="0.8","Automatic tracking excludes unpaired history");

        AccountProfilePrefs.Account="incomplete-pair";
        AccountProfilePrefs.SetInt("DF_KDKills_v1",26);
        Check(ProgressionSummary.KillDeathRatioLabel=="—","Incomplete restored pair stays unknown");
        Match("repair",4,5,true);
        Check(ProgressionSummary.KillDeathRatioLabel=="0.8","Incomplete pair restarts both counts together");
        AccountProfilePrefs.Account="all-modes";
        Check(ProgressionSummary.KillDeathRatioLabel=="0.8","Accounts remain isolated");
        Console.WriteLine("PASS: lifetime K/D aggregation, receipts, interrupted saves, culture, zero deaths, legacy accounts and overflow.");
    }
}
