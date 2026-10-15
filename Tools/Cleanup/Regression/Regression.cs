using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Alterverse.Prototype;
using Alterverse.UI;
using UnityEngine;
using Object=UnityEngine.Object;

public static class Regression
{
    static int checks, failures;
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string scenario){checks++;if(!value)throw new Exception(scenario);}
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Hidden|BindingFlags.Public).Invoke(target,args);
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Hidden).SetValue(target,value);
    static void Reset()
    {
        GameObject.All.Clear();Camera.main=null;NetworkManagerReset();
        CoopAuthority.Active=false;CoopAuthority.ActivePlan=null;CoopAuthority.LocalHasServerAuthority=true;
        CoopAuthority.LocalIsHost=true;CoopAuthority.CanSimulateHostGameplay=true;
        CoopRosterBuilder.LocalSlot=0;CoopRosterBuilder.Success=true;
        PrototypeBootstrap.FailArena=PrototypeBootstrap.FailPlayer=false;PrototypeBootstrap.ArenaBuilds=0;
        CoopLobbyService.Instance=null;OnlinePartyService.Instance=null;EdgegapOnlineFlow.Instance=null;
        Time.time=Time.unscaledTime=0;Time.deltaTime=.1f;Time.timeScale=1;
        typeof(MatchClock).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        MainMenuController.Instance=new GameObject("DF_MainMenu").AddComponent<MainMenuController>();
        DimensionAudioReset();
    }
    static void NetworkManagerReset()=>Unity.Netcode.NetworkManager.Singleton=null;
    static void DimensionAudioReset(){Alterverse.Audio.DimensionAudio.Count=0;Alterverse.Onboarding.OnboardingSessionGuard.Recalls=0;}
    static void Run(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.GetBaseException().Message);}}
    static CoopMatchPlan Plan(bool online=false)
    {
        CoopMatchPlan p=online?new OnlineMatchPlan():new CoopMatchPlan();
        p.MatchId="round-0";p.Session=new MatchSession(p.MatchId,online?SessionMode.OnlineCasual:SessionMode.Private);
        for(int i=0;i<MatchSession.MaxPlayers;i++)p.Session.TryAssign(i,new MatchPlayerSlot{PlayerId="p"+i,DisplayName="P"+i,Team=i/MatchSession.TeamSize,Hero=(HeroId)(i%8),IsBot=i>2,IsLocal=i==1,IsHost=i==0,Ready=true});
        return p;
    }
    static PrototypeBootstrap Bootstrap()=>new GameObject("AV_PrototypeBootstrap").AddComponent<PrototypeBootstrap>();
    static bool Build(PrototypeBootstrap boot)
    {
        object result=Call(boot,"BuildPrototype");
        return result is bool?(bool)result:GameObject.Find("DF_MatchWorld")!=null;
    }
    static void BuildFailures()
    {
        for(int failure=0;failure<3;failure++)
        {
            Reset();var boot=Bootstrap();
            PrototypeBootstrap.FailArena=failure==0;PrototypeBootstrap.FailPlayer=failure==1;
            if(failure==2){CoopAuthority.Active=true;CoopAuthority.ActivePlan=Plan();CoopRosterBuilder.Success=false;}
            MatchClock.Set(PauseReason.Background,true);
            Check(!Build(boot),"failed build reported success");
            Check(MainMenuController.IsVisible&&MainMenuController.Instance.gameObject.activeInHierarchy,"failed build lost menu");
            Check(!CoopAuthority.Active&&!MatchExit.Leaving,"failed build retained match authority/exit lock");
            Check(GameObject.Find("DF_MatchWorld")==null&&GameObject.Find("AV_Player")==null,"failed build retained visible world/actor");
            Check(MatchClock.InBackground&&Time.timeScale==0,"recovery released background pause");
            var failed=GameObject.All.First(g=>g.name=="DF_MatchWorld");
            Check(failed.destroyed&&!failed.activeInHierarchy,"failed world not hidden and destroyed");
            Check(GameObject.All.Where(g=>g.name=="Fixture terrain"||g.name=="AV_Player").All(g=>g.transform.parent==failed.transform),"partial build roots escaped ownership");
        }
    }
    static void MenuRecovery()
    {
        Reset();Bootstrap();PrototypeBootstrap.FailArena=true;
        MainMenuController.Instance.Launch(false);
        Check(MainMenuController.IsVisible&&MainMenuController.Instance.gameObject.activeInHierarchy,"menu launch overwrote synchronous failure recovery");
        Check(MatchClock.Paused&&Time.timeScale==0,"failed launch advanced gameplay");
        PrototypeBootstrap.FailArena=false;
        MainMenuController.Instance.Launch(false);
        Check(!MainMenuController.IsVisible&&!MainMenuController.Instance.gameObject.activeInHierarchy,"successful retry left menu visible");
        Check(GameObject.Find("DF_MatchWorld")!=null,"retry did not build new world");
    }
    static void RepeatedWorlds()
    {
        Reset();var boot=Bootstrap();
        for(int round=0;round<30;round++)
        {
            Check(Build(boot),"valid round failed");
            int builds=PrototypeBootstrap.ArenaBuilds;Check(Build(boot)&&PrototypeBootstrap.ArenaBuilds==builds,"double entry rebuilt world");
            var world=GameObject.Find("DF_MatchWorld");Check(world!=null,"world missing");
            Check(GameObject.All.Where(g=>g.name=="AV_Player"&&!g.destroyed).Count(g=>g.activeInHierarchy)==1,"duplicate active player");
            var routine=(IEnumerator)Call(boot,"RestartPrototype");Check(routine.MoveNext(),"restart did not yield cleanup frame");
            Check(!world.activeInHierarchy&&world.destroyed,"previous world visible during transition");
            Check(MatchClock.Paused,"restart did not hold transition pause");
            Check(!routine.MoveNext(),"unexpected restart continuation");
            Check(!MatchClock.Paused,"restart retained transition pause");
        }
    }
    static void OnlineFailure()
    {
        Reset();Bootstrap();CoopAuthority.Active=true;var p=(OnlineMatchPlan)Plan(true);CoopAuthority.ActivePlan=p;
        PrototypeBootstrap.FailArena=true;
        Check(!OnlineMatchRuntime.Enter(p),"online entry reported success after build failure");
        Check(!CoopAuthority.Active&&MainMenuController.IsVisible,"online failure retained authority/lost menu");
        Reset();Bootstrap();CoopAuthority.Active=true;CoopAuthority.ActivePlan=Plan();CoopRosterBuilder.LocalSlot=-1;
        Check(!Build(Object.FindAnyObjectByType<PrototypeBootstrap>()),"missing local slot accepted");
        Check(MainMenuController.IsVisible&&!CoopAuthority.Active,"missing-slot exit was incomplete");
    }
    static void ExitBarrier()
    {
        Reset();var world=new GameObject("DF_MatchWorld");CoopAuthority.Active=true;
        OnlinePartyService.Instance=new OnlinePartyService{Busy=true,InParty=true};
        CoopLobbyService.Instance=new CoopLobbyService{InLobby=true};
        var exiting=MatchExit.ReturnToMenuAsync();
        Check(MatchExit.Leaving&&!world.activeInHierarchy&&!exiting.IsCompleted,"exit bypassed in-flight session operation");
        MatchExit.ReturnToMenuAsync().GetAwaiter().GetResult();
        Check(!exiting.IsCompleted,"duplicate exit completed the first exit early");
        OnlinePartyService.Instance.Busy=false;exiting.GetAwaiter().GetResult();
        Check(!OnlinePartyService.Instance.InParty&&!CoopLobbyService.Instance.InLobby,"exit retained party/lobby membership");
        Check(!CoopAuthority.Active&&!MatchExit.Leaving&&world.destroyed&&MainMenuController.IsVisible,"exit state not settled");
    }
    static GameObject Actor()
    {
        var g=new GameObject("actor");g.transform.position=new Vector3(5,0,2);
        g.AddComponent<Health>();g.AddComponent<CharacterController>();g.AddComponent<HeroLocomotion>();g.AddComponent<HeroMotor>();
        g.AddComponent<Renderer>();g.AddComponent<Renderer>().enabled=false;return g;
    }
    static void RecallLastFrame(bool pause)
    {
        Reset();var g=Actor();var recall=g.AddComponent<BaseRecall>();Set(recall,"channelDuration",.05f);recall.BeginRecall();
        var c=recall.Routines.Last();Check(recall.Progress>=1,"fixture did not reach final yield");
        if(pause){Time.timeScale=0;Check(c.Enumerator.MoveNext(),"completed recall ignored foreground wait");}
        g.transform.position+=Vector3.right*.3f;Time.timeScale=1;
        Check(!c.Enumerator.MoveNext(),"recall failed to settle");
        Check(Math.Abs(g.transform.position.x-5.3f)<.001f,"final-frame movement did not cancel teleport");
        Check(!recall.IsRecalling&&recall.Progress==0,"cancelled recall retained state");
        Check(Alterverse.Onboarding.OnboardingSessionGuard.Recalls==0&&Alterverse.Audio.DimensionAudio.Last==Alterverse.Audio.DimensionSound.RecallCancel,"cancel emitted successful-recall effects");
    }
    static void RecallNormalAndInterrupt()
    {
        Reset();var g=Actor();var recall=g.AddComponent<BaseRecall>();Set(recall,"channelDuration",.05f);recall.BeginRecall();
        g.transform.position+=Vector3.right*.1f;Check(!recall.Routines.Last().Enumerator.MoveNext(),"valid recall did not finish");
        Check(Math.Abs(g.transform.position.x-5)<.001f&&!recall.IsRecalling,"valid recall did not return to spawn");
        Check(g.GetComponent<HeroLocomotion>().Resets==1&&g.GetComponent<HeroMotor>().Clears==1,"recall retained movement/input");
        recall.BeginRecall();var c=recall.Routines.Last();g.GetComponent<Health>().Hit(0,1);
        Check(!recall.IsRecalling&&c.Stopped,"shield-only damage failed to interrupt recall");
    }
    static void HeroRespawnPause()
    {
        Reset();var g=Actor();var r=g.AddComponent<HeroRespawn>();Set(r,"firstRespawnDelay",0f);g.GetComponent<Health>().Kill();
        Check(r.RespawnReady&&r.IsRespawning,"zero-countdown fixture not ready");
        Time.timeScale=0;r.ConfirmRespawn();Check(g.GetComponent<Health>().IsDead&&r.RespawnReady,"paused confirmation revived hero");
        Time.timeScale=1;r.enabled=false;r.ConfirmRespawn();Check(g.GetComponent<Health>().IsDead,"disabled confirmation revived hero");
        r.enabled=true;g.transform.position=Vector3.zero;r.ConfirmRespawn();
        Check(!g.GetComponent<Health>().IsDead&&!r.IsRespawning,"foreground respawn failed");
        var renderers=g.GetComponentsInChildren<Renderer>(true);Check(renderers[0].enabled&&!renderers[1].enabled,"respawn lost renderer states");
        Check(g.GetComponent<CharacterController>().enabled&&g.GetComponent<HeroMotor>().Clears==1,"respawn retained collider/input state");
    }
    static void BotRespawn()
    {
        foreach(float delay in new[]{0f,.5f})
        {
            Reset();var g=Actor();g.AddComponent<Collider>().enabled=false;var r=g.AddComponent<Alterverse.AI.MobaBotRespawn>();
            Set(r,"firstRespawnDelay",delay);Call(r,"OnEnable");Time.timeScale=0;g.GetComponent<Health>().Kill();
            var c=r.Routines.Last();for(int i=0;i<10;i++){Check(g.GetComponent<Health>().IsDead,"paused bot revived");c.Enumerator.MoveNext();}
            Time.timeScale=1;for(int i=0;i<20&&c.Enumerator.MoveNext();i++){}
            Check(!r.IsRespawning&&!g.GetComponent<Health>().IsDead,"bot failed to resume countdown");
            Check(g.GetComponent<CharacterController>().enabled&&!g.GetComponentsInChildren<Collider>(true)[1].enabled,"bot lost original collider states");
        }
    }
    static void PauseOwnership()
    {
        for(int mask=0;mask<64;mask++)
        {
            Reset();for(int i=0;i<6;i++)MatchClock.Set((PauseReason)(1<<i),(mask&(1<<i))!=0);
            MatchClock.SetImpact(.16f);Check(Time.timeScale==(mask==0?.16f:0),"impact released active pause");
            for(int i=0;i<6;i++)
            {
                MatchClock.Set((PauseReason)(1<<i),false);
                Check(MatchClock.Paused==((mask&~((1<<(i+1))-1))!=0),"releasing a reason released another owner");
            }
            MatchClock.SetImpact(1);Check(Time.timeScale==1,"impact reset retained slow motion");
        }
    }
    static void MatchResults()
    {
        Reset();var f=new GameObject("front").AddComponent<DimensionsFront>();Set(f,"matchDurationSeconds",60f);
        int ends=0,overtimes=0,sudden=0;f.MatchEnded+=t=>ends++;f.OvertimeStarted+=()=>overtimes++;f.SuddenDeathStarted+=()=>sudden++;
        Time.deltaTime=60;Call(f,"Update");Check(f.IsOvertime&&overtimes==1&&!f.HasEnded,"tie skipped overtime");
        Time.timeScale=0;Call(f,"Update");Check(f.TimeRemaining==120,"pause advanced overtime");
        Time.timeScale=1;Time.deltaTime=120;Call(f,"Update");Check(f.IsSuddenDeath&&sudden==1,"tie skipped sudden death");
        f.RegisterHeroKill(0);Check(f.HasEnded&&f.Winner==0&&ends==1,"sudden death did not resolve first scoring kill");
        f.RegisterHeroKill(1);f.RegisterCoreDestroyed(0);Call(f,"Update");Check(ends==1,"ended match resolved twice");
        f.ResetMatch();Check(!f.HasEnded&&!f.IsOvertime&&!f.IsSuddenDeath&&f.Winner==-1&&f.ZeroControl==50,"reset retained match state");
        f.RegisterMinionKill(0);f.RegisterTowerDestroyed(0);Check(f.ZeroControl==50,"legacy lane hooks changed PvP score");
        f.ApplyNetworkState(0,100,0,false,false,true,0);f.ApplyNetworkState(0,100,0,false,false,true,0);Check(ends==2,"replicated result fired twice");
    }
    static void RoundsAndRosters()
    {
        foreach(bool online in new[]{false,true})
        {
            var first=Plan(online);var previous=first;
            for(int round=1;round<=5;round++)
            {
                int mask=round==1?7:3;var next=CoopMatchPlan.NextRound(previous,"round-"+round,mask);
                Check(next.Session.IsFull&&next.MatchId!=previous.MatchId&&next.Session.SessionId==next.MatchId,"round lost full roster/fresh receipt");
                Check((next is OnlineMatchPlan)==online&&next.HumanCount==(round==1?3:2),"round lost mode/human count");
                for(int i=0;i<MatchSession.MaxPlayers;i++)Check(next.Session.Slots[i].Team==first.Session.Slots[i].Team&&next.Session.Slots[i].Hero==first.Session.Slots[i].Hero,"round changed team/hero");
                Check(next.Session.Slots[1].IsLocal&&!next.Session.Slots[1].IsBot,"round lost local player");
                if(round>1)Check(next.Session.Slots[2].IsBot&&!next.Session.Slots[2].IsLocal,"departed human not replaced");
                Check(first.Session.Slots[2].PlayerId=="p2"&&!first.Session.Slots[2].IsBot,"restart mutated prior roster");previous=next;
            }
            bool rejected=false;try{CoopMatchPlan.NextRound(previous,previous.MatchId,3);}catch(InvalidOperationException){rejected=true;}Check(rejected,"duplicate round accepted");
        }
        for(int a=0;a<8;a++)for(int b=0;b<8;b++)if(a!=b)
        {
            var members=new[]{new CoopLobbyService.MemberView{PlayerId="host",IsHost=true,Hero=(HeroId)a},new CoopLobbyService.MemberView{PlayerId="guest",IsLocal=true,Hero=(HeroId)b}};
            var plan=CoopMatchPlan.Build("pair-"+a+b,ArenaVariant.Moon,Alterverse.AI.BotDifficulty.Normal,members);
            Check(plan.Session.IsFull&&plan.HumanCount==2,"two-hero roster not full");
            Check(plan.Session.Slots.Take(MatchSession.TeamSize).Select(s=>s.Hero).Distinct().Count()==MatchSession.TeamSize,"allied hero duplicate");
            Check(plan.Session.Slots.Take(MatchSession.TeamSize).All(s=>s.Team==0)&&plan.Session.Slots.Skip(MatchSession.TeamSize).All(s=>s.Team==1),"roster team sizes changed");
        }
    }
    static void DailyBoundaries()
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        for(int year=2020;year<=2035;year++)for(int month=1;month<=12;month++)for(int hour=0;hour<24;hour+=3)
        {
            var utc=new DateTime(year,month,DateTime.DaysInMonth(year,month),hour,30,0,DateTimeKind.Utc);
            Check(DailyCalendar.BerlinDate(utc)==TimeZoneInfo.ConvertTimeFromUtc(utc,zone).Date,"Berlin calendar differs from independent timezone database");
        }
        for(int streak=1;streak<=7;streak++)
        {
            Check(LoginStreakProgress.NextDay(20260102,20260101,20260102,streak)==0,"same day awarded twice");
            Check(LoginStreakProgress.NextDay(20260102,20260101,20260103,streak)==0,"clock rollback awarded reward");
            Check(LoginStreakProgress.NextDay(20260102,20260101,20260101,streak)==streak%7+1,"streak failed to wrap");
        }
    }
    static void LocalPresentationOwnership()
    {
        for(int team=0;team<2;team++)
        {
            Reset();CoopAuthority.Active=true;
            // An authoritative remote human comes first in discovery order. A guest's
            // actual local actor has replicated respawn, not HeroRespawn.
            var remote=Actor();remote.AddComponent<HeroRespawn>();remote.AddComponent<BasicAttack>();remote.AddComponent<TeamMember>().Initialize(1-team);
            remote.AddComponent<CoopHeroSlot>().Configure(0,new MatchPlayerSlot{IsBot=false},false);
            var local=Actor();local.AddComponent<BasicAttack>();local.AddComponent<TeamMember>().Initialize(team);
            local.AddComponent<CoopHeroSlot>().Configure(1,new MatchPlayerSlot{IsBot=false},true);
            var buff=local.AddComponent<PlanetBuffReceiver>();remote.AddComponent<PlanetBuffReceiver>();
            Check(LocalHeroContext.Find()==local&&LocalHeroContext.Member.Team==team,"remote respawn actor captured local ownership");
            Check(LocalHeroContext.IsLocal(local)&&!LocalHeroContext.IsLocal(remote),"local cosmetics leaked onto remote human");
            var hud=new GameObject("HUD").AddComponent<PlanetHUD>();Call(hud,"Update");Check(hud.Buff==buff,"planet HUD used remote buff");
            var audio=new GameObject("audio").AddComponent<Alterverse.Audio.MatchAudioDirector>();
            Call(audio,"OnMatchEnded",team);Check(Alterverse.Audio.DimensionAudio.Last==Alterverse.Audio.DimensionSound.Victory,"winning guest received defeat cue");
            Call(audio,"OnMatchEnded",1-team);Check(Alterverse.Audio.DimensionAudio.Last==Alterverse.Audio.DimensionSound.Defeat,"losing guest received victory cue");
            var music=new GameObject("music").AddComponent<Alterverse.Audio.ProductionMusicDirector>();
            Call(music,"End",team);Check(music.Result==4,"winning guest chose defeat music state");
            Call(music,"End",1-team);Check(music.Result==5,"losing guest chose victory music state");
            local.SetActive(false);Check(LocalHeroContext.Find()==null,"headless/no-local state selected remote human");
        }
        Reset();var solo=Actor();solo.AddComponent<HeroRespawn>();solo.AddComponent<BasicAttack>();solo.AddComponent<TeamMember>().Initialize(0);
        Check(LocalHeroContext.Find()==solo&&LocalHeroContext.IsLocal(solo),"offline owner was lost");
    }
    public static int Main()
    {
        Run("failed arena/player/roster cleanup",BuildFailures);Run("menu failure and retry",MenuRecovery);
        Run("thirty world restarts and duplicate entry",RepeatedWorlds);Run("online build/missing-slot failure",OnlineFailure);
        Run("exit busy barrier and duplicate exit",ExitBarrier);Run("recall final movement",()=>RecallLastFrame(false));
        Run("recall pause/resume final movement",()=>RecallLastFrame(true));Run("recall success and shield interruption",RecallNormalAndInterrupt);
        Run("hero respawn pause/disable",HeroRespawnPause);Run("bot paused/zero-delay respawn",BotRespawn);
        Run("all pause ownership combinations",PauseOwnership);Run("timer/overtime/sudden death/result",MatchResults);
        Run("private/online rounds and all hero pairs",RoundsAndRosters);Run("daily calendar/streak",DailyBoundaries);
        Run("guest/host/solo local presentation ownership",LocalPresentationOwnership);
        Console.WriteLine("CHECKS="+checks+" FAILURES="+failures+" (engine/network/geometry substitutes; no Unity or live purchases)");return failures==0?0:1;
    }
}
