using Alterverse.Visuals;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    // One permanent base guardian per team. It protects the spawn healing plate,
    // may reposition only inside a short leash, and never respawns after destruction.
    [RequireComponent(typeof(Health),typeof(TeamMember))]
    public sealed class HealingGuardMech : MonoBehaviour
    {
        [SerializeField] private float protectRadius=7f;
        [SerializeField] private float leashRadius=4.5f;
        [SerializeField] private float hardReturnRadius=5.25f;
        [SerializeField] private float returnReleaseRadius=3.15f;
        [SerializeField] private float attackRange=4.6f;
        [SerializeField] private float attackDamage=13f;
        [SerializeField] private float attackCooldown=1.2f;
        [SerializeField] private float moveSpeed=1.55f;
        [SerializeField] private float scanInterval=.22f;

        private Health health,target;
        private int team;
        private Vector3 guardPost,healingPlate;
        private float nextAttack,nextScan;
        private Color energy;
        private bool forcedReturn;

        public static GameObject Spawn(int team,Vector3 healingPlate)
        {
            bool zero=team==0;
            Color energy=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color dark=zero?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark;

            // Offset beside the plate so the healing plus and arriving heroes stay unobstructed.
            Vector3 spawn=healingPlate+new Vector3(0,.86f,zero?2.75f:-2.75f);
            GameObject mech=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mech.name=zero?"DF_ZeroHealingGuardMech":"DF_LumenHealingGuardMech";
            mech.transform.position=spawn;
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)mech.transform.SetParent(world.transform,true);

            mech.transform.localScale=new Vector3(1.02f,1.02f,1.02f);
            Renderer renderer=mech.GetComponent<Renderer>();
            // Keep the primitive only as the gameplay collider host. The visible body is fully modular below.
            if(renderer!=null)renderer.enabled=false;

            CapsuleCollider collider=mech.GetComponent<CapsuleCollider>();
            if(collider!=null){collider.radius=.72f;collider.height=2.05f;}

            Health hp=mech.AddComponent<Health>();
            hp.Configure(650,true);
            mech.AddComponent<TeamMember>().Initialize(team);
            HealingGuardMech guard=mech.AddComponent<HealingGuardMech>();
            guard.Initialize(team,spawn,healingPlate,energy);

            if(ArenaVariantRuntime.IsGreenPlanet)GreenAlienMechVisual.Build(mech.transform,team,true);
            else if(ArenaVariantRuntime.IsMars)MarsStoneGolemMechVisual.Build(mech.transform,team,true);
            else BuildRobotVisual(mech.transform,zero,dark,energy);

            return mech;
        }

        public static void BuildRobotVisual(Transform root,int team)
        {
            bool zero=Mathf.Clamp(team,0,1)==0;
            Color energy=zero?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color dark=zero?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark;
            BuildRobotVisual(root,zero,dark,energy);
        }

        private static void BuildRobotVisual(Transform root,bool zero,Color dark,Color energy)
        {
            Color armor=Color.Lerp(dark,new Color(.34f,.37f,.42f,1f),.48f);
            Color joint=Color.Lerp(dark,Color.black,.18f);

            // Torso hierarchy: chest -> abdomen -> pelvis. This removes the previous single-block silhouette.
            CreatePart(root,"DF_GuardChestShell",PrimitiveType.Cube,new Vector3(0,.78f,0),new Vector3(1.78f,.82f,1.02f),armor,Quaternion.Euler(zero?0f:2f,0,zero?-4f:0f));
            CreatePart(root,"DF_GuardChestPlate",PrimitiveType.Cube,new Vector3(0,.80f,.55f),new Vector3(1.18f,.52f,.16f),dark,Quaternion.identity);
            CreateEnergyPart(root,"DF_GuardCore",PrimitiveType.Cylinder,new Vector3(0,.78f,.66f),new Vector3(.34f,.12f,.34f),energy,Quaternion.Euler(90,0,0));
            CreatePart(root,"DF_GuardAbdomen",PrimitiveType.Cube,new Vector3(0,.34f,.02f),new Vector3(.78f,.34f,.64f),joint,Quaternion.identity);
            CreatePart(root,"DF_GuardPelvis",PrimitiveType.Cube,new Vector3(0,.05f,0),new Vector3(1.18f,.30f,.74f),armor,Quaternion.identity);

            // Head and neck: small sensor head with a clearly readable forward-facing glowing visor.
            CreatePart(root,"DF_GuardNeck",PrimitiveType.Cylinder,new Vector3(0,1.25f,0),new Vector3(.22f,.18f,.22f),joint,Quaternion.identity);
            CreatePart(root,"DF_GuardSensorHead",PrimitiveType.Cube,new Vector3(0,1.50f,.02f),new Vector3(.78f,.42f,.58f),armor,Quaternion.Euler(0,zero?-5f:0,0));
            CreateEnergyPart(root,"DF_GuardVisor",PrimitiveType.Cube,new Vector3(0,1.51f,.34f),new Vector3(.58f,.13f,.09f),energy,Quaternion.identity);
            CreatePart(root,"DF_GuardHeadCowl",PrimitiveType.Cube,new Vector3(0,1.75f,-.02f),new Vector3(.46f,.16f,.48f),dark,Quaternion.Euler(zero?0:0,0,zero?-8f:0));
            CreateEnergyPart(root,"DF_GuardSensorEye",PrimitiveType.Sphere,new Vector3(zero?.18f:0f,1.51f,.405f),new Vector3(.10f,.10f,.08f),energy,Quaternion.identity);

            // Shoulders and short heavy arms stay visibly detached from the torso.
            BuildArm(root,-1f,zero,dark,armor,joint,energy);
            BuildArm(root,1f,zero,dark,armor,joint,energy);

            // True articulated legs: thigh, knee joint, shin and broad foot plate.
            BuildLeg(root,-1f,zero,dark,armor,joint);
            BuildLeg(root,1f,zero,dark,armor,joint);

            // Back module gives the robot a functional defensive profile without reading as another torso block.
            CreatePart(root,"DF_GuardBackReactor",PrimitiveType.Cube,new Vector3(0,.80f,-.62f),new Vector3(.72f,.62f,.24f),dark,Quaternion.identity);
            CreateEnergyPart(root,"DF_GuardBackStatus",PrimitiveType.Cube,new Vector3(0,.89f,-.76f),new Vector3(.30f,.20f,.06f),energy,Quaternion.identity);
            if(zero)
                CreatePart(root,"DF_GuardAntenna",PrimitiveType.Cylinder,new Vector3(.28f,2.00f,-.04f),new Vector3(.055f,.42f,.055f),armor,Quaternion.Euler(0,0,-12f));
            else
            {
                CreatePart(root,"DF_GuardAntennaL",PrimitiveType.Cylinder,new Vector3(-.18f,1.96f,-.04f),new Vector3(.04f,.32f,.04f),armor,Quaternion.Euler(0,0,5f));
                CreatePart(root,"DF_GuardAntennaR",PrimitiveType.Cylinder,new Vector3(.18f,1.96f,-.04f),new Vector3(.04f,.32f,.04f),armor,Quaternion.Euler(0,0,-5f));
            }
        }

        private static void BuildArm(Transform root,float side,bool zero,Color dark,Color armor,Color joint,Color energy)
        {
            float asym=zero&&side<0?-8f:zero&&side>0?5f:0f;
            CreatePart(root,side<0?"DF_GuardShoulderL":"DF_GuardShoulderR",PrimitiveType.Sphere,new Vector3(side*1.02f,.84f,0),new Vector3(.52f,.56f,.62f),joint,Quaternion.identity);
            CreatePart(root,side<0?"DF_GuardShoulderArmorL":"DF_GuardShoulderArmorR",PrimitiveType.Cube,new Vector3(side*1.16f,.88f,.02f),new Vector3(.44f,.34f,.70f),armor,Quaternion.Euler(0,0,side*(zero?7f:2f)));
            CreatePart(root,side<0?"DF_GuardUpperArmL":"DF_GuardUpperArmR",PrimitiveType.Cube,new Vector3(side*1.22f,.47f,.02f),new Vector3(.38f,.72f,.48f),armor,Quaternion.Euler(0,0,side*(10f+asym)));
            CreatePart(root,side<0?"DF_GuardElbowL":"DF_GuardElbowR",PrimitiveType.Sphere,new Vector3(side*1.32f,.13f,.06f),new Vector3(.28f,.28f,.28f),joint,Quaternion.identity);
            CreatePart(root,side<0?"DF_GuardForearmL":"DF_GuardForearmR",PrimitiveType.Cube,new Vector3(side*1.37f,-.10f,.18f),new Vector3(.46f,.54f,.62f),dark,Quaternion.Euler(side*4f,0,side*(4f+asym*.4f)));
            CreateEnergyPart(root,side<0?"DF_GuardShieldEmitterL":"DF_GuardShieldEmitterR",PrimitiveType.Cylinder,new Vector3(side*1.48f,-.08f,.48f),new Vector3(.22f,.12f,.22f),energy,Quaternion.Euler(90,0,0));
        }

        private static void BuildLeg(Transform root,float side,bool zero,Color dark,Color armor,Color joint)
        {
            float toe=zero&&side<0?.10f:0f;
            CreatePart(root,side<0?"DF_GuardHipJointL":"DF_GuardHipJointR",PrimitiveType.Sphere,new Vector3(side*.43f,-.05f,0),new Vector3(.34f,.34f,.34f),joint,Quaternion.identity);
            CreatePart(root,side<0?"DF_GuardThighL":"DF_GuardThighR",PrimitiveType.Cube,new Vector3(side*.48f,-.25f,.02f),new Vector3(.42f,.40f,.50f),armor,Quaternion.Euler(0,0,side*(zero?3f:1f)));
            CreatePart(root,side<0?"DF_GuardKneeL":"DF_GuardKneeR",PrimitiveType.Sphere,new Vector3(side*.50f,-.47f,.09f),new Vector3(.32f,.28f,.32f),joint,Quaternion.identity);
            CreatePart(root,side<0?"DF_GuardShinL":"DF_GuardShinR",PrimitiveType.Cube,new Vector3(side*.52f,-.64f,.04f),new Vector3(.46f,.36f,.52f),dark,Quaternion.Euler(0,0,side*(zero?-2f:0f)));
            CreatePart(root,side<0?"DF_GuardFootL":"DF_GuardFootR",PrimitiveType.Cube,new Vector3(side*.54f,-.82f,.20f+toe),new Vector3(.58f,.20f,.86f),armor,Quaternion.identity);
        }

        private void Initialize(int assignedTeam,Vector3 post,Vector3 plate,Color accent)
        {
            team=Mathf.Clamp(assignedTeam,0,1);
            guardPost=post;
            healingPlate=plate;
            energy=accent;
            health=GetComponent<Health>();
            nextAttack=Time.time+.4f;
        }

        private void Update()
        {
            if(Time.timeScale<=0f||health==null||health.IsDead)return;

            Vector3 plateDelta=transform.position-healingPlate;plateDelta.y=0;
            if(plateDelta.sqrMagnitude>hardReturnRadius*hardReturnRadius)forcedReturn=true;
            if(forcedReturn)
            {
                target=null;
                MoveTowards(guardPost);
                plateDelta=transform.position-healingPlate;plateDelta.y=0;
                if(plateDelta.sqrMagnitude<=returnReleaseRadius*returnReleaseRadius)forcedReturn=false;
                return;
            }

            if(target!=null&&!ValidTarget(target))target=null;
            if(Time.time>=nextScan)
            {
                nextScan=Time.time+scanInterval;
                target=FindThreat();
            }

            if(target==null)
            {
                ReturnHome();
                return;
            }

            Vector3 delta=target.transform.position-transform.position;
            delta.y=0;
            if(delta.sqrMagnitude<=attackRange*attackRange)
            {
                Face(delta);
                if(Time.time>=nextAttack)
                {
                    nextAttack=Time.time+attackCooldown;
                    target.TakeDamage(attackDamage,gameObject);
                    PrototypeVfx.ProjectileLine(transform.position+Vector3.up*.9f,target.transform.position+Vector3.up*.65f,energy,.12f);
                }
                return;
            }

            MoveWithinLeash(target.transform.position);
        }

        private Health FindThreat()
        {
            Health best=null;
            float bestScore=float.MaxValue;
            float protectSqr=protectRadius*protectRadius;

            for(int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(candidate==null||candidate==health||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                if(!Enemy(candidate))continue;

                Vector3 fromPlate=candidate.transform.position-healingPlate;fromPlate.y=0;
                if(fromPlate.sqrMagnitude>protectSqr)continue;

                // Enemies closest to the healing plate are the highest protection priority.
                float score=fromPlate.sqrMagnitude;
                if(candidate.GetComponent<Alterverse.Characters.HeroIdentity>()!=null)score*=.72f;
                if(score<bestScore){bestScore=score;best=candidate;}
            }
            return best;
        }

        private bool ValidTarget(Health candidate)
        {
            if(candidate==null||candidate.IsDead||!candidate.isActiveAndEnabled||!Enemy(candidate))return false;
            Vector3 d=candidate.transform.position-healingPlate;d.y=0;
            return d.sqrMagnitude<=protectRadius*protectRadius*1.15f;
        }

        private bool Enemy(Health candidate)
        {
            TeamMember member=candidate.GetComponent<TeamMember>();
            if(member!=null)return member.Team!=team;
            LaneMinion minion=candidate.GetComponent<LaneMinion>();
            return minion!=null&&minion.Team!=team;
        }

        private void MoveWithinLeash(Vector3 destination)
        {
            Vector3 desired=destination;desired.y=guardPost.y;
            Vector3 fromPlate=desired-healingPlate;fromPlate.y=0;
            if(fromPlate.sqrMagnitude>leashRadius*leashRadius)
                desired=healingPlate+fromPlate.normalized*leashRadius;
            desired.y=guardPost.y;
            MoveTowards(desired);
        }

        private void ReturnHome()
        {
            Vector3 delta=guardPost-transform.position;delta.y=0;
            if(delta.sqrMagnitude>.12f)MoveTowards(guardPost);
        }

        private void MoveTowards(Vector3 point)
        {
            Vector3 direction=point-transform.position;direction.y=0;
            if(direction.sqrMagnitude<.04f)return;
            float distance=direction.magnitude;direction/=distance;
            transform.position+=direction*Mathf.Min(moveSpeed*Time.deltaTime,distance);
            Face(direction);
        }

        private void Face(Vector3 direction)
        {
            direction.y=0;
            if(direction.sqrMagnitude>.001f)
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),7f*Time.deltaTime);
        }

        private static void CreatePart(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color,Quaternion rotation)
        {
            GameObject part=GameObject.CreatePrimitive(type);
            part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=pos;part.transform.localRotation=rotation;part.transform.localScale=scale;
            Collider collider=part.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
            Renderer renderer=part.GetComponent<Renderer>();if(renderer!=null){renderer.material.color=color;renderer.shadowCastingMode=ShadowCastingMode.On;}
        }

        private static void CreateEnergyPart(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color,Quaternion rotation)
        {
            GameObject part=GameObject.CreatePrimitive(type);
            part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=pos;part.transform.localRotation=rotation;part.transform.localScale=scale;
            Collider collider=part.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
            Renderer renderer=part.GetComponent<Renderer>();
            if(renderer!=null)
            {
                renderer.material.color=color;renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(renderer.material.HasProperty("_EmissionColor")){renderer.material.EnableKeyword("_EMISSION");renderer.material.SetColor("_EmissionColor",color*2.6f);}
            }
        }
    }
}
