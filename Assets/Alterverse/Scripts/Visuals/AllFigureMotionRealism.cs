using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only locomotion pass shared by every moving combat figure.
    /// It never moves the gameplay root/collider. Instead it adds torso/hip inertia,
    /// individual distance-driven gait timing and heavy mech weight to the rendered body.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class AllFigureMotionRealism : MonoBehaviour
    {
        private enum MotionKind { Hero, Soldier, Minion, Mech, Heavy }

        private sealed class AdditivePart
        {
            public readonly Transform Transform;
            private Quaternion previousRotation=Quaternion.identity;
            private Vector3 previousPosition;

            public AdditivePart(Transform target){Transform=target;}

            public void Apply(Quaternion rotation,Vector3 position)
            {
                if(Transform==null)return;
                Transform.localRotation=Transform.localRotation*Quaternion.Inverse(previousRotation);
                Transform.localPosition-=previousPosition;
                Transform.localRotation=Transform.localRotation*rotation;
                Transform.localPosition+=position;
                previousRotation=rotation;
                previousPosition=position;
            }

            public void Clear()
            {
                if(Transform==null)return;
                Transform.localRotation=Transform.localRotation*Quaternion.Inverse(previousRotation);
                Transform.localPosition-=previousPosition;
                previousRotation=Quaternion.identity;
                previousPosition=Vector3.zero;
            }
        }

        private MotionKind kind;
        private float seed,phase,cadenceScale,strideScale;
        private Vector3 lastPosition,smoothedVelocity;
        private float lastSpeed,moveWeight,smoothedAcceleration,smoothedTurn;
        private Vector3 lastForward;

        private Transform visualRoot,leftLeg,rightLeg,leftArm,rightArm;
        private Quaternion leftLegRest=Quaternion.identity,rightLegRest=Quaternion.identity;
        private Quaternion leftArmRest=Quaternion.identity,rightArmRest=Quaternion.identity;
        private bool overrideLegs,overrideArms;

        private AdditivePart bodyPart,pelvisPart,headPart,minionBodyPart;
        private readonly List<AdditivePart> heroTorsoParts=new();
        private float nextHierarchyRetry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallBootstrap()
        {
            FigureMotionBootstrap.Ensure();
        }

        private void Awake()
        {
            ResolveKind();
            int id=Mathf.Abs(GetInstanceID());
            seed=(id%997)/997f;
            phase=seed*Mathf.PI*2f;
            cadenceScale=.94f+((id*37)%101)/100f*.12f;
            strideScale=.95f+((id*53)%101)/100f*.10f;
            lastPosition=transform.position;
            lastForward=Flatten(transform.forward);
            if(lastForward.sqrMagnitude<.001f)lastForward=Vector3.forward;
            ResolveHierarchy();
        }

        private void OnDisable()
        {
            ClearAdditives();
        }

        private void LateUpdate()
        {
            float dt=Time.deltaTime;
            if(dt<=.00001f)return;

            if(bodyPart==null&&pelvisPart==null&&minionBodyPart==null&&heroTorsoParts.Count==0&&Time.unscaledTime>=nextHierarchyRetry)
            {
                nextHierarchyRetry=Time.unscaledTime+.75f;
                ResolveHierarchy();
            }

            UpdateKinematics(dt);
            AnimatePresentation(dt);
        }

        private void ResolveKind()
        {
            if(HasComponent("HeroIdentity")||HasComponent("PrototypeEnemyBot"))kind=MotionKind.Hero;
            else if(HasComponent("TrenchSoldier"))kind=MotionKind.Soldier;
            else if(HasComponent("HealingGuardMech")||HasComponent("GreenAlienMechVisual")||
                    HasComponent("MarsStoneGolemMechVisual")||name.IndexOf("SiegeMech",StringComparison.OrdinalIgnoreCase)>=0)
                kind=MotionKind.Mech;
            else if(HasComponent("HotZoneTank"))kind=MotionKind.Heavy;
            else kind=MotionKind.Minion;
        }

        private bool HasComponent(string typeName)
        {
            MonoBehaviour[] behaviours=GetComponents<MonoBehaviour>();
            for(int i=0;i<behaviours.Length;i++)
                if(behaviours[i]!=null&&behaviours[i].GetType().Name==typeName)return true;
            return false;
        }

        private void ResolveHierarchy()
        {
            ClearAdditives();
            heroTorsoParts.Clear();
            bodyPart=pelvisPart=headPart=minionBodyPart=null;
            leftLeg=rightLeg=leftArm=rightArm=null;
            overrideLegs=overrideArms=false;

            if(kind==MotionKind.Hero)SetupHero();
            else if(kind==MotionKind.Soldier)SetupSoldier();
            else if(kind==MotionKind.Minion)SetupMinion();
            else if(kind==MotionKind.Mech)SetupMech();
            else SetupHeavy();
        }

        private void SetupHero()
        {
            visualRoot=FindDescendant("DF_VisualRoot");
            if(visualRoot==null)return;

            Transform[] all=visualRoot.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(t==visualRoot)continue;
                string n=t.name.ToLowerInvariant();
                if(IsHeroTorsoName(n))heroTorsoParts.Add(new AdditivePart(t));
            }

            Transform head=FindBest(all,"head","helmet");
            if(head!=null)headPart=new AdditivePart(head);
        }

        private void SetupSoldier()
        {
            visualRoot=FindDescendant("DF_TrenchSoldierVisual");
            if(visualRoot==null)return;

            Transform torsoPivot=visualRoot.Find("DF_MotionSoldierUpperBody");
            if(torsoPivot==null)
            {
                torsoPivot=new GameObject("DF_MotionSoldierUpperBody").transform;
                torsoPivot.SetParent(visualRoot,false);
                torsoPivot.localPosition=new Vector3(0,1.15f,0);

                List<Transform> toGroup=new();
                for(int i=0;i<visualRoot.childCount;i++)
                {
                    Transform child=visualRoot.GetChild(i);
                    if(child==torsoPivot)continue;
                    string n=child.name;
                    if(n=="DF_TrenchSoldier_LegL"||n=="DF_TrenchSoldier_LegR"||n=="DF_TrenchSoldier_Pelvis")continue;
                    toGroup.Add(child);
                }
                for(int i=0;i<toGroup.Count;i++)toGroup[i].SetParent(torsoPivot,true);
            }

            bodyPart=new AdditivePart(torsoPivot);
            Transform pelvis=FindDescendant("DF_TrenchSoldier_Pelvis");
            if(pelvis!=null)pelvisPart=new AdditivePart(pelvis);

            leftLeg=FindDescendant("DF_TrenchSoldier_LegL");
            rightLeg=FindDescendant("DF_TrenchSoldier_LegR");
            if(leftLeg!=null&&rightLeg!=null)
            {
                leftLegRest=Quaternion.Euler(0,0,-2f);
                rightLegRest=Quaternion.Euler(0,0,2f);
                overrideLegs=true;
            }
        }

        private void SetupMinion()
        {
            // Regular lane soldiers are currently a single capsule. Give the render mesh its own
            // presentation transform so weight/bob can be animated without touching gameplay collision.
            Renderer hostRenderer=GetComponent<Renderer>();
            if(hostRenderer==null)return;

            Transform motionBody=transform.Find("DF_MinionMotionBody");
            if(motionBody==null)
            {
                MeshFilter sourceFilter=GetComponent<MeshFilter>();
                MeshRenderer sourceRenderer=GetComponent<MeshRenderer>();
                if(sourceFilter==null||sourceRenderer==null)return;

                GameObject proxy=new("DF_MinionMotionBody");
                motionBody=proxy.transform;
                motionBody.SetParent(transform,false);
                MeshFilter filter=proxy.AddComponent<MeshFilter>();
                filter.sharedMesh=sourceFilter.sharedMesh;
                MeshRenderer renderer=proxy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode=sourceRenderer.shadowCastingMode;
                renderer.receiveShadows=sourceRenderer.receiveShadows;
                sourceRenderer.enabled=false;

                List<Transform> accessories=new();
                for(int i=0;i<transform.childCount;i++)
                {
                    Transform child=transform.GetChild(i);
                    if(child!=motionBody&&child.name.StartsWith("DF_",StringComparison.Ordinal))
                        accessories.Add(child);
                }
                for(int i=0;i<accessories.Count;i++)accessories[i].SetParent(motionBody,true);
            }
            minionBodyPart=new AdditivePart(motionBody);
        }

        private void SetupMech()
        {
            visualRoot=FindDescendant("DF_GreenAlienMechVisual")??FindDescendant("DF_MarsStoneGolemVisual");
            if(visualRoot!=null)
            {
                Transform[] all=visualRoot.GetComponentsInChildren<Transform>(true);
                Transform torso=FindBest(all,"mechtorso","torso");
                Transform pelvis=FindBest(all,"mechpelvis","pelvis");
                Transform head=FindBest(all,"headpivot","head");
                leftLeg=FindSidePivot(all,true,"mechleg","leg");
                rightLeg=FindSidePivot(all,false,"mechleg","leg");
                leftArm=FindSidePivot(all,true,"mecharm","arm");
                rightArm=FindSidePivot(all,false,"mecharm","arm");

                if(torso!=null)bodyPart=new AdditivePart(torso);
                if(pelvis!=null)pelvisPart=new AdditivePart(pelvis);
                if(head!=null)headPart=new AdditivePart(head);
                CacheMechLimbRests();
                return;
            }

            SetupGuardRobot();
        }

        private void SetupGuardRobot()
        {
            Transform existingBody=transform.Find("DF_MotionGuardBody");
            Transform body=existingBody;
            Transform pelvis=transform.Find("DF_MotionGuardPelvis");
            Transform armL=FindDescendant("DF_MotionGuardArmL");
            Transform armR=FindDescendant("DF_MotionGuardArmR");
            Transform legL=transform.Find("DF_MotionGuardLegL");
            Transform legR=transform.Find("DF_MotionGuardLegR");

            if(body==null)
            {
                List<Transform> originals=new();
                for(int i=0;i<transform.childCount;i++)
                {
                    Transform child=transform.GetChild(i);
                    if(child.name.StartsWith("DF_Guard",StringComparison.Ordinal))originals.Add(child);
                }
                if(originals.Count==0)return;

                pelvis=CreatePivot("DF_MotionGuardPelvis",new Vector3(0,.08f,0),transform);
                body=CreatePivot("DF_MotionGuardBody",new Vector3(0,.83f,0),transform);
                armL=CreatePivot("DF_MotionGuardArmL",new Vector3(-1.02f,.96f,0),transform);
                armR=CreatePivot("DF_MotionGuardArmR",new Vector3(1.02f,.96f,0),transform);
                legL=CreatePivot("DF_MotionGuardLegL",new Vector3(-.44f,-.06f,0),transform);
                legR=CreatePivot("DF_MotionGuardLegR",new Vector3(.44f,-.06f,0),transform);

                armL.SetParent(body,true);
                armR.SetParent(body,true);

                for(int i=0;i<originals.Count;i++)
                {
                    Transform part=originals[i];
                    string n=part.name;
                    if(IsGuardArm(n,true))part.SetParent(armL,true);
                    else if(IsGuardArm(n,false))part.SetParent(armR,true);
                    else if(IsGuardLeg(n,true))part.SetParent(legL,true);
                    else if(IsGuardLeg(n,false))part.SetParent(legR,true);
                    else if(IsGuardPelvis(n))part.SetParent(pelvis,true);
                    else part.SetParent(body,true);
                }
            }

            bodyPart=new AdditivePart(body);
            pelvisPart=pelvis!=null?new AdditivePart(pelvis):null;
            leftArm=armL;rightArm=armR;leftLeg=legL;rightLeg=legR;
            CacheMechLimbRests();
        }

        private void SetupHeavy()
        {
            // Hot-zone creatures/tanks already own variant-specific limb animation.
            // Add only a whole-body inertia layer through a visual pivot when one is available.
            Transform candidate=FindDescendantContains("Visual");
            if(candidate!=null&&candidate!=transform)bodyPart=new AdditivePart(candidate);
        }

        private void CacheMechLimbRests()
        {
            if(leftLeg!=null&&rightLeg!=null)
            {
                leftLegRest=leftLeg.localRotation;
                rightLegRest=rightLeg.localRotation;
                overrideLegs=true;
            }
            if(leftArm!=null&&rightArm!=null)
            {
                leftArmRest=leftArm.localRotation;
                rightArmRest=rightArm.localRotation;
                overrideArms=true;
            }
        }

        private void UpdateKinematics(float dt)
        {
            Vector3 position=transform.position;
            Vector3 instant=(position-lastPosition)/dt;
            instant.y=0;
            lastPosition=position;

            float velocityBlend=1f-Mathf.Exp(-dt*10f);
            smoothedVelocity=Vector3.Lerp(smoothedVelocity,instant,velocityBlend);
            float speed=smoothedVelocity.magnitude;

            float referenceSpeed=ReferenceSpeed();
            float targetWeight=Mathf.InverseLerp(.06f,referenceSpeed*.35f,speed);
            moveWeight=Mathf.MoveTowards(moveWeight,targetWeight,dt*(targetWeight>moveWeight?6f:8f));

            float acceleration=(speed-lastSpeed)/dt;
            lastSpeed=speed;
            smoothedAcceleration=Mathf.Lerp(smoothedAcceleration,
                Mathf.Clamp(acceleration/Mathf.Max(.1f,referenceSpeed*2f),-1f,1f),
                1f-Mathf.Exp(-dt*7f));

            Vector3 forward=Flatten(transform.forward);
            if(forward.sqrMagnitude<.001f)forward=lastForward;
            float turn=Vector3.SignedAngle(lastForward,forward,Vector3.up)/dt;
            lastForward=forward;
            smoothedTurn=Mathf.Lerp(smoothedTurn,Mathf.Clamp(turn/180f,-1f,1f),1f-Mathf.Exp(-dt*8f));

            float strideLength=StrideLength()*strideScale;
            if(speed>.025f)phase+=speed*dt/Mathf.Max(.15f,strideLength)*Mathf.PI*2f*cadenceScale;
            if(phase>Mathf.PI*200f)phase-=Mathf.PI*200f;
        }

        private void AnimatePresentation(float dt)
        {
            float referenceSpeed=ReferenceSpeed();
            float speedN=Mathf.Clamp01(smoothedVelocity.magnitude/Mathf.Max(.1f,referenceSpeed));
            float lateral=Mathf.Clamp(Vector3.Dot(smoothedVelocity,transform.right)/Mathf.Max(.1f,referenceSpeed),-1f,1f);
            float step=GaitWave(phase)*moveWeight;
            float impact=Mathf.Pow(Mathf.Abs(Mathf.Sin(phase)),kind==MotionKind.Mech?10f:7f)*moveWeight;

            switch(kind)
            {
                case MotionKind.Hero:
                    AnimateHero(step,impact,speedN,lateral);
                    break;
                case MotionKind.Soldier:
                    AnimateSoldier(step,impact,speedN,lateral);
                    break;
                case MotionKind.Minion:
                    AnimateMinion(step,impact,speedN,lateral);
                    break;
                case MotionKind.Mech:
                    AnimateMech(step,impact,speedN,lateral);
                    break;
                default:
                    AnimateHeavy(step,impact,speedN,lateral);
                    break;
            }
        }

        private void AnimateHero(float step,float impact,float speedN,float lateral)
        {
            float pitch=speedN*2.8f+smoothedAcceleration*2.2f;
            float yaw=-step*3.4f-smoothedTurn*2.0f;
            float roll=-smoothedTurn*4.2f-lateral*2.0f+step*.8f;
            Quaternion torso=Quaternion.Euler(pitch,yaw,roll);

            for(int i=0;i<heroTorsoParts.Count;i++)heroTorsoParts[i].Apply(torso,Vector3.zero);
            headPart?.Apply(Quaternion.Euler(-pitch*.35f,-yaw*.45f,-roll*.50f),Vector3.zero);
        }

        private void AnimateSoldier(float step,float impact,float speedN,float lateral)
        {
            if(overrideLegs)
            {
                float angle=step*(24f+3f*strideScale);
                float turnYaw=smoothedTurn*5f+lateral*2f;
                leftLeg.localRotation=leftLegRest*Quaternion.Euler(angle,turnYaw,-smoothedTurn*2f);
                rightLeg.localRotation=rightLegRest*Quaternion.Euler(-angle,turnYaw,smoothedTurn*2f);
            }

            float pitch=speedN*4.2f+smoothedAcceleration*2.8f-impact*.9f;
            float yaw=-step*5.2f-smoothedTurn*2.5f;
            float roll=-smoothedTurn*5.5f-lateral*2.5f+step*1.3f;
            bodyPart?.Apply(Quaternion.Euler(pitch,yaw,roll),Vector3.down*impact*.018f);
            pelvisPart?.Apply(Quaternion.Euler(-pitch*.22f,-yaw*.72f,-roll*.35f),Vector3.zero);
        }

        private void AnimateMinion(float step,float impact,float speedN,float lateral)
        {
            float pitch=speedN*5.5f+smoothedAcceleration*4f-impact*1.2f;
            float yaw=-step*2.5f;
            float roll=-smoothedTurn*7f-lateral*3f+step*1.2f;
            Vector3 bob=Vector3.up*((1f-impact)*.018f*moveWeight);
            minionBodyPart?.Apply(Quaternion.Euler(pitch,yaw,roll),bob);
        }

        private void AnimateMech(float step,float impact,float speedN,float lateral)
        {
            if(overrideLegs)
            {
                float angle=step*(15f+2f*strideScale);
                float turnYaw=smoothedTurn*3.5f;
                leftLeg.localRotation=leftLegRest*Quaternion.Euler(angle,turnYaw,-smoothedTurn*1.2f);
                rightLeg.localRotation=rightLegRest*Quaternion.Euler(-angle,turnYaw,smoothedTurn*1.2f);
            }
            if(overrideArms)
            {
                float armSwing=step*6.5f;
                leftArm.localRotation=leftArmRest*Quaternion.Euler(-armSwing,0,-smoothedTurn*1.4f);
                rightArm.localRotation=rightArmRest*Quaternion.Euler(armSwing,0,smoothedTurn*1.4f);
            }

            float pitch=speedN*2.8f+smoothedAcceleration*2.6f-impact*1.3f;
            float yaw=-step*2.7f-smoothedTurn*1.5f;
            float roll=-smoothedTurn*4.2f-lateral*1.8f+step*.7f;
            bodyPart?.Apply(Quaternion.Euler(pitch,yaw,roll),Vector3.down*impact*.028f);
            pelvisPart?.Apply(Quaternion.Euler(-pitch*.18f,-yaw*.82f,-roll*.46f),Vector3.down*impact*.012f);
            headPart?.Apply(Quaternion.Euler(-pitch*.42f,-yaw*.70f,-roll*.62f),Vector3.zero);
        }

        private void AnimateHeavy(float step,float impact,float speedN,float lateral)
        {
            float pitch=speedN*2.2f+smoothedAcceleration*2.2f;
            float roll=-smoothedTurn*3.8f-lateral*1.5f;
            bodyPart?.Apply(Quaternion.Euler(pitch,-smoothedTurn*1.2f,roll),Vector3.down*impact*.016f);
        }

        private float GaitWave(float value)
        {
            float raw=Mathf.Sin(value);
            float shaped=Mathf.Sign(raw)*Mathf.Pow(Mathf.Abs(raw),kind==MotionKind.Mech?.72f:.88f);
            float asymmetry=Mathf.Sin(value*2f+seed*Mathf.PI*2f)*(kind==MotionKind.Mech?.06f:.10f);
            return Mathf.Clamp(shaped+asymmetry,-1f,1f);
        }

        private float ReferenceSpeed()
        {
            return kind switch
            {
                MotionKind.Hero=>5.6f,
                MotionKind.Soldier=>3.25f,
                MotionKind.Minion=>2.8f,
                MotionKind.Mech=>2.15f,
                _=>2.5f
            };
        }

        private float StrideLength()
        {
            return kind switch
            {
                MotionKind.Hero=>1.52f,
                MotionKind.Soldier=>1.08f,
                MotionKind.Minion=>.82f,
                MotionKind.Mech=>1.34f,
                _=>1.18f
            };
        }

        private void ClearAdditives()
        {
            bodyPart?.Clear();
            pelvisPart?.Clear();
            headPart?.Clear();
            minionBodyPart?.Clear();
            for(int i=0;i<heroTorsoParts.Count;i++)heroTorsoParts[i]?.Clear();
            if(overrideLegs)
            {
                if(leftLeg!=null)leftLeg.localRotation=leftLegRest;
                if(rightLeg!=null)rightLeg.localRotation=rightLegRest;
            }
            if(overrideArms)
            {
                if(leftArm!=null)leftArm.localRotation=leftArmRest;
                if(rightArm!=null)rightArm.localRotation=rightArmRest;
            }
        }

        private Transform FindDescendant(string exactName)
        {
            Transform[] all=GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)if(all[i].name==exactName)return all[i];
            return null;
        }

        private Transform FindDescendantContains(string token)
        {
            Transform[] all=GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
                if(all[i]!=transform&&all[i].name.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0)return all[i];
            return null;
        }

        private static Transform FindBest(Transform[] all,params string[] tokens)
        {
            for(int token=0;token<tokens.Length;token++)
                for(int i=0;i<all.Length;i++)
                    if(all[i].name.IndexOf(tokens[token],StringComparison.OrdinalIgnoreCase)>=0)return all[i];
            return null;
        }

        private static Transform FindSidePivot(Transform[] all,bool left,params string[] tokens)
        {
            string suffix=left?"l":"r";
            for(int token=0;token<tokens.Length;token++)
            {
                for(int i=0;i<all.Length;i++)
                {
                    string n=all[i].name.ToLowerInvariant();
                    if(n.Contains(tokens[token])&&(n.EndsWith(suffix)||n.EndsWith("_"+suffix)))return all[i];
                }
            }
            return null;
        }

        private static bool IsHeroTorsoName(string n)
        {
            if(n.Contains("arm")||n.Contains("hand")||n.Contains("leg")||n.Contains("boot")||
               n.Contains("foot")||n.Contains("head")||n.Contains("helmet")||n.Contains("weapon")||
               n.Contains("staff")||n.Contains("rifle")||n.Contains("sword"))return false;
            return n=="df_torso"||n.Contains("torso")||n.Contains("chest")||n.Contains("spine")||
                   n.Contains("mantle")||n.Contains("shoulderarmor");
        }

        private static bool IsGuardArm(string n,bool left)
        {
            string side=left?"L":"R";
            return n.Contains("Shoulder")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("UpperArm")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Elbow")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Forearm")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("ArmStatus")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Wrist")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Hand")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains(left?"FingerL_":"FingerR_");
        }

        private static bool IsGuardLeg(string n,bool left)
        {
            string side=left?"L":"R";
            return n.Contains("HipJoint")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Thigh")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Knee")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Shin")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Ankle")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Foot")&&n.EndsWith(side,StringComparison.Ordinal)||
                   n.Contains("Heel")&&n.EndsWith(side,StringComparison.Ordinal);
        }

        private static bool IsGuardPelvis(string n)
        {
            return n.Contains("Waist")||n.Contains("Pelvis")||n.Contains("HipArmor");
        }

        private static Transform CreatePivot(string name,Vector3 localPosition,Transform parent)
        {
            Transform pivot=new GameObject(name).transform;
            pivot.SetParent(parent,false);
            pivot.localPosition=localPosition;
            return pivot;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y=0;
            return v.sqrMagnitude>.0001f?v.normalized:Vector3.zero;
        }
    }

    internal sealed class FigureMotionBootstrap : MonoBehaviour
    {
        private float nextScan;

        public static void Ensure()
        {
            if(FindAnyObjectByType<FigureMotionBootstrap>()!=null)return;
            GameObject go=new("DF_FigureMotionBootstrap");
            DontDestroyOnLoad(go);
            go.hideFlags=HideFlags.HideInHierarchy|HideFlags.DontSave;
            go.AddComponent<FigureMotionBootstrap>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+1f;

            MonoBehaviour[] all=FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for(int i=0;i<all.Length;i++)
            {
                MonoBehaviour behaviour=all[i];
                if(behaviour==null)continue;
                string n=behaviour.GetType().Name;
                if(!IsMovingFigureType(n))continue;

                GameObject actor=behaviour.gameObject;
                if(actor.GetComponent<AllFigureMotionRealism>()==null)
                    actor.AddComponent<AllFigureMotionRealism>();
            }
        }

        private static bool IsMovingFigureType(string typeName)
        {
            return typeName=="HeroIdentity"||typeName=="TrenchSoldier"||typeName=="LaneMinion"||
                   typeName=="HealingGuardMech"||typeName=="HotZoneTank"||
                   typeName=="GreenAlienMechVisual"||typeName=="MarsStoneGolemMechVisual"||
                   typeName=="PrototypeEnemyBot";
        }
    }
}
