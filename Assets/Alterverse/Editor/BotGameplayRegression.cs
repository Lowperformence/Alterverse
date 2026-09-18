#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    // Runs real components in Play mode; no test-framework package dependency.
    public static class BotGameplayRegression
    {
        private static readonly List<GameObject> Created = new();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("DIMENSIONS FRONT/Checks/Bot and minion regression (Play mode)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode before running these checks.");
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                CheckBrainAndRespawn();
                CheckTargetRetention();
                CheckAbilitySlots();
                CheckMinionDuel();
                Debug.Log("DIMENSIONS FRONT: all bot/minion regression assertions passed.");
            }
            finally
            {
                Time.timeScale = previousScale;
                foreach (GameObject obj in Created) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
                Created.Clear();
            }
        }

        private static GameObject Unit(string name, int team, Vector3 position)
        {
            GameObject obj = new(name);
            Created.Add(obj);
            obj.transform.position = position;
            obj.AddComponent<Health>().Configure(100);
            obj.AddComponent<TeamMember>().Initialize(team);
            return obj;
        }
        private static object Call(object obj, string method, params object[] args)
        {
            MethodInfo info = obj.GetType().GetMethod(method, Private);
            if (info == null) throw new MissingMethodException(obj.GetType().Name, method);
            return info.Invoke(obj, args);
        }
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("DIMENSIONS FRONT regression: " + message);
        }
        private static void CheckBrainAndRespawn()
        {
            GameObject bot = Unit("Check_Brain", 0, new Vector3(-34, 1, -4));
            BotTargetSensor sensor = bot.AddComponent<BotTargetSensor>();
            MobaBotBrain brain = bot.AddComponent<MobaBotBrain>(); brain.Initialize(0, 16);
            BotLanePlan lane = bot.AddComponent<BotLanePlan>(); lane.Initialize(0, 16);
            MobaBotMover mover = bot.AddComponent<MobaBotMover>();
            Health hp = bot.GetComponent<Health>();
            hp.TakeDamage(50);
            Set(sensor, "currentTarget", Unit("Check_Enemy", 1, new Vector3(-30, 1, -4)).GetComponent<Health>());
            Call(brain, "Decide");
            Assert(brain.State == MobaBotBrain.BotState.Fight, "50% HP bot must fight instead of walking through combat.");
            hp.TakeDamage(30); Call(brain, "Decide");
            Assert(brain.State == MobaBotBrain.BotState.Retreat, "Low HP must trigger retreat.");
            hp.Heal(30); Call(brain, "Decide");
            Assert(brain.State == MobaBotBrain.BotState.Recover, "Recovery must persist until 80% HP.");
            Set(mover, "velocity", Vector3.left * 5f);
            hp.RestoreFull(); brain.ResumeLaneAfterRespawn();
            Assert(brain.State == MobaBotBrain.BotState.Lane && sensor.CurrentTarget == null, "Respawn must clear retreat and target.");
            Assert((Vector3)typeof(MobaBotMover).GetField("velocity", Private).GetValue(mover) == Vector3.zero, "Respawn must clear old velocity.");
            Assert(lane.GetDestination(brain.State).z == 16f, "Recovered bot must head to its assigned lane.");
            bot.transform.position = Vector3.zero;
            Call(mover, "MoveToward", new Vector3(.1f, 0, 0), .35f);
            Assert(bot.transform.position == Vector3.zero, "Arrival radius must stop movement.");
        }
        private static void CheckTargetRetention()
        {
            Vector3 origin = new(1000, 1, 1000);
            GameObject bot = Unit("Check_Sensor", 0, origin);
            BotTargetSensor sensor = bot.AddComponent<BotTargetSensor>();
            Health first = Unit("Check_First", 1, origin + Vector3.right * 4f).GetComponent<Health>();
            Health second = Unit("Check_Second", 1, origin + Vector3.right * 3.9f).GetComponent<Health>();
            Set(sensor, "currentTarget", first); Call(sensor, "Scan");
            Assert(sensor.CurrentTarget == first, "A tiny distance advantage must not change targets.");
            second.transform.position = origin + Vector3.right; Call(sensor, "Scan");
            Assert(sensor.CurrentTarget == second, "A substantially closer enemy must be selectable.");
            second.TakeDamage(100); Call(sensor, "Scan");
            Assert(sensor.CurrentTarget == first, "A dead target must be replaced immediately.");
        }
        private static HeroBotAbilities Abilities(HeroId hero, Vector3 position)
        {
            GameObject bot = Unit("Check_" + hero, 0, position);
            bot.AddComponent<HeroIdentity>().Configure(hero);
            bot.AddComponent<HeroAbilityKit>().Configure(hero);
            bot.AddComponent<BotTargetSensor>();
            HeroBotAbilities abilities = bot.AddComponent<HeroBotAbilities>();
            float[] ready = (float[])typeof(HeroBotAbilities).GetField("ready", Private).GetValue(abilities);
            Array.Clear(ready, 0, ready.Length);
            return abilities;
        }
        private static void CheckAbilitySlots()
        {
            Vector3 origin = new(2000, 1, 2000);
            HeroBotAbilities rook = Abilities(HeroId.Rook, origin);
            Health hp = rook.GetComponent<Health>(); hp.TakeDamage(50);
            Assert((bool)Call(rook, "TryCast", 1, null) && hp.Current > 50, "Rook W must heal without an enemy despite zero cast range.");
            Health enemy = Unit("Check_SkillEnemy", 1, origin + Vector3.right * 2f).GetComponent<Health>();
            Call(rook, "TryCast", 2, enemy);
            Assert(enemy.Current < 100, "Rook E must damage, not heal.");
            HeroBotAbilities kaelon = Abilities(HeroId.Kaelon, origin + Vector3.forward * 20);
            enemy.transform.position = kaelon.transform.position + Vector3.right * 2;
            enemy.RestoreFull(); Call(kaelon, "TryCast", 2, enemy);
            Assert(enemy.Current < 100, "Kaelon E must damage, not heal.");
            HeroBotAbilities mira = Abilities(HeroId.Mira, origin + Vector3.forward * 40);
            Health ally = Unit("Check_Ally", 0, mira.transform.position + Vector3.right * 20).GetComponent<Health>(); ally.TakeDamage(80);
            Assert(!(bool)Call(mira, "TryCast", 1, null), "Support must not heal across the map or waste cooldown.");
            ally.transform.position = mira.transform.position + Vector3.right;
            Assert((bool)Call(mira, "TryCast", 1, null) && ally.Current > 20, "Support must heal a wounded nearby ally without an enemy.");
        }
        private static void CheckMinionDuel()
        {
            Vector3 origin = new(3000, .5f, 3000);
            GameObject a = Unit("Check_MinionA", 0, origin), b = Unit("Check_MinionB", 1, origin + Vector3.right);
            LaneMinion ma = a.AddComponent<LaneMinion>(), mb = b.AddComponent<LaneMinion>();
            ma.Initialize(0, b.transform.position); mb.Initialize(1, a.transform.position);
            Set(ma, "nextAttackTime", 0f); Set(mb, "nextAttackTime", 0f);
            Call(ma, "Update"); Call(mb, "Update");
            Assert(a.GetComponent<Health>().Current < 100 && b.GetComponent<Health>().Current < 100, "Opposing minions must damage each other.");
            float before = b.GetComponent<Health>().Current;
            Time.timeScale = 0; Set(ma, "nextAttackTime", 0f); Call(ma, "Update");
            Assert(b.GetComponent<Health>().Current == before, "Paused minions must not attack.");
            Time.timeScale = 1;
        }
    }
}
#endif
