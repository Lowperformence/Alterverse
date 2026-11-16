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
        enum Shape { None,Scorpion,Spider,Bear,Octopus,Mech,Ferrox,Vespera,Rubrox }
        Shape shape;
        MechMachineGunVisual machineGun;
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
            health=GetComponent<Health>();machineGun=GetComponentInChildren<MechMachineGunVisual>();owned.Clear();
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
            else if((body=Find("DF_RubroxBody"))!=null)
            {shape=Shape.Rubrox;left=Find("DF_RubroxCrown_L");right=Find("DF_RubroxCrown_R");}
            Keep(body);Keep(left);Keep(right);Keep(leftElbow);Keep(rightElbow);Keep(leftHand);Keep(rightHand);Keep(head);Keep(tail);
        }

        void Restore(){if(!posed)return;foreach(var p in owned)if(p.Bone!=null){p.Bone.localRotation=p.Rotation;p.Bone.localPosition=p.Position;}posed=false;}
        float C(float draw,float hit,float through)=>CombatMotion.Curve(Age,contact,contact+.42f,draw,hit,through);
        void Rotate(Transform t,Vector3 axis,float angle){if(t!=null)t.rotation=Quaternion.AngleAxis(angle,axis)*t.rotation;}

        void SolveClawStrike(Transform upper,Transform elbow,Transform hand,float side,int variant)
        {
            if(upper==null||elbow==null||hand==null)return;
            Vector3 f=transform.forward,r=transform.right,u=transform.up;
            Vector3 guard=hand.position;
            float reach=Vector3.Distance(upper.position,elbow.position)+Vector3.Distance(elbow.position,hand.position);
            float hitHeight=variant==1?.08f:variant==2?-.16f:(shape==Shape.Spider?-.12f:0f);
            Vector3 draw=guard+r*side*(variant==2?.36f:.28f)-f*.30f+u*(variant==1?-.16f:.16f);
            Vector3 hit=upper.position+f*reach*.88f-r*side*reach*.15f+u*hitHeight;
            Vector3 through=hit-r*side*(variant==2?.36f:.30f)+f*.05f+u*(variant==1?.16f:-.06f);
            Vector3 target=CombatMotion.Path(Age,contact,contact+.42f,guard,draw,hit,through);
            CombatMotion.Solve(upper,elbow,hand,target,upper.position+r*side*reach*.7f+u*.2f);
        }

        void LateUpdate()
        {
            if(!Active||health!=null&&health.IsDead||!HeroCombatStatus.CanAct(gameObject)){Restore();return;}
            if(shape==Shape.Mech&&machineGun!=null){Restore();return;} // The weapon owns firing recoil; no competing arm attack layer.
            if(body==null)return;
            // Stable authored basis on every frame, never multiply last frame's attack rotation.
            posed=true;Restore();posed=true;
            Vector3 f=transform.forward,r=transform.right,u=transform.up;
            float side=(serial&1)==0?-1:1;
            int variant=serial%3;
            bool twin=variant==2;
            Rotate(body,u,shape==Shape.Mech?C(-1,1.5f,2):twin?C(-2,2,3):C(-7*side,8*side,12*side));
            if(shape==Shape.Scorpion||shape==Shape.Spider)
            {
                if(twin)
                {
                    SolveClawStrike(left,leftElbow,leftHand,-1f,variant);
                    SolveClawStrike(right,rightElbow,rightHand,1f,variant);
                    Rotate(body,r,C(-6,10,14));
                }
                else
                {
                    SolveClawStrike(side>0?right:left,side>0?rightElbow:leftElbow,side>0?rightHand:leftHand,side,variant);
                }
                Rotate(tail,r,twin?C(-18,30,38):C(-12,23,30));
                Rotate(head,r,twin?C(-7,12,16):C(-4,8,11));
            }
            else if(shape==Shape.Bear)
            {
                body.position+=f*C(-.08f,.19f,.24f);
                Rotate(head,r,twin?C(-14,19,25):C(-9,13,18));
                if(twin)
                {
                    Rotate(body,r,C(-10,14,18));
                    Rotate(left,u,C(18,-22,-32));Rotate(right,u,C(-18,22,32));
                    Rotate(left,r,C(-16,22,28));Rotate(right,r,C(-16,22,28));
                }
                else
                {
                    Transform paw=side>0?right:left;
                    Rotate(paw,u,C(-side*20,side*25,side*38));
                    Rotate(paw,r,variant==1?C(14,-18,-24):C(-12,16,22));
                }
            }
            else if(shape==Shape.Octopus)
            {
                if(twin)
                {
                    Rotate(left,u,C(-28,24,36));Rotate(right,u,C(28,-24,-36));
                    Rotate(left,r,C(-18,12,18));Rotate(right,r,C(-18,12,18));
                }
                else
                {
                    Rotate(side>0?right:left,u,C(-side*26,side*22,side*34));
                    Rotate(left,r,C(-14,6,12));Rotate(right,r,C(-14,6,12));
                }
                body.position+=f*C(-.06f,.12f,.16f);
            }
            else if(shape==Shape.Mech)
            {
                // Keep a held machine gun aimed forward, with a small recoil instead of a punch.
                Rotate(right,u,C(-1,1,1.5f));Rotate(right,r,C(-1,3,4));
                Rotate(left,u,C(1,-1,-1.5f));
            }
            else if(shape==Shape.Ferrox)
            {
                if(twin)
                {
                    Rotate(left,u,C(-24,20,30));Rotate(right,u,C(24,-20,-30));
                    Rotate(left,r,C(-14,12,18));Rotate(right,r,C(-14,12,18));
                }
                else
                {
                    Rotate(side>0?right:left,u,C(-side*22,side*18,side*28));
                    Rotate(side>0?right:left,r,variant==1?C(10,-12,-16):C(-12,10,15));
                }
                body.position+=f*C(-.06f,.10f,.14f);
            }
            else if(shape==Shape.Vespera)
            {
                Rotate(left,u,C(twin?-26:-20,twin?14:9,twin?20:14));
                Rotate(right,u,C(twin?26:20,twin?-14:-9,twin?-20:-14));
                Rotate(body,r,C(variant==1?6:-6,variant==1?-8:7,variant==1?-12:10));
                body.position+=f*C(-.04f,.09f,.12f);
            }
            else if(shape==Shape.Rubrox)
            {
                Rotate(body,r,C(twin?-12:-8,twin?15:10,twin?20:14));
                body.position+=f*C(-.05f,.11f,.15f);
                Rotate(left,r,C(-10,12,16));Rotate(right,r,C(-10,12,16));
            }
        }

        public void Seek(float elapsed){started-=Mathf.Max(0,elapsed);}
        void OnDisable(){Restore();started=-100;}
    }
}
