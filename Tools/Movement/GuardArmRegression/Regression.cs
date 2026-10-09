using System;
using UnityEngine;
using Alterverse.Visuals;

namespace UnityEngine
{
    public enum PrimitiveType { Sphere, Cube, Capsule, Cylinder }
    public struct Color { public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} }
    public class Material { }
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
    }
}
class Regression
{
    static int assertions;static bool observe;static float maxArmor,maxStep,maxArm,maxElbow;
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
        Check(maxArm>15&&maxElbow>15,"Arm articulation disappeared");
        Console.WriteLine("{0} assertions, 16 guard scenarios; shoulder deviation {1:F3} deg, largest frame jump {2:F3} deg; articulated arm {3:F3} deg, elbow {4:F3} deg",assertions,maxArmor,maxStep,maxArm,maxElbow);
        return 0;
    }
}
