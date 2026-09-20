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
            bool hadDifficulty=PlayerPrefs.HasKey("DF_BotDifficulty");
            int previousDifficulty=PlayerPrefs.GetInt("DF_BotDifficulty");
            // Remove existing movers from the separation registry for an isolated synchronous check.
            var suspended = new List<MobaBotMover>();
            foreach (MobaBotMover mover in UnityEngine.Object.FindObjectsByType<MobaBotMover>())
                if (mover.enabled) { suspended.Add(mover); mover.enabled = false; }
            try
            {
                Time.timeScale = 1f;
                BotTeamRegression.Run();
                PlayerPrefs.SetInt("DF_BotDifficulty",(int)BotDifficulty.Normal);
                CheckBrainAndRespawn();
                CheckObstacleRecovery();
                CheckArrivalSeparationStability();
                CheckTargetRetention();
                CheckHeroCombatPriority();
                CheckRecentAttackerPriority();
                CheckBaseRecoveryDefense();
                CheckChaseDisengagement();
                CheckHumanizedDecisionMemory();
                CheckDifficulty();
                CheckAbilitySlots();
                CheckMinionDuel();
                CheckTowerAndHazardDecisions();
                Debug.Log("DIMENSIONS FRONT: all bot/minion regression assertions passed.");
            }
            finally
            {
                Time.timeScale = previousScale;
                if(hadDifficulty)PlayerPrefs.SetInt("DF_BotDifficulty",previousDifficulty);
                else PlayerPrefs.DeleteKey("DF_BotDifficulty");
                PlayerPrefs.Save();
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

            // A later progress-only stall must not keep routing around an old obstacle.
            Set(mover,"recoveryBlocker",wallCollider);
            Call(mover,"BeginStuckRecovery",Vector3.forward*2f);
            Assert(typeof(MobaBotMover).GetField("recoveryBlocker",Private).GetValue(mover)==null,
                "Progress-watch recovery must clear a stale collider from an earlier collision.");
        }

        private static void CheckArrivalSeparationStability()
        {
            Vector3 origin=new(0f,1f,0f);
            GameObject bot=Unit("Check_ArrivalStable",0,origin);
            bot.AddComponent<HeroIdentity>().Configure(HeroId.Rook);
            bot.AddComponent<HeroCombatProfile>().Configure(HeroId.Rook);
            bot.AddComponent<BotTargetSensor>();
            bot.AddComponent<MobaBotBrain>().Initialize(0,16);
            bot.AddComponent<BotLanePlan>().Initialize(0,16);
            MobaBotMover mover=bot.AddComponent<MobaBotMover>();
            Set(mover,"towers",Array.Empty<LaneTower>());
            Set(mover,"pulses",Array.Empty<Alterverse.Combat.PlanetPulseAttack>());
            Set(mover,"nextWorldScan",float.MaxValue);

            GameObject crowd=Unit("Check_ArrivalCrowd",0,origin+Vector3.forward*.75f);
            crowd.AddComponent<BotTargetSensor>();
            crowd.AddComponent<MobaBotBrain>().Initialize(0,16);
            crowd.AddComponent<BotLanePlan>().Initialize(0,16);
            crowd.AddComponent<MobaBotMover>();

            Vector3 destination=origin+Vector3.forward*.10f;
            Vector3 before=bot.transform.position;
            Call(mover,"MoveToward",destination,.35f);
            Vector3 moved=bot.transform.position-before;
            Vector3 radial=destination-before;radial.y=0;radial.Normalize();

            Assert(Mathf.Abs(Vector3.Dot(moved,radial))<.001f,
                "An arrived bot must not be pushed radially across its stop boundary by crowd separation.");
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
        private static void CheckHeroCombatPriority()
        {
            Vector3 origin=new(12500,1,12500);
            GameObject bot=Unit("Check_HeroFirst",0,origin);
            bot.AddComponent<HeroIdentity>().Configure(HeroId.Nyra);
            bot.AddComponent<HeroCombatProfile>().Configure(HeroId.Nyra);
            BotTargetSensor sensor=bot.AddComponent<BotTargetSensor>();

            GameObject minionObject=Unit("Check_CloseMinion",1,origin+Vector3.right);
            minionObject.AddComponent<LaneMinion>().Initialize(1,origin);
            GameObject towerObject=Unit("Check_CloseTower",1,origin+Vector3.right*1.4f);
            LaneTower tower=towerObject.AddComponent<LaneTower>();tower.Initialize(1);
            GameObject heroObject=Unit("Check_FartherHero",1,origin+Vector3.right*5f);
            heroObject.AddComponent<HeroIdentity>().Configure(HeroId.Elyra);
            Health hero=heroObject.GetComponent<Health>();

            Call(sensor,"Scan");Set(sensor,"candidateSince",Time.time-2f);Call(sensor,"Scan");
            Assert(sensor.CurrentTarget==hero,
                "A legal visible enemy hero must outrank a much closer tower or minion.");

            // Low-health hero execution should beat a modest distance advantage inside the hero tier.
            GameObject healthyObject=Unit("Check_HealthyHero",1,origin+Vector3.forward*3f);
            healthyObject.AddComponent<HeroIdentity>().Configure(HeroId.Elyra);
            Health healthy=healthyObject.GetComponent<Health>();
            GameObject woundedObject=Unit("Check_WoundedHero",1,origin+Vector3.forward*4f);
            woundedObject.AddComponent<HeroIdentity>().Configure(HeroId.Elyra);
            Health wounded=woundedObject.GetComponent<Health>();
            Set(wounded,"<Current>k__BackingField",20f);
            Assert((float)Call(sensor,"Score",wounded)<(float)Call(sensor,"Score",healthy),
                "A safely reachable low-health hero must gain execute priority over a modestly closer healthy hero.");

            // Once the hero fight is gone, a covered tower becomes a valid siege target ahead of farming.
            hero.TakeDamage(hero.Max);healthy.TakeDamage(healthy.Max);wounded.TakeDamage(wounded.Max);
            GameObject coverObject=Unit("Check_TowerCover",0,towerObject.transform.position+Vector3.left);
            coverObject.AddComponent<LaneMinion>().Initialize(0,towerObject.transform.position);
            Call(sensor,"Scan");Set(sensor,"candidateSince",Time.time-2f);Call(sensor,"Scan");
            Assert(sensor.CurrentTarget==towerObject.GetComponent<Health>(),
                "After hero threats are gone, a tower with allied minion cover should outrank enemy lane farming.");
        }

        private static void CheckRecentAttackerPriority()
        {
            Vector3 origin=new(13000,1,13000);
            GameObject bot=Unit("Check_ThreatMemory",0,origin);
            bot.AddComponent<HeroIdentity>().Configure(HeroId.Nyra);
            bot.AddComponent<HeroCombatProfile>().Configure(HeroId.Nyra);
            BotTargetSensor sensor=bot.AddComponent<BotTargetSensor>();

            GameObject attackerObject=Unit("Check_RecentAttacker",1,origin+Vector3.right*2.2f);
            attackerObject.AddComponent<HeroIdentity>().Configure(HeroId.Veyr);
            Health attacker=attackerObject.GetComponent<Health>();
            GameObject decoyObject=Unit("Check_CloserDecoy",1,origin+Vector3.right*1.6f);
            decoyObject.AddComponent<HeroIdentity>().Configure(HeroId.Kaelon);
            Health decoy=decoyObject.GetComponent<Health>();

            float beforeAttacker=(float)Call(sensor,"Score",attacker);
            float beforeDecoy=(float)Call(sensor,"Score",decoy);
            Assert(beforeAttacker>beforeDecoy,
                "Without threat memory the meaningfully closer visible enemy should win distance-led selection.");

            sensor.Disengage(attacker);
            bot.GetComponent<Health>().TakeDamage(5f,attackerObject);
            Assert(typeof(BotTargetSensor).GetField("disengagedTarget",Private).GetValue(sensor)==null,
                "A hero that attacks again must clear an old disengage lock for self-defence.");
            float afterAttacker=(float)Call(sensor,"Score",attacker);
            float afterDecoy=(float)Call(sensor,"Score",decoy);
            Assert(afterAttacker<afterDecoy,
                "A recent visible attacker must briefly outrank a slightly closer harmless hero.");
        }

        private static void CheckBaseRecoveryDefense()
        {
            Vector3 home=new(-27.5f,1,0);
            GameObject bot=Unit("Check_BaseRecovery",0,home);
            bot.AddComponent<HeroIdentity>().Configure(HeroId.Kaelon);
            bot.AddComponent<HeroCombatProfile>().Configure(HeroId.Kaelon);
            BotTargetSensor sensor=bot.AddComponent<BotTargetSensor>();
            MobaBotBrain brain=bot.AddComponent<MobaBotBrain>();brain.Initialize(0,16);

            Health enemy=Unit("Check_BaseInvader",1,home+Vector3.right*2.5f).GetComponent<Health>();
            Set(sensor,"currentTarget",enemy);
            Health hp=bot.GetComponent<Health>();
            hp.TakeDamage(75f);
            Call(brain,"Decide");
            Assert(brain.State==MobaBotBrain.BotState.Retreat,
                "A critically wounded base defender may still retreat to heal.");

            hp.Heal(35f);
            Set(sensor,"currentTarget",enemy);
            Call(brain,"Decide");
            Assert(brain.State==MobaBotBrain.BotState.Fight,
                "A sufficiently recovered bot must re-engage a visible invader instead of remaining passive on the healing plate.");
            Assert(brain.BaseDefenseActive,
                "Visible base pressure must hold a short local-defense window.");
        }

        private static void CheckChaseDisengagement()
        {
            Vector3 origin=new(12000,1,12000);
            GameObject bot=Unit("Check_Disengage",0,origin);
            BotTargetSensor sensor=bot.AddComponent<BotTargetSensor>();
            Health enemy=Unit("Check_Fleeing",1,origin+Vector3.right*6).GetComponent<Health>();
            Set(sensor,"currentTarget",enemy);Call(sensor,"Scan");
            Vector3 observed=sensor.ObservedTargetPosition;
            enemy.transform.position+=Vector3.forward;
            Assert(sensor.ObservedTargetPosition==observed,"Movement must use the observed position until the next perception tick.");
            sensor.Disengage(enemy);Call(sensor,"Scan");
            Set(sensor,"candidateSince",Time.time-2f);Call(sensor,"Scan");
            Assert(sensor.CurrentTarget==null,"A fleeing enemy must not immediately restart an abandoned chase.");
            enemy.transform.position=origin+Vector3.right;
            Call(sensor,"Scan");Set(sensor,"candidateSince",Time.time-2f);Call(sensor,"Scan");
            Assert(sensor.CurrentTarget==enemy,"Disengagement must still allow close-range self-defence after reaction delay.");
            float score=(float)Call(sensor,"Score",enemy);
            for(int i=0;i<8;i++)Assert((float)Call(sensor,"Score",enemy)==score,
                "Unchanged visible targets must not reroll their priority each scan.");
        }

        private static void CheckHumanizedDecisionMemory()
        {
            Vector3 origin=new(11000,1,11000);
            GameObject bot=Unit("Check_HumanMemory",0,origin);
            bot.AddComponent<HeroIdentity>().Configure(HeroId.Nyra);
            bot.AddComponent<HeroCombatProfile>().Configure(HeroId.Nyra);
            BotTargetSensor sensor=bot.AddComponent<BotTargetSensor>();
            MobaBotBrain brain=bot.AddComponent<MobaBotBrain>();brain.Initialize(0,16);
            Health enemy=Unit("Check_HumanMemoryEnemy",1,origin+Vector3.right*4).GetComponent<Health>();

            Set(sensor,"brain",brain);
            Set(sensor,"currentTarget",enemy);
            Call(sensor,"Scan");
            Assert(sensor.CurrentTarget==enemy,"Visible enemy must remain a valid humanized target.");
            Call(brain,"Decide");
            Assert(brain.State==MobaBotBrain.BotState.Fight,
                "A perceived enemy must put the bot into a committed fight state.");
            Vector3 remembered=sensor.LastKnownEnemyPosition;

            GameObject wall=new("Check_HumanMemoryWall");Created.Add(wall);
            wall.transform.position=origin+Vector3.right*2;
            wall.AddComponent<BoxCollider>().size=new Vector3(.45f,3f,3f);
            Physics.SyncTransforms();
            Call(sensor,"Scan");
            Assert(sensor.CurrentTarget==null&&sensor.HasRecentEnemyMemory,
                "Losing sight must drop live tracking but retain a short last-seen memory.");

            enemy.transform.position=origin+Vector3.right*8;
            Assert((sensor.LastKnownEnemyPosition-remembered).sqrMagnitude<.0001f,
                "Last-seen memory must stay frozen instead of following a hidden enemy.");

            Call(brain,"Decide");
            Assert(brain.State==MobaBotBrain.BotState.Fight,
                "A bot may briefly investigate a recently seen enemy instead of instantly forgetting the fight.");

            Set(sensor,"lastSeenTime",-100f);
            Set(brain,"fightCommitUntil",0f);
            Call(brain,"Decide");
            Assert(brain.State==MobaBotBrain.BotState.Lane,
                "Expired perception memory and commitment must return the bot to lane play.");
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
                for(int level=0;level<4;level++)
                {
                    BotDifficultySettings.Selected=(BotDifficulty)level;
                    Assert(PlayerPrefs.GetInt(key)==level&&BotDifficultySettings.Selected==(BotDifficulty)level,"Difficulty is stored and read back.");
                    for(int style=0;style<6;style++)
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
                        Set(sensor,"candidateSince",Time.time-brain.Difficulty.ReactionDelay*brain.DecisionRhythm-.01f);Call(sensor,"Scan");
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
            enemy.transform.position=origin+Vector3.right*8f;
            Assert(!(bool)Call(rook,"TryCast",2,enemy)&&rook.GetComponent<HeroAbilityExecution>().Remaining(2)==0,
                "An out-of-radius self-centred attack must preserve its cooldown.");
            enemy.transform.position=origin+Vector3.right*2f;
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
            Assert(!(bool)Call(mover, "TryGetPulseEscape", args), "New warnings must respect reaction delay.");
            Set(mover,"hazardSeenAt",Time.time-1f);
            Assert((bool)Call(mover, "TryGetPulseEscape", args), "Bot in a warning circle must request an escape after reaction delay.");
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



