using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Time-domain paths shared by the damage scheduler and the visible rig. No local bone-axis assumptions.
    public static class CombatMotion
    {
        public static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3f-2f*t);}
        public static float Curve(float age,float contact,float end,float draw,float hit,float through)
        {
            if(age<0f||age>=end)return 0f;
            float load=contact*.55f,follow=contact+(end-contact)*.30f;
            if(age<load)return Mathf.Lerp(0,draw,Ease(age/load));
            if(age<contact)return Mathf.Lerp(draw,hit,Mathf.Pow(Mathf.Clamp01((age-load)/(contact-load)),2f));
            if(age<follow)return Mathf.Lerp(hit,through,Ease((age-contact)/(follow-contact)));
            return Mathf.Lerp(through,0,Ease((age-follow)/(end-follow)));
        }
        public static Vector3 Path(float age,float contact,float end,Vector3 guard,Vector3 draw,Vector3 hit,Vector3 through)
        {
            return guard+new Vector3(Curve(age,contact,end,draw.x-guard.x,hit.x-guard.x,through.x-guard.x),
                Curve(age,contact,end,draw.y-guard.y,hit.y-guard.y,through.y-guard.y),
                Curve(age,contact,end,draw.z-guard.z,hit.z-guard.z,through.z-guard.z));
        }
        public static void Solve(Transform upper,Transform elbow,Transform hand,Vector3 target,Vector3 hint,float maxExtension=.975f)
        {
            if(upper==null||elbow==null||hand==null)return;
            Vector3 origin=upper.position,line=target-origin;
            float a=Vector3.Distance(origin,elbow.position),b=Vector3.Distance(elbow.position,hand.position);
            if(a<.001f||b<.001f||line.sqrMagnitude<.000001f)return;
            float distance=Mathf.Clamp(line.magnitude,Mathf.Abs(a-b)+.001f,(a+b)*Mathf.Clamp(maxExtension,.9f,.99999f));
            Vector3 dir=line.normalized,bend=Vector3.ProjectOnPlane(hint-origin,dir);
            if(bend.sqrMagnitude<.000001f)bend=Vector3.Cross(dir,Mathf.Abs(dir.y)<.9f?Vector3.up:Vector3.right);
            bend.Normalize();float along=(a*a+distance*distance-b*b)/(2*distance);
            Vector3 knee=origin+dir*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Quaternion wrist=hand.rotation;
            upper.rotation=Quaternion.FromToRotation(elbow.position-origin,knee-origin)*upper.rotation;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,origin+dir*distance-elbow.position)*elbow.rotation;
            hand.rotation=wrist;
        }
        public static event System.Action<GameObject,float,Vector3> Started;
        public static void BeginAimed(GameObject actor,float windup,Vector3 point,bool relay=true)
        {
            Present(actor,windup,point);
            if(relay)Started?.Invoke(actor,windup,point);
        }
        public static void Begin(GameObject actor,float windup,Health target,bool relay=true)
        {
            Vector3 aim=target!=null?target.transform.position:actor.transform.position+actor.transform.forward;
            Present(actor,windup,aim,target);
            if(relay)Started?.Invoke(actor,windup,aim);
        }
        public static void Present(GameObject actor,float windup,Vector3 aim,Health target=null)
        {
            if(actor.GetComponent<HeroIdentity>()!=null)
            {
                actor.GetComponent<HeroVisualMotion>()?.PulseBasic(windup);
                var melee=MeleeBasicAttackAnimation.For(actor);melee?.Begin(windup,target);melee?.SetAim(aim);
                actor.GetComponent<HeroLocomotion>()?.FaceAction(aim-actor.transform.position,windup+.4f);
            }
            else
            {
                var gesture=actor.GetComponent<CreatureAttackMotion>()??actor.AddComponent<CreatureAttackMotion>();
                gesture.Begin(windup,aim);
                actor.GetComponentInChildren<ReferenceMonsterRig>()?.PulseAttack(windup);
                actor.GetComponent<PlanetSentinelVisual>()?.PulseAttack(windup);
                actor.GetComponent<LaneUnitVisualMotion>()?.PulseAttack(windup);
                var gun=actor.GetComponentInChildren<MechMachineGunVisual>();
                if(gun!=null){gun.SetAim(aim,target);gun.Pulse(windup);}
                if(actor.GetComponent<HotZoneTank>()!=null||gun!=null&&actor.GetComponent<LaneMinion>()!=null)
                {
                    var cue=actor.GetComponent<MonsterAttackCue>()??actor.AddComponent<MonsterAttackCue>();
                    cue.Begin(windup,aim,target);
                }
            }
        }
    }
}
