using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.UI;

class Regression
{
    static int checks;
    static void Check(bool valid,string name){checks++;if(!valid)throw new Exception(name);}
    static void Near(float actual,float expected,string name)
        =>Check(Math.Abs(actual-expected)<.01f,name+" actual="+actual+" expected="+expected);
    static void Call(object instance,string name)=>GameObject.Invoke(instance,name);
    static void Reset()
    {
        foreach(var g in GameObject.All.ToArray())g.SetActive(false);
        GameObject.All.Clear();Health.Active.Clear();Clock.Reset();Camera.main=null;
        CoopAuthority.Active=false;CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.LocalIsHost=true;
        Unity.Netcode.NetworkManager.Singleton=null;
        AimingSettings.Current.overrides=Array.Empty<AimingSettings.AttackOverride>();
        Resources.PreviewShaderAvailable=true;
    }
    static GameObject Hero(HeroId hero,int team,Vector3 point)
    {
        var g=new GameObject(hero.ToString());g.transform.position=point;
        g.AddComponent<TeamMember>().Initialize(team);g.AddComponent<Health>();
        g.AddComponent<HeroIdentity>().Configure(hero);g.AddComponent<HeroCombatProfile>().Configure(hero);
        g.AddComponent<HeroAbilityKit>().Configure(hero);g.AddComponent<HeroMotor>();g.AddComponent<HeroLocomotion>();
        g.AddComponent<HeroAbilityExecution>();g.AddComponent<PlayerHeroAbilities>();g.AddComponent<BasicAttack>();
        Array.Clear((float[])typeof(HeroAbilityExecution).GetField("ready",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g.GetComponent<HeroAbilityExecution>()));
        return g;
    }
    static void Step(float dt,Action move=null)
    {
        foreach(var g in GameObject.All.ToArray())
        {
            var body=g.GetComponent<HeroLocomotion>();
            if(body!=null){body.FrameStartPosition=g.transform.position;body.Advance(Time.time+dt);}
        }
        Clock.Advance(dt);move?.Invoke();
        foreach(var g in GameObject.All.ToArray())if(g.activeInHierarchy)
        {
            if(g.GetComponent<HeroAbilityExecution>() is {} execution)Call(execution,"Update");
            if(g.GetComponent<HeroAttackDelivery>() is {} delivery)Call(delivery,"Update");
        }
    }
    static void Run(float duration,int fps=60){for(int i=0;i<Mathf.CeilToInt(duration*fps);i++)Step(1f/fps);}
    static float Hp(GameObject target)=>target.GetComponent<Health>().Current;
    static Vector3 Angle(float degrees,float distance)=>new Vector3(Mathf.Sin(degrees*Mathf.Deg2Rad),0,Mathf.Cos(degrees*Mathf.Deg2Rad))*distance;
    static GameObject Wall(Vector3 point,Vector3 size)
    {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=point;wall.transform.localScale=size;return wall;}
    static AimPose Straight(GameObject actor)=>new AimPose(Vector3.forward,actor.transform.position+Vector3.forward*6.2f);
    static void ConeBoundaries()
    {
        foreach(int fps in new[]{30,60,120})foreach(float degrees in new[]{-34.9f,0f,34.9f})foreach(float distance in new[]{1f,4f,6.2f})
        {
            Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);var target=Hero(HeroId.Kaelon,1,Angle(degrees,distance));
            var aim=ManualAiming.For(actor);Check(aim.Begin(0,Vector2.zero),"hook gesture begins");
            Check(aim.Pose.HasAimPoint,"whole displayed cone captures at "+degrees+" / "+distance);
            Check(aim.Release(),"cone edge casts");Run(1,fps);
            Near(Hp(target),972,"stationary hook connects at cone/range edge, "+fps+" FPS");
        }
        foreach(var point in new[]{Angle(-50f,4),Angle(50f,4),Vector3.forward*7.2f})
        {
            Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);Hero(HeroId.Kaelon,1,point);
            var aim=ManualAiming.For(actor);aim.Begin(0,Vector2.zero);
            Check(!aim.Pose.HasAimPoint,"no capture outside displayed cone/range");
        }
    }
    static void GrazingContact()
    {
        foreach(int fps in new[]{30,60,120})
        {
            Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);var target=Hero(HeroId.Kaelon,1,new Vector3(5,0,4));
            // The flight at x=0 is clear; the target's centre sightline clips cover.
            Wall(new Vector3(.35f,.5f,2),new Vector3(.25f,2,.2f));
            var aim=ManualAiming.For(actor);aim.Begin(0,Vector2.zero);
            Check(!aim.Pose.HasAimPoint,"free hook starts with target outside capture cone");
            Check(aim.Release(),"grazing hook casts through real gesture");
            // The enemy enters the committed trajectory after release, beside the wall.
            target.transform.position=new Vector3(.5f,0,4);Run(1,fps);
            Near(Hp(target),1000,"cover blocks contact even for the active cone");
            Check(!target.GetComponent<HeroLocomotion>().IsForcedMoving,"blocked pull path cannot drag through cover");
        }
    }
    static GameObject[] Surfaces(AbilityRangePreviewOverlay overlay)
        =>(GameObject[])typeof(AbilityRangePreviewOverlay).GetField("surfaces",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(overlay);
    static void Preview()
    {
        foreach(float degrees in new[]{-34.9f,0f,34.9f})
        {
            Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);var target=Hero(HeroId.Kaelon,1,Angle(degrees,4));
            var aim=ManualAiming.For(actor);aim.Begin(0,Vector2.zero);
            var overlay=actor.AddComponent<AbilityRangePreviewOverlay>();Call(overlay,"LateUpdate");
            var surfaces=Surfaces(overlay);
            Check(surfaces[0]!=null&&surfaces[0].activeSelf,"hook exposes its true hitbox cone");
            Check(surfaces[1]==null||!surfaces[1].activeSelf,"no misleading narrow hit line");
            var cone=surfaces[0].GetComponent<MeshRenderer>().sharedMaterial;
            Near(cone.Floats[Shader.PropertyToID("_Mode")],1,"actual hook hitbox is a cone");
            Near(cone.Floats[Shader.PropertyToID("_ConeCos")],Mathf.Cos(35*Mathf.Deg2Rad),"preview matches authoritative half angle");
            Near(Hp(target),1000,"preview does not inflict damage");
            Near(actor.GetComponent<PlayerHeroAbilities>().QRemaining,0,"preview does not spend cooldown");
            Check(aim.Release(),"preview can release");Call(overlay,"LateUpdate");
            Check(surfaces.All(s=>s==null||!s.activeSelf),"release hides both surfaces");
            Run(1);Near(Hp(target),972,"shown path connects");
        }
        foreach(bool cancelled in new[]{false,true})
        {
            Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);var aim=ManualAiming.For(actor);aim.Begin(0,Vector2.zero);
            Wall(new Vector3(0,.5f,2),new Vector3(2,2,.2f));
            var overlay=actor.AddComponent<AbilityRangePreviewOverlay>();Call(overlay,"LateUpdate");
            var surfaces=Surfaces(overlay);
            Check(surfaces[0]!=null&&surfaces[0].activeSelf,"cone remains visible when cover is present");
            Check(surfaces[1]==null||!surfaces[1].activeSelf,"no fake narrow hit line beside cover");
            if(cancelled)aim.Drag(Vector2.zero,1,true);else aim.Cancel();
            Call(overlay,"LateUpdate");Check(surfaces.All(s=>s==null||!s.activeSelf),"cancel hides cone and path");
        }
        Reset();var other=Hero(HeroId.Nyra,0,Vector3.zero);ManualAiming.For(other).Begin(2,Vector2.zero);
        var otherOverlay=other.AddComponent<AbilityRangePreviewOverlay>();Call(otherOverlay,"LateUpdate");
        Check(Surfaces(otherOverlay)[1]==null,"other heroes keep their existing preview");
        Reset();var nerok=Hero(HeroId.Nerok,0,Vector3.zero);ManualAiming.For(nerok).Begin(1,Vector2.zero);
        var selfOverlay=nerok.AddComponent<AbilityRangePreviewOverlay>();Call(selfOverlay,"LateUpdate");
        Check(Surfaces(selfOverlay).All(s=>s==null),"self shield has no targeting geometry");
    }
    static void HookRules()
    {
        foreach(int fps in new[]{30,60,120})
        {
            Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);var target=Hero(HeroId.Kaelon,1,Vector3.forward*4);
            Wall(new Vector3(0,.5f,2),new Vector3(2,2,.2f));
            Check(actor.GetComponent<PlayerHeroAbilities>().CastAimed(0,Straight(actor)),"blocked hook can be attempted");Run(1,fps);
            Near(Hp(target),1000,"full cover still blocks hook damage");
            Check(!target.GetComponent<HeroLocomotion>().IsForcedMoving,"full cover blocks pull");
            Near(actor.GetComponent<Health>().Shield,0,"wall miss grants no passive shield");

            Reset();actor=Hero(HeroId.Nerok,0,Vector3.zero);target=Hero(HeroId.Kaelon,1,Vector3.forward*4);
            var second=Hero(HeroId.Kaelon,1,Vector3.forward*5.5f);var ally=Hero(HeroId.Kaelon,0,Vector3.forward*2);
            Check(actor.GetComponent<PlayerHeroAbilities>().CastQ(),"tap hook acquires");Run(1,fps);
            Near(Hp(target),972,"first enemy takes exactly one hook hit");Near(Hp(second),1000,"hook cannot pierce a second enemy");
            Near(Hp(ally),1000,"hook ignores allies");Near(actor.GetComponent<Health>().Shield,12,"hero contact grants passive shield once");
            Check(target.GetComponent<HeroLocomotion>().IsForcedMoving,"clear body contact starts pull");
            Check(!actor.GetComponent<PlayerHeroAbilities>().CastQ(),"cooldown prevents immediate repeated hook");

            Reset();actor=Hero(HeroId.Nerok,0,Vector3.zero);target=Hero(HeroId.Kaelon,1,Vector3.forward*4);
            target.GetComponent<Health>().DamageGate=()=>false;
            actor.GetComponent<PlayerHeroAbilities>().CastQ();Run(1,fps);
            Near(Hp(target),1000,"damage gate rejects hook");Near(actor.GetComponent<Health>().Shield,0,"gated contact grants no passive shield");
            Check(!target.GetComponent<HeroLocomotion>().IsForcedMoving,"gated contact cannot pull");

            Reset();actor=Hero(HeroId.Nerok,0,Vector3.zero);target=Hero(HeroId.Kaelon,1,Vector3.forward*4);
            var aim=ManualAiming.For(actor);aim.Begin(0,Vector2.zero);aim.Release();
            // The target remains inside the old 70-degree selection cone but dodges its frozen shot.
            target.transform.position+=Vector3.right*5;Run(1,fps);
            Near(Hp(target),1000,"body outside the committed cone still dodges");

            foreach(bool launched in new[]{false,true})
            {
                Reset();actor=Hero(HeroId.Nerok,0,Vector3.zero);target=Hero(HeroId.Kaelon,1,Vector3.forward*4);
                actor.GetComponent<PlayerHeroAbilities>().CastQ();Run(launched?.34f:.1f,fps);
                (actor.GetComponent<HeroCombatStatus>()??actor.AddComponent<HeroCombatStatus>()).Stun(1);Run(1,fps);
                Near(Hp(target),launched?972:1000,"stun interrupts preparation and preserves released flight");
            }
        }
        Reset();var bot=Hero(HeroId.Nerok,0,Vector3.zero);var foe=Hero(HeroId.Kaelon,1,Angle(25,4));
        Check(bot.GetComponent<HeroAbilityExecution>().TryCast(0,foe.GetComponent<Health>(),Vector3.forward),"bot target hook uses shared delivery");
        Run(1);Near(Hp(foe),972,"bot hook hits");
        Reset();bot=Hero(HeroId.Nerok,0,Vector3.zero);
        var minion=new GameObject("minion");minion.transform.position=Vector3.forward*3;
        minion.AddComponent<Health>();minion.AddComponent<LaneMinion>().Team=1;minion.AddComponent<HeroLocomotion>();
        Check(bot.GetComponent<PlayerHeroAbilities>().CastQ(),"hook selects minion");Run(1);
        Near(Hp(minion),972,"minion body takes hook damage");Near(bot.GetComponent<Health>().Shield,0,"minion hit grants no hero-only passive");
        Check(minion.GetComponent<HeroLocomotion>().IsForcedMoving,"mobile minion can be pulled");
    }
    static void RemainingAttacks()
    {
        foreach(int fps in new[]{30,60,120})
        {
            Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);var target=Hero(HeroId.Kaelon,1,Vector3.forward*1.5f);
            Check(actor.GetComponent<BasicAttack>().TryAttack(),"basic accepts valid target");Run(.1f,fps);Near(Hp(target),1000,"basic waits for windup");
            Run(.3f,fps);Near(Hp(target),976,"basic deals catalog damage once");
            Check(actor.GetComponent<PlayerHeroAbilities>().CastW(),"pressure hull casts");Near(actor.GetComponent<Health>().Shield,45,"pressure hull grants 45 shield");
            Near(HeroCombatStatus.Speed(actor),1.15f,"pressure hull grants movement boost");
            Check(!actor.GetComponent<PlayerHeroAbilities>().CastW(),"shield cannot repeat during cooldown");

            Reset();actor=Hero(HeroId.Nerok,0,Vector3.zero);target=Hero(HeroId.Kaelon,1,Vector3.forward*2.8f);
            var flank=Hero(HeroId.Kaelon,1,Vector3.left*2.7f);var outside=Hero(HeroId.Kaelon,1,Vector3.forward*2.81f);
            var ally=Hero(HeroId.Kaelon,0,Vector3.right*2);
            Check(actor.GetComponent<PlayerHeroAbilities>().CastE(),"breaker wave casts");Run(.1f,fps);Near(Hp(target),1000,"wave waits for warning");Run(.5f,fps);
            Near(Hp(target),974,"wave includes 2.8m radius edge");Near(Hp(flank),974,"wave hits multiple enemies around caster");
            Near(Hp(outside),1000,"wave excludes outside radius");Near(Hp(ally),1000,"wave excludes allies");Near(HeroCombatStatus.Speed(target),.75f,"wave applies catalog slow");

            Reset();actor=Hero(HeroId.Nerok,0,Vector3.zero);Vector3 center=Vector3.forward*4;
            target=Hero(HeroId.Kaelon,1,center);var edge=Hero(HeroId.Kaelon,1,center+Vector3.right*2.3f);
            outside=Hero(HeroId.Kaelon,1,center+Vector3.right*2.31f);ally=Hero(HeroId.Kaelon,0,center);
            Check(actor.GetComponent<PlayerHeroAbilities>().CastAimed(3,new AimPose(Vector3.forward,center)),"vortex places at chosen point");
            Run(.6f,fps);Near(Hp(target),1000,"vortex has no damage during warning");Run(3.3f,fps);
            Near(Hp(target),940,"vortex deals exactly six ticks / 60 damage");Near(Hp(edge),940,"vortex includes displayed radius edge");
            Near(Hp(outside),1000,"vortex excludes outside radius");Near(Hp(ally),1000,"vortex excludes allies");
        }
    }
    static void NetworkHook()
    {
        Reset();var actor=Hero(HeroId.Nerok,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;
        var target=Hero(HeroId.Kaelon,1,Angle(34.9f,4));var aim=ManualAiming.For(actor);aim.Begin(0,Vector2.zero);
        var pose=aim.Pose;aim.Cancel();var commands=new GameObject("commands").AddComponent<CoopGameplayCommands>();
        var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
        CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(commands,"Update");
        Check(CoopGameplayCommands.TrySendAbility(actor,0,pose),"client sends captured hook point");
        var packet=manager.CustomMessagingManager.LastPacket;
        CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;
        CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
        actor.transform.position=Vector3.right*.7f;
        manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V6"](1,new Unity.Netcode.FastBufferReader(packet));Run(1);
        Near(Hp(target),972,"host uses same captured hook trajectory after caster movement");
    }
    static void Main()
    {
        ConeBoundaries();GrazingContact();Preview();HookRules();RemainingAttacks();NetworkHook();
        Console.WriteLine("PASS "+checks+" Nerok checks; engine/network fixtures, not a Unity build.");
    }
}

