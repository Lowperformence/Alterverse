using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
    /// <summary>One visible rig for reference-built biped monsters; combat remains on HotZoneTank.</summary>
    [DefaultExecutionOrder(950)]
    public sealed class ReferenceMonsterRig : MonoBehaviour
    {
        private Transform host, chest, head;
        private readonly Dictionary<string,Transform> joints = new Dictionary<string,Transform>();
        private readonly Dictionary<Transform,Quaternion> rest = new Dictionary<Transform,Quaternion>();
        private Vector3 previous; private float phase, moving, attackUntil;
        private readonly Vector3[] plants = new Vector3[2], swingStart = new Vector3[2], swingEnd = new Vector3[2];
        private readonly bool[] swinging = new bool[2];
        private bool planted;
        public Transform Muzzle { get; private set; }
        private Transform leftMuzzle;
        public Transform AttackSocket=>(attackSerial&1)==0&&leftMuzzle!=null?leftMuzzle:Muzzle;
        public Color EnergyColor { get; private set; }
        public static ReferenceMonsterRig Build(Transform host, string resource, Transform visualParent = null)
        {
            var data = ResourceCreatureMesh.Load(resource);
            if (data == null || data.joints == null) return null;
            var root = new GameObject("DF_ReferenceMonster_" + resource).transform;
            root.SetParent(visualParent != null ? visualParent : host, false);
            var rig = root.gameObject.AddComponent<ReferenceMonsterRig>(); rig.host = host;rig.species=resource;
            rig.joints.Add("",root);
            foreach (var joint in data.joints)
            {
                if (!rig.joints.TryGetValue(joint.parent, out Transform parent) || joint.position == null || joint.position.Length != 3)
                { Destroy(root.gameObject); return null; }
                var bone = new GameObject(joint.path.Substring(joint.path.LastIndexOf('/')+1)).transform;
                bone.SetParent(parent,false); bone.localPosition = new Vector3(joint.position[0],joint.position[1],joint.position[2]);
                rig.joints.Add(joint.path,bone); rig.rest.Add(bone,bone.localRotation);
            }
            Color[] colors = resource == "GJMagentaGolem" ? new[] { new Color(.085f,.015f,.061f),new Color(.44f,.045f,.29f),new Color(.72f,.13f,.43f),new Color(.87f,.28f,.86f),new Color(.28f,.045f,.18f) } :
                resource == "MarsSkeleton" ? new[] { new Color(.075f,.065f,.06f),new Color(.59f,.54f,.42f),new Color(.76f,.70f,.54f),new Color(.62f,.18f,.85f),new Color(.26f,.22f,.16f) } :
                resource == "GreenStalker" ? new[] { new Color(.041f,.063f,.056f),new Color(.23f,.32f,.26f),new Color(.46f,.51f,.39f),new Color(.43f,.62f,.24f),new Color(.095f,.13f,.10f) } :
                new[] { new Color(.045f,.085f,.12f),new Color(.44f,.59f,.65f),new Color(.74f,.84f,.88f),new Color(.06f,.77f,1f),new Color(.23f,.38f,.46f) };
            rig.EnergyColor=colors[3];
            if (ResourceCreatureMesh.Apply(root,resource,colors,shareGeometry:true) == null) { Destroy(root.gameObject); return null; }
            rig.chest = rig.joints["Hips/Chest"]; rig.head = rig.joints["Hips/Chest/Head"];
            rig.Muzzle = new GameObject("DF_ReferenceMonsterMuzzle").transform;
            rig.Muzzle.SetParent(rig.joints["Hips/Chest/ArmR/ElbowR/HandR"],false);
            rig.Muzzle.localPosition = new Vector3(0,-.10f,.28f);
            rig.leftMuzzle=new GameObject("DF_ReferenceMonsterMuzzleL").transform;
            rig.leftMuzzle.SetParent(rig.joints["Hips/Chest/ArmL/ElbowL/HandL"],false);
            rig.leftMuzzle.localPosition=rig.Muzzle.localPosition;
            rig.previous = host.position; rig.PlaceOnSurface(); return rig;
        }
        float attackStarted=-100f,contact=.22f;int attackSerial;string species;
        public void PulseAttack(float windup=.22f){contact=Mathf.Max(.01f,windup);attackStarted=Time.time;attackUntil=Time.time+contact+.42f;attackSerial++;}
        public void Seek(float elapsed){attackStarted-=elapsed;attackUntil-=elapsed;}
        private void PlaceOnSurface()
        {
            Vector3 p = host.position;
            p.y = LunarSurfaceField.Surface(p.x,p.z).y + .035f;
            transform.position = p; transform.rotation = host.rotation;
        }
        private void OnEnable()
        {
            if(host==null)return;
            previous=host.position;phase=moving=0f;planted=false;
            attackStarted=-100f;attackUntil=0f;attackSerial=0;
            PlaceOnSurface();
        }
        private Vector3 Ground(Vector3 p) { p.y = LunarSurfaceField.Surface(p.x,p.z).y + .16f; return p; }
        private void LateUpdate()
        {
            if (host == null) return;
            PlaceOnSurface();
            if (Time.timeScale <= 0f) { previous=host.position; return; }
            Vector3 delta=host.position-previous;delta.y=0;previous=host.position;
            if(delta.sqrMagnitude>16f){planted=false;delta=Vector3.zero;}
            float speed=delta.magnitude/Mathf.Max(Time.deltaTime,.001f);
            moving=Mathf.MoveTowards(moving,Mathf.Clamp01(speed/.7f),Time.deltaTime*5f);
            phase+=delta.magnitude*2.8f;
            foreach(var pair in rest)pair.Key.localRotation=pair.Value;
            float age=Time.time-attackStarted;
            float attack=CombatMotion.Curve(age,contact,contact+.42f,-.3f,1f,.65f);
            chest.rotation=Quaternion.AngleAxis(CombatMotion.Curve(age,contact,contact+.42f,-18,12,23),host.up)*chest.rotation;
            head.localRotation=Quaternion.Euler(2f*Mathf.Sin(Time.time*1.3f),0,0);
            for(int i=0;i<2;i++)
            {
                string side=i==0?"L":"R";float sign=i==0?-1f:1f;
                Transform arm=joints["Hips/Chest/Arm"+side], elbow=joints["Hips/Chest/Arm"+side+"/Elbow"+side];
                Transform hand=joints["Hips/Chest/Arm"+side+"/Elbow"+side+"/Hand"+side];
                float reach=Vector3.Distance(arm.position,elbow.position)+Vector3.Distance(elbow.position,hand.position);
                Vector3 guard=arm.position-host.up*reach*.58f+host.forward*reach*.28f+host.right*sign*reach*.1f;
                bool striking=i==(attackSerial%2);
                Vector3 target=guard;
                if(striking&&Time.time<attackUntil)
                {
                    bool slash=species=="MarsSkeleton"||species=="GreenStalker";
                    Vector3 draw=arm.position+host.right*sign*reach*(slash?.48f:.18f)-host.forward*reach*.15f-host.up*reach*.2f;
                    Vector3 hit=arm.position+host.forward*reach*.90f-host.up*reach*.20f;
                    Vector3 through=hit+(slash?-host.right*sign*reach*.40f:host.forward*reach*.035f);
                    target=CombatMotion.Path(age,contact,contact+.42f,guard,draw,hit,through);
                }
                CombatMotion.Solve(arm,elbow,hand,target,arm.position+host.right*sign*reach*.65f-host.up*reach*.35f);
                Transform hip=joints["Hips/Thigh"+side],knee=joints["Hips/Thigh"+side+"/Shin"+side],foot=joints["Hips/Thigh"+side+"/Shin"+side+"/Foot"+side];
                Vector3 home=Ground(foot.position);float cycle=Mathf.Repeat(phase/(Mathf.PI*2f)+i*.5f,1f);
                if(!planted){plants[i]=home;swingStart[i]=home;swingEnd[i]=home;swinging[i]=false;}
                bool swing=moving>.05f&&cycle>.60f;
                if(swing&&!swinging[i]){swingStart[i]=plants[i];swingEnd[i]=Ground(home+host.forward*.28f);}
                Vector3 legTarget=plants[i];
                if(swing){float t=(cycle-.60f)/.40f;legTarget=Vector3.Lerp(swingStart[i],swingEnd[i],Mathf.SmoothStep(0,1,t))+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.16f);}
                else if(swinging[i]){plants[i]=Ground(swingEnd[i]);legTarget=plants[i];}
                if(moving<.03f) {plants[i]=Vector3.Lerp(plants[i],home,Time.deltaTime*7f);legTarget=plants[i];}
                swinging[i]=swing;
                SolveLeg(hip,knee,foot,legTarget,host.forward);
                foot.rotation=host.rotation;
            }
            planted=true;
        }
        private static void SolveLeg(Transform hip,Transform knee,Transform foot,Vector3 target,Vector3 forward)
        {
            Vector3 origin=hip.position;float upper=Vector3.Distance(origin,knee.position),lower=Vector3.Distance(knee.position,foot.position);
            Vector3 line=target-origin;if(line.sqrMagnitude<.00001f||upper<.001f||lower<.001f)return;
            float d=Mathf.Clamp(line.magnitude,Mathf.Abs(upper-lower)+.002f,upper+lower-.002f);Vector3 dir=line.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(forward,dir).normalized;float along=(upper*upper+d*d-lower*lower)/(2*d);
            Vector3 k=origin+dir*along+bend*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            hip.rotation=Quaternion.FromToRotation(knee.position-origin,k-origin)*hip.rotation;
            knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,origin+dir*d-knee.position)*knee.rotation;
        }
    }
}


