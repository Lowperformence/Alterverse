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
            object[] components={g.GetComponent<HeroAbilityExecution>(),g.GetComponent<HeroAttackDelivery>(),g.GetComponent<HeroVfxPool>()};
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
        int emitted=0;Action<HeroVfxEvent> observer=e=>emitted++;HeroVfxEvents.Emitted+=observer;
        CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;
        foreach(var packet in packets)relay.Receive(0,packet);
        Check(emitted==0,"Client nested replays cannot publish or echo another event");
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
        relay.Unbind();AllCues();CommandDamage();
        Console.WriteLine("PASS "+checks+" production online VFX and combat/shader checks (controlled Unity/GPU/network fixtures)");
    }
}
