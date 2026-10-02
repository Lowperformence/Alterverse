using System.Collections.Generic;using Alterverse.AI;using Alterverse.Audio;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Multiplayer;using Alterverse.Profile;using Alterverse.Shop;using Alterverse.UI;using UnityEngine;
namespace Alterverse.Match
{
public sealed class MatchScoreboard:MonoBehaviour
{
private MatchResultScreen resultScreen;
public bool ResultCommitted => matchEnded && rewardGranted;
private bool resultSnapshotReady,resultSnapshotWarning;
private int battlePassXpBefore;
private BotDifficulty resultDifficulty;
private string rewardId=System.Guid.NewGuid().ToString("N");private float nextRewardAttempt;private bool saveWarningShown;
private sealed class Entry{public Health Health;public TeamMember Member;public string Name;public int Kills,Deaths,Assists;public int Score=>Kills*100+Assists*50-Deaths*15;}public static MatchScoreboard Instance{get;private set;}[SerializeField]private float refreshInterval=.5f,assistWindow=8f;private readonly Dictionary<Health,Entry> entries=new();private readonly Dictionary<Health,Dictionary<TeamMember,float>> damageCredit=new();private float nextRefresh;private HeroRespawn playerRespawn;private CoopRespawnReplica coopRespawn;private DimensionsFront front;private GUIStyle header,row,respawnStyle,frontStyle;private bool matchEnded,rewardGranted,rewardPending;private int winningTeam,matchReward,matchXp,matchBaseReward,matchShardBonus,matchBaseXp,matchXpBonus,matchBonusPercent,playerKills,playerDeaths,playerAssists,playerObjectives,playerQuestKills,playerHotZoneCaptures,playerSoldierKills,battlePassLevelBefore=1;private float playerDamageDealt,playerDamageTaken,playerHealing,playerFrontContribution;private UniversalHeroCosmetic matchBonusCosmetic=UniversalHeroCosmetic.Original;private BattleItemType matchItemDrop;
private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;rewardGranted=false;matchEnded=false;MatchClock.Set(PauseReason.Result,false);}
private void Start(){RefreshRoster();RefreshLocalRespawn();front=Object.FindAnyObjectByType<DimensionsFront>();if(front!=null)front.MatchEnded+=OnMatchEnded;}
private void Update(){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+refreshInterval;RefreshRoster();RefreshLocalRespawn();if(front==null){front=Object.FindAnyObjectByType<DimensionsFront>();if(front!=null)front.MatchEnded+=OnMatchEnded;}}if(matchEnded)RefreshResultPresentation();}
private void RefreshLocalRespawn()
{
if(!CoopAuthority.Active){playerRespawn??=Object.FindAnyObjectByType<HeroRespawn>();return;}
CoopHeroSlot[] slots=Object.FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].IsLocalHuman)
{playerRespawn=slots[i].GetComponent<HeroRespawn>();coopRespawn=slots[i].GetComponent<CoopRespawnReplica>();return;}
}
private static bool IsHero(TeamMember member)=>member!=null&&(member.GetComponent<HeroRespawn>()!=null||member.GetComponent<MobaBotRespawn>()!=null||member.GetComponent<CoopHeroSlot>()!=null);public void RecordDamage(Health victim,GameObject source){if(victim==null||source==null)return;TeamMember attacker=source.GetComponentInParent<TeamMember>(),victimTeam=victim.GetComponent<TeamMember>();if(!IsHero(attacker)||!IsHero(victimTeam)||attacker==victimTeam||attacker.Team==victimTeam.Team)return;if(!damageCredit.TryGetValue(victim,out Dictionary<TeamMember,float> credits)){credits=new Dictionary<TeamMember,float>();damageCredit[victim]=credits;}credits[attacker]=Time.time;}
private void RefreshRoster(){foreach(TeamMember member in Object.FindObjectsByType<TeamMember>()){if(!IsHero(member))continue;Health health=member.GetComponent<Health>();if(health==null||entries.ContainsKey(health))continue;HeroIdentity identity=member.GetComponent<HeroIdentity>();CoopHeroSlot coop=member.GetComponent<CoopHeroSlot>();string n=coop!=null?CoopDisplayName(coop):(member.GetComponent<HeroRespawn>()!=null?PlayerProfile.PlayerName:(identity!=null?identity.DisplayName:DisplayName(member.gameObject.name)));entries.Add(health,new Entry{Health=health,Member=member,Name=n});health.DiedWithSource+=OnHeroDied;}}private void OnHeroDied(Health victim,GameObject source){if(victim==null||!entries.TryGetValue(victim,out Entry victimEntry))return;victimEntry.Deaths++;TeamMember killer=source!=null?source.GetComponentInParent<TeamMember>():null;if(IsHero(killer)&&victimEntry.Member!=null&&killer.Team!=victimEntry.Member.Team){Entry killerEntry=FindEntry(killer);if(killerEntry!=null)killerEntry.Kills++;if(damageCredit.TryGetValue(victim,out Dictionary<TeamMember,float> credits))foreach(var credit in credits)if(IsHero(credit.Key)&&credit.Key!=killer&&Time.time-credit.Value<=assistWindow&&credit.Key.Team==killer.Team){Entry assist=FindEntry(credit.Key);if(assist!=null){assist.Assists++;MatchTelemetry.Instance?.RecordAssist(assist.Health);}}}damageCredit.Remove(victim);}private Entry FindEntry(TeamMember member){foreach(Entry e in entries.Values)if(e.Member==member)return e;return null;}
private static string CoopDisplayName(CoopHeroSlot slot){CoopMatchPlan plan=CoopAuthority.ActivePlan;if(slot!=null&&plan?.Session?.Slots!=null&&slot.Slot>=0&&slot.Slot<plan.Session.Slots.Length){MatchPlayerSlot data=plan.Session.Slots[slot.Slot];if(data!=null&&!string.IsNullOrWhiteSpace(data.DisplayName))return data.DisplayName;}HeroIdentity identity=slot!=null?slot.GetComponent<HeroIdentity>():null;return identity!=null?identity.DisplayName:"PLAYER";}
private TeamMember LocalPlayerMember(){if(CoopAuthority.Active){CoopHeroSlot[] slots=Object.FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].IsLocalHuman)return slots[i].GetComponent<TeamMember>();}playerRespawn??=Object.FindAnyObjectByType<HeroRespawn>();return playerRespawn!=null?playerRespawn.GetComponent<TeamMember>():null;}
private static string DisplayName(string raw)=>raw.Replace("AV_","").Replace("Bot_"," ");public int KillsForTeam(int team)=>TeamKills(team);private int TeamKills(int team){int total=0;foreach(Entry e in entries.Values)if(e.Member!=null&&e.Member.Team==team)total+=e.Kills;return total;}private void OnMatchEnded(int team){if(matchEnded)return;winningTeam=team;battlePassLevelBefore=BattlePassProgress.Level;battlePassXpBefore=BattlePassProgress.Xp;resultDifficulty=BotDifficultySettings.Current;matchEnded=true;rewardPending=true;TeamMember player=LocalPlayerMember();bool won=player!=null&&player.Team==winningTeam;DimensionAudio.Play(won?DimensionSound.Victory:DimensionSound.Defeat,1f);MatchClock.Set(PauseReason.Result,true);}private void LateUpdate()
{
if(!rewardPending||rewardGranted||Time.unscaledTime<nextRewardAttempt)return;
rewardPending=false;RefreshRoster();
TeamMember player=LocalPlayerMember();bool won=player!=null&&player.Team==winningTeam;Entry playerEntry=player!=null?FindEntry(player):null;

if(CoopAuthority.Active)
{
if(!CoopMatchResultSync.TryGetLocalResult(out CoopMatchResultSync.SlotResult networkResult,out int networkWinner,out string receipt))
{rewardPending=true;nextRewardAttempt=Time.unscaledTime+.25f;return;}
winningTeam=networkWinner;won=player!=null&&player.Team==winningTeam;rewardId=receipt;
playerKills=networkResult.Kills;playerDeaths=networkResult.Deaths;playerAssists=networkResult.Assists;playerObjectives=networkResult.Objectives;
MatchTelemetry.HeroStats localTelemetry=playerEntry!=null?MatchTelemetry.Instance?.GetStats(playerEntry.Health):null;playerQuestKills=localTelemetry?.Kills??networkResult.Kills;playerHotZoneCaptures=localTelemetry?.HotZoneCaptures??0;playerSoldierKills=localTelemetry?.SoldierKills??0;playerDamageDealt=networkResult.DamageDealt;playerDamageTaken=networkResult.DamageTaken;playerHealing=networkResult.Healing;playerFrontContribution=networkResult.FrontContribution;
ApplyNetworkResultsToEntries();
}
else
{
playerKills=playerEntry?.Kills??0;playerDeaths=playerEntry?.Deaths??0;playerAssists=playerEntry?.Assists??0;
MatchTelemetry.HeroStats telemetry=playerEntry!=null?MatchTelemetry.Instance?.GetStats(playerEntry.Health):null;
playerObjectives=telemetry?.Objectives??0;playerQuestKills=telemetry?.Kills??playerKills;playerHotZoneCaptures=telemetry?.HotZoneCaptures??0;playerSoldierKills=telemetry?.SoldierKills??0;playerDamageDealt=telemetry?.DamageDealt??0;playerDamageTaken=telemetry?.DamageTaken??0;playerHealing=telemetry?.Healing??0;playerFrontContribution=telemetry?.FrontContribution??0;
}

try{MatchRewardResult reward=MatchRewardService.GrantMatchReward(won,playerKills,playerAssists,playerObjectives,rewardId,playerQuestKills,playerHotZoneCaptures,playerSoldierKills);matchReward=reward.Shards;matchXp=reward.Xp;matchBaseReward=reward.BaseShards;matchShardBonus=reward.ShardBonus;matchBaseXp=reward.BaseXp;matchXpBonus=reward.XpBonus;matchBonusCosmetic=reward.BonusCosmetic;matchBonusPercent=reward.BonusPercent;matchItemDrop=won?BattleItemInventory.RollVictoryDrop(rewardId):BattleItemType.None;rewardGranted=true;}catch(System.Exception){rewardPending=true;nextRewardAttempt=Time.unscaledTime+2;if(!saveWarningShown){saveWarningShown=true;Debug.LogWarning("DIMENSIONS FRONT: reward save pending; retrying the same receipt.");}}
}
private void ApplyNetworkResultsToEntries(){foreach(Entry e in entries.Values){CoopHeroSlot slot=e.Member!=null?e.Member.GetComponent<CoopHeroSlot>():null;if(slot==null||!CoopMatchResultSync.TryGetSlotResult(slot.Slot,out CoopMatchResultSync.SlotResult s))continue;e.Kills=s.Kills;e.Deaths=s.Deaths;e.Assists=s.Assists;}}
private float Performance(Entry e){MatchTelemetry.HeroStats s=MatchTelemetry.Instance?.GetStats(e.Health);return s!=null?s.MvpScore:e.Score;}private Entry Mvp(){Entry best=null;float bestScore=float.MinValue;foreach(Entry e in entries.Values){float score=Performance(e);if(best==null||score>bestScore){best=e;bestScore=score;}}return best;}
private float HudScale=>Mathf.Max(.1f,Mathf.Min(Screen.safeArea.height/720f,Screen.safeArea.width/960f));
private float HudWidth=>Screen.safeArea.width/HudScale;private float HudHeight=>Screen.safeArea.height/HudScale;
private void OnGUI(){if(MainMenuController.IsVisible)return;Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;bool enabled=GUI.enabled;try{Rect safe=Screen.safeArea;GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,Vector3.one*(matchEnded?MatchResultLayout.Scale(safe):HudScale));DrawHud();}finally{GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabled;}}
private void DrawHud()
{
if(MainMenuController.IsVisible)return;
header??=new GUIStyle(GUI.skin.label){fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
row??=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleCenter};
respawnStyle??=new GUIStyle(GUI.skin.button){fontSize=22,fontStyle=FontStyle.Bold};
frontStyle??=new GUIStyle(GUI.skin.label){fontSize=14,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
if(matchEnded){DrawResult();return;}

bool authoredRespawn=playerRespawn!=null&&playerRespawn.IsRespawning;
bool replicatedRespawn=coopRespawn!=null&&coopRespawn.IsRespawning;
if(!authoredRespawn&&!replicatedRespawn)return;

float width=Mathf.Min(680,HudWidth*.82f),x=(HudWidth-width)*.5f,y=Mathf.Max(90,HudHeight*.15f);
List<Entry> zero=new List<Entry>(),lumen=new List<Entry>();
foreach(Entry e in entries.Values)(e.Member.Team==0?zero:lumen).Add(e);
zero.Sort((a,b)=>Performance(b).CompareTo(Performance(a)));lumen.Sort((a,b)=>Performance(b).CompareTo(Performance(a)));
float boardHeight=82+Mathf.Max(zero.Count,lumen.Count)*27;
GUI.Box(new Rect(x,y,width,boardHeight+82),GUIContent.none);
GUI.Label(new Rect(x,y+5,width,25),"DIMENSIONS FRONT  •  K / D / A / SCORE",header);
if(front!=null)GUI.Label(new Rect(x,y+30,width,22),$"REALITY FRONT   ZERO {front.ZeroControl:0.0}%   |   LUMEN {front.LumenControl:0.0}%",frontStyle);
DrawTeam(zero,x+10,y+57,width*.5f-15,"ZERO");DrawTeam(lumen,x+width*.5f+5,y+57,width*.5f-15,"LUMEN");
Rect action=new Rect(x+width*.25f,y+boardHeight+18,width*.5f,48);
if(authoredRespawn&&playerRespawn.RespawnReady){if(GUI.Button(action,"RESPAWN",respawnStyle))playerRespawn.ConfirmRespawn();}
else
{
float remaining=authoredRespawn?playerRespawn.RemainingSeconds:coopRespawn.RemainingSeconds;
GUI.Label(action,$"RESPAWN IN  {Mathf.CeilToInt(remaining)}",header);
}
}
private void RefreshResultPresentation()
{
    if(resultScreen!=null&&resultSnapshotReady==rewardGranted&&resultSnapshotWarning==saveWarningShown)return;
    TeamMember player=playerRespawn!=null?playerRespawn.GetComponent<TeamMember>():null;
    Entry entry=player!=null?FindEntry(player):null;
    MatchTelemetry.HeroStats stats=entry!=null?MatchTelemetry.Instance?.GetStats(entry.Health):null;
    HeroIdentity hero=player!=null?player.GetComponent<HeroIdentity>():null;
    var snapshot=new MatchResultScreenData
    {
        Won=player!=null&&player.Team==winningTeam,Ready=rewardGranted,Retrying=saveWarningShown,
        Difficulty=BotDifficultySettings.Label(resultDifficulty),Hero=hero!=null?hero.DisplayName:"PLAYER",
        Kills=CoopAuthority.Active?playerKills:(entry?.Kills??0),Deaths=CoopAuthority.Active?playerDeaths:(entry?.Deaths??0),Assists=CoopAuthority.Active?playerAssists:(entry?.Assists??0),Objectives=CoopAuthority.Active?playerObjectives:(stats?.Objectives??0),
        Xp=matchXp,Shards=matchReward,BaseXp=matchBaseXp,XpBonus=matchXpBonus,BaseShards=matchBaseReward,ShardBonus=matchShardBonus,
        BonusPercent=matchBonusPercent,BonusName=WingMatchRewardBonus.DisplayName(matchBonusCosmetic),Drop=matchItemDrop
    };
    if(rewardGranted)
    {
        snapshot.AccountLevel=PlayerExperience.Level;snapshot.AccountMax=snapshot.AccountLevel>=PlayerExperience.MaxLevel;
        snapshot.AccountInto=PlayerExperience.XpIntoLevel;snapshot.AccountRequired=snapshot.AccountInto+PlayerExperience.XpToNextLevel;
        snapshot.AccountProgress=PlayerExperience.LevelProgress;
        snapshot.PassLevel=BattlePassProgress.Level;snapshot.PassMax=snapshot.PassLevel>=BattlePassProgress.MaxLevel;
        snapshot.PassInto=BattlePassProgress.XpIntoLevel;snapshot.PassRequired=snapshot.PassInto+BattlePassProgress.XpToNextLevel;
        snapshot.PassProgress=BattlePassProgress.LevelProgress;
        // Display the observed progression delta, including the BP cap. Never grant or recompute a reward here.
        snapshot.PassXpGained=Mathf.Max(0,BattlePassProgress.Xp-battlePassXpBefore);
        snapshot.PassLevelUp=snapshot.PassLevel>battlePassLevelBefore;
    }
    var details=new List<MatchResultScreenData.Metric>(18);
    details.Add(new MatchResultScreenData.Metric("PLAYED HERO",snapshot.Hero));
    details.Add(new MatchResultScreenData.Metric("TEAM KILLS  /  ZERO — LUMEN",TeamKills(0)+" — "+TeamKills(1)));
    if(CoopAuthority.Active||stats!=null)
    {
        float dealt=CoopAuthority.Active?playerDamageDealt:stats.DamageDealt;
        float taken=CoopAuthority.Active?playerDamageTaken:stats.DamageTaken;
        float healing=CoopAuthority.Active?playerHealing:stats.Healing;
        int objectives=CoopAuthority.Active?playerObjectives:stats.Objectives;
        float contribution=CoopAuthority.Active?playerFrontContribution:stats.FrontContribution;
        details.Add(new MatchResultScreenData.Metric("DAMAGE DEALT",dealt.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("DAMAGE TAKEN",taken.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("HEALING",healing.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("OBJECTIVES",objectives.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("FRONT CONTRIBUTION",contribution.ToString("N1")));
    }
    Entry mvp=Mvp();
    if(mvp!=null)
    {
        details.Add(new MatchResultScreenData.Metric("MATCH MVP",mvp.Name));
        details.Add(new MatchResultScreenData.Metric("MVP  /  KILLS — DEATHS — ASSISTS",mvp.Kills+" — "+mvp.Deaths+" — "+mvp.Assists));
    }
    if(rewardGranted)
    {
        details.Add(new MatchResultScreenData.Metric("ACCOUNT XP  /  TOTAL",matchXp.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("ACCOUNT XP  /  BASE",matchBaseXp.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("SHARDS  /  TOTAL",matchReward.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("SHARDS  /  BASE",matchBaseReward.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("ACCOUNT XP  /  WING BONUS",matchXpBonus.ToString("N0")));
        details.Add(new MatchResultScreenData.Metric("SHARDS  /  WING BONUS",matchShardBonus.ToString("N0")));
        if(matchBonusPercent>0)details.Add(new MatchResultScreenData.Metric("WING BONUS",snapshot.BonusName+"  +"+matchBonusPercent+"%"));
        details.Add(new MatchResultScreenData.Metric("BATTLE PASS XP  /  NO WING MULTIPLIER",snapshot.PassXpGained.ToString("N0")));
    }
    snapshot.Details=details.ToArray();
    resultScreen??=new MatchResultScreen();resultScreen.SetData(snapshot);
    resultSnapshotReady=rewardGranted;resultSnapshotWarning=saveWarningShown;
}
private void DrawResult()
{
    if(resultScreen==null)return;
    float scale=MatchResultLayout.Scale(Screen.safeArea);
    if(resultScreen.Draw(Screen.safeArea.width/scale,Screen.safeArea.height/scale)&&rewardGranted)
    {
        _ = MatchExit.ReturnToMenuAsync();
    }
}
private void DrawTeam(List<Entry> team,float x,float y,float width,string title){GUI.Label(new Rect(x,y,width,22),title,header);for(int i=0;i<team.Count;i++){Entry e=team[i];bool founder=e.Member.GetComponent<HeroRespawn>()!=null&&FounderEntitlement.Owned;string badge=founder?"◆ ":"";GUI.Label(new Rect(x,y+24+i*27,width,24),$"{badge}{e.Name,-13}   {e.Kills} / {e.Deaths} / {e.Assists} / {Performance(e):0}",row);}}private void OnDestroy(){foreach(var pair in entries)if(pair.Key!=null)pair.Key.DiedWithSource-=OnHeroDied;if(front!=null)front.MatchEnded-=OnMatchEnded;entries.Clear();if(Instance==this)Instance=null;}
}
}
