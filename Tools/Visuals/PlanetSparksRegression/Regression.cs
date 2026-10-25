using System;
using System.Linq;
using Alterverse.Editor;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
static class Regression
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        const string path="Assets/Epic Toon FX/Prefabs/Environment/Sparks/Sparks.prefab";
        var native=new GameObject("Sparks");native.transform.localScale=Vector3.one*2f;
        var ps=native.AddComponent<ParticleSystem>();
        var material=new Material(new Shader()){name="AuthoredSparks"};material.SetColor("_Color",new Color(1f,.5f,.1f,.7f));
        AssetDatabase.Assets["Assets/Epic Toon FX/Materials/Sparks.mat"]=material;
        native.AddComponent<ParticleSystemRenderer>().sharedMaterials=new Material[]{material,null};
        native.AddComponent<DemoController>();native.AddComponent<AudioSource>();native.AddComponent<Collider>();native.SetActive(false);
        AssetDatabase.Assets[path]=native;
        AssetDatabase.Assets["Assets/Epic Toon FX/Prefabs/2D Alternate/Sparks.prefab"]=new GameObject("2D Sparks");
        PlanetAttackSparksBuilder.Build();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/CombatVfx/PlanetAttackSparks/Sparks.prefab");
        var definition=prefab.GetComponent<PlanetAttackSparksVfx>();
        Check(definition.sourcePath==path,"exact native Sparks, excluding 2D/demo alternatives");
        Check(!prefab.activeSelf,"prepared effect does not emit before placement");
        Check(prefab.GetComponentsInChildren<AudioSource>(true).Length==0&&prefab.GetComponentsInChildren<Collider>(true).Length==0&&prefab.GetComponentsInChildren<DemoController>(true).Length==0,"decoration cannot add audio, physics or demo gameplay");
        var prepared=prefab.GetComponentsInChildren<ParticleSystem>(true).Single();
        Check(prepared.main.startLifetime==ps.main.startLifetime&&prepared.main.startSpeed==ps.main.startSpeed&&prepared.main.maxParticles==ps.main.maxParticles,"native particle detail, speed and lifetime retained");
        Check(!prepared.main.loop&&prepared.main.scalingMode==ParticleSystemScalingMode.Hierarchy,"one native composition scales and cleans up as a unit");
        Check(prepared.transform.localScale.x==2f,"authored composition scale remains intact inside wrapper");
        Check(!native.activeSelf&&ps.main.loop&&native.GetComponent<DemoController>()!=null,"purchased source untouched");
        Check(prefab.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials[1]==null,"optional empty material slots remain valid");
        int saves=AssetDatabase.Saves;PlanetAttackSparksBuilder.Build();Check(AssetDatabase.Saves==saves,"repeated preparation is idempotent");
        Check(!PlanetAttackSparksVfx.TryPlay(new()),"missing preparation cannot change existing impact behavior");
        Resources.Prefab=prefab;var world=new GameObject("DF_MatchWorld");PlanetAttackSparksVfx.Prepare(world.transform);
        int objects=GameObject.All.Count,audio=Alterverse.Audio.DimensionAudio.Calls;
        for(int i=0;i<32;i++)
        {
            var point=new Vector3(i,.3f,-i);Check(PlanetAttackSparksVfx.TryPlay(point),"simultaneous planet impacts have reserved Sparks");
            var effect=world.GetComponentsInChildren<PlanetAttackSparksVfx>(true).Single(e=>e.gameObject.activeSelf&&e.transform.position.x==i);
            Check(effect.transform.localScale.x==8f&&effect.transform.localScale.y==8f&&effect.transform.localScale.z==8f,"another +100 percent doubles the previous four-times scale");
            Check(effect.transform.position.y==.3f&&effect.transform.position.z==-i,"Sparks appears at actual impact point");
        }
        Check(GameObject.All.Count==objects,"impact playback instantiates no new objects");
        Check(Alterverse.Audio.DimensionAudio.Calls==audio,"Sparks adds no sound");
        Check(!PlanetAttackSparksVfx.TryPlay(new()),"saturated cosmetic pool leaves original impact untouched");
        Time.time=20;
        foreach(var effect in world.GetComponentsInChildren<PlanetAttackSparksVfx>(true))
        {
            foreach(var system in effect.GetComponentsInChildren<ParticleSystem>(true))system.Age=20;
            GameObject.Invoke(effect,"LateUpdate");
            Check(!effect.gameObject.activeSelf&&!effect.gameObject.Destroyed,"completed Sparks returns to pool");
        }
        Check(PlanetAttackSparksVfx.TryPlay(new Vector3(9,.3f,5))&&GameObject.All.Count==objects,"later impacts reuse prepared Sparks");
        Console.WriteLine($"PASS {checks} native planet Sparks source/additive-hook/scale/audio-exclusion/pool/lifecycle checks. Controlled fixtures; Unity appearance and device acceptance remain separate.");
    }
}
