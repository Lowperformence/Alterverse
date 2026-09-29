using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Runtime visual upgrade for the Mars Hot-Zone skeleton.
    ///
    /// The original skeleton animates one long arm capsule while both claw transforms live
    /// directly under the monster root. That makes the arms swing but leaves the hands behind.
    /// This polish layer keeps the original animation as the motion driver, hides the single-piece
    /// arm meshes, and rebuilds each arm as a shoulder -> upper arm -> elbow -> forearm -> wrist ->
    /// hand hierarchy. The original claw transforms are re-parented into the wrists, so the existing
    /// attack muzzle reference also follows the animated hand automatically.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class MarsSkeletonVisualPolish : MonoBehaviour
    {
        private const string ManagerName="DF_MarsSkeletonVisualPolish";
        private const string RigRootName="DF_MarsSkeleton_ArticulatedRig";
        private const float ScanInterval=.20f;

        private static bool installed;
        private readonly Dictionary<int,SkeletonRig> rigs=new();
        private readonly List<int> deadRigIds=new();
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){installed=false;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(installed)return;
            installed=true;
            GameObject manager=new(ManagerName);
            DontDestroyOnLoad(manager);
            manager.AddComponent<MarsSkeletonVisualPolish>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+ScanInterval;

            IReadOnlyList<HotZoneTank> active=HotZoneTank.Active;
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank tank=active[i];
                if(tank==null||!tank.IsMarsSkeleton)continue;
                int id=tank.GetInstanceID();
                if(rigs.ContainsKey(id))continue;

                SkeletonRig rig=SkeletonRig.TryBuild(tank);
                if(rig!=null)rigs.Add(id,rig);
            }
        }

        private void LateUpdate()
        {
            if(rigs.Count==0)return;
            deadRigIds.Clear();
            foreach(KeyValuePair<int,SkeletonRig> pair in rigs)
            {
                SkeletonRig rig=pair.Value;
                if(rig==null||!rig.IsAlive){deadRigIds.Add(pair.Key);continue;}
                rig.Tick();
            }
            for(int i=0;i<deadRigIds.Count;i++)rigs.Remove(deadRigIds[i]);
        }

        private sealed class SkeletonRig
        {
            private static readonly Quaternion SourceLeftBase=Quaternion.Euler(0,0,-10f);
            private static readonly Quaternion SourceRightBase=Quaternion.Euler(0,0,10f);

            private readonly Transform root;
            private readonly Transform sourceLeftArm,sourceRightArm;
            private readonly Transform leftShoulder,rightShoulder,leftElbow,rightElbow,leftWrist,rightWrist;
            private readonly Quaternion leftShoulderBase,rightShoulderBase;
            private float detailClock;

            public bool IsAlive=>root!=null;

            private SkeletonRig(
                Transform root,
                Transform sourceLeftArm,
                Transform sourceRightArm,
                Transform leftShoulder,
                Transform rightShoulder,
                Transform leftElbow,
                Transform rightElbow,
                Transform leftWrist,
                Transform rightWrist)
            {
                this.root=root;
                this.sourceLeftArm=sourceLeftArm;
                this.sourceRightArm=sourceRightArm;
                this.leftShoulder=leftShoulder;
                this.rightShoulder=rightShoulder;
                this.leftElbow=leftElbow;
                this.rightElbow=rightElbow;
                this.leftWrist=leftWrist;
                this.rightWrist=rightWrist;
                leftShoulderBase=leftShoulder.localRotation;
                rightShoulderBase=rightShoulder.localRotation;
                detailClock=Random.value*Mathf.PI*2f;
            }

            public static SkeletonRig TryBuild(HotZoneTank tank)
            {
                if(tank==null)return null;
                Transform root=tank.transform;
                if(root.Find(RigRootName)!=null)return null;

                Transform sourceLeft=root.Find("DF_HotZoneTank_TitanArmL");
                Transform sourceRight=root.Find("DF_HotZoneTank_TitanArmR");
                Transform leftHand=root.Find("DF_HotZoneTank_TitanClawL");
                Transform rightHand=root.Find("DF_HotZoneTank_TitanClawR");
                if(sourceLeft==null||sourceRight==null||leftHand==null||rightHand==null)return null;

                Material bone=MaterialOf(sourceLeft);
                Material joint=MaterialOf(leftHand)??bone;
                Material energy=MaterialOf(rightHand)??joint;
                Material dark=MaterialOf(root.Find("DF_HotZoneTank_TitanBrowRidge"))??joint;
                if(bone==null)return null;

                // The old straight capsules still receive the original HotZoneTank gait/attack
                // rotations. Keep them as invisible animation drivers instead of duplicating the
                // private gait logic here.
                SetVisible(sourceLeft,false);
                SetVisible(sourceRight,false);
                SetVisible(root.Find("DF_HotZoneTank_TitanWristCuffL"),false);
                SetVisible(root.Find("DF_HotZoneTank_TitanWristCuffR"),false);

                Transform rigRoot=Node(RigRootName,root,Vector3.zero,Quaternion.identity);
                BuildTorsoAndSkullDetails(root,rigRoot,bone,joint,dark);

                ArmRig left=BuildArm(rigRoot,leftHand,-1f,bone,joint,dark,false);
                ArmRig right=BuildArm(rigRoot,rightHand,1f,bone,joint,energy,true);

                return new SkeletonRig(root,sourceLeft,sourceRight,
                    left.shoulder,right.shoulder,left.elbow,right.elbow,left.wrist,right.wrist);
            }

            public void Tick()
            {
                if(sourceLeftArm==null||sourceRightArm==null)return;
                detailClock+=Time.deltaTime;

                // HotZoneTank already contains the exact walk, idle sway, wind-up and attack kick.
                // Read that delta after its Update and apply it to the new anatomical shoulder pivots.
                Quaternion leftDelta=Quaternion.Inverse(SourceLeftBase)*sourceLeftArm.localRotation;
                Quaternion rightDelta=Quaternion.Inverse(SourceRightBase)*sourceRightArm.localRotation;
                leftShoulder.localRotation=leftShoulderBase*leftDelta;
                rightShoulder.localRotation=rightShoulderBase*rightDelta;

                float leftSwing=SignedX(leftDelta);
                float rightSwing=SignedX(rightDelta);
                float idleFlex=Mathf.Sin(detailClock*1.65f)*1.8f;

                // A small permanent bend plus motion-dependent flex removes the rigid stick-arm look.
                float leftBend=15f+Mathf.Abs(leftSwing)*.24f+idleFlex;
                float rightBend=15f+Mathf.Abs(rightSwing)*.24f-idleFlex;
                leftElbow.localRotation=Quaternion.Euler(leftBend,0,-4f);
                rightElbow.localRotation=Quaternion.Euler(rightBend,0,4f);

                // Wrists counter-rotate slightly so the claws feel attached but not robotic.
                leftWrist.localRotation=Quaternion.Euler(-leftBend*.18f,Mathf.Sin(detailClock*1.9f)*2.2f,-2f);
                rightWrist.localRotation=Quaternion.Euler(-rightBend*.18f,-Mathf.Sin(detailClock*1.9f)*2.2f,2f);
            }

            private static ArmRig BuildArm(Transform parent,Transform hand,float side,Material bone,Material joint,Material accent,bool attackHand)
            {
                string suffix=side<0?"L":"R";
                Transform shoulder=Node("DF_MarsSkeleton_ShoulderPivot"+suffix,parent,new Vector3(side*1.12f,1.72f,0),Quaternion.identity);

                // Humerus with a visible bone head. Its pivot is the actual shoulder, not the
                // middle of a long capsule, which keeps the whole limb connected while swinging.
                Part("DF_MarsSkeleton_Humerus"+suffix,PrimitiveType.Capsule,shoulder,
                    new Vector3(side*.105f,-.39f,.035f),new Vector3(.17f,.43f,.17f),bone,Quaternion.Euler(0,0,-side*15f));
                Part("DF_MarsSkeleton_ShoulderHead"+suffix,PrimitiveType.Sphere,shoulder,
                    new Vector3(side*.015f,-.035f,.02f),new Vector3(.24f,.20f,.24f),joint,Quaternion.identity);

                Transform elbow=Node("DF_MarsSkeleton_ElbowPivot"+suffix,shoulder,new Vector3(side*.21f,-.80f,.055f),Quaternion.identity);
                Part("DF_MarsSkeleton_ElbowJoint"+suffix,PrimitiveType.Sphere,elbow,Vector3.zero,
                    new Vector3(.25f,.21f,.25f),joint,Quaternion.identity);
                Part("DF_MarsSkeleton_ElbowSpur"+suffix,PrimitiveType.Capsule,elbow,
                    new Vector3(side*.15f,.015f,-.02f),new Vector3(.075f,.20f,.075f),bone,Quaternion.Euler(0,0,side*58f));

                // Two separate forearm bones (radius/ulna silhouette) instead of one straight tube.
                Part("DF_MarsSkeleton_Radius"+suffix,PrimitiveType.Capsule,elbow,
                    new Vector3(side*.055f,-.35f,.055f),new Vector3(.095f,.37f,.095f),bone,Quaternion.Euler(0,0,-side*4f));
                Part("DF_MarsSkeleton_Ulna"+suffix,PrimitiveType.Capsule,elbow,
                    new Vector3(-side*.055f,-.35f,-.035f),new Vector3(.085f,.36f,.085f),bone,Quaternion.Euler(0,0,side*3f));

                Transform wrist=Node("DF_MarsSkeleton_WristPivot"+suffix,elbow,new Vector3(side*.055f,-.72f,.035f),Quaternion.identity);
                Part("DF_MarsSkeleton_WristJoint"+suffix,PrimitiveType.Sphere,wrist,Vector3.zero,
                    new Vector3(.19f,.16f,.20f),joint,Quaternion.identity);
                Part("DF_MarsSkeleton_WristCuff"+suffix,PrimitiveType.Cube,wrist,
                    new Vector3(0,-.045f,.01f),new Vector3(.31f,.13f,.30f),joint,Quaternion.Euler(0,0,side*5f));

                // This is the important bug fix: move the ORIGINAL claw under the animated wrist.
                // The right claw is also HotZoneTank.barrelPivot, so muzzle VFX/projectiles now come
                // from the moving hand instead of the old fixed root-space location.
                hand.SetParent(wrist,false);
                hand.localPosition=new Vector3(0,-.17f,.10f);
                hand.localRotation=Quaternion.identity;

                BuildHandDetails(hand,side,bone,joint,accent,attackHand);
                return new ArmRig(shoulder,elbow,wrist);
            }

            private static void BuildHandDetails(Transform hand,float side,Material bone,Material joint,Material accent,bool attackHand)
            {
                string suffix=side<0?"L":"R";
                Material knuckle=attackHand?accent:joint;

                // Knuckles and two phalanges per finger make the claw read as a skeletal hand.
                for(int i=-1;i<=1;i++)
                {
                    float spread=i*.145f;
                    Part("DF_MarsSkeleton_Knuckle"+suffix+i,PrimitiveType.Sphere,hand,
                        new Vector3(spread,-.015f,.31f),new Vector3(.105f,.085f,.10f),knuckle,Quaternion.identity);
                    Part("DF_MarsSkeleton_FingerA"+suffix+i,PrimitiveType.Capsule,hand,
                        new Vector3(spread,-.02f,.48f),new Vector3(.055f,.17f,.055f),bone,Quaternion.Euler(90f-i*3f,0,side*i*5f));
                    Part("DF_MarsSkeleton_FingerB"+suffix+i,PrimitiveType.Capsule,hand,
                        new Vector3(spread+side*i*.018f,-.075f,.70f),new Vector3(.047f,.13f,.047f),bone,Quaternion.Euler(76f-i*4f,0,side*i*7f));
                }

                // Crooked thumb for an asymmetrical, less manufactured silhouette.
                Part("DF_MarsSkeleton_Thumb"+suffix,PrimitiveType.Capsule,hand,
                    new Vector3(side*.25f,-.08f,.30f),new Vector3(.06f,.15f,.06f),bone,Quaternion.Euler(67f,side*18f,side*34f));
            }

            private static void BuildTorsoAndSkullDetails(Transform root,Transform parent,Material bone,Material joint,Material dark)
            {
                // Clavicles tie the rib cage into the shoulder joints and make the upper body look
                // like a skeleton instead of disconnected primitives.
                Part("DF_MarsSkeleton_ClavicleL",PrimitiveType.Capsule,parent,new Vector3(-.55f,1.61f,.035f),
                    new Vector3(.105f,.58f,.105f),bone,Quaternion.Euler(0,0,67f));
                Part("DF_MarsSkeleton_ClavicleR",PrimitiveType.Capsule,parent,new Vector3(.55f,1.61f,.035f),
                    new Vector3(.105f,.58f,.105f),bone,Quaternion.Euler(0,0,-67f));
                Part("DF_MarsSkeleton_Sternum",PrimitiveType.Capsule,parent,new Vector3(0,1.27f,.12f),
                    new Vector3(.11f,.49f,.10f),joint,Quaternion.identity);

                // Individual vertebra knobs and an intentionally broken rib shard add small readable
                // horror detail without adding gameplay colliders.
                for(int i=0;i<5;i++)
                {
                    Part("DF_MarsSkeleton_Vertebra"+i,PrimitiveType.Sphere,parent,new Vector3(0,.76f+i*.27f,-.16f),
                        new Vector3(.18f,.12f,.16f),joint,Quaternion.identity);
                }
                Part("DF_MarsSkeleton_BrokenRib",PrimitiveType.Capsule,parent,new Vector3(-.88f,1.11f,.18f),
                    new Vector3(.075f,.28f,.075f),bone,Quaternion.Euler(13f,0,39f));
                Part("DF_MarsSkeleton_ShoulderSpurL",PrimitiveType.Capsule,parent,new Vector3(-1.24f,1.93f,-.02f),
                    new Vector3(.07f,.23f,.07f),bone,Quaternion.Euler(0,0,-38f));
                Part("DF_MarsSkeleton_ShoulderSpurR",PrimitiveType.Capsule,parent,new Vector3(1.24f,1.93f,-.02f),
                    new Vector3(.07f,.20f,.07f),bone,Quaternion.Euler(0,0,38f));

                Transform skull=root.Find("DF_HotZoneTank_TitanSkull");
                if(skull==null)return;

                // Deep socket shadows sit just behind the existing glowing eyes.
                Part("DF_MarsSkeleton_EyeSocketL",PrimitiveType.Sphere,skull,new Vector3(-.28f,.08f,.635f),
                    new Vector3(.25f,.20f,.055f),dark,Quaternion.identity);
                Part("DF_MarsSkeleton_EyeSocketR",PrimitiveType.Sphere,skull,new Vector3(.28f,.08f,.635f),
                    new Vector3(.25f,.20f,.055f),dark,Quaternion.identity);

                // Uneven teeth and three thin crack marks keep the face creepy at gameplay distance.
                for(int i=0;i<6;i++)
                {
                    float x=-.225f+i*.09f;
                    float drop=(i==1||i==4)?.055f:.075f;
                    Part("DF_MarsSkeleton_Tooth"+i,PrimitiveType.Cube,skull,new Vector3(x,-.52f,.68f),
                        new Vector3(.045f,drop,.055f),bone,Quaternion.Euler(i%2==0?3f:-4f,0,(i-3)*1.8f));
                }
                Part("DF_MarsSkeleton_SkullCrackA",PrimitiveType.Cube,skull,new Vector3(-.11f,.27f,.72f),
                    new Vector3(.025f,.18f,.018f),dark,Quaternion.Euler(0,0,-24f));
                Part("DF_MarsSkeleton_SkullCrackB",PrimitiveType.Cube,skull,new Vector3(-.18f,.14f,.73f),
                    new Vector3(.020f,.12f,.018f),dark,Quaternion.Euler(0,0,31f));
                Part("DF_MarsSkeleton_SkullCrackC",PrimitiveType.Cube,skull,new Vector3(.17f,.32f,.72f),
                    new Vector3(.018f,.10f,.018f),dark,Quaternion.Euler(0,0,18f));
            }

            private static float SignedX(Quaternion rotation)
            {
                float x=rotation.eulerAngles.x;
                return x>180f?x-360f:x;
            }

            private static Material MaterialOf(Transform part)
            {
                if(part==null)return null;
                Renderer renderer=part.GetComponent<Renderer>();
                return renderer!=null?renderer.sharedMaterial:null;
            }

            private static void SetVisible(Transform part,bool visible)
            {
                if(part==null)return;
                Renderer renderer=part.GetComponent<Renderer>();
                if(renderer!=null)renderer.enabled=visible;
            }

            private static Transform Node(string name,Transform parent,Vector3 localPosition,Quaternion localRotation)
            {
                GameObject go=new(name);
                Transform t=go.transform;
                t.SetParent(parent,false);
                t.localPosition=localPosition;
                t.localRotation=localRotation;
                t.localScale=Vector3.one;
                return t;
            }

            private static Transform Part(string name,PrimitiveType type,Transform parent,Vector3 localPosition,Vector3 localScale,Material material,Quaternion localRotation)
            {
                GameObject go=GameObject.CreatePrimitive(type);
                go.name=name;
                Transform t=go.transform;
                t.SetParent(parent,false);
                t.localPosition=localPosition;
                t.localScale=localScale;
                t.localRotation=localRotation;
                Collider collider=go.GetComponent<Collider>();
                if(collider!=null){collider.enabled=false;Destroy(collider);}
                Renderer renderer=go.GetComponent<Renderer>();
                if(renderer!=null)renderer.sharedMaterial=material;
                return t;
            }

            private readonly struct ArmRig
            {
                public readonly Transform shoulder,elbow,wrist;
                public ArmRig(Transform shoulder,Transform elbow,Transform wrist)
                {
                    this.shoulder=shoulder;
                    this.elbow=elbow;
                    this.wrist=wrist;
                }
            }
        }
    }
}
