using System;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using UnityEngine;

partial class TimmyMovementRegression
{
    static int checks, failures;
    static void Check(bool ok,string reason)
    {
        checks++;
        if(!ok){failures++;Console.WriteLine("FAIL "+reason);}
    }
    static void Near(float actual,float expected,string reason)
        =>Check(Math.Abs(actual-expected)<.015f,reason+" actual="+actual+" expected="+expected);
    static void Call(object value,string method)=>GameObject.Invoke(value,method);
    static void Reset()
    {
        foreach(var g in GameObject.All.ToArray())g.SetActive(false);
        GameObject.All.Clear();Health.Active.Clear();Clock.Reset();
        CoopAuthority.Active=false;CoopAuthority.LocalHasServerAuthority=true;
        CoopAuthority.LocalIsHost=true;Unity.Netcode.NetworkManager.Singleton=null;
        Camera.main=null;UnityEngine.InputSystem.Keyboard.current=null;
        AimingSettings.Current.overrides=new AimingSettings.AttackOverride[0];
    }
    static GameObject Hero(HeroId id,int team,Vector3 position)
    {
        var g=new GameObject(id.ToString());g.transform.position=position+Vector3.up;
        g.AddComponent<TeamMember>().Initialize(team);g.AddComponent<Health>().Configure(1000);
        g.AddComponent<CharacterController>();g.AddComponent<HeroIdentity>().Configure(id);
        g.AddComponent<HeroAbilityKit>().Configure(id);g.AddComponent<HeroCombatProfile>().Configure(id);
        g.AddComponent<HeroMotor>();g.AddComponent<HeroAbilityExecution>();
        g.AddComponent<PlayerHeroAbilities>();g.AddComponent<BasicAttack>();
        Array.Clear((float[])typeof(HeroAbilityExecution).GetField("ready",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g.GetComponent<HeroAbilityExecution>()),0,4);
        return g;
    }
    static float Total(GameObject g)=>g.GetComponent<Health>().Current+g.GetComponent<Health>().Shield;
    static void Step(float dt,GameObject actor,Vector2 actorInput,GameObject target,Vector2 targetInput)
    {
        Clock.BeginFrame(dt);
        actor.GetComponent<HeroMotor>().SetMoveInput(actorInput);
        target.GetComponent<HeroMotor>().SetMoveInput(targetInput);
        foreach(var g in GameObject.All.ToArray().Where(x=>x.activeInHierarchy))
            if(g.GetComponent<HeroLocomotion>() is HeroLocomotion b)Call(b,"Update");
        foreach(var g in GameObject.All.ToArray().Where(x=>x.activeInHierarchy))
            if(g.GetComponent<HeroMotor>() is HeroMotor m)Call(m,"Update");
        foreach(var g in GameObject.All.ToArray().Where(x=>x.activeInHierarchy))
        {
            if(g.GetComponent<HeroAbilityExecution>() is HeroAbilityExecution e)Call(e,"Update");
            if(g.GetComponent<HeroAttackDelivery>() is HeroAttackDelivery d)Call(d,"Update");
        }
        Clock.Resume();
        foreach(var g in GameObject.All.ToArray().Where(x=>x.activeInHierarchy))
            if(g.GetComponent<HeroLocomotion>() is HeroLocomotion b)Call(b,"LateUpdate");
    }
    static AimPose Aim(GameObject a,GameObject b)=>new AimPose(b.transform.position-a.transform.position,b.transform.position,null,true);
    static bool Rooted(GameObject b)=>b.GetComponent<HeroCombatStatus>().Rooted;
    static void Run(float seconds,int fps,GameObject a,GameObject b,Vector2 ai=default,Vector2 bi=default)
    {for(int i=0;i<Mathf.CeilToInt(seconds*fps);i++)Step(1f/fps,a,ai,b,bi);}
    static void Main()
    {
        BasicMovementCases();
        foreach(int fps in new[]{30,60,120})
        {
            Reset();var a=Hero(HeroId.Timmy,0,Vector3.zero);var b=Hero(HeroId.Veyr,1,Vector3.forward*5);
            Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(3,Aim(a,b)),"Scratch retreat cast "+fps);
            for(int i=0;i<(int)(fps*.7f);i++)Step(1f/fps,a,Vector2.zero,b,new Vector2(0,1));
            Check(Rooted(b),"Scratch catches a running opponent still inside the 9m zone "+fps+" distance="+HeroCombatRules.Distance(a.transform.position,b.transform.position));
            for(int i=0;i<fps*3;i++)Step(1f/fps,a,Vector2.zero,b,new Vector2(0,1));
            Near(Total(b),1000-TimmyBalance.ScratchDamage,"Scratch retreat full damage "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*1.52f);
            // Warm both real motors to the same run speed, then swing while chasing.
            for(int i=0;i<fps;i++)Step(1f/fps,a,new Vector2(0,1),b,new Vector2(0,1));
            Check(a.GetComponent<BasicAttack>().TryAttack(),"Claw chasing cast "+fps);
            for(int i=0;i<(int)(fps*.3f);i++)Step(1f/fps,a,new Vector2(0,1),b,new Vector2(0,1));
            Near(Total(b),1000-TimmyBalance.BasicDamage,"Claw survives its own attack slowdown while chasing "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*1.52f);
            Run(1,fps,a,b,new Vector2(0,1),new Vector2(0,1));
            for(int i=0;i<fps*3;i++)
            {
                a.GetComponent<BasicAttack>().TryAttack();
                Step(1f/fps,a,new Vector2(0,1),b,new Vector2(0,1));
            }
            Near(Total(b),1000-TimmyBalance.BasicDamage*4,"held Claw keeps connecting across four chase attacks "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*2.12f);
            Run(1,fps,a,b,new Vector2(0,1),new Vector2(0,1));
            Check(a.GetComponent<PlayerHeroAbilities>().CastQ(),"Q chasing cast "+fps);
            Run(.4f,fps,a,b,new Vector2(0,1),new Vector2(0,1));
            Near(Total(b),1000-TimmyBalance.ClawDamage,"Q connects while chasing "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*5);
            Check(a.GetComponent<PlayerHeroAbilities>().CastUltimate(),"Scratch sideways cast "+fps);
            Run(.7f,fps,a,b,Vector2.zero,new Vector2(1,0));
            Check(Rooted(b),"Scratch tracks sideways movement within the original cone "+fps);
            Check(!Rooted(a),"Scratch never roots Timmy "+fps);
            float position=a.transform.position.x;
            Run(.4f,fps,a,b,new Vector2(-1,0));
            Check(a.transform.position.x<position-.2f,"Timmy moves during the victim hold "+fps);
            Run(2,fps,a,b,new Vector2(-1,0));
            Near(Total(b),1000-TimmyBalance.ScratchDamage,"movement preserves all Scratch ticks "+fps);
            Check(!Rooted(b),"victim is released on schedule "+fps);

            // Stay close to the angular/radial boundary, then leave it. Later
            // re-entry cannot restore a victim already disqualified by the cast.
            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);
            b=Hero(HeroId.Veyr,1,new Vector3(2.8f,0,4.15f));
            Check(a.GetComponent<PlayerHeroAbilities>().CastUltimate(),"Scratch cone-edge cast "+fps);
            Run(.15f,fps,a,b,Vector2.zero,new Vector2(1,0));
            b.transform.position=new Vector3(5,1,4.15f);Step(1f/fps,a,Vector2.zero,b,Vector2.zero);
            b.transform.position=new Vector3(2.8f,1,4.15f);Run(3,fps,a,b);
            Near(Total(b),1000,"field exit during warning stays revoked after re-entry "+fps);
            Check(!Rooted(b)&&a.transform.position.z>3,"field exit keeps the leap but applies no root "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*5);
            a.GetComponent<PlayerHeroAbilities>().CastUltimate();Run(.3f,fps,a,b);
            b.transform.position=new Vector3(6,1,5);Step(1f/fps,a,Vector2.zero,b,Vector2.zero);
            b.transform.position=new Vector3(0,1,5);Run(3,fps,a,b);
            Near(Total(b),1000,"field exit during flight stays revoked after re-entry "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*10);
            Check(a.GetComponent<PlayerHeroAbilities>().CastUltimate(),"no eligible victim still leaps "+fps);
            Run(.8f,fps,a,b);Near(a.transform.position.z,TimmyBalance.ScratchRange,"untargeted Scratch distance "+fps);
            Near(Total(b),1000,"untargeted leap never acquires a late contact "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*1.5f);
            Check(a.GetComponent<BasicAttack>().TryAttack(),"basic escape test cast "+fps);
            b.transform.position=Vector3.up+Vector3.forward*1.85f;Run(.4f,fps,a,b);
            Near(Total(b),1000-TimmyBalance.BasicDamage,"accepted claw commits within its initial range "+fps);
            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*1.85f);
            Check(!a.GetComponent<BasicAttack>().TryAttack(),"Claw cannot acquire an out-of-range basic "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*2);
            a.GetComponent<PlayerHeroAbilities>().CastQ();b.transform.position=Vector3.up+Vector3.right*2;
            Run(.5f,fps,a,b);Near(Total(b),1000,"Q leaving the melee cone still misses "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*4);
            Check(a.GetComponent<PlayerHeroAbilities>().CastW(),"W with live movement cast "+fps);
            Run(.4f,fps,a,b,Vector2.zero,new Vector2(1,0));
            Near(Total(b),1000-TimmyBalance.DustDamage,"W landing connects once with moving enemy "+fps);
            Run(2.5f,fps,a,b);Near(Total(b),1000-TimmyBalance.DustDamage,"W mist adds no duplicate damage "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*2);
            Check(a.GetComponent<PlayerHeroAbilities>().CastE(),"E actual dash cast "+fps);
            Run(.3f,fps,a,b);Near(a.transform.position.z,TimmyBalance.JetRange,"E actual dash reaches 4.5m "+fps);
            Near(Total(b),1000,"E stays a movement ability "+fps);

            Reset();a=Hero(HeroId.Timmy,0,Vector3.zero);b=Hero(HeroId.Veyr,1,Vector3.forward*5);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,2.5f);wall.transform.localScale=new Vector3(4,3,.3f);
            Check(!a.GetComponent<PlayerHeroAbilities>().CastUltimate(),"wall rejects targeted Scratch "+fps);
            Check(a.GetComponent<HeroAbilityExecution>().Remaining(3)==0,"wall rejection preserves cooldown "+fps);
            Near(Total(b),1000,"wall cannot be bypassed by target tracking "+fps);
        }
        Reset();var hitchActor=Hero(HeroId.Timmy,0,Vector3.zero);var hitchTarget=Hero(HeroId.Veyr,1,Vector3.forward*5);
        Check(hitchActor.GetComponent<PlayerHeroAbilities>().CastUltimate(),"Scratch hitch cast");
        Run(.3f,60,hitchActor,hitchTarget,Vector2.zero,new Vector2(0,1));
        Step(.3f,hitchActor,Vector2.zero,hitchTarget,new Vector2(0,1));
        Check(Rooted(hitchTarget),"a long landing frame cannot lose an eligible in-field target");
        foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
        {
            if(hero==HeroId.Timmy)continue;
            Reset();var actor=Hero(hero,0,Vector3.zero);var target=Hero(HeroId.Veyr,1,Vector3.forward*20);
            Run(1,60,actor,target,new Vector2(0,1));float before=actor.transform.position.z;
            actor.GetComponent<HeroMotor>().NotifyAttack(.5f,Vector3.forward);
            Run(.3f,60,actor,target,new Vector2(0,1));
            float ratio=(actor.transform.position.z-before)/(actor.GetComponent<HeroCombatProfile>().MoveSpeed*.3f);
            Check(ratio>.87f&&ratio<.90f,"existing hero retains its attack movement slowdown: "+hero);
        }
        Console.WriteLine((failures==0?"PASS ":"FAILED ")+checks+" Timmy production motor/locomotion checks; failures="+failures);
        if(failures!=0)Environment.Exit(1);
    }
}
