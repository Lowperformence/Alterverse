#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
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
            // Remove existing movers from the separation registry for an isolated synchronous check.
            var suspended = new List<MobaBotMover>();
            foreach (MobaBotMover mover in UnityEngine.Object.FindObjectsByType<MobaBotMover>())
                if (mover.enabled) { suspended.Add(mover); mover.enabled = false; }
            try
            {
                Time.timeScale = 1f;
                CheckBrainAndRespawn();
                CheckObstacleRecovery();
                CheckTargetRetention();
                CheckDifficulty();
                CheckAbilitySlots();
                CheckMinionDuel();
                CheckTowerAndHazardDecisions();
                Debug.Log("DIMENSIONS FRONT: all bot/minion regression assertions passed.");
            }
            finally
            {
                Time.timeScale = previousScale;
                foreach (GameObject obj in Created) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
                Created.Clear();
                foreach (MobaBotMover mover in suspended) if (mover != null) mover.enabled = true;
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
            Set(mover, "detour", Vector3.forward);
            Set(mover, "detourUntil", Time.time + 5f);
            Set(mover, "recoveryAttempts", 2);
            hp.RestoreFull(); brain.ResumeLaneAfterRespawn();
            Assert(brain.State == MobaBotBrain.BotState.Lane && sensor.CurrentTarget == null, "Respawn must clear retreat and target.");
            Assert((Vector3)typeof(MobaBotMover).GetField("velocity", Private).GetValue(mover) == Vector3.zero, "Respawn must clear old velocity.");
            Assert((Vector3)typeof(MobaBotMover).GetField("detour", Private).GetValue(mover) == Vector3.zero, "Respawn must clear old detour.");
            Assert((int)typeof(MobaBotMover).GetField("recoveryAttempts", Private).GetValue(mover) == 0, "Respawn must clear stuck recovery stage.");
            Assert(lane.GetDestination(brain.State).z == 16f, "Recovered bot must head to its assigned lane.");
            bot.transform.position = Vector3.zero;
            Call(mover, "MoveToward", new Vector3(.1f, 0, 0), .35f);
            Assert(bot.transform.position == Vector3.zero, "Arrival radius must stop movement.");
        }
        private static void CheckObstacleRecovery()
        {
            Vector3 origin = new(6000, 1, 6000);
            GameObject bot = Unit("Check_ObstacleBot", 0, origin);
            bot.AddComponent<HeroIdentity>().Configure(HeroId.Rook);
            bot.AddComponent<HeroCombatProfile>().Configure(HeroId.Rook);
            bot.AddComponent<BotTargetSensor>();
            MobaBotBrain brain = bot.AddComponent<MobaBotBrain>(); brain.Initialize(0, 16);
            bot.AddComponent<BotLanePlan>().Initialize(0, 16);
            MobaBotMover mover = bot.AddComponent<MobaBotMover>();
            HeroLocomotion locomotion = bot.GetComponent<HeroLocomotion>();

            GameObject wall = new("Check_PathWall"); Created.Add(wall);
            wall.transform.position = origin + Vector3.right * 1.1f;
            BoxCollider wallCollider = wall.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(.35f, 3f, 3.2f);
            Physics.SyncTransforms();

            Assert(locomotion.TryFindClearDirection(Vector3.right, 1.35f, 1, out Vector3 steer),
                "Blocked bot must find a local clear steering direction.");
            Assert(Vector3.Dot(steer, Vector3.right) < .98f,
                "Obstacle recovery must not keep steering straight into the blocker.");

            Call(mover, "BeginStuckRecovery", Vector3.right);
            float detourUntil = (float)typeof(MobaBotMover).GetField("detourUntil", Private).GetValue(mover);
            Vector3 detour = (Vector3)typeof(MobaBotMover).GetField("detour", Private).GetValue(mover);
            Assert(detourUntil > Time.time && detour.sqrMagnitude > .5f,
                "A sustained collision must start a committed detour instead of retrying the wall.");
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
            Assert(sensor.CurrentTarget == first, "A challenger must wait for reaction delay.");
            Set(sensor,"candidateSince",Time.time-1f);Call(sensor,"Scan");
            Assert(sensor.CurrentTarget == second, "A substantially closer enemy must be selected after reaction delay.");
            second.TakeDamage(100); Call(sensor, "Scan");
            Assert(sensor.CurrentTarget == null, "Dead targets must be cleared immediately without instant reacquisition.");
            Set(sensor,"candidateSince",Time.time-1f);Call(sensor,"Scan");
            Assert(sensor.CurrentTarget == first, "A living target must be reacquired after reaction delay.");
        }
        private static void CheckDifficulty()
        {
            const string key="DF_BotDifficulty";
            bool existed=PlayerPrefs.HasKey(key);int previous=PlayerPrefs.GetInt(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert(BotDifficultySettings.Selected==BotDifficulty.Normal,"Missing preference defaults to NORMAL.");
                PlayerPrefs.SetInt(key,999);
                Assert(BotDifficultySettings.Selected==BotDifficulty.Normal,"Invalid preference defaults to NORMAL.");
                for(int level=0;level<3;level++)
                {
                    BotDifficultySettings.Selected=(BotDifficulty)level;
                    Assert(PlayerPrefs.GetInt(key)==level&&BotDifficultySettings.Selected==(BotDifficulty)level,"Difficulty is stored and read back.");
                    for(int style=0;style<5;style++)
                    {
                        var bot=Unit("Check_Difficulty",0,new Vector3(8000+level*100,1,8000+style*20));
                        bot.AddComponent<HeroIdentity>().Configure(HeroId.Nyra);
                        var combat=bot.AddComponent<HeroCombatProfile>();combat.Configure(HeroId.Nyra);
                        float hp=combat.Health,damage=combat.Attack,speed=combat.MoveSpeed,interval=combat.AttackInterval;
                        var personality=bot.AddComponent<BotPersonality>();personality.Configure((BotPersonality.Style)style);
                        var sensor=bot.AddComponent<BotTargetSensor>();
                        var brain=bot.AddComponent<MobaBotBrain>();brain.Initialize(0,16);brain.RefreshPersonality();
                        Assert(brain.DifficultyLevel==(BotDifficulty)level,"Bot captures selected difficulty.");
                        Assert(brain.Aggression==personality.Aggression&&brain.Caution==personality.Caution,"Personality survives every difficulty.");
                        Assert(hp==combat.Health&&damage==combat.Attack&&speed==combat.MoveSpeed&&interval==combat.AttackInterval,"Difficulty must not mutate combat stats.");
                        Set(sensor,"brain",brain);
                        var enemy=Unit("Check_Reaction",1,bot.transform.position+Vector3.right*4).GetComponent<Health>();
                        Call(sensor,"Scan");Assert(sensor.CurrentTarget==null,"No frame-zero target acquisition.");
                        Set(sensor,"candidateSince",Time.time-brain.Difficulty.ReactionDelay-.01f);Call(sensor,"Scan");
                        Assert(sensor.CurrentTarget==enemy,"Every difficulty eventually acquires a visible target.");
                        var wall=new GameObject("Check_SightWall");Created.Add(wall);
                        wall.transform.position=bot.transform.position+Vector3.right*2;
                        wall.AddComponent<BoxCollider>().size=new Vector3(.5f,3,3);
                        Physics.SyncTransforms();Call(sensor,"Scan");
                        Assert(sensor.CurrentTarget==null,"A solid wall blocks perception on every difficulty.");
                        UnityEngine.Object.DestroyImmediate(wall);
                        enemy.transform.position+=Vector3.right*30;Call(sensor,"Scan");
                        Assert(sensor.CurrentTarget==null,"Memory cannot track an out-of-sight target.");
                    }
                }
            }
            finally
            {
                if(existed)PlayerPrefs.SetInt(key,previous);else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
        private static HeroBotAbilities Abilities(HeroId hero, Vector3 position)
        {
            GameObject bot = Unit("Check_" + hero, 0, position);
            bot.AddComponent<HeroIdentity>().Configure(hero);
            bot.AddComponent<HeroAbilityKit>().Configure(hero);
            bot.AddComponent<BotTargetSensor>();
            HeroBotAbilities abilities = bot.AddComponent<HeroBotAbilities>();
            float[] ready = (float[])typeof(HeroAbilityExecution).GetField("ready", Private).GetValue(abilities.GetComponent<HeroAbilityExecution>());
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
            Assert(rook.GetComponent<HeroAbilityExecution>().Remaining(2) > 0, "Rook E must start its telegraphed area attack.");
            HeroBotAbilities kaelon = Abilities(HeroId.Kaelon, origin + Vector3.forward * 20);
            enemy.transform.position = kaelon.transform.position + Vector3.right * 2;
            enemy.RestoreFull(); Call(kaelon, "TryCast", 2, enemy);
            Assert(kaelon.GetComponent<HeroAbilityExecution>().Remaining(2) > 0, "Kaelon E must start its telegraphed control field.");
            HeroBotAbilities mira = Abilities(HeroId.Mira, origin + Vector3.forward * 40);
            Health ally = Unit("Check_Ally", 0, mira.transform.position + Vector3.right * 20).GetComponent<Health>(); ally.gameObject.AddComponent<HeroIdentity>().Configure(HeroId.Nyra); ally.TakeDamage(80);
            Assert(!(bool)Call(mira, "TryCast", 1, null), "Support must not heal across the map or waste cooldown.");
            ally.transform.position = mira.transform.position + Vector3.right;
            Assert((bool)Call(mira, "TryCast", 1, null) && ally.Current > 20, "Support must heal a wounded nearby ally without an enemy.");
        }
        private static void CheckTowerAndHazardDecisions()
        {
            Vector3 center = new(4000, 1, 4000);
            GameObject towerObject = Unit("Check_Tower", 1, center);
            CapsuleCollider towerBody=towerObject.AddComponent<CapsuleCollider>();towerBody.radius=.70f;towerBody.height=2.3f;towerBody.center=Vector3.up*.45f;
            LaneTower tower = towerObject.AddComponent<LaneTower>(); tower.Initialize(1);
            GameObject hero = Unit("Check_TowerHero", 0, center + Vector3.right);
            hero.AddComponent<HeroIdentity>().Configure(HeroId.Rook);
            GameObject wave = Unit("Check_TowerMinion", 0, center + Vector3.right * 3f);
            wave.AddComponent<LaneMinion>().Initialize(0, center);
            Health selected = (Health)Call(tower, "FindEnemyTarget");
            Assert(selected == wave.GetComponent<Health>(), "Tower must prefer a minion over a nearer hero, without depending on colliders.");
            Set(tower, "currentTarget", selected);
            Assert(tower.HasMinionCoverFor(0), "Targeted living minion must provide cover.");
            selected.TakeDamage(100);
            Assert(!tower.HasMinionCoverFor(0), "Dead minion must stop providing cover immediately.");
            Assert((Health)Call(tower, "FindEnemyTarget") == hero.GetComponent<Health>(), "Tower must switch to hero when the wave is gone.");

            GameObject deathTowerObject = Unit("Check_TowerDeath", 1, center + Vector3.forward * 8f);
            LaneTower deathTower = deathTowerObject.AddComponent<LaneTower>(); deathTower.Initialize(1);
            Health deathTowerHealth = deathTowerObject.GetComponent<Health>();
            deathTowerHealth.TakeDamage(deathTowerHealth.Max);
            Assert(deathTowerHealth.IsDead && !deathTower.enabled, "Lethal tower damage must immediately enter the destruction state.");
            hero.transform.position = center + Vector3.right * (tower.AttackRange + .01f);
            Assert(Call(tower, "FindEnemyTarget") == null, "Tower must obey the exact planar range.");

            BotTargetSensor sensor = hero.AddComponent<BotTargetSensor>();
            MobaBotBrain brain = hero.AddComponent<MobaBotBrain>(); brain.Initialize(0, 16);
            hero.AddComponent<BotLanePlan>().Initialize(0, 16);
            MobaBotMover mover = hero.AddComponent<MobaBotMover>();
            Set(mover, "towers", new[] { tower }); Set(mover, "nextWorldScan", float.MaxValue);
            hero.transform.position = center + Vector3.left * 7f;
            Vector3 guarded = (Vector3)Call(mover, "GuardAgainstTowers", center + Vector3.right * 7f);
            Assert(Vector3.Distance(guarded,center)>=tower.AttackRange+.70f, "An uncovered movement step must remain outside tower danger.");
            Assert(!mover.CanRepositionTo(center + Vector3.right * 7f), "A dash must not bypass tower avoidance.");
            selected.RestoreFull(); Set(tower, "currentTarget", selected);
            Assert(mover.CanRepositionTo(center + Vector3.left * 2f), "A live minion under fire must permit a push.");

            GameObject alliedTowerObject = Unit("Check_AlliedTower", 0, center + Vector3.forward * 10f);
            CapsuleCollider alliedBody=alliedTowerObject.AddComponent<CapsuleCollider>();alliedBody.radius=.70f;alliedBody.height=2.3f;alliedBody.center=Vector3.up*.45f;
            LaneTower alliedTower=alliedTowerObject.AddComponent<LaneTower>();alliedTower.Initialize(0);
            Set(mover,"towers",new[]{alliedTower});
            hero.transform.position=alliedTowerObject.transform.position+Vector3.left*1.35f;
            Vector3 skirted=(Vector3)Call(mover,"GuardAgainstTowers",hero.transform.position+Vector3.right*.25f);
            Assert(Mathf.Abs(skirted.z-hero.transform.position.z)>.001f,"Bots must skirt allied tower bodies instead of walking straight into them.");

            GameObject planetObject = Unit("Check_Pulse", 1, center + Vector3.forward * 30f);
            Alterverse.Combat.PlanetPulseAttack pulse = planetObject.AddComponent<Alterverse.Combat.PlanetPulseAttack>();
            Set(pulse, "telegraphing", true);
            Assert(pulse.IsTelegraphing, "A live planet must expose its active warning.");
            Set(mover, "pulses", new[] { pulse }); Set(mover, "towers", Array.Empty<LaneTower>());
            hero.transform.position = planetObject.transform.position + Vector3.left * 2f;
            object[] args = { Vector3.zero };
            Assert((bool)Call(mover, "TryGetPulseEscape", args), "Bot in a warning circle must request an escape.");
            Vector3 escape = (Vector3)args[0];
            Assert(Vector3.Distance(escape, planetObject.transform.position) > pulse.BlastRadius, "Escape point must be outside blast radius.");
            Assert(!mover.CanRepositionTo(planetObject.transform.position), "Offensive dash must not move deeper into a warned explosion.");
            Assert(mover.CanRepositionTo(escape), "Escape dash must remain possible.");
            pulse.enabled = false;
            Assert(!pulse.IsTelegraphing, "Disabled warnings must not influence bots.");

            GameObject other = Unit("Check_Overlap", 0, hero.transform.position);
            other.AddComponent<BotTargetSensor>(); other.AddComponent<MobaBotBrain>().Initialize(0, 16);
            other.AddComponent<BotLanePlan>().Initialize(0, 16);
            MobaBotMover second = other.AddComponent<MobaBotMover>();
            Vector3 firstPush = (Vector3)Call(mover, "SeparationVelocity");
            Vector3 secondPush = (Vector3)Call(second, "SeparationVelocity");
            Assert(firstPush.sqrMagnitude > 0 && Vector3.Dot(firstPush, secondPush) < 0, "Exact overlaps must separate in opposite directions.");
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

