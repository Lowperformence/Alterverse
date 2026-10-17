using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.AI
{
    // One bounded, allocation-free tactical blackboard per team. Enemy data comes ONLY
    // from sensor snapshots; own roster and public map infrastructure can be queried.
    public sealed class BotTeamCoordinator
    {
        public enum Intent { Group, Push, Defend, ContestObjective, TakeObjective, Teamfight, Disengage, Siege, Collapse, Roam }
        public struct Observation
        {
            public Health Target;
            public Vector3 Position;
            public float Hp, SeenAt;
            public HeroId Role;
            public bool Hero, Controlled;
        }
        private struct Ally
        {
            public Health Health;
            public Vector3 Position;
            public HeroId Role;
            public float Power;
        }
        private static readonly List<MobaBotBrain> members=new(10);
        private static readonly BotTeamCoordinator[] teams={new(0),new(1)};
        private readonly int team;
        private readonly Ally[] allies=new Ally[10];
        private readonly Observation[] seen=new Observation[64];
        private readonly Health[] reportedDeaths=new Health[10];
        private readonly float[] deathTimes=new float[10];
        private int allyCount,seenCount,deathCursor,previousDead;
        private float nextTick,holdUntil,regroupUntil,tempoUntil,focusUntil,focusStarted;
        private Health focus;
        private Vector3 focusPosition,centroid;
        private RealityCore ownCore;
        private HotZoneDirector hotZone;
        private float nextEventScan;
        public Intent Call { get; private set; }=Intent.Roam;
        public Vector3 Destination { get; private set; }
        public int Revision { get; private set; }
        public bool CoreEmergency { get; private set; }
        public bool HasPlan { get; private set; }
        public bool IsHotZoneCall { get; private set; }
        public float ChangedAt { get; private set; }
        public int AliveHeroes=>allyCount;
        public Health Focus=>Time.time<focusUntil?focus:null;
        public float FocusAge=>Time.time-focusStarted;
        public Vector3 FocusPosition=>focusPosition;
        private Vector3 Home=>new(team==0?-ArenaLayout.HealingX:ArenaLayout.HealingX,0,0);
        private Vector3 Forward=>team==0?Vector3.right:Vector3.left;
#if UNITY_EDITOR || DEBUG
        public int TacticalTicks { get; private set; }
        public int FocusCalls { get; private set; }
        public int RegroupCalls { get; private set; }
        public int TempoPushes { get; private set; }
        public int ObservedKills { get; private set; }
#endif
        private BotTeamCoordinator(int assignedTeam){team=assignedTeam;}
        public static BotTeamCoordinator ForTeam(int team)=>teams[Mathf.Clamp(team,0,1)];
        public static void Register(MobaBotBrain bot){if(!members.Contains(bot))members.Add(bot);}
        public static void Unregister(MobaBotBrain bot)
        {
            members.Remove(bot);
            bool remaining=false;
            for(int i=0;i<members.Count;i++)if(members[i]!=null&&members[i].Team==bot.Team){remaining=true;break;}
            if(!remaining)ForTeam(bot.Team).Clear();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){members.Clear();teams[0].Clear();teams[1].Clear();}
        private void Clear()
        {
            allyCount=seenCount=deathCursor=previousDead=Revision=0;
            ChangedAt=0;
            nextTick=holdUntil=regroupUntil=tempoUntil=focusUntil=focusStarted=nextEventScan=0;
            focus=null;ownCore=null;hotZone=null;HasPlan=CoreEmergency=IsHotZoneCall=false;
            Call=Intent.Roam;Destination=Home;centroid=Home;
            System.Array.Clear(allies,0,allies.Length);System.Array.Clear(seen,0,seen.Length);
            System.Array.Clear(reportedDeaths,0,reportedDeaths.Length);
#if UNITY_EDITOR || DEBUG
            TacticalTicks=FocusCalls=RegroupCalls=TempoPushes=ObservedKills=0;
#endif
        }
        public int SlotFor(MobaBotBrain bot)
        {
            int slot=0;
            for(int i=0;i<members.Count;i++)
            {
                MobaBotBrain other=members[i];if(other==null||other.Team!=team)continue;
                if(other==bot)return slot;slot++;
            }
            return 0;
        }
        public bool Follows(MobaBotBrain bot)
        {
            if(!bot.Difficulty.Coordinated||!HasPlan)return false;

            // Roam is a search order, not a rally point. Making coordinated bots follow one
            // shared Roam destination caused them to arrive, stand there and wait for the
            // coordinator's next sector change. During normal downtime every healthy bot
            // should keep searching independently instead of wasting match time.
            if(Call==Intent.Roam)return false;

            if(Call==Intent.Defend&&!CoreEmergency)
            {
                int nearer=0;float distance=FlatSqr(bot.transform.position-Destination);
                for(int i=0;i<members.Count;i++)
                {
                    var other=members[i];if(other==null||other==bot||other.Team!=team||!other.isActiveAndEnabled||other.Vitals==null||other.Vitals.IsDead)continue;
                    float otherDistance=FlatSqr(other.transform.position-Destination);
                    if(otherDistance<distance || Mathf.Approximately(otherDistance,distance)&&SlotFor(other)<SlotFor(bot))nearer++;
                }
                return nearer<2;
            }
            // Hard keeps one independent roamer; this assignment changes with a committed call, not each frame.
            return CoreEmergency||Call==Intent.Disengage||bot.Difficulty.FullTeamCalls||(SlotFor(bot)+Revision)%5!=4;
        }
        public void Tick(BotDifficulty difficulty)
        {
            if(Time.timeScale<=0||Time.time<nextTick)return;
            bool expert=difficulty>=BotDifficulty.Expert;
            nextTick=Time.time+(difficulty==BotDifficulty.Nightmare?.28f:expert?.35f:.60f);
#if UNITY_EDITOR || DEBUG
            TacticalTicks++;
#endif
            ReadOwnTeam();ReadObservations();
            if(allyCount==0){focus=null;HasPlan=false;return;}
            if(Time.time>=nextEventScan)
            {nextEventScan=Time.time+2f;hotZone=Object.FindAnyObjectByType<HotZoneDirector>();}
            SelectFocus(expert);
            ChoosePlan();
        }
        private void ReadOwnTeam()
        {
            allyCount=0;centroid=Vector3.zero;int dead=0;
            ownCore=null;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];if(h==null||!h.isActiveAndEnabled)continue;
                var core=h.GetComponent<RealityCore>();
                if(core!=null&&!h.IsDead&&core.Team==team)ownCore=core;
                var identity=h.GetComponent<HeroIdentity>();var member=h.GetComponent<TeamMember>();
                if(identity==null||member==null||member.Team!=team)continue;
                if(h.IsDead){dead++;continue;}
                if(allyCount>=allies.Length)continue;
                float ready=0;var execution=h.GetComponent<HeroAbilityExecution>();
                if(execution!=null){if(execution.Remaining(0)<=0)ready+=.12f;if(execution.Remaining(3)<=0)ready+=.16f;}
                allies[allyCount++]=new Ally{Health=h,Position=h.transform.position,Role=identity.Hero,
                    Power=RolePower(identity.Hero)*(.35f+.65f*h.Normalized)+ready};
                centroid+=h.transform.position;
            }
            centroid=allyCount>0?centroid/allyCount:Home;
            if(dead>=2&&dead>previousDead)regroupUntil=Time.time+5f;
            previousDead=dead;
        }
        private void ReadObservations()
        {
            seenCount=0;
            for(int m=0;m<members.Count;m++)
            {
                var bot=members[m];
                if(bot==null||bot.Team!=team||!bot.isActiveAndEnabled||bot.Vitals==null||!bot.Vitals.isActiveAndEnabled||bot.Vitals.IsDead)continue;
                var sensor=bot.Sensor;if(sensor==null)continue;
                for(int i=0;i<sensor.ObservationCount;i++)
                {
                    Observation sample=sensor.ObservationAt(i);
                    if(sample.Target==null||Time.time-sample.SeenAt>.65f)continue;
                    int existing=-1;
                    for(int j=0;j<seenCount;j++)if(seen[j].Target==sample.Target){existing=j;break;}
                    if(existing>=0){if(sample.SeenAt>seen[existing].SeenAt)seen[existing]=sample;}
                    else if(seenCount<seen.Length)seen[seenCount++]=sample;
                }
            }
        }
        public void ReportVisibleDefeat(Health target)
        {
            for(int i=0;i<reportedDeaths.Length;i++)if(reportedDeaths[i]==target&&Time.time-deathTimes[i]<2f)return;
            reportedDeaths[deathCursor]=target;deathTimes[deathCursor]=Time.time;deathCursor=(deathCursor+1)%reportedDeaths.Length;
            tempoUntil=Time.time+7f;
#if UNITY_EDITOR || DEBUG
            ObservedKills++;
#endif
        }
        private void SelectFocus(bool expert)
        {
            Observation best=default;float bestScore=float.MaxValue;bool retained=false;
            for(int i=0;i<seenCount;i++)
            {
                Observation e=seen[i];if(!e.Hero||Time.time-e.SeenAt>.40f)continue;
                int nearby=AlliesNear(e.Position,10f),reinforcements=EnemiesNear(e.Position,7f);
                if(nearby<2||reinforcements>nearby||FightRatio(e.Position)<.95f)continue;
                float score=(2f+Mathf.Sqrt(FlatSqr(e.Position-centroid)))*(.25f+e.Hp);
                // Peel takes priority when a visible enemy is on our backline.
                for(int a=0;a<allyCount;a++)
                    if(IsBackline(allies[a].Role)&&FlatSqr(allies[a].Position-e.Position)<3.3f*3.3f)score*=.55f;
                if(e.Target==focus&&Time.time<focusUntil){score*=.65f;retained=true;}
                if(score<bestScore){bestScore=score;best=e;}
            }
            if(best.Target==null){focus=null;focusUntil=0;return;}
            if(best.Target!=focus||Time.time>=focusUntil||!retained)
            {
                focus=best.Target;focusStarted=Time.time;focusUntil=Time.time+(expert?2.2f:1.5f);
#if UNITY_EDITOR || DEBUG
                FocusCalls++;
#endif
            }
            focusPosition=best.Position;
        }
        private void ChoosePlan()
        {
            CoreEmergency=false;
            if(ownCore!=null&&ownCore.IsVulnerable)
            {
                // A vulnerable core is not automatically an emergency. The old behaviour issued a
                // permanent DEFEND call as soon as lane towers fell, which could pin the losing team
                // around its healing plate even when no enemy was actually in the base.
                CoreEmergency=ThreatsNear(ownCore.transform.position,18f)>0;
                if(CoreEmergency)
                {
                    Commit(Intent.Defend,ownCore.transform.position+Forward*3f,true);
                    return;
                }
            }
            if(allyCount<=2&&previousDead>=2||Time.time<regroupUntil)
            {
                Vector3 rally=Home+Forward*7f;
                if(allyCount>=3&&AlliesNear(centroid,8f)>=3&&Time.time>=regroupUntil)
                    regroupUntil=0;
                else {Commit(Intent.Disengage,rally,true);return;}
            }
            if(Focus!=null)
            {
                Commit(EnemiesNear(focusPosition,7f)==1?Intent.Collapse:Intent.Teamfight,focusPosition,false);return;
            }
            if(EnemiesNear(centroid,11f)>0&&FightRatio(centroid)<.75f)
            {Commit(Intent.Disengage,Vector3.Lerp(centroid,Home,.5f),true);return;}
            // A remembered enemy disappearing is NOT counted as a kill. Tempo requires a visible defeat.
            bool tempo=Time.time<tempoUntil&&EnemiesNear(centroid,11f)==0;

            // Team-wide planet buffs beat the Hot Zone. Availability comes from a recent sensor
            // observation; never rotate to an unavailable planet or force a losing objective fight.
            DimensionPlanet planetObjective=null;
            Vector3 planetPosition=Vector3.zero;
            float planetDistance=float.MaxValue;
            for(int i=0;i<seenCount;i++)
            {
                var e=seen[i];if(e.Target==null||e.Target.IsDead)continue;
                var planet=e.Target.GetComponent<DimensionPlanet>();
                if(planet==null||!planet.Available)continue;
                float distance=FlatSqr(e.Position-centroid);
                if(distance>18f*18f||distance>=planetDistance||FightRatio(e.Position)<.9f)continue;
                planetObjective=planet;planetPosition=e.Position;planetDistance=distance;
            }
            if(planetObjective!=null)
            {
                Commit(EnemiesNear(planetPosition,9f)>0?Intent.ContestObjective:Intent.TakeObjective,planetPosition,IsHotZoneCall);
                return;
            }

            // No eligible planet: rally at the live position of the recurring Hot Zone.
            if(hotZone!=null&&hotZone.IsActive)
            {Commit(Intent.ContestObjective,hotZone.transform.position,false,true);return;}

            if(!tempo&&allyCount>=3&&AlliesNear(centroid,10f)<Mathf.Min(3,allyCount))
            {
                // Regroup at the most connected ally when no PvP event is active.
                Vector3 rally=allies[0].Position;int connected=-1;
                for(int i=0;i<allyCount;i++){int n=AlliesNear(allies[i].Position,10f);if(n>connected){connected=n;rally=allies[i].Position;}}
                Commit(Intent.Group,rally,false);return;
            }
            // No lane/tower fallback: when no fight or event is active, move through open
            // arena sectors to find the next PvP contact.
            Commit(Intent.Roam,FreeRoamPoint(),tempo);
        }
        private Vector3 FreeRoamPoint()
        {
            int phase=(Mathf.FloorToInt(Time.time/7f)+team*3)%6;
            float forward=team==0?1f:-1f;
            Vector3 point=phase switch
            {
                0=>new Vector3(0,0,0),
                1=>new Vector3(5f*forward,0,10f),
                2=>new Vector3(5f*forward,0,-10f),
                3=>new Vector3(-5f*forward,0,7f),
                4=>new Vector3(-5f*forward,0,-7f),
                _=>new Vector3(10f*forward,0,0)
            };
            point.y=centroid.y;
            return point;
        }

        private void Commit(Intent intent,Vector3 destination,bool urgent,bool hotZoneObjective=false)
        {
            bool same=HasPlan&&Call==intent&&IsHotZoneCall==hotZoneObjective&&FlatSqr(destination-Destination)<4f;
            if(same)return;
            if(HasPlan&&!urgent&&Time.time<holdUntil)return;
            Call=intent;Destination=destination;IsHotZoneCall=hotZoneObjective;
            holdUntil=Time.time+(intent==Intent.Disengage?2.5f:2f);
            ChangedAt=Time.time;Revision++;HasPlan=true;
#if UNITY_EDITOR || DEBUG
            if(intent==Intent.Disengage)RegroupCalls++;
            if(Time.time<tempoUntil&&(intent==Intent.Roam||intent==Intent.TakeObjective))TempoPushes++;
#endif
        }
        public float FightRatio(Vector3 point)
        {
            float own=0,enemy=0;
            for(int i=0;i<allyCount;i++)
                if(FlatSqr(allies[i].Position-point)<11f*11f)own+=allies[i].Power;
            for(int i=0;i<seenCount;i++)
                if(seen[i].Hero&&Time.time-seen[i].SeenAt<=.65f&&FlatSqr(seen[i].Position-point)<11f*11f)enemy+=RolePower(seen[i].Role)*(.35f+.65f*seen[i].Hp);
            // Small defensive structures are deliberately not part of fight-strength evaluation.
            // Bastions deal zero damage; the trench's 5 HP/s chip damage is too minor to drive teamfight scoring.
            return enemy>.01f?own/enemy:2f;
        }
        public bool UnsafeDefense(Vector3 point)
        {
            // Compatibility hook for callers: bastions/trenches are not hard tactical no-go zones.
            // Bots may fight, chase and reposition near them; solid structure collision is handled by movement.
            return false;
        }
        public int EnemiesNear(Vector3 point,float radius)
        {int n=0;for(int i=0;i<seenCount;i++)if(seen[i].Hero&&Time.time-seen[i].SeenAt<=.65f&&FlatSqr(seen[i].Position-point)<=radius*radius)n++;return n;}
        private int ThreatsNear(Vector3 point,float radius)
        {
            int n=0;
            for(int i=0;i<seenCount;i++)
            {
                if(FlatSqr(seen[i].Position-point)>radius*radius)continue;
                Health target=seen[i].Target;
                if(seen[i].Hero||target!=null&&target.GetComponent<TeamMember>()!=null)n++;
            }
            return n;
        }
        public int AlliesNear(Vector3 point,float radius)
        {int n=0;for(int i=0;i<allyCount;i++)if(FlatSqr(allies[i].Position-point)<=radius*radius)n++;return n;}
        public bool CanFinish(Health target,Vector3 point)
        {
            for(int i=0;i<seenCount;i++)if(seen[i].Target==target&&seen[i].Hero&&Time.time-seen[i].SeenAt<=.65f&&seen[i].Hp<.30f)
                return AlliesNear(point,8f)>=2&&FightRatio(point)>1.25f;
            return false;
        }
        public float TargetBias(Health target,HeroId role)
        {
            float bias=target==Focus?.035f:1f;
            for(int i=0;i<seenCount;i++)if(seen[i].Target==target&&seen[i].Hero)
            {
                var e=seen[i];
                if(role==HeroId.Veyr&&EnemiesNear(e.Position,6f)==1)bias*=.55f;
                if(role==HeroId.Kaelon||role==HeroId.Rook)
                    for(int a=0;a<allyCount;a++)if(IsBackline(allies[a].Role)&&FlatSqr(allies[a].Position-e.Position)<12f){bias*=.45f;break;}
                break;
            }
            return bias;
        }
        public Vector3 Formation(MobaBotBrain bot,HeroId role,Vector3 objective,bool combat)
        {
            Vector3 forward=objective-centroid;forward.y=0;
            if(forward.sqrMagnitude<.1f)forward=Forward;else forward.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,forward);

            // Wider role lanes and a small stable per-bot stagger keep coordinated teams readable
            // without turning them into a rigid parade formation or a single overlapping clump.
            int slot=SlotFor(bot);
            float roleSide=role switch
            {
                HeroId.Veyr=>2.75f,
                HeroId.Kaelon=>-.35f,
                HeroId.Nyra=>2.05f,
                HeroId.Elyra=>.75f,
                HeroId.Mira=>-2.35f,
                HeroId.Rook=>-1.45f,
                HeroId.Traveler=>1.35f,
                _=>0f
            };
            float slotSide=slot%5 switch{0=>-.34f,1=>.28f,2=>-.12f,3=>.42f,_=>-.24f};
            float slotDepth=slot%5 switch{0=>.18f,1=>-.24f,2=>.34f,3=>-.10f,_=>.06f};
            Vector3 point=objective+side*(roleSide+slotSide);

            if(combat)
            {
                float range=role switch
                {
                    HeroId.Kaelon=>1.50f,
                    HeroId.Rook=>1.80f,
                    HeroId.Veyr=>1.45f,
                    HeroId.Mira=>5.35f,
                    HeroId.Nyra=>5.75f,
                    HeroId.Elyra=>5.20f,
                    HeroId.Traveler=>4.75f,
                    _=>5.25f
                };
                range=Mathf.Max(.9f,range+slotDepth);

                // Assassin attacks from a flank rather than stacking directly behind the frontline.
                if(role==HeroId.Veyr)point=objective+side*1.35f*(slot%2==0?1:-1);
                point-=forward*range;

                if(IsBackline(role))
                {
                    for(int i=0;i<allyCount;i++)
                    {
                        if(allies[i].Role!=HeroId.Kaelon&&allies[i].Role!=HeroId.Rook)continue;
                        if(FlatSqr(allies[i].Position-objective)>12f*12f)continue;
                        // Never walk ahead of the nearby frontline to acquire range.
                        float ahead=Vector3.Dot(point-(allies[i].Position-forward*1.9f),forward);
                        if(ahead>0)point-=forward*ahead;
                        break;
                    }
                }
            }
            else
            {
                float depth=role==HeroId.Mira?3.35f:IsBackline(role)?2.9f:role==HeroId.Rook?.9f:role==HeroId.Kaelon?.25f:1.15f;
                point-=forward*Mathf.Max(.15f,depth+slotDepth);
            }

            point.y=bot.transform.position.y;return point;
        }
        public Vector3 SupportPosition(MobaBotBrain bot,Vector3 fallback)
        {
            float best=.60f;Vector3 result=fallback;
            for(int i=0;i<allyCount;i++)
            {
                Ally ally=allies[i];
                if(ally.Health==null||ally.Health==bot.Vitals||FlatSqr(ally.Position-bot.transform.position)>12f*12f)continue;
                float need=ally.Health.Normalized*(IsBackline(ally.Role)?.8f:1f);
                if(need>=best)continue;
                best=need;
                Vector3 side=Vector3.Cross(Vector3.up,Forward);
                float supportSide=SlotFor(bot)%2==0?.85f:-.85f;
                result=ally.Position-Forward*2.45f+side*supportSide;
                result.y=bot.transform.position.y;
            }
            return result;
        }
        public Vector3 SafeRetreat(Vector3 position)
        {
            Vector3 best=Home;float distance=FlatSqr(Home-position);

            for(int i=0;i<allyCount;i++)
            {
                var ally=allies[i];float d=FlatSqr(ally.Position-position);
                if(d<9f||d>distance||Vector3.Dot(ally.Position-position,Forward)>1f||EnemiesNear(ally.Position,7f)>=AlliesNear(ally.Position,7f))continue;
                distance=d;best=ally.Position-Forward*2f;
            }
            best.y=position.y;return best;
        }

        public static bool IsBackline(HeroId role)=>role==HeroId.Nyra||role==HeroId.Elyra||role==HeroId.Mira||role==HeroId.Traveler;
        private static float RolePower(HeroId role)
        {
            // Both teams are evaluated from the current hero catalog. Fixed role
            // weights went stale when health and attack balance changed.
            HeroCombatStats stats=HeroCombatProfile.StatsFor(role);
            float basicDps=HeroIdentity.AttackDamage(role)/Mathf.Max(.2f,stats.AttackInterval);
            float durability=HeroIdentity.BaseHealth(role);
            float runningSpeed=stats.MoveSpeed*HeroCombatProfile.GlobalMoveSpeedMultiplier;
            float power=.52f*basicDps/25f+.22f*durability/250f+
                .10f*stats.Control+.10f*stats.Sustain+.06f*runningSpeed/5.5f;
            return Mathf.Clamp(power,.65f,1.55f);
        }
        private static float FlatSqr(Vector3 value){value.y=0;return value.sqrMagnitude;}
    }
}

