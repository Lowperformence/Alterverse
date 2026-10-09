#if UNITY_EDITOR
using System;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public static class CombatMotionRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Attack paths and rig-independent IK")]
        public static void Run()
        {
            // Distinct local axes, rotated parents and unequal limb lengths must resolve the same world target.
            for(int variant=0;variant<24;variant++)
            {
                var root=new GameObject("IK regression");
                try
                {
                    root.transform.rotation=Quaternion.Euler(variant*13,variant*29,variant*7);
                    Transform upper=new GameObject("upper").transform;upper.SetParent(root.transform,false);
                    Transform elbow=new GameObject("elbow").transform;elbow.SetParent(upper,false);
                    Transform hand=new GameObject("hand").transform;hand.SetParent(elbow,false);
                    upper.localRotation=Quaternion.Euler(variant*17,variant*11,variant*23);
                    elbow.localPosition=new Vector3(0,-.47f,0);elbow.localRotation=Quaternion.Euler(variant*31,variant*3,variant*19);
                    hand.localPosition=new Vector3(.06f,-.34f,.03f);
                    Vector3 target=upper.position+new Vector3(.25f,-.25f,.45f);Quaternion wrist=hand.rotation;
                    float a=Vector3.Distance(upper.position,elbow.position),b=Vector3.Distance(elbow.position,hand.position);
                    CombatMotion.Solve(upper,elbow,hand,target,upper.position+Vector3.right*.6f-Vector3.up*.4f);
                    Require(Vector3.Distance(hand.position,target)<.002f,"IK misses target on rotated rig "+variant);
                    Require(Mathf.Abs(Vector3.Distance(upper.position,elbow.position)-a)<.001f,"Upper arm stretched");
                    Require(Mathf.Abs(Vector3.Distance(elbow.position,hand.position)-b)<.001f,"Forearm stretched");
                    Require(Quaternion.Angle(hand.rotation,wrist)<.1f,"Weapon frame rotated independently");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            foreach(float contact in new[]{.16f,.18f,.20f,.22f})
            {
                float end=contact+.42f;
                Require(Mathf.Abs(CombatMotion.Curve(contact,contact,end,-.3f,1f,1.2f)-1f)<.0001f,"Contact timing mismatch");
                Require(CombatMotion.Curve(0,contact,end,-.3f,1f,1.2f)==0,"Guard start mismatch");
                Require(CombatMotion.Curve(end,contact,end,-.3f,1f,1.2f)==0,"Recovery does not reach guard");
                foreach(float boundary in new[]{contact*.55f,contact,contact+(end-contact)*.3f,end})
                    Require(Mathf.Abs(CombatMotion.Curve(boundary-.00001f,contact,end,-.3f,1f,1.2f)-CombatMotion.Curve(boundary+.00001f,contact,end,-.3f,1f,1.2f))<.002f,"Discontinuous attack path");
            }
            CheckGuardShoulderMounts();
            Debug.Log("DF: 24 imported-axis IK variants, four attack timelines and rigid guard shoulder mounts passed. Run live hero/monster checks for weapon and shield clearance.");
        }
        static void CheckGuardShoulderMounts()
        {
            var actor=new GameObject("Guard shoulder regression");
            try
            {
                Alterverse.Combat.HealingGuardMech.BuildRobotVisual(actor.transform,0);
                var motion=actor.AddComponent<LaneUnitVisualMotion>();motion.ConfigureRobot();
                Transform torso=actor.transform.Find("DF_LocomotionPose");
                Require(torso!=null,"Guard visual root missing");
                foreach(string side in new[]{"L","R"})
                {
                    Transform arm=torso.Find("DF_GuardArmMotion"+side);
                    Transform elbow=arm!=null?arm.Find("DF_GuardElbowMotion"+side):null;
                    Require(arm!=null&&elbow!=null,"Guard arm chain missing");
                    Transform plate=torso.Find("DF_GuardShoulderArmor"+side),cap=torso.Find("DF_GuardShoulderCap"+side);
                    Require(plate!=null&&cap!=null,"Shoulder armor must be mounted on the torso");
                    Require(elbow.Find("DF_GuardForearmArmor"+side)!=null,"Forearm armor must follow the elbow");
                    Vector3 position=plate.position,capPosition=cap.position;
                    Quaternion rotation=plate.rotation,capRotation=cap.rotation;
                    for(int frame=0;frame<120;frame++)
                    {
                        arm.localRotation=Quaternion.Euler(frame*7,frame*11,frame*13);
                        elbow.localRotation=Quaternion.Euler(frame*3,frame*5,frame*9);
                        Require(Vector3.Distance(plate.position,position)<.0001f&&Quaternion.Angle(plate.rotation,rotation)<.05f,"Arm rotation moved the shoulder plate");
                        Require(Vector3.Distance(cap.position,capPosition)<.0001f&&Quaternion.Angle(cap.rotation,capRotation)<.05f,"Arm rotation moved the shoulder cap");
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(actor);}
        }
        static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    }
}
#endif
