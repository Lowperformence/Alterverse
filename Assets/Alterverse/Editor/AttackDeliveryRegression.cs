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
                type.GetField(field).SetValue(entry,Time.time-1);
                slots.SetValue(entry,i);
            }
        }
        private static void Drain(HeroAttackDelivery delivery)
        {Due(delivery,"ReleaseAt");Tick(delivery);Due(delivery,"HitAt");Tick(delivery);}
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
            if(!Application.isPlaying||Time.timeScale<=0)
            {Debug.LogWarning("Run attack delivery checks in a disposable, unpaused Play Mode match.");return;}
            for(int hero=0;hero<6;hero++)
            {
                GameObject source=Actor((HeroId)hero,0,new Vector3(0,80,0));
                GameObject victim=Actor(HeroId.Kaelon,1,new Vector3(1,80,0));
                try
                {
                    Health target=victim.GetComponent<Health>();Health self=source.GetComponent<Health>();
                    HeroAttackDelivery delivery=HeroAttackDelivery.For(source);
                    float power=HeroIdentity.AttackDamage((HeroId)hero);
                    HeroCombatRules.BasicHit(source,target,power);
                    Check(target.Current==1000,"basic dealt damage before windup");
                    Tick(delivery);Check(target.Current==1000,"early update dealt damage");
                    Due(delivery,"ReleaseAt");Tick(delivery);
                    Check(target.Current==(HeroAttackDelivery.IsRanged((HeroId)hero)?1000:1000-power),"release/contact timing mismatch");
                    Due(delivery,"HitAt");Tick(delivery);
                    Check(target.Current==1000-power,"contact changed basic damage");
                    Tick(delivery);Check(target.Current==1000-power,"basic resolved twice");

                    target.RestoreFull();target.AddShield(100,4,false);
                    HeroCombatRules.BasicHit(source,target,power);Drain(delivery);
                    Check(target.Current==1000&&Mathf.Approximately(target.Shield,100-power),"shield absorption changed");

                    target.RestoreFull();HeroCombatRules.BasicHit(source,target,power);
                    victim.transform.position=new Vector3(30,80,0);Drain(delivery);
                    Check(target.Current==1000,"out-of-range attack connected");
                    victim.transform.position=new Vector3(1,80,0);

                    HeroCombatRules.BasicHit(source,target,power);
                    var status=source.GetComponent<HeroCombatStatus>()??source.AddComponent<HeroCombatStatus>();
                    status.Stun(.5f);Drain(delivery);status.Clear();Drain(delivery);
                    Check(target.Current==1000,"stun retained a pending hit");

                    HeroCombatRules.BasicHit(source,target,power);source.SetActive(false);source.SetActive(true);Drain(delivery);
                    Check(target.Current==1000,"disable retained a pending hit");

                    HeroCombatRules.BasicHit(source,target,power);self.TakeDamage(2000);self.RestoreFull();Drain(delivery);
                    Check(target.Current==1000,"death/restore retained a pending hit");

                    HeroCombatRules.BasicHit(source,target,power);target.DamageGate=()=>false;Drain(delivery);
                    Check(target.Current==1000,"damage gate bypassed");target.DamageGate=null;

                    if(HeroAttackDelivery.IsRanged((HeroId)hero))
                    {
                        HeroCombatRules.BasicHit(source,target,power);Due(delivery,"ReleaseAt");Tick(delivery);
                        victim.transform.position=new Vector3(30,80,0);Due(delivery,"HitAt");Tick(delivery);
                        Check(target.Current==1000,"range loss during flight connected");
                        victim.transform.position=new Vector3(1,80,0);
                    }
                    // These Q abilities use new delivery; Ione Q is an unchanged area coroutine.
                    if((HeroId)hero!=HeroId.Elyra)
                    {
                        var execution=source.AddComponent<HeroAbilityExecution>();
                        Check(execution.TryCast(0,target,Vector3.forward),"Q rejected");
                        float remaining=execution.Remaining(0);
                        Check(target.Current==1000&&remaining>0,"Q timing/cooldown not committed at cast");
                        Drain(delivery);
                        float expected=HeroAbilityKit.LoadoutFor((HeroId)hero).Q.Power;
                        Check(target.Current==1000-expected,"Q damage changed");
                        Check(Mathf.Abs(execution.Remaining(0)-remaining)<.001f,"delivery rewrote cooldown");
                    }
                }
                finally
                {
                    source.SetActive(false);victim.SetActive(false);
                    UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(victim);
                }
            }
            Debug.Log("DIMENSIONS FRONT: six-hero delivery, single application, shields, range, stun, disable, death, gate and targeted-Q checks passed. Visual/audio/device tests remain separate.");
        }
    }
}
#endif
