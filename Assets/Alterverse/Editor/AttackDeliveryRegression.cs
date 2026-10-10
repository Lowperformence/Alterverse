#if UNITY_EDITOR
using System;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    /// <summary>Run in a disposable Play Mode match. Advances pending timestamps, not game time.</summary>
    public static class AttackDeliveryRegression
    {
        private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        private static void Check(bool value,string reason)
        {if(!value)throw new InvalidOperationException("Attack delivery: "+reason);}
        private static void Tick(HeroAttackDelivery delivery)
        {typeof(HeroAttackDelivery).GetMethod("Update",Hidden).Invoke(delivery,null);}
        private static void Due(HeroAttackDelivery delivery,string field)
        {
            Array slots=(Array)typeof(HeroAttackDelivery).GetField("pending",Hidden).GetValue(delivery);
            for(int i=0;i<slots.Length;i++)
            {
                object entry=slots.GetValue(i);Type type=entry.GetType();
                if(!(bool)type.GetField("Active").GetValue(entry))continue;
                type.GetField(field=="HitAt"?"SampleTime":field).SetValue(entry,Time.time-1);
                slots.SetValue(entry,i);
            }
        }
        private static void Drain(HeroAttackDelivery delivery)
        {Due(delivery,"ReleaseAt");Tick(delivery);Due(delivery,"HitAt");Tick(delivery);}
        private static float Total(Health health)=>health.Current+health.Shield;
        private static float Fresh(Health health){health.RestoreFull();return Total(health);}
        private static GameObject Actor(HeroId hero,int team,Vector3 position)
        {
            var go=new GameObject("DF_AttackDeliveryRegression");go.transform.position=position;
            go.AddComponent<Health>().Configure(1000);
            go.AddComponent<HeroIdentity>().Configure(hero);
            go.AddComponent<HeroCombatProfile>().Configure(hero);
            go.AddComponent<HeroAbilityKit>().Configure(hero);
            go.AddComponent<TeamMember>().Initialize(team);
            return go;
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Attack delivery (Play mode)")]
        public static void Run()
        {
            if(!Application.isPlaying||Time.timeScale<=0||Alterverse.Multiplayer.CoopAuthority.Active)
            {Debug.LogWarning("Run attack delivery checks in a disposable, unpaused Play Mode match.");return;}
            for(int hero=0;hero<HeroIdentity.HeroCount;hero++)
            {
                GameObject source=Actor((HeroId)hero,0,new Vector3(0,80,0));
                GameObject victim=Actor(HeroId.Kaelon,1,new Vector3(1,80,0));
                try
                {
                    Health target=victim.GetComponent<Health>();Health self=source.GetComponent<Health>();
                    HeroAttackDelivery delivery=HeroAttackDelivery.For(source);
                    float power=HeroIdentity.AttackDamage((HeroId)hero);
                    float applied=power*((HeroId)hero==HeroId.Rook?1.05f:1f),baseline=Total(target);
                    HeroCombatRules.BasicHit(source,target,power);
                    Check(Mathf.Approximately(Total(target),baseline),"basic dealt damage before windup");
                    Tick(delivery);Check(Mathf.Approximately(Total(target),baseline),"early update dealt damage");
                    Due(delivery,"ReleaseAt");Tick(delivery);
                    Check(Mathf.Abs(Total(target)-(HeroAttackDelivery.IsRanged((HeroId)hero)?baseline:baseline-applied))<.002f,"release/contact timing mismatch");
                    Due(delivery,"HitAt");Tick(delivery);
                    Check(Mathf.Abs(Total(target)-(baseline-applied))<.002f,"contact changed basic damage");
                    Tick(delivery);Check(Mathf.Abs(Total(target)-(baseline-applied))<.002f,"basic resolved twice");

                    baseline=Fresh(target);target.AddShield(100,4,false);
                    HeroCombatRules.BasicHit(source,target,power);Drain(delivery);
                    Check(target.Current==1000&&Mathf.Approximately(target.TemporaryShield,100-applied),"shield absorption changed");

                    baseline=Fresh(target);HeroCombatRules.BasicHit(source,target,power);
                    victim.transform.position=new Vector3(30,80,0);Drain(delivery);
                    Check(Mathf.Approximately(Total(target),baseline),"out-of-range attack connected");
                    victim.transform.position=new Vector3(1,80,0);

                    HeroCombatRules.BasicHit(source,target,power);
                    var status=source.GetComponent<HeroCombatStatus>()??source.AddComponent<HeroCombatStatus>();
                    status.Stun(.5f);Drain(delivery);status.Clear();Drain(delivery);
                    Check(Mathf.Approximately(Total(target),baseline),"stun retained a pending hit");

                    HeroCombatRules.BasicHit(source,target,power);source.SetActive(false);source.SetActive(true);Drain(delivery);
                    Check(Mathf.Approximately(Total(target),baseline),"disable retained a pending hit");

                    HeroCombatRules.BasicHit(source,target,power);self.TakeDamage(2000);self.RestoreFull();Drain(delivery);
                    Check(Mathf.Approximately(Total(target),baseline),"death/restore retained a pending hit");

                    HeroCombatRules.BasicHit(source,target,power);target.DamageGate=()=>false;Drain(delivery);
                    Check(Mathf.Approximately(Total(target),baseline),"damage gate bypassed");target.DamageGate=null;

                    if(HeroAttackDelivery.IsRanged((HeroId)hero))
                    {
                        HeroCombatRules.BasicHit(source,target,power);Due(delivery,"ReleaseAt");Tick(delivery);
                        victim.transform.position=new Vector3(30,80,0);status.Stun(.5f);
                        Due(delivery,"HitAt");Tick(delivery);
                        Check(Mathf.Approximately(Total(target),baseline),"released projectile tracked a dodging enemy");
                        status.Clear();baseline=Fresh(target);victim.transform.position=new Vector3(1,80,0);
                    }
                    if(!HeroAttackDelivery.IsRanged((HeroId)hero))
                    {
                        float range=source.GetComponent<HeroCombatProfile>().AttackRange;
                        baseline=Fresh(target);victim.transform.position=new Vector3(range-.05f,80,0);
                        HeroCombatRules.BasicHit(source,target,power);
                        victim.transform.position=new Vector3(range+.20f,80,0);Drain(delivery);
                        Check(Mathf.Approximately(Total(target),baseline),"melee hit beyond the displayed range");
                        baseline=Fresh(target);HeroCombatRules.BasicHit(source,target,power);Drain(delivery);
                        Check(Mathf.Approximately(Total(target),baseline),"grace allowed an initially invalid attack");
                        victim.transform.position=new Vector3(1,80,0);
                    }
                    // Ione Q uses the fixed-centre area coroutine.
                    if((HeroId)hero!=HeroId.Elyra)
                    {
                        var execution=source.AddComponent<HeroAbilityExecution>();
                        Check(execution.TryCast(0,target,Vector3.forward),"Q rejected");
                        float remaining=execution.Remaining(0);
                        Check(Mathf.Approximately(Total(target),baseline)&&remaining>0,"Q timing/cooldown not committed at cast");
                        Drain(delivery);
                        float expected=HeroAbilityKit.LoadoutFor((HeroId)hero).Q.Power*((HeroId)hero==HeroId.Rook?1.05f:1f);
                        Check(Mathf.Abs(Total(target)-(baseline-expected))<.002f,"Q damage changed");
                        Check(Mathf.Abs(execution.Remaining(0)-remaining)<.001f,"delivery rewrote cooldown");
                    }
                }
                finally
                {
                    source.SetActive(false);victim.SetActive(false);
                    UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(victim);
                }
            }
            HookChecks();
            Debug.Log("DIMENSIONS FRONT: all-hero delivery, single application, shields, range, stun, disable, death, gate and targeted-Q checks passed. Visual/audio/device tests remain separate.");
        }

        private static void HookChecks()
        {
            GameObject source=Actor(HeroId.Nerok,0,new Vector3(0,80,0));
            GameObject victim=Actor(HeroId.Kaelon,1,new Vector3(4,80,0));
            GameObject wall=null;
            try
            {
                var execution=source.AddComponent<HeroAbilityExecution>();
                var victimExecution=victim.AddComponent<HeroAbilityExecution>();
                var target=victim.GetComponent<Health>();
                var delivery=HeroAttackDelivery.For(source);
                var status=source.GetComponent<HeroCombatStatus>()??source.AddComponent<HeroCombatStatus>();
                float before=Total(target),power=HeroAbilityKit.LoadoutFor(HeroId.Nerok).Q.Power;
                delivery.Ability(execution,0,target,windup:0);Tick(delivery);
                status.Stun(.5f);
                typeof(HeroAbilityExecution).GetMethod("Update",Hidden).Invoke(execution,null);
                Due(delivery,"HitAt");Tick(delivery);
                Check(Mathf.Abs(before-Total(target)-power)<.002f,"released hook cancelled on caster stun");
                var body=victim.GetComponent<HeroLocomotion>();
                Check(body.IsForcedMoving,"hook contact did not start pull");
                victim.GetComponent<HeroCombatStatus>().Stun(.5f);
                typeof(HeroAbilityExecution).GetMethod("Update",Hidden).Invoke(victimExecution,null);
                Check(body.IsForcedMoving,"victim stun cancelled incoming hook pull");
                body.ResetMotion();status.Clear();before=Fresh(target);
                victim.transform.position=new Vector3(4,80,0);

                delivery.Ability(execution,0,target,windup:0);Tick(delivery);
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name="DF_HookWallRegression";wall.layer=GameplayCollision.Structures;
                wall.transform.position=new Vector3(2,80.4f,0);wall.transform.localScale=new Vector3(.3f,2,2);
                Physics.SyncTransforms();
                Due(delivery,"HitAt");Tick(delivery);
                Check(Mathf.Approximately(Total(target),before),"hook passed through a real wall");
                Check(!body.IsForcedMoving,"blocked hook pulled victim");
                delivery.Ability(execution,0,target,windup:0);Drain(delivery);
                Check(Mathf.Approximately(Total(target),before),"hook released through an existing wall");
                wall.SetActive(false);Physics.SyncTransforms();
                target.DamageGate=()=>false;
                delivery.Ability(execution,0,target,windup:0);Drain(delivery);
                Check(Mathf.Approximately(Total(target),before),"hook bypassed damage gate");
                Check(!body.IsForcedMoving,"rejected hook damage still pulled victim");
            }
            finally
            {
                source.SetActive(false);victim.SetActive(false);if(wall!=null)wall.SetActive(false);
                UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(victim);
                if(wall!=null)UnityEngine.Object.Destroy(wall);
            }
        }
    }
}
#endif
