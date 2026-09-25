using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>
    /// Spawns one support soldier from each team trench every 30 seconds.
    /// Two trenches per team = two soldiers per team per wave.
    /// </summary>
    public sealed class TrenchReinforcementDirector : MonoBehaviour
    {
        public const float WaveInterval=30f;
        public const int MaxAlivePerTeam=10;
        private float nextWaveAt;
        private int waveIndex;

        private void Start(){nextWaveAt=Time.time+WaveInterval;}

        private void Update()
        {
            if(Time.timeScale<=0f||Time.time<nextWaveAt)return;
            nextWaveAt+=WaveInterval;
            SpawnWave();
        }

        private void SpawnWave()
        {
            IReadOnlyList<DefenseTrench> trenches=DefenseTrench.Active;
            if(trenches==null||trenches.Count==0)return;

            int[] alive={TrenchSoldier.AliveForTeam(0),TrenchSoldier.AliveForTeam(1)};
            for(int i=0;i<trenches.Count;i++)
            {
                DefenseTrench trench=trenches[i];
                if(trench==null)continue;
                int team=Mathf.Clamp(trench.Team,0,1);
                if(alive[team]>=MaxAlivePerTeam)continue;

                // Reinforcements no longer spawn at the trench throat. That point can sit against
                // the rear/side collision shell and trap a fresh CharacterController before it has
                // room to steer. Deploy near the team's healing zone instead, on a verified clear
                // staging point, then let TrenchSoldier immediately join the allied hero group.
                Vector3 spawn=ResolveDeploymentPoint(team,alive[team],trench);
                string key=$"TRENCH_SOLDIER:{trench.gameObject.name}:W{waveIndex}";
                TrenchSoldier.Spawn(team,spawn,alive[team],key);
                alive[team]++;
            }
            waveIndex++;
        }

        private static Vector3 ResolveDeploymentPoint(int team,int slot,DefenseTrench sourceTrench)
        {
            // Unity 6.6: use the parameterless FindObjectsByType overload. Do not depend on
            // SpawnHealingZone exposing a Team property; choose the healing zone on the owning
            // side of the arena and then prefer the one closest to the source trench.
            SpawnHealingZone[] zones=Object.FindObjectsByType<SpawnHealingZone>();
            SpawnHealingZone bestZone=null;
            float bestDistance=float.MaxValue;

            for(int i=0;i<zones.Length;i++)
            {
                SpawnHealingZone zone=zones[i];
                if(zone==null)continue;
                float x=zone.transform.position.x;
                bool belongsToTeam=team==0?x<0f:x>0f;
                if(!belongsToTeam)continue;

                Vector3 d=zone.transform.position-sourceTrench.transform.position;d.y=0f;
                float sqr=d.sqrMagnitude;
                if(sqr>=bestDistance)continue;
                bestDistance=sqr;
                bestZone=zone;
            }

            // The fallback is the same safe base-side staging area the soldiers already use when
            // no hero/coordinator destination is available, so reinforcements never depend on a trench.
            Vector3 center=bestZone!=null
                ? bestZone.transform.position
                : new Vector3(team==0?-16.25f:16.25f,.14f,0f);

            Vector3 attack=team==0?Vector3.right:Vector3.left;
            float slotLateral=((slot%5)-2)*.82f;
            int[] sideProbe={0,1,-1,2,-2,3,-3};

            for(int pass=0;pass<2;pass++)
            {
                float forward=2.65f+pass*1.45f;
                for(int i=0;i<sideProbe.Length;i++)
                {
                    float lateral=slotLateral+sideProbe[i]*.78f;
                    Vector3 candidate=center+attack*forward+Vector3.forward*lateral;
                    candidate=GameplayCollision.ClampToRoundedArena(candidate,39f,32f,1f);
                    // Gameplay collision uses the arena floor/zone height, while LunarSurfaceField
                    // contains visual relief. Spawning at visual relief could start the tiny
                    // CharacterController partially inside the real floor collider.
                    candidate.y=center.y;
                    if(IsDeploymentClear(candidate))return candidate;
                }
            }

            Vector3 fallback=center+attack*4.6f+Vector3.forward*slotLateral;
            fallback=GameplayCollision.ClampToRoundedArena(fallback,39f,32f,1f);
            fallback.y=center.y;
            return fallback;
        }

        private static bool IsDeploymentClear(Vector3 position)
        {
            int obstacleMask=GameplayCollision.MovementMask&~(1<<GameplayCollision.Ground);
            Vector3 bottom=position+Vector3.up*.10f;
            Vector3 top=position+Vector3.up*.78f;
            return !Physics.CheckCapsule(bottom,top,.30f,obstacleMask,QueryTriggerInteraction.Ignore);
        }
    }

    /// <summary>
    /// Lightweight team infantry. They are followers, not lane minions: heroes remain the leaders.
    /// </summary>
    [RequireComponent(typeof(Health),typeof(TeamMember))]
    public sealed class TrenchSoldier : MonoBehaviour
    {
        private const float MaxHp=30f;
        private const float Damage=3f;
        private const float AttackRange=5.6f;
        private const float DetectionRange=9.5f;
        private const float AttackCooldown=1.15f;
        private const float MoveSpeed=3.15f;
        private const float ArenaHalfWidth=39f;
        private const float ArenaHalfHeight=32f;
        private const float GroupRefresh=.48f;
        private const float TargetRefresh=.24f;

        private static readonly List<TrenchSoldier> active=new(24);
        public static IReadOnlyList<TrenchSoldier> Active=>active;

        private int team,formationSlot;
        private Health health,target,leader;
        private HeroLocomotion locomotion;
        private Vector3 groupAnchor;
        private float nextGroupScan,nextTargetScan,nextAttack;
        private Transform visualRoot,leftLeg,rightLeg,muzzle;
        private Quaternion leftLegBase,rightLegBase;
        private Vector3 visualRest;
        private float moveBlend;

        private Material suitMat,darkMat,visorMat,weaponMat,energyMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()=>active.Clear();
        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable(){active.Remove(this);}

        public int Team=>team;
        public Health Vitals=>health;

        public static int AliveForTeam(int teamId)
        {
            int count=0;
            for(int i=0;i<active.Count;i++)
            {
                TrenchSoldier soldier=active[i];
                if(soldier!=null&&soldier.team==teamId&&soldier.health!=null&&!soldier.health.IsDead)count++;
            }
            return count;
        }

        public static TrenchSoldier Spawn(int teamId,Vector3 position,int slot,string networkKey)
        {
            int team=Mathf.Clamp(teamId,0,1);
            GameObject go=new(team==0?"DF_ZeroTrenchSoldier":"DF_LumenTrenchSoldier");
            go.layer=GameplayCollision.Units;
            go.transform.position=position;
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)go.transform.SetParent(world.transform,true);

            Health hp=go.AddComponent<Health>();hp.Configure(MaxHp,true);
            go.AddComponent<TeamMember>().Initialize(team);
            CharacterController controller=go.AddComponent<CharacterController>();
            controller.radius=.22f;controller.height=.78f;controller.center=new Vector3(0,.34f,0);
            controller.stepOffset=0f;controller.skinWidth=.025f;controller.minMoveDistance=0f;
            go.AddComponent<HeroLocomotion>();
            TrenchSoldier soldier=go.AddComponent<TrenchSoldier>();
            soldier.Initialize(team,slot);

            CoopWorldEntityKey key=go.AddComponent<CoopWorldEntityKey>();
            key.Configure(networkKey);
            return soldier;
        }

        private void Initialize(int assignedTeam,int slot)
        {
            team=assignedTeam;
            formationSlot=Mathf.Max(0,slot);
            health=GetComponent<Health>();
            locomotion=GetComponent<HeroLocomotion>();
            // Bind to the hero group immediately on spawn. The periodic refresh below keeps the
            // formation current afterwards, but the soldier never spends its first frames leaderless.
            TeamSupportGroup.TryGetAnchor(team,transform.position,out leader,out groupAnchor);
            nextGroupScan=Time.time+GroupRefresh;
            nextTargetScan=Time.time+Random.Range(.05f,.18f);
            nextAttack=Time.time+Random.Range(.25f,.70f);
            BuildVisual();
        }

        private void Update()
        {
            if(health==null||health.IsDead||Time.timeScale<=0f)return;

            if(!CoopAuthority.CanSimulateHostGameplay)
            {
                AnimateVisual();
                return;
            }

            if(Time.time>=nextGroupScan)
            {
                nextGroupScan=Time.time+GroupRefresh;
                TeamSupportGroup.TryGetAnchor(team,transform.position,out leader,out groupAnchor);
            }

            if(Time.time>=nextTargetScan)
            {
                nextTargetScan=Time.time+TargetRefresh;
                if(!ValidTarget(target))target=FindTarget();
                else
                {
                    Health focus=TeamSupportGroup.FocusFor(team);
                    if(focus!=null&&focus!=target&&ValidTarget(focus))target=focus;
                }
            }

            if(TryHazardEscape(out Vector3 hazardEscape))
            {
                MoveToward(hazardEscape,.20f,1.18f);
                return;
            }

            Vector3 formation=FormationDestination();
            bool disengaging=BotTeamCoordinator.ForTeam(team).HasPlan&&
                BotTeamCoordinator.ForTeam(team).Call==BotTeamCoordinator.Intent.Disengage;

            if(disengaging)
            {
                // Retreat with the group, but do not become a harmless target. If an enemy is
                // already within rifle range, return fire while continuing toward formation.
                if(target!=null)
                {
                    Vector3 retreatThreat=target.transform.position-transform.position;retreatThreat.y=0;
                    if(retreatThreat.sqrMagnitude<=AttackRange*AttackRange&&Time.time>=nextAttack)Fire(target);
                }
                MoveToward(formation,.75f,1f);
                return;
            }

            if(target!=null)
            {
                Vector3 delta=target.transform.position-transform.position;delta.y=0;
                float distSqr=delta.sqrMagnitude;
                bool targetNearGroup=leader==null||(target.transform.position-groupAnchor).sqrMagnitude<9.5f*9.5f;

                if(distSqr<=AttackRange*AttackRange)
                {
                    locomotion.FaceAction(delta,AttackCooldown*.70f);
                    if(Time.time>=nextAttack)Fire(target);
                    moveBlend=Mathf.MoveTowards(moveBlend,0f,Time.deltaTime*5f);
                    AnimateVisual();
                    return;
                }

                if(targetNearGroup&&distSqr<DetectionRange*DetectionRange)
                {
                    MoveToward(target.transform.position,AttackRange*.80f,1.06f);
                    return;
                }
            }

            MoveToward(formation,.72f,1f);
        }

        private Health FindTarget()
        {
            Health focus=TeamSupportGroup.FocusFor(team);
            if(focus!=null&&ValidTarget(focus))return focus;

            Health best=null;
            float bestScore=float.MaxValue;
            float maxSqr=DetectionRange*DetectionRange;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(candidate==null||candidate==health||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                if(!TeamSupportGroup.IsEnemyCombatUnit(candidate,team))continue;
                Vector3 delta=candidate.transform.position-transform.position;delta.y=0;
                float sqr=delta.sqrMagnitude;if(sqr>maxSqr)continue;

                float score=sqr;
                if(candidate.GetComponent<HeroIdentity>()!=null)score*=.62f;
                if(candidate.GetComponent<TrenchSoldier>()!=null)score*=.86f;
                if(score<bestScore){bestScore=score;best=candidate;}
            }
            return best;
        }

        private bool ValidTarget(Health candidate)
        {
            if(candidate==null||candidate.IsDead||!candidate.isActiveAndEnabled||
               !TeamSupportGroup.IsEnemyCombatUnit(candidate,team))return false;
            Vector3 d=candidate.transform.position-transform.position;d.y=0;
            if(d.sqrMagnitude>DetectionRange*DetectionRange*1.30f)return false;
            if(leader!=null)
            {
                Vector3 fromGroup=candidate.transform.position-groupAnchor;fromGroup.y=0;
                if(fromGroup.sqrMagnitude>11f*11f)return false;
            }
            return true;
        }

        private void Fire(Health victim)
        {
            if(victim==null||victim.IsDead)return;
            nextAttack=Time.time+AttackCooldown;
            Vector3 origin=muzzle!=null?muzzle.position:transform.position+Vector3.up*.85f+transform.forward*.55f;
            Vector3 hit=victim.transform.position+Vector3.up*.55f;
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            PrototypeVfx.ProjectileLine(origin,hit,Color.Lerp(energy,new Color(.10f,.58f,1f),.50f),.085f);
            if(Random.value<.28f)PrototypeVfx.Burst(origin,energy,.07f,2);
            victim.TakeDamage(Damage,gameObject);
        }

        private Vector3 FormationDestination()
        {
            if(leader!=null&&!leader.IsDead)
            {
                int lane=formationSlot%5;
                float lateral=(lane-2)*.52f;
                float depth=1.15f+(formationSlot/5)*.48f;
                Transform lt=leader.transform;
                Vector3 slot=groupAnchor+lt.right*lateral-lt.forward*depth;
                slot.y=transform.position.y;
                return slot;
            }

            BotTeamCoordinator coordinator=BotTeamCoordinator.ForTeam(team);
            if(coordinator.HasPlan)
            {
                Vector3 d=coordinator.Destination;d.y=transform.position.y;return d;
            }

            Vector3 home=new(team==0?-25.25f:25.25f,transform.position.y,0);
            return home+(team==0?Vector3.right:Vector3.left)*9f;
        }

        private void MoveToward(Vector3 destination,float stopDistance,float speedScale)
        {
            Vector3 delta=destination-transform.position;delta.y=0;
            if(delta.sqrMagnitude<=stopDistance*stopDistance)
            {
                moveBlend=Mathf.MoveTowards(moveBlend,0f,Time.deltaTime*5f);
                AnimateVisual();
                return;
            }

            Vector3 desired=delta.normalized+Separation()*.72f;
            desired.y=0;if(desired.sqrMagnitude>.001f)desired.Normalize();

            float step=MoveSpeed*speedScale*Time.deltaTime;
            Vector3 moved=locomotion.Move(desired*step,ArenaHalfWidth,ArenaHalfHeight);

            // A trench wall or another static structure may block the straight line. Probe an
            // alternate direction immediately instead of leaving the infantry standing still.
            if(moved.sqrMagnitude<step*step*.08f&&
               locomotion.TryFindClearDirection(desired,1.0f,(formationSlot&1)==0?1:-1,out Vector3 detour))
            {
                Vector3 retry=locomotion.Move(detour*step,ArenaHalfWidth,ArenaHalfHeight);
                if(retry.sqrMagnitude>moved.sqrMagnitude)moved=retry;
            }

            if(moved.sqrMagnitude>.00001f)locomotion.FaceMovement(moved);
            else locomotion.FaceMovement(desired);

            moveBlend=Mathf.MoveTowards(moveBlend,moved.sqrMagnitude>.00001f?1f:0f,Time.deltaTime*6f);
            AnimateVisual();
        }

        private Vector3 Separation()
        {
            Vector3 result=Vector3.zero;
            for(int i=0;i<active.Count;i++)
            {
                TrenchSoldier other=active[i];
                if(other==null||other==this||other.team!=team||other.health==null||other.health.IsDead)continue;
                Vector3 d=transform.position-other.transform.position;d.y=0;
                float sqr=d.sqrMagnitude;
                if(sqr<.01f||sqr>1.15f*1.15f)continue;
                result+=d.normalized*(1f-Mathf.Sqrt(sqr)/1.15f);
            }
            return Vector3.ClampMagnitude(result,1f);
        }

        private bool TryHazardEscape(out Vector3 escape)
        {
            escape=transform.position;
            Vector3 push=Vector3.zero;bool danger=false;

            foreach(PlanetPulseAttack pulse in PlanetPulseAttack.Active)
                if(pulse!=null&&pulse.IsTelegraphing)AccumulateHazard(pulse.ImpactPosition,pulse.BlastRadius+.65f,ref push,ref danger);
            foreach(MeteorStrike meteor in MeteorStrike.Active)
                if(meteor!=null&&meteor.IsTelegraphing)AccumulateHazard(meteor.ImpactPosition,meteor.BlastRadius+.55f,ref push,ref danger);
            foreach(NoxaraMeteorStrike meteor in NoxaraMeteorStrike.Active)
                if(meteor!=null&&meteor.IsTelegraphing)AccumulateHazard(meteor.ImpactPosition,meteor.BlastRadius+.55f,ref push,ref danger);
            foreach(SpaceBombStrike bomb in SpaceBombStrike.Active)
                if(bomb!=null&&bomb.IsTelegraphing)AccumulateHazard(bomb.ImpactPosition,bomb.BlastRadius+.55f,ref push,ref danger);

            if(!danger||push.sqrMagnitude<.001f)return false;
            push.y=0;push.Normalize();
            escape=transform.position+push*3.2f;
            escape.y=transform.position.y;
            return true;
        }

        private void AccumulateHazard(Vector3 center,float radius,ref Vector3 push,ref bool danger)
        {
            Vector3 away=transform.position-center;away.y=0;
            float distance=away.magnitude;
            if(distance>=radius)return;
            danger=true;
            if(distance<.05f)away=team==0?Vector3.left:Vector3.right;
            else away/=distance;
            push+=away*(1f+Mathf.Clamp01((radius-distance)/Mathf.Max(.1f,radius)));
        }

        private void BuildVisual()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return;

            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color suit=new(.77f,.80f,.80f,1f);
            Color suitDark=new(.27f,.30f,.31f,1f);
            Color visor=new(.045f,.075f,.105f,1f);
            Color weapon=new(.06f,.28f,.52f,1f);

            suitMat=MakeMaterial(shader,suit,.18f,.46f,Color.black);
            darkMat=MakeMaterial(shader,suitDark,.45f,.38f,Color.black);
            visorMat=MakeMaterial(shader,visor,.34f,.72f,new Color(.02f,.10f,.16f)*.35f);
            weaponMat=MakeMaterial(shader,weapon,.58f,.40f,Color.black);
            energyMat=MakeMaterial(shader,teamEnergy,.08f,.74f,teamEnergy*2.2f);

            visualRoot=new GameObject("DF_TrenchSoldierVisual").transform;
            visualRoot.SetParent(transform,false);
            visualRoot.localScale=Vector3.one*.36f;
            visualRest=visualRoot.localPosition;

            // Compact astronaut infantry silhouette based on the supplied reference.
            Part("Torso",PrimitiveType.Capsule,visualRoot,new Vector3(0,1.15f,0),new Vector3(.72f,.70f,.48f),suitMat,Quaternion.identity);
            Part("ChestArmor",PrimitiveType.Cube,visualRoot,new Vector3(0,1.26f,.28f),new Vector3(.65f,.42f,.13f),suitMat,Quaternion.Euler(-4,0,0));
            Part("ChestMark",PrimitiveType.Cube,visualRoot,new Vector3(-.18f,1.31f,.36f),new Vector3(.16f,.11f,.025f),energyMat,Quaternion.identity);
            Part("Backpack",PrimitiveType.Cube,visualRoot,new Vector3(0,1.22f,-.34f),new Vector3(.52f,.55f,.24f),darkMat,Quaternion.identity);
            Part("Pelvis",PrimitiveType.Cube,visualRoot,new Vector3(0,.73f,0),new Vector3(.58f,.28f,.40f),darkMat,Quaternion.identity);

            Part("Helmet",PrimitiveType.Sphere,visualRoot,new Vector3(0,1.86f,.02f),new Vector3(.58f,.58f,.55f),suitMat,Quaternion.identity);
            Part("HelmetCrown",PrimitiveType.Cube,visualRoot,new Vector3(0,2.06f,-.02f),new Vector3(.38f,.16f,.40f),suitMat,Quaternion.Euler(-5,0,0));
            Part("Visor",PrimitiveType.Sphere,visualRoot,new Vector3(0,1.86f,.27f),new Vector3(.46f,.36f,.16f),visorMat,Quaternion.identity);
            Part("HelmetLight",PrimitiveType.Cube,visualRoot,new Vector3(.27f,1.89f,.29f),new Vector3(.08f,.07f,.025f),energyMat,Quaternion.identity);
            Part("HelmetBrow",PrimitiveType.Cube,visualRoot,new Vector3(0,2.04f,.25f),new Vector3(.50f,.07f,.11f),darkMat,Quaternion.Euler(-5,0,0));
            Part("HelmetVisorTrace",PrimitiveType.Cube,visualRoot,new Vector3(0,1.79f,.419f),new Vector3(.32f,.026f,.016f),energyMat,Quaternion.identity);

            leftLeg=Pivot("LegL",visualRoot,new Vector3(-.20f,.68f,0),Quaternion.Euler(0,0,-2));
            rightLeg=Pivot("LegR",visualRoot,new Vector3(.20f,.68f,0),Quaternion.Euler(0,0,2));
            leftLegBase=leftLeg.localRotation;rightLegBase=rightLeg.localRotation;
            BuildLeg(leftLeg,-1f);BuildLeg(rightLeg,1f);

            // Both hands visibly grip the rifle. The weapon points along local +Z, matching
            // the soldier's facing direction instead of floating sideways across the chest.
            Transform armL=Pivot("ArmL",visualRoot,new Vector3(-.40f,1.43f,.02f),Quaternion.Euler(18,0,-12));
            Transform armR=Pivot("ArmR",visualRoot,new Vector3(.40f,1.43f,.02f),Quaternion.Euler(16,0,12));
            Part("UpperArmL",PrimitiveType.Capsule,armL,new Vector3(.04f,-.25f,.10f),new Vector3(.19f,.34f,.19f),suitMat,Quaternion.Euler(10,0,-8));
            Part("UpperArmR",PrimitiveType.Capsule,armR,new Vector3(-.04f,-.25f,.10f),new Vector3(.19f,.34f,.19f),suitMat,Quaternion.Euler(10,0,8));

            Transform leftHand=Pivot("LeftHand",visualRoot,new Vector3(-.16f,1.12f,.60f),Quaternion.Euler(-8,0,-8));
            Transform rightHand=Pivot("RightHand",visualRoot,new Vector3(.16f,1.18f,.43f),Quaternion.Euler(-6,0,6));
            Part("ForearmL",PrimitiveType.Capsule,armL,new Vector3(.16f,-.50f,.29f),new Vector3(.17f,.36f,.17f),darkMat,Quaternion.Euler(64,0,-18));
            Part("ForearmR",PrimitiveType.Capsule,armR,new Vector3(-.14f,-.48f,.24f),new Vector3(.17f,.34f,.17f),darkMat,Quaternion.Euler(60,0,16));
            Part("HandL",PrimitiveType.Cube,leftHand,Vector3.zero,new Vector3(.18f,.16f,.20f),darkMat,Quaternion.identity);
            Part("HandR",PrimitiveType.Cube,rightHand,Vector3.zero,new Vector3(.18f,.16f,.20f),darkMat,Quaternion.identity);

            // Parent the rifle to the right hand so it is literally carried by the hand hierarchy.
            Transform rifle=Pivot("Rifle",rightHand,new Vector3(-.16f,-.04f,.04f),Quaternion.Euler(-3,0,0));
            Part("RifleBody",PrimitiveType.Cube,rifle,new Vector3(0,0,.34f),new Vector3(.20f,.17f,.72f),weaponMat,Quaternion.identity);
            Part("RifleStock",PrimitiveType.Cube,rifle,new Vector3(0,.01f,-.10f),new Vector3(.24f,.20f,.28f),darkMat,Quaternion.identity);
            Part("RifleCore",PrimitiveType.Cube,rifle,new Vector3(.11f,.08f,.36f),new Vector3(.055f,.055f,.28f),energyMat,Quaternion.identity);
            Part("RifleForeGrip",PrimitiveType.Cube,rifle,new Vector3(-.16f,-.09f,.50f),new Vector3(.12f,.22f,.14f),darkMat,Quaternion.Euler(8,0,0));
            Part("RifleBarrel",PrimitiveType.Cylinder,rifle,new Vector3(0,0,.82f),new Vector3(.055f,.30f,.055f),darkMat,Quaternion.Euler(90,0,0));
            Part("RifleMuzzleBrake",PrimitiveType.Cube,rifle,new Vector3(0,0,1.13f),new Vector3(.13f,.11f,.14f),weaponMat,Quaternion.identity);
            Part("RifleMuzzleEmitter",PrimitiveType.Cube,rifle,new Vector3(0,0,1.207f),new Vector3(.065f,.06f,.012f),energyMat,Quaternion.identity);
            muzzle=Pivot("Muzzle",rifle,new Vector3(0,0,1.22f),Quaternion.identity);
        }

        private void BuildLeg(Transform leg,float side)
        {
            Part(side<0?"ThighL":"ThighR",PrimitiveType.Capsule,leg,new Vector3(0,-.25f,0),new Vector3(.22f,.38f,.23f),suitMat,Quaternion.identity);
            Part(side<0?"KneeL":"KneeR",PrimitiveType.Sphere,leg,new Vector3(0,-.51f,.06f),new Vector3(.20f,.17f,.20f),darkMat,Quaternion.identity);
            Part(side<0?"ShinL":"ShinR",PrimitiveType.Capsule,leg,new Vector3(0,-.75f,.04f),new Vector3(.20f,.36f,.21f),suitMat,Quaternion.identity);
            Part(side<0?"BootL":"BootR",PrimitiveType.Cube,leg,new Vector3(0,-1.00f,.13f),new Vector3(.28f,.18f,.42f),darkMat,Quaternion.Euler(-4,0,0));
        }

        private void AnimateVisual()
        {
            if(visualRoot==null)return;
            float t=Time.time;
            float stride=Mathf.Sin(t*7.2f)*moveBlend;
            visualRoot.localPosition=visualRest+Vector3.up*(Mathf.Abs(stride)*.030f);
            if(leftLeg!=null)leftLeg.localRotation=leftLegBase*Quaternion.Euler(stride*22f,0,0);
            if(rightLeg!=null)rightLeg.localRotation=rightLegBase*Quaternion.Euler(-stride*22f,0,0);
        }

        private static Transform Pivot(string name,Transform parent,Vector3 position,Quaternion rotation)
        {
            Transform t=new GameObject("DF_TrenchSoldier_"+name).transform;
            t.SetParent(parent,false);t.localPosition=position;t.localRotation=rotation;return t;
        }

        private static Transform Part(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material,Quaternion rotation)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            if(type==PrimitiveType.Cube)go.GetComponent<MeshFilter>().sharedMesh=ProductionMeshFactory.BeveledBox();
            go.name="DF_TrenchSoldier_"+name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation;
            Collider collider=go.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null){renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
            return go.transform;
        }

        private static Material MakeMaterial(Shader shader,Color color,float metallic,float smoothness,Color emission)
        {
            Material material=new(shader){color=color};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(emission.maxColorComponent>0f&&material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",emission);
            }
            return material;
        }

        private void OnDestroy()
        {
            active.Remove(this);
            if(suitMat!=null)Destroy(suitMat);
            if(darkMat!=null)Destroy(darkMat);
            if(visorMat!=null)Destroy(visorMat);
            if(weaponMat!=null)Destroy(weaponMat);
            if(energyMat!=null)Destroy(energyMat);
        }
    }

    /// <summary>
    /// Shared low-cost group query for support soldiers and Hot Zone rewards.
    /// It intentionally reads heroes only when choosing leadership, so support units never
    /// take command or inflate the bot coordinator's hero-strength calculations.
    /// </summary>
    public static class TeamSupportGroup
    {
        public static bool TryGetAnchor(int team,Vector3 from,out Health leader,out Vector3 anchor)
        {
            leader=null;anchor=Vector3.zero;
            int bestNearby=-1;float bestDistance=float.MaxValue;

            for(int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(!IsAlliedHero(candidate,team))continue;
                int nearby=0;
                for(int j=0;j<Health.Active.Count;j++)
                {
                    Health other=Health.Active[j];
                    if(!IsAlliedHero(other,team))continue;
                    Vector3 groupDelta=other.transform.position-candidate.transform.position;groupDelta.y=0;
                    if(groupDelta.sqrMagnitude<=9f*9f)nearby++;
                }
                Vector3 d=candidate.transform.position-from;d.y=0;
                float distance=d.sqrMagnitude;
                if(nearby>bestNearby||nearby==bestNearby&&distance<bestDistance)
                {
                    bestNearby=nearby;bestDistance=distance;leader=candidate;
                }
            }

            if(leader==null)return false;

            Vector3 sum=Vector3.zero;int count=0;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health other=Health.Active[i];
                if(!IsAlliedHero(other,team))continue;
                Vector3 d=other.transform.position-leader.transform.position;d.y=0;
                if(d.sqrMagnitude>9f*9f)continue;
                sum+=other.transform.position;count++;
            }
            anchor=count>0?sum/count:leader.transform.position;
            return true;
        }

        public static bool TryGetMonsterAnchor(int team,Vector3 from,out Health leader,out Vector3 anchor)
        {
            leader=null;anchor=Vector3.zero;

            // Hot Zone rewards behave like heavy members of the infantry group first.
            // If no living soldier is reasonably close, they immediately fall back to the
            // hero group so the reward never wanders around the map by itself.
            const float soldierJoinRadius=15f;
            float joinSqr=soldierJoinRadius*soldierJoinRadius;
            TrenchSoldier closest=null;
            float closestSqr=float.MaxValue;

            IReadOnlyList<TrenchSoldier> soldiers=TrenchSoldier.Active;
            for(int i=0;i<soldiers.Count;i++)
            {
                TrenchSoldier soldier=soldiers[i];
                if(soldier==null||soldier.Team!=team||soldier.Vitals==null||soldier.Vitals.IsDead)continue;
                Vector3 d=soldier.transform.position-from;d.y=0;
                float sqr=d.sqrMagnitude;
                if(sqr>joinSqr||sqr>=closestSqr)continue;
                closest=soldier;closestSqr=sqr;
            }

            if(closest!=null)
            {
                Vector3 sum=Vector3.zero;int count=0;
                for(int i=0;i<soldiers.Count;i++)
                {
                    TrenchSoldier soldier=soldiers[i];
                    if(soldier==null||soldier.Team!=team||soldier.Vitals==null||soldier.Vitals.IsDead)continue;
                    Vector3 d=soldier.transform.position-closest.transform.position;d.y=0;
                    if(d.sqrMagnitude>8f*8f)continue;
                    sum+=soldier.transform.position;count++;
                }

                leader=closest.Vitals;
                anchor=count>0?sum/count:closest.transform.position;
                return true;
            }

            return TryGetAnchor(team,from,out leader,out anchor);
        }

        public static Health FocusFor(int team)
        {
            BotTeamCoordinator coordinator=BotTeamCoordinator.ForTeam(team);
            Health focus=coordinator.Focus;
            if(focus==null||focus.IsDead||!IsEnemyCombatUnit(focus,team))return null;
            return focus;
        }

        public static bool IsEnemyCombatUnit(Health candidate,int team)
        {
            if(candidate==null||candidate.IsDead)return false;
            if(candidate.GetComponent<RealityCore>()!=null||candidate.GetComponent<LaneTower>()!=null)return false;

            TeamMember member=candidate.GetComponent<TeamMember>();
            if(member!=null)return member.Team!=team;

            LaneMinion minion=candidate.GetComponent<LaneMinion>();
            return minion!=null&&minion.Team!=team;
        }

        private static bool IsAlliedHero(Health candidate,int team)
        {
            if(candidate==null||candidate.IsDead||!candidate.isActiveAndEnabled||candidate.GetComponent<HeroIdentity>()==null)return false;
            TeamMember member=candidate.GetComponent<TeamMember>();
            return member!=null&&member.Team==team;
        }
    }
}
