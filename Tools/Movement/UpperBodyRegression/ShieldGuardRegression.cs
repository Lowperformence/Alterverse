using UnityEngine;
using Alterverse.Visuals;

partial class Regression
{
    static void ShieldGuardCases()
    {
        var actor=new GameObject("Auren shield actor").transform;
        var hand=new GameObject("HandL").transform;
        hand.parent=actor;hand.localRotation=Quaternion.Euler(13,-24,8);
        var center=new GameObject("ShieldCenter").transform;center.parent=hand;
        var top=new GameObject("ShieldTop").transform;top.parent=hand;top.localPosition=Vector3.up;
        var front=new GameObject("ShieldFront").transform;front.parent=hand;front.localPosition=Vector3.forward;
        var guard=new ShieldGuardFrame();
        Check(guard.Orient(actor,hand,center,top,front),"Shield guard calibrates fitted wrist");
        Quaternion fitted=hand.rotation;
        foreach(int fps in new[]{30,60,120})for(int frame=0;frame<fps*8;frame++)
        {
            float t=frame/(float)fps;
            actor.rotation=Quaternion.Euler(0,t*29,0);
            hand.localRotation=Quaternion.Euler(Mathf.Sin(t)*1.2f,Mathf.Cos(t)*.7f,0);
            // Skin/pose refreshes may change the live marker frame before final wrist alignment.
            top.localPosition=Vector3.up+Vector3.right*(Mathf.Sin(t*3)*.025f);
            front.localPosition=Vector3.forward+Vector3.up*(Mathf.Cos(t*2)*.025f);
            Check(guard.Orient(actor,hand,center,top,front),"Shield guard stays valid during body refresh");
            Near(Quaternion.Angle(hand.rotation,actor.rotation*fitted),0,.08f,"Shield orientation independent of breathing and old correction");
            Quaternion once=hand.rotation;guard.Orient(actor,hand,center,top,front);
            Near(Quaternion.Angle(once,hand.rotation),0,.08f,"Repeated shield finalization is idempotent");
        }
        Check(!guard.Orient(null,hand,center,top,front),"Missing actor is safe");
        var invalid=new ShieldGuardFrame();top.localPosition=front.localPosition;
        Check(!invalid.Orient(actor,hand,center,top,front),"Collinear shield markers do not create an invalid quaternion");
    }
}
