using Alterverse.Visuals;
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
            if(renderer!=null){renderer.material.color=dark;renderer.shadowCastingMode=ShadowCastingMode.On;}

            CapsuleCollider collider=mech.GetComponent<CapsuleCollider>();
            if(collider!=null){collider.radius=.72f;collider.height=2.05f;}

            Health hp=mech.AddComponent<Health>();
            hp.Configure(650,true);
            mech.AddComponent<TeamMember>().Initialize(team);
            HealingGuardMech guard=mech.AddComponent<HealingGuardMech>();
            guard.Initialize(team,spawn,healingPlate,energy);

            // Same visual language as the minute-six siege unit, but with a taller defensive silhouette.
            CreatePart(mech.transform,"DF_GuardChest",PrimitiveType.Cube,new Vector3(0,.32f,0),new Vector3(1.95f,1.02f,1.18f),dark);
            CreatePart(mech.transform,"DF_GuardCore",PrimitiveType.Cube,new Vector3(0,.36f,.63f),new Vector3(.78f,.48f,.13f),energy);
            CreatePart(mech.transform,"DF_GuardShoulderL",PrimitiveType.Cube,new Vector3(-1.10f,.50f,0),new Vector3(.50f,.82f,.92f),energy);
            CreatePart(mech.transform,"DF_GuardShoulderR",PrimitiveType.Cube,new Vector3(1.10f,.50f,0),new Vector3(.50f,.82f,.92f),energy);
            CreatePart(mech.transform,"DF_GuardCrown",PrimitiveType.Cube,new Vector3(0,1.48f,.02f),new Vector3(.46f,.72f,.78f),energy);
            CreatePart(mech.transform,"DF_GuardHip",PrimitiveType.Cube,new Vector3(0,-.43f,0),new Vector3(1.34f,.32f,.82f),dark);
            CreatePart(mech.transform,"DF_GuardLegL",PrimitiveType.Cube,new Vector3(-.61f,-.78f,0),new Vector3(.44f,.82f,.60f),dark);
            CreatePart(mech.transform,"DF_GuardLegR",PrimitiveType.Cube,new Vector3(.61f,-.78f,0),new Vector3(.44f,.82f,.60f),dark);
            CreatePart(mech.transform,"DF_GuardShieldFinL",PrimitiveType.Cube,new Vector3(-1.23f,.34f,.18f),new Vector3(.18f,1.28f,.78f),energy);
            CreatePart(mech.transform,"DF_GuardShieldFinR",PrimitiveType.Cube,new Vector3(1.23f,.34f,.18f),new Vector3(.18f,1.28f,.78f),energy);

            return mech;
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

        private static void CreatePart(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color)
        {
            GameObject part=GameObject.CreatePrimitive(type);
            part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=pos;part.transform.localScale=scale;
            Collider collider=part.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
            Renderer renderer=part.GetComponent<Renderer>();if(renderer!=null){renderer.material.color=color;renderer.shadowCastingMode=ShadowCastingMode.On;}
        }
    }
}
