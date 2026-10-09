using System;
using UnityEngine;

partial class PlanHarness {
 static void Near(Vector3 a,Vector3 b,string what){if(Vector3.Distance(a,b)>.000001f)throw new Exception(what);}
 static void Assert(bool b,string what){if(!b)throw new Exception(what);}
 static void Main(){int count=0;foreach(float side in new[]{-1f,1f}){
 var h=new PlanHarness();var hip=new GameObject("hip").transform;hip.position=new Vector3(side*.15f,1f,0);var foot=new GameObject("foot").transform;
 var leg=new Leg{hip=hip,foot=foot,length=1f,rest=new Vector3(side*.15f,.05f,0),sole=.05f,restRotation=Quaternion.identity};
 var offset=new Vector3(0,0,.15f);var hint=new Vector3(side*.15f,.5f,.3f);var rotation=Quaternion.Euler(13,27,8);
 h.PlanAuthored(leg,offset,hint,true,rotation,.99f);Vector3 anchor=leg.target;
 h.motion.PresentedPosition=new Vector3(.04f,0,.06f);hip.position+=h.motion.PresentedPosition;
 h.PlanAuthored(leg,new Vector3(0,0,.08f),hint,true,rotation,0f);Near(anchor,leg.target,"contact anchor moved through phase wrap");count++;
 h.PlanAuthored(leg,new Vector3(0,.12f,-.1f),hint,false,rotation,.25f);Near(leg.target,h.motion.PresentedPosition+new Vector3(side*.15f,.17f,-.1f),"swing source height/offset lost");count++;
 h.PrepareFoot(leg,.5f,true);Assert(Quaternion.Angle(foot.rotation,rotation)<.001f,"source foot rotation lost on level floor");count++;
 h.PlanAuthored(leg,new Vector3(0,0,.15f),hint,true,rotation,.6f);Near(leg.target,h.motion.PresentedPosition+new Vector3(side*.15f,.05f,.15f),"new contact did not land at sampled target");count++;
 var before=leg.target;h.Plan(leg,0,.5f,0,Vector3.forward,false,.001f);Assert(!leg.authoredSample&&leg.contact,"stop failed to restore fallback");Assert(Vector3.Distance(leg.target,before)<.003f,"stop snapped instead of settling");count++;
 for(int i=0;i<500;i++)h.Plan(leg,0,.5f,0,Vector3.forward,false,.016f);Near(leg.target,h.motion.PresentedPosition+new Vector3(side*.15f,.05f,0),"stop failed to settle");count++;
 }
 Console.WriteLine("PASS exact production PlanAuthored/Plan/PrepareFoot methods: "+count+" tests across both legs. Ground plane/model math stubs, not Unity.");}
}
