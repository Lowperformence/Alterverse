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
    static GameObject Native(string name)
    {
        var native=new GameObject(name);native.transform.localScale=Vector3.one*2f;
        var ps=native.AddComponent<ParticleSystem>();
        var material=new Material(new Shader()){name="Authored"+name};material.SetColor("_Color",new Color(1f,.5f,.1f,.7f));
        AssetDatabase.Assets["Assets/Epic Toon FX/Materials/"+name+".mat"]=material;
        native.AddComponent<ParticleSystemRenderer>().sharedMaterials=new Material[]{material,null};
        var sub=new GameObject("Death sub-emitter");sub.transform.SetParent(native.transform,false);
        var subSystem=sub.AddComponent<ParticleSystem>();subSystem.main.playOnAwake=false;
        native.AddComponent<DemoController>();native.AddComponent<AudioSource>();native.AddComponent<Collider>();native.SetActive(false);
        return native;
    }
    static void FireballSourceDiscovery()
    {
        const AttackExplosionVfx.Kind kind=AttackExplosionVfx.Kind.MeteorFireball;
        var impact=Native("Fireball impact");
        string[] aliases={
            "Assets/Epic Toon FX/Prefabs/Combat/Explosions/Fireball/ExplosionFireballFire.prefab",
            "Assets/Epic Toon FX/Prefabs/Combat/Explosions/FireballExplosion/ExplosionFireballBlue.prefab",
            "Assets/Epic Toon FX/Prefabs/Combat/Impacts/Fireball/FireballHitFire.prefab",
            "Assets/Epic Toon FX/Prefabs/Combat/Explosions/Fireball/Orange.prefab",
            "Assets/Epic Toon FX/Prefabs/Combat/Explosions/Fireball/FireballFire.prefab"};
        foreach(string alias in aliases)
        {
            AssetDatabase.Assets[alias]=impact;
            Check(AttackExplosionVfxBuilder.FindSource(kind)==alias,"reordered names and split family/category folders resolve: "+alias);
            AssetDatabase.Assets.Remove(alias);
        }
        foreach(string invalid in new[]{
            "Assets/Epic Toon FX/Prefabs/Combat/Projectiles/Fireball/FireballWhite.prefab",
            "Assets/Epic Toon FX/Prefabs/Combat/Muzzleflash/Fireball/FireballFlashFire.prefab",
            "Assets/Epic Toon FX/Prefabs/Combat/Explosions/Fireball/FireballTrail.prefab"})
            Check(AttackExplosionVfxBuilder.SourceRank(invalid,kind)<0,"moving shot, White suffix, muzzle and trail are not impacts");

        const string shotPath="Assets/Epic Toon FX/Demo/Missile Prefabs/Fireball/FireBallFireOBJ.prefab";
        const string effectPath="Assets/Epic Toon FX/Prefabs/Combat/Explosions/Shared/ExplosionFire.prefab";
        var wrapper=new GameObject("FireBallFireOBJ");var controller=wrapper.AddComponent<DemoController>();
        AssetDatabase.Assets[shotPath]=wrapper;AssetDatabase.Assets[effectPath]=impact;
        controller.References["explosionPrefab"]=impact;
        Check(AttackExplosionVfxBuilder.FindSource(kind)==effectPath,"demo Fireball wrapper resolves its separate native impact, despite generic impact filename");
        controller.References.Clear();controller.ObjectReferences["hitEffect"]=impact.GetComponent<ParticleSystem>();
        Check(AttackExplosionVfxBuilder.FindSource(kind)==effectPath,"component reference resolves the native impact prefab root");
        controller.ObjectReferences.Clear();controller.References["effectOnCollision"]=impact;
        Check(AttackExplosionVfxBuilder.FindSource(kind)==effectPath,"collision effect reference is recognized");
        controller.References.Clear();controller.References["muzzleFlash"]=impact;
        bool refused=false;try{AttackExplosionVfxBuilder.FindSource(kind);}catch(System.IO.FileNotFoundException){refused=true;}
        Check(refused,"muzzle reference alone cannot become Fireball explosion");
        controller.References.Clear();controller.References["explosionPrefab"]=wrapper;
        refused=false;try{AttackExplosionVfxBuilder.FindSource(kind);}catch(System.IO.FileNotFoundException){refused=true;}
        Check(refused,"self-reference cannot import the moving demo projectile");
        const string twoD="Assets/Epic Toon FX/Prefabs/2D/Shared/Impact.prefab";
        AssetDatabase.Assets[twoD]=impact;AssetDatabase.Assets.Remove(effectPath);
        controller.References["explosionPrefab"]=impact;
        refused=false;try{AttackExplosionVfxBuilder.FindSource(kind);}catch(System.IO.FileNotFoundException){refused=true;}
        Check(refused,"referenced 2D effect cannot be substituted");
        AssetDatabase.Assets.Remove(twoD);AssetDatabase.Assets[effectPath]=impact;
        AttackExplosionVfxBuilder.BuildOne(kind);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/"+AttackExplosionVfx.ResourcePath(kind)+".prefab");
        Check(prefab.GetComponent<AttackExplosionVfx>().sourcePath==effectPath,"build records actual referenced impact source");
        Check(prefab.GetComponentsInChildren<DemoController>(true).Length==0&&prefab.GetComponentsInChildren<AudioSource>(true).Length==0,"neither demo controller nor its sounds enter prepared effect");
        Check(wrapper.GetComponent<DemoController>()==controller&&!wrapper.Destroyed,"discovery does not mutate or instantiate the demo wrapper");
        AssetDatabase.Assets.Clear();
    }
    static void Main()
    {
        FireballSourceDiscovery();
        var nuke=Native("NukeVerticalExplosionFire");var fireball=Native("FireballExplosionFire");
        var kinds=new[]{AttackExplosionVfx.Kind.FluxNuke,AttackExplosionVfx.Kind.MeteorFireball};
        string[] families={"NukeVerticalExplosion","FireballExplosion"};
        string[] sources=new string[2];
        for(int index=0;index<2;index++)
        {
            var kind=kinds[index];var native=index==0?nuke:fireball;string family=families[index];
            string familyRoot="Assets/Epic Toon FX/Prefabs/Combat/Explosions/"+family;
            sources[index]=familyRoot+"/"+family+"Fire.prefab";
            string[] aliases={sources[index],familyRoot+"/"+family+".prefab",familyRoot+"/Orange.prefab",familyRoot+"Blue/Blue.prefab","Assets/Epic Toon FX/Prefabs/"+family+"Red.prefab"};
            foreach(string alias in aliases)
            {
                AssetDatabase.Assets[alias]=native;
                Check(AttackExplosionVfxBuilder.FindSource(kind)==alias,"native family/colour variant discovered: "+alias);
                AssetDatabase.Assets.Remove(alias);
            }
            foreach(string invalid in new[]{
                "Assets/Epic Toon FX/Prefabs/2D Alternate/"+family+"Fire.prefab",
                "Assets/Epic Toon FX/Prefabs/2D/"+family+"Fire.prefab",
                "Assets/Epic Toon FX/Demo/"+family+"Fire.prefab",
                "Assets/OtherVfx/"+family+"Fire.prefab",
                "Assets/Epic Toon FX/Prefabs/Combat/Explosions/NukeConeExplosion/NukeConeExplosionFire.prefab",
                "Assets/Epic Toon FX/Prefabs/Combat/Projectiles/Fireball/FireballFire.prefab"})
            {
                Check(AttackExplosionVfxBuilder.SourceRank(invalid,kind)<0,"2D/demo/other package/cone/projectile is never substituted");
                AssetDatabase.Assets[invalid]=native;
            }
            bool diagnosed=false;try{AttackExplosionVfxBuilder.FindSource(kind);}
            catch(System.IO.FileNotFoundException e){diagnosed=e.Message.Contains("Native imported candidates:")&&e.Message.Contains("Demo reference sources inspected:");}
            Check(diagnosed,"missing family reports imported candidates");
            AssetDatabase.Assets[sources[index]]=new GameObject("Non-particle decoy");
            AssetDatabase.Assets[familyRoot+"/Orange.prefab"]=native;
            Check(AttackExplosionVfxBuilder.FindSource(kind)==familyRoot+"/Orange.prefab","non-particle decoy skipped");
            AssetDatabase.Assets[sources[index]]=native;
        }
        const string legacy="Assets/Alterverse/Resources/CombatVfx/PlanetAttackSparks/Sparks.prefab";
        const string purchased="Assets/Epic Toon FX/Prefabs/Environment/Sparks/Sparks.prefab";
        AssetDatabase.Assets[legacy]=new GameObject("Obsolete generated Sparks");
        AssetDatabase.Assets[purchased]=new GameObject("Purchased Sparks");
        AttackExplosionVfxBuilder.Build();
        Check(!AssetDatabase.Assets.ContainsKey(legacy)&&AssetDatabase.Assets.ContainsKey(purchased),"cleanup deletes only generated Sparks, preserving purchased assets");
        for(int index=0;index<2;index++)
        {
            var kind=kinds[index];var native=index==0?nuke:fireball;
            string asset="Assets/Alterverse/Resources/"+AttackExplosionVfx.ResourcePath(kind)+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);var definition=prefab.GetComponent<AttackExplosionVfx>();
            Check(definition.sourcePath==sources[index],"intended native family selected, fire variant preferred");
            Check(!prefab.activeSelf,"effect remains inactive until placed");
            Check(prefab.GetComponentsInChildren<AudioSource>(true).Length==0&&prefab.GetComponentsInChildren<Collider>(true).Length==0&&prefab.GetComponentsInChildren<DemoController>(true).Length==0,"import cannot add audio/physics/demo gameplay");
            var prepared=prefab.GetComponentsInChildren<ParticleSystem>(true).First();var source=native.GetComponent<ParticleSystem>();
            Check(prepared.main.startLifetime==source.main.startLifetime&&prepared.main.startSpeed==source.main.startSpeed&&prepared.main.maxParticles==source.main.maxParticles,"native speed, lifetime and particle budget preserved");
            Check(!prepared.main.loop&&prepared.main.scalingMode==ParticleSystemScalingMode.Hierarchy,"full composition scales together and ends");
            Check(prepared.transform.localScale.x==2f,"authored inner scale preserved");
            Check(!native.activeSelf&&source.main.loop&&native.GetComponent<DemoController>()!=null,"purchased source untouched");
            Check(prefab.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials[1]==null,"optional empty trail slot accepted");
            Check(prefab.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials[0].shader.name=="DimensionsFront/AttackExplosionParticles","only new effects use independent tint shader");
            Resources.Prefabs[AttackExplosionVfx.ResourcePath(kind)]=prefab;
        }
        int saves=AssetDatabase.Saves;AttackExplosionVfxBuilder.Build();Check(AssetDatabase.Saves==saves,"repeated import is idempotent");
        Check(!AttackExplosionVfx.TryPlay(kinds[0],new())&&!AttackExplosionVfx.TryMeteor(new(),Color.white),"unprepared effects let callers retain old impact");
        var world=new GameObject("DF_MatchWorld");AttackExplosionVfx.Prepare(world.transform);
        int objects=GameObject.All.Count,audio=Alterverse.Audio.DimensionAudio.Calls;
        Check(world.GetComponentsInChildren<AttackExplosionVfx>(true).Length==40,"both pools reserved during loading");
        for(int index=0;index<2;index++)
        {
            var kind=kinds[index];int capacity=index==0?8:32;float scale=index==0?4f/3f:3f;
            for(int i=0;i<capacity;i++)
            {
                var point=new Vector3(i,.3f,-i);var color=i%2==0?new Color(1f,.63f,.23f):new Color(.08f,1f,.22f);
                Check(index==0?AttackExplosionVfx.TryPlay(kind,point):AttackExplosionVfx.TryMeteor(point,color),"simultaneous impacts use reserved effect");
                var effect=world.GetComponentsInChildren<AttackExplosionVfx>(true).Single(e=>e.sourcePath==sources[index]&&e.gameObject.activeSelf&&e.transform.position.x==i);
                Check(effect.transform.localScale.x==scale&&effect.transform.localScale.y==scale&&effect.transform.localScale.z==scale,"Flux reduced to one third of previous 4x size; meteor remains 3x");
                Check(effect.transform.position.y==.3f&&effect.transform.position.z==-i,"actual impact position retained");
                var block=effect.GetComponentsInChildren<Renderer>(true).Single().Block;var tint=block.Vectors["_EffectTint"];
                Check(index==0?tint.w==0:tint.w==1&&tint.x==color.r&&tint.y==color.g&&tint.z==color.b,"native nuke colour retained; meteor hue matches orange/poison attack per instance");
                Check(!effect.GetComponentsInChildren<ParticleSystem>(true).Last().Playing,"sub-emitters are not spuriously autoplayed");
            }
            Check(!AttackExplosionVfx.TryPlay(kind,new()),"full pool cannot allocate during impact");
        }
        Check(GameObject.All.Count==objects&&Alterverse.Audio.DimensionAudio.Calls==audio,"no impact instantiation or extra sound");
        Time.time=20;
        var effects=world.GetComponentsInChildren<AttackExplosionVfx>(true);
        foreach(var effect in effects)
        {
            var systems=effect.GetComponentsInChildren<ParticleSystem>(true);foreach(var system in systems)system.Age=20;
            systems.Last().Playing=true;systems.Last().Age=0;
            GameObject.Invoke(effect,"LateUpdate");
            Check(effect.gameObject.activeSelf,"late sub-emitter tail is allowed to finish");
            systems.Last().Age=20;GameObject.Invoke(effect,"LateUpdate");
            Check(!effect.gameObject.activeSelf&&!effect.gameObject.Destroyed,"completed impact returns to pool");
        }
        Check(AttackExplosionVfx.TryMeteor(new Vector3(9,.2f,5),new Color(.3f,.4f,1f),.5f,4.16f),"Ione replay accepts original radius and event age");
        var replay=effects.Single(e=>e.gameObject.activeSelf);var clip=replay.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
        Check(clip.x==9&&clip.y==5&&clip.z==4.16f&&clip.w==1,"Ione still clips at real gameplay radius");
        Check(GameObject.All.Count==objects,"replayed impact reuses existing instance");
        Check(!AttackExplosionVfx.TryMeteor(new(),Color.white,float.NaN)&&!AttackExplosionVfx.TryMeteor(new(),Color.white,-1f),"invalid replay age cannot start particles");
        Console.WriteLine($"PASS {checks} native source, import isolation, colour, scale, pooling, tail and replay checks. Controlled fixtures; native Unity appearance remains separate.");
    }
}
