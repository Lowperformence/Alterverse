using System;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using Unity.Netcode;
using UnityEngine;

static partial class VfxRegression
{
    static int checks;
    static void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;}
    static GameObject Hero(HeroId hero,int slot,bool local=false)
    {
        var actor=new GameObject("hero"+slot);actor.AddComponent<HeroIdentity>().Configure(hero);
        actor.AddComponent<Health>();actor.AddComponent<TeamMember>().Initialize(slot%2);
        actor.AddComponent<HeroVisualMotion>();var net=actor.AddComponent<CoopHeroSlot>();net.Slot=slot;net.IsLocalHuman=local;
        actor.AddComponent<HeroCombatProfile>().Configure(hero);actor.AddComponent<HeroAbilityKit>().Configure(hero);
        actor.AddComponent<BasicAttack>();actor.AddComponent<PlayerHeroAbilities>();
        return actor;
    }
    static MeshFilter[] Visible()=>GameObject.All.Select(g=>g.GetComponent<MeshFilter>())
        .Where(f=>f!=null&&f.GetComponent<MeshRenderer>()?.enabled==true).ToArray();
    static void Hide(GameObject actor)
    {foreach(var f in Visible())f.gameObject.SetActive(false);}
    static void Reset()
    {
        GameObject.All.Clear();Health.Active.Clear();UnityEngine.Clock.Reset();Debug.ErrorCount=0;Bolt7Vfx.Calls=0;
        typeof(HeroVfxPool).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
        typeof(CoopGameplayCommands).GetProperty("Instance").SetValue(null,null);
        CoopAuthority.Active=true;CoopAuthority.LocalIsHost=false;CoopAuthority.LocalHasServerAuthority=true;
        CoopAuthority.ActivePlan=new();CoopAuthority.ActivePlan.Session.Slots[0]=new MatchPlayerSlot{PlayerId="test"};
        NetworkManager.Singleton=new(){IsServer=true,IsListening=true,IsConnectedClient=true};
        NetworkManager.Singleton.ServerTime.Time=100;
        Resources.HeroShaderSupported=true;SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
        Resources.AttackAtlasAvailable=Resources.AttackAtlasShaderSupported=Resources.AttackAtlasTextureAvailable=true;
        Alterverse.Performance.AdaptiveVfxBudget.AllowDecorative=true;
        CoopHeroStateSync.LegacyRelays=CoopHeroStateSync.TimmyRelays=0;
    }
    static void Step(float dt)
    {
        UnityEngine.Clock.Advance(dt);
        foreach(var g in GameObject.All.ToArray())
        {
            if(!g.activeInHierarchy)continue;
            object[] components={g.GetComponent<HeroLocomotion>(),g.GetComponent<HeroAbilityExecution>(),g.GetComponent<HeroAttackDelivery>(),g.GetComponent<HeroVfxPool>()};
            foreach(var component in components)if(component!=null)component.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(component,null);
            var pool=g.GetComponent<HeroVfxPool>();if(pool!=null)GameObject.Invoke(pool,"LateUpdate");
        }
    }
    static void CommandDamage()
    {
        foreach(int fps in new[]{30,60,120})foreach(bool unsupported in new[]{false,true})foreach(HeroId id in Enum.GetValues(typeof(HeroId)))foreach(bool ability in new[]{false,true})
        {
            Reset();var manager=NetworkManager.Singleton;Resources.HeroShaderSupported=false;
            SystemInfo.graphicsDeviceType=unsupported?UnityEngine.Rendering.GraphicsDeviceType.Vulkan:UnityEngine.Rendering.GraphicsDeviceType.Null;
            var server=Hero(id,0);var victim=Hero(HeroId.Kaelon,1);
            float range=ability?AimingSettings.Current.For(id,0).castRange:server.GetComponent<HeroCombatProfile>().AttackRange;
            if(range<=0)range=AimingSettings.Current.For(id,0).aoeRadius;
            victim.transform.position=Vector3.forward*Mathf.Min(3,range*.65f);
            var client=Hero(id,0,true);var clientVictim=Hero(HeroId.Kaelon,1);clientVictim.transform.position=victim.transform.position;
            var commands=new GameObject("commands").AddComponent<CoopGameplayCommands>();typeof(CoopGameplayCommands).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(commands,null);
            var relay=new GameObject("relay").AddComponent<CoopHeroStateSync>();relay.Bind(manager.CustomMessagingManager);
            relay.Slot(server.GetComponent<CoopHeroSlot>());relay.Slot(victim.GetComponent<CoopHeroSlot>());
            CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;
            var pose=new AimPose(Vector3.forward,clientVictim.transform.position,clientVictim.GetComponent<Health>(),true);
            Check(ability?CoopGameplayCommands.TrySendAbility(client,0,pose):CoopGameplayCommands.TrySendBasic(client,pose),"Real client serializes combat command");
            string name=manager.CustomMessagingManager.LastMessage;byte[] bytes=manager.CustomMessagingManager.LastPacket;
            CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;
            manager.CustomMessagingManager.Handlers[name](7,new FastBufferReader(bytes));
            float initial=victim.GetComponent<Health>().Current+victim.GetComponent<Health>().Shield;
            for(int frame=0;frame<fps;frame++)Step(1f/fps);
            float after=victim.GetComponent<Health>().Current+victim.GetComponent<Health>().Shield;
            Check(after<initial,"Authorized real command damages on headless/unsupported graphics "+id+" ability="+ability+" fps="+fps);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Check((int)typeof(CoopGameplayCommands).GetField("receivedCombat",flags).GetValue(commands)==1&&
                (int)typeof(CoopGameplayCommands).GetField("authorizedCombat",flags).GetValue(commands)==1&&
                (int)typeof(CoopGameplayCommands).GetField("acceptedCombat",flags).GetValue(commands)==1,"Diagnostics distinguish received, authorized and executed commands");
            Check((int)typeof(CoopGameplayCommands).GetField("humanDamagingHits",flags).GetValue(commands)>0,"Diagnostics isolate applied human damage from bot damage, including shields");
            var events=manager.CustomMessagingManager.Packets.Where(x=>x.Name=="DF_COOP_HERO_VFX_V1").Select(x=>x.Data).ToArray();
            Check(events.Length>0,"Damage path publishes VFX even without a supported server shader");
            Check(Visible().Length==0,"Unsupported graphics leaves no partially initialized effect renderers");
            Check(Debug.ErrorCount==(unsupported?1:0),"Expected shader failure is observable, while a headless server never attempts GPU initialization");
            Resources.HeroShaderSupported=true;SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
            typeof(HeroVfxPool).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
            relay.Slot(client.GetComponent<CoopHeroSlot>());relay.Slot(clientVictim.GetComponent<CoopHeroSlot>());
            CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;
            float clientHealth=clientVictim.GetComponent<Health>().Current;
            foreach(var packet in events)relay.Receive(0,packet);
            Check(Visible().Length>0,"Real authoritative combat creates client effect meshes "+id+" ability="+ability+" fps="+fps);
            Check(clientVictim.GetComponent<Health>().Current==clientHealth,"Visual replay never applies duplicate damage");
            relay.Unbind();
        }
    }
    static void AllCues()
    {
        Reset();var manager=NetworkManager.Singleton;
        var relay=new GameObject("relay").AddComponent<CoopHeroStateSync>();relay.Bind(manager.CustomMessagingManager);
        var actor=Hero(HeroId.Nyra,0);var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*3;
        relay.Slot(actor.GetComponent<CoopHeroSlot>());relay.Slot(target.GetComponent<CoopHeroSlot>());
        Action[] cues={
            ()=>HeroSkillVfx.Cast(actor,0),()=>HeroSkillVfx.Basic(actor,target.transform.position,.4f,target.transform,false),
            ()=>HeroSkillVfx.Spell(actor,target.transform.position,2,.4f,target.transform),()=>HeroSkillVfx.Impact(actor,target.GetComponent<Health>(),0),
            ()=>HeroSkillVfx.Area(actor,target.transform.position,2,1,true,false,1),()=>HeroSkillVfx.Dash(actor,Vector3.zero,Vector3.forward*2,1,.2f),
            ()=>HeroSkillVfx.TargetWarning(actor,target.GetComponent<Health>(),1),()=>HeroSkillVfx.Support(actor,target.GetComponent<Health>(),true,2),
            ()=>HeroSkillVfx.SupportLink(actor,target.GetComponent<Health>()),()=>HeroSkillVfx.Debuff(actor,target.GetComponent<Health>()),
            ()=>HeroSkillVfx.Ultimate(actor,target.transform.position),()=>HeroSkillVfx.CancelWarnings(actor),()=>HeroSkillVfx.CancelProjectiles(actor),
            ()=>HeroSkillVfx.ShieldContact(actor,target),()=>HeroSkillVfx.PlayerHit(actor,target)
        };
        for(int i=0;i<cues.Length;i++)
        {
            int before=manager.CustomMessagingManager.Packets.Count;cues[i]();
            Check(manager.CustomMessagingManager.Packets.Count==before+1,"Exactly one event for "+(HeroVfxCue)i);
            Check(manager.CustomMessagingManager.LastPacket.Length<256,"Bounded packet for "+(HeroVfxCue)i);
        }
        var packets=manager.CustomMessagingManager.Packets.Select(x=>x.Data).ToArray();
        int pulses=actor.GetComponent<HeroVisualMotion>().AbilityPulses;
        int emitted=0;Action<HeroVfxEvent> observer=e=>emitted++;HeroVfxEvents.Emitted+=observer;
        CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;
        foreach(var packet in packets)relay.Receive(0,packet);
        Check(emitted==0,"Client nested replays cannot publish or echo another event");
        Check(actor.GetComponent<HeroVisualMotion>().AbilityPulses==pulses+1,"Remote cast replays the actual body ability animation once");
        actor.GetComponent<CoopHeroSlot>().IsLocalHuman=true;
        relay.Receive(0,packets[0]);
        Check(actor.GetComponent<HeroVisualMotion>().AbilityPulses==pulses+1,"Acknowledged cast does not restart the owner's predicted animation");
        actor.GetComponent<CoopHeroSlot>().IsLocalHuman=false;
        manager.ServerTime.Time=100.05;relay.Receive(0,packets[0]);manager.ServerTime.Time=100;
        Check(Mathf.Abs(actor.GetComponent<HeroVisualMotion>().Seeked-.05f)<.0001f,"Remote animation seeks packet age instead of restarting a full windup late");
        int visible=Visible().Length;manager.ServerTime.Time=110;relay.Receive(0,packets[1]);
        Check(Visible().Length==visible,"Stale projectile does not restart");manager.ServerTime.Time=100;
        byte[] oldRound=(byte[])packets[1].Clone();oldRound[1]=(byte)'x';relay.Receive(0,oldRound);
        Check(Visible().Length==visible,"Previous-round effect cannot be replayed");
        HeroVfxEvents.Emitted-=observer;relay.Unbind();
        foreach(var id in new[]{HeroId.Rook,HeroId.Nerok,HeroId.Timmy})
        {
            Reset();manager=NetworkManager.Singleton;relay=new GameObject("relay").AddComponent<CoopHeroStateSync>();relay.Bind(manager.CustomMessagingManager);
            actor=Hero(id,0);relay.Slot(actor.GetComponent<CoopHeroSlot>());
            if(id==HeroId.Rook)HeroSkillVfx.Cast(actor,3);
            else if(id==HeroId.Nerok)HeroSkillVfx.Area(actor,Vector3.zero,2,.4f,false,false,3);
            else HeroSkillVfx.Spell(actor,Vector3.forward*3,1,.3f);
            Check(manager.CustomMessagingManager.Packets.Count==1,"Nested special cue has one generic publication "+id);
            Check(CoopHeroStateSync.LegacyRelays+CoopHeroStateSync.TimmyRelays==0,"No duplicate specialized relay "+id);
            relay.Unbind();
        }
        // Replaying a received warning/flight uses its original start time.
        Reset();actor=Hero(HeroId.Nyra,0);target=Hero(HeroId.Kaelon,1);
        using(HeroVfxEvents.Replay(.3f,Vector3.zero))HeroSkillVfx.Area(actor,Vector3.forward,2,1,true,false,1);
        Check(Visible().Length>0,"Live warning appears after packet delay");Step(.71f);
        Check(Visible().Length==0,"Warning expires at original deadline rather than a full duration after receipt");
        Reset();actor=Hero(HeroId.Nyra,0);target=Hero(HeroId.Kaelon,1);
        using(HeroVfxEvents.Replay(.1f,Vector3.zero))HeroSkillVfx.Support(actor,target.GetComponent<Health>(),true,2);
        Step(.1f);Check(Visible().Length>0,"Shield presentation survives VFX arriving before health snapshot");
        target.GetComponent<Health>().Shield=30;Step(.1f);target.GetComponent<Health>().Shield=0;Step(.01f);
        Check(Visible().Length==0,"A shield breaks immediately after its positive snapshot was observed");
    }
    static void TenHeroVfxReserve()
    {
        Reset();NetworkManager.Singleton.IsServer=false;CoopAuthority.LocalHasServerAuthority=false;
        var actors=new GameObject[10];for(int i=0;i<actors.Length;i++){actors[i]=Hero(HeroId.Elyra,i);actors[i].transform.position=Vector3.right*i;}
        HeroVfxPool.Warm();var flags=BindingFlags.NonPublic|BindingFlags.Static;
        object pool=typeof(HeroVfxPool).GetField("instance",flags).GetValue(null);
        var slots=(Array)typeof(HeroVfxPool).GetField("slots",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pool);
        for(int i=0;i<actors.Length;i++)HeroSkillVfx.Area(actors[i],actors[i].transform.position+Vector3.forward*2,2,.5f,true,false,1);
        foreach(var actor in actors)
        {
            bool warning=false;foreach(object slot in slots)
            {
                var t=slot.GetType();if((bool)t.GetField("Active").GetValue(slot)&&(bool)t.GetField("Warning").GetValue(slot)&&ReferenceEquals(t.GetField("Owner").GetValue(slot),actor))warning=true;
            }
            Check(warning,"Ten-combatant visual reserve retains an essential warning for every caster");
        }
        Step(.6f);Check(Visible().Length==0,"Simultaneous warning burst expires without accumulating render slots");
    }
    static void RealMotionEpochs()
    {
        foreach(int fps in new[]{30,60,120})foreach(bool forced in new[]{false,true})
        {
            Reset();var actor=Hero(HeroId.Veyr,0);var body=actor.GetComponent<HeroLocomotion>();
            Check(forced?body.BeginForcedMove(Vector3.forward*6,.3f):body.BeginDash(Vector3.forward*6),"Real locomotion accepts displacement");
            // Do not poll the revision during travel: even a dash completely between sends must be retained.
            for(int frame=0;frame<fps;frame++)Step(1f/fps);
            Check((actor.transform.position-Vector3.forward*6).sqrMagnitude<.0001f,"Real dash/forced movement reaches endpoint at "+fps);
            Check(body.MovementRevision==2,"Unobserved start and completed end both advance the epoch");
            Check(body.BeginDash(Vector3.forward*9),"Following dash starts");
            Check(body.MovementRevision==3,"Following dash advances fresh epoch");
            actor.GetComponent<HeroCombatStatus>().Stun(.5f);Step(1f/fps);
            Check(!body.IsDashing&&body.MovementRevision==4,"Interrupted dash publishes its own final epoch");
        }
        Reset();var a=Hero(HeroId.Veyr,0);var motion=a.GetComponent<HeroLocomotion>();
        var wall=new GameObject("wall");wall.transform.position=Vector3.forward*3;wall.transform.localScale=new Vector3(3,3,1);wall.AddComponent<Collider>();
        Check(motion.BeginDash(Vector3.forward*6),"Dash accepts clear portion before obstruction");
        for(int i=0;i<60;i++)Step(1f/60);
        Check(a.transform.position.z>1&&a.transform.position.z<3,"Production sweep stops dash before wall in deterministic collider fixture");
        Check(motion.MovementRevision==2,"Colliding dash also retains its final movement epoch");
        motion.MarkNetworkRelocation();Check(motion.MovementRevision==3,"Instant respawn relocation has an explicit movement epoch even without an active dash");
    }
    static void ServerFrameTelemetry()
    {
        Reset();var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var sample=typeof(CoopGameplayCommands).GetMethod("SampleServerFrame",flags);
        for(int i=0;i<18;i++){Time.deltaTime=1f/60;sample.Invoke(service,null);}
        for(int i=0;i<2;i++){Time.deltaTime=.1f;sample.Invoke(service,null);}
        var consume=typeof(CoopGameplayCommands).GetMethod("ConsumeFrameReport",flags);
        string report=(string)consume.Invoke(service,null);
        Check(report.Contains("frameP95Ms=100")&&report.Contains("frameMaxMs=100.0"),"Native telemetry retains attack-time frame spikes hidden by an average CPU sample");
        Check(((string)consume.Invoke(service,null)).Contains("serverFps=0.0"),"Telemetry starts a fresh bounded interval after reporting");
    }
    static void ConfirmedBasicWindups()
    {
        foreach(HeroId id in Enum.GetValues(typeof(HeroId)))
        {
            Reset();var manager=NetworkManager.Singleton;
            var relay=new GameObject("relay").AddComponent<CoopHeroStateSync>();relay.Bind(manager.CustomMessagingManager);
            var actor=Hero(id,0,true);var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward;
            relay.Slot(actor.GetComponent<CoopHeroSlot>());relay.Slot(target.GetComponent<CoopHeroSlot>());
            Check(actor.GetComponent<BasicAttack>().TryAttackNetworkAuthoritative(target.GetComponent<Health>()),"Host accepts basic for "+id);
            Check(manager.CustomMessagingManager.Packets.Count==1,"Accepted basic has one bounded semantic confirmation "+id);
            var packet=manager.CustomMessagingManager.LastPacket;
            CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;
            int starts=CombatMotion.Starts;manager.ServerTime.Time=100.05;
            relay.Receive(7,packet);Check(CombatMotion.Starts==starts,"Nonserver sender cannot confirm owner basic "+id);
            actor.GetComponent<CoopHeroSlot>().IsLocalHuman=false;
            relay.Receive(0,packet);Check(CombatMotion.Starts==starts,"Remote basic keeps snapshot presentation "+id);
            actor.GetComponent<CoopHeroSlot>().IsLocalHuman=true;
            relay.Receive(0,packet);Check(CombatMotion.Starts==starts+1,"Confirmed owner basic presents once "+id);
            Check(Mathf.Abs(CombatMotion.Elapsed-.05f)<.0001f,"Owner basic seeks server age "+id);
            manager.ServerTime.Time=101;relay.Receive(0,packet);
            Check(CombatMotion.Starts==starts+1,"Expired basic confirmation never restarts windup "+id);
            Check(manager.CustomMessagingManager.Packets.Count==1,"Owner confirmation cannot echo network event "+id);
            relay.Unbind();
        }
    }

    static void PendingCooldowns()
    {
        Reset();var manager=NetworkManager.Singleton;
        var server=Hero(HeroId.Nyra,0);var victim=Hero(HeroId.Kaelon,1);victim.transform.position=Vector3.forward*2;
        var client=Hero(HeroId.Nyra,0,true);var target=Hero(HeroId.Kaelon,1);target.transform.position=victim.transform.position;
        var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();GameObject.Invoke(service,"Update");
        manager.IsServer=false;CoopAuthority.LocalHasServerAuthority=false;
        var pose=new AimPose(Vector3.forward,target.transform.position,target.GetComponent<Health>(),true);
        var basic=client.GetComponent<BasicAttack>();var abilities=client.GetComponent<PlayerHeroAbilities>();
        Check(basic.TryAttackAimed(pose),"Owner starts predicted basic");
        var basicPacket=manager.CustomMessagingManager.LastPacket;
        float predicted=basic.RemainingCooldown;basic.ApplyNetworkCooldown(0,0);
        Check(basic.RemainingCooldown==predicted,"A pre-command zero snapshot cannot erase pending basic cooldown");
        Check(!basic.TryAttackAimed(pose),"Held input cannot restart the same pending windup");
        Check(abilities.CastAimed(0,pose),"Owner starts independent predicted ability");
        var abilityPacket=manager.CustomMessagingManager.LastPacket;
        float skill=abilities.RemainingFor(0);abilities.ApplyNetworkCooldown(0,0,1);
        Check(abilities.RemainingFor(0)==skill,"Acknowledging a basic cannot clear a newer ability");
        basic.ApplyNetworkCooldown(.25f,1);Check(Mathf.Abs(basic.RemainingCooldown-.25f)<.00001f,"Older action may accept its own acknowledgement while another is pending");
        manager.IsServer=true;CoopAuthority.LocalHasServerAuthority=true;
        manager.CustomMessagingManager.Handlers["DF_COOP_BASIC_V7"](7,new FastBufferReader(basicPacket));
        Check(CoopGameplayCommands.ProcessedCommand(0)==1,"Authorized basic is acknowledged");
        int accepted=(int)typeof(CoopGameplayCommands).GetField("acceptedCombat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(service);
        manager.CustomMessagingManager.Handlers["DF_COOP_BASIC_V7"](7,new FastBufferReader(basicPacket));
        Check((int)typeof(CoopGameplayCommands).GetField("acceptedCombat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(service)==accepted,"Duplicate command cannot execute damage twice");
        // Force a legitimate rejection after the local prediction: server is still on cooldown.
        server.GetComponent<HeroAbilityExecution>().MarkNetworkRequestCooldown(0);
        manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V7"](7,new FastBufferReader(abilityPacket));
        Check(CoopGameplayCommands.ProcessedCommand(0)==2,"Rejected but processed command also acknowledges prediction");
        manager.IsServer=false;CoopAuthority.LocalHasServerAuthority=false;
        abilities.ApplyNetworkCooldown(0,0,2);Check(abilities.RemainingFor(0)==0,"Rejected request can restore the true server cooldown");
        // Repeated old snapshots cannot override a newer local intent, including another basic.
        UnityEngine.Clock.Advance(1);Check(basic.TryAttackAimed(pose),"Next legitimate basic can start");
        predicted=basic.RemainingCooldown;basic.ApplyNetworkCooldown(0,1);
        Check(basic.RemainingCooldown==predicted,"A stale acknowledged snapshot cannot erase the following attack");
        CoopAuthority.ActivePlan=new();basic.ApplyNetworkCooldown(0,0);
        Check(basic.RemainingCooldown==0,"New round discards pending commands from the previous plan");
        CoopGameplayCommands.ProcessedCommand(0); // establish the new plan before exercising wrap
        typeof(CoopGameplayCommands).GetField("nextCommandSequence",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(service,uint.MaxValue-1);
        Check(CoopGameplayCommands.TrySendBasic(client,pose),"Last unsigned command can be sent");
        Check(CoopGameplayCommands.CanApplyCooldown(client,-1,uint.MaxValue),"Last unsigned command can be acknowledged");
        Check(CoopGameplayCommands.TrySendAbility(client,0,pose),"Sequence wrap skips reserved zero");
        Check(!CoopGameplayCommands.CanApplyCooldown(client,0,uint.MaxValue)&&CoopGameplayCommands.CanApplyCooldown(client,0,1),"Wrapped command does not accept a stale pre-wrap acknowledgement");
    }
    sealed class Outcome
    {
        public float ActorHealth,ActorShield,TargetHealth,TargetShield,AllyHealth,AllyShield,Cooldown;
        public Vector3 Position;
        public string[] Events;
        public bool AtlasSeen,NativeBackendSeen;
    }
    static Outcome Scenario(HeroId id,int action,bool online,int fps,bool render=false)
    {
        Reset();var manager=NetworkManager.Singleton;
        CoopAuthority.Active=online;SystemInfo.graphicsDeviceType=render?UnityEngine.Rendering.GraphicsDeviceType.Vulkan:UnityEngine.Rendering.GraphicsDeviceType.Null;
        var actor=Hero(id,0);var target=Hero(HeroId.Kaelon,1);var ally=Hero(HeroId.Kaelon,2);
        actor.GetComponent<Health>().Current=500;ally.GetComponent<Health>().Current=500;
        float range=action<0?actor.GetComponent<HeroCombatProfile>().AttackRange:AimingSettings.Current.For(id,action).castRange;
        if(range<=0)range=2;
        target.transform.position=Vector3.forward*Mathf.Min(2,range*.6f);ally.transform.position=Vector3.forward*1.2f;
        // Wait out the shared initial ultimate lock; do not bypass production readiness checks.
        UnityEngine.Clock.Advance(HeroAbilityKit.UltimateInitialLock+1);
        Health selected=id==HeroId.Mira&&(action==1||action==2)?ally.GetComponent<Health>():target.GetComponent<Health>();
        var pose=new AimPose(Vector3.forward,selected.transform.position,selected,true);
        var events=new System.Collections.Generic.List<string>();
        Action<HeroVfxEvent> observer=e=>{if(e.Actor==actor)events.Add(e.Cue+":"+e.Slot);};
        HeroVfxEvents.Emitted+=observer;
        bool accepted;
        if(online)
        {
            // Serialize an actual client request, then feed its bytes to the server handler.
            var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();GameObject.Invoke(service,"Update");
            manager.IsServer=false;CoopAuthority.LocalHasServerAuthority=false;actor.GetComponent<CoopHeroSlot>().IsLocalHuman=true;
            Check(action<0?CoopGameplayCommands.TrySendBasic(actor,pose):CoopGameplayCommands.TrySendAbility(actor,action,pose),"Parity request serializes "+id+action);
            var bytes=manager.CustomMessagingManager.LastPacket;var message=manager.CustomMessagingManager.LastMessage;
            manager.IsServer=true;CoopAuthority.LocalHasServerAuthority=true;actor.GetComponent<CoopHeroSlot>().IsLocalHuman=false;
            manager.CustomMessagingManager.Handlers[message](7,new FastBufferReader(bytes));
            accepted=(int)typeof(CoopGameplayCommands).GetField("acceptedCombat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(service)==1;
        }
        else accepted=action<0?actor.GetComponent<BasicAttack>().TryAttackAimed(pose):actor.GetComponent<PlayerHeroAbilities>().CastAimed(action,pose);
        Check(accepted,"Valid offline/server action begins "+id+action+" fps="+fps);
        bool atlasSeen=AtlasVisible().Length>0;
        for(int frame=0;frame<fps*5;frame++){Step(1f/fps);if(render)atlasSeen|=AtlasVisible().Length>0;}
        HeroVfxEvents.Emitted-=observer;
        return new Outcome{ActorHealth=actor.GetComponent<Health>().Current,ActorShield=actor.GetComponent<Health>().Shield,
            TargetHealth=target.GetComponent<Health>().Current,TargetShield=target.GetComponent<Health>().Shield,
            AllyHealth=ally.GetComponent<Health>().Current,AllyShield=ally.GetComponent<Health>().Shield,
            Position=actor.transform.position,Events=events.ToArray(),AtlasSeen=atlasSeen,NativeBackendSeen=Bolt7Vfx.Calls>0,
            Cooldown=action<0?actor.GetComponent<BasicAttack>().RemainingCooldown:actor.GetComponent<PlayerHeroAbilities>().RemainingFor(action)};
    }
    static void OfflineOnlineParity()
    {
        foreach(int fps in new[]{30,60,120})foreach(HeroId id in Enum.GetValues(typeof(HeroId)))for(int action=-1;action<4;action++)
        {
            var offline=Scenario(id,action,false,fps);var online=Scenario(id,action,true,fps);
            Check(Mathf.Abs(offline.ActorHealth-online.ActorHealth)<.001f&&Mathf.Abs(offline.ActorShield-online.ActorShield)<.001f,"Parity actor HP/shield "+id+action);
            Check(Mathf.Abs(offline.TargetHealth-online.TargetHealth)<.001f&&Mathf.Abs(offline.TargetShield-online.TargetShield)<.001f,"Parity damage "+id+action);
            Check(Mathf.Abs(offline.AllyHealth-online.AllyHealth)<.001f&&Mathf.Abs(offline.AllyShield-online.AllyShield)<.001f,"Parity support "+id+action);
            Check((offline.Position-online.Position).sqrMagnitude<.000001f,"Parity dash endpoint "+id+action);
            Check(Mathf.Abs(offline.Cooldown-online.Cooldown)<.001f,"Parity cooldown "+id+action);
            Check(offline.Events.SequenceEqual(online.Events),"Parity semantic VFX sequence "+id+action);
            Check(online.Events.Length>0,"Action produces semantic presentation "+id+action);
        }
    }
    static object ActiveShield(GameObject target)
    {
        object pool=typeof(HeroVfxPool).GetField("instance",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        if(pool==null)return null;
        foreach(object slot in (Array)typeof(HeroVfxPool).GetField("slots",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pool))
        {
            if(slot==null)continue;var t=slot.GetType();
            if((bool)t.GetField("Active").GetValue(slot)&&(bool)t.GetField("ShieldBound").GetValue(slot)&&
                ((MeshFilter)t.GetField("Filter").GetValue(slot)).sharedMesh.name=="DF_AuthoredFx_Shield"&&
                ReferenceEquals(t.GetField("Follow").GetValue(slot),target.transform))return slot;
        }
        return null;
    }
    static Transform SlotTransform(object slot)=>(Transform)slot.GetType().GetField("Transform").GetValue(slot);
    static void NetworkPresentationFollowers()
    {
        foreach(int fps in new[]{30,60,120})
        {
            Reset();CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton.IsServer=false;
            var actor=Hero(HeroId.Nyra,0,true);var target=Hero(HeroId.Kaelon,1);
            var motion=target.GetComponent<HeroVisualMotion>();motion.HasNetworkPresentation=true;
            target.transform.position=motion.PresentedPosition=new Vector3(0,0,4);
            var state=target.AddComponent<NetworkHealthReplica>();state.ApplyNetworkSnapshot(950,50,false);
            var hp=target.GetComponent<Health>();hp.Shield=state.Shield;
            HeroSkillVfx.Support(actor,hp,true,3);
            object shield=ActiveShield(target);Check(shield!=null,"Authoritative shield creates a live attached effect");
            var pool=GameObject.All.Select(g=>g.GetComponent<HeroVfxPool>()).First(x=>x!=null);
            var targeting=actor.GetComponent<SoftTargeting>()??actor.AddComponent<SoftTargeting>();
            typeof(SoftTargeting).GetField("current",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(targeting,hp);
            Camera.main=new GameObject("camera").AddComponent<Camera>();
            var hud=new GameObject("target HUD").AddComponent<Alterverse.UI.TargetIndicatorHUD>();GameObject.Invoke(hud,"Start");
            var buffer=new HeroPresentationBuffer();buffer.Push(target.transform.position,Quaternion.identity,0,true);
            // Includes variable inter-arrival times and a 570ms packet gap: retain last known state.
            float[] arrivals={.08f,.17f,.31f,.34f,.42f,.99f,1.08f,1.17f};int next=0;
            for(int frame=1;frame<=fps*1.25f;frame++)
            {
                UnityEngine.Clock.Advance(1f/fps);
                while(next<arrivals.Length&&Time.time>=arrivals[next])
                {
                    float received=arrivals[next++];target.transform.position=new Vector3(received*4,0,4);
                    buffer.Push(target.transform.position,Quaternion.identity,received,false);
                    state.ApplyNetworkSnapshot(950,50,false);
                }
                hp.Shield=state.Shield;
                Vector3 previous=SlotTransform(shield).position;
                GameObject.Invoke(pool,"Update");
                Check((SlotTransform(shield).position-previous).sqrMagnitude<.000001f,"Update does not draw from a stale presentation pose before hero LateUpdate");
                buffer.Evaluate(Time.time,out motion.PresentedPosition,out motion.PresentedRotation);
                Vector3 raw=target.transform.position;GameObject.Invoke(pool,"LateUpdate");
                Check(ActiveShield(target)!=null,"Packet gap cannot erase the last authoritative shield");
                Check((SlotTransform(shield).position-(motion.PresentedPosition+Vector3.down*.70f)).sqrMagnitude<.000001f,"Shield uses the visible position in the same rendered frame at "+fps);
                int rings=Alterverse.UI.CombatGlyphs.Rings;GameObject.Invoke(hud,"OnGUI");
                Check(Alterverse.UI.CombatGlyphs.Rings==rings+2,"Soft target is actually painted by the production HUD");
                Check(Mathf.Abs(Alterverse.UI.CombatGlyphs.LastCenter.x-motion.PresentedPosition.x)<.00001f,"Reticle tracks the rendered hero instead of stepped snapshot position");
                Check((target.transform.position-raw).sqrMagnitude==0,"Presentation never changes authoritative collision/target position");
            }
            state.ApplyNetworkSnapshot(950,0,false);hp.Shield=state.Shield;GameObject.Invoke(pool,"Update");
            Check(ActiveShield(target)==null,"Confirmed shield break still removes the effect immediately");
            // Switching targets must invalidate the cached visual anchor.
            var other=Hero(HeroId.Elyra,3);other.transform.position=new Vector3(9,0,4);
            var otherMotion=other.GetComponent<HeroVisualMotion>();otherMotion.HasNetworkPresentation=true;otherMotion.PresentedPosition=new Vector3(7,0,4);
            typeof(SoftTargeting).GetField("current",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(targeting,other.GetComponent<Health>());
            GameObject.Invoke(hud,"OnGUI");Check(Mathf.Abs(Alterverse.UI.CombatGlyphs.LastCenter.x-7)<.00001f,"Changing target resolves its own presentation instead of retaining the old hero");
            otherMotion.HasNetworkPresentation=false;otherMotion.PresentedPosition=Vector3.zero;
            GameObject.Invoke(hud,"OnGUI");Check(Mathf.Abs(Alterverse.UI.CombatGlyphs.LastCenter.x-9)<.00001f,"Unready/local/disabled presentation falls back to actual transform");
            var minion=new GameObject("ordinary target");minion.transform.position=new Vector3(12,0,4);
            Check((HeroPresentationAnchor.Position(minion.transform)-minion.transform.position).sqrMagnitude==0,"Non-hero anchors retain ordinary world transforms");
            Check((HeroPresentationAnchor.Position(actor.transform,actor.GetComponent<HeroVisualMotion>())-actor.transform.position).sqrMagnitude==0,"Local owner keeps immediate predicted position");
            other.transform.localScale=new Vector3(2,3,.5f);other.transform.rotation=Quaternion.Euler(0,90,0);
            otherMotion.HasNetworkPresentation=true;otherMotion.PresentedRotation=Quaternion.Euler(0,180,0);
            Vector3 local=new Vector3(1,1,2),scaled=new Vector3(2,3,1);
            Check((HeroPresentationAnchor.TransformPoint(other.transform,otherMotion,local)-
                (otherMotion.PresentedPosition+otherMotion.PresentedRotation*scaled)).sqrMagnitude<.000001f,
                "Attached effects preserve scaled local offsets while using rendered rotation");
            Check((HeroPresentationAnchor.Rotation(other.transform,otherMotion)*Vector3.forward-
                otherMotion.PresentedRotation*Vector3.forward).sqrMagnitude<.000001f,"Attached effects face the rendered hero");
            otherMotion.HasNetworkPresentation=false;
            Check((HeroPresentationAnchor.TransformPoint(other.transform,otherMotion,local)-
                other.transform.TransformPoint(local)).sqrMagnitude<.000001f,"Offline attached effects retain native transform conversion");
        }
        Reset();var unit=new GameObject("health snapshot");var snapshot=unit.AddComponent<NetworkHealthReplica>();
        CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton.IsServer=false;
        snapshot.ApplyNetworkSnapshot(900,50,false);UnityEngine.Clock.Advance(2);
        Check(snapshot.Shield==50,"Real snapshot shield getter does not invent a local 350ms expiration");
        snapshot.ApplyNetworkSnapshot(900,20,false);Check(snapshot.Shield==20,"Authoritative shield reduction is immediate");
        snapshot.ApplyNetworkSnapshot(900,0,false);snapshot.AddShield(100,1,false);
        Check(snapshot.Shield==0,"Client cannot grant itself a shield while waiting for state");
        CoopAuthority.Active=false;CoopAuthority.LocalHasServerAuthority=true;
        snapshot.AddShield(40,.2f,false);Check(snapshot.Shield==40,"Offline/server ability shield still starts");
        UnityEngine.Clock.Advance(.21f);Check(snapshot.Shield==0,"Offline/server timed shields still expire normally");
        var attributes=typeof(HeroVfxPool).GetCustomAttributes(typeof(DefaultExecutionOrder),false);
        Check(attributes.Length==1,"Effect renderer declares its ordering after the hero presentation phase");
    }
    static void ThalaHealingRoutes()
    {
        Reset();ThalaHealingVfx.Available=true;ThalaHealingVfx.Targets=ThalaHealingVfx.Areas=0;
        var actor=Hero(HeroId.Mira,0);var target=Hero(HeroId.Kaelon,2);target.transform.position=new Vector3(3,0,4);
        var health=target.GetComponent<Health>();float hp=health.Current,shield=health.Shield;
        int old=Visible().Length;
        using(HeroVfxEvents.Replay(.17f,Vector3.zero))HeroSkillVfx.Support(actor,health,false,4);
        Check(ThalaHealingVfx.Targets==1&&ThalaHealingVfx.Target==target.transform,"Growth Veil routes native small healing to the actual single target");
        Check(Mathf.Abs(ThalaHealingVfx.Elapsed-.17f)<.0001f,"Target healing preserves delayed network playback time");
        Check(Visible().Length==old,"Prepared target healing replaces old layers rather than stacking them");
        HeroSkillVfx.Support(actor,health,true,4);
        Check(ThalaHealingVfx.Targets==1&&Visible().Length>old,"Venn Shelter and shield visuals retain their original rendering");
        int areas=ThalaHealingVfx.Areas;var center=new Vector3(7,0,-3);
        HeroSkillVfx.Area(actor,center,5,.4f,true,true,3);
        Check(ThalaHealingVfx.Areas==areas,"Wildsurge warning retains its original true-radius presentation");
        old=Visible().Length;
        using(HeroVfxEvents.Replay(.12f,Vector3.zero))HeroSkillVfx.Area(actor,center,5,.3f,false,true,3);
        Check(ThalaHealingVfx.Areas==areas+1&&ThalaHealingVfx.Radius==5&&ThalaHealingVfx.Center.x==7&&ThalaHealingVfx.Center.z==-3,"Wildsurge uses the large heal at its real center and live AoE radius");
        Check(Mathf.Abs(ThalaHealingVfx.Elapsed-.12f)<.0001f,"Area healing preserves delayed network playback time");
        Check(Visible().Length==old,"Native Wildsurge replaces old area polish and motifs");
        HeroSkillVfx.Area(actor,center,5,.3f,false,true,0);
        HeroSkillVfx.Area(Hero(HeroId.Kaelon,4),center,5,.3f,false,true,3);
        Check(ThalaHealingVfx.Areas==areas+1,"Only Thala Wildsurge uses the new large healing effect");
        int targets=ThalaHealingVfx.Targets;HeroSkillVfx.Support(Hero(HeroId.Rook,6),health,false,.45f);
        Check(ThalaHealingVfx.Targets==targets,"Other heroes keep their previous heal presentation");
        Check(health.Current==hp&&health.Shield==shield,"Visual helpers never apply healing or shields themselves");
        ThalaHealingVfx.Available=false;old=Visible().Length;
        HeroSkillVfx.Support(actor,health,false,4);
        Check(Visible().Length>old,"Unprepared healing keeps its visible original fallback");
    }
    static void SelectedAbilityRoutes()
    {
        Reset();SelectedAbilityVfx.Available=true;SelectedAbilityVfx.Areas=SelectedAbilityVfx.Shots=SelectedAbilityVfx.Echoes=0;
        var zhaev=Hero(HeroId.Veyr,0);var victim=Hero(HeroId.Kaelon,1);var health=victim.GetComponent<Health>();victim.transform.position=Vector3.forward*2;
        int before=Visible().Length;HeroSkillVfx.Spell(zhaev,victim.transform.position,1,.2f);
        Check(SelectedAbilityVfx.Shots==1&&Visible().Length==before,"Rift Scar replaces old purple-X release rather than stacking it");
        HeroSkillVfx.Impact(zhaev,health,3);Check(SelectedAbilityVfx.Echoes==1,"confirmed Vacant Echo hit selects blood explosion");
        foreach(var hero in new[]{HeroId.Kaelon,HeroId.Rook})
        {
            var actor=Hero(hero,2);before=Visible().Length;HeroSkillVfx.Area(actor,new Vector3(2,0,3),3.2f,.3f,false,false,3);
            Check(SelectedAbilityVfx.LastKind==(hero==HeroId.Kaelon?SelectedAbilityVfx.Circle:SelectedAbilityVfx.Fire),"selected ultimate maps to correct native family "+hero);
            Check(SelectedAbilityVfx.LastRadius==3.2f&&Visible().Length==before,"selected ultimate preserves live radius and replaces old core "+hero);
        }
        var timmy=Hero(HeroId.Timmy,4);SelectedAbilityVfx.SmokeCalls=0;HeroSkillVfx.Dash(timmy,Vector3.zero,Vector3.forward*3,2,.24f);
        HeroSkillVfx.Dash(timmy,Vector3.zero,Vector3.forward*3,3,.24f);
        Check(SelectedAbilityVfx.SmokeCalls==4,"Jet Pounce and Scratch each emit smoke at both jetpack nozzles");
        var manager=NetworkManager.Singleton;var relay=new GameObject("selected relay").AddComponent<CoopHeroStateSync>();relay.Bind(manager.CustomMessagingManager);
        relay.Slot(timmy.GetComponent<CoopHeroSlot>());relay.Slot(victim.GetComponent<CoopHeroSlot>());
        int starts=SelectedAbilityVfx.HoldStarts,stops=SelectedAbilityVfx.HoldStops;before=manager.CustomMessagingManager.Packets.Count;
        HeroSkillVfx.TimmyHold(timmy,health,1.8f);var start=manager.CustomMessagingManager.LastPacket;
        HeroSkillVfx.TimmyHold(timmy,health,0);var stop=manager.CustomMessagingManager.LastPacket;
        Check(manager.CustomMessagingManager.Packets.Count==before+2,"hold start and end each publish one bounded semantic event");
        CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;
        relay.Receive(0,start);manager.ServerTime.Time+=2;relay.Receive(0,stop);
        Check(SelectedAbilityVfx.HoldStarts==starts+2&&SelectedAbilityVfx.HoldStops==stops+2,"clients receive stars start and even delayed explicit stop");
        Check(manager.CustomMessagingManager.Packets.Count==before+2,"replayed hold events cannot echo");
        relay.Unbind();SelectedAbilityVfx.Available=false;SelectedAbilityVfx.StarsSound=null;
    }
    static void TravelerLightningRoutes()
    {
        Reset();TravelerLightningVfx.Available=true;TravelerLightningVfx.Casts=TravelerLightningVfx.Launches=TravelerLightningVfx.Hits=TravelerLightningVfx.Areas=0;
        var actor=Hero(HeroId.Traveler,0);var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*3;var hp=target.GetComponent<Health>();
        int before=Visible().Length;HeroSkillVfx.Cast(actor,0);HeroSkillVfx.Spell(actor,target.transform.position,0,.2f);HeroSkillVfx.Impact(actor,hp,0);
        Check(TravelerLightningVfx.Casts==1&&TravelerLightningVfx.Launches==1&&TravelerLightningVfx.Hits==1,"Temporal routes preparation, flight and confirmed contact through native chain");
        Check(Visible().Length==before,"Temporal native chain replaces all former cast/shot/contact layers");
        HeroSkillVfx.Cast(actor,2);HeroSkillVfx.Spell(actor,target.transform.position,2,.38f);HeroSkillVfx.Impact(actor,hp,2);HeroSkillVfx.Area(actor,target.transform.position,2.2f,.3f,false,false,2);
        Check(TravelerLightningVfx.Areas==1&&TravelerLightningVfx.Radius==2.2f&&Visible().Length==before,"Chrono native chain owns one central actual-radius detonation");
        HeroSkillVfx.Area(actor,target.transform.position,2.2f,.38f,true,false,2);Check(Visible().Length>before,"Chrono retains its accurate original warning boundary");
        before=TravelerLightningVfx.Areas;HeroSkillVfx.Area(actor,target.transform.position,2.5f,.3f,false,false,3);Check(TravelerLightningVfx.Areas==before,"Traveler ultimate remains outside the two selected shot chains");
        before=Visible().Length;HeroSkillVfx.Debuff(actor,hp,2);Check(Visible().Length==before,"selected Chrono does not stack an old debuff ring over native contact");
        HeroSkillVfx.Debuff(actor,hp,3);Check(Visible().Length>before,"Traveler ultimate retains its original victim debuff presentation");
        TravelerLightningVfx.Available=false;TravelerLightningVfx.NativeClips.Clear();
    }
    static void Main()
    {
        var manager=new NetworkManager{IsServer=true,IsListening=true,IsConnectedClient=true};
        NetworkManager.Singleton=manager;manager.ServerTime.Time=100;
        CoopAuthority.Active=true;CoopAuthority.LocalIsHost=false;CoopAuthority.LocalHasServerAuthority=true;
        var relay=new GameObject("relay").AddComponent<CoopHeroStateSync>();relay.Bind(manager.CustomMessagingManager);
        var actor=Hero(HeroId.Nyra,0);var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*3;
        relay.Slot(actor.GetComponent<CoopHeroSlot>());relay.Slot(target.GetComponent<CoopHeroSlot>());
        HeroSkillVfx.Spell(actor,target.transform.position,2,.4f,target.transform);
        Check(manager.CustomMessagingManager.Packets.Count==1,"Dedicated authority publishes a normal spell with LocalIsHost false");
        Check(manager.CustomMessagingManager.LastPacket.Length<256,"Spell event fits bounded writer");
        var packet=manager.CustomMessagingManager.LastPacket;
        CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;
        int before=Visible().Length;relay.Receive(0,packet);
        Check(Visible().Length>before,"Production receiver and full facade create visible client meshes");
        Check(manager.CustomMessagingManager.Packets.Count==1,"Client replay cannot echo an event");
        before=Visible().Length;relay.Receive(9,packet);Check(Visible().Length==before,"A non-server cannot inject visual events");
        relay.Unbind();NetworkPresentationFollowers();AllCues();CommandDamage();TenHeroVfxReserve();RealMotionEpochs();ServerFrameTelemetry();ConfirmedBasicWindups();PendingCooldowns();OfflineOnlineParity();
        AttackAtlasChecks();ThalaHealingRoutes();SelectedAbilityRoutes();TravelerLightningRoutes();
        Console.WriteLine("PASS "+checks+" production online VFX and combat/shader checks (controlled Unity/GPU/network fixtures)");
    }
}


