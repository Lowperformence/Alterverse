using System.Collections.Generic;using Alterverse.AI;using Alterverse.Audio;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Multiplayer;using Alterverse.Profile;using Alterverse.Shop;using Alterverse.UI;using UnityEngine;
namespace Alterverse.Match
{
public sealed class MatchScoreboard:MonoBehaviour
{
private MatchResultScreen resultScreen;
private FrontEndTheme boardTheme;
private GUIStyle scoreValue;
private CoopHeroSlot localHeroSlot;
private CoopMatchPlan localHeroPlan;
private float nextLocalRespawnSearch,nextRespawnBoardRefresh;
private readonly List<Entry> respawnZero=new(4),respawnLumen=new(4);
private static readonly string[] respawnHeadings={"K","D","A","SCORE"};
private bool respawnBoardReady;
private string zeroFrontLabel,lumenFrontLabel,respawnCountdownLabel;
private int respawnCountdownSecond=-1;

public bool ResultCommitted => matchEnded && rewardGranted;
private bool resultSnapshotReady,resultSnapshotWarning;
private int battlePassXpBefore;
private BotDifficulty resultDifficulty;
private string rewardId=System.Guid.NewGuid().ToString("N");private float nextRewardAttempt;private bool saveWarningShown;
private sealed class Entry{public Health Health;public TeamMember Member;public string Name;public int Kills,Deaths,Assists;public int Score=>Kills*100+Assists*50-Deaths*15;
public float BoardPerformance;public bool BoardOwn;
public string BoardName,BoardKills,BoardDeaths,BoardAssists,BoardScore;
}public static MatchScoreboard Instance{get;private set;}[SerializeField]private float refreshInterval=.5f,assistWindow=8f;private readonly Dictionary<Health,Entry> entries=new();private readonly Dictionary<Health,Dictionary<TeamMember,float>> damageCredit=new();private float nextRefresh;private HeroRespawn playerRespawn;private CoopRespawnReplica coopRespawn;private DimensionsFront front;private bool matchEnded,rewardGranted,rewardPending;private int winningTeam,matchReward,matchXp,matchBaseReward,matchShardBonus,matchBaseXp,matchXpBonus,playerKills,playerDeaths,playerAssists,playerObjectives,playerQuestKills,playerHotZoneCaptures,playerSoldierKills,battlePassLevelBefore=1;private float matchBonusPercent;private float playerDamageDealt,playerDamageTaken,playerHealing,playerFrontContribution;private UniversalHeroCosmetic matchBonusCosmetic=UniversalHeroCosmetic.Original;private BattleItemType matchItemDrop;
private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;rewardGranted=false;matchEnded=false;MatchClock.Set(PauseReason.Result,false);}
private void Start(){RefreshRoster();RefreshLocalRespawn();front=Object.FindAnyObjectByType<DimensionsFront>();if(front!=null)front.MatchEnded+=OnMatchEnded;}
private void Update(){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+refreshInterval;RefreshRoster();RefreshLocalRespawn();if(front==null){front=Object.FindAnyObjectByType<DimensionsFront>();if(front!=null)front.MatchEnded+=OnMatchEnded;}}if(matchEnded)RefreshResultPresentation();else UpdateRespawnBoard();}
private void RefreshLocalRespawn()
{
if(!CoopAuthority.Active)
{
localHeroSlot=null;localHeroPlan=null;
if(playerRespawn==null)playerRespawn=Object.FindAnyObjectByType<HeroRespawn>();
coopRespawn=null;return;
}
bool samePlan=object.ReferenceEquals(localHeroPlan,CoopAuthority.ActivePlan);
if(samePlan&&localHeroSlot!=null&&localHeroSlot.IsLocalHuman)
{
playerRespawn=localHeroSlot.GetComponent<HeroRespawn>();
coopRespawn=localHeroSlot.GetComponent<CoopRespawnReplica>();return;
}
if(samePlan&&Time.unscaledTime<nextLocalRespawnSearch)return;
localHeroPlan=CoopAuthority.ActivePlan;nextLocalRespawnSearch=Time.unscaledTime+.5f;
localHeroSlot=null;playerRespawn=null;coopRespawn=null;
CoopHeroSlot[] slots=Object.FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].IsLocalHuman)
{
localHeroSlot=slots[i];playerRespawn=slots[i].GetComponent<HeroRespawn>();
coopRespawn=slots[i].GetComponent<CoopRespawnReplica>();return;
}
}
private static bool IsHero(TeamMember member)=>member!=null&&(member.GetComponent<HeroRespawn>()!=null||member.GetComponent<MobaBotRespawn>()!=null||member.GetComponent<CoopHeroSlot>()!=null);public void RecordDamage(Health victim,GameObject source){if(victim==null||source==null)return;TeamMember attacker=source.GetComponentInParent<TeamMember>(),victimTeam=victim.GetComponent<TeamMember>();if(!IsHero(attacker)||!IsHero(victimTeam)||attacker==victimTeam||attacker.Team==victimTeam.Team)return;if(!damageCredit.TryGetValue(victim,out Dictionary<TeamMember,float> credits)){credits=new Dictionary<TeamMember,float>();damageCredit[victim]=credits;}credits[attacker]=Time.time;}
private void RefreshRoster()
{
// Health already maintains the active gameplay registry. Avoid sorting and allocating
// a scene-wide TeamMember array twice a second on both server and clients.
IReadOnlyList<Health> active=Health.Active;
for(int i=0;i<active.Count;i++)
{
Health health=active[i];if(health==null||entries.ContainsKey(health))continue;
TeamMember member=health.GetComponent<TeamMember>();if(!IsHero(member))continue;
HeroIdentity identity=member.GetComponent<HeroIdentity>();CoopHeroSlot coop=member.GetComponent<CoopHeroSlot>();string n=coop!=null?CoopDisplayName(coop):(member.GetComponent<HeroRespawn>()!=null?PlayerProfile.PlayerName:(identity!=null?identity.DisplayName:DisplayName(member.gameObject.name)));entries.Add(health,new Entry{Health=health,Member=member,Name=n});health.DiedWithSource+=OnHeroDied;}}private void OnHeroDied(Health victim,GameObject source){if(victim==null||!entries.TryGetValue(victim,out Entry victimEntry))return;victimEntry.Deaths++;TeamMember killer=source!=null?source.GetComponentInParent<TeamMember>():null;if(IsHero(killer)&&victimEntry.Member!=null&&killer.Team!=victimEntry.Member.Team){Entry killerEntry=FindEntry(killer);if(killerEntry!=null)killerEntry.Kills++;if(damageCredit.TryGetValue(victim,out Dictionary<TeamMember,float> credits))foreach(var credit in credits)if(IsHero(credit.Key)&&credit.Key!=killer&&Time.time-credit.Value<=assistWindow&&credit.Key.Team==killer.Team){Entry assist=FindEntry(credit.Key);if(assist!=null){assist.Assists++;MatchTelemetry.Instance?.RecordAssist(assist.Health);}}}damageCredit.Remove(victim);}private Entry FindEntry(TeamMember member){foreach(Entry e in entries.Values)if(e.Member==member)return e;return null;}
// Absolute server snapshots repair packet loss without replaying death or reward events.
public void GetNetworkScore(Health health,out int kills,out int deaths,out int assists)
{
 kills=deaths=assists=0;
 if(health==null||!CoopAuthority.CanSimulateHostGameplay)return;
 if(!entries.TryGetValue(health,out Entry entry)){RefreshRoster();entries.TryGetValue(health,out entry);}
 if(entry==null)return;
 kills=entry.Kills;deaths=entry.Deaths;assists=entry.Assists;
}
public void ApplyNetworkScore(Health health,int kills,int deaths,int assists)
{
 if(health==null||!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority)return;
 if(!entries.TryGetValue(health,out Entry entry)){RefreshRoster();entries.TryGetValue(health,out entry);}
 if(entry==null)return;
 entry.Kills=Mathf.Max(0,kills);entry.Deaths=Mathf.Max(0,deaths);entry.Assists=Mathf.Max(0,assists);
}
private static string CoopDisplayName(CoopHeroSlot slot){CoopMatchPlan plan=CoopAuthority.ActivePlan;if(slot!=null&&plan?.Session?.Slots!=null&&slot.Slot>=0&&slot.Slot<plan.Session.Slots.Length){MatchPlayerSlot data=plan.Session.Slots[slot.Slot];if(data!=null&&!string.IsNullOrWhiteSpace(data.DisplayName))return data.DisplayName;}HeroIdentity identity=slot!=null?slot.GetComponent<HeroIdentity>():null;return identity!=null?identity.DisplayName:"PLAYER";}
private TeamMember LocalPlayerMember()
{
RefreshLocalRespawn();
return CoopAuthority.Active?(localHeroSlot!=null?localHeroSlot.GetComponent<TeamMember>():null):
    playerRespawn!=null?playerRespawn.GetComponent<TeamMember>():null;
}
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
// Online quests use authoritative end-of-match totals. Local telemetry can miss
// remote events; keep it only as a fallback for older servers without the suffix.
MatchTelemetry.HeroStats localTelemetry=playerEntry!=null?MatchTelemetry.Instance?.GetStats(playerEntry.Health):null;
playerQuestKills=networkResult.HasQuestStats?networkResult.QuestKills:networkResult.Kills;
playerHotZoneCaptures=networkResult.HasQuestStats?networkResult.HotZoneCaptures:(localTelemetry?.HotZoneCaptures??0);
playerSoldierKills=networkResult.HasQuestStats?networkResult.SoldierKills:(localTelemetry?.SoldierKills??0);
playerDamageDealt=networkResult.DamageDealt;playerDamageTaken=networkResult.DamageTaken;playerHealing=networkResult.Healing;playerFrontContribution=networkResult.FrontContribution;
ApplyNetworkResultsToEntries();
}
else
{
playerKills=playerEntry?.Kills??0;playerDeaths=playerEntry?.Deaths??0;playerAssists=playerEntry?.Assists??0;
MatchTelemetry.HeroStats telemetry=playerEntry!=null?MatchTelemetry.Instance?.GetStats(playerEntry.Health):null;
playerObjectives=telemetry?.Objectives??0;playerQuestKills=telemetry?.Kills??playerKills;playerHotZoneCaptures=telemetry?.HotZoneCaptures??0;playerSoldierKills=telemetry?.SoldierKills??0;playerDamageDealt=telemetry?.DamageDealt??0;playerDamageTaken=telemetry?.DamageTaken??0;playerHealing=telemetry?.Healing??0;playerFrontContribution=telemetry?.FrontContribution??0;
}

