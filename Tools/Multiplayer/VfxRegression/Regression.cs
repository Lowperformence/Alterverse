using System;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using Unity.Netcode;
using UnityEngine;

static class VfxRegression
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
        GameObject.All.Clear();Health.Active.Clear();UnityEngine.Clock.Reset();Debug.ErrorCount=0;
        typeof(HeroVfxPool).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
        typeof(CoopGameplayCommands).GetProperty("Instance").SetValue(null,null);
        CoopAuthority.Active=true;CoopAuthority.LocalIsHost=false;CoopAuthority.LocalHasServerAuthority=true;
        CoopAuthority.ActivePlan=new();CoopAuthority.ActivePlan.Session.Slots[0]=new MatchPlayerSlot{PlayerId="test"};
        NetworkManager.Singleton=new(){IsServer=true,IsListening=true,IsConnectedClient=true};
        NetworkManager.Singleton.ServerTime.Time=100;
        Resources.HeroShaderSupported=true;SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
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
            Check(Visible().Length>0,"Real authoritative combat creates client effect meshes");
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
        manager.CustomMessagingManager.Handlers["DF_COOP_BASIC_V6"](7,new FastBufferReader(basicPacket));
        Check(CoopGameplayCommands.ProcessedCommand(0)==1,"Authorized basic is acknowledged");
        int accepted=(int)typeof(CoopGameplayCommands).GetField("acceptedCombat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(service);
        manager.CustomMessagingManager.Handlers["DF_COOP_BASIC_V6"](7,new FastBufferReader(basicPacket));
        Check((int)typeof(CoopGameplayCommands).GetField("acceptedCombat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(service)==accepted,"Duplicate command cannot execute damage twice");
        // Force a legitimate rejection after the local prediction: server is still on cooldown.
        server.GetComponent<HeroAbilityExecution>().MarkNetworkRequestCooldown(0);
        manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V6"](7,new FastBufferReader(abilityPacket));
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
    }
    static Outcome Scenario(HeroId id,int action,bool online,int fps)
    {
        Reset();var manager=NetworkManager.Singleton;
        CoopAuthority.Active=online;SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Null;
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
        for(int frame=0;frame<fps*5;frame++)Step(1f/fps);
        HeroVfxEvents.Emitted-=observer;
        return new Outcome{ActorHealth=actor.GetComponent<Health>().Current,ActorShield=actor.GetComponent<Health>().Shield,
            TargetHealth=target.GetComponent<Health>().Current,TargetShield=target.GetComponent<Health>().Shield,
            AllyHealth=ally.GetComponent<Health>().Current,AllyShield=ally.GetComponent<Health>().Shield,
            Position=actor.transform.position,Events=events.ToArray(),
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
        relay.Unbind();AllCues();CommandDamage();TenHeroVfxReserve();RealMotionEpochs();ServerFrameTelemetry();PendingCooldowns();OfflineOnlineParity();
        Console.WriteLine("PASS "+checks+" production online VFX and combat/shader checks (controlled Unity/GPU/network fixtures)");
    }
}
