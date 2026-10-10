using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Releases the visible attack from the animated socket before authoritative contact.
    [DisallowMultipleComponent,DefaultExecutionOrder(1400)]
    public sealed class MonsterAttackCue:MonoBehaviour
    {
        HotZoneTank tank;Health owner,target;
        MechMachineGunVisual gun;
        Vector3 aim;float started=-100,contact=.22f;bool released,charged;
        public void Begin(float windup,Vector3 point,Health victim)
        {
            tank=GetComponent<HotZoneTank>();owner=GetComponent<Health>();gun=GetComponentInChildren<MechMachineGunVisual>();
            if(tank==null&&gun==null)return;
            target=victim;aim=point;contact=Mathf.Max(.01f,windup);started=Time.time;released=charged=false;
            if(target==null)
            {
                // The animation relay supplies an aim point. Resolve a nearby enemy only for visual tracking.
                var team=GetComponent<TeamMember>();float nearest=1.5f*1.5f;
                for(int i=0;i<Health.Active.Count;i++)
                {
                    var hp=Health.Active[i];if(hp==null||hp.IsDead||hp==owner)continue;
                    var other=hp.GetComponent<TeamMember>();if(team==null||other==null||other.Team==team.Team)continue;
                    float distance=(hp.transform.position-aim).sqrMagnitude;
                    if(distance<nearest){nearest=distance;target=hp;}
                }
            }
        }
        public void Seek(float elapsed){started-=Mathf.Max(0,elapsed);}
        void LateUpdate()
        {
            if(released||tank==null&&gun==null||owner==null||owner.IsDead||!HeroCombatStatus.CanAct(gameObject)||Time.timeScale<=0)return;
            float age=Time.time-started,release=contact*.32f;
            if(age<release)
            {
                if(!charged){charged=true;if(tank!=null)MonsterAttackVfx.Charge(gameObject,tank.PresentationMuzzle,tank.PresentationSocket,contact,age);}
                return;
            }
            released=true;
            if(age>=contact)return; // A delayed network event never replays an expired flight.
            Vector3 hit=(target!=null?target.transform.position:aim)+Vector3.up*.55f;
            Vector3 muzzle=gun!=null&&gun.Muzzle!=null?gun.Muzzle.position:tank!=null?tank.PresentationMuzzle:transform.position+Vector3.up*.9f;
            MonsterAttackVfx.Launch(gameObject,muzzle,hit,contact-release,age-release,target!=null?target.transform:null);
        }
        void OnDisable(){released=true;started=-100;target=null;}
    }
}