try{MatchRewardResult reward=MatchRewardService.GrantMatchReward(won,playerKills,playerAssists,playerObjectives,rewardId,playerQuestKills,playerHotZoneCaptures,playerSoldierKills,deaths:playerDeaths);matchReward=reward.Shards;matchXp=reward.Xp;matchBaseReward=reward.BaseShards;matchShardBonus=reward.ShardBonus;matchBaseXp=reward.BaseXp;matchXpBonus=reward.XpBonus;matchBonusCosmetic=reward.BonusCosmetic;matchBonusPercent=reward.BonusPercent;matchItemDrop=won?BattleItemInventory.RollVictoryDrop(rewardId):BattleItemType.None;rewardGranted=true;}catch(System.Exception){rewardPending=true;nextRewardAttempt=Time.unscaledTime+2;if(!saveWarningShown){saveWarningShown=true;Debug.LogWarning("DIMENSIONS FRONT: reward save pending; retrying the same receipt.");}}
}
private void ApplyNetworkResultsToEntries(){foreach(Entry e in entries.Values){CoopHeroSlot slot=e.Member!=null?e.Member.GetComponent<CoopHeroSlot>():null;if(slot==null||!CoopMatchResultSync.TryGetSlotResult(slot.Slot,out CoopMatchResultSync.SlotResult s))continue;e.Kills=s.Kills;e.Deaths=s.Deaths;e.Assists=s.Assists;}}
private float Performance(Entry e)
{
    if(CoopAuthority.Active&&!CoopAuthority.LocalHasServerAuthority)
    {
        CoopHeroSlot slot=e.Health!=null?e.Health.GetComponent<CoopHeroSlot>():null;
        if(slot!=null&&CoopMatchResultSync.TryGetSlotResult(slot.Slot,out CoopMatchResultSync.SlotResult result))
            return PerformanceScore(result.Kills,result.Deaths,result.Assists,result.DamageDealt,result.DamageTaken,result.Healing,result.FrontContribution);
        // Live clients receive authoritative K/D/A but no complete combat telemetry.
        return e.Score;
    }
    MatchTelemetry.HeroStats stats=MatchTelemetry.Instance?.GetStats(e.Health);
    return stats!=null?PerformanceScore(e.Kills,e.Deaths,e.Assists,stats.DamageDealt,stats.DamageTaken,stats.Healing,stats.FrontContribution):e.Score;
}
private static float PerformanceScore(int kills,int deaths,int assists,float damageDealt,float damageTaken,float healing,float contribution)
    =>kills*110+assists*65-deaths*25+damageDealt*.055f+damageTaken*.025f+healing*.065f+contribution*38f;
