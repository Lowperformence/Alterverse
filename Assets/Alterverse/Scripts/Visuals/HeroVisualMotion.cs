using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    /// <summary>Distance-driven presentation. Does not write the actor's movement or balance values.</summary>
    [DefaultExecutionOrder(930)]
    public sealed class HeroVisualMotion : MonoBehaviour
    {
        private sealed class ClothPanel
        {
            public Mesh mesh;
            public Vector3[] rest,vertices;
            public float top,bottom,phase;
            public Vector2 offset,velocity;
        }
        private readonly List<ClothPanel> clothPanels=new();
        private AuthoredHeroPresentation authored;
        private HeroIdentity identity;
        private Health health;
        private HeroLocomotion locomotion;
        private HeroProceduralRig articulatedRig;
        private Transform root,torso,head,legL,legR,bootL,bootR,armL,armR,orbitL,orbitR,wingL,wingR,cape,blade;
        private Quaternion torsoRest,headRest,bootRestL,bootRestR,armRestL,armRestR,bladeRest;
        private Vector3 basePosition,lastPosition,bootPositionL,bootPositionR;
        private float hitUntil,hitStarted;
        private float hitFromSide;
        private float phase,strideWeight,attackUntil,attackDuration,attackWeight,lean,displayedSpeed;
        private Vector3 previousWorldVelocity;
        private Quaternion displayedRotation=Quaternion.identity;
        private Vector3 displayedScale;
        private bool wasDead,moving;
        private int abilitySlot=-1,attackSerial;
        private float windupUntil,windupDuration,empowerUntil,dashUntil,attackStarted;
        private float previousHeading,smoothedTurnRate;
        private Transform muzzle,weaponR,weaponL,orb,gauntletR,gauntletL;
        private Vector3 rootScale,weaponPositionR,weaponPositionL,orbRest;
        private Quaternion weaponRestR,weaponRestL;
        private Quaternion attackArmR,attackArmL,attackWeaponR,attackWeaponL;
        private Quaternion displayedArmL=Quaternion.identity,displayedArmR=Quaternion.identity;
        private TrailRenderer veyrTrailL,veyrTrailR;
        private Renderer[] energyRenderers;
        private MaterialPropertyBlock energyBlock;
        private static readonly int EnergyBoost=Shader.PropertyToID("_EnergyBoost");
        public Vector3 ReleasePoint
        {
            get
            {
                if(authored!=null)return authored.ReleasePoint;
                if(muzzle!=null)return muzzle.position;
                if(orb!=null)return orb.position;
                HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
                if(hero==HeroId.Mira&&weaponR!=null)return weaponR.TransformPoint(new Vector3(0,.80f,.06f));
                if(hero==HeroId.Kaelon&&weaponR!=null)return weaponR.TransformPoint(new Vector3(0,.54f,.29f));
                bool left=attackSerial%2==0;
                if(hero==HeroId.Rook)
                {
                    Transform hand=left?gauntletL:gauntletR;
                    if(hand!=null)return hand.TransformPoint(new Vector3(0,-.10f,.23f));
                }
                Transform weapon=hero==HeroId.Veyr&&left&&weaponL!=null?weaponL:weaponR;
                return weapon!=null?weapon.TransformPoint(new Vector3(0,-.26f,.24f)):transform.position+Vector3.up*.25f;
            }
        }
        // Stable values can later drive an Animator blend tree without replacing movement logic.
        public float PlanarSpeed { get; private set; }
        public float GaitPhase=>phase;
        public bool ArmActionActive=>Time.time<attackUntil||Time.time<windupUntil||Time.time<hitUntil||Dashing;
        // A straight jab/cross envelope for the imported brawler rig. Specials retain
        // their existing delivery; basic attacks never sweep both fists inward.
        public bool TryGetBrawlerPunch(out float extension, out float draw, out bool right)
        {
            extension = draw = 0f; right = attackSerial % 2 != 0;
            if (identity == null || identity.Hero != HeroId.Rook || abilitySlot >= 0 || Time.time >= attackUntil) return false;
            if (Time.time < windupUntil)
            {
                draw = Mathf.SmoothStep(0f, 1f, 1f - (windupUntil - Time.time) / Mathf.Max(.001f, windupDuration));
                return true;
            }
            float t = Mathf.Clamp01((Time.time - Mathf.Max(attackStarted, windupUntil)) / Mathf.Max(.001f, attackDuration));
            extension = t < .28f ? Mathf.SmoothStep(0f, 1f, t / .28f) : 1f - Mathf.SmoothStep(0f, 1f, (t - .28f) / .72f);
            draw = (1f - Mathf.SmoothStep(0f, 1f, t / .16f)) * .55f;
            return true;
        }
        public bool Dashing=>locomotion!=null&&locomotion.OwnsMovementFrame;
        public void PulseAttack(bool ultimate)
        {
            
            // Snapshot the visible pose before restarting a combo; never snap a raised weapon to rest.
            attackArmR=displayedArmR;attackArmL=displayedArmL;
            attackWeaponR=Rest(weaponR);attackWeaponL=Rest(weaponL);
            abilitySlot=ultimate?3:-1;attackSerial++;
            GetComponent<VoidWingPresentation>()?.Pulse();
            HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
            bool melee=hero==HeroId.Veyr||hero==HeroId.Kaelon||hero==HeroId.Rook;
            attackDuration=melee?(ultimate?.46f:hero==HeroId.Veyr?.70f:hero==HeroId.Kaelon?.40f:.34f):(ultimate?.32f:.22f);
            attackUntil=Time.time+attackDuration;attackWeight=ultimate?1f:hero==HeroId.Veyr?1f:.62f;windupUntil=0;windupDuration=0;
            attackStarted=Time.time;
            if(hero==HeroId.Veyr){if(veyrTrailL!=null){veyrTrailL.emitting=false;veyrTrailL.Clear();}if(veyrTrailR!=null){veyrTrailR.emitting=false;veyrTrailR.Clear();}}
        }
        public void PulseBasic(float windup)
        {
            
            PulseAttack(false);
            windupUntil=Time.time+windup;windupDuration=Mathf.Max(0,windup);attackUntil=identity!=null&&identity.Hero==HeroId.Veyr?attackStarted+.70f:windupUntil+attackDuration;
        }
        public void PulseAbility(int slot,float windup)
        {
            
            PulseAttack(slot==3);abilitySlot=slot;
            windupDuration=Mathf.Max(0,windup);windupUntil=Time.time+windupDuration;attackUntil=windupUntil+attackDuration;
            if(identity!=null&&identity.Hero==HeroId.Rook&&slot==3)Empower(1.1f);
        }
        public void Empower(float seconds){empowerUntil=Time.time+Mathf.Max(0,seconds);}
        public void PulseDash(){dashUntil=Time.time+.17f;}
        private Transform FindPart(string name)
        {
            if(authored!=null)return authored.Control(name);
            if(root==null)return null;
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name=="DF_"+name)return t;
            return null;
        }
        private void Start()
        {
            authored=GetComponent<AuthoredHeroPresentation>();
            
            // Native Unity objects must be created in a lifecycle callback, not a field initializer.
            energyBlock=new MaterialPropertyBlock();
            identity=GetComponent<HeroIdentity>();health=GetComponent<Health>();locomotion=GetComponent<HeroLocomotion>();
            root=transform.Find("DF_VisualRoot");
            // Menu previews use the same visual rig but do not participate in combat,
            // so Health is optional. The gameplay visual root is still required for posing.
            if(root==null)
            {
                Debug.LogError("[Alterverse Movement] Hero movement failed to start on '"+name+"': DF_VisualRoot is missing.",this);
                enabled=false;return;
            }
            if(GetComponents<HeroVisualMotion>().Length!=1)
            {
                Debug.LogError("[Alterverse Movement] Multiple HeroVisualMotion controllers on '"+name+"'.",this);
                enabled=false;return;
            }
            articulatedRig=GetComponent<HeroProceduralRig>()??gameObject.AddComponent<HeroProceduralRig>();
            articulatedRig.Build(root);
            if(!articulatedRig.Ready)
            {
                Debug.LogError("[Alterverse Movement] Procedural joints are incomplete on '"+name+"'; HeroVisualMotion is disabled. Check the limb meshes and rig build errors.",this);
                enabled=false;
                return;
            }
            Debug.Log("[Alterverse Movement] HeroVisualMotion active on '"+name+"' ("+identity?.Hero+"). Guard v2: one shoulder/weapon pose owner.",this);
            SetupCloth();
            basePosition=root.localPosition;rootScale=root.localScale;displayedScale=rootScale;displayedRotation=root.localRotation;lastPosition=transform.position;
            previousHeading=transform.eulerAngles.y;
            energyRenderers=root.GetComponentsInChildren<Renderer>();
            gauntletR=FindPart("GauntletR");gauntletL=FindPart("GauntletL");
            torso=FindPart("Torso");head=FindPart("Head");torsoRest=Rest(torso);headRest=Rest(head);
            muzzle=FindPart("Muzzle");
            HeroId riggedHero=identity!=null?identity.Hero:HeroId.Veyr;
            weaponR=FindPart("WeaponR")??(riggedHero==HeroId.Veyr?FindPart("BladeR"):null);
            weaponL=FindPart("WeaponL")??(riggedHero==HeroId.Veyr?FindPart("BladeL"):null);
            if(riggedHero==HeroId.Veyr)
            {
                // The generated Veyr blades are separate meshes. Attach them to the wrist
                // pivots so guard and attack poses visibly move the swords with the hands.
                if(weaponR!=null&&articulatedRig.RightWrist!=null)weaponR.SetParent(articulatedRig.RightWrist,true);
                if(weaponL!=null&&articulatedRig.LeftWrist!=null)weaponL.SetParent(articulatedRig.LeftWrist,true);
            }
            orb=FindPart("Orb");
            if(weaponR!=null){weaponPositionR=weaponR.localPosition;weaponRestR=weaponR.localRotation;}
            if(weaponL!=null){weaponPositionL=weaponL.localPosition;weaponRestL=weaponL.localRotation;}
            if(riggedHero==HeroId.Veyr&&authored==null){veyrTrailL=CreateVeyrTrail(weaponL,false);veyrTrailR=CreateVeyrTrail(weaponR,true);}
            if(orb!=null)orbRest=orb.localPosition;
            legL=FindPart("LegL");legR=FindPart("LegR");bootL=FindPart("BootL");bootR=FindPart("BootR");
            armL=FindPart("ArmL")??FindPart("GauntletL");armR=FindPart("ArmR")??FindPart("GauntletR");
            orbitL=root.Find("DF_OrbitL");orbitR=root.Find("DF_OrbitR");wingL=root.Find("DF_WingL");wingR=root.Find("DF_WingR");cape=FindPart("Cape");blade=FindPart("BladeR");
            if(bootL!=null)bootPositionL=bootL.localPosition;if(bootR!=null)bootPositionR=bootR.localPosition;
            if(health!=null)health.Damaged+=OnDamaged;
            bootRestL=Rest(bootL);bootRestR=Rest(bootR);armRestL=Rest(armL);armRestR=Rest(armR);bladeRest=Rest(blade);
            displayedArmL=armRestL;displayedArmR=armRestR;
        }
        private void OnDamaged(float applied,float absorbed,GameObject source)
        {
            if(applied<=0f&&absorbed<=0f)return;
            hitStarted=Time.time;
            hitUntil=Time.time+.46f;
            hitFromSide=0f;
            if(source!=null)
            {
                Vector3 localSource=transform.InverseTransformPoint(source.transform.position);
                hitFromSide=Mathf.Abs(localSource.x)<.05f?0f:Mathf.Sign(localSource.x);
            }
        }
        private void OnEnable()
        {
            if(root==null||articulatedRig==null||!articulatedRig.Ready)return;
            ResetPresentationAfterRelocation();
            lastPosition=transform.position;
        }
        private void ResetPresentationAfterRelocation()
        {
            wasDead=moving=false;
            phase=strideWeight=lean=displayedSpeed=PlanarSpeed=smoothedTurnRate=0f;
            previousWorldVelocity=Vector3.zero;
            previousHeading=transform.eulerAngles.y;
            attackUntil=windupUntil=dashUntil=empowerUntil=hitUntil=hitStarted=windupDuration=0f;hitFromSide=0f;
            abilitySlot=-1;
            if(veyrTrailL!=null){veyrTrailL.emitting=false;veyrTrailL.Clear();}
            if(veyrTrailR!=null){veyrTrailR.emitting=false;veyrTrailR.Clear();}
            root.localPosition=basePosition;
            root.localRotation=displayedRotation=Quaternion.identity;
            root.localScale=displayedScale=rootScale;
            if(armL!=null)armL.localRotation=armRestL;
            if(armR!=null)armR.localRotation=armRestR;
            displayedArmL=armRestL;displayedArmR=armRestR;
            if(torso!=null)torso.localRotation=torsoRest;
            if(head!=null)head.localRotation=headRest;
            if(weaponL!=null){weaponL.localPosition=weaponPositionL;weaponL.localRotation=weaponRestL;}
            if(weaponR!=null){weaponR.localPosition=weaponPositionR;weaponR.localRotation=weaponRestR;}
            foreach(ClothPanel panel in clothPanels){panel.offset=Vector2.zero;panel.velocity=Vector2.zero;}
            articulatedRig?.ResetPose();
        }
        private void OnDestroy(){if(veyrTrailMaterial!=null)Destroy(veyrTrailMaterial);if(health!=null)health.Damaged-=OnDamaged;foreach(ClothPanel panel in clothPanels)if(panel.mesh!=null)Destroy(panel.mesh);clothPanels.Clear();}
        private static Quaternion Rest(Transform t)=>t!=null?t.localRotation:Quaternion.identity;
        private static void Swing(Transform t,Quaternion rest,float angle){if(t!=null)t.localRotation=rest*Quaternion.Euler(angle,0,0);}
        private void LateUpdate()
        {
            
            if(root==null)return;
            Vector3 delta=transform.position-lastPosition;delta.y=0;lastPosition=transform.position;
            if(Time.timeScale<=0){UpdateCloth(Time.unscaledDeltaTime,Time.unscaledTime,Vector3.zero);previousWorldVelocity=Vector3.zero;return;}
            float dt=Mathf.Max(Time.deltaTime,.0001f);
            if(health!=null&&health.IsDead){wasDead=true;PlanarSpeed=0;strideWeight=0;attackUntil=windupUntil=dashUntil=empowerUntil=0;root.localScale=rootScale;displayedScale=rootScale;displayedSpeed=0;SetEnergy(0);return;}
            // Recall/respawn is a relocation, not a sprint across the arena.
            if(wasDead||delta.magnitude>Mathf.Max(3,dt*40))
            {
                ResetPresentationAfterRelocation();
                delta=Vector3.zero;
            }
            HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
            bool heavy=hero==HeroId.Kaelon||hero==HeroId.Rook;
            float travel=delta.magnitude;
            float rawTurnRate=Mathf.Clamp(Mathf.DeltaAngle(previousHeading,transform.eulerAngles.y)/dt,-540f,540f);
            smoothedTurnRate=Mathf.Lerp(smoothedTurnRate,rawTurnRate,1-Mathf.Exp(-12f*dt));
            float turnRate=smoothedTurnRate;
            previousHeading=transform.eulerAngles.y;
            PlanarSpeed=travel/dt;
            Vector3 worldVelocity=delta/dt;
            Vector3 localVelocity=transform.InverseTransformDirection(worldVelocity);
            // Subtract in the same coordinate frame: turning alone is not acceleration.
            Vector3 localAcceleration=transform.InverseTransformDirection((worldVelocity-previousWorldVelocity)/dt);
            localAcceleration=Vector3.ClampMagnitude(localAcceleration,45f);
            previousWorldVelocity=worldVelocity;
            // Ease the visible gait in and out while keeping collision and input immediate.
            displayedSpeed=Mathf.Lerp(displayedSpeed,PlanarSpeed,1-Mathf.Exp(-(PlanarSpeed>displayedSpeed?12f:9f)*dt));
            float response=1-Mathf.Exp(-11f*dt);
            // Tiny steering corrections should not restart the walk cycle. The gameplay root
            // can still settle by a few centimetres without making the hero visually shuffle.
            if(moving){if(displayedSpeed<.10f)moving=false;}
            else if(displayedSpeed>.28f)moving=true;
            if(Dashing)moving=false;
            float desired=Dashing||!moving?0f:Mathf.InverseLerp(.10f,1.1f,displayedSpeed);
            strideWeight=Mathf.Lerp(strideWeight,desired,response);
            // One complete gait cycle per traveled stride, not one per fixed time interval.
            float strideLength=heavy?1.65f:1.45f;
            // All heroes complete 39% as many visible gait cycles over the same travel distance.
            if(!Dashing)phase=Mathf.Repeat(phase+travel/strideLength*Mathf.PI*2*.39f,Mathf.PI*2);
            float runBlend=Mathf.InverseLerp(1.8f,4.5f,displayedSpeed)*strideWeight;
            // The articulated rig owns its feet and knees. Moving the legacy boot as well
            // would apply each stride twice and detach it from the ankle joint.
            // The procedural joints own the legs and feet. No imported clip or secondary gait path.
            // Keep the upper body in a composed combat guard while the legs carry the gait.
            // Keep the shoulder swing small and time-driven so weapons remain composed.
            // Never feed a later presentation component's pose back into next frame's smoothing.
            Quaternion armBeforeL=displayedArmL,armBeforeR=displayedArmR;
            if(armL!=null)armL.localRotation=armBeforeL;
            if(armR!=null)armR.localRotation=armBeforeR;
            float forwardLean=Mathf.Clamp(localAcceleration.z*.055f+localVelocity.z*.11f,-2.5f,5f);
            lean=Mathf.Lerp(lean,forwardLean,1-Mathf.Exp(-13f*dt));
            float t=Time.time;
            float idle=Mathf.Sin(t*1.8f)*.014f*(1-strideWeight);
            float bob=(1-Mathf.Cos(phase*2))*.035f*strideWeight*(heavy?.8f:1);
            if(hero==HeroId.Elyra)idle+=Mathf.Sin(t*1.3f)*.018f;
            if(Time.time<windupUntil&&!HeroCombatStatus.CanAct(gameObject)){windupUntil=attackUntil=windupDuration=0;}
            float preparation=Time.time<windupUntil?1:0;
            float windupProgress=windupDuration>.001f?Mathf.Clamp01((Time.time-attackStarted)/windupDuration):1f;
            // Kaelon's Gravity Anchor sweeps the hammer through a full circle; Rook's Wreckstorm spins through its full area hit.
            bool fullSpin=(hero==HeroId.Kaelon||hero==HeroId.Rook)&&abilitySlot==2;
            float spinYaw=fullSpin&&preparation>0?360f*Mathf.SmoothStep(0,1,windupProgress):0f;
            // Orun Overdrive lifts the rendered hero and brings him down exactly as its warning resolves.
            root.localPosition=basePosition+Vector3.up*(idle+bob);
            float progress=attackDuration>0?Mathf.Clamp01(1-(attackUntil-Time.time)/attackDuration):1;
            bool meleeStrike=hero==HeroId.Veyr&&abilitySlot!=2||
                (hero==HeroId.Kaelon||hero==HeroId.Rook)&&abilitySlot!=1;
            float delivery=meleeStrike&&preparation>0?StrikeDelivery(windupProgress):0f;
            float kick=preparation>0?delivery*attackWeight:
                Time.time<attackUntil?(1f-Mathf.SmoothStep(0f,1f,progress))*attackWeight:0f;
            bool spellCaster=hero==HeroId.Elyra||hero==HeroId.Mira||hero==HeroId.Traveler;
            float castCharge=spellCaster&&abilitySlot>=0&&preparation>0f?Mathf.SmoothStep(0f,1f,windupProgress):0f;
            float castRelease=spellCaster&&abilitySlot>=0&&preparation<=0f?Mathf.Clamp01(kick/Mathf.Max(.01f,attackWeight)):0f;
            ApplyAttackPose(hero,kick,preparation,progress,windupProgress,attackWeight);
            // Return to a readable guard between attacks. The rifle uses its authored shoulder
            // hold; melee heroes keep both fists forward instead of swinging them with the stride.
            bool veyrBasic=hero==HeroId.Veyr&&abilitySlot==-1&&Time.time<attackUntil;
            bool attacking=veyrBasic||preparation>.001f||kick>.025f;
            // Small synchronized breath and weight shift, without alternating arm pumping.
            // Keep the sight picture especially steady on Nyra's shoulder rifle.
            float guardBreath=Mathf.Sin(t*1.5f)*.30f*(1f-strideWeight);
            float guardStep=Mathf.Sin(phase*2f)*.45f*strideWeight;
            float guardLift=(guardBreath+guardStep)*(hero==HeroId.Nyra?.35f:1f);
            if(!attacking)
            {
                // A distinct ready pose for every class. Pivots stay on the existing shoulders,
                // and the authored weapon grip still moves with the forearm hierarchy.
                Vector3 readyL=Vector3.zero,readyR=Vector3.zero;
                switch(hero)
                {
                    case HeroId.Veyr:
                        // A crossed dual-blade guard: both swords angle inward into a clear X.
                        readyL=VeyrGuardArm(false);readyR=VeyrGuardArm(true);break;
                    case HeroId.Rook:
                        readyL=new Vector3(-38f,0f,-10f);readyR=new Vector3(-38f,0f,10f);break;
                    case HeroId.Kaelon:
                        readyL=new Vector3(-20f,0f,12f);readyR=new Vector3(-28f,0f,-12f);break;
                    case HeroId.Elyra:
                        readyL=new Vector3(-25f,0f,16f);readyR=new Vector3(-34f,0f,-12f);break;
                    case HeroId.Mira:
                        readyL=new Vector3(-30f,0f,14f);readyR=new Vector3(-20f,0f,-10f);break;
                    case HeroId.Traveler:
                        readyL=new Vector3(-28f,0f,13f);readyR=new Vector3(-38f,0f,-12f);break;
                    // Nyra keeps the rifle's authored two-handed shoulder aim.
                }
                float shoulderSwing=Mathf.Sin(t*1.2f)*9f*strideWeight*(hero==HeroId.Nyra?.25f:hero==HeroId.Veyr?.45f:1f);
                readyL.x+=guardLift+shoulderSwing;readyR.x+=guardLift-shoulderSwing;
                if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(readyL);
                if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(readyR);
                // Keep Veyr's paired blades tilted toward center so they cross in front of him.
                // Their hilts stay at the hand sockets; only the wrist/blade direction changes.
                if(hero==HeroId.Veyr)
                {
                    if(weaponL!=null)weaponL.localRotation=weaponRestL*VeyrGuardBlade(false);
                    if(weaponR!=null)weaponR.localRotation=weaponRestR*VeyrGuardBlade(true);
                }
            }
            float hitAge=Time.time-hitStarted;
            float hitWeight=hitUntil>Time.time?1f-Mathf.SmoothStep(0f,.46f,hitAge):0f;
            float recoil=hitWeight*(heavy?3f:4.5f);
            float hitTorso=hitWeight*(heavy?13f:17f);
            // Blend into guard and attack poses smoothly; movement remains in the legs and torso.
            float armBlend=veyrBasic?1f:1-Mathf.Exp(-(preparation>0||kick>0?28f:12f)*dt);
            if(armL!=null)armL.localRotation=Quaternion.Slerp(armBeforeL,armL.localRotation,armBlend);
            if(armR!=null)armR.localRotation=Quaternion.Slerp(armBeforeR,armR.localRotation,armBlend);
            if(hitWeight>0f)
            {
                // A hit is absorbed chiefly by the torso. Keep the weapon grip composed.
                float leftBrace=hero==HeroId.Nyra?0f:hitFromSide<0f?.12f:.06f;
                float rightBrace=hero==HeroId.Nyra?0f:hitFromSide>0f?.12f:.06f;
                Quaternion leftFlinch=armRestL*Quaternion.Euler(-34f,-hitFromSide*7f,hitFromSide<0f?-18f:12f);
                Quaternion rightFlinch=armRestR*Quaternion.Euler(-34f,-hitFromSide*7f,hitFromSide>0f?18f:-12f);
                if(armL!=null)armL.localRotation=Quaternion.Slerp(armL.localRotation,leftFlinch,hitWeight*leftBrace);
                if(armR!=null)armR.localRotation=Quaternion.Slerp(armR.localRotation,rightFlinch,hitWeight*rightBrace);
            }
            if(armL!=null)displayedArmL=armL.localRotation;
            if(armR!=null)displayedArmR=armR.localRotation;
            float energy=Mathf.Max(kick,Mathf.Max(preparation*.65f,Time.time<empowerUntil?.85f:0));
            SetEnergy(energy);
            bool phaseDash=(Dashing||Time.time<dashUntil)&&(hero==HeroId.Veyr||hero==HeroId.Elyra);
            // Compress the render rig only; collider and movement are untouched.
            Vector3 targetScale=Vector3.Scale(rootScale,phaseDash?new Vector3(.55f,.94f,.88f):Vector3.one);
            displayedScale=Vector3.Lerp(displayedScale,targetScale,1-Mathf.Exp(-18f*dt));
            root.localScale=displayedScale;
            float idleShift=Mathf.Sin(t*.8f+phase*.12f)*(1-strideWeight);
            float sway=Mathf.Sin(phase)*strideWeight*Mathf.Lerp(1.6f,3.2f,runBlend)*(heavy?.8f:1)+idleShift*.45f;
            Quaternion targetRotation=Quaternion.Euler((Dashing?9:lean)-7*kick-recoil,spinYaw,sway);
            // Full ability spins need their continuous angle; smoothing quaternions there would skip the arc.
            displayedRotation=fullSpin&&preparation>0?targetRotation:Quaternion.Slerp(displayedRotation,targetRotation,1-Mathf.Exp(-16f*dt));
            root.localRotation=displayedRotation;
            float turnCounter=Mathf.Clamp(-turnRate*.025f,-12f,12f);
            if(torso!=null)torso.localRotation=torsoRest*Quaternion.Euler(lean*.35f,turnCounter,-sway*.45f);
            if(head!=null)head.localRotation=headRest*Quaternion.Euler(-lean*.35f,-turnCounter*.45f+Mathf.Sin(phase)*2f*strideWeight,-sway*.5f);
            if(orbitL!=null){orbitL.localPosition=new Vector3(-.58f,.28f+Mathf.Sin(t*1.4f)*.08f,.03f);orbitL.localRotation=Quaternion.Euler(38,t*35,-35);}
            if(orbitR!=null){orbitR.localPosition=new Vector3(.58f,.28f-Mathf.Sin(t*1.4f)*.08f,.03f);orbitR.localRotation=Quaternion.Euler(38,-t*35,35);}
            if(orb!=null)orb.localPosition=orbRest+Vector3.up*(Mathf.Sin(t*2)*.025f+preparation*.08f);
            if(wingL!=null)wingL.localRotation=Quaternion.Euler(0,-18,-18-Mathf.Sin(t*2.1f)*4);
            if(wingR!=null)wingR.localRotation=Quaternion.Euler(0,18,18+Mathf.Sin(t*2.1f)*4);
            if(cape!=null)cape.localRotation=Quaternion.Euler(2+Mathf.Min(PlanarSpeed,8)*.3f,0,Mathf.Sin(t*2)*.5f);
            UpdateCloth(dt,t,delta/dt);
            // Veyr's blades are held in a steady guard; do not make them bob with each step.
            // One trajectory, driven by distance actually travelled by the gameplay root.
            // Hip swing, knee flex and ankle counter-rotation form a simple articulated gait.
            if(articulatedRig!=null&&articulatedRig.Ready)
            {
                // Give each leg a grounded stance, toe-off and airborne recovery instead of
                // rotating both limbs through a uniform sine wave. Phase still follows root travel.
                float hipRange=Mathf.Lerp(22f,38f,runBlend)*strideWeight;
                float kneeRange=Mathf.Lerp(30f,52f,runBlend)*strideWeight;
                float leftCycle=Mathf.Repeat(phase/(Mathf.PI*2f),1f);
                float rightCycle=Mathf.Repeat(leftCycle+.5f,1f);
                EvaluateRunLeg(leftCycle,hipRange,kneeRange,strideWeight,Mathf.Lerp(.66f,.56f,runBlend),out float leftHipAngle,out float leftKneeAngle,out float leftAnkleAngle,authored!=null);
                EvaluateRunLeg(rightCycle,hipRange,kneeRange,strideWeight,Mathf.Lerp(.66f,.56f,runBlend),out float rightHipAngle,out float rightKneeAngle,out float rightAnkleAngle,authored!=null);
                float brace=Mathf.Max(hitWeight*.72f,Mathf.Max(castCharge,castRelease*.38f));
                leftKneeAngle+=brace*(castCharge>0f||castRelease>0f?9f:11f);
                rightKneeAngle+=brace*(castCharge>0f||castRelease>0f?9f:11f);
                Quaternion leftHip=Quaternion.Euler(leftHipAngle,0f,Mathf.Sin(phase)*2.5f*strideWeight);
                Quaternion rightHip=Quaternion.Euler(rightHipAngle,0f,Mathf.Sin(phase+Mathf.PI)*2.5f*strideWeight);
                Quaternion leftKnee=Quaternion.Euler(leftKneeAngle,0f,0f);
                Quaternion rightKnee=Quaternion.Euler(rightKneeAngle,0f,0f);
                Quaternion leftAnkle=Quaternion.Euler(leftAnkleAngle,0f,0f);
                Quaternion rightAnkle=Quaternion.Euler(rightAnkleAngle,0f,0f);
                // Elbows flex forward; they must not alternate into hyperextension, or
                // continue their walking oscillation while the shoulders deliver an attack.
                // No gait-synchronous elbow pumping. Keep a small relaxed bend as a base;
                // class guard and attack key poses below provide the visible action.
                float elbowL=-12f;
                float elbowR=-12f;
                float wristL=0f,wristR=0f;
                float actionBlend=preparation>0?Mathf.SmoothStep(0f,1f,windupProgress):
                    Mathf.Clamp01(kick/Mathf.Max(.01f,attackWeight));
                float charge=preparation>0?1f-delivery:0f;
                switch(hero)
                {
                    case HeroId.Rook:
                    case HeroId.Veyr:
                        if(veyrBasic){elbowL=-48f;elbowR=-48f;break;}
                        bool both=abilitySlot==2||abilitySlot==3;
                        bool rightHand=attackSerial%2!=0;
                        float strikingElbow=Mathf.Lerp(-14f,-72f,charge);
                        elbowR=Mathf.Lerp(elbowR,both||rightHand?strikingElbow:-52f,actionBlend);
                        elbowL=Mathf.Lerp(elbowL,both||!rightHand?strikingElbow:-52f,actionBlend);
                        break;
                    case HeroId.Kaelon:
                        elbowR=Mathf.Lerp(elbowR,Mathf.Lerp(-20f,-68f,charge),actionBlend);
                        elbowL=Mathf.Lerp(elbowL,-42f,actionBlend);
                        wristR=Mathf.Lerp(6f,-12f,charge)*actionBlend;
                        break;
                    case HeroId.Nyra:
                        // A carried rifle keeps its grip rather than pumping the elbows.
                        elbowR=Mathf.Lerp(-18f,-24f,actionBlend);
                        elbowL=Mathf.Lerp(-24f,-30f,actionBlend);
                        break;
                    default:
                        elbowR=Mathf.Lerp(elbowR,Mathf.Lerp(-24f,-58f,charge),actionBlend);
                        elbowL=Mathf.Lerp(elbowL,Mathf.Lerp(-36f,-62f,charge),actionBlend);
                        wristR=-14f*actionBlend;
                        wristL=-10f*actionBlend;
                        break;
                }
                if(!attacking)
                {
                    // Shoulder, forearm and wrist hold the class silhouette in every gait phase.
                    if(hero==HeroId.Veyr||hero==HeroId.Rook)
                    {elbowL=-48f;elbowR=-48f;}
                    else if(hero==HeroId.Kaelon)
                    {elbowL=-34f;elbowR=-42f;}
                    else if(hero==HeroId.Elyra||hero==HeroId.Mira||hero==HeroId.Traveler)
                    {elbowL=-26f;elbowR=-30f;}
                    // A small shared elbow lift gives the guard life without a walking arm pump.
                    // Keep the rifle nearly steady and leave all attack poses untouched.
                    float elbowLift=Mathf.Sin(t*1.2f)*9.75f*(Dashing?0f:1f)*(hero==HeroId.Nyra?.4f:1f);
                    elbowL+=elbowLift;
                    elbowR+=elbowLift;
                }
                float hitGuard=Mathf.Clamp01(hitWeight);
                elbowL-=hero==HeroId.Nyra?0f:hitGuard*(hitFromSide<0f?4f:2f);
                elbowR-=hero==HeroId.Nyra?0f:hitGuard*(hitFromSide>0f?4f:2f);
                articulatedRig.SetProceduralPose(1f,
                    leftHip,leftKnee,leftAnkle,rightHip,rightKnee,rightAnkle,
                    Quaternion.Euler(elbowL,0f,0f),Quaternion.Euler(wristL,0f,0f),
                    Quaternion.Euler(elbowR,0f,0f),Quaternion.Euler(wristR,0f,0f));
            }

            // Full-body weight shift and counter-rotation. These are visual skeleton poses only;
            // attack and ability delivery still own the original combat timing.
            if(articulatedRig!=null)
            {
                float anticipation=preparation>0f?Mathf.SmoothStep(0f,1f,windupProgress)*(1f-delivery):0f;
                float attackSide=attackSerial%2==0?-1f:1f;
                float attackPitch=0f,attackTwist=0f,hipTwist=0f,attackRoll=0f;
                // Angles below already contain anticipation/contact weights; do not weight twice.
                switch(hero)
                {
                    case HeroId.Veyr:
                        if(veyrBasic)
                        {
                            float strike=VeyrStrikeWeight(Time.time-attackStarted);
                            attackPitch=-5f*strike;
                            attackTwist=3f*strike;
                            hipTwist=0f;
                            attackRoll=0f;
                        }
                        else
                        {
                            attackPitch=6f*anticipation-14f*kick;
                            attackTwist=attackSide*(-22f*anticipation+34f*kick);
                            hipTwist=attackSide*(-9f*anticipation+16f*kick);
                            attackRoll=attackSide*(5f*anticipation-8f*kick);
                        }
                        break;
                    case HeroId.Kaelon:
                        attackPitch=8f*anticipation-16f*kick;
                        attackTwist=-10f*anticipation+13f*kick;
                        hipTwist=-5f*anticipation+8f*kick;
                        attackRoll=-3f*anticipation+4f*kick;
                        break;
                    case HeroId.Rook:
                        attackPitch=10f*anticipation-17f*kick;
                        attackTwist=attackSide*(-13f*anticipation+19f*kick);
                        hipTwist=attackSide*(-8f*anticipation+12f*kick);
                        attackRoll=attackSide*(4f*anticipation-5f*kick);
                        break;
                    case HeroId.Nyra:
                        attackPitch=2f*anticipation-6f*kick;
                        attackTwist=-4f*anticipation+5f*kick;
                        hipTwist=-2f*anticipation+3f*kick;
                        break;
                    default:
                        attackPitch=5f*anticipation-8f*kick;
                        attackTwist=attackSide*(-5f*anticipation+7f*kick);
                        hipTwist=attackSide*(-2f*anticipation+4f*kick);
                        attackRoll=attackSide*2f*kick;
                        break;
                }

                float gaitHipYaw=Mathf.Sin(phase)*2.2f*strideWeight;
                float gaitSpineYaw=-Mathf.Sin(phase)*2.8f*strideWeight;
                float pelvisPitch=Mathf.Clamp(lean*.22f,-2f,2.5f)+Mathf.Sin(phase*2f)*.7f*strideWeight+recoil*.25f;
                float pelvisRoll=Mathf.Clamp(localAcceleration.x*.035f,-3f,3f)+attackRoll*.35f;
                // Turn the visual pelvis into a corner and lead strikes from the hip.
                // Clamp the combined turn so the anchored legs never see an extreme twist.
                if(spellCaster&&abilitySlot>=0)
                {
                    // Gather power through the hips, then let the chest and casting hand lead the release.
                    attackPitch+=7f*castCharge-10f*castRelease;
                    attackTwist+=attackSide*(16f*castCharge-8f*castRelease);
                    hipTwist+=attackSide*(-7f*castCharge+4f*castRelease);
                    attackRoll+=attackSide*(3f*castCharge-2f*castRelease);
                }
                float pelvisYaw=Mathf.Clamp(turnCounter*.30f+gaitHipYaw+hipTwist*.72f-hitFromSide*hitWeight*5f,-16f,16f);
                Quaternion pelvisPose=Quaternion.Euler(pelvisPitch,pelvisYaw,pelvisRoll);
                Quaternion lowerSpinePose=Quaternion.Euler(-lean*.18f+attackPitch*.28f-recoil*.45f-hitTorso*.25f,
                    -turnCounter*.16f-gaitHipYaw*.5f+hipTwist*.28f,
                    -sway*.24f-attackRoll*.30f+hitFromSide*hitWeight*3f);
                // Animate the shared joints so the chest, shoulders, neck and face move together.
                float idleRock=attacking||Dashing?0f:1f;
                float rockForward=Mathf.Sin(t*1.2f)*idleRock;
                float rockSide=Mathf.Sin(t*1.2f+Mathf.PI*.5f)*idleRock;
                Quaternion upperSpinePose=Quaternion.Euler(-lean*.24f+attackPitch*.72f-recoil*.85f-hitTorso*.90f+rockForward*8f,
                    turnCounter*.22f+gaitSpineYaw+attackTwist+hitFromSide*hitWeight*7f+rockSide*4.5f,
                    sway*.22f+attackRoll*.55f-hitFromSide*hitWeight*4f);
                Quaternion neckPose=Quaternion.Euler(lean*.08f-attackPitch*.18f+recoil*.45f+hitTorso*.32f,
                    -turnCounter*.20f-attackTwist*.35f+rockForward*12f,
                    -sway*.10f-attackRoll*.35f);
                articulatedRig.SetBodyPose(1f,pelvisPose,lowerSpinePose,upperSpinePose,neckPose);
            }
            UpdateVeyrTrails(veyrBasic);
        }
        private static void EvaluateRunLeg(float cycle,float hipRange,float kneeRange,float weight,float stance,
            out float hipAngle,out float kneeAngle,out float ankleAngle,bool imported=false)
        {
            // A clear contact pattern: heel settles, the sole stays quiet under load, then
            // the toes push off. Running shortens contact; it does not just swing the legs faster.
            cycle=Mathf.Repeat(cycle,1f);
            stance=Mathf.Clamp(stance,.52f,.68f);
            if(cycle<stance)
            {
                float u=cycle/stance;
                float hipU=Mathf.SmoothStep(0f,1f,u);
                hipAngle=Mathf.Lerp(hipRange,-hipRange,hipU)*(imported?-1f:1f);

                float load=u<.14f?Mathf.Lerp(0f,1f,Mathf.SmoothStep(0f,1f,u/.14f)):
                    u>.78f?Mathf.Lerp(1f,0f,Mathf.SmoothStep(.78f,1f,u)):1f;
                kneeAngle=Mathf.Lerp(2f,8f,load)*weight;

                float footPitch;
                if(u<.12f) footPitch=Mathf.Lerp(-7f,0f,Mathf.SmoothStep(0f,1f,u/.12f));
                else if(u<.76f) footPitch=0f; // quiet, planted sole
                else footPitch=Mathf.Lerp(0f,18f,Mathf.SmoothStep(0f,1f,(u-.76f)/.24f));
                // Counter-rotate against thigh and shin so the sole stays planted; the final
                // part releases into a distinct toe-off rather than drawing a pendulum arc.
                ankleAngle=Mathf.Clamp(footPitch-hipAngle-kneeAngle,-60f,60f)*weight;
                return;
            }

            float swing=Mathf.Clamp01((cycle-stance)/(1f-stance));
            float eased=Mathf.SmoothStep(0f,1f,swing);
            hipAngle=Mathf.Lerp(-hipRange,hipRange,eased)*(imported?-1f:1f);
            // Bend after push-off, clear the ground, then extend into a heel-first landing.
            float kneeLift=Mathf.Sin(Mathf.PI*swing);
            kneeAngle=(Mathf.Lerp(5f,1f,eased)+kneeRange*kneeLift)*weight;

            float toeOffAngle=Mathf.Clamp(18f+(imported?-hipRange:hipRange)-5f,-60f,60f)*weight;
            float recoveryAngle=-9f*weight;
            ankleAngle=swing<.22f
                ?Mathf.Lerp(toeOffAngle,recoveryAngle,Mathf.SmoothStep(0f,1f,swing/.22f))
                :recoveryAngle;
            if(swing>.78f)
            {
                float plantAngle=Mathf.Clamp(-7f-hipAngle-kneeAngle,-60f,60f)*weight;
                ankleAngle=Mathf.Lerp(ankleAngle,plantAngle,Mathf.SmoothStep(.78f,1f,(swing-.78f)/.22f));
            }
        }

        private void SetupCloth()
        {
            // Clone only fabric meshes for this hero. Armor and collision geometry stay shared.
            Transform[] visualParts=root.GetComponentsInChildren<Transform>(true);
            foreach(Transform part in visualParts)
            {
                if(part==null||part==root||!StylizedHeroVisual.IsFlowingFabric(part.name))continue;
                MeshFilter filter=part.GetComponent<MeshFilter>();
                MeshRenderer renderer=part.GetComponent<MeshRenderer>();
                if(filter==null||renderer==null||!renderer.enabled||filter.sharedMesh==null)continue;
                Mesh mesh=Instantiate(filter.sharedMesh);
                mesh.name=filter.sharedMesh.name+"_Flow";
                mesh.MarkDynamic();
                Vector3[] rest=mesh.vertices;
                if(rest.Length==0){Destroy(mesh);continue;}
                float top=float.NegativeInfinity,bottom=float.PositiveInfinity;
                foreach(Vector3 vertex in rest){top=Mathf.Max(top,vertex.y);bottom=Mathf.Min(bottom,vertex.y);}
                // Bring the hem inward and shorten it while leaving the shoulder attachment in place.
                // Every seam receives the same taper as its panel, so the pieces still meet.
                float height=Mathf.Max(.001f,top-bottom);
                for(int v=0;v<rest.Length;v++)
                {
                    Vector3 point=rest[v];
                    float loose=Mathf.Clamp01((top-point.y)/height);
                    point.x*=Mathf.Lerp(1f,.72f,loose);
                    point.y=top-(top-point.y)*.84f;
                    point.z*=Mathf.Lerp(1f,.82f,loose);
                    rest[v]=point;
                }
                mesh.vertices=rest;
                bottom=top-height*.84f;
                mesh.RecalculateBounds();
                Bounds bounds=mesh.bounds;bounds.Expand(.9f);mesh.bounds=bounds;
                filter.sharedMesh=mesh;
                clothPanels.Add(new ClothPanel{mesh=mesh,rest=rest,vertices=(Vector3[])rest.Clone(),
                    top=top,bottom=bottom,phase=clothPanels.Count*1.31f});
            }
        }
        private void UpdateCloth(float deltaTime,float time,Vector3 worldVelocity)
        {
            if(clothPanels.Count==0)return;
            float dt=Mathf.Clamp(deltaTime,0f,.05f);
            if(dt<=0f)return;
            Vector3 localVelocity=transform.InverseTransformDirection(worldVelocity);
            float speed=Mathf.Min(9f,new Vector2(localVelocity.x,localVelocity.z).magnitude);
            Vector2 travel=new Vector2(Mathf.Clamp(-localVelocity.x*.045f,-.20f,.20f),
                Mathf.Clamp(-localVelocity.z*.055f,-.28f,.28f));
            for(int i=0;i<clothPanels.Count;i++)
            {
                ClothPanel panel=clothPanels[i];
                float flutter=Mathf.Sin(time*2.7f+panel.phase);
                Vector2 target=travel+new Vector2(flutter*.035f,Mathf.Sin(time*2.1f+panel.phase)*.024f);
                panel.velocity+=(target-panel.offset)*(26f*dt);
                panel.velocity*=Mathf.Exp(-7f*dt);
                panel.offset+=panel.velocity*dt;
                float height=Mathf.Max(.001f,panel.top-panel.bottom);
                for(int v=0;v<panel.rest.Length;v++)
                {
                    Vector3 point=panel.rest[v];
                    float loose=Mathf.Clamp01((panel.top-point.y)/height);
                    float bend=loose*loose;
                    float fold=Mathf.Sin(time*3.5f-loose*3.2f+panel.phase)*(.016f+speed*.002f);
                    panel.vertices[v]=point+new Vector3((panel.offset.x+fold)*bend,
                        0f,(panel.offset.y+fold*.7f)*bend);
                }
                panel.mesh.vertices=panel.vertices;
            }
        }

        private void SetEnergy(float energy)
        {
            if(energyRenderers==null||energyBlock==null)return;
            foreach(Renderer renderer in energyRenderers)
            {
                if(renderer==null||!renderer.enabled)continue;
                renderer.GetPropertyBlock(energyBlock);energyBlock.SetFloat(EnergyBoost,energy);renderer.SetPropertyBlock(energyBlock);
            }
        }

        private static float VeyrStrikeWeight(float seconds)
        {
            if(seconds<.14f)return 0f;
            if(seconds<.35f)return Mathf.SmoothStep(0f,1f,(seconds-.14f)/.21f);
            return 1f-Mathf.SmoothStep(0f,1f,(seconds-.35f)/.35f);
        }

        private void PoseVeyrBasic(float seconds)
        {
            // Both weapons retain their fixed grip rotation. The shoulder pivots drive the
            // entire hand/forearm/sword chain across the center line in opposite directions.
            float open=seconds<.14f?Mathf.SmoothStep(0f,1f,seconds/.14f):1f;
            float cut=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((seconds-.14f)/.21f));
            float through=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((seconds-.35f)/.11f));
            float recover=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((seconds-.46f)/.24f));
            Quaternion left=Quaternion.Slerp(Quaternion.Euler(VeyrGuardArm(false)),Quaternion.Euler(-44f,-30f,-72f),open);
            Quaternion right=Quaternion.Slerp(Quaternion.Euler(VeyrGuardArm(true)),Quaternion.Euler(-44f,30f,72f),open);
            left=Quaternion.Slerp(left,Quaternion.Euler(-68f,10f,42f),cut);
            right=Quaternion.Slerp(right,Quaternion.Euler(-68f,-10f,-42f),cut);
            left=Quaternion.Slerp(left,Quaternion.Euler(-61f,14f,54f),through);
            right=Quaternion.Slerp(right,Quaternion.Euler(-61f,-14f,-54f),through);
            left=Quaternion.Slerp(left,Quaternion.Euler(VeyrGuardArm(false)),recover);
            right=Quaternion.Slerp(right,Quaternion.Euler(VeyrGuardArm(true)),recover);
            if(armL!=null)armL.localRotation=armRestL*left;
            if(armR!=null)armR.localRotation=armRestR*right;
            if(weaponL!=null)weaponL.localRotation=weaponRestL*VeyrGuardBlade(false);
            if(weaponR!=null)weaponR.localRotation=weaponRestR*VeyrGuardBlade(true);
        }

        private Material veyrTrailMaterial;
        private TrailRenderer CreateVeyrTrail(Transform weapon,bool right)
        {
            if(weapon==null)return null;
            var tip=new GameObject(right?"DF_VeyrSlashTipR":"DF_VeyrSlashTipL");
            tip.transform.SetParent(weapon,false);
            tip.transform.localPosition=new Vector3(right?.20f:-.20f,-.50f,1.02f);
            var trail=tip.AddComponent<TrailRenderer>();
            trail.time=.12f;
            trail.startWidth=.105f;
            trail.endWidth=0f;
            trail.minVertexDistance=.015f;
            trail.numCornerVertices=3;
            trail.numCapVertices=3;
            trail.alignment=LineAlignment.View;
            Shader shader=Shader.Find("Sprites/Default");
            if(shader!=null)
            {
                if(veyrTrailMaterial==null)veyrTrailMaterial=new Material(shader){name="DF_OwnedVeyrTrail"};
                trail.sharedMaterial=veyrTrailMaterial;
            }
            trail.startColor=new Color(.66f,.94f,1f,.85f);
            trail.endColor=new Color(.31f,.69f,1f,0f);
            trail.emitting=false;
            return trail;
        }

        private void UpdateVeyrTrails(bool basic)
        {
            if(veyrTrailL==null&&veyrTrailR==null)return;
            float age=Time.time-attackStarted;
            bool emit=basic&&age>=.14f&&age<.47f&&health!=null&&!health.IsDead;
            if(veyrTrailL!=null)veyrTrailL.emitting=emit;
            if(veyrTrailR!=null)veyrTrailR.emitting=emit;
        }

        // Presentation-only keyframes; HeroAttackDelivery owns windup/flight damage timing.
        // The weapon is a child of the shoulder rig, so both arm and weapon follow the full strike arc.
        private static float StrikeDelivery(float windupProgress)
        {
            // Preserve combat's release time: the last 45% is the forward strike, not a hold.
            return Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.55f,1f,windupProgress));
        }

        private void PoseStrike(Transform joint,Quaternion rest,Vector3 drawn,Vector3 contact,Vector3 through,
            float preparation,float progress,float weight=1)
        {
            if(joint==null)return;
            Quaternion ready=Quaternion.Euler(drawn),hit=Quaternion.Euler(contact),finish=Quaternion.Euler(through),pose;
            if(preparation>0)
            {
                float charge=Mathf.Clamp01((Time.time-attackStarted)/Mathf.Max(.001f,windupUntil-attackStarted));
                // Load the shoulder first, then accelerate through the existing windup.
                // At release this converges to the same contact pose as progress == 0.
                Quaternion loaded=Quaternion.Slerp(Quaternion.identity,ready,
                    Mathf.SmoothStep(0f,1f,Mathf.Clamp01(charge/.55f)));
                pose=Quaternion.Slerp(loaded,hit,StrikeDelivery(charge));
            }
            else
            {
                // Damage has already resolved. Contact is frame zero; no post-hit fake windup.
                if(progress<.24f)pose=Quaternion.Slerp(hit,finish,Mathf.SmoothStep(0,1,progress/.24f));
                else pose=Quaternion.Slerp(finish,Quaternion.identity,Mathf.SmoothStep(0,1,(progress-.24f)/.76f));
            }
            Quaternion target=rest*Quaternion.Slerp(Quaternion.identity,pose,weight);
            // Restore the current walking swing during recovery rather than jumping into it on the next frame.
            if(preparation<=0&&progress>.24f)
                target=Quaternion.Slerp(target,joint.localRotation,Mathf.SmoothStep(0,1,(progress-.24f)/.76f));
            Quaternion previous=joint==armR?attackArmR:joint==armL?attackArmL:joint==weaponR?attackWeaponR:attackWeaponL;
            float entry=preparation>0?Mathf.SmoothStep(0,1,(Time.time-attackStarted)/.055f):1;
            joint.localRotation=Quaternion.Slerp(previous,target,entry);
        }

        private static Vector3 VeyrGuardArm(bool right)
            =>right?new Vector3(-42f,10f,24f):new Vector3(-18f,-12f,-32f);

        private static Quaternion VeyrGuardBlade(bool right)
            // The blade axes face forward: positive yaw turns the left blade right, negative yaw turns the right blade left.
            // Turn each in-game guard blade another 20% toward the opposite side.
            =>right?Quaternion.Euler(-18f,-36.96f,48f):Quaternion.Euler(16f,40.32f,-48f);

        private void ApplyAttackPose(HeroId hero,float kick,float preparation,float progress,float windupProgress,float attackWeight)
        {
            if(weaponR!=null){weaponR.localPosition=weaponPositionR;weaponR.localRotation=weaponRestR;}
            if(weaponL!=null){weaponL.localPosition=weaponPositionL;weaponL.localRotation=weaponRestL;}
            if(hero==HeroId.Veyr&&abilitySlot==-1&&Time.time<attackUntil)
            {
                PoseVeyrBasic(Time.time-attackStarted);
                return;
            }
            if(kick<=0&&preparation<=0&&Time.time>=empowerUntil)return;
            switch(hero)
            {
                case HeroId.Veyr:
                    if(abilitySlot==2)break; // Dash has its own presentation.
                    bool right=attackSerial%2!=0;
                    bool dual=abilitySlot==3;
                    // One hand cuts diagonally across the body; the free hand keeps its guard.
                    if(right||dual)
                    {
                        PoseStrike(armR,armRestR,new Vector3(-36,28,42),new Vector3(-94,-34,-8),new Vector3(-60,-58,-30),
                            preparation,progress);
                        PoseStrike(weaponR,weaponRestR,new Vector3(-24,24,18),new Vector3(30,-52,-26),new Vector3(52,-74,-44),
                            preparation,progress);
                    }
                    else
                    {
                        if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(VeyrGuardArm(true));
                        if(weaponR!=null)weaponR.localRotation=weaponRestR*VeyrGuardBlade(true);
                    }
                    if(!right||dual)
                    {
                        PoseStrike(armL,armRestL,new Vector3(-36,-28,-42),new Vector3(-94,34,8),new Vector3(-60,58,30),
                            preparation,progress);
                        PoseStrike(weaponL,weaponRestL,new Vector3(-24,-24,-18),new Vector3(30,52,26),new Vector3(52,74,44),
                            preparation,progress);
                    }
                    else
                    {
                        if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(VeyrGuardArm(false));
                        if(weaponL!=null)weaponL.localRotation=weaponRestL*VeyrGuardBlade(false);
                    }
                    break;
                case HeroId.Kaelon:
                    if(abilitySlot==1)
                    {
                        Swing(armL,armRestL,-55*(preparation+kick));break;
                    }
                    // Negative shoulder X raises the hand forward; wrist rotation brings the hammer head down.
                    PoseStrike(armR,armRestR,new Vector3(-150,-15,-18),new Vector3(-75,8,-8),new Vector3(-35,16,8),preparation,progress);
                    PoseStrike(weaponR,weaponRestR,new Vector3(-35,0,-10),new Vector3(110,0,5),new Vector3(130,0,12),preparation,progress);
                    PoseStrike(armL,armRestL,new Vector3(-28,0,8),new Vector3(-45,0,8),new Vector3(-30,0,5),preparation,progress);
                    break;
                case HeroId.Nyra:
                    if(weaponR!=null)weaponR.localPosition=weaponPositionR+Vector3.back*.12f*kick;
                    if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(-7*kick,0,0);
                    break;
                case HeroId.Elyra:
                case HeroId.Mira:
                case HeroId.Traveler:
                    if(abilitySlot>=0)
                    {
                        // Two-beat cast: hands gather near the chest, then the lead palm/weapon
                        // drives forward while the support hand stays open and stabilizes.
                        float charge=preparation>0f?Mathf.SmoothStep(0f,1f,windupProgress):0f;
                        float release=preparation<=0f?Mathf.Clamp01(kick/Mathf.Max(.01f,attackWeight)):0f;
                        float side=attackSerial%2==0?-1f:1f;
                        if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(-18f-30f*charge-76f*release,
                            side*(8f*charge+5f*release),-14f-18f*charge-8f*release);
                        if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(-22f-36f*charge-30f*release,
                            -side*(10f*charge+3f*release),18f+24f*charge+8f*release);
                    }
                    else
                    {
                        if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(
                            identity!=null&&identity.Hero==HeroId.Traveler?-34*preparation-36*kick:-45*preparation-42*kick,
                            0,-18*(preparation+kick));
                        if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(
                            identity!=null&&identity.Hero==HeroId.Traveler?-18*preparation-20*kick:-25*preparation-25*kick,
                            0,20*kick);
                    }
                    break;
                case HeroId.Rook:
                    if(abilitySlot==1)
                    {
                        Swing(armR,armRestR,-38*(preparation+kick));Swing(armL,armRestL,-38*(preparation+kick));break;
                    }
                    bool slam=abilitySlot==2||abilitySlot==3;
                    bool punchRight=attackSerial%2!=0;
                    Vector3 drawR=slam?new Vector3(-150,-12,-12):new Vector3(-20,-22,-12);
                    Vector3 hitR=slam?new Vector3(-65,4,0):new Vector3(-95,12,0);
                    Vector3 finishR=slam?new Vector3(-22,8,6):new Vector3(-105,20,8);
                    PoseStrike(armR,armRestR,drawR,hitR,finishR,preparation,progress,slam||punchRight?1:.25f);
                    PoseStrike(armL,armRestL,new Vector3(drawR.x,-drawR.y,-drawR.z),
                        new Vector3(hitR.x,-hitR.y,-hitR.z),new Vector3(finishR.x,-finishR.y,-finishR.z),
                        preparation,progress,slam||!punchRight?1:.25f);
                    break;
            }
        }

    }
}

