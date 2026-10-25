using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Combat;
using Alterverse.Editor;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
static class Regression
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        var material=new Material(new Shader()){name="NativeAura"};material.SetColor("_Color",new Color(.1f,.6f,1));
        AssetDatabase.Assets["Assets/Epic Toon FX/Materials/Aura.mat"]=material;
        string nativePath="Assets/Epic Toon FX/Prefabs/Combat/Magic/Aura/MagicAuraBlue.prefab";
        var native=new GameObject("MagicAuraBlue");native.AddComponent<ParticleSystem>();
        native.AddComponent<ParticleSystemRenderer>().sharedMaterials=new Material[]{material,null};
        native.AddComponent<DemoController>();native.AddComponent<Collider>();native.AddComponent<AudioSource>();
        AssetDatabase.Assets[nativePath]=native;
        HotZoneSpawnAuraBuilder.Build();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/CombatVfx/HotZoneSpawnAura/Aura.prefab");
        var definition=prefab.GetComponent<HotZoneSpawnAura>();
        Check(definition.sourcePath==nativePath,"actual purchased Magic Aura selected");
        Check(!prefab.activeSelf&&definition.nativeRadius==6,"saved inactive and horizontal footprint measured");
        Check(prefab.GetComponentsInChildren<DemoController>(true).Length==0&&prefab.GetComponentsInChildren<Collider>(true).Length==0&&prefab.GetComponentsInChildren<AudioSource>(true).Length==0,"no demo, physics or unrequested audio");
        var prepared=prefab.GetComponentsInChildren<ParticleSystem>(true).Single();
        Check(!prepared.main.loop&&prepared.main.duration<=1.2f,"native loop becomes a spawn pulse");
        Check(native.GetComponent<ParticleSystem>().main.loop&&native.GetComponent<DemoController>()!=null,"purchased source untouched");
        int saves=AssetDatabase.Saves;HotZoneSpawnAuraBuilder.Build();Check(saves==AssetDatabase.Saves,"import idempotent");

        var renderer=native.GetComponent<ParticleSystemRenderer>();var system=native.GetComponent<ParticleSystem>();
        system.trails.enabled=true;bool failed=false;
        try{BomberNukeBuilder.ConvertRendererMaterials(renderer,new Shader(),new(),"Assets/Prepared",nativePath);}catch(InvalidOperationException e){failed=e.Message.Contains("trail material 1")&&e.Message.Contains(nativePath);}
        Check(failed,"enabled trail without material remains a source-specific error");system.trails.enabled=false;

        Resources.Prefab=prefab;var world=new GameObject("DF_MatchWorld");CoopAuthority.Active=true;
        HotZoneTank.PrepareForMatch(world.transform);
        Check(HotZoneTank.Constructions==4,"two independently built actors reserved for either winning team");
        Check(HotZoneTank.Active.Count==0&&Health.Active.Count==0,"preparation never advertises actors to gameplay or network registries");
        Check(!HotZoneTank.FinishMatchPreparation(),"loading remains visible while detail managers finish");
        Time.unscaledTime=1;Check(HotZoneTank.FinishMatchPreparation(),"loading completes after detail warmup");
        var reserved=world.GetComponentsInChildren<HotZoneTank>(true);
        Check(reserved.All(t=>!t.gameObject.activeInHierarchy&&t.IsReservedReward),"all complete actors stay inactive before objective resolution");
        int loads=Resources.Loads,objects=GameObject.All.Count;
        Check(HotZoneTank.Spawn(0,0)&&HotZoneTank.Spawn(0,1),"same team can win two objectives");
        Check(HotZoneTank.Active.Count==2&&!HotZoneTank.Spawn(1,2),"host retains two-living-monster cap");
        Check(HotZoneTank.Constructions==4&&GameObject.All.Count==objects&&Resources.Loads==loads,"spawn does no actor construction, instantiation or resource loading");
        Check(HotZoneTank.Active.All(t=>t.TankHealth.Current==476&&!t.TankHealth.DestroyOnDeath),"prepared actors spawn with full original health");
        Check(HotZoneTank.Active.Select(t=>t.GetComponent<CoopWorldEntityKey>().Key).Distinct().Count()==2,"occurrences retain distinct network identities");
        Check(reserved.All(t=>t.gameObject.GetComponentsInChildren<CoopWorldEntityKey>(true).Length==1),"reuse does not add duplicate identity components");
        var auras=world.GetComponentsInChildren<HotZoneSpawnAura>(true);
        Check(auras.Count(a=>a.gameObject.activeInHierarchy)==2,"one aura per simultaneous monster spawn");
        Check(auras.All(a=>a.transform.position.y==2.04f&&a.transform.localScale.x==1),"aura placed on ground and scaled to measured monster width");
        var actor=HotZoneTank.Active[0];Vector3 fixedPoint=auras[0].transform.position;actor.transform.position=new Vector3(22,3,14);
        Check(auras[0].transform.position.x==fixedPoint.x&&auras[0].transform.position.z==fixedPoint.z,"aura stays at spawn point while monster moves");
        foreach(var aura in auras){foreach(var ps in aura.GetComponentsInChildren<ParticleSystem>(true))ps.Playing=false;Time.time+=20;GameObject.Invoke(aura,"LateUpdate");}
        Check(auras.All(a=>!a.gameObject.activeSelf&&!a.gameObject.Destroyed),"aura returns to its reserve rather than being destroyed");
        foreach(var t in HotZoneTank.Active.ToArray())HotZoneTank.ReturnToRewardPool(t.gameObject);
        for(int occurrence=2;occurrence<22;occurrence++)
        {
            Check(HotZoneTank.Spawn(occurrence%2,occurrence),"later objective reuses available prepared actor");
            var t=HotZoneTank.Active.Single();Check(t.GetComponent<CoopWorldEntityKey>().Key==HotZoneRewardKey.Create(occurrence,occurrence%2),"reuse replaces previous identity");
            HotZoneTank.ReturnToRewardPool(t.gameObject);
        }
        Check(HotZoneTank.Constructions==4&&Resources.Loads==loads,"later objectives do not reintroduce construction/loading spikes");

        foreach(var aura in auras){foreach(var ps in aura.GetComponentsInChildren<ParticleSystem>(true))ps.Playing=false;Time.time+=20;GameObject.Invoke(aura,"LateUpdate");}
        CoopAuthority.LocalHasServerAuthority=false;
        Check(!HotZoneTank.Spawn(1,100),"client cannot create authoritative reward");
        var replica=HotZoneTank.EnsureNetworkReplica(HotZoneRewardKey.Create(100,1));
        Check(replica!=null&&!auras.Any(a=>a.gameObject.activeSelf),"replica waits for actual authoritative location before aura");
        var identity=replica.GetComponent<CoopWorldEntityKey>();identity.ApplyTarget(new Vector3(7,.95f,-9),0,false);
        var clientAura=auras.Single(a=>a.gameObject.activeSelf);
        Check(clientAura.transform.position.x==7&&clientAura.transform.position.z==-9,"client aura uses first host sample location");
        int visible=auras.Count(a=>a.gameObject.activeSelf);identity.ApplyTarget(new Vector3(8,.95f,-9),0,true);
        Check(auras.Count(a=>a.gameObject.activeSelf)==visible,"later host corrections do not replay spawn aura");
        Check(ReferenceEquals(replica,HotZoneTank.EnsureNetworkReplica(HotZoneRewardKey.Create(100,1))),"repeated snapshots do not duplicate replica");
        Check(HotZoneTank.ReturnToRewardPool(replica.gameObject)&&!replica.gameObject.Destroyed,"network removal preserves reusable actor");
        Console.WriteLine($"PASS {checks} Hot Zone reserve/authority/network identity/spawn location/aura source/material/lifecycle checks. Controlled substitutes; Unity rendering and device profiling remain separate.");
    }
}
