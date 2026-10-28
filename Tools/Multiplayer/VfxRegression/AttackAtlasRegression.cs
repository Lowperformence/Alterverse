using System;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;

static partial class VfxRegression
{
    static MeshFilter[] AtlasVisible()=>Visible().Where(x=>x.GetComponent<MeshRenderer>()
        .sharedMaterial?.shader?.name=="DimensionsFront/AttackAtlas").ToArray();
    static int Frame(MeshFilter f)=>(int)f.GetComponent<MeshRenderer>().Block.Floats[Shader.PropertyToID("_AtlasCell")];

    static void AttackAtlasChecks()
    {
        // Use the real action/delivery/coroutine path, rather than just calling a new
        // presentation helper. Rendering must leave the complete combat result alone.
        foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int action=-1;action<4;action++)
        {
            var plain=Scenario(hero,action,false,60);
            var textured=Scenario(hero,action,false,60,true);
            if(hero==HeroId.Bolt7)
                Check(textured.NativeBackendSeen,"BOLT-7 routes to its separately tested native backend: "+action);
            else Check(textured.AtlasSeen,"Actual action uses imported VFX: "+hero+" / "+action);
            Check(plain.ActorHealth==textured.ActorHealth&&plain.ActorShield==textured.ActorShield&&
                plain.TargetHealth==textured.TargetHealth&&plain.TargetShield==textured.TargetShield&&
                plain.AllyHealth==textured.AllyHealth&&plain.AllyShield==textured.AllyShield,
                "Imported art cannot change damage, heal or shield results: "+hero+" / "+action);
            Check((plain.Position-textured.Position).sqrMagnitude<.000001f&&plain.Cooldown==textured.Cooldown&&
                plain.Events.SequenceEqual(textured.Events),"Imported art cannot change movement, cooldown or wire cues: "+hero+" / "+action);
        }

        foreach(int fps in new[]{30,60,120})
        {
            Reset();var actor=Hero(HeroId.Elyra,0);var target=Hero(HeroId.Kaelon,1);
            target.transform.position=Vector3.forward*4;
            int delivery=HeroVfxPool.BeginDelivery();
            HeroVfxPool.Sprite(HeroFxSprite.Fireball,Vector3.zero,target.transform.position,
                new Vector2(.4f,.4f),Color.white,1f,actor,HeroFxMotion.Travel,endFollow:target.transform);
            HeroVfxPool.EndDelivery();
            for(int frame=0;frame<fps/2;frame++)Step(1f/fps);
            var fx=AtlasVisible().Single();
            Check(Frame(fx)>=55&&Frame(fx)<=56,"Flipbook is driven by actual age at "+fps+" FPS");
            Check(Mathf.Abs(fx.transform.position.z-2)<.001f,"Textured projectile reaches its halfway point at "+fps+" FPS");
            Check(fx.sharedMesh.Vertices.Count==4&&fx.sharedMesh.UVs.Count==4&&fx.sharedMesh.Triangles.Count==6,
                "Imported effects reuse a single two-triangle UV quad");
            int pausedFrame=Frame(fx);Time.timeScale=0;Step(.25f);
            Check(Frame(fx)==pausedFrame,"Pause freezes imported animation without a global shader clock");Time.timeScale=1;
            HeroVfxPool.CancelDelivery(delivery);
            Check(AtlasVisible().Length==0,"Cancelling the real delivery removes its imported projectile immediately");

            Reset();actor=Hero(HeroId.Elyra,0);
            using(HeroVfxEvents.Replay(.5f,Vector3.zero))
                HeroVfxPool.Sprite(HeroFxSprite.Fireball,Vector3.zero,Vector3.forward*4,new Vector2(.4f,.4f),
                    Color.white,1f,actor,HeroFxMotion.Travel);
            fx=AtlasVisible().Single();Check(Frame(fx)==56,"Network replay seeks the original flipbook frame");
            Step(.51f);Check(AtlasVisible().Length==0,"Replayed flipbook expires at the original deadline");

            Reset();actor=Hero(HeroId.Timmy,0);
            HeroSkillVfx.TimmyEffect(actor,2,Vector3.zero,Vector3.forward*3,0,.3f,false);
            Check(AtlasVisible().Length==2,"Timmy's imported jet flames attach to both actual nozzles");
            HeroSkillVfx.TimmyEffect(actor,4,Vector3.zero,Vector3.zero,0,0,false);
            Check(AtlasVisible().Length==0,"Interrupted Timmy movement cancels both imported jet layers");
        }

