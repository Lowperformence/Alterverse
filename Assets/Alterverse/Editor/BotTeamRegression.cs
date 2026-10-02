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
    // Real coordinator, sensor and brain components; synchronous fixtures, no simulated win rates.
    public static class BotTeamRegression
    {
        private static readonly List<GameObject> created=new();
        private static readonly List<MobaBotBrain> bots=new();
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static BotTeamCoordinator Team=>BotTeamCoordinator.ForTeam(0);
        [MenuItem("DIMENSIONS FRONT/Checks/Team difficulty regression (Play mode)")]
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
            float scale=Time.timeScale;bool saved=PlayerPrefs.HasKey("DF_BotAdaptiveTier");int previous=PlayerPrefs.GetInt("DF_BotAdaptiveTier");
            var healths=new List<Health>(Health.Active);
            var activeBrains=new List<MobaBotBrain>();
            var activeColliders=new List<Collider>();
            foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider>())
                if(collider.enabled){activeColliders.Add(collider);collider.enabled=false;}
            foreach(var brain in UnityEngine.Object.FindObjectsByType<MobaBotBrain>())
                if(brain.enabled){activeBrains.Add(brain);brain.enabled=false;}
            // Remove the running match from the shared health registry without deleting it.
            foreach(var h in healths)if(h!=null)h.enabled=false;
            try
            {
                Time.timeScale=1;
                CheckDifficultyAndOutnumbered();Cleanup();
                CheckFocusAndFormation();Cleanup();
                CheckCallParticipation();Cleanup();
                CheckRegroupAndRespawn();Cleanup();
                CheckVictoryTempo();Cleanup();
                CheckCoreDefense();Cleanup();
                CheckHiddenEnemyAndChase();Cleanup();
                Debug.Log("DIMENSIONS FRONT: team AI scenario assertions passed (not a full-match balance test).");
            }
            finally
            {
                Cleanup();
                foreach(var collider in activeColliders)if(collider!=null)collider.enabled=true;
                Physics.SyncTransforms();
                foreach(var h in healths)if(h!=null)h.enabled=true;
                foreach(var b in activeBrains)if(b!=null)b.enabled=true;
                Time.timeScale=scale;
                if(saved)PlayerPrefs.SetInt("DF_BotAdaptiveTier",previous);else PlayerPrefs.DeleteKey("DF_BotAdaptiveTier");
                PlayerPrefs.Save();
            }
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Print team AI telemetry")]
        public static void PrintTelemetry()
        {
            for(int i=0;i<2;i++)
            {
                var t=BotTeamCoordinator.ForTeam(i);
                Debug.Log($"AI team {i}: intent={t.Call}, alive={t.AliveHeroes}, ticks={t.TacticalTicks}, focus calls={t.FocusCalls}, regroup calls={t.RegroupCalls}, observed kills={t.ObservedKills}, tempo calls={t.TempoPushes}");
            }
        }
        private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Private).SetValue(owner,value);
        private static object Invoke(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,Private).Invoke(owner,args);
        private static void Assert(bool value,string message){if(!value)throw new InvalidOperationException("Team AI regression: "+message);}
        private static GameObject Unit(string name,int team,Vector3 position,HeroId? role=null)
        {
            var obj=new GameObject(name);created.Add(obj);obj.transform.position=position;
            obj.AddComponent<Health>().Configure(100);obj.AddComponent<TeamMember>().Initialize(team);
            if(role.HasValue){obj.AddComponent<HeroIdentity>().Configure(role.Value);obj.AddComponent<HeroCombatProfile>().Configure(role.Value);}
            return obj;
        }
        private static MobaBotBrain Bot(HeroId role,Vector3 position,BotDifficulty difficulty=BotDifficulty.Expert)
        {
            PlayerPrefs.SetInt("DF_BotAdaptiveTier",(int)difficulty);
            var obj=Unit("TeamCheck_"+role+"_"+bots.Count,0,position,role);
            obj.AddComponent<BotTargetSensor>();var brain=obj.AddComponent<MobaBotBrain>();brain.Initialize(0,16);bots.Add(brain);
            return brain;
        }
        private static void Perceive()
        {
            foreach(var bot in bots)
            {
                Invoke(bot.Sensor,"Scan");Set(bot.Sensor,"candidateSince",Time.time-2f);Invoke(bot.Sensor,"Scan");
            }
        }
        private static void Think(BotDifficulty difficulty=BotDifficulty.Expert)
        {
            Perceive();Set(Team,"nextTick",0f);Team.Tick(difficulty);
            foreach(var bot in bots)
            {
                if(bot.Vitals.IsDead)continue;
                Invoke(bot,"Decide");Set(bot,"callReadyAt",0f);Invoke(bot,"Decide");
            }
        }
        private static void MarkDead(Health health)
        {
            // Set the fixture's death state without emitting global kill rewards or spawning pickups.
            Set(health,"<IsDead>k__BackingField",true);Set(health,"<Current>k__BackingField",0f);
        }
        private static void Cleanup()
        {
            foreach(var obj in created)if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();bots.Clear();Invoke(Team,"Clear");
        }
        private static void CheckDifficultyAndOutnumbered()
        {
            for(int level=0;level<= (int)BotDifficulty.Nightmare;level++)
            {
                var difficulty=(BotDifficulty)level;
                var bot=Bot(HeroId.Kaelon,new Vector3(0,1,0),difficulty);
                for(int i=0;i<3;i++)Unit("Threat",1,new Vector3(3,1,i-1),HeroId.Rook);
                Think(difficulty);
                if(level>=1)Assert(bot.State==MobaBotBrain.BotState.Retreat,"Medium/Hard/Expert/Nightmare must disengage from a healthy 1v3.");
                else Assert(!bot.FollowTeamCall,"Easy must not receive coordinated focus calls.");
                Cleanup();
            }
            var easy=BotDifficultyProfile.For(BotDifficulty.Easy);var medium=BotDifficultyProfile.For(BotDifficulty.Normal);
            var hard=BotDifficultyProfile.For(BotDifficulty.Hard);var expert=BotDifficultyProfile.For(BotDifficulty.Expert);
            Assert(easy.ReactionDelay>medium.ReactionDelay&&medium.ReactionDelay>hard.ReactionDelay&&hard.ReactionDelay>expert.ReactionDelay,"Reaction tiers must remain distinct.");
            Assert(!easy.Coordinated&&medium.Coordinated&&hard.Coordinated&&!hard.FullTeamCalls&&expert.FullTeamCalls,"Medium and above use shared tactics; Expert and Nightmare follow as a full team.");
            Assert(medium.TeamCallDelay>hard.TeamCallDelay&&hard.TeamCallDelay>expert.TeamCallDelay,"Team-call reactions must remain slower on Medium than Hard/Expert.");
        }
        private static void CheckFocusAndFormation()
        {
            var tank=Bot(HeroId.Kaelon,new Vector3(-3,1,0));
            var marksman=Bot(HeroId.Nyra,new Vector3(-5,1,1));
            var support=Bot(HeroId.Mira,new Vector3(-5,1,-1));
            Health target=Unit("Wounded",1,new Vector3(1,1,0),HeroId.Elyra).GetComponent<Health>();
            Set(target,"<Current>k__BackingField",25f);Think();
            Assert(Team.Focus==target,"Three allies must call focus on the visible isolated wounded hero.");
            Perceive();foreach(var b in bots)Assert(b.Sensor.CurrentTarget==target,"All three must select the focus after perception delay.");
            Vector3 front=Team.Formation(tank,HeroId.Kaelon,target.transform.position,true);
            Vector3 back=Team.Formation(marksman,HeroId.Nyra,target.transform.position,true);
            Vector3 heal=Team.Formation(support,HeroId.Mira,target.transform.position,true);
            Assert(front.x>back.x&&front.x>heal.x,"Tank must lead and backline stay behind.");
            Assert(Vector3.Distance(back,heal)>.5f&&Vector3.Distance(front,back)>1f,"Roles must not stack on one point.");
            Set(tank.Vitals,"<Current>k__BackingField",35f);Set(tank,"lastHealth",.35f);Invoke(tank,"Decide");
            Assert(tank.State==MobaBotBrain.BotState.Fight,"35% HP with allies can finish a 25% isolated opponent.");
            int revision=Team.Revision;Invoke(Team,"Commit",BotTeamCoordinator.Intent.Roam,new Vector3(6,1,8),false);
            Assert(Team.Revision==revision,"An ordinary new plan must respect intent hold time.");
        }
        private static void CheckCallParticipation()
        {
            for(int level=2;level<= (int)BotDifficulty.Nightmare;level++)
            {
                var difficulty=(BotDifficulty)level;
                for(int i=0;i<5;i++)Bot((HeroId)i,new Vector3(-4,1,i-2),difficulty);
                Perceive();Set(Team,"nextTick",0f);Team.Tick(difficulty);
                foreach(var bot in bots)
                {
                    Invoke(bot,"Decide");
                    Assert(!bot.FollowTeamCall,"New team calls require a reaction delay.");
                }
                Think(difficulty);int following=0;
                foreach(var bot in bots)if(bot.FollowTeamCall)following++;
                Assert(following==(level==2?4:5),"Hard assigns four of five; Expert/Nightmare assign all five to an ordinary call.");
                Cleanup();
            }
        }
        private static void CheckRegroupAndRespawn()
        {
            for(int i=0;i<5;i++)Bot((HeroId)i,new Vector3(-5-i,1,i));Think();
            for(int i=0;i<3;i++)MarkDead(bots[i].Vitals);Think();
            Assert(Team.Call==BotTeamCoordinator.Intent.Disengage,"Two survivors after three deaths must regroup.");
            var returning=bots[0];returning.Vitals.RestoreFull();returning.ResumeRoamAfterRespawn();Think();
            Assert(returning.FollowTeamCall&&returning.TacticalWithdrawal,"A respawn must join the active regroup instead of feeding alone.");
        }
        private static void CheckVictoryTempo()
        {
            Bot(HeroId.Kaelon,new Vector3(-4,1,0));Bot(HeroId.Nyra,new Vector3(-5,1,1));Bot(HeroId.Mira,new Vector3(-5,1,-1));
            Health enemy=Unit("LastVisibleEnemy",1,new Vector3(0,1,0),HeroId.Elyra).GetComponent<Health>();
            Think();MarkDead(enemy);Think();
            Assert(Team.Call==BotTeamCoordinator.Intent.Roam,
                "A cleared fight with an observed kill must return to free-roam PvP.");
        }
        private static void CheckCoreDefense()
        {
            var frontObject=new GameObject("TestFront");created.Add(frontObject);var front=frontObject.AddComponent<DimensionsFront>();
            Set(front,"lumenControl",85f);
            var core=Unit("OwnCore",0,new Vector3(-27,1,0)).AddComponent<RealityCore>();core.Initialize(0);Set(core,"front",front);
            Bot(HeroId.Kaelon,new Vector3(-22,1,0));Bot(HeroId.Nyra,new Vector3(-23,1,1));Bot(HeroId.Mira,new Vector3(-24,1,-1));
            Think();
            Assert(!Team.CoreEmergency&&Team.Call!=BotTeamCoordinator.Intent.Defend,
                "A merely vulnerable core with no observed base threat must not pin the losing team in permanent defence.");

            Unit("CoreThreat",1,new Vector3(-20,1,0),HeroId.Rook);Think();
            Assert(Team.CoreEmergency&&Team.Call==BotTeamCoordinator.Intent.Defend,"Visible pressure on a vulnerable core overrides all map objectives.");
            foreach(var b in bots)Assert(b.FollowTeamCall,"All available Expert bots must answer core danger.");
        }
        private static void CheckHiddenEnemyAndChase()
        {
            var bot=Bot(HeroId.Kaelon,new Vector3(0,1,0));Bot(HeroId.Nyra,new Vector3(-1,1,1));Bot(HeroId.Mira,new Vector3(-1,1,-1));
            Health enemy=Unit("VisibleThenHidden",1,new Vector3(6,1,0),HeroId.Elyra).GetComponent<Health>();Think();
            bot.Sensor.Disengage(enemy);Invoke(bot.Sensor,"Scan");Set(bot.Sensor,"candidateSince",Time.time-2f);Invoke(bot.Sensor,"Scan");
            Assert(bot.Sensor.CurrentTarget==null,"A chase abort cannot be immediately undone by a team focus call.");
            var wall=new GameObject("SightWall");created.Add(wall);wall.transform.position=new Vector3(3,1,0);wall.AddComponent<BoxCollider>().size=new Vector3(.5f,4,20);
            Physics.SyncTransforms();Think();
            Assert(Team.Focus==null,"A target behind a wall must leave the team focus.");
            Assert(Team.ObservedKills==0,"Losing sight is not a won fight.");
            enemy.transform.position=new Vector3(9,1,8);Set(Team,"nextTick",0f);Team.Tick(BotDifficulty.Expert);
            Assert(Team.Focus==null,"Hidden live movement must not become a focus call.");
        }
    }
}
#endif
