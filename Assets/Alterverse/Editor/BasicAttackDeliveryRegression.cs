#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class BasicAttackDeliveryRegression
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [MenuItem("DIMENSIONS FRONT/Checks/All hero basic delivery (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || Time.timeScale <= 0f || Alterverse.Multiplayer.CoopAuthority.Active)
                throw new InvalidOperationException("Run in unpaused offline Play Mode.");
            var objects = new List<GameObject>();
            try
            {
                for (int h = 0; h < HeroIdentity.HeroCount; h++)
                {
                    HeroId hero = (HeroId)h;
                    Vector3 origin = new(15000f + h * 50f, 0f, 15000f);
                    Health actor = Spawn("BasicCheck_" + hero, 0, origin, hero);
                    Health target = Spawn("BasicCheck_Target", 1, origin + Vector3.forward, null);
                    HeroAttackDelivery delivery = actor.gameObject.AddComponent<HeroAttackDelivery>();
                    float initial = target.Current;
                    delivery.Basic(target, 20f, 3f);
                    Step(delivery);
                    Check(target.Current == initial, hero + ": no damage before windup");
                    Due(delivery, false);
                    Step(delivery);
                    if (HeroAttackDelivery.IsRanged(hero))
                    {
                        Check(target.Current == initial, hero + ": no damage before projectile arrival");
                        Due(delivery, true);
                        Step(delivery);
                    }
                    float expected = hero == HeroId.Rook ? 21f : 20f;
                    Check(Mathf.Abs(initial - target.Current - expected) < .001f, hero + ": one correctly sized hit");
                    Step(delivery);
                    Check(Mathf.Abs(initial - target.Current - expected) < .001f, hero + ": no repeated damage");

                    target.RestoreFull();
                    delivery.Basic(target, 20f, 3f);
                    target.transform.position = origin + Vector3.forward * 10f;
                    Due(delivery, false);
                    Step(delivery);
                    if (HeroAttackDelivery.IsRanged(hero)) { Due(delivery, true); Step(delivery); }
                    Check(Mathf.Abs(target.Max-target.Current-expected)<.001f, hero + ": accepted target survives retreat during windup");
                    // Draining again must never duplicate an already committed strike.
                    Due(delivery, true); Step(delivery);
                    Check(Mathf.Abs(target.Max-target.Current-expected)<.001f, hero + ": accepted strike resolves once");
                    target.RestoreFull();
                    target.transform.position = origin + Vector3.forward;
                    delivery.Basic(target, 20f, 3f);
                    actor.gameObject.AddComponent<HeroCombatStatus>().Stun(.5f);
                    Step(delivery);
                    actor.GetComponent<HeroCombatStatus>().Clear();
                    Due(delivery, true);
                    Step(delivery);
                    Check(target.Current == target.Max, hero + ": stun cancels pending damage");
                    delivery.Basic(target, 20f, 3f);
                    typeof(Health).GetProperty("IsDead").SetValue(actor, true);
                    Due(delivery, true);
                    Step(delivery);
                    Check(target.Current == target.Max, hero + ": death cancels pending damage");
                }
                Debug.Log($"All {HeroIdentity.HeroCount} hero basics passed: windup, flight, single damage, committed targets, stun/death cancellation.");
            }
            finally
            {
                foreach (GameObject obj in objects) UnityEngine.Object.DestroyImmediate(obj);
            }

            Health Spawn(string name, int team, Vector3 position, HeroId? hero)
            {
                var obj = new GameObject(name); objects.Add(obj); obj.transform.position = position;
                obj.AddComponent<TeamMember>().Initialize(team);
                if (hero.HasValue) obj.AddComponent<HeroIdentity>().Configure(hero.Value);
                Health hp = obj.AddComponent<Health>(); hp.Configure(1000f);
                return hp;
            }
        }

        // Move only scheduler clocks, allowing boundary checks without changing global Time.
        private static void Due(HeroAttackDelivery delivery, bool arrival)
        {
            Array slots = (Array)typeof(HeroAttackDelivery).GetField("pending", PrivateInstance).GetValue(delivery);
            foreach (int i in Indices(slots.Length))
            {
                object slot = slots.GetValue(i);
                Type type = slot.GetType();
                if (!(bool)type.GetField("Active").GetValue(slot)) continue;
                type.GetField("ReleaseAt").SetValue(slot, Time.time - 1f);
                if (arrival) type.GetField("SampleTime").SetValue(slot, Time.time - 1f);
                slots.SetValue(slot, i);
            }
        }
        private static IEnumerable<int> Indices(int count) { for (int i = 0; i < count; i++) yield return i; }
        private static void Step(HeroAttackDelivery delivery) => typeof(HeroAttackDelivery).GetMethod("Update", PrivateInstance).Invoke(delivery, null);
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
