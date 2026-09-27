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
        private HeroIdentity identity;
        private Health health;
        private HeroLocomotion locomotion;
        private HeroProceduralRig articulatedRig;
        private Transform root,torso,head,legL,legR,bootL,bootR,armL,armR,orbitL,orbitR,wingL,wingR,cape,blade;
        private Quaternion torsoRest,headRest,bootRestL,bootRestR,armRestL,armRestR,bladeRest;
        private Vector3 basePosition,lastPosition,bootPositionL,bootPositionR;
        private float previousHealth,hitUntil;
        private float phase,strideWeight,attackUntil,attackDuration,attackWeight,lean,displayedSpeed;
        private Vector3 previousLocalVelocity;
        private Quaternion displayedRotation=Quaternion.identity;
        private Vector3 displayedScale;
        private bool wasDead,moving;
        private int abilitySlot=-1,attackSerial;
        private float windupUntil,windupDuration,empowerUntil,dashUntil,attackStarted;
        private float previousHeading;
        private Transform muzzle,weaponR,weaponL,orb,gauntletR,gauntletL;
        private Vector3 rootScale,weaponPositionR,weaponPositionL,orbRest;
        private Quaternion weaponRestR,weaponRestL;
        private Quaternion attackArmR,attackArmL,attackWeaponR,attackWeaponL;
        private Renderer[] energyRenderers;
        private MaterialPropertyBlock energyBlock;
        private static readonly int EnergyBoost=Shader.PropertyToID("_EnergyBoost");
        public Vector3 ReleasePoint
        {
            get
            {
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
        public bool Dashing=>locomotion!=null&&locomotion.OwnsMovementFrame;
        public void PulseAttack(bool ultimate)
        {
            // Snapshot the visible pose before restarting a combo; never snap a raised weapon to rest.
            attackArmR=Rest(armR);attackArmL=Rest(armL);
            attackWeaponR=Rest(weaponR);attackWeaponL=Rest(weaponL);
            abilitySlot=ultimate?3:-1;attackSerial++;
            GetComponent<VoidWingPresentation>()?.Pulse();
            HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
            bool melee=hero==HeroId.Veyr||hero==HeroId.Kaelon||hero==HeroId.Rook;
            attackDuration=melee?(ultimate?.46f:hero==HeroId.Kaelon?.40f:.34f):(ultimate?.32f:.22f);
            attackUntil=Time.time+attackDuration;attackWeight=ultimate?1f:.62f;windupUntil=0;windupDuration=0;
            attackStarted=Time.time;
        }
        public void PulseBasic(float windup)
        {
            PulseAttack(false);
            windupUntil=Time.time+windup;windupDuration=Mathf.Max(0,windup);attackUntil=windupUntil+attackDuration;
        }
        public void PulseAbility(int slot,float windup)
        {
            PulseAttack(slot==3);abilitySlot=slot;
            windupDuration=Mathf.Max(0,windup);windupUntil=Time.time+windupDuration;attackUntil=windupUntil+attackDuration;
            if(identity!=null&&identity.Hero==HeroId.Rook&&slot==3)Empower(1.1f);
        }
        public void Empower(float seconds)=>empowerUntil=Time.time+Mathf.Max(0,seconds);
        public void PulseDash()=>dashUntil=Time.time+.17f;
        private Transform FindPart(string name)
        {
            if(root==null)return null;
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name=="DF_"+name)return t;
            return null;
        }
        private void Start()
        {
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
            if(GetComponent<AllFigureMotionRealism>()?.enabled==true)
                Debug.LogError("[Alterverse Movement] Competing AllFigureMotionRealism on hero '"+name+"'.",this);
            articulatedRig=GetComponent<HeroProceduralRig>()??gameObject.AddComponent<HeroProceduralRig>();
            articulatedRig.Build(root);
            if(!articulatedRig.Ready)
                Debug.LogError("[Alterverse Movement] Procedural joints are incomplete on '"+name+"'; gait has been stopped instead of switching to another controller.",this);
            Debug.Log("[Alterverse Movement] HeroVisualMotion active on '"+name+"' ("+identity?.Hero+").",this);
            SetupCloth();
            basePosition=root.localPosition;rootScale=root.localScale;displayedScale=rootScale;displayedRotation=root.localRotation;lastPosition=transform.position;
            previousHeading=transform.eulerAngles.y;
            energyRenderers=root.GetComponentsInChildren<Renderer>();
            gauntletR=FindPart("GauntletR");gauntletL=FindPart("GauntletL");
            torso=FindPart("Torso");head=FindPart("Head");torsoRest=Rest(torso);headRest=Rest(head);
            muzzle=FindPart("Muzzle");weaponR=FindPart("WeaponR");weaponL=FindPart("WeaponL");orb=FindPart("Orb");
            if(weaponR!=null){weaponPositionR=weaponR.localPosition;weaponRestR=weaponR.localRotation;}
            if(weaponL!=null){weaponPositionL=weaponL.localPosition;weaponRestL=weaponL.localRotation;}
            if(orb!=null)orbRest=orb.localPosition;
            legL=FindPart("LegL");legR=FindPart("LegR");bootL=FindPart("BootL");bootR=FindPart("BootR");
            armL=root.Find("DF_ArmL")??root.Find("DF_GauntletL");armR=root.Find("DF_ArmR")??root.Find("DF_GauntletR");
            orbitL=root.Find("DF_OrbitL");orbitR=root.Find("DF_OrbitR");wingL=root.Find("DF_WingL");wingR=root.Find("DF_WingR");cape=root.Find("DF_Cape");blade=root.Find("DF_BladeR");
            if(bootL!=null)bootPositionL=bootL.localPosition;if(bootR!=null)bootPositionR=bootR.localPosition;
            if(health!=null){previousHealth=health.Current;health.Changed+=OnHealthChanged;}
            bootRestL=Rest(bootL);bootRestR=Rest(bootR);armRestL=Rest(armL);armRestR=Rest(armR);bladeRest=Rest(blade);
        }
        private void OnHealthChanged(float current,float max)
        {if(current<previousHealth-.01f)hitUntil=Time.time+.12f;previousHealth=current;}
        private void OnDestroy(){if(health!=null)health.Changed-=OnHealthChanged;foreach(ClothPanel panel in clothPanels)if(panel.mesh!=null)Destroy(panel.mesh);clothPanels.Clear();}
        private static Quaternion Rest(Transform t)=>t!=null?t.localRotation:Quaternion.identity;
        private static void Swing(Transform t,Quaternion rest,float angle){if(t!=null)t.localRotation=rest*Quaternion.Euler(angle,0,0);}
        private void LateUpdate()
        {
            if(root==null)return;
            Vector3 delta=transform.position-lastPosition;delta.y=0;lastPosition=transform.position;
            if(Time.timeScale<=0){UpdateCloth(Time.unscaledDeltaTime,Time.unscaledTime,Vector3.zero);previousLocalVelocity=Vector3.zero;return;}
            float dt=Mathf.Max(Time.deltaTime,.0001f);
            if(health!=null&&health.IsDead){wasDead=true;PlanarSpeed=0;strideWeight=0;attackUntil=windupUntil=dashUntil=empowerUntil=0;root.localScale=rootScale;displayedScale=rootScale;displayedSpeed=0;SetEnergy(0);return;}
            // Recall/respawn is a relocation, not a sprint across the arena.
            if(wasDead||delta.magnitude>Mathf.Max(3,dt*40))
            {wasDead=false;moving=false;phase=0;strideWeight=lean=displayedSpeed=0;previousLocalVelocity=Vector3.zero;root.localPosition=basePosition;root.localRotation=displayedRotation=Quaternion.identity;displayedScale=rootScale;delta=Vector3.zero;}
            HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
            bool heavy=hero==HeroId.Kaelon||hero==HeroId.Rook;
            float travel=delta.magnitude;
            float turnRate=Mathf.DeltaAngle(previousHeading,transform.eulerAngles.y)/dt;
            previousHeading=transform.eulerAngles.y;
            PlanarSpeed=travel/dt;
            Vector3 localVelocity=transform.InverseTransformDirection(delta/dt);
            Vector3 localAcceleration=(localVelocity-previousLocalVelocity)/dt;
            previousLocalVelocity=localVelocity;
            // Ease the visible gait in and out while keeping collision and input immediate.
            displayedSpeed=Mathf.Lerp(displayedSpeed,PlanarSpeed,1-Mathf.Exp(-(PlanarSpeed>displayedSpeed?12f:9f)*dt));
            float response=1-Mathf.Exp(-11f*dt);
            // Tiny steering corrections should not restart the walk cycle. The gameplay root
            // can still settle by a few centimetres without making the hero visually shuffle.
            if(moving){if(PlanarSpeed<.10f)moving=false;}
            else if(PlanarSpeed>.28f)moving=true;
            if(Dashing)moving=false;
            float desired=Dashing||!moving?0f:Mathf.InverseLerp(.10f,1.1f,displayedSpeed);
            strideWeight=Mathf.Lerp(strideWeight,desired,response);
            // One complete gait cycle per traveled stride, not one per fixed time interval.
            float strideLength=heavy?1.65f:1.45f;
            if(!Dashing)phase=Mathf.Repeat(phase+travel/strideLength*Mathf.PI*2,Mathf.PI*2);
            float runBlend=Mathf.InverseLerp(1.8f,4.5f,displayedSpeed)*strideWeight;
            float step=Mathf.Sin(phase)*Mathf.Lerp(30f,48f,runBlend)*strideWeight;
            // The articulated rig owns its feet and knees. Moving the legacy boot as well
            // would apply each stride twice and detach it from the ankle joint.
            // The procedural joints own the legs and feet. No imported clip or secondary gait path.
            Swing(armL,armRestL,-step*.85f);Swing(armR,armRestR,step*.85f);
            float forwardLean=Mathf.Clamp(localAcceleration.z*.055f+localVelocity.z*.11f,-2.5f,5f);
            lean=Mathf.Lerp(lean,forwardLean,1-Mathf.Exp(-13f*dt));
            Quaternion armBeforeL=Rest(armL),armBeforeR=Rest(armR);
            float t=Time.time;
            float idle=Mathf.Sin(t*1.8f)*.014f*(1-strideWeight);
            float bob=(1-Mathf.Cos(phase*2))*.012f*strideWeight*(heavy?.8f:1);
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
            float kick=Time.time<attackUntil&&preparation==0?Mathf.Cos(progress*Mathf.PI*.5f)*attackWeight:0;
            ApplyAttackPose(hero,kick,preparation,progress);
            // Blend shoulders across gait and spell changes; attack timing remains owned by combat.
            float armBlend=1-Mathf.Exp(-(preparation>0||kick>0?28f:17f)*dt);
            if(armL!=null)armL.localRotation=Quaternion.Slerp(armBeforeL,armL.localRotation,armBlend);
            if(armR!=null)armR.localRotation=Quaternion.Slerp(armBeforeR,armR.localRotation,armBlend);
            float energy=Mathf.Max(kick,Mathf.Max(preparation*.65f,Time.time<empowerUntil?.85f:0));
            SetEnergy(energy);
            bool phaseDash=(Dashing||Time.time<dashUntil)&&(hero==HeroId.Veyr||hero==HeroId.Elyra);
            // Compress the render rig only; collider and movement are untouched.
            Vector3 targetScale=Vector3.Scale(rootScale,phaseDash?new Vector3(.55f,.94f,.88f):Vector3.one);
            displayedScale=Vector3.Lerp(displayedScale,targetScale,1-Mathf.Exp(-18f*dt));
            root.localScale=displayedScale;
            float recoil=Time.time<hitUntil?Mathf.Sin((hitUntil-Time.time)/.12f*Mathf.PI)*(heavy?1:2):0;
            float idleShift=Mathf.Sin(t*.8f+phase*.12f)*(1-strideWeight);
            float sway=Mathf.Sin(phase)*strideWeight*Mathf.Lerp(1.6f,3.2f,runBlend)*(heavy?.8f:1)+idleShift*.45f;
            Quaternion targetRotation=Quaternion.Euler((Dashing?9:lean)-7*kick-recoil,spinYaw,sway);
            // Full ability spins need their continuous angle; smoothing quaternions there would skip the arc.
            displayedRotation=fullSpin&&preparation>0?targetRotation:Quaternion.Slerp(displayedRotation,targetRotation,1-Mathf.Exp(-16f*dt));
            root.localRotation=displayedRotation;
            float turnCounter=Mathf.Clamp(-turnRate*.025f,-12f,12f);
            if(torso!=null)torso.localRotation=torsoRest*Quaternion.Euler(lean*.35f,turnCounter-Mathf.Sin(phase)*3f*strideWeight,-sway*.45f);
            if(head!=null)head.localRotation=headRest*Quaternion.Euler(-lean*.35f,-turnCounter*.45f+Mathf.Sin(phase)*2f*strideWeight,-sway*.5f);
            if(orbitL!=null){orbitL.localPosition=new Vector3(-.58f,.28f+Mathf.Sin(t*1.4f)*.08f,.03f);orbitL.localRotation=Quaternion.Euler(38,t*35,-35);}
            if(orbitR!=null){orbitR.localPosition=new Vector3(.58f,.28f-Mathf.Sin(t*1.4f)*.08f,.03f);orbitR.localRotation=Quaternion.Euler(38,-t*35,35);}
            if(orb!=null)orb.localPosition=orbRest+Vector3.up*(Mathf.Sin(t*2)*.025f+preparation*.08f);
            if(wingL!=null)wingL.localRotation=Quaternion.Euler(0,-18,-18-Mathf.Sin(t*2.1f)*4);
            if(wingR!=null)wingR.localRotation=Quaternion.Euler(0,18,18+Mathf.Sin(t*2.1f)*4);
            if(cape!=null)cape.localRotation=Quaternion.Euler(2+Mathf.Min(PlanarSpeed,8)*.3f,0,Mathf.Sin(t*2)*.5f);
            UpdateCloth(dt,t,delta/dt);
            if(blade!=null)blade.localRotation=bladeRest*Quaternion.Euler(22*strideWeight,0,-12*strideWeight);
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
                EvaluateRunLeg(leftCycle,hipRange,kneeRange,out float leftHipAngle,out float leftKneeAngle,out float leftAnkleAngle);
                EvaluateRunLeg(rightCycle,hipRange,kneeRange,out float rightHipAngle,out float rightKneeAngle,out float rightAnkleAngle);
                Quaternion leftHip=Quaternion.Euler(leftHipAngle,0f,Mathf.Sin(phase)*2.5f*strideWeight);
                Quaternion rightHip=Quaternion.Euler(rightHipAngle,0f,Mathf.Sin(phase+Mathf.PI)*2.5f*strideWeight);
                Quaternion leftKnee=Quaternion.Euler(leftKneeAngle,0f,0f);
                Quaternion rightKnee=Quaternion.Euler(rightKneeAngle,0f,0f);
                Quaternion leftAnkle=Quaternion.Euler(leftAnkleAngle,0f,0f);
                Quaternion rightAnkle=Quaternion.Euler(rightAnkleAngle,0f,0f);
                float armCounter=Mathf.Lerp(12f,22f,runBlend)*strideWeight;
                articulatedRig.SetProceduralPose(1f,
                    leftHip,leftKnee,leftAnkle,rightHip,rightKnee,rightAnkle,
                    Quaternion.Euler(-Mathf.Sin(phase)*armCounter,0f,0f),Quaternion.identity,
                    Quaternion.Euler(-Mathf.Sin(phase+Mathf.PI)*armCounter,0f,0f),Quaternion.identity);
            }
        }
        private static void EvaluateRunLeg(float cycle,float hipRange,float kneeRange,
            out float hipAngle,out float kneeAngle,out float ankleAngle)
        {
            // About three fifths of the cycle keeps the foot planted while the body passes over it.
            const float stance=.62f;
            cycle=Mathf.Repeat(cycle,1f);
            if(cycle<stance)
            {
                float u=Mathf.SmoothStep(0f,1f,cycle/stance);
                hipAngle=Mathf.Lerp(hipRange,-hipRange,u);
                kneeAngle=Mathf.Lerp(3f,7f,u);
                ankleAngle=Mathf.Lerp(-6f,12f,u);
                return;
            }

            float swing=Mathf.SmoothStep(0f,1f,(cycle-stance)/(1f-stance));
            float arc=Mathf.Sin(Mathf.PI*swing);
            hipAngle=Mathf.Lerp(-hipRange,hipRange,swing);
            kneeAngle=kneeRange*arc;
            // Toe-off points the foot down; recovery brings it back level before contact.
            ankleAngle=Mathf.Lerp(13f,-8f,swing)+arc*5f;
        }

        private void SetupCloth()
        {
            // Clone only fabric meshes for this hero. Armor and collision geometry stay shared.
            foreach(Transform part in root)
            {
                if(!StylizedHeroVisual.IsFlowingFabric(part.name))continue;
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

        // Presentation-only keyframes; HeroAttackDelivery owns windup/flight damage timing.
        // The weapon is a child of the shoulder rig, so both arm and weapon follow the full strike arc.
        private void PoseStrike(Transform joint,Quaternion rest,Vector3 drawn,Vector3 contact,Vector3 through,
            float preparation,float progress,float weight=1)
        {
            if(joint==null)return;
            Quaternion ready=Quaternion.Euler(drawn),hit=Quaternion.Euler(contact),finish=Quaternion.Euler(through),pose;
            if(preparation>0)
            {
                float charge=Mathf.Clamp01((Time.time-attackStarted)/Mathf.Max(.001f,windupUntil-attackStarted));
                pose=Quaternion.Slerp(Quaternion.identity,ready,Mathf.SmoothStep(0,1,Mathf.Min(1,charge*2)));
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

        private void ApplyAttackPose(HeroId hero,float kick,float preparation,float progress)
        {
            if(weaponR!=null){weaponR.localPosition=weaponPositionR;weaponR.localRotation=weaponRestR;}
            if(weaponL!=null){weaponL.localPosition=weaponPositionL;weaponL.localRotation=weaponRestL;}
            if(kick<=0&&preparation<=0&&Time.time>=empowerUntil)return;
            switch(hero)
            {
                case HeroId.Veyr:
                    if(abilitySlot==2)break; // Dash has its own presentation.
                    bool right=attackSerial%2!=0;
                    bool dual=abilitySlot==3;
                    PoseStrike(armR,armRestR,new Vector3(-30,-38,-40),new Vector3(-88,32,12),new Vector3(-48,58,25),
                        preparation,progress,right||dual?1:.30f);
                    PoseStrike(armL,armRestL,new Vector3(-30,38,40),new Vector3(-88,-32,-12),new Vector3(-48,-58,-25),
                        preparation,progress,!right||dual?1:.30f);
                    PoseStrike(weaponR,weaponRestR,new Vector3(12,-20,-20),new Vector3(32,20,15),new Vector3(48,32,25),preparation,progress,right||dual?1:.30f);
                    PoseStrike(weaponL,weaponRestL,new Vector3(12,20,20),new Vector3(32,-20,-15),new Vector3(48,-32,-25),preparation,progress,!right||dual?1:.30f);
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
                    if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(
                        identity!=null&&identity.Hero==HeroId.Traveler?-34*preparation-36*kick:-45*preparation-42*kick,
                        0,-18*(preparation+kick));
                    if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(
                        identity!=null&&identity.Hero==HeroId.Traveler?-18*preparation-20*kick:-25*preparation-25*kick,
                        0,20*kick);
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
