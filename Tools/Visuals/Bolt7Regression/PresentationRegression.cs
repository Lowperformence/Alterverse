using System;
using System.Reflection;
using Alterverse.Visuals;
using Alterverse.Multiplayer;
using UnityEngine;

static class PresentationRegression
{
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static Bolt7Model Rig(Bolt7Visual visual)=>(Bolt7Model)typeof(Bolt7Visual).GetField("rig",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(visual);
    static void Tick(Bolt7Visual visual,float now){Time.time=Time.unscaledTime=now;Time.deltaTime=1f/60f;GameObject.Invoke(visual,"LateUpdate");}
    static void Main()
    {
        CoopAuthority.Active=true;
        var actor=new GameObject("remote Bolt");actor.AddComponent<CoopHeroSlot>();actor.AddComponent<Alterverse.Combat.Health>();Bolt7Visual.Create(actor);
        var visual=actor.GetComponent<Bolt7Visual>();var rig=Rig(visual);
        var headwear=actor.GetComponent<HeadwearPresentation>();
        Check(headwear!=null&&headwear.Root.name=="DF_VisualRoot"&&headwear.Root.parent==actor.transform&&headwear.Hero==Alterverse.Characters.HeroId.Bolt7,
            "Remote BOLT must initialize headwear against its actual visual root before a cosmetic snapshot arrives");
        actor.transform.position=new Vector3(.1f,0,0);Tick(visual,.016f);
        Check(rig.LastClip=="Walk","Grounded spawn idle cannot lock out the first walking frame");
        actor.transform.position=Vector3.zero;
        int walking=0;float previousVisible=0,maxVisibleStep=0;
        for(int frame=0;frame<120;frame++)
        {
            float now=3f+frame/60f;Time.unscaledTime=now;
            if(frame%3==0)
            {
                actor.transform.position=new Vector3(frame/60f*6f,0,0);
                visual.ReceivePresentationSnapshot(actor.transform.position,Quaternion.identity,false,false,now);
            }
            Tick(visual,now);
            float visible=actor.transform.position.x+rig.Root.transform.localPosition.x;
            if(frame>15){if(rig.LastClip=="Walk")walking++;maxVisibleStep=Math.Max(maxVisibleStep,Math.Abs(visible-previousVisible));}
            previousVisible=visible;
        }
        Check(walking==104,"Walking continues on every render frame between 20 Hz snapshots");
        Check(maxVisibleStep<.12f,"Render position advances smoothly instead of jumping once per packet");
        Check(Math.Abs(actor.transform.position.x-11.7f)<.001f,"Presentation never moves the authoritative gameplay root");
        for(int frame=0;frame<60;frame++)Tick(visual,5f+frame/60f);
        Check(rig.LastClip=="Idle","Packet silence settles to idle without inventing more travel");
        Time.unscaledTime=7f;actor.transform.position=new Vector3(40,0,0);
        visual.ReceivePresentationSnapshot(actor.transform.position,Quaternion.identity,true,false,7f);Tick(visual,7f);
        Check(rig.LastClip=="Idle"&&Math.Abs(rig.Root.transform.localPosition.x)<.001f,"Respawn/teleport resets the buffer instead of running across the arena");
        var owner=new GameObject("owner Bolt");owner.AddComponent<CoopHeroSlot>().IsLocalHuman=true;owner.AddComponent<Alterverse.Combat.Health>();Bolt7Visual.Create(owner);
        var ownerVisual=owner.GetComponent<Bolt7Visual>();Tick(ownerVisual,10f);owner.transform.position=new Vector3(.1f,0,0);
        Check(owner.GetComponent<HeadwearPresentation>()?.Root==Rig(ownerVisual).Root.transform,"Local BOLT skipped the shared headwear renderer");
        ownerVisual.ReceivePresentationSnapshot(new Vector3(99,0,0),Quaternion.identity,false,false,10f);Tick(ownerVisual,10.016f);
        Check(Rig(ownerVisual).LastClip=="Walk"&&Rig(ownerVisual).Root.transform.localPosition.x==0,"Local owner keeps immediate movement and ignores remote presentation snapshots");
        var preview=new GameObject("preview Bolt");var selection=preview.AddComponent<Alterverse.Shop.SkinPreviewSelection>();Bolt7Visual.Create(preview);
        var previewHat=preview.GetComponent<HeadwearPresentation>();
        Check(previewHat!=null&&ReferenceEquals(previewHat.Preview,selection)&&previewHat.Root==Rig(preview.GetComponent<Bolt7Visual>()).Root.transform,
            "BOLT preview selection must reach headwear after its rig has been created");
        Bolt7Visual.Create(preview);
        Check(ReferenceEquals(previewHat,preview.GetComponent<HeadwearPresentation>()),"Repeated BOLT Create duplicated/replaced the headwear renderer");
        CoopAuthority.Active=false;
        Console.WriteLine("PASS: "+checks+" BOLT-7 network gait/position/owner/relocation checks (controlled rig and engine).");
    }
}
