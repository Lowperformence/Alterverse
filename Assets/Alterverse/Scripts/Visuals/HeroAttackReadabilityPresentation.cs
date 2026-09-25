using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only layer that makes every hero attack physically readable from the match camera.
    /// It observes existing cooldown state, never applies damage/status/movement, and reuses HeroVfxPool.
    /// HeroVisualMotion remains the base animation owner; this component runs afterwards and adds pose/VFX emphasis.
    /// </summary>
    [DefaultExecutionOrder(1500)]
    public sealed class HeroAttackReadabilityPresentation : MonoBehaviour
    {
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private HeroAbilityExecution abilities;
        private BasicAttack basic;
        private Health health;
        private TeamMember team;
        private HeroVisualMotion motion;

        private Transform root,armL,armR,weaponL,weaponR,gauntletL,gauntletR,orb,muzzle;
        private Vector3 gauntletBaseL,gauntletBaseR;
        private bool partsReady;

        private float previousBasic;
        private readonly float[] previousAbility=new float[4];
        private bool cooldownsPrimed;

        private bool active;
        private int activeSlot=-1; // -1 = basic attack
        private float started,windup,duration;
        private Vector3 visualTarget;
        private int attackSerial;

        private void Awake()
        {
            identity=GetComponent<HeroIdentity>();
            kit=GetComponent<HeroAbilityKit>();
            abilities=GetComponent<HeroAbilityExecution>();
            basic=GetComponent<BasicAttack>();
            health=GetComponent<Health>();
            team=GetComponent<TeamMember>();
            motion=GetComponent<HeroVisualMotion>();
        }

        private void Start()=>PrimeCooldowns();

        private void PrimeCooldowns()
        {
            if(basic==null)basic=GetComponent<BasicAttack>();
            if(abilities==null)abilities=GetComponent<HeroAbilityExecution>();
            if(kit==null)kit=GetComponent<HeroAbilityKit>();
            if(identity==null)identity=GetComponent<HeroIdentity>();
            previousBasic=basic!=null?basic.RemainingCooldown:0f;
            for(int i=0;i<4;i++)previousAbility[i]=abilities!=null?abilities.Remaining(i):0f;
            cooldownsPrimed=true;
        }

        private void Update()
        {
            if(Time.timeScale<=0f||health!=null&&health.IsDead)return;
            if(!cooldownsPrimed){PrimeCooldowns();return;}

            DetectBasic();
            DetectAbilities();
        }

        private void DetectBasic()
        {
            if(basic==null)return;
            float current=basic.RemainingCooldown;
            float cooldown=Mathf.Max(.1f,basic.AttackCooldown);
            // A real attack is the large 0 -> full-cooldown jump. Small network corrections do not retrigger visuals.
            if(current>cooldown*.68f&&previousBasic<cooldown*.22f)
                BeginPresentation(-1,BasicWindup(identity!=null?identity.Hero:HeroId.Veyr));
            previousBasic=current;
        }

        private void DetectAbilities()
        {
            if(abilities==null||kit==null||identity==null)return;
            for(int slot=0;slot<4;slot++)
            {
                float current=abilities.Remaining(slot);
                float cooldown=Mathf.Max(.1f,abilities.Cooldown(slot));
                // Ultimate begins locked at match start; the baseline is primed in Start, so only a later cast jump fires.
                if(current>cooldown*.68f&&previousAbility[slot]<cooldown*.22f)
                {
                    AbilitySpec spec=kit.Spec(slot);
                    float visualWindup=Mathf.Max(spec.Windup,AbilityWindup(identity.Hero,slot));
                    BeginPresentation(slot,visualWindup);
                }
                previousAbility[slot]=current;
            }
        }

        private void BeginPresentation(int slot,float attackWindup)
        {
            if(identity==null)return;
            active=true;activeSlot=slot;started=Time.time;windup=Mathf.Max(.04f,attackWindup);
            duration=windup+(slot<0?.24f:slot==3?.42f:.30f);
            attackSerial++;

            AbilitySpec spec=slot>=0&&kit!=null?kit.Spec(slot):default;
            float range=slot>=0?spec.Range:BasicVisualRange(identity.Hero);
            float radius=slot>=0?spec.Radius:0f;
            visualTarget=ResolveVisualTarget(slot,range,radius);

            EmitPresentationVfx(identity.Hero,slot,visualTarget,windup,spec);
        }

        private void LateUpdate()
        {
            if(!active||identity==null)return;
            if(!partsReady)partsReady=TryCacheParts();
            float elapsed=Time.time-started;
            if(elapsed>=duration){active=false;RestoreTranslatedParts();return;}
            if(!partsReady)return;

            float charge=elapsed<windup?Mathf.SmoothStep(0f,1f,elapsed/Mathf.Max(.001f,windup)):1f;
            float release=elapsed<windup?0f:Mathf.Clamp01((elapsed-windup)/Mathf.Max(.001f,duration-windup));
            float contact=elapsed<windup?charge:1f-Mathf.SmoothStep(0f,1f,release);
            ApplyBodyPresentation(identity.Hero,activeSlot,charge,contact,release);
        }

        private bool TryCacheParts()
        {
            root=transform.Find("DF_VisualRoot");
            if(root==null)return false;
            armL=FindPart("ArmL");armR=FindPart("ArmR");
            weaponL=FindPart("WeaponL");weaponR=FindPart("WeaponR");
            gauntletL=FindPart("GauntletL");gauntletR=FindPart("GauntletR");
            orb=FindPart("Orb");muzzle=FindPart("Muzzle");
            if(gauntletL!=null)gauntletBaseL=gauntletL.localPosition;
            if(gauntletR!=null)gauntletBaseR=gauntletR.localPosition;
            return true;
        }

        private Transform FindPart(string shortName)
        {
            if(root==null)return null;
            string wanted="DF_"+shortName;
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)if(all[i]!=null&&all[i].name==wanted)return all[i];
            return null;
        }

        private void ApplyBodyPresentation(HeroId hero,int slot,float charge,float contact,float release)
        {
            switch(hero)
            {
                case HeroId.Kaelon: PoseKaelon(slot,charge,contact,release);break;
                case HeroId.Rook: PoseRook(slot,charge,contact,release);break;
                case HeroId.Elyra: PoseCaster(slot,charge,contact,true);break;
                case HeroId.Mira: PoseCaster(slot,charge,contact,false);break;
                case HeroId.Nyra: PoseMarksman(slot,charge,contact);break;
                case HeroId.Traveler: PoseTraveler(slot,charge,contact);break;
                case HeroId.Veyr: PoseVeyr(slot,charge,contact);break;
            }
        }

        private void PoseKaelon(int slot,float charge,float contact,float release)
        {
            // Gravity Anchor: the base animation rotates the body 360 degrees; keep the hammer visibly extended
            // through the whole turn so the rotation reads as a real weapon sweep rather than an in-place spin.
            if(slot==2)
            {
                AddRot(armR,-58f*contact,18f*contact,-66f*contact);
                AddRot(weaponR,-22f*contact,0,38f*contact);
                AddRot(armL,-18f*contact,0,28f*contact);
                return;
            }
            // Citadel and Anchor Strike visibly load the hammer overhead/back, then drive it through the contact point.
            float lift=slot==3?charge:charge*.72f;
            AddRot(armR,-82f*lift+105f*(1f-contact),slot==3?0:18f*contact,-28f*lift);
            AddRot(weaponR,-48f*lift+68f*(1f-contact),0,22f*contact);
            if(root!=null&&slot==3)root.localRotation*=Quaternion.Euler(-10f*contact,0,0);
        }

        private void PoseRook(int slot,float charge,float contact,float release)
        {
            bool left=(attackSerial&1)==0;
            Transform fist=left?gauntletL:gauntletR;
            Transform arm=left?armL:armR;

            if(slot==2)
            {
                // Wreckstorm: both fists stay wide while the existing body spin resolves.
                AddRot(armL,-42f*contact,-28f*contact,56f*contact);
                AddRot(armR,-42f*contact,28f*contact,-56f*contact);
                SetGauntletExtension(gauntletL,gauntletBaseL,.24f*contact);
                SetGauntletExtension(gauntletR,gauntletBaseR,.24f*contact);
                return;
            }
            if(slot==3)
            {
                // Overdrive already lifts the render rig in HeroVisualMotion; fists rise during ascent and punch down on landing.
                AddRot(armL,-105f*charge+132f*(1f-contact),0,18f);
                AddRot(armR,-105f*charge+132f*(1f-contact),0,-18f);
                return;
            }
            if(slot==1)
            {
                AddRot(armL,-35f*charge,0,20f*charge);AddRot(armR,-35f*charge,0,-20f*charge);
                return;
            }

            // Basic / Wreck Swing: actual visible fist thrust and snap-back, alternating left/right.
            AddRot(arm,-56f*charge+72f*(1f-contact),left?-12f:12f,left?28f:-28f);
            float extension=.46f*contact;
            if(fist!=null)SetGauntletExtension(fist,left?gauntletBaseL:gauntletBaseR,extension);
            Transform other=left?gauntletR:gauntletL;
            if(other!=null)SetGauntletExtension(other,left?gauntletBaseR:gauntletBaseL,0f);
        }

        private void PoseCaster(int slot,float charge,float contact,bool gravitational)
        {
            float raise=Mathf.Clamp01(charge*.9f+contact*.35f);
            AddRot(armL,-58f*raise,-18f*raise,gravitational?-34f:-18f);
            AddRot(armR,-58f*raise,18f*raise,gravitational?34f:18f);
            if(orb!=null)
            {
                float pulse=Mathf.Sin(Mathf.Clamp01((Time.time-started)/duration)*Mathf.PI);
                orb.localPosition+=Vector3.up*(.12f*raise+.08f*pulse);
            }
            if(root!=null&&slot==3)root.localRotation*=Quaternion.Euler(-8f*charge,0,0);
        }

        private void PoseMarksman(int slot,float charge,float contact)
        {
            // Shoulder locks into the shot, then a visible recoil follows release.
            AddRot(armR,-18f*charge+20f*(1f-contact),0,-8f*charge);
            AddRot(armL,-10f*charge,0,8f*charge);
            AddRot(weaponR,slot==3?-7f*contact:-4f*contact,0,0);
            if(root!=null)root.localRotation*=Quaternion.Euler(2f*(1f-contact),0,0);
        }

        private void PoseTraveler(int slot,float charge,float contact)
        {
            if(slot==1)return; // Rift Step movement already owns the silhouette.
            AddRot(armL,-48f*charge,-16f*charge,-24f*charge);
            AddRot(armR,-48f*charge,16f*charge,24f*charge);
            if(root!=null&&slot==3)root.localRotation*=Quaternion.Euler(-6f*contact,0,0);
        }

        private void PoseVeyr(int slot,float charge,float contact)
        {
            // VeyrAttackPresentation owns the broad chest-height blade polish; this only emphasizes ability commitment.
            if(slot==2)return;
            AddRot(armL,-14f*charge,-22f*contact,-18f*contact);
            AddRot(armR,-14f*charge,22f*contact,18f*contact);
        }

        private void RestoreTranslatedParts()
        {
            if(identity==null||identity.Hero!=HeroId.Rook)return;
            if(gauntletL!=null)gauntletL.localPosition=gauntletBaseL;
            if(gauntletR!=null)gauntletR.localPosition=gauntletBaseR;
        }

        private static void AddRot(Transform t,float x,float y,float z)
        {
            if(t!=null)t.localRotation*=Quaternion.Euler(x,y,z);
        }

        private static void SetGauntletExtension(Transform fist,Vector3 rest,float forward)
        {
            if(fist!=null)fist.localPosition=rest+Vector3.forward*forward;
        }

        private Vector3 ResolveVisualTarget(int slot,float range,float radius)
        {
            Vector3 origin=transform.position;
            if(range<=.01f)return origin;
            bool wantAlly=identity!=null&&identity.Hero==HeroId.Mira&&(slot==1||slot==2);
            Health best=null;float bestScore=float.MaxValue;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(candidate==null||candidate==health||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                TeamMember member=candidate.GetComponent<TeamMember>();
                if(team==null||member==null)continue;
                bool ally=member.Team==team.Team;
                if(ally!=wantAlly)continue;
                Vector3 delta=candidate.transform.position-origin;delta.y=0;
                float distance=delta.magnitude;
                if(distance>range+Mathf.Max(.4f,radius))continue;
                float facing=distance>.01f?Vector3.Dot(transform.forward,delta/distance):1f;
                if(!wantAlly&&facing<-.15f)continue;
                float score=distance*(1.08f-Mathf.Max(0,facing)*.38f);
                if(score<bestScore){bestScore=score;best=candidate;}
            }
            if(best!=null)return best.transform.position;
            return origin+transform.forward*Mathf.Clamp(range*.72f,1.4f,6f);
        }

        private void EmitPresentationVfx(HeroId hero,int slot,Vector3 target,float attackWindup,AbilitySpec spec)
        {
            Color color=StylizedArtDirection.HeroAccent(hero);
            Vector3 origin=ReleasePoint();
            Vector3 ground=target;ground.y=Mathf.Max(.08f,target.y+.08f);
            float impactDelay=Mathf.Max(.02f,attackWindup);

            if(slot>=0)EmitAbilitySignature(slot,ground,impactDelay,spec,color);

            if(slot<0)
            {
                // Existing HeroSkillVfx owns projectile travel; this layer adds only readable release/impact accents.
                HeroVfxPool.Emit(HeroFxShape.Halo,origin,origin,Vector3.one*.30f,color,.16f,HeroFxMotion.Expand,owner:gameObject);
                if(hero==HeroId.Veyr||hero==HeroId.Kaelon)
                    HeroVfxPool.Emit(HeroFxShape.Slash,transform.position+Vector3.up*.42f,Vector3.zero,new Vector3(2.25f,.16f,2.25f),color,.24f,HeroFxMotion.Sweep,Quaternion.LookRotation(transform.forward),gameObject,delay:attackWindup*.55f);
                else if(hero==HeroId.Rook)
                    HeroVfxPool.Emit(HeroFxShape.Burst,ground+Vector3.up*.20f,Vector3.zero,Vector3.one*.58f,color,.18f,HeroFxMotion.Expand,owner:gameObject,delay:attackWindup);
                return;
            }

            switch(hero)
            {
                case HeroId.Kaelon:
                    if(slot==2)
                    {
                        // Multiple weapon arcs around the real rotating hammer make the 360-degree strike readable.
                        for(int i=0;i<4;i++)
                            HeroVfxPool.Emit(HeroFxShape.Slash,transform.position+Vector3.up*.45f,Vector3.zero,new Vector3(3.0f,.18f,3.0f),color,.18f,HeroFxMotion.Sweep,Quaternion.Euler(0,i*90f,0),gameObject,delay:attackWindup*i/4f);
                        HeroVfxPool.Emit(HeroFxShape.Burst,transform.position+Vector3.up*.14f,Vector3.zero,Vector3.one*1.25f,color,.22f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    }
                    else if(slot==3)
                    {
                        HeroVfxPool.Emit(HeroFxShape.Cracks,ground,Vector3.zero,new Vector3(3.6f,.10f,3.6f),color,.34f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome,ground,Vector3.zero,new Vector3(3.8f,1.25f,3.8f),color,.32f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    }
                    else
                        HeroVfxPool.Emit(HeroFxShape.Slash,transform.position+Vector3.up*.48f,Vector3.zero,new Vector3(2.9f,.18f,2.9f),color,.25f,HeroFxMotion.Sweep,Quaternion.LookRotation(transform.forward),gameObject,delay:attackWindup*.55f);
                    break;

                case HeroId.Rook:
                    if(slot==2)
                    {
                        for(int i=0;i<3;i++)HeroVfxPool.Emit(HeroFxShape.Slash,transform.position+Vector3.up*.40f,Vector3.zero,new Vector3(2.8f,.17f,2.8f),color,.18f,HeroFxMotion.Sweep,Quaternion.Euler(0,i*120f,0),gameObject,delay:attackWindup*i/3f);
                        HeroVfxPool.Emit(HeroFxShape.Burst,transform.position+Vector3.up*.12f,Vector3.zero,Vector3.one*1.15f,color,.20f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    }
                    else if(slot==3)
                    {
                        HeroVfxPool.Emit(HeroFxShape.Cracks,transform.position+Vector3.up*.08f,Vector3.zero,new Vector3(3.2f,.08f,3.2f),color,.35f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome,transform.position+Vector3.up*.08f,Vector3.zero,new Vector3(3.25f,1.05f,3.25f),color,.32f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    }
                    else if(slot==0)
                        HeroVfxPool.Emit(HeroFxShape.Burst,ground+Vector3.up*.22f,Vector3.zero,Vector3.one*.72f,color,.20f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    else
                        HeroVfxPool.Emit(HeroFxShape.Halo,transform.position+Vector3.up*.65f,Vector3.zero,Vector3.one*.75f,color,.26f,HeroFxMotion.Expand,owner:gameObject);
                    break;

                case HeroId.Elyra:
                    if(slot==0||slot==3)
                    {
                        // Visible skyfall: the orb reaches the warning center exactly as gameplay windup resolves.
                        float height=slot==3?8.5f:5.7f;
                        float fall=Mathf.Clamp(attackWindup*.82f,.28f,.62f);
                        float delay=Mathf.Max(0f,attackWindup-fall);
                        Vector3 sky=ground+Vector3.up*height;
                        float size=slot==3?.92f:.58f;
                        HeroVfxPool.Emit(HeroFxShape.Orb,sky,ground+Vector3.up*.22f,Vector3.one*size,color,fall,HeroFxMotion.Travel,Quaternion.identity,gameObject,delay:delay);
                        HeroVfxPool.Emit(HeroFxShape.Halo,sky,ground+Vector3.up*.24f,Vector3.one*(size*1.45f),color,fall,HeroFxMotion.Travel,Quaternion.identity,gameObject,delay:delay);
                        HeroVfxPool.Emit(HeroFxShape.Burst,ground+Vector3.up*.20f,Vector3.zero,Vector3.one*(slot==3?1.75f:1.15f),color,.22f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome,ground,Vector3.zero,new Vector3(spec.Radius>0?spec.Radius:2f,.95f,spec.Radius>0?spec.Radius:2f),color,.30f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                        HeroVfxPool.Emit(HeroFxShape.Cracks,ground,Vector3.zero,new Vector3(spec.Radius>0?spec.Radius:2f,.06f,spec.Radius>0?spec.Radius:2f),color,.32f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    }
                    else if(slot==1)
                    {
                        HeroVfxPool.Emit(HeroFxShape.Rift,ground+Vector3.up*.9f,Vector3.zero,new Vector3(1.0f,2.4f,1f),color,.34f,HeroFxMotion.Contract,owner:gameObject,delay:impactDelay*.55f);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome,ground,Vector3.zero,new Vector3(2.5f,.75f,2.5f),color,.28f,HeroFxMotion.Contract,owner:gameObject,delay:impactDelay);
                    }
                    else HeroVfxPool.Emit(HeroFxShape.Rift,transform.position+Vector3.up*.65f,Vector3.zero,new Vector3(.65f,1.25f,.65f),color,.20f,HeroFxMotion.Contract,owner:gameObject);
                    break;

                case HeroId.Veyr:
                    if(slot==2)HeroVfxPool.Emit(HeroFxShape.Rift,transform.position+Vector3.up*.55f,visualTarget+Vector3.up*.55f,new Vector3(.72f,1.15f,.72f),color,.20f,HeroFxMotion.Travel,Quaternion.LookRotation(transform.forward),gameObject);
                    else
                    {
                        HeroVfxPool.Emit(HeroFxShape.Slash,transform.position+Vector3.up*.55f,Vector3.zero,new Vector3(slot==3?3.3f:2.6f,.16f,slot==3?3.3f:2.6f),color,.23f,HeroFxMotion.Sweep,Quaternion.LookRotation(transform.forward),gameObject,delay:attackWindup*.55f);
                        if(slot==3)HeroVfxPool.Emit(HeroFxShape.Rift,ground+Vector3.up*.65f,Vector3.zero,new Vector3(.95f,1.7f,.95f),color,.34f,HeroFxMotion.Contract,owner:gameObject,delay:impactDelay);
                    }
                    break;

                case HeroId.Nyra:
                    HeroVfxPool.Emit(HeroFxShape.Halo,origin,Vector3.zero,Vector3.one*(slot==3?.62f:.38f),color,.18f,HeroFxMotion.Expand,owner:gameObject);
                    if(slot==2)HeroVfxPool.Emit(HeroFxShape.Hex,ground,Vector3.zero,new Vector3(1.45f,.05f,1.45f),color,.28f,HeroFxMotion.Contract,owner:gameObject,delay:impactDelay);
                    else if(slot==3)for(int i=0;i<4;i++)HeroVfxPool.Emit(HeroFxShape.Bolt,origin,ground+Vector3.up*.35f,new Vector3(.16f,.16f,.75f),color,.18f,HeroFxMotion.Travel,Quaternion.LookRotation((ground+Vector3.up*.35f)-origin),gameObject,delay:attackWindup+i*.18f);
                    break;

                case HeroId.Mira:
                    HeroVfxPool.Emit(HeroFxShape.Petals,slot==3?transform.position:ground+Vector3.up*.15f,Vector3.zero,Vector3.one*(slot==3?2.7f:.85f),color,.34f,HeroFxMotion.Expand,owner:gameObject,delay:slot==3?impactDelay:attackWindup*.35f);
                    HeroVfxPool.Emit(HeroFxShape.Halo,origin,Vector3.zero,Vector3.one*.52f,color,.22f,HeroFxMotion.Expand,owner:gameObject);
                    break;

                case HeroId.Traveler:
                    if(slot==0)HeroVfxPool.Emit(HeroFxShape.Bolt,origin,ground+Vector3.up*.35f,new Vector3(.22f,.22f,1.0f),color,.24f,HeroFxMotion.Travel,Quaternion.LookRotation((ground+Vector3.up*.35f)-origin),gameObject,delay:attackWindup*.35f);
                    else if(slot==1)HeroVfxPool.Emit(HeroFxShape.Rift,transform.position+Vector3.up*.6f,visualTarget+Vector3.up*.6f,new Vector3(.75f,1.25f,.75f),color,.22f,HeroFxMotion.Travel,Quaternion.LookRotation(transform.forward),gameObject);
                    else
                    {
                        HeroVfxPool.Emit(HeroFxShape.Runes,ground,Vector3.zero,new Vector3(spec.Radius>0?spec.Radius:2.1f,.05f,spec.Radius>0?spec.Radius:2.1f),color,.36f,HeroFxMotion.Orbit,owner:gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Rift,ground+Vector3.up*.75f,Vector3.zero,new Vector3(.85f,1.55f,.85f),color,.30f,HeroFxMotion.Contract,owner:gameObject,delay:impactDelay);
                        if(slot==3)HeroVfxPool.Emit(HeroFxShape.ShockDome,ground,Vector3.zero,new Vector3(3.2f,.95f,3.2f),color,.32f,HeroFxMotion.Contract,owner:gameObject,delay:impactDelay);
                    }
                    break;
            }
        }

        // A stable silhouette for each button survives the distant match camera and dark terrain.
        // These are presentation-only marks; existing gameplay warnings remain authoritative.
        private void EmitAbilitySignature(int slot,Vector3 ground,float impactDelay,AbilitySpec spec,Color accent)
        {
            Vector3 caster=transform.position+Vector3.up*.11f;
            Color bright=Color.Lerp(accent,Color.white,.48f);bright.a=1f;
            float radius=Mathf.Clamp(spec.Radius>0f?spec.Radius:1.5f,1.4f,3.5f);
            switch(slot)
            {
                case 0: // Fast strike: a directional slash and a compact impact spark.
                    HeroVfxPool.Emit(HeroFxShape.Slash,caster,Vector3.zero,new Vector3(2.4f,.08f,2.4f),bright,.38f,HeroFxMotion.Sweep,Quaternion.LookRotation(transform.forward),gameObject);
                    HeroVfxPool.Emit(HeroFxShape.Burst,ground+Vector3.up*.20f,Vector3.zero,Vector3.one*.92f,bright,.32f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    break;
                case 1: // Utility: angular hexagonal field stays visible through the cast.
                    HeroVfxPool.Emit(HeroFxShape.Hex,caster,Vector3.zero,new Vector3(2.4f,.05f,2.4f),bright,.55f,HeroFxMotion.Expand,owner:gameObject);
                    HeroVfxPool.Emit(HeroFxShape.Hex,ground,Vector3.zero,new Vector3(radius*1.7f,.05f,radius*1.7f),bright,.45f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    break;
                case 2: // Area/control: rotating runes identify the affected location.
                    HeroVfxPool.Emit(HeroFxShape.Runes,ground,Vector3.zero,new Vector3(radius*2f,.06f,radius*2f),bright,Mathf.Max(.65f,impactDelay+.25f),HeroFxMotion.Orbit,owner:gameObject);
                    HeroVfxPool.Emit(HeroFxShape.Ring,ground+Vector3.up*.035f,Vector3.zero,new Vector3(radius*2f,.05f,radius*2f),accent,.44f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    break;
                case 3: // Ultimate: broad warning and a vertical energy dome on release.
                    HeroVfxPool.Emit(HeroFxShape.Runes,ground,Vector3.zero,new Vector3(radius*2.3f,.06f,radius*2.3f),bright,Mathf.Max(.75f,impactDelay+.30f),HeroFxMotion.Orbit,owner:gameObject);
                    HeroVfxPool.Emit(HeroFxShape.ShockDome,ground+Vector3.up*.06f,Vector3.zero,new Vector3(radius*2.2f,1.4f,radius*2.2f),bright,.50f,HeroFxMotion.Expand,owner:gameObject,delay:impactDelay);
                    break;
            }
        }

        private Vector3 ReleasePoint()
        {
            if(motion!=null)return motion.ReleasePoint;
            if(muzzle!=null)return muzzle.position;
            if(orb!=null)return orb.position;
            return transform.position+Vector3.up*.75f+transform.forward*.35f;
        }

        private static float BasicWindup(HeroId hero)=>hero switch
        {
            HeroId.Veyr=>.09f,HeroId.Kaelon=>.16f,HeroId.Nyra=>.08f,HeroId.Elyra=>.14f,
            HeroId.Mira=>.12f,HeroId.Rook=>.13f,HeroId.Traveler=>.10f,_=>.10f
        };

        private static float AbilityWindup(HeroId hero,int slot)
        {
            if(slot==3&&hero==HeroId.Nyra)return .22f;
            return BasicWindup(hero)+.04f;
        }

        private static float BasicVisualRange(HeroId hero)=>hero switch
        {
            HeroId.Nyra=>7.2f,HeroId.Elyra=>6.2f,HeroId.Mira=>5.8f,HeroId.Traveler=>6.3f,
            HeroId.Veyr=>2.3f,HeroId.Kaelon=>2.6f,HeroId.Rook=>2.25f,_=>2.4f
        };
    }
}

