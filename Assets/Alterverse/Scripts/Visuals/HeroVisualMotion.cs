using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    /// <summary>Distance-driven presentation. Does not write the actor's movement or balance values.</summary>
    public sealed class HeroVisualMotion : MonoBehaviour
    {
        private HeroIdentity identity;
        private Health health;
        private HeroLocomotion locomotion;
        private Transform root,legL,legR,bootL,bootR,armL,armR,orbitL,orbitR,wingL,wingR,cape,blade;
        private Quaternion bootRestL,bootRestR,armRestL,armRestR,bladeRest;
        private Vector3 basePosition,lastPosition,bootPositionL,bootPositionR;
        private float previousHealth,hitUntil;
        private float phase,strideWeight,attackUntil,attackDuration,attackWeight,previousSpeed,lean;
        private bool wasDead;
        private int abilitySlot=-1,attackSerial;
        private float windupUntil,empowerUntil,dashUntil;
        private Transform muzzle,weaponR,weaponL,orb;
        private Vector3 rootScale,weaponPositionR,weaponPositionL,orbRest;
        private Quaternion weaponRestR,weaponRestL;
        private Renderer[] energyRenderers;
        private readonly MaterialPropertyBlock energyBlock=new();
        private static readonly int EnergyBoost=Shader.PropertyToID("_EnergyBoost");
        public Vector3 ReleasePoint=>muzzle!=null?muzzle.position:orb!=null?orb.position:weaponR!=null?weaponR.position+transform.forward*.35f:transform.position+Vector3.up*.25f;
        // Stable values can later drive an Animator blend tree without replacing movement logic.
        public float PlanarSpeed { get; private set; }
        public float GaitPhase=>phase;
        public bool Dashing=>locomotion!=null&&locomotion.OwnsMovementFrame;
        public void PulseAttack(bool ultimate)
        {
            abilitySlot=ultimate?3:-1;attackSerial++;
            GetComponent<VoidWingPresentation>()?.Pulse();
            attackDuration=ultimate?.32f:.22f;attackUntil=Time.time+attackDuration;attackWeight=ultimate?1f:.62f;windupUntil=0;
        }
        public void PulseAbility(int slot,float windup)
        {
            PulseAttack(slot==3);abilitySlot=slot;
            windupUntil=Time.time+Mathf.Max(0,windup);attackUntil=windupUntil+attackDuration;
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
            identity=GetComponent<HeroIdentity>();health=GetComponent<Health>();locomotion=GetComponent<HeroLocomotion>();
            root=transform.Find("DF_VisualRoot");if(root==null)return;
            basePosition=root.localPosition;rootScale=root.localScale;lastPosition=transform.position;
            energyRenderers=root.GetComponentsInChildren<Renderer>();
            muzzle=FindPart("Muzzle");weaponR=FindPart("WeaponR");weaponL=FindPart("WeaponL");orb=FindPart("Orb");
            if(weaponR!=null){weaponPositionR=weaponR.localPosition;weaponRestR=weaponR.localRotation;}
            if(weaponL!=null){weaponPositionL=weaponL.localPosition;weaponRestL=weaponL.localRotation;}
            if(orb!=null)orbRest=orb.localPosition;
            legL=root.Find("DF_LegL");legR=root.Find("DF_LegR");bootL=root.Find("DF_BootL");bootR=root.Find("DF_BootR");
            armL=root.Find("DF_ArmL")??root.Find("DF_GauntletL");armR=root.Find("DF_ArmR")??root.Find("DF_GauntletR");
            orbitL=root.Find("DF_OrbitL");orbitR=root.Find("DF_OrbitR");wingL=root.Find("DF_WingL");wingR=root.Find("DF_WingR");cape=root.Find("DF_Cape");blade=root.Find("DF_BladeR");
            if(bootL!=null)bootPositionL=bootL.localPosition;if(bootR!=null)bootPositionR=bootR.localPosition;
            if(health!=null){previousHealth=health.Current;health.Changed+=OnHealthChanged;}
            bootRestL=Rest(bootL);bootRestR=Rest(bootR);armRestL=Rest(armL);armRestR=Rest(armR);bladeRest=Rest(blade);
        }
        private void OnHealthChanged(float current,float max)
        {if(current<previousHealth-.01f)hitUntil=Time.time+.12f;previousHealth=current;}
        private void OnDestroy(){if(health!=null)health.Changed-=OnHealthChanged;}
        private static Quaternion Rest(Transform t)=>t!=null?t.localRotation:Quaternion.identity;
        private static void Swing(Transform t,Quaternion rest,float angle){if(t!=null)t.localRotation=rest*Quaternion.Euler(angle,0,0);}
        private static void PoseLeg(Transform leg,Transform foot,float side)
        {
            if(leg==null||foot==null)return;
            Vector3 hip=new Vector3(side,-.26f,0),ankle=foot.localPosition+Vector3.up*.08f;
            Vector3 axis=ankle-hip;
            leg.localPosition=(hip+ankle)*.5f;
            leg.localRotation=Quaternion.FromToRotation(Vector3.down,axis);
            Vector3 scale=leg.localScale;scale.y=axis.magnitude*.5f;leg.localScale=scale;
        }
        private void PlantFoot(Transform foot,Vector3 rest,Quaternion rotation,float gait,float stride)
        {
            if(foot==null)return;
            float cycle=Mathf.Repeat(gait/(Mathf.PI*2),1);
            bool stance=cycle<.5f;
            float progress=stance?cycle*2:(cycle-.5f)*2;
            // During stance the foot moves backward at the traveled-distance rate.
            float z=(stance?Mathf.Lerp(1,-1,progress):Mathf.Lerp(-1,1,progress))*stride*.25f;
            float lift=stance?0:Mathf.Sin(progress*Mathf.PI)*.12f;
            foot.localPosition=rest+new Vector3(0,lift,z)*strideWeight;
            foot.localRotation=rotation*Quaternion.Euler(stance?0:-Mathf.Sin(progress*Mathf.PI)*12*strideWeight,0,0);
        }
        private void LateUpdate()
        {
            if(root==null||health==null)return;
            Vector3 delta=transform.position-lastPosition;delta.y=0;lastPosition=transform.position;
            if(Time.timeScale<=0){previousSpeed=0;return;}
            float dt=Mathf.Max(Time.deltaTime,.0001f);
            if(health.IsDead){wasDead=true;PlanarSpeed=0;strideWeight=0;attackUntil=windupUntil=dashUntil=empowerUntil=0;root.localScale=rootScale;return;}
            // Recall/respawn is a relocation, not a sprint across the arena.
            if(wasDead||delta.magnitude>Mathf.Max(3,dt*40))
            {wasDead=false;phase=0;strideWeight=previousSpeed=lean=0;root.localPosition=basePosition;root.localRotation=Quaternion.identity;delta=Vector3.zero;}
            HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
            bool heavy=hero==HeroId.Kaelon||hero==HeroId.Rook;
            float travel=delta.magnitude;
            PlanarSpeed=travel/dt;
            float response=1-Mathf.Exp(-24*dt);
            float desired=Dashing?0:Mathf.Clamp01(PlanarSpeed/.8f);
            if(Dashing)strideWeight=0;
            strideWeight=Mathf.Lerp(strideWeight,desired,response);
            // One complete gait cycle per traveled stride, not one per fixed time interval.
            float strideLength=heavy?2.35f:hero==HeroId.Veyr?2.5f:2.1f;
            if(!Dashing)phase=Mathf.Repeat(phase+travel/strideLength*Mathf.PI*2,Mathf.PI*2);
            float step=Mathf.Sin(phase)*(heavy?25:30)*strideWeight;
            PlantFoot(bootL,bootPositionL,bootRestL,phase,strideLength);
            PlantFoot(bootR,bootPositionR,bootRestR,phase+Mathf.PI,strideLength);
            PoseLeg(legL,bootL,bootPositionL.x);PoseLeg(legR,bootR,bootPositionR.x);
            Swing(armL,armRestL,hero==HeroId.Kaelon?0:-step*.45f);Swing(armR,armRestR,hero==HeroId.Nyra||hero==HeroId.Mira?0:step*.45f);
            float acceleration=Mathf.Clamp((PlanarSpeed-previousSpeed)/dt,-30,30);previousSpeed=PlanarSpeed;
            lean=Mathf.Lerp(lean,Mathf.Clamp(acceleration*.1f,-2.5f,3)+Mathf.Min(PlanarSpeed,8)*.35f,1-Mathf.Exp(-16*dt));
            float t=Time.time;
            float idle=Mathf.Sin(t*1.8f)*.012f*(1-strideWeight);
            float bob=(1-Mathf.Cos(phase*2))*.018f*strideWeight*(heavy?.7f:1);
            if(hero==HeroId.Elyra)idle+=Mathf.Sin(t*1.3f)*.018f;
            root.localPosition=basePosition+Vector3.up*(idle+bob);
            if(Time.time<windupUntil&&!HeroCombatStatus.CanAct(gameObject)){windupUntil=attackUntil=0;}
            float preparation=Time.time<windupUntil?1:0;
            float progress=Mathf.Clamp01(1-(attackUntil-Time.time)/attackDuration);
            float kick=Time.time<attackUntil&&preparation==0?Mathf.Cos(progress*Mathf.PI*.5f)*attackWeight:0;
            ApplyAttackPose(hero,kick,preparation,progress);
            float energy=Mathf.Max(kick,Mathf.Max(preparation*.65f,Time.time<empowerUntil?.85f:0));
            if(energyRenderers!=null)foreach(Renderer renderer in energyRenderers)
            {
                if(renderer==null||!renderer.enabled)continue;
                renderer.GetPropertyBlock(energyBlock);energyBlock.SetFloat(EnergyBoost,energy);renderer.SetPropertyBlock(energyBlock);
            }
            bool phaseDash=(Dashing||Time.time<dashUntil)&&(hero==HeroId.Veyr||hero==HeroId.Elyra);
            // Compress the render rig only; collider and movement are untouched.
            root.localScale=Vector3.Scale(rootScale,phaseDash?new Vector3(.55f,.94f,.88f):Vector3.one);
            float recoil=Time.time<hitUntil?Mathf.Sin((hitUntil-Time.time)/.12f*Mathf.PI)*(heavy?1:2):0;
            root.localRotation=Quaternion.Euler((Dashing?10:lean)-7*kick-recoil,0,Mathf.Sin(phase)*strideWeight*(heavy?.8f:1.3f));
            if(orbitL!=null){orbitL.localPosition=new Vector3(-.58f,.28f+Mathf.Sin(t*1.4f)*.08f,.03f);orbitL.localRotation=Quaternion.Euler(38,t*35,-35);}
            if(orbitR!=null){orbitR.localPosition=new Vector3(.58f,.28f-Mathf.Sin(t*1.4f)*.08f,.03f);orbitR.localRotation=Quaternion.Euler(38,-t*35,35);}
            if(orb!=null)orb.localPosition=orbRest+Vector3.up*(Mathf.Sin(t*2)*.025f+preparation*.08f);
            if(wingL!=null)wingL.localRotation=Quaternion.Euler(0,-18,-18-Mathf.Sin(t*2.1f)*4);
            if(wingR!=null)wingR.localRotation=Quaternion.Euler(0,18,18+Mathf.Sin(t*2.1f)*4);
            if(cape!=null)cape.localRotation=Quaternion.Euler(8+Mathf.Min(PlanarSpeed,8)*1.6f,0,8+Mathf.Sin(t*2)*2);
            if(blade!=null)blade.localRotation=bladeRest*Quaternion.Euler(22*strideWeight,0,-12*strideWeight);
        }
        private void ApplyAttackPose(HeroId hero,float kick,float preparation,float progress)
        {
            if(weaponR!=null){weaponR.localPosition=weaponPositionR;weaponR.localRotation=weaponRestR;}
            if(weaponL!=null){weaponL.localPosition=weaponPositionL;weaponL.localRotation=weaponRestL;}
            if(kick<=0&&preparation<=0&&Time.time>=empowerUntil)return;
            float side=attackSerial%2==0?1:-1;
            switch(hero)
            {
                case HeroId.Veyr:
                    if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(-24*preparation-65*kick,55*kick*side,-24*kick);
                    if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(-20*preparation-45*kick,-45*kick*side,24*kick);
                    break;
                case HeroId.Kaelon:
                    if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(-75*preparation+70*kick,0,-12*kick);
                    if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(-18*(preparation+kick),0,0);
                    break;
                case HeroId.Nyra:
                    if(weaponR!=null)weaponR.localPosition=weaponPositionR+Vector3.back*.12f*kick;
                    if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(-7*kick,0,0);
                    break;
                case HeroId.Elyra:
                case HeroId.Mira:
                    if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(-45*preparation-42*kick,0,-18*(preparation+kick));
                    if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(-25*preparation-25*kick,0,20*kick);
                    break;
                case HeroId.Rook:
                    float boost=Time.time<empowerUntil?.22f:0;
                    float slam=abilitySlot==2||abilitySlot==3?1:0;
                    if(armR!=null)armR.localRotation=armRestR*Quaternion.Euler(-85*preparation+(-65+130*slam)*kick-20*boost,25*kick,0);
                    if(armL!=null)armL.localRotation=armRestL*Quaternion.Euler(-65*preparation+(-40+110*slam)*kick-20*boost,-25*kick,0);
                    break;
            }
        }

    }
}

