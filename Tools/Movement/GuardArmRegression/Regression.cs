using System;
using UnityEngine;
using Alterverse.Visuals;

namespace UnityEngine
{
    public enum PrimitiveType { Sphere, Cube, Capsule, Cylinder }
    public struct Color { public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} }
    public class Material { }
    public class Mesh { }
    public class Collider:Component { }
    public class Renderer:Component { public Material sharedMaterial; }
}
namespace Alterverse.Match { public static class ArenaVariantRuntime { public static bool IsKryon; } }
namespace Alterverse.Combat
{
    public partial class HealingGuardMech
    {
        public static void Arms(Transform root)
        {foreach(float side in new[]{-1f,1f})BuildArm(root,side,true,new Color(),new Color(),new Color(),new Color());}
        // Geometry/material construction is replaced; production names, offsets and rotations are used.
        static void CreatePart(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color,Quaternion rot)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.transform.localRotation=rot;go.AddComponent<Renderer>();}
        static void CreateEnergyPart(Transform p,string n,PrimitiveType t,Vector3 pos,Vector3 s,Color c,Quaternion q)
        {CreatePart(p,n,t,pos,s,c,q);}
    }
}
namespace Alterverse.Visuals
{
    // Only mesh/material creation is replaced; production planetary arm hierarchy is extracted unchanged.
    public class GuardGeometry
    {
        protected static Transform Pivot(string name,Transform parent,Vector3 pos,Quaternion rot)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;t.localRotation=rot;return t;}
        protected static Transform Part(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material material,Quaternion rot,bool energy)
        {var t=Pivot(name,parent,pos,rot);t.localScale=scale;return t;}
        protected static Transform MeshPart(string name,Mesh mesh,Transform parent,Vector3 pos,Vector3 scale,Material material,Quaternion rot)
        {return Part(name,PrimitiveType.Cube,parent,pos,scale,material,rot,false);}
        protected static Transform Rock(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,Vector3 rot)
        {return Part(name,PrimitiveType.Cube,parent,pos,scale,material,Quaternion.Euler(rot.x,rot.y,rot.z),false);}
        protected static Transform Crack(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,Vector3 rot)
        {return Rock(name,parent,pos,scale,material,rot);}
    }
    public partial class GreenAlienMechVisual
    {
        public void Arms(Transform root)
        {foreach(float side in new[]{-1f,1f})BuildArm(root,side,true,null,null,null,null,null,null,null);}
    }
    public partial class MarsStoneGolemMechVisual
    {
        public void Arms(Transform root)
        {foreach(float side in new[]{-1f,1f})BuildArm(root,side,true,null,null,null,null,null,null,null);}
    }
    public sealed partial class LaneUnitVisualMotion
    {
        public void Prepare()
        {
            configured=robot=true;visualRoot=new GameObject("DF_LocomotionPose").transform;visualRoot.SetParent(transform,false);
            for(int i=transform.childCount-1;i>=0;i--){var child=transform.GetChild(i);if(child!=visualRoot)child.SetParent(visualRoot,true);}
            BindRobotParts();BuildAttackChain(ref armL,-1,out attackElbowL,out attackHandL);BuildAttackChain(ref armR,1,out attackElbowR,out attackHandR);
            CaptureRestPose();lastPosition=transform.position;
        }
        public void Tick(){LateUpdate();}
        public Transform Part(string n){return FindDeep(n);}
        public Transform Root=>visualRoot;
        public Transform Wrist(bool left)=>left?attackHandL:attackHandR;
    }
}
class Regression
{
    static int assertions;static bool observe;static float maxArmor,maxStep,maxArm,maxElbow;
    static float maxWristDrift,maxWristStep;
    static void Check(bool c,string why){assertions++;if(!c)throw new Exception(why);}
    static void Run(int fps,int scenario)
    {
        Time.time=0;Time.deltaTime=1f/fps;Time.timeScale=1;
        var actor=new GameObject("DF_ZeroHealingGuardMech");actor.transform.localScale=Vector3.one*1.02f;
        var names=new[]{"DF_GuardShoulderArmorL","DF_GuardShoulderCapL","DF_GuardShoulderArmorR","DF_GuardShoulderCapR"};
        Alterverse.Combat.HealingGuardMech.Arms(actor.transform);
        var initialPositions=new Vector3[names.Length];var initialRotations=new Quaternion[names.Length];
        for(int i=0;i<names.Length;i++){var part=actor.transform.Find(names[i]);initialPositions[i]=part.position;initialRotations[i]=part.rotation;}
        var motion=actor.AddComponent<LaneUnitVisualMotion>();motion.Prepare();
        var armor=new Transform[names.Length];var positions=new Vector3[names.Length];var rotations=new Quaternion[names.Length];var previous=new Quaternion[names.Length];
        for(int i=0;i<names.Length;i++)
        {
            armor[i]=motion.Part(names[i]);positions[i]=motion.Root.InverseTransformPoint(armor[i].position);rotations[i]=previous[i]=Quaternion.Inverse(motion.Root.rotation)*armor[i].rotation;
            Check(Vector3.Distance(armor[i].position,initialPositions[i])<.0001f&&Quaternion.Angle(armor[i].rotation,initialRotations[i])<.05f,"Binding changed authored shoulder pose");
            if(!observe)Check(armor[i].parent==motion.Root,"Shoulder plate is still in the moving limb chain");
        }
        var arm=motion.Part("DF_GuardArmMotionR");var elbow=motion.Part("DF_GuardElbowMotionR");var hand=motion.Part("DF_GuardHandR");
        float a=Vector3.Distance(arm.position,elbow.position),b=Vector3.Distance(elbow.position,hand.position);
        Check(hand.IsChildOf(elbow),"Hand disconnected from forearm");
        for(int frame=0;frame<fps*20;frame++)
        {
            Time.time=frame*Time.deltaTime;
            if(scenario==1||scenario==3)actor.transform.position+=Vector3.forward*.9f*Time.deltaTime;
            if(scenario==2||scenario==3)actor.transform.rotation=Quaternion.Euler(0,Time.time*95,0);
            if(scenario>=2 && frame%(fps*2)==0)motion.PulseAttack(.18f);
            var rootPos=actor.transform.position;var rootRot=actor.transform.rotation;var rootScale=actor.transform.localScale;
            motion.Tick();
            Check(Vector3.Distance(actor.transform.position,rootPos)<.00001f && Quaternion.Angle(actor.transform.rotation,rootRot)<.01f && Vector3.Distance(actor.transform.localScale,rootScale)<.00001f,"Pose wrote gameplay root");
            Check(Math.Abs(Vector3.Distance(arm.position,elbow.position)-a)<.0001f,"Upper arm stretched");
            Check(Math.Abs(Vector3.Distance(elbow.position,hand.position)-b)<.0001f,"Forearm stretched");
            maxArm=Math.Max(maxArm,Quaternion.Angle(arm.localRotation,Quaternion.identity));maxElbow=Math.Max(maxElbow,Quaternion.Angle(elbow.localRotation,Quaternion.identity));
            for(int i=0;i<armor.Length;i++)
            {
                var current=Quaternion.Inverse(motion.Root.rotation)*armor[i].rotation;
                float angle=Quaternion.Angle(rotations[i],current);maxArmor=Math.Max(maxArmor,angle);maxStep=Math.Max(maxStep,Quaternion.Angle(previous[i],current));previous[i]=current;
                if(!observe){Check(angle<.05f,"Shoulder plate spins with attack IK: "+names[i]+" angle="+angle);Check(Vector3.Distance(positions[i],motion.Root.InverseTransformPoint(armor[i].position))<.0001f,"Shoulder plate orbits arm hinge");}
            }
        }
        actor.transform.position+=new Vector3(40,0,0);motion.Tick();
        Check(Quaternion.Angle(arm.localRotation,Quaternion.identity)<.05f,"Teleport did not restore arm");
        Time.timeScale=0;var pause=arm.localRotation;motion.Tick();Check(Quaternion.Angle(arm.localRotation,pause)<.05f,"Paused arm changed");Time.timeScale=1;
    }
    static int Main(string[] args)
    {
        observe=args.Length>0;
        foreach(int fps in new[]{24,30,60,120})for(int scenario=0;scenario<4;scenario++)Run(fps,scenario);
        foreach(int fps in new[]{24,30,60,120})foreach(int kind in new[]{1,2,0})foreach(bool gun in new[]{false,true})RunWrists(fps,kind,gun);
        Check(maxArm>15&&maxElbow>15,"Arm articulation disappeared");
        Console.WriteLine("{0} assertions, 16 guard scenarios; shoulder deviation {1:F3} deg, largest frame jump {2:F3} deg; articulated arm {3:F3} deg, elbow {4:F3} deg",assertions,maxArmor,maxStep,maxArm,maxElbow);
        Console.WriteLine("24 additional wrist scenarios: Moon/Green/Mars, with/without gun, 24/30/60/120 FPS; idle drift {0:F3} deg, idle frame jump {1:F3} deg",maxWristDrift,maxWristStep);
        return 0;
    }

