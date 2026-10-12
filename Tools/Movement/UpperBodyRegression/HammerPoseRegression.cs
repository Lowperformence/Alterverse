using UnityEngine;
using Alterverse.Visuals;

partial class Regression
{
    static void HammerSweepRecovery()
    {
        foreach(bool shortened in new[]{false,true})
        {
            Rig r=Make(Alterverse.Characters.HeroId.Kaelon,false,shortened);
            r.motion.PlanarSpeed=0;r.motion.LocomotionWeight=0;
            var pose=r.model.gameObject.AddComponent<SiegeTankAppearance>();
            var grip=new GameObject("HammerGrip").transform;grip.parent=r.bones[9];
            var top=new GameObject("HammerTop").transform;top.parent=r.bones[9];top.localPosition=Vector3.up*.65f;
            var front=new GameObject("HammerFront").transform;front.parent=r.bones[9];front.localPosition=top.localPosition+Vector3.forward*.12f;
            pose.ConfigureMarkers(grip,top,front);CaptureGuard(r);
            Quaternion ready=r.bones[9].rotation;Vector3 readyPosition=r.bones[9].position;
            Vector3 fitted=r.grips[1].localPosition;Quaternion fittedRotation=r.grips[1].localRotation;
            r.melee.Begin(.22f);
            float duration=PrivateFloat(r.melee,"duration");
            var sweep=typeof(MeleeBasicAttackAnimation).GetMethod("ApplyHeavySweep",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int step=0;step<=60;step++)
            {
                float age=duration*step/60f;sweep.Invoke(r.melee,new object[]{age});
                Check(!float.IsNaN(r.bones[9].position.x),"Hammer sweep remains finite");
                SameV(fitted,r.grips[1].localPosition,"Sweep retains fitted palm contact");
                SameQ(fittedRotation,r.grips[1].localRotation,"Sweep retains grip attachment rotation");
                if(step==0||step==60)
                {
                    Near(Quaternion.Angle(ready,r.bones[9].rotation),0,.08f,"Sweep starts and finishes in the marked diagonal guard");
                    Near(Vector3.Distance(readyPosition,r.bones[9].position),0,.002f,"Sweep starts and finishes at the same hand position");
                }
            }
        }
    }

    static void HammerPoseCases()
    {
        var actor=new GameObject("Auren guard actor").transform;
        var hand=new GameObject("HandR").transform;
        hand.parent=actor;hand.localPosition=new Vector3(.35f,.9f,.3f);
        hand.localRotation=Quaternion.Euler(19,-34,52);
        var grip=new GameObject("HammerGrip").transform;grip.parent=hand;
        grip.localPosition=new Vector3(.02f,-.01f,.04f);
        var top=new GameObject("HammerTop").transform;top.parent=hand;
        top.localPosition=grip.localPosition+Vector3.up*.65f;
        var front=new GameObject("HammerFront").transform;front.parent=hand;
        front.localPosition=top.localPosition+Vector3.forward*.12f;
        var pose=actor.gameObject.AddComponent<SiegeTankAppearance>();
        pose.ConfigureMarkers(grip,top,front);
        Vector3 contact=grip.localPosition;Quaternion local=grip.localRotation;
        for(int turn=0;turn<360;turn++)
        {
            actor.rotation=Quaternion.Euler(8,turn,-5);
            // A fresh retargeted wrist precedes every production guard update.
            hand.localRotation=Quaternion.Euler(19,-34,52);
            pose.OrientHammerGuard(actor,hand);
            Vector3 shaft=(top.position-grip.position).normalized;
            Check(Vector3.Dot(shaft,actor.up)>.85f,"Hammer stays above its hand");
            Check(Vector3.Dot(shaft,actor.right)>.25f,"Hammer clears the head toward the weapon side");
            Check(Vector3.Dot(shaft,actor.forward)>.24f,"Hammer stays in front of the shoulder");
            Near(Vector3.Dot(shaft,SiegeTankAppearance.HammerGuardShaft(actor)),1,.00001f,"Fitted shaft matches shared recovery axis");
            // The presentation/preview runs this later alignment after its pose solver.
            // It used to rotate the new diagonal back into the old upright frame.
            pose.AlignGuard(actor,null,hand,1f);
            Near(Vector3.Dot((top.position-grip.position).normalized,SiegeTankAppearance.HammerGuardShaft(actor)),1,.00001f,"Preview final alignment retains the marked diagonal");
            Quaternion once=hand.rotation;pose.OrientHammerGuard(actor,hand);
            Near(Quaternion.Angle(once,hand.rotation),0,.08f,"Repeated hammer finalization has no feedback drift");
            SameV(contact,grip.localPosition,"Hammer contact preserved");
            SameQ(local,grip.localRotation,"Hammer grip child rotation preserved");
        }
        pose.OrientHammerGuard(null,hand);pose.OrientHammerGuard(actor,null);
        var empty=new GameObject("No hammer").AddComponent<SiegeTankAppearance>();
        Quaternion before=hand.rotation;empty.OrientHammerGuard(actor,hand);
        SameQ(before,hand.rotation,"Missing marker fallback leaves wrist unchanged");
        hand.localRotation=Quaternion.Euler(14,55,-23);
        Quaternion striking=hand.rotation;pose.AlignGuard(actor,null,hand,0f);
        SameQ(striking,hand.rotation,"Zero guard weight leaves the active strike wrist untouched");
    }
}
