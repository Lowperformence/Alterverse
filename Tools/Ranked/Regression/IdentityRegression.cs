using System;
using Alterverse.Profile;
using UnityEngine;
partial class Regression
{
 static void IdentityCases()
 {
  Check(SafeIdentityCatalog.PlayerNameCapacity==1280000000L,"complete bundled identity catalog loaded");
  Check(SafeIdentityCatalog.ClanNameCapacity==1280000000L,"clan capacity exceeds one million");
  foreach(string bad in new[]{"Custom Name","SolarFox123456","SolarFox12345678","SolarFox１２３４５６７","solarfox0000001","SolarFox0000001\n"})
   Check(!SafeIdentityCatalog.IsPlayerName(bad),"free or malformed name rejected");
  string previous=null;
  for(int i=0;i<100;i++)
  {
   string name=SafeIdentityCatalog.GeneratePlayerName(previous);
   Check(SafeIdentityCatalog.IsPlayerName(name)&&name.Length<=16&&name!=previous,"approved reroll");previous=name;
   Check(SafeIdentityCatalog.IsClanName(SafeIdentityCatalog.GenerateClanName()),"approved clan reroll");
   Check(SafeIdentityCatalog.IsClanTag(SafeIdentityCatalog.GenerateClanTag()),"numeric clan tag");
  }
  AccountProfilePrefs.Detach();
  PlayerPrefs.SetInt("DF_Shards",240);AccountProfilePrefs.GetInt("DF_Shards");
  PlayerPrefs.SetString("DF_PlayerName","Old free name");AccountProfilePrefs.GetString("DF_PlayerName");
  var initial=new[]{new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="SolarFox0000001"}};
  AccountProfilePrefs.Attach("identity-first",0,initial);
  Check(AccountProfilePrefs.GetInt("DF_Shards")==240,"initial server identity preserves first local progress import");
  Check(PlayerProfile.PlayerName=="SolarFox0000001","server identity wins over old local text");
  PlayerProfile.RerollName();
  Check(PlayerProfile.PlayerName==initial[0].text,"registered player cannot reroll name");
  AccountProfilePrefs.SetString("DF_PlayerName","LunarOwl0000002");
  AccountProfilePrefs.DeleteKey("DF_PlayerName");
  Check(PlayerProfile.PlayerName==initial[0].text,"registered identity is immutable");
  AccountProfilePrefs.Attach("identity-other",0,new[]{new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="LunarOwl0000002"}});
  Check(PlayerProfile.PlayerName=="LunarOwl0000002"&&AccountProfilePrefs.GetInt("DF_Shards")==0,"identity and progress isolated between accounts");
  // Older devices may have a dirty cached name; the first cloud assignment wins.
  var stale=new AccountProfilePrefs.State{dirty=true,revision=0,profile=new[]{
   new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="LunarOwl1234567"},
   new AccountProfilePrefs.Entry{key="DF_Shards",kind=0,number=240}}};
  PlayerPrefs.SetString("DF_AccountProfileCache_identity-first",JsonUtility.ToJson(stale));
  AccountProfilePrefs.Attach("identity-first",0,initial);
  Check(PlayerProfile.PlayerName==initial[0].text,"first server-assigned name survives stale offline cache");
  Check(AccountProfilePrefs.GetInt("DF_Shards")==240,"offline progress preserved without replacing name");
  var submitted=AccountProfilePrefs.Capture();
  AccountProfilePrefs.SetInt("DF_Shards",241);
  AccountProfilePrefs.ApplyRemote(1,new[]{
   new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="SolarFox0000001"},
   new AccountProfilePrefs.Entry{key="DF_Shards",kind=0,number=240}},submitted);
  Check(PlayerProfile.PlayerName==initial[0].text&&AccountProfilePrefs.GetInt("DF_Shards")==241,
   "cloud reconciliation retains permanent name and concurrent progress");
  AccountProfilePrefs.Attach("identity-legacy",5,new[]{new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="https://example.com"}});
  Check(PlayerProfile.PlayerName=="CONNECTING..."&&!AccountProfilePrefs.Dirty,
   "legacy text is not published or replaced with a client-generated account name");
  var legacy=new SocialService.Snapshot{
   me=new SocialService.Player{playerId="legacy-player",name="Old name"},
   clan=new SocialService.Clan{clanId="legacy-clan",name="Old clan",tag="FREE",description="https://discord.gg/test",
    members=new[]{new SocialService.Player{playerId="member",name="Another name"}}}};
  SocialService.CleanSnapshot(legacy);
  Check(SafeIdentityCatalog.IsPlayerName(legacy.me.name)&&SafeIdentityCatalog.IsPlayerName(legacy.clan.members[0].name),"legacy server player text hidden");
  Check(SafeIdentityCatalog.IsClanName(legacy.clan.name)&&SafeIdentityCatalog.IsClanTag(legacy.clan.tag),"legacy server clan text hidden");
  Check(legacy.clan.description==SafeIdentityCatalog.Descriptions[0],"legacy server description replaced by preset");
  AccountProfilePrefs.Detach();
 }
}
