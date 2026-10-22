using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Alterverse.Match;
using Alterverse.Combat;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using Alterverse.UI;

namespace UnityEngine
{
    public enum FindObjectsInactive {Exclude}
    public class Object
    {
        public static int Searches;
        public static readonly List<object> Scene=new();
        public static T[] FindObjectsByType<T>(FindObjectsInactive state=FindObjectsInactive.Exclude)
        {Searches++;return Scene.OfType<T>().ToArray();}
        public static T FindAnyObjectByType<T>()where T:class
        {Searches++;return Scene.OfType<T>().FirstOrDefault();}
    }
    public class Component:Object
    {public GameObject gameObject;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();}
    public class MonoBehaviour:Component {}
    public class GameObject
    {
        public string name="actor";readonly Dictionary<Type,object> components=new();
        public T AddComponent<T>()where T:Component,new(){var component=new T{gameObject=this};components[typeof(T)]=component;Object.Scene.Add(component);if(component is Health h)Health.Active.Add(h);return component;}
        public T GetComponent<T>()where T:class=>components.TryGetValue(typeof(T),out var c)?c as T:null;
    }
    public struct Rect {public Rect(float x,float y,float width,float height){}}
    public struct Color {}
    public enum TextAnchor {MiddleRight}
    public class GUIStyle {public TextAnchor alignment;public bool wordWrap;public int fontSize;public GUIStyle(){}public GUIStyle(GUIStyle other){}}
    public static class Time {public static float unscaledTime;}
    public static class Mathf
    {
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static int CeilToInt(float v)=>(int)Math.Ceiling(v);
    }
}
namespace Alterverse.Combat {public class Health:Component {
 public static readonly List<Health> Active=new();
 public event Action<Health,GameObject> DiedWithSource;
 public void Die()=>DiedWithSource?.Invoke(this,gameObject);
}}
namespace Alterverse.Characters
{
    public class TeamMember:Component {public int Team;}
    public class HeroIdentity:Component {public string DisplayName="BOT";}
    public class MobaBotRespawn:Component {}
    public class HeroRespawn:Component
    {public bool IsRespawning,RespawnReady;public float RemainingSeconds;public int Confirmed;public void ConfirmRespawn(){Confirmed++;}}
    public static class LocalHeroContext {public static bool IsLocal(GameObject actor)=>actor.GetComponent<CoopHeroSlot>()?.IsLocalHuman==true;}
}
namespace Alterverse.Profile
{
    public static class PlayerProfile {public static string PlayerName="LOCAL";}
    public static class FounderEntitlement
    {public static int Reads;public static bool Owned {get{Reads++;return true;}}}
}
namespace Alterverse.Multiplayer
{
    public class CoopMatchPlan {}
    public static class CoopAuthority {public static bool Active,LocalHasServerAuthority;public static CoopMatchPlan ActivePlan=new();}
    public class CoopHeroSlot:Component {public bool IsLocalHuman;public int Slot;}
    public static class CoopMatchResultSync
    {
        public struct SlotResult {public int Kills,Deaths,Assists;public float DamageDealt,DamageTaken,Healing,FrontContribution;}
        public static readonly Dictionary<int,SlotResult> Results=new();
        public static bool TryGetSlotResult(int slot,out SlotResult result)=>Results.TryGetValue(slot,out result);
    }
    public class CoopRespawnReplica:Component {public bool IsRespawning;public float RemainingSeconds;}
}
namespace Alterverse.UI
{
    public static class MainMenuController {public static bool IsVisible;}
    public class FrontEndTheme
    {
        public GUIStyle Small=new(),Heading=new(),Body=new();
        public static Color Zero,Positive,Warning;
        public static int Paints,Actions;public static bool PressAction;
        public static string LastBadge;
        public static string[] Labels=new string[128];public static int LabelCount;
        public void Panel(Rect r){Paints++;}
        public void Label(Rect r,string s,GUIStyle style,bool muted=false){Labels[LabelCount++%128]=s;}
        public void FitLabel(Rect r,string s,GUIStyle style)=>Label(r,s,style);
        public void Frame(Rect r,bool emphasis){}
        public static void Fill(Rect r,Color c){}
        public void StatusBadge(Rect r,string s,Color c){LastBadge=s;}
        public bool Action(Rect r,string s,bool ready){Actions++;return PressAction;}
    }
}
namespace Alterverse.Match
{
    public class DimensionsFront {public float ZeroControl,LumenControl;}
    public class MatchTelemetry
    {
        public class HeroStats {public float MvpScore,DamageDealt,DamageTaken,Healing,FrontContribution;}
        public static MatchTelemetry Instance=new();public static int Reads;
        public readonly Dictionary<Health,HeroStats> Stats=new();
        public HeroStats GetStats(Health h){Reads++;return Stats.TryGetValue(h,out var stats)?stats:null;}
    }
    public sealed partial class MatchScoreboard
    {
        private FrontEndTheme boardTheme;private GUIStyle scoreValue;
        private readonly Dictionary<Health,Entry> entries=new();
        private HeroRespawn playerRespawn;private CoopRespawnReplica coopRespawn;
        private DimensionsFront front=new();private bool matchEnded;
        private float HudWidth=>960;private float HudHeight=>720;
        private void DrawResult(){}
        public int DeathEvents;
        private void OnHeroDied(Health victim,GameObject source)=>DeathEvents++;
        private static bool IsHero(TeamMember member)=>member!=null&&(member.GetComponent<HeroRespawn>()!=null||member.GetComponent<MobaBotRespawn>()!=null||member.GetComponent<CoopHeroSlot>()!=null);
        private static string CoopDisplayName(CoopHeroSlot slot)=>"ONLINE";
        private static string DisplayName(string value)=>value;
        public void Enroll()=>RefreshRoster();
        public int Enrolled=>entries.Count;
        public void Discover()=>RefreshLocalRespawn();
        public void Tick()=>UpdateRespawnBoard();
        public void Paint()=>DrawHud();
        public TeamMember Local()=>LocalPlayerMember();
        public void Seed(GameObject actor,string name,int kills=0)
        {var h=actor.GetComponent<Health>();entries[h]=new Entry{Health=h,Member=actor.GetComponent<TeamMember>(),Name=name,Kills=kills};}
        public void KillScore(GameObject actor,int value)=>entries[actor.GetComponent<Health>()].Kills=value;
        public float Score(GameObject actor)=>Performance(entries[actor.GetComponent<Health>()]);
        public string FirstName(int team)=>(team==0?respawnZero:respawnLumen)[0].Name;
        public string FirstKills(int team)=>(team==0?respawnZero:respawnLumen)[0].BoardKills;
        public object TeamList=>respawnZero;
        public bool Ready=>respawnBoardReady;
    }
}
class Regression
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static GameObject Hero(int team,bool local=false)
    {
        var actor=new GameObject();actor.AddComponent<Health>();actor.AddComponent<TeamMember>().Team=team;
        actor.AddComponent<CoopHeroSlot>().IsLocalHuman=local;actor.AddComponent<CoopRespawnReplica>();return actor;
    }
    static void RosterCases()
    {
        Health.Active.Clear();UnityEngine.Object.Scene.Clear();
        var board=new MatchScoreboard();var hero=Hero(0,true);var enemy=Hero(1);
        var nonHero=new GameObject();nonHero.AddComponent<Health>();nonHero.AddComponent<TeamMember>();
        board.Enroll();Check(board.Enrolled==2,"registry includes heroes and excludes non-hero health owners");
        int scans=UnityEngine.Object.Searches;long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1200;i++)board.Enroll();
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Check(UnityEngine.Object.Searches==scans,"scoreboard roster never performs scene-wide discovery");
        Check(bytes==0,"stable roster updates allocate no temporary scene arrays");
        hero.GetComponent<Health>().Die();Check(board.DeathEvents==1,"repeated registration cannot duplicate death subscription");
        var later=Hero(1);board.Enroll();Check(board.Enrolled==3,"late hero joins the roster");
        var disabled=Hero(1);Health.Active.Remove(disabled.GetComponent<Health>());board.Enroll();
        Check(board.Enrolled==3,"inactive health is not enrolled until enabled");
        Health.Active.Add(disabled.GetComponent<Health>());board.Enroll();Check(board.Enrolled==4,"re-enabled hero is enrolled");
        Health.Active.Remove(hero.GetComponent<Health>());board.Enroll();Check(board.Enrolled==4,"death/despawn keeps earned scoreboard counters");
        Console.WriteLine($"1200 roster refreshes: allocatedBytes={bytes}, sceneSearches={UnityEngine.Object.Searches-scans}.");
        Health.Active.Clear();UnityEngine.Object.Scene.Clear();
    }
    static void Main()
    {
        PerformanceCases();
        RosterCases();
        CoopAuthority.Active=true;
        var board=new MatchScoreboard();var local=Hero(0,true);var ally=Hero(0);var enemy=Hero(1);
        board.Seed(local,"LOCAL",1);board.Seed(ally,"ALLY",2);board.Seed(enemy,"ENEMY",3);
        local.GetComponent<CoopRespawnReplica>().IsRespawning=true;
        local.GetComponent<CoopRespawnReplica>().RemainingSeconds=5;
        board.Discover();board.Tick();board.Paint();
        Check(board.Local()==local.GetComponent<TeamMember>(),"online ownership uses actual local human");
        Check(board.FirstName(0)=="ALLY"&&board.FirstKills(0)=="2","teams retain descending performance order");
        int scans=UnityEngine.Object.Searches,stats=MatchTelemetry.Reads,founder=Alterverse.Profile.FounderEntitlement.Reads;
        object list=board.TeamList;string badge=FrontEndTheme.LastBadge;
        long allocated=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1200;i++)board.Paint();
        allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Console.WriteLine($"1200 paints: allocatedBytes={allocated}, sceneSearches={UnityEngine.Object.Searches-scans}, telemetryReads={MatchTelemetry.Reads-stats}.");
        Check(UnityEngine.Object.Searches==scans,"painting the death table never repeats global scene discovery");
        Check(MatchTelemetry.Reads==stats,"painting never recomputes or sorts performance");
        Check(Alterverse.Profile.FounderEntitlement.Reads==founder,"painting never rereads founder entitlement");
        Check(allocated==0,"steady production paint paths allocate zero bytes against no-allocation GUI fixtures");
        Check(ReferenceEquals(list,board.TeamList)&&ReferenceEquals(badge,FrontEndTheme.LastBadge),"lists and countdown label are reused");
        board.KillScore(local,8);Time.unscaledTime=.2f;board.Tick();
        Check(board.FirstName(0)=="ALLY","view refresh is bounded during the same quarter second");
        Time.unscaledTime=.26f;board.Tick();board.Paint();
        Check(board.FirstName(0)=="LOCAL"&&board.FirstKills(0)=="8","live score changes appear on the next bounded refresh");
        local.GetComponent<CoopRespawnReplica>().RemainingSeconds=4;board.Paint();
        Check(FrontEndTheme.LastBadge=="RESPAWN IN 4s","countdown updates independently of sorted row cache");
        Check(FrontEndTheme.Actions==0,"online replica never exposes local respawn confirmation");
        local.GetComponent<CoopRespawnReplica>().IsRespawning=false;board.Tick();
        Check(!board.Ready,"finishing respawn invalidates the old view");
        int paints=FrontEndTheme.Paints;board.Paint();Check(FrontEndTheme.Paints==paints,"living player sees no death overlay");
        local.GetComponent<CoopHeroSlot>().IsLocalHuman=false;var replacement=Hero(1,true);CoopAuthority.ActivePlan=new();
        board.Discover();Check(board.Local()==replacement.GetComponent<TeamMember>(),"new plan rebinds local ownership");
        CoopAuthority.Active=false;var solo=new GameObject();solo.AddComponent<TeamMember>();solo.AddComponent<Health>();
        var respawn=solo.AddComponent<HeroRespawn>();respawn.IsRespawning=true;respawn.RespawnReady=true;
        var offline=new MatchScoreboard();offline.Seed(solo,"SOLO");offline.Discover();offline.Tick();
        FrontEndTheme.PressAction=true;offline.Paint();Check(respawn.Confirmed==1,"offline confirmation still calls ConfirmRespawn exactly once");
        UnityEngine.Object.Scene.Clear();CoopAuthority.Active=true;CoopAuthority.ActivePlan=new();
        var missing=new MatchScoreboard();missing.Discover();scans=UnityEngine.Object.Searches;
        for(int i=0;i<100;i++)missing.Local();
        Check(UnityEngine.Object.Searches==scans,"partially built roster bounds missing-owner discovery");
        var later=Hero(0,true);Time.unscaledTime+=.51f;missing.Discover();
        Check(missing.Local()==later.GetComponent<TeamMember>(),"missing owner is discovered after the retry interval");
        Console.WriteLine($"PASS: {checks} respawn HUD checks; 1200 paints: {allocated} managed bytes and no scene scans. Native GUI/Android untested.");
    }
    static void PerformanceCases()
    {
        var actor=Hero(0,true);var health=actor.GetComponent<Health>();actor.GetComponent<CoopHeroSlot>().Slot=3;
        var board=new MatchScoreboard();board.Seed(actor,"PERFORMANCE",2);
        CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;
        Check(board.Score(actor)==200,"live client without a final result retains K/D/A fallback");
        MatchTelemetry.Instance.Stats[health]=new MatchTelemetry.HeroStats{DamageDealt=100,DamageTaken=200,Healing=50,FrontContribution=2};
        Check(board.Score(actor)==200,"live client does not rank by incomplete local combat telemetry");
        CoopMatchResultSync.Results[3]=new CoopMatchResultSync.SlotResult{Kills=1,Deaths=2,Assists=3,DamageDealt=100,DamageTaken=200,Healing=50,FrontContribution=2};
        Check(Math.Abs(board.Score(actor)-344.75f)<.01f,"client ranks completed results using authoritative totals");
        CoopAuthority.LocalHasServerAuthority=true;
        Check(Math.Abs(board.Score(actor)-309.75f)<.01f,"server uses its current roster and authoritative telemetry");
        CoopAuthority.Active=false;
        Check(Math.Abs(board.Score(actor)-309.75f)<.01f,"solo scoreboard retains full local performance calculation");
        MatchTelemetry.Instance.Stats.Clear();
        Check(board.Score(actor)==200,"missing telemetry keeps the existing K/D/A fallback");
        CoopMatchResultSync.Results.Clear();CoopAuthority.LocalHasServerAuthority=false;
        Health.Active.Clear();UnityEngine.Object.Scene.Clear();
    }
}
