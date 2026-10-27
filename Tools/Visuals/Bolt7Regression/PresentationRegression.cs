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
    static void MovingFire(bool online,bool localHuman,bool server)
    {
        CoopAuthority.Active=online;CoopAuthority.LocalHasServerAuthority=server;
        var actor=new GameObject("walk and fire");actor.AddComponent<CoopHeroSlot>().IsLocalHuman=localHuman;
        var health=actor.AddComponent<Alterverse.Combat.Health>();Bolt7Visual.Create(actor);
        var visual=actor.GetComponent<Bolt7Visual>();var rig=Rig(visual);
        bool remote=online&&!server&&!localHuman;
        float clock=20f;
        for(int frame=0;frame<90;frame++)
        {
            clock=20f+frame/60f;Time.time=Time.unscaledTime=clock;
            if(!remote||frame%3==0)
            {
                actor.transform.position=new Vector3(frame*.1f,0,0);
                if(remote)visual.ReceivePresentationSnapshot(actor.transform.position,Quaternion.identity,false,false,clock);
            }
            if(frame%6==0)visual.Shot();
            Tick(visual,clock);
            if(frame>12)Check(rig.BaseClip=="Walk"&&rig.LastClip=="Fire_Loop"&&rig.UpperBodyOnly,
                "Sustained shots preserve walking every rendered frame, online and offline");
        }
        Check(Math.Abs(actor.transform.position.x-(remote?8.7f:8.9f))<.001f,"Firing presentation cannot move the gameplay root");
        if(remote){Time.time=Time.unscaledTime=clock+.01f;visual.ReceivePresentationSnapshot(actor.transform.position,Quaternion.identity,true,false,clock+.01f);}
        for(int frame=1;frame<=60;frame++)
        {
            float now=clock+frame/60f;Time.time=now;visual.Shot();Tick(visual,now);
        }
        Check(rig.LastClip=="Fire_Loop"&&!rig.UpperBodyOnly,"Stationary firing retains its authored full-body pose");
        Tick(visual,clock+2f);Check(rig.LastClip=="Idle","Stopping fire and movement restores idle");
        Time.time=clock+2.01f;visual.Skill(0);visual.Shot();actor.transform.position+=Vector3.right*.1f;Tick(visual,clock+2.02f);
        Check(rig.LastClip=="Grenade"&&!rig.UpperBodyOnly,"Skills retain full-body priority");
        health.Die();Tick(visual,clock+2.03f);Check(rig.LastClip=="Death"&&!rig.UpperBodyOnly,"Death overrides walking and firing");
        health.Restore();Tick(visual,clock+2.04f);Check(rig.LastClip=="Idle","Respawn clears the mixed firing pose");
    }
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
        MovingFire(false,true,false);
        MovingFire(true,true,false);
        MovingFire(true,false,false);
        MovingFire(true,false,true);
        Console.WriteLine("PASS: "+checks+" BOLT-7 network gait/position/owner/relocation checks (controlled rig and engine).");
    }
}
