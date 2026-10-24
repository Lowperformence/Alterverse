using System;
using System.Linq;
using System.Reflection;
using Alterverse.Editor;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

static class Regression
{
    private static int checks;
    private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    private static void Reset()=>typeof(BomberNukeVfx).GetMethod("ResetStatic",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static void Main()
    {
        var original=new GameObject("Native cone");var ps=original.AddComponent<ParticleSystem>();
        original.AddComponent<AudioSource>();original.AddComponent<Collider>();original.AddComponent<DemoController>();
        var material=new Material(new Shader{name="Native additive"}){name="Native fire"};
        var texture=new Texture{name="Native texture"};material.SetTexture("_MainTex",texture);
        material.SetColor("_TintColor",new Color(1,.35f,.08f,.65f));material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",1);
        original.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{material};
        AssetDatabase.Assets[BomberNukeBuilder.Source]=original;AssetDatabase.Assets["Native.mat"]=material;
        BomberNukeBuilder.Build();
        const string generated="Assets/Alterverse/Resources/CombatVfx/BomberNuke/Explosion.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(generated);var definition=prefab.GetComponent<BomberNukeVfx>();
        Check(prefab!=null&&!prefab.activeSelf,"prepared prefab must not emit before runtime placement");
        Check(definition.nativeRadius==6,"native height must not shrink the horizontal footprint");
        Check(BomberNukeBuilder.Source.Contains("NukeConeExplosionFire.prefab")&&!BomberNukeBuilder.Source.Contains("2D"),"exact purchased 3D source");
        Check(prefab.GetComponentsInChildren<AudioSource>(true).Length==0,"keep original external bomb sound only");
        Check(prefab.GetComponentsInChildren<Collider>(true).Length==0,"no cosmetic collision gameplay");
        Check(prefab.GetComponentsInChildren<DemoController>(true).Length==0,"no demo logic");
        var prepared=prefab.GetComponentsInChildren<ParticleSystem>(true).Single();
        Check(prepared.main.maxParticles==321,"native detail count is retained");
        Check(prepared.main.startLifetime==7&&prepared.main.startSpeed==8,"authored lifetime and speed retained");
        Check(prepared.subEmitters.enabled,"authored sub-emitters retained");
        Check(!prepared.main.loop&&prepared.main.stopAction==ParticleSystemStopAction.None,"one shot with owned cleanup");
        Check(prepared.main.scalingMode==ParticleSystemScalingMode.Hierarchy,"native shape scales as a composition");
        Check(ps.main.loop&&ps.main.maxParticles==321&&original.GetComponent<AudioSource>()!=null,"source prefab remains untouched");
        var converted=prefab.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials.Single();
        Check(converted.GetTexture("_MainTex")==texture,"original texture retained");
        Check(converted.GetColor("_Tint").a==.65f&&converted.GetColor("_Tint").g==.35f,"native material colour and alpha retained");
        Check(converted.GetFloat("_SrcBlend")==5&&converted.GetFloat("_DstBlend")==1,"native blend retained");
        Check(converted.shader.name=="DimensionsFront/BomberNukeParticles","dedicated URP footprint shader");
        int saves=AssetDatabase.Saves;BomberNukeBuilder.Build();Check(AssetDatabase.Saves==saves,"repeat preparation is idempotent");
        AssetDatabase.Dependency="2";BomberNukeBuilder.Build();Check(AssetDatabase.Saves==saves+1,"source dependency changes rebuild the one composition");
        prefab=AssetDatabase.LoadAssetAtPath<GameObject>(generated);Resources.Prefab=prefab;Reset();
        int sound=Alterverse.Audio.DimensionAudio.Calls;
        var maps=Enum.GetValues<Alterverse.Match.ArenaVariant>();
        for(int map=0;map<maps.Length;map++)
        {
            Alterverse.Match.ArenaVariantRuntime.Current=maps[map];
            int before=GameObject.All.Count;var strike=new Alterverse.Combat.Detonation();strike.Resolve();strike.Resolve();
            var explosion=GameObject.All.Skip(before).Single(g=>g.name=="DF_Bomber_NukeConeExplosion");
            Check(explosion.activeSelf,"nuke visible on map "+map);
            Check(Math.Abs(explosion.transform.localScale.x-3.24f/6)<.0001f,"native scale follows actual blast radius on map "+map);
            var clip=explosion.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
            Check(clip.x==4&&clip.y==-9&&clip.z==3.24f&&clip.w==1,"world footprint locked to real impact on map "+map);
            Check(Alterverse.Audio.DimensionAudio.Calls==sound+map+1,"one original sound, including repeated resolve, on map "+map);
            Check(Alterverse.Visuals.BomberExplosionVfx.Calls==0,"no doubled old detonation on map "+map);
            var live=explosion.GetComponentsInChildren<ParticleSystem>(true).Single();var life=explosion.GetComponent<BomberNukeVfx>();
            Time.time+=10;GameObject.Invoke(life,"LateUpdate");Check(!explosion.Destroyed,"living smoke/sub-emitter is not cut by a short timer");
            live.Playing=false;GameObject.Invoke(life,"LateUpdate");Check(explosion.Destroyed,"finished composition is cleaned up");
        }
        Check(Alterverse.Audio.DimensionAudio.Gain==.65f,"original explosion gain retained");
        Check(!BomberNukeVfx.TryPlay(new(),0)&&!BomberNukeVfx.TryPlay(new(),float.NaN),"invalid radii rejected");
        Resources.Prefab=null;Reset();new Alterverse.Combat.Detonation().Resolve();
        Check(Alterverse.Visuals.BomberExplosionVfx.Calls==1,"missing import retains exactly one original visible explosion");
        Check(Debug.Warnings==1,"missing import is reported");
        AssetDatabase.Assets.Remove(BomberNukeBuilder.Source);bool blocked=false;
        try {new BomberNukeBuilder().OnPreprocessBuild(new());}catch(UnityEditor.Build.BuildFailedException){blocked=true;}
        Check(blocked,"player build cannot silently ship without the selected purchased source");
        Console.WriteLine($"PASS {checks} bomber Nuke import/playback/audio/footprint contracts across all {maps.Length} production arena variants. Controlled engine/editor fixtures; native Unity/GPU/phone review remains separate.");
    }
}
