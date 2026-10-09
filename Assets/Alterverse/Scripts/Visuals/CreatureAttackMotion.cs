using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    /// <summary>Body-specific final attack layer; colliders and actor displacement are never animated.</summary>
    [DefaultExecutionOrder(1300)]
    public sealed class CreatureAttackMotion:MonoBehaviour
    {
        struct Pose { public Transform Bone; public Quaternion Rotation; public Vector3 Position; }
        readonly List<Pose> owned=new();
        Transform body,left,right,leftElbow,rightElbow,leftHand,rightHand,head,tail;
        Health health; Vector3 aim; float started=-100,contact=.22f; int serial; bool posed;
        enum Shape { None,Scorpion,Spider,Bear,Octopus,Mech,Ferrox,Vespera }
        Shape shape;
        public float ContactTime=>contact;
        public float Age=>Time.time-started;
        public bool WindingUp=>Age>=0&&Age<contact;
        public bool Active=>Age>=0&&Age<contact+.42f;
        public void Begin(float windup,Vector3 target)
        {
            if(body==null)Bind();
            Restore();started=Time.time;contact=Mathf.Max(.01f,windup);aim=target;serial++;
        }
        Transform Find(string name)
        {foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
        void Keep(Transform t){if(t!=null)owned.Add(new Pose{Bone=t,Rotation=t.localRotation,Position=t.localPosition});}
        void Bind()
        {
            health=GetComponent<Health>();owned.Clear();
            body=Find("DF_PyraxisScorpionPolished");
            left=Find("LeftClawRoot");right=Find("RightClawRoot");
            if(left!=null&&right!=null)
            {
                shape=Shape.Scorpion;body=left.parent;tail=Find("TailRoot");
                leftElbow=left.Find("ClawElbow");rightElbow=right.Find("ClawElbow");
                leftHand=leftElbow?.Find("PincerPalmRoot");rightHand=rightElbow?.Find("PincerPalmRoot");
            }
            else if((body=Find("DF_NoxaraSpiderBody"))!=null)
            {
                shape=Shape.Spider;left=Find("DF_NoxaraSpiderLeg_0");right=Find("DF_NoxaraSpiderLeg_4");
                leftElbow=Find("DF_NoxaraSpiderKnee_0");rightElbow=Find("DF_NoxaraSpiderKnee_4");
                leftHand=Find("DF_NoxaraSpiderAnkle_0");rightHand=Find("DF_NoxaraSpiderAnkle_4");head=Find("DF_NoxaraSpiderHeadPivot");
            }
            else if((body=Find("DF_KryonBearBody"))!=null)
            {shape=Shape.Bear;left=Find("DF_KryonBearLeg_0");right=Find("DF_KryonBearLeg_1");head=Find("DF_KryonBearHeadPivot");}
            else if((body=Find("DF_NeptuneOctopusVisual"))!=null)
            {shape=Shape.Octopus;left=Find("Tentacle_1");right=Find("Tentacle_7");}
            else if((body=Find("DF_MoonGuardUpperBody"))!=null)
            {shape=Shape.Mech;left=Find("DF_MoonGuardArmL");right=Find("DF_MoonGuardArmR");}
            else if((body=Find("DF_FerroxBody"))!=null)
            {shape=Shape.Ferrox;left=Find("DF_FerroxClaw_L");right=Find("DF_FerroxClaw_R");}
            else if((body=Find("DF_VesperaBody"))!=null)
            {shape=Shape.Vespera;left=Find("DF_VesperaGill_L");right=Find("DF_VesperaGill_R");}
            Keep(body);Keep(left);Keep(right);Keep(leftElbow);Keep(rightElbow);Keep(leftHand);Keep(rightHand);Keep(head);Keep(tail);
        }
        void Restore(){if(!posed)return;foreach(var p in owned)if(p.Bone!=null){p.Bone.localRotation=p.Rotation;p.Bone.localPosition=p.Position;}posed=false;}
        float C(float draw,float hit,float through)=>CombatMotion.Curve(Age,contact,contact+.42f,draw,hit,through);
        void Rotate(Transform t,Vector3 axis,float angle){if(t!=null)t.rotation=Quaternion.AngleAxis(angle,axis)*t.rotation;}
        void LateUpdate()
        {
            if(!Active||health!=null&&health.IsDead||!HeroCombatStatus.CanAct(gameObject)){Restore();return;}
            if(body==null)return;
            // Stable authored basis on every frame, never multiply last frame's attack rotation.
            posed=true;Restore();posed=true;
            Vector3 f=transform.forward,r=transform.right,u=transform.up;
            float side=(serial&1)==0?-1:1;
            Rotate(body,u,C(-7*side,8*side,12*side));
            if(shape==Shape.Scorpion||shape==Shape.Spider)
            {
                Transform upper=side>0?right:left,elbow=side>0?rightElbow:leftElbow,hand=side>0?rightHand:leftHand;
                if(upper!=null&&elbow!=null&&hand!=null)
                {
                    Vector3 guard=hand.position;float reach=Vector3.Distance(upper.position,elbow.position)+Vector3.Distance(elbow.position,hand.position);
                    Vector3 hit=upper.position+f*reach*.88f-r*side*reach*.15f-u*(shape==Shape.Spider?.12f:0);
                    Vector3 target=CombatMotion.Path(Age,contact,contact+.42f,guard,guard+r*side*.28f-f*.30f+u*.12f,hit,hit-r*side*.30f+f*.05f);
                    CombatMotion.Solve(upper,elbow,hand,target,upper.position+r*side*reach*.7f+u*.2f);
                }
                // Tail coils then strikes forward with the claw; no unrelated arm-lifting pose.
                Rotate(tail,r,C(-12,23,30));Rotate(head,r,C(-4,8,11));
            }
            else if(shape==Shape.Bear)
            {
                body.position+=f*C(-.08f,.19f,.24f);
                Rotate(head,r,C(-9,13,18));
                Rotate(side>0?right:left,u,C(-side*20,side*25,side*38));
                Rotate(side>0?right:left,r,C(-12,16,22));
            }
            else if(shape==Shape.Octopus)
            {
                Rotate(left,u,C(-22,18,28));Rotate(right,u,C(22,-18,-28));
                Rotate(left,r,C(-14,6,12));Rotate(right,r,C(-14,6,12));
                body.position+=f*C(-.06f,.12f,.16f);
            }
            else if(shape==Shape.Mech)
            {
                Rotate(right,u,C(-32,28,40));Rotate(right,r,C(-8,-16,-12));
                Rotate(left,u,C(4,-5,-7));
            }
            else if(shape==Shape.Ferrox)
            {
                Rotate(left,u,C(-18,14,24));Rotate(right,u,C(18,-14,-24));
                Rotate(left,r,C(-10,8,12));Rotate(right,r,C(-10,8,12));
                body.position+=f*C(-.06f,.10f,.14f);
            }
            else if(shape==Shape.Vespera)
            {
                Rotate(left,u,C(-20,9,14));Rotate(right,u,C(20,-9,-14));
                Rotate(body,r,C(-6,7,10));body.position+=f*C(-.04f,.09f,.12f);
            }
        }
        public void Seek(float elapsed){started-=Mathf.Max(0,elapsed);}
        void OnDisable(){Restore();started=-100;}
    }
}