        foreach(bool missing in new[]{true,false})
        {
            Reset();Resources.AttackAtlasAvailable=!missing;Resources.AttackAtlasShaderSupported=missing;
            var actor=Hero(HeroId.Nyra,0);var target=Hero(HeroId.Kaelon,1);
            HeroSkillVfx.Basic(actor,Vector3.forward*3,.4f,target.transform);
            Check(Visible().Length>0&&AtlasVisible().Length==0&&Debug.ErrorCount==0,
                "Absent/unsupported optional atlas preserves working projectile cores");
        }
        Reset();Resources.AttackAtlasTextureAvailable=false;
        var noTextureOwner=Hero(HeroId.Nyra,0);
        HeroSkillVfx.Basic(noTextureOwner,Vector3.forward*3,.4f);
        Check(Visible().Length>0&&AtlasVisible().Length==0,
            "Unassigned texture cannot turn a valid material into a black/opaque fallback quad");
        Reset();var owner=Hero(HeroId.VoidKnight,0);
        HeroSkillVfx.Basic(owner,Vector3.forward*1.5f);
        Check(AtlasVisible().Length==1&&Visible().All(f=>f.sharedMesh.name!="DF_AuthoredFx_Orb"),
            "Void Knight basic is a heavy sword swing rather than a ranged orb");
        Step(.4f);HeroSkillVfx.Area(owner,Vector3.forward*2,2,.5f,true,false,3);
        Check(AtlasVisible().Length==0,"Imported art does not replace exact warning geometry");
        Reset();owner=Hero(HeroId.Mira,0);
        Alterverse.Performance.AdaptiveVfxBudget.AllowDecorative=false;
        HeroSkillVfx.Basic(owner,Vector3.forward*3,.4f);
        Check(Visible().Length>0&&AtlasVisible().Length==0,"Battery/overload budget drops textures while retaining attack cores");

