using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// VFX-only attack cues observed from the existing cooldown state.
    /// It observes existing cooldown state, never applies damage/status/movement, and reuses HeroVfxPool.
    /// HeroVisualMotion is the only pose owner. This component never writes to character transforms.
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

        private Transform root,orb,muzzle;

        private float previousBasic;
        private readonly float[] previousAbility=new float[4];
        private bool cooldownsPrimed;

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
            AbilitySpec spec=slot>=0&&kit!=null?kit.Spec(slot):default;
            float range=slot>=0?spec.Range:BasicVisualRange(identity.Hero);
            float radius=slot>=0?spec.Radius:0f;
            Vector3 visualTarget=ResolveVisualTarget(slot,range,radius);

            EmitPresentationVfx(identity.Hero,slot,visualTarget,Mathf.Max(.04f,attackWindup),spec);
        }

        private Transform FindPart(string shortName)
        {
            if(root==null)return null;
            string wanted="DF_"+shortName;
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)if(all[i]!=null&&all[i].name==wanted)return all[i];
            return null;
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
            Color color=hero==HeroId.Mira?new Color(1f,.72f,.28f):hero==HeroId.Traveler?new Color(.68f,.28f,1f):StylizedArtDirection.HeroAccent(hero);
            Vector3 origin=ReleasePoint();
            if(slot>=0)
            {
                // Gameplay VFX already draw the real target, impact and warning. This animation
                // layer only confirms the cast close to the character, avoiding duplicate floor art.
                EmitAbilitySignature(hero,slot,target,attackWindup,spec,color);
                return;
            }

            // Basic attacks get one short weapon-side flash. HeroSkillVfx owns the hit.
            HeroFxShape release=hero==HeroId.Nyra||hero==HeroId.Traveler?HeroFxShape.Bolt:
                hero==HeroId.Mira?HeroFxShape.Seed:hero==HeroId.Elyra?HeroFxShape.Runes:
                hero==HeroId.Veyr?HeroFxShape.Slash:hero==HeroId.Kaelon?HeroFxShape.Hex:HeroFxShape.Burst;
            HeroVfxPool.Emit(release,origin,origin,Vector3.one*.30f,color,.12f,
                HeroFxMotion.Expand,owner:gameObject);
            EmitReleaseDetail(hero,-1,origin,color);
        }

        // A short cast mark confirms the button press; the hero-specific effect above owns the
        // target and impact. This avoids covering every ability with the same giant dome/rune.
        private void EmitAbilitySignature(HeroId hero,int slot,Vector3 ground,float impactDelay,AbilitySpec spec,Color accent)
        {
            Vector3 hand=ReleasePoint();
            Vector3 chest=transform.position+Vector3.up*.58f;
            Vector3 feet=transform.position+Vector3.up*.09f;
            Color bright=Color.Lerp(accent,Color.white,.48f);
            bright.a=1f;
            float scale=slot==3?1.35f:slot==2?1.08f:slot==1?.92f:.82f;
            float life=slot==3?.34f:.22f;
            Quaternion facing=Quaternion.LookRotation(transform.forward);
            HeroFxShape shape;
            Vector3 point;
            HeroFxMotion motion=HeroFxMotion.Expand;
            switch(hero)
            {
                case HeroId.Veyr:
                    shape=slot==0?HeroFxShape.Slash:slot==1?HeroFxShape.Rift:
                        slot==2?HeroFxShape.Halo:HeroFxShape.Shard;
                    point=hand;motion=slot==0||slot==3?HeroFxMotion.Sweep:HeroFxMotion.Contract;break;
                case HeroId.Kaelon:
                    shape=slot==0?HeroFxShape.Hex:slot==1?HeroFxShape.Shield:
                        slot==2?HeroFxShape.Cracks:HeroFxShape.ShockDome;
                    point=slot==1?chest:slot==0?hand:chest;break;
                case HeroId.Nyra:
                    shape=slot==0?HeroFxShape.Bolt:slot==1?HeroFxShape.Burst:
                        slot==2?HeroFxShape.Ring:HeroFxShape.Shard;
                    point=hand;break;
                case HeroId.Elyra:
                    shape=slot==0?HeroFxShape.Orb:slot==1?HeroFxShape.Runes:
                        slot==2?HeroFxShape.Rift:HeroFxShape.ShockDome;
                    point=chest;motion=slot==1?HeroFxMotion.Orbit:HeroFxMotion.Contract;break;
                case HeroId.Mira:
                    shape=slot==0?HeroFxShape.Seed:slot==1?HeroFxShape.Plus:
                        slot==2?HeroFxShape.Shield:HeroFxShape.Petals;
                    point=chest;motion=slot==3?HeroFxMotion.Orbit:HeroFxMotion.Expand;break;
                case HeroId.Rook:
                    shape=slot==0?HeroFxShape.Slash:slot==1?HeroFxShape.Shield:
                        slot==2?HeroFxShape.Cracks:HeroFxShape.Burst;
                    point=chest;break;
                case HeroId.Traveler:
                    shape=slot==0?HeroFxShape.Bolt:slot==1?HeroFxShape.Rift:
                        slot==2?HeroFxShape.Runes:HeroFxShape.Halo;
                    point=hand;motion=HeroFxMotion.Contract;break;
                default: shape=HeroFxShape.Burst;point=hand;break;
            }
            HeroVfxPool.Emit(shape,point,point,
                new Vector3(scale,scale*.52f,scale),bright,life,motion,facing,gameObject);
            EmitReleaseDetail(hero,slot,hand,accent);
            if(slot==3)
                HeroVfxPool.Emit(HeroFxShape.Halo,chest,chest,
                    Vector3.one*.70f,new Color(bright.r,bright.g,bright.b,.72f),
                    .28f,HeroFxMotion.Contract,facing,gameObject);
        }

        // Small secondary accents stay at the weapon or hand. Gameplay VFX own the real
        // target, so this layer cannot paint a second, inaccurate impact zone.
        private void EmitReleaseDetail(HeroId hero,int slot,Vector3 hand,Color accent)
        {
            Vector3 chest=transform.position+Vector3.up*.68f;
            Quaternion facing=Quaternion.LookRotation(transform.forward);
            Color core=Color.Lerp(accent,Color.white,.72f);
            core.a=.86f;
            float power=slot==3?1.25f:slot<0?.74f:1f;
            switch(hero)
            {
                case HeroId.Veyr:
                    // Twin blade cuts meet in front of the chest, independently of the sword rig.
                    for(int side=0;side<2;side++)
                        HeroVfxPool.Emit(HeroFxShape.Slash,chest+transform.forward*.48f,Vector3.zero,
                            new Vector3(1.12f*power,.08f,1.12f*power),side==0?accent:core,.20f,
                            HeroFxMotion.Sweep,facing*Quaternion.Euler(65f,0f,side==0?-38f:38f),gameObject,
                            delay:side*.035f,decorative:true);
                    break;
                case HeroId.Kaelon:
                    HeroVfxPool.Emit(HeroFxShape.Hex,hand,Vector3.zero,Vector3.one*.54f*power,core,.19f,
                        HeroFxMotion.Contract,facing,gameObject,delay:.035f,decorative:true);
                    break;
                case HeroId.Rook:
                    HeroVfxPool.Emit(HeroFxShape.Burst,hand,Vector3.zero,Vector3.one*.46f*power,core,.13f,
                        HeroFxMotion.Expand,facing,gameObject,delay:.045f,decorative:true);
                    break;
                case HeroId.Elyra:
                    HeroVfxPool.Emit(HeroFxShape.Runes,chest,Vector3.zero,Vector3.one*.56f*power,core,.27f,
                        HeroFxMotion.Orbit,facing,gameObject,delay:.025f,decorative:true);
                    break;
                case HeroId.Nyra:
                    HeroVfxPool.Emit(HeroFxShape.Ring,hand,Vector3.zero,Vector3.one*.42f*power,core,.16f,
                        HeroFxMotion.Contract,facing*Quaternion.Euler(90f,0f,0f),gameObject,
                        delay:.025f,decorative:true);
                    break;
                case HeroId.Mira:
                    HeroVfxPool.Emit(HeroFxShape.Petals,hand,Vector3.zero,Vector3.one*.53f*power,core,.27f,
                        HeroFxMotion.Orbit,facing,gameObject,delay:.04f,decorative:true);
                    break;
                case HeroId.Traveler:
                    HeroVfxPool.Emit(HeroFxShape.Rift,hand+transform.forward*.14f,Vector3.zero,
                        new Vector3(.40f*power,.72f*power,.35f*power),core,.25f,
                        HeroFxMotion.Contract,facing,gameObject,delay:.025f,decorative:true);
                    break;
            }
        }

        private Vector3 ReleasePoint()
        {
            if(motion==null)motion=GetComponent<HeroVisualMotion>();
            if(motion!=null)return motion.ReleasePoint;
            if(root==null)root=transform.Find("DF_VisualRoot");
            if(muzzle==null)muzzle=FindPart("Muzzle");
            if(orb==null)orb=FindPart("Orb");
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