private Entry Mvp(){Entry best=null;float bestScore=float.MinValue;foreach(Entry e in entries.Values){float score=Performance(e);if(best==null||score>bestScore){best=e;bestScore=score;}}return best;}
private float HudScale=>Mathf.Max(.1f,Mathf.Min(Screen.safeArea.height/720f,Screen.safeArea.width/960f));
private float HudWidth=>Screen.safeArea.width/HudScale;private float HudHeight=>Screen.safeArea.height/HudScale;
private void OnGUI(){if(MainMenuController.IsVisible)return;Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;bool enabled=GUI.enabled;try{Rect safe=Screen.safeArea;GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,Vector3.one*(matchEnded?MatchResultLayout.Scale(safe):HudScale));DrawHud();}finally{GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabled;}}
// IMGUI calls DrawHud multiple times per frame. Build the changing scoreboard view
// at four Hz in Update, keeping discovery, list allocation and sorting out of painting.
private void UpdateRespawnBoard()
{
    bool waiting=playerRespawn!=null&&playerRespawn.IsRespawning||coopRespawn!=null&&coopRespawn.IsRespawning;
    if(!waiting){respawnBoardReady=false;return;}
    if(respawnBoardReady&&Time.unscaledTime<nextRespawnBoardRefresh)return;
    RefreshRespawnBoard();
}
private void RefreshRespawnBoard()
{
    nextRespawnBoardRefresh=Time.unscaledTime+.25f;
    respawnZero.Clear();respawnLumen.Clear();
    TeamMember local=LocalPlayerMember();
    foreach(Entry e in entries.Values)
    {
        if(e.Member==null)continue;
        e.BoardPerformance=Performance(e);e.BoardOwn=e.Member==local;
        e.BoardName=(e.BoardOwn&&FounderEntitlement.Owned?"◆ ":"")+e.Name;
        e.BoardKills=e.Kills.ToString();e.BoardDeaths=e.Deaths.ToString();
        e.BoardAssists=e.Assists.ToString();e.BoardScore=e.BoardPerformance.ToString("0");
        (e.Member.Team==0?respawnZero:respawnLumen).Add(e);
    }
    respawnZero.Sort((a,b)=>b.BoardPerformance.CompareTo(a.BoardPerformance));
    respawnLumen.Sort((a,b)=>b.BoardPerformance.CompareTo(a.BoardPerformance));
    if(front!=null)
    {
        zeroFrontLabel=$"ZERO  •  FRONT {front.ZeroControl:0.0}%";
        lumenFrontLabel=$"LUMEN  •  FRONT {front.LumenControl:0.0}%";
    }
    respawnBoardReady=true;
}
private void DrawHud()
{
    if(MainMenuController.IsVisible)return;
    boardTheme??=new FrontEndTheme();
    scoreValue??=new GUIStyle(boardTheme.Small){alignment=TextAnchor.MiddleRight,wordWrap=false,fontSize=15};
    if(matchEnded){DrawResult();return;}
    bool authoredRespawn=playerRespawn!=null&&playerRespawn.IsRespawning;
    bool replicatedRespawn=coopRespawn!=null&&coopRespawn.IsRespawning;
    if(!authoredRespawn&&!replicatedRespawn)return;

    float width=Mathf.Min(820,HudWidth*.88f),x=(HudWidth-width)*.5f,y=Mathf.Max(90,HudHeight*.15f);
    if(!respawnBoardReady)RefreshRespawnBoard();
    float boardHeight=198+Mathf.Max(respawnZero.Count,respawnLumen.Count)*42;
    boardTheme.Panel(new Rect(x,y,width,boardHeight));
    boardTheme.Label(new Rect(x+22,y+12,width-44,34),"TEAM SCOREBOARD",boardTheme.Heading);
    if(front!=null)
    {
        float controlWidth=(width-52)*.5f;
        boardTheme.StatusBadge(new Rect(x+22,y+52,controlWidth,26),zeroFrontLabel,FrontEndTheme.Zero);
        boardTheme.StatusBadge(new Rect(x+30+controlWidth,y+52,controlWidth,26),lumenFrontLabel,FrontEndTheme.Positive);
    }
    DrawTeam(respawnZero,x+22,y+90,width*.5f-32,"ZERO");
    DrawTeam(respawnLumen,x+width*.5f+10,y+90,width*.5f-32,"LUMEN");
    Rect action=new Rect(x+width*.25f,y+boardHeight-62,width*.5f,46);
    if(authoredRespawn&&playerRespawn.RespawnReady)
    {
        if(boardTheme.Action(action,"RETURN TO THE FRONT",true))playerRespawn.ConfirmRespawn();
    }
    else
    {
        float remaining=authoredRespawn?playerRespawn.RemainingSeconds:coopRespawn.RemainingSeconds;
        int seconds=Mathf.CeilToInt(remaining);
        if(seconds!=respawnCountdownSecond||respawnCountdownLabel==null)
        {respawnCountdownSecond=seconds;respawnCountdownLabel=$"RESPAWN IN {seconds}s";}
        boardTheme.StatusBadge(action,respawnCountdownLabel,FrontEndTheme.Warning);
    }
}
private void RefreshResultPresentation()
{
    if(resultScreen!=null&&resultSnapshotReady==rewardGranted&&resultSnapshotWarning==saveWarningShown)return;
    TeamMember player=LocalPlayerMember();
    Entry entry=player!=null?FindEntry(player):null;
    MatchTelemetry.HeroStats stats=entry!=null?MatchTelemetry.Instance?.GetStats(entry.Health):null;
    HeroIdentity hero=player!=null?player.GetComponent<HeroIdentity>():null;
    var snapshot=new MatchResultScreenData
    {
        Won=player!=null&&player.Team==winningTeam,Ready=rewardGranted,Retrying=saveWarningShown,
        ExitLabel=NetworkRoundLoop.OnlineReturnsToLobby?"BACK TO LOBBY":"BACK TO MENU",
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
        if(matchBonusPercent>0)details.Add(new MatchResultScreenData.Metric("WING BONUS",snapshot.BonusName+"  +"+WingMatchRewardBonus.PercentLabel(matchBonusPercent)+"%"));
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
        _ = NetworkRoundLoop.OnlineReturnsToLobby ? MatchExit.ReturnToOnlineLobbyAsync() : MatchExit.ReturnToMenuAsync();
    }
}
private void DrawTeam(List<Entry> team,float x,float y,float width,string title)
{
    Color tint=title=="ZERO"?FrontEndTheme.Zero:FrontEndTheme.Positive;
    float nameWidth=width-152;
    boardTheme.Label(new Rect(x,y,nameWidth,28),title,boardTheme.Body);

    for(int column=0;column<4;column++)
        boardTheme.Label(new Rect(x+nameWidth+column*32,y,column==3?52:28,28),respawnHeadings[column],scoreValue,true);
    for(int i=0;i<team.Count;i++)
    {
        Entry e=team[i];float rowY=y+34+i*42;
        Rect r=new Rect(x,rowY,width,38);boardTheme.Panel(r);
        if(e.BoardOwn)boardTheme.Frame(r,true);
        FrontEndTheme.Fill(new Rect(x+1,rowY+9,2,20),tint);
        boardTheme.FitLabel(new Rect(x+10,rowY,nameWidth-14,38),e.BoardName,boardTheme.Small);
        for(int column=0;column<4;column++)
        {
            string value=column==0?e.BoardKills:column==1?e.BoardDeaths:column==2?e.BoardAssists:e.BoardScore;
            boardTheme.Label(new Rect(x+nameWidth+column*32,rowY,column==3?48:28,38),value,scoreValue);
        }
    }
}
private void OnDestroy(){foreach(var pair in entries)if(pair.Key!=null)pair.Key.DiedWithSource-=OnHeroDied;if(front!=null)front.MatchEnded-=OnMatchEnded;entries.Clear();if(Instance==this)Instance=null;}
}
}




