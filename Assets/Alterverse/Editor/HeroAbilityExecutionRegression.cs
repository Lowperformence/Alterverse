#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public sealed class HeroAbilityExecutionRegression : MonoBehaviour
    {
        private readonly List<GameObject> units=new();
        private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        [MenuItem("DIMENSIONS FRONT/Checks/All hero abilities (Play mode)")]
        public static void Launch()
        {
            if(!Application.isPlaying||Time.timeScale<=0||!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
                throw new InvalidOperationException("Use a running disposable offline or host match.");
            new GameObject("DF_AbilityRegression").AddComponent<HeroAbilityExecutionRegression>().StartCoroutine("Run");
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Auren passive regeneration (Play mode)")]
        public static void LaunchAurenPassive()
        {
            if(!Application.isPlaying||Time.timeScale<=0||!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
                throw new InvalidOperationException("Use a running disposable offline or host match.");
            new GameObject("DF_AurenPassiveRegression").AddComponent<HeroAbilityExecutionRegression>()
                .StartCoroutine("RunAurenPassive");
        }

        private IEnumerator RunAurenPassive()
        {
            try
            {
                Vector3 p=new(0,80,0);
                Health auren=Hero(HeroId.Kaelon,0,p).GetComponent<Health>();
                Health sivra=Hero(HeroId.Nyra,0,p+Vector3.right*3).GetComponent<Health>();
                auren.TakeDamage(200);
                sivra.TakeDamage(200);
                float tankStart=auren.Current, otherStart=sivra.Current;
                Check(tankStart<auren.Max&&otherStart<sivra.Max,"regen fixture must be wounded");
                yield return new WaitForSeconds(2.15f);
                Check(Mathf.Abs((auren.Current-tankStart)-
                    2f*HeroAbilityKit.AurenPassiveRegenPerSecond)<.05f,
                    "Auren must restore exactly 2 HP/s while alive");
                Check(Mathf.Abs(sivra.Current-otherStart)<.01f,
                    "other heroes must not inherit Auren's passive");

                auren.Heal(auren.Max,false);
                yield return new WaitForSeconds(1.1f);
                Check(Mathf.Abs(auren.Current-auren.Max)<.01f,
                    "passive may not exceed maximum HP");
                auren.TakeDamage(2000);
                yield return new WaitForSeconds(1.1f);
                Check(auren.IsDead&&auren.Current==0f,
                    "passive may not heal or revive a dead Auren");
                Debug.Log("DF: Auren 2 HP/s passive, hero isolation, maximum HP and death guard passed.");
            }
            finally {ClearUnits();Destroy(gameObject);}
        }

        private GameObject Hero(HeroId id,int team,Vector3 position)
        {
            GameObject go=new("AbilityCheck_"+id);units.Add(go);go.transform.position=position;
            go.AddComponent<HeroIdentity>().Configure(id);
            go.AddComponent<TeamMember>().Initialize(team);
            go.AddComponent<Health>().Configure(1000);
            go.AddComponent<HeroAbilityKit>().Configure(id);
            go.AddComponent<HeroAbilityExecution>();
            return go;
        }
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("Ability regression: "+message);}
        private static void Unlock(HeroAbilityExecution execution)
        {Array.Clear((float[])typeof(HeroAbilityExecution).GetField("ready",Hidden).GetValue(execution),0,4);}
        private void ClearUnits()
        {foreach(var go in units)if(go!=null){go.SetActive(false);Destroy(go);}units.Clear();}
        private IEnumerator Run()
        {
            try
            {
                // Elevated arena coordinates avoid collision with a running match's terrain/actors.
                // Every slot gets a fresh fixture so shield, cooldown and stun cannot conceal failures.
                foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
                {
                    Vector3 p=new(0,80,0);
                    GameObject actor=Hero(hero,0,p),enemy=Hero(HeroId.Kaelon,1,p+Vector3.right*2);
                    Health self=actor.GetComponent<Health>(),target=enemy.GetComponent<Health>();
                    var execution=actor.GetComponent<HeroAbilityExecution>();Unlock(execution);
                    bool heal=hero==HeroId.Mira&&(slot==1||slot==3)||hero==HeroId.Rook&&slot==1;
                    Health ally=Hero(HeroId.Nyra,0,p+Vector3.left).GetComponent<Health>();
                    if(heal)self.TakeDamage(200);
                    if(hero==HeroId.Mira&&(slot==1||slot==2||slot==3))ally.TakeDamage(300);
                    float before=target.Current+target.Shield,selfBefore=self.Current+self.Shield,allyBefore=ally.Current+ally.Shield;
                    int sightlineHits=0;float sightlineDamage=0;
                    Action<float,float,GameObject> countSightline=(damage,absorbed,source)=>
                    {if(source==actor){sightlineHits++;sightlineDamage+=damage+absorbed;}};
                    if(hero==HeroId.Nyra&&slot==3)target.Damaged+=countSightline;
                    Vector3 start=actor.transform.position;
                    Check(execution.TryCast(slot,target,Vector3.forward),hero+" slot "+slot+" rejects valid cast");
                    Check(execution.Remaining(slot)>0,hero+" slot "+slot+" has no cooldown");
                    Check(!execution.TryCast(slot,target,Vector3.forward),hero+" slot "+slot+" repeats during cooldown");
                    yield return new WaitForSeconds(hero==HeroId.Nerok&&slot==3?3.9f:1.05f);
                    bool mobility=hero==HeroId.Veyr&&slot==2||hero==HeroId.Nyra&&slot==1||hero==HeroId.Elyra&&slot==2||hero==HeroId.Traveler&&slot==1;
                    bool support=hero==HeroId.Mira&&(slot==1||slot==2||slot==3);
                    bool defensive=(hero==HeroId.Kaelon||hero==HeroId.Rook||hero==HeroId.Nerok)&&slot==1;
                    if(mobility)Check(HeroCombatRules.Distance(start,actor.transform.position)>.1f,hero+" dash did not move");
                    else if(support)Check(ally.Current+ally.Shield>allyBefore,hero+" support did not affect ally");
                    else if(defensive)Check(self.Current+self.Shield>selfBefore,hero+" defensive skill blocked by base shield");
                    else Check(target.Current+target.Shield<before,hero+" slot "+slot+" did not damage enemy");
                    if(hero==HeroId.Nyra&&slot==3)
                    {
                        target.Damaged-=countSightline;
                        Check(sightlineHits==3,"Sightline Break must deliver exactly three hits");
                        Check(Mathf.Abs(sightlineDamage-actor.GetComponent<HeroAbilityKit>().Ultimate.Power)<.01f,
                            "Sightline Break changed total volley damage");
                    }
                    Check(ally.Current+ally.Shield>=allyBefore,hero+" harmed ally");
                    ClearUnits();yield return null;
                }
                GameObject mage=Hero(HeroId.Elyra,0,new Vector3(0,80,0));
                Health victim=Hero(HeroId.Nyra,1,new Vector3(2,83,0)).GetComponent<Health>();
                var cast=mage.GetComponent<HeroAbilityExecution>();
                Health low=Hero(HeroId.Nyra,1,new Vector3(2,80,1)).GetComponent<Health>();
                float lowBefore=low.Current+low.Shield;
                float total=victim.Current+victim.Shield;
                Check(cast.TryCast(0,victim,Vector3.zero),"height fixture rejected");
                yield return new WaitForSeconds(.5f);
                Check(victim.Current+victim.Shield<total&&low.Current+low.Shield<lowBefore,"planar field misses target on different height");
                victim.RestoreFull();Unlock(cast);total=victim.Current+victim.Shield;
                Check(cast.TryCast(3,victim,Vector3.zero),"interrupt fixture rejected");
                HeroCombatStatus.Apply(mage.GetComponent<Health>(),1,0,.1f);
                yield return new WaitForSeconds(.8f);
                Check(victim.Current+victim.Shield==total,"short stun failed to interrupt longer windup");
                Unlock(cast);Check(cast.TryCast(0,victim,Vector3.zero),"death fixture rejected");
                mage.GetComponent<Health>().TakeDamage(2000);
                yield return new WaitForSeconds(.5f);
                Check(victim.Current+victim.Shield==total,"death failed to cancel area");
                ClearUnits();yield return null;
                GameObject tank=Hero(HeroId.Kaelon,0,new Vector3(0,80,0));
                victim=Hero(HeroId.Nyra,1,new Vector3(2,80,0)).GetComponent<Health>();victim.DamageGate=()=>false;
                cast=tank.GetComponent<HeroAbilityExecution>();
                Check(cast.TryCast(0,victim,Vector3.zero),"damage-gate fixture rejected cast");
                yield return new WaitForSeconds(.5f);
                Check(HeroCombatStatus.CanAct(victim.gameObject)&&HeroCombatStatus.Speed(victim.gameObject)==1,"blocked hit still applied control");
                Unlock(cast);cast.enabled=false;
                Check(!cast.TryCast(1,null,Vector3.zero),"disabled execution accepted cast");
                Debug.Log("DF: all eight hero kits / 32 slots, cooldowns, ally filters, planar areas, base shields, stun/death interrupts and blocked-hit control passed.");
            }
            finally{ClearUnits();Destroy(gameObject);}
        }
    }
}
#endif
