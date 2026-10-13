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
        Check(ProgressionSummary.KillDeathRatioLabel=="0.0","Empty profile");
        // Very different match ratios verify that totals are divided, never averaged.
        Match("solo",4,1,false);
        Match("coop",3,8,true);
        Match("online-casual",1,1,true);
        Match("online-ranked",0,0,true);
        Check(ProgressionSummary.Kills==8&&ProgressionSummary.Deaths==10,"All modes aggregate");
        Check(ProgressionSummary.Matches==4,"All match results counted");
        Check(Math.Abs(ProgressionSummary.KillDeathRatio-.8)<.000001,"Total kills / total deaths");
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
        Check(ProgressionSummary.KillDeathRatioLabel=="0.8","One decimal with stable separator");
        Match("online-ranked",100,100,true);
        Check(ProgressionSummary.Kills==8&&ProgressionSummary.Deaths==10,"Receipt retry does not double count");

        AccountProfilePrefs.Account="interrupted";
        AccountProfilePrefs.FailAfterSaves=2;
        try { Match("recovered",6,3,true); throw new Exception("Save did not interrupt"); }
        catch(InvalidOperationException) { }
        AccountProfilePrefs.FailAfterSaves=-1;
        Match("recovered",6,3,true);
        Check(ProgressionSummary.Kills==6&&ProgressionSummary.Deaths==3,"Journal replay restores absolute totals once");
        Check(ProgressionSummary.Matches==1,"Interrupted match counted once");

        AccountProfilePrefs.Account="deathless";
        Match("perfect",4,0,false);
        Check(ProgressionSummary.KillDeathRatioLabel=="4.0"&&ProgressionSummary.Deaths==0,"No division by zero or fictional persisted death");
        AccountProfilePrefs.Account="negative";
        Match("invalid",-4,-3,false);
        Check(ProgressionSummary.Kills==0&&ProgressionSummary.Deaths==0,"Negative inputs clamped");

        AccountProfilePrefs.Account="overflow";
        AccountProfilePrefs.SetInt("DF_LifetimeKills",int.MaxValue-1);
        AccountProfilePrefs.SetInt("DF_LifetimeDeaths",int.MaxValue-1);
        Match("cap",4,8,true);
        Check(ProgressionSummary.Kills==int.MaxValue&&ProgressionSummary.Deaths==int.MaxValue,"Counters saturate without overflow");

        AccountProfilePrefs.Account="legacy";
        AccountProfilePrefs.SetInt("DF_LifetimeKills",26);
        Check(ProgressionSummary.Kills==26&&ProgressionSummary.Deaths==0,"Existing kills preserved without inventing historical deaths");
        Match("first-tracked",2,4,false);
        Check(ProgressionSummary.Kills==28&&ProgressionSummary.Deaths==4,"Legacy profile gains death counter");
        AccountProfilePrefs.Account="all-modes";
        Check(ProgressionSummary.KillDeathRatioLabel=="0.8","Accounts remain isolated");
        Console.WriteLine("PASS: lifetime K/D aggregation, receipts, interrupted saves, culture, zero deaths, legacy accounts and overflow.");
    }
}
