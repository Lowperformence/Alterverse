using System;
using System.Linq;
using UnityEngine;
using Alterverse.Multiplayer;

namespace Alterverse.Multiplayer
{
    public sealed partial class CoopHeroStateSync
    {
        private readonly CoopHeroSlot[] slots=new CoopHeroSlot[MatchSession.MaxPlayers];
        private CoopMatchPlan slotPlan;
        private float nextRefresh;
        private int localSlot=-1;
        private string localPlayerId="";
        public CoopHeroSlot[] SceneActors=Array.Empty<CoopHeroSlot>();
        public int SceneScans;
        private T[] FindObjectsByType<T>(FindObjectsInactive value)
        { SceneScans++;return SceneActors.Cast<T>().ToArray(); }
        public void Refresh(bool immediate=false)=>RefreshSlots(immediate);
        public void RemoveCached(int slot)=>slots[slot]=null;
        public CoopHeroSlot Cached(int slot)=>slots[slot];
        public int Local=>localSlot;
        public string LocalId=>localPlayerId;
    }
}
class Regression
{
    static int checks;
    static void Check(bool value,string label)
    {checks++;if(!value)throw new Exception(label);}
    static CoopHeroSlot Actor(int index,bool local=false)
    {
        var actor=new GameObject().AddComponent<CoopHeroSlot>();
        actor.Slot=index;actor.IsLocalHuman=local;actor.PlayerId="p"+index;
        return actor;
    }
    static void Main()
    {
        CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;
        var sync=new CoopHeroStateSync();
        sync.SceneActors=Enumerable.Range(0,8).Select(i=>Actor(i,i==3)).ToArray();
        sync.Refresh(true);
        Check(sync.SceneScans==1&&sync.Local==3&&sync.LocalId=="p3","initial roster discovers local ownership");
        for(int i=0;i<1200;i++){sync.Refresh();Check(sync.SceneScans==1,"stable ten-minute round never repeats the global scene scan");}
        foreach(var actor in sync.SceneActors)
            Check(actor.GetComponent<CoopHeroReplicaPresentation>()!=null,"clients retain replica presentation");
        sync.SceneActors[5]=Actor(5);sync.RemoveCached(5);sync.Refresh();
        Check(sync.SceneScans==2&&sync.Cached(5)==sync.SceneActors[5],"missing/replaced actor is rediscovered");
        sync.SceneActors[6].Slot=7;sync.Refresh();
        Check(sync.SceneScans==3&&sync.Cached(6)==null,"slot reassignment invalidates cache");
        sync.SceneActors[6]=Actor(6);sync.Refresh();
        Check(sync.SceneScans==4&&sync.Cached(6)==sync.SceneActors[6],"incomplete roster retries until complete");
        CoopAuthority.ActivePlan=new CoopMatchPlan();
        sync.SceneActors=Enumerable.Range(0,8).Select(i=>Actor(i,i==1)).ToArray();
        sync.Refresh();
        Check(sync.SceneScans==5&&sync.Local==1&&sync.LocalId=="p1","new round refreshes complete old roster");
        sync.Refresh(true);Check(sync.SceneScans==6,"explicit refresh is honored");
        CoopAuthority.LocalHasServerAuthority=true;
        var server=new CoopHeroStateSync{SceneActors=Enumerable.Range(0,8).Select(i=>Actor(i)).ToArray()};
        server.Refresh(true);
        foreach(var actor in server.SceneActors)
            Check(actor.GetComponent<CoopHeroReplicaPresentation>()==null,"server does not create client renderer-cache components");
        CoopAuthority.Active=false;server.Refresh(true);
        Check(server.SceneScans==1,"inactive match never scans");
        Console.WriteLine($"PASS: {checks} roster cache checks; stable round scene scans: 1 instead of 1201. Native Unity/FPS untested.");
    }
}
