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
        private LaneUnitVisualMotion visualMotion;
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
            hp.Configure(520f,true);
            mech.AddComponent<TeamMember>().Initialize(team);
            HealingGuardMech guard=mech.AddComponent<HealingGuardMech>();
            guard.Initialize(team,spawn,healingPlate,energy);

            if(ArenaVariantRuntime.IsGreenPlanet)GreenAlienMechVisual.Build(mech.transform,team,true);
            else if(FrontierPlanetLayout.Active)PlanetSentinelVisual.Build(mech.transform,team,false);
            else if(ArenaVariantRuntime.IsMars)MarsStoneGolemMechVisual.Build(mech.transform,team,true);
            else if(ArenaVariantRuntime.IsNeptune)NeptuneDiverMechVisual.Build(mech.transform,team);
            else BuildRobotVisual(mech.transform,zero,dark,energy);
            if(!FrontierPlanetLayout.Active)mech.AddComponent<LaneUnitVisualMotion>().ConfigureRobot();

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
            // Heavy lunar security mech inspired by the supplied military reference:
            // olive slab armor, dark exposed mechanics, compact sensor head, backpack pods,
            // oversized forearms and armored shins. Keep team identity in the emissive accents.
            Color olive=new(.19f,.24f,.19f,1f);
            Color oliveLight=new(.30f,.34f,.28f,1f);
            Color oliveDark=new(.105f,.135f,.115f,1f);
            Color joint=new(.045f,.055f,.055f,1f);
            Color trim=new(.16f,.18f,.17f,1f);

            // Narrow waist and broad upper body create a true biped-mech silhouette.
            CreatePart(root,"DF_GuardWaistCore",PrimitiveType.Cylinder,new Vector3(0,.26f,-.03f),new Vector3(.48f,.18f,.48f),joint,Quaternion.identity);
            CreatePart(root,"DF_GuardPelvisCore",PrimitiveType.Cube,new Vector3(0,.02f,0),new Vector3(.92f,.34f,.66f),oliveDark,Quaternion.identity);
            CreatePart(root,"DF_GuardHipArmorL",PrimitiveType.Cube,new Vector3(-.48f,.08f,.02f),new Vector3(.36f,.42f,.58f),olive,Quaternion.Euler(0,-5,-6));
            CreatePart(root,"DF_GuardHipArmorR",PrimitiveType.Cube,new Vector3(.48f,.08f,.02f),new Vector3(.36f,.42f,.58f),olive,Quaternion.Euler(0,5,6));

            // Main torso: dark inner chassis with layered green plates.
            CreatePart(root,"DF_GuardTorsoCore",PrimitiveType.Cube,new Vector3(0,.83f,-.05f),new Vector3(1.46f,.78f,.82f),joint,Quaternion.identity);
            CreatePart(root,"DF_GuardChestShell",PrimitiveType.Cube,new Vector3(0,.90f,.06f),new Vector3(1.62f,.76f,.88f),olive,Quaternion.Euler(-3,0,0));
            CreatePart(root,"DF_GuardChestPlate",PrimitiveType.Cube,new Vector3(0,.90f,.54f),new Vector3(1.18f,.48f,.13f),oliveLight,Quaternion.Euler(-3,0,0));
            CreatePart(root,"DF_GuardChestInset",PrimitiveType.Cube,new Vector3(0,.73f,.62f),new Vector3(.62f,.18f,.055f),oliveDark,Quaternion.identity);
            CreateEnergyPart(root,"DF_GuardChestStripL",PrimitiveType.Cube,new Vector3(-.24f,.76f,.655f),new Vector3(.16f,.065f,.03f),energy,Quaternion.identity);
            CreateEnergyPart(root,"DF_GuardChestStripR",PrimitiveType.Cube,new Vector3(.24f,.76f,.655f),new Vector3(.16f,.065f,.03f),energy,Quaternion.identity);
            CreatePart(root,"DF_GuardChestPanelL",PrimitiveType.Cube,new Vector3(-.56f,.98f,.52f),new Vector3(.30f,.24f,.08f),oliveDark,Quaternion.Euler(-2,-7,-4));
            CreatePart(root,"DF_GuardChestPanelR",PrimitiveType.Cube,new Vector3(.56f,.98f,.52f),new Vector3(.30f,.24f,.08f),oliveDark,Quaternion.Euler(-2,7,4));

            // Compact sensor head, tucked into the chest like the reference.
            CreatePart(root,"DF_GuardNeck",PrimitiveType.Cylinder,new Vector3(0,1.34f,-.02f),new Vector3(.20f,.16f,.20f),joint,Quaternion.identity);
            CreatePart(root,"DF_GuardSensorHeadCore",PrimitiveType.Cube,new Vector3(0,1.55f,.05f),new Vector3(.70f,.40f,.54f),oliveDark,Quaternion.identity);
            CreatePart(root,"DF_GuardSensorHeadArmor",PrimitiveType.Cube,new Vector3(0,1.61f,.10f),new Vector3(.76f,.34f,.48f),olive,Quaternion.Euler(-5,0,0));
            CreateEnergyPart(root,"DF_GuardVisor",PrimitiveType.Cube,new Vector3(0,1.52f,.39f),new Vector3(.48f,.09f,.055f),energy,Quaternion.identity);
            CreatePart(root,"DF_GuardBrow",PrimitiveType.Cube,new Vector3(0,1.72f,.25f),new Vector3(.58f,.10f,.24f),oliveLight,Quaternion.Euler(-8,0,0));
            CreateEnergyPart(root,"DF_GuardSensorEyeL",PrimitiveType.Sphere,new Vector3(-.21f,1.55f,.42f),new Vector3(.075f,.075f,.055f),energy,Quaternion.identity);
            CreateEnergyPart(root,"DF_GuardSensorEyeR",PrimitiveType.Sphere,new Vector3(.21f,1.55f,.42f),new Vector3(.075f,.075f,.055f),energy,Quaternion.identity);

            BuildArm(root,-1f,zero,oliveDark,olive,joint,energy);
            BuildArm(root,1f,zero,oliveDark,olive,joint,energy);
            BuildLeg(root,-1f,zero,oliveDark,olive,joint);
            BuildLeg(root,1f,zero,oliveDark,olive,joint);

            // Dual backpack/sensor pods and antenna make the silhouette immediately different.
            CreatePart(root,"DF_GuardBackpack",PrimitiveType.Cube,new Vector3(0,.98f,-.62f),new Vector3(.90f,.70f,.34f),joint,Quaternion.identity);
            CreatePart(root,"DF_GuardPodL",PrimitiveType.Cube,new Vector3(-.50f,1.38f,-.49f),new Vector3(.42f,.42f,.42f),olive,Quaternion.Euler(-3,-4,-2));
            CreatePart(root,"DF_GuardPodR",PrimitiveType.Cube,new Vector3(.50f,1.38f,-.49f),new Vector3(.42f,.42f,.42f),olive,Quaternion.Euler(-3,4,2));
            CreateEnergyPart(root,"DF_GuardPodLensL",PrimitiveType.Cube,new Vector3(-.50f,1.37f,-.26f),new Vector3(.18f,.10f,.04f),energy,Quaternion.identity);
            CreateEnergyPart(root,"DF_GuardPodLensR",PrimitiveType.Cube,new Vector3(.50f,1.37f,-.26f),new Vector3(.18f,.10f,.04f),energy,Quaternion.identity);
            CreatePart(root,"DF_GuardAntennaMast",PrimitiveType.Cylinder,new Vector3(.52f,1.96f,-.48f),new Vector3(.028f,.48f,.028f),trim,Quaternion.Euler(0,0,-4));
            CreateEnergyPart(root,"DF_GuardAntennaTip",PrimitiveType.Sphere,new Vector3(.56f,2.45f,-.48f),new Vector3(.060f,.060f,.060f),energy,Quaternion.identity);

            // Small hard-surface details visible from the gameplay camera.
            for(int i=-1;i<=1;i++)
            {
                float x=i*.31f;
                CreatePart(root,"DF_GuardChestBolt_"+i,PrimitiveType.Cylinder,new Vector3(x,.58f,.60f),
                    new Vector3(.040f,.020f,.040f),trim,Quaternion.Euler(90,0,0));
            }
            CreatePart(root,"DF_GuardIDPlate",PrimitiveType.Cube,new Vector3(-.46f,1.10f,.56f),new Vector3(.24f,.11f,.035f),zero?new Color(.20f,.24f,.22f):new Color(.24f,.22f,.20f),Quaternion.identity);
            // Attached service panels inherit the existing articulated part transforms.
            Transform chestPanel=root.Find("DF_GuardChestPlate"),pack=root.Find("DF_GuardBackpack");
            if(chestPanel!=null)for(int i=0;i<5;i++)
                CreatePart(chestPanel,"DF_ServiceVent_"+i,PrimitiveType.Cube,new Vector3(-.28f+i*.14f,-.12f,.54f),new Vector3(.045f,.28f,.08f),joint,Quaternion.Euler(0,0,-12));
            if(pack!=null)for(int i=0;i<4;i++)
                CreatePart(pack,"DF_BackCoolingFin_"+i,PrimitiveType.Cube,new Vector3(0,-.30f+i*.19f,-.55f),new Vector3(.76f,.06f,.16f),trim,Quaternion.identity);
            foreach(string side in new[]{"L","R"})
            {
                Transform forearm=root.Find("DF_GuardForearmArmor"+side);
                if(forearm==null)continue;
                for(int i=0;i<3;i++)CreatePart(forearm,"DF_InstrumentRib_"+side+i,PrimitiveType.Cube,new Vector3(0,-.24f+i*.22f,.53f),new Vector3(.62f,.055f,.08f),trim,Quaternion.identity);
            }
        }

        private static void BuildArm(Transform root,float side,bool zero,Color dark,Color armor,Color joint,Color energy)
        {
            bool left=side<0f;
            // Oversized shoulder box + slab forearm follows the supplied military-mech reference.
            CreatePart(root,left?"DF_GuardShoulderCoreL":"DF_GuardShoulderCoreR",PrimitiveType.Sphere,
                new Vector3(side*1.02f,.96f,.00f),new Vector3(.46f,.42f,.48f),joint,Quaternion.identity);
            CreatePart(root,left?"DF_GuardShoulderArmorL":"DF_GuardShoulderArmorR",PrimitiveType.Cube,
                new Vector3(side*1.17f,1.00f,.04f),new Vector3(.50f,.46f,.72f),armor,Quaternion.Euler(0,0,side*5f));
            CreatePart(root,left?"DF_GuardShoulderCapL":"DF_GuardShoulderCapR",PrimitiveType.Cube,
                new Vector3(side*1.23f,1.10f,.09f),new Vector3(.24f,.18f,.54f),new Color(.30f,.34f,.28f),Quaternion.Euler(0,0,side*7f));

            CreatePart(root,left?"DF_GuardUpperArmL":"DF_GuardUpperArmR",PrimitiveType.Capsule,
                new Vector3(side*1.20f,.55f,.02f),new Vector3(.26f,.54f,.28f),dark,Quaternion.Euler(0,0,side*4f));
            CreatePart(root,left?"DF_GuardElbowL":"DF_GuardElbowR",PrimitiveType.Sphere,
                new Vector3(side*1.25f,.20f,.08f),new Vector3(.25f,.24f,.25f),joint,Quaternion.identity);

            CreatePart(root,left?"DF_GuardForearmCoreL":"DF_GuardForearmCoreR",PrimitiveType.Cube,
                new Vector3(side*1.28f,-.09f,.10f),new Vector3(.38f,.60f,.40f),dark,Quaternion.Euler(0,0,side*2f));
            CreatePart(root,left?"DF_GuardForearmArmorL":"DF_GuardForearmArmorR",PrimitiveType.Cube,
                new Vector3(side*1.34f,-.06f,.27f),new Vector3(.50f,.68f,.38f),armor,Quaternion.Euler(-3,0,side*3f));
            CreatePart(root,left?"DF_GuardForearmPanelL":"DF_GuardForearmPanelR",PrimitiveType.Cube,
                new Vector3(side*1.35f,-.05f,.49f),new Vector3(.32f,.44f,.055f),new Color(.30f,.34f,.28f),Quaternion.identity);
            CreateEnergyPart(root,left?"DF_GuardArmStatusL":"DF_GuardArmStatusR",PrimitiveType.Cube,
                new Vector3(side*1.35f,-.25f,.525f),new Vector3(.12f,.055f,.025f),energy,Quaternion.identity);

            CreatePart(root,left?"DF_GuardWristL":"DF_GuardWristR",PrimitiveType.Cylinder,
                new Vector3(side*1.31f,-.48f,.16f),new Vector3(.15f,.11f,.15f),joint,Quaternion.identity);
            CreatePart(root,left?"DF_GuardHandL":"DF_GuardHandR",PrimitiveType.Cube,
                new Vector3(side*1.31f,-.64f,.20f),new Vector3(.30f,.24f,.34f),dark,Quaternion.Euler(-4,0,0));
            for(int finger=-1;finger<=1;finger++)
                CreatePart(root,(left?"DF_GuardFingerL_":"DF_GuardFingerR_")+finger,PrimitiveType.Cube,
                    new Vector3(side*(1.31f+finger*.07f),-.78f,.31f),new Vector3(.070f,.18f,.085f),joint,Quaternion.Euler(-12,0,0));
        }

        private static void BuildLeg(Transform root,float side,bool zero,Color dark,Color armor,Color joint)
        {
            bool left=side<0f;
            CreatePart(root,left?"DF_GuardHipJointL":"DF_GuardHipJointR",PrimitiveType.Sphere,
                new Vector3(side*.44f,-.06f,0),new Vector3(.34f,.34f,.34f),joint,Quaternion.identity);
            CreatePart(root,left?"DF_GuardThighCoreL":"DF_GuardThighCoreR",PrimitiveType.Capsule,
                new Vector3(side*.46f,-.35f,.00f),new Vector3(.30f,.52f,.32f),dark,Quaternion.identity);
            CreatePart(root,left?"DF_GuardThighArmorL":"DF_GuardThighArmorR",PrimitiveType.Cube,
                new Vector3(side*.49f,-.33f,.21f),new Vector3(.46f,.54f,.38f),armor,Quaternion.Euler(-3,0,side*2f));

            CreatePart(root,left?"DF_GuardKneeL":"DF_GuardKneeR",PrimitiveType.Sphere,
                new Vector3(side*.50f,-.69f,.10f),new Vector3(.28f,.25f,.29f),joint,Quaternion.identity);
            CreatePart(root,left?"DF_GuardKneePlateL":"DF_GuardKneePlateR",PrimitiveType.Cube,
                new Vector3(side*.50f,-.68f,.31f),new Vector3(.30f,.18f,.16f),new Color(.30f,.34f,.28f),Quaternion.Euler(-7,0,0));

            CreatePart(root,left?"DF_GuardShinCoreL":"DF_GuardShinCoreR",PrimitiveType.Capsule,
                new Vector3(side*.52f,-1.00f,.08f),new Vector3(.29f,.52f,.32f),dark,Quaternion.identity);
            CreatePart(root,left?"DF_GuardShinArmorL":"DF_GuardShinArmorR",PrimitiveType.Cube,
                new Vector3(side*.53f,-1.00f,.29f),new Vector3(.50f,.60f,.40f),armor,Quaternion.Euler(-5,0,side*2f));
            CreatePart(root,left?"DF_GuardShinSideL":"DF_GuardShinSideR",PrimitiveType.Cube,
                new Vector3(side*.72f,-1.00f,.08f),new Vector3(.15f,.46f,.34f),new Color(.105f,.135f,.115f),Quaternion.identity);

            CreatePart(root,left?"DF_GuardAnkleL":"DF_GuardAnkleR",PrimitiveType.Cylinder,
                new Vector3(side*.53f,-1.34f,.17f),new Vector3(.16f,.11f,.16f),joint,Quaternion.identity);
            CreatePart(root,left?"DF_GuardFootCoreL":"DF_GuardFootCoreR",PrimitiveType.Cube,
                new Vector3(side*.54f,-1.48f,.34f),new Vector3(.58f,.22f,.84f),dark,Quaternion.identity);
            CreatePart(root,left?"DF_GuardFootArmorL":"DF_GuardFootArmorR",PrimitiveType.Cube,
                new Vector3(side*.54f,-1.44f,.58f),new Vector3(.56f,.18f,.46f),armor,Quaternion.Euler(-10,0,0));
            CreatePart(root,left?"DF_GuardHeelL":"DF_GuardHeelR",PrimitiveType.Cube,
                new Vector3(side*.54f,-1.46f,-.08f),new Vector3(.42f,.18f,.34f),joint,Quaternion.identity);
        }

        private void Initialize(int assignedTeam,Vector3 post,Vector3 plate,Color accent)
        {
            gameObject.layer=GameplayCollision.Units;
            team=Mathf.Clamp(assignedTeam,0,1);
            guardPost=post;
            healingPlate=plate;
            energy=accent;
            health=GetComponent<Health>();
            nextAttack=Time.time+.4f;
        }

        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if(Time.timeScale<=0f||health==null||health.IsDead)return;
            visualMotion??=GetComponent<LaneUnitVisualMotion>();

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
                    visualMotion?.PulseAttack();
                    GetComponent<PlanetSentinelVisual>()?.PulseAttack();
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
            return member!=null&&member.Team!=team;
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
            if(type==PrimitiveType.Cube)part.GetComponent<MeshFilter>().sharedMesh=ProductionMeshFactory.BeveledBox();
            Collider collider=part.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
            Renderer renderer=part.GetComponent<Renderer>();
            if(renderer!=null)
            {
                Material material=RuntimeShaderLibrary.Create(color,false,.42f,.46f);
                if(material!=null)
                {
                    material.name=name+"_RuntimeLit";
                    renderer.sharedMaterial=material;
                }
                renderer.shadowCastingMode=ShadowCastingMode.On;
                renderer.receiveShadows=true;
            }
        }

        private static void CreateEnergyPart(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color,Quaternion rotation)
        {
            GameObject part=GameObject.CreatePrimitive(type);
            part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=pos;part.transform.localRotation=rotation;part.transform.localScale=scale;
            if(type==PrimitiveType.Cube)part.GetComponent<MeshFilter>().sharedMesh=ProductionMeshFactory.BeveledBox();
            Collider collider=part.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
            Renderer renderer=part.GetComponent<Renderer>();
            if(renderer!=null)
            {
                Material material=RuntimeShaderLibrary.Create(color,true,0f,0f,color*2.6f);
                if(material!=null)
                {
                    material.name=name+"_RuntimeUnlit";
                    renderer.sharedMaterial=material;
                }
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
            }
        }
    }
}


