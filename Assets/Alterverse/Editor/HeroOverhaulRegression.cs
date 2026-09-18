#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public sealed class HeroOverhaulRegression : MonoBehaviour
    {
        private readonly List<GameObject> units = new();
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("DIMENSIONS FRONT/Checks/Hero overhaul regression (Play mode)")]
        public static void Launch()
        {
            if (!Application.isPlaying || Time.timeScale <= 0) throw new InvalidOperationException("Run in an unpaused disposable Play-mode match.");
            new GameObject("DF_HeroRegression").AddComponent<HeroOverhaulRegression>().StartCoroutine("RunChecks");
        }
        private GameObject Hero(HeroId id, int team, Vector3 position)
        {
            GameObject go = new("Check_" + id); units.Add(go); go.transform.position = position;
            go.AddComponent<HeroIdentity>().Configure(id); var profile = go.AddComponent<HeroCombatProfile>(); profile.Configure(id);
            go.AddComponent<Health>().Configure(profile.Health); go.AddComponent<TeamMember>().Initialize(team);
            go.AddComponent<HeroAbilityKit>().Configure(id); go.AddComponent<HeroAbilityExecution>();
            return go;
        }
        private static void Assert(bool value, string message) { if (!value) throw new Exception("Hero regression: " + message); }
        private static void Unlock(HeroAbilityExecution execution) => Array.Clear((float[])typeof(HeroAbilityExecution).GetField("ready",Hidden).GetValue(execution),0,4);
        private IEnumerator RunChecks()
        {
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 p = new(1000+i*30,1,1000);
                    GameObject actor = Hero((HeroId)i,0,p), enemy = Hero(HeroId.Kaelon,1,p+Vector3.right*2);
                    HeroAbilityExecution execution = actor.GetComponent<HeroAbilityExecution>();
                    Assert(execution.Remaining(3) > 44f, "Every ultimate starts locked for 45 seconds.");
                    Assert(!execution.TryCast(3,enemy.GetComponent<Health>(),Vector3.forward), "Locked ultimate must reject input.");
                    Health hp = enemy.GetComponent<Health>();
                    float before = hp.Current;
                    var attack = actor.AddComponent<BasicAttack>(); attack.enabled = false;
                    Assert(attack.TryAttack(), "Player must target collider-free enemies.");
                    float playerDamage = before-hp.Current; hp.RestoreFull();
                    var sensor = actor.AddComponent<BotTargetSensor>(); sensor.enabled = false;
                    typeof(BotTargetSensor).GetField("currentTarget",Hidden).SetValue(sensor,hp);
                    var brain = actor.AddComponent<MobaBotBrain>(); brain.Initialize(0,16); brain.enabled = false;
                    typeof(MobaBotBrain).GetMethod("Decide",Hidden).Invoke(brain,null);
                    var combat = actor.AddComponent<MobaBotCombat>(); combat.enabled = false;
                    typeof(MobaBotCombat).GetMethod("Update",Hidden).Invoke(combat,null);
                    Assert(Mathf.Abs(playerDamage-(before-hp.Current)) < .001f, "Player and bot basic damage must match for " + (HeroId)i);
                    Assert(Mathf.Abs(playerDamage-actor.GetComponent<HeroCombatProfile>().Attack) < .001f, "No hidden ranged damage tax.");
                    hp.RestoreFull(); hp.AddShield(50,.1f); hp.TakeDamage(30);
                    Assert(hp.Current==hp.Max && Mathf.Abs(hp.Shield-20)<.01f, "Shield absorbs damage before health.");
                    hp.TakeDamage(25); Assert(Mathf.Abs(hp.Current-(hp.Max-5))<.01f, "Shield overflow reaches health exactly once.");
                    HeroCombatStatus.Apply(hp,.65f,2,.55f);
                    Assert(!HeroCombatStatus.CanAct(enemy)&&HeroCombatStatus.Speed(enemy)==0,"Stun blocks action and movement.");
                    hp.RestoreFull(); Assert(HeroCombatStatus.CanAct(enemy)&&HeroCombatStatus.Speed(enemy)==1 && hp.Shield==0,"Respawn clears status and shields.");
                }
                Vector3 center = new(2000,1,2000);
                GameObject mage = Hero(HeroId.Elyra,0,center);
                Health a = Hero(HeroId.Veyr,1,center+Vector3.right*4).GetComponent<Health>();
                Health b = Hero(HeroId.Nyra,1,center+Vector3.right*4+Vector3.forward).GetComponent<Health>();
                Health escaped = Hero(HeroId.Nyra,1,center+Vector3.right*4-Vector3.forward).GetComponent<Health>();
                Health friendly = Hero(HeroId.Rook,0,center+Vector3.right*4).GetComponent<Health>();
                var cast = mage.GetComponent<HeroAbilityExecution>();
                Assert(cast.TryCast(0,a,Vector3.zero),"Mage starts area cast.");
                Assert(a.Current==a.Max && b.Current==b.Max,"No damage before telegraph resolves.");
                escaped.transform.position += Vector3.forward*8;
                yield return new WaitForSeconds(.55f);
                Assert(a.Current<a.Max && b.Current<b.Max,"AoE hits both enemies inside the radius.");
                Assert(escaped.Current==escaped.Max && friendly.Current==friendly.Max,"Leaving warning avoids damage; allies are excluded.");
                Unlock(cast); a.RestoreFull(); cast.TryCast(0,a,Vector3.zero); mage.GetComponent<Health>().TakeDamage(1000);
                yield return new WaitForSeconds(.55f);
                Assert(a.Current==a.Max,"Death cancels pending area damage.");
                GameObject support = Hero(HeroId.Mira,0,center+Vector3.forward*30);
                Health ally = Hero(HeroId.Nyra,0,support.transform.position+Vector3.right).GetComponent<Health>(); ally.TakeDamage(70);
                Health distant = Hero(HeroId.Nyra,0,support.transform.position+Vector3.right*20).GetComponent<Health>(); distant.TakeDamage(100);
                var heal = support.GetComponent<HeroAbilityExecution>();
                Assert(heal.TryCast(1,null,Vector3.zero),"Support can heal without an enemy.");
                Assert(Mathf.Abs(ally.Current-(ally.Max-28))<.01f && distant.Current==distant.Max-100,"Healing amount and range are exact.");
                Assert(heal.TryCast(2,null,Vector3.zero)&&ally.Shield==50,"Support E grants a real shield.");
                for(int i=0;i<6;i++)
                {
                    GameObject visual=Hero((HeroId)i,i%2,new Vector3(3000+i*10,1,3000));
                    visual.AddComponent<StylizedHeroVisual>().Build();
                    yield return null; // deferred removal of the source panels after batching
                    var renderers=visual.GetComponentsInChildren<MeshRenderer>();
                    var materials=new HashSet<Material>();
                    Assert(visual.GetComponent<MeshRenderer>()==null,"Hero host must never render a primitive.");
                    Assert(renderers.Length>0 && renderers.Length<=24,"Hero renderer budget exceeded: "+(HeroId)i);
                    foreach(var renderer in renderers)
                    {
                        materials.Add(renderer.sharedMaterial);
                        Mesh mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                        Assert(mesh!=null && mesh.vertexCount>0 && mesh.triangles.Length>0,"Every visible part has real geometry.");
                    }
                    Assert(!materials.Contains(null)&&materials.Count<=6,"Hero material budget exceeded: "+(HeroId)i);
                }
                Debug.Log("DIMENSIONS FRONT: hero overhaul regression PASSED.");
            }
            finally
            {
                foreach(GameObject unit in units) if(unit!=null) Destroy(unit);
                Destroy(gameObject);
            }
        }
    }
}
#endif