    static void RunWrists(int fps,int kind,bool withGun)
    {
        Time.time=0;Time.deltaTime=1f/fps;Time.timeScale=1;
        var actor=new GameObject("GuardWristRegression");
        if(kind==0)Alterverse.Combat.HealingGuardMech.Arms(actor.transform);
        else
        {
            string prefix=kind==1?"DF_GreenMech":"DF_MarsGolem";
            foreach(string side in new[]{"L","R"})new GameObject(prefix+"Leg"+side).transform.SetParent(actor.transform,false);
            if(kind==1)new GreenAlienMechVisual().Arms(actor.transform);else new MarsStoneGolemMechVisual().Arms(actor.transform);
        }
        var motion=actor.AddComponent<LaneUnitVisualMotion>();motion.Prepare();
        var hands=new[]{motion.Wrist(true),motion.Wrist(false)};
        Check(hands[0]!=null&&hands[1]!=null,"Both guard wrists must bind");
        var rest=new[]{hands[0].localRotation,hands[1].localRotation};
        if(withGun){var gun=hands[1].gameObject.AddComponent<MechMachineGunVisual>();gun.StableHoldReady=true;gun.HoldingHandPosition=hands[1].position;}
        motion.Tick();
        var expected=new[]{hands[0].rotation,hands[1].rotation};var previous=(Quaternion[])expected.Clone();
        for(int frame=1;frame<fps*8;frame++)
        {
            Time.time=frame*Time.deltaTime;motion.Tick();
            for(int side=0;side<2;side++)
            {
                float drift=Quaternion.Angle(expected[side],hands[side].rotation);
                maxWristDrift=Math.Max(maxWristDrift,drift);maxWristStep=Math.Max(maxWristStep,Quaternion.Angle(previous[side],hands[side].rotation));previous[side]=hands[side].rotation;
                if(!observe)Check(drift<.1f,"Stationary guard wrist spins: kind="+kind+" gun="+withGun+" fps="+fps+" side="+side+" drift="+drift);
            }
        }
        for(int frame=0;frame<fps*6;frame++)
        {
            Time.time+=Time.deltaTime;actor.transform.position+=actor.transform.forward*.9f*Time.deltaTime;
            actor.transform.rotation=Quaternion.Euler(0,Time.time*95,0);
            if(frame%fps==0)motion.PulseAttack(.18f);
            var root=actor.transform.rotation;motion.Tick();
            Check(Quaternion.Angle(root,actor.transform.rotation)<.01f,"Wrist correction wrote actor rotation");
        }
        // Repeat an identical settled pose after motion/attacks; its orientation must have no history.
        for(int frame=0;frame<fps*2;frame++){Time.time+=Time.deltaTime;motion.Tick();}
        expected=new[]{hands[0].rotation,hands[1].rotation};
        for(int frame=0;frame<fps;frame++)
        {
            Time.time+=Time.deltaTime;motion.Tick();
            for(int side=0;side<2;side++)if(!observe)Check(Quaternion.Angle(expected[side],hands[side].rotation)<.1f,"Guard wrist drifts after attacks/turns");
        }
        actor.transform.position+=Vector3.right*40;motion.Tick();
        if(!observe)for(int side=0;side<2;side++)Check(Quaternion.Angle(hands[side].localRotation,rest[side])<.1f,"Relocation must restore authored wrist pose");
        Time.timeScale=0;var paused=new[]{hands[0].rotation,hands[1].rotation};motion.Tick();
        for(int side=0;side<2;side++)Check(Quaternion.Angle(paused[side],hands[side].rotation)<.1f,"Pause changed wrist pose");
        Time.timeScale=1;
    }
}