        Reset();owner=Hero(HeroId.Nyra,0);HeroVfxPool.Warm();int objectCount=GameObject.All.Count;
        for(int i=0;i<500;i++)HeroVfxPool.Sprite(HeroFxSprite.Burst,Vector3.zero,Vector3.zero,
            new Vector2(1,1),Color.white,.5f,owner);
        Check(GameObject.All.Count==objectCount&&AtlasVisible().Length<=128,
            "Saturated imported art creates no more render objects and respects the decorative capacity");
        for(int i=0;i<10;i++)HeroVfxPool.Emit(HeroFxShape.Ring,Vector3.zero,Vector3.zero,
            Vector3.one*2,Color.white,.5f,HeroFxMotion.Hold,owner:owner,warning:true);
        Check(Visible().Count(f=>f.sharedMesh.name=="DF_AuthoredFx_Ring")==10,
            "Texture saturation leaves all ten danger markers available");
        HeroVfxPool.Emit(HeroFxShape.Laser,Vector3.zero,Vector3.forward*3,Vector3.one,Color.white,.5f,
            HeroFxMotion.LaserBeam,owner:owner);
        Check(Visible().Any(f=>f.sharedMesh.name=="DF_AuthoredFx_Laser"),"Texture saturation preserves the core shot reserve");
        Step(.6f);Check(Visible().Length==0,"Burst load releases all imported render slots");
        for(int i=0;i<500;i++)PrototypeVfx.Burst(Vector3.zero,Color.white);
        Check(Visible().Length<=128,"Mass soldier contact bursts cannot consume protected player-shot slots");
        HeroVfxPool.Emit(HeroFxShape.Laser,Vector3.zero,Vector3.forward*3,Vector3.one,Color.white,.5f,
            HeroFxMotion.LaserBeam,owner:owner);
        Check(Visible().Any(f=>f.sharedMesh.name=="DF_AuthoredFx_Laser"),"Player shot remains readable during mass soldier contact bursts");
        UnitAndSharedEffects();
    }

    static void UnitAndSharedEffects()
    {
        var styles=new System.Collections.Generic.HashSet<MonsterAttackStyle>();
        foreach(Alterverse.Match.ArenaVariant arena in Enum.GetValues(typeof(Alterverse.Match.ArenaVariant)))
        {
            Reset();Alterverse.Match.ArenaVariantRuntime.Current=arena;
            var monster=new GameObject("planet monster");var hp=monster.AddComponent<Health>();
            var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*3;
            styles.Add(MonsterAttackVfx.StyleFor(monster));
            MonsterAttackVfx.Launch(monster,Vector3.up,target.transform.position,.4f,target:target.transform);
            Check(AtlasVisible().Length>0&&Visible().Length>AtlasVisible().Length,
                "Planet attack retains its core and gains imported detail: "+arena);
            hp.IsDead=true;Step(.02f);
            Check(AtlasVisible().Length==0,"Monster death cancels imported attack layers: "+arena);
            Reset();Alterverse.Match.ArenaVariantRuntime.Current=arena;
            monster=new GameObject("planet contact");monster.AddComponent<Health>();
            MonsterAttackVfx.PresentImpact(monster,Vector3.zero,Vector3.forward*3);
            Check(AtlasVisible().Length>0,"Planet contact gains its appropriate imported impact: "+arena);
            Step(.4f);Check(Visible().Length==0,"Planet effect releases every slot: "+arena);
        }
        Check(styles.Count==12,"The fixture exercises all twelve production monster attack styles");
        Reset();var eye=new GameObject("laser owner");eye.AddComponent<Health>();var victim=Hero(HeroId.Nyra,1);
        victim.transform.position=Vector3.forward*3;
        MonsterAttackVfx.PresentLaser(eye,victim.transform,1,0);
        Check(AtlasVisible().Length==2,"Monster laser has compact eye and contact glows");
        MonsterAttackVfx.PresentLaser(eye,null,0,0);
        Check(Visible().Length==0,"Stopping the monster beam removes its imported glows immediately");

        Reset();var source=new GameObject("shared source");HeroVfxPool.Warm();int count=GameObject.All.Count;
        Action[] effects={
            ()=>PrototypeVfx.Burst(Vector3.zero,Color.white),
            ()=>PrototypeVfx.Ring(Vector3.zero,Color.white),
            ()=>PrototypeVfx.Slash(source.transform,Color.white),
            ()=>PrototypeVfx.ProjectileLine(Vector3.zero,Vector3.forward*3,Color.white),
            ()=>PrototypeVfx.BioBolt(Vector3.zero,Vector3.forward*3,Color.white,Color.white),
            ()=>PrototypeVfx.Shockwave(Vector3.zero,Color.white),
            ()=>PrototypeVfx.GravityField(Vector3.zero,Color.white),
            ()=>PrototypeVfx.Bloom(Vector3.zero,Color.white),
            ()=>PrototypeVfx.Guard(Vector3.zero,Color.white),
            ()=>PrototypeVfx.FutureMark(source.transform,Color.white,.3f),
            ()=>BomberExplosionVfx.Play(Vector3.zero,new Color(1f,.4f,.1f),2f)
        };
        foreach(var effect in effects)
        {
            effect();Check(Visible().Length>0,"Shared combat effect has a readable core");
            Step(.025f);Check(Visible().Length>0,"Unowned shared effects survive the scheduler update");
            Check(GameObject.All.Count==count,"Shared effects allocate no per-use GameObjects, particle systems or primitives");
            Step(3f);Check(Visible().Length==0,"Shared combat effect releases every pool slot");
        }
        Reset();SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Null;
        foreach(var effect in effects)effect();
        Check(Visible().Length==0&&GameObject.All.Count<=1,"Shared effects construct no graphics objects on a headless server");
    }
}

