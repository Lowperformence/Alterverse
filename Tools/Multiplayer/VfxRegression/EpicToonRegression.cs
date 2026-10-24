using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;

static partial class VfxRegression
{
    static readonly string[] EpicKeys={"Slash","HeavySlash","Claw","SwordImpact","Orb","Missile","Rune","Shield","Heal","Portal","Vortex","Water","Smoke","Nova","Punch","Impact","FireImpact","Spark","Fire","Muzzle","Jet","HeavyImpact","NatureMissile","NatureImpact","NatureNova","ArcaneMissile","MagicImpact","ShadowMissile","FireNova","BladeWave","SharpBolt","LeafDart","SpinStrike","BomberNuke","LightningMissile","LightningMuzzle","LightningImpact","LightningNova","HealField","HealPod","HealPickup","TimmyJet","WeatherSnow","WeatherRain","WeatherWind","WeatherFog","SpinPortal","MiniHit","SoftHit","LeafHit","MagicHit"};
    static EpicVfxLibrary TestLibrary()
    {
        var library=ScriptableObject.CreateInstance<EpicVfxLibrary>();
        library.entries=EpicKeys.Concat(EpicVfxLibrary.AbilityLooks.Where(l=>l.Source!="ExactBeam").Select(l=>l.Key))
            .Concat(EpicVfxLibrary.AbilityLooks.SelectMany(l=>new[]{l.Key+"_Cast",l.Key+"_Hit"}))
            .Select(key=>new EpicVfxLibrary.Entry{key=key,prefab=new GameObject("fixture_"+key),scale=.5f,nativeSeconds=1}).ToArray();
        library.beamMaterial=new Material(new Shader{name="DimensionsFront/EpicParticles"});return library;
    }
    static object Field(object value,string field)=>value.GetType().GetField(field,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
    static object Pool()=>typeof(HeroVfxPool).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static object[] LiveSlots()=>Pool()==null?Array.Empty<object>():((Array)Field(Pool(),"slots")).Cast<object>().Where(s=>s!=null&&(bool)Field(s,"Active")).ToArray();
    static void EpicReset()
    {
        Reset();ParticleSystem.ForceNoParticles=false;Resources.EpicLibrary=TestLibrary();CoopAuthority.Active=false;
    }
    static void ExclusiveReplacement()
    {
        foreach(HeroId id in Enum.GetValues(typeof(HeroId)))foreach(int action in new[]{-1,0,1,2,3})
        {
            if(id==HeroId.Nerok||id==HeroId.Timmy)continue; // Explicit original-look exceptions.
            EpicReset();var actor=Hero(id,0);var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*3;
            float hp=target.GetComponent<Health>().Current;
            if(action<0)HeroSkillVfx.Basic(actor,target.transform.position,.4f,target.transform);
            else HeroSkillVfx.Cast(actor,action);
            var slots=LiveSlots();Check(slots.Length>0,"Every hero/action emits replacement presentation "+id+action);
            foreach(var slot in slots)
            {
                var epic=Field(slot,"Epic");bool beam=(bool)Field(slot,"EpicBeam");
                bool physical=new[]{HeroFxShape.AnchorProjectile,HeroFxShape.AnchorChain,HeroFxShape.TimmyGrenade}.Contains((HeroFxShape)Field(slot,"Shape"));
                Check(epic!=null||beam||physical||(bool)Field(slot,"Warning"),"No legacy cosmetic core in catalog mode "+id+action);
                if(epic!=null)Check(!((MeshRenderer)Field(slot,"Renderer")).enabled,"Old mesh is disabled whenever a native effect owns the slot");
            }
            Check(target.GetComponent<Health>().Current==hp,"Presentation alone cannot damage "+id+action);
            Step(6);Check(LiveSlots().Length==0,"Native effects expire with authoritative visual slot "+id+action);
        }
    }
    static void ParticleLifecycle()
    {
        EpicReset();var actor=Hero(HeroId.Veyr,0);Color tint=new(.2f,.7f,.9f,.8f);
        HeroVfxPool.Emit(HeroFxShape.Orb,Vector3.zero,Vector3.forward*4,Vector3.one,tint,.4f,
            HeroFxMotion.Travel,owner:actor,delay:.2f,cancelOnOwnerDeath:true);
        object slot=LiveSlots().Single();var epic=Field(slot,"Epic");var root=(GameObject)Field(epic,"Root");var ps=((ParticleSystem[])Field(epic,"Systems")).Single();
        Check(!root.activeSelf&&ps.Simulated==0,"Delayed effects stay hidden and unsimulated");
        Step(.3f);Check(root.activeSelf&&Mathf.Abs(ps.Simulated-.1f)<.001f,"Particle time follows existing delayed timeline");
        Check(ps.PlayCalls>0&&ps.particleCount>0,"Stopped native emitters are explicitly started before simulation");
        var renderer=((Renderer[])Field(epic,"Renderers")).Single();var color=renderer.Block.Colors[Shader.PropertyToID("_Tint")];
        Check(color.r==tint.r&&color.g==tint.g&&color.b==tint.b,"Native material receives exact ability color");
        HeroVfxPool.CancelProjectiles(actor);Check(!root.activeSelf&&ps.Cleared&&LiveSlots().Length==0,"Cancellation clears particles immediately");
        HeroVfxPool.Emit(HeroFxShape.Orb,Vector3.zero,Vector3.forward,Vector3.one,tint,.4f,HeroFxMotion.Travel,owner:actor,cancelOnOwnerDeath:true);
        Check(ReferenceEquals(Field(LiveSlots().Single(),"Epic"),epic),"Repeated shot reuses cleared native instance");
        actor.GetComponent<Health>().TakeDamage(10000);Step(.01f);Check(LiveSlots().Length==0&&!root.activeSelf,"Owner death removes native shot without a lingering duplicate");
        EpicReset();actor=Hero(HeroId.Veyr,0);
        using(HeroVfxEvents.Replay(.6f,Vector3.zero))HeroSkillVfx.Basic(actor,Vector3.forward,.4f);
        Check(LiveSlots().Length==0,"Expired network cue cannot restart native attack");
        EpicReset();actor=Hero(HeroId.Elyra,0);
        for(int i=0;i<180;i++)HeroVfxPool.Emit(HeroFxShape.Orb,Vector3.zero,Vector3.zero,Vector3.one,tint,1,owner:actor);
        HeroVfxPool.Emit(HeroFxShape.Ring,Vector3.zero,Vector3.zero,Vector3.one,tint,1,owner:actor,warning:true);
        Check(LiveSlots().Count(s=>(bool)Field(s,"Warning"))==1,"Saturated native particles preserve separate warning reserve");
        Check((int)Field(Pool(),"epicInstances")<=160,"Native instance cache is bounded");
        var warning=LiveSlots().Single(s=>(bool)Field(s,"Warning"));
        Check(Field(warning,"Epic")==null&&((MeshRenderer)Field(warning,"Renderer")).enabled,"Telegraph keeps exact authored geometry");
    }
    static void RenderingParity()
    {
        Resources.EpicLibrary=TestLibrary();
        foreach(int fps in new[]{30,60,120})foreach(HeroId id in Enum.GetValues(typeof(HeroId)))for(int action=-1;action<4;action++)
        {
            var baseline=Scenario(id,action,false,fps,false);var native=Scenario(id,action,false,fps,true);
            Check(Mathf.Abs(baseline.TargetHealth-native.TargetHealth)<.001f&&Mathf.Abs(baseline.AllyHealth-native.AllyHealth)<.001f&&
                Mathf.Abs(baseline.ActorHealth-native.ActorHealth)<.001f,"New renderer preserves damage/healing "+id+action+fps);
            Check(Mathf.Abs(baseline.ActorShield-native.ActorShield)<.001f&&Mathf.Abs(baseline.TargetShield-native.TargetShield)<.001f&&Mathf.Abs(baseline.AllyShield-native.AllyShield)<.001f,"New renderer preserves shields "+id+action+fps);
            Check((baseline.Position-native.Position).sqrMagnitude<.000001f&&Mathf.Abs(baseline.Cooldown-native.Cooldown)<.001f,"New renderer preserves movement/cooldowns "+id+action+fps);
            Check(baseline.Events.SequenceEqual(native.Events),"New renderer preserves semantic event order "+id+action+fps);
        }
    }
    static void RealActionChecklist()
    {
        Resources.EpicLibrary=TestLibrary();
        foreach(HeroId id in Enum.GetValues(typeof(HeroId)))for(int action=-1;action<4;action++)
        {
            if(id==HeroId.Nerok||id==HeroId.Timmy)
            {
                Console.WriteLine("CHECKLIST|"+HeroIdentity.NameFor(id)+"|"+(action<0?"Basisangriff":HeroAbilityKit.LoadoutFor(id).Spec(action).Name)+"|"+AimingSettings.Current.For(id,action).aimType+"|Original unveraendert|Original-Ausnahme");
                continue;
            }
            var keys=new HashSet<string>();int samples=0;
            ObserveFrame=()=>
            {
                foreach(var slot in LiveSlots())
                {
                    // Other actors can supply independent shield/hit feedback. Check this caster's cues.
                    var owner=(GameObject)Field(slot,"Owner");
                    if(owner==null||owner.GetComponent<HeroIdentity>()?.Hero!=id)continue;
                    bool warning=(bool)Field(slot,"Warning");var shape=(HeroFxShape)Field(slot,"Shape");
                    var epic=Field(slot,"Epic");
                    if(epic!=null)
                    {
                        if(!((GameObject)Field(epic,"Root")).activeSelf)continue;
                        samples++;
                        Check(!((MeshRenderer)Field(slot,"Renderer")).enabled,"No old/new overlap during actual action "+id+action);
                        Check(((ParticleSystem[])Field(epic,"Systems")).Any(ps=>ps.particleCount>0),"Actual action starts its native emitter "+id+action);
                        keys.Add((string)Field(epic,"Key"));
                    }
                    else if((bool)Field(slot,"EpicBeam")){keys.Add("ExactBeam");samples++;}
                    else if(!warning&&shape!=HeroFxShape.Shield&&shape!=HeroFxShape.TimmyGrenade)
                        Check(false,"Unreplaced cosmetic cue in actual action "+id+action+" shape="+shape);
                }
            };
            try{Scenario(id,action,false,60,true);}
            finally{ObserveFrame=null;}
            Check(samples>0&&keys.Count>0,"Every real basic/Q/W/E/ultimate has a new effect "+id+action);
            var look=EpicVfxLibrary.FindLook(id.ToString(),action);
            Check(look!=null&&(keys.Contains(look.Source=="ExactBeam"?"ExactBeam":look.Key)||keys.Any(k=>k.StartsWith(look.Key+"_"))),
                "Actual action uses its own visual concept "+id+action+" expected="+look?.Key+" observed="+string.Join(",",keys));
            if(action==3||id==HeroId.Rook&&action==2||id==HeroId.Kaelon&&action==2)
                Check(keys.Contains(look.Key),"damaging area/ultimate uses its actual primary composition, not only preparation or hit "+id+action);
            Console.WriteLine("CHECKLIST|"+HeroIdentity.NameFor(id)+"|"+(action<0?"Basisangriff":HeroAbilityKit.LoadoutFor(id).Spec(action).Name)+"|"+AimingSettings.Current.For(id,action).aimType+"|"+string.Join(", ",keys.OrderBy(k=>k))+"|Code geprueft; Unity-Sichttest offen");
        }
    }
    static void NerokPreservation()
    {
        for(int action=0;action<6;action++)
        {
            EpicReset();var actor=Hero(HeroId.Nerok,0);var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*3;
            switch(action)
            {
                case 0:HeroSkillVfx.Basic(actor,target.transform.position,.3f,target.transform);break;
                case 1:HeroSkillVfx.Cast(actor,1);break;
                case 2:HeroSkillVfx.SharkAnchor(actor,target.transform.position,.4f,target.transform);break;
                case 3:HeroSkillVfx.SharkPullChain(actor,target.GetComponent<Health>(),.6f);break;
                case 4:HeroSkillVfx.SharkVortex(actor,target.transform.position,2.3f,1);break;
                case 5:HeroSkillVfx.Area(actor,target.transform.position,2.8f,.4f,false,slot:2);break;
            }
            var live=LiveSlots();Check(live.Length>0,"Original Nerok cue remains present "+action);
            Check(live.All(slot=>Field(slot,"Epic")==null&&!(bool)Field(slot,"EpicBeam")),"No Epic substitution anywhere in Nerok cue "+action);
            if(action==2)Check(live.Length==4&&live.Any(slot=>(HeroFxShape)Field(slot,"Shape")==HeroFxShape.AnchorProjectile),"Nerok's complete original anchor recipe remains intact");
            if(action==3)Check(live.Length==1&&(HeroFxMotion)Field(live[0],"Motion")==HeroFxMotion.Tether&&ReferenceEquals(Field(live[0],"EndFollow"),target.transform),"Nerok's original single chain/tether retains target following");
            if(action==4)Check(live.Count(slot=>(HeroFxShape)Field(slot,"Shape")==HeroFxShape.WaterSpiral)==2&&live.Count(slot=>(HeroFxShape)Field(slot,"Shape")==HeroFxShape.WaveCrest)==3,"Nerok's original ultimate water layers are restored");
        }
        EpicReset();var nerok=Hero(HeroId.Nerok,0);
        Check(nerok.AddComponent<HeroAbilityReadabilityBoost>().enabled&&nerok.AddComponent<HeroAttackReadabilityPresentation>().enabled,"Nerok retains his two original presentation observers");
    }
    static void BackendAvailability()
    {
        EpicReset();Resources.HeroShaderSupported=false;
        var actor=Hero(HeroId.Nyra,0);var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*3;
        HeroSkillVfx.Basic(actor,target.transform.position,.4f,target.transform);
        var slot=LiveSlots().Single();var visual=Field(slot,"Epic");
        Check(visual!=null&&((HeroVfxPool)Pool()).enabled,"A failed legacy shader cannot disable the supported new backend");
        Check(((Material)Field(Pool(),"material")).name=="DF_EpicWarningSurface","Supported particle shader provides an independent white warning surface");
        Check(!((MeshRenderer)Field(slot,"Renderer")).enabled,"New backend draws exclusively even when old shader is unsupported");
        Check(Debug.ErrorCount==0,"Recoverable old-shader failure does not report a total renderer failure");
        HeroVfxPool.Emit(HeroFxShape.Ring,Vector3.zero,Vector3.zero,Vector3.one*2,Color.white,.4f,
            HeroFxMotion.Hold,owner:actor,warning:true);
        var warning=LiveSlots().Single(s=>(bool)Field(s,"Warning"));
        Check(((MeshRenderer)Field(warning,"Renderer")).enabled&&((MeshRenderer)Field(warning,"Renderer")).sharedMaterial.shader.isSupported,"Warning geometry remains renderable during legacy shader recovery");
        Check(Mathf.Abs(((Transform)Field(warning,"Transform")).localScale.x-2)<.001f,"Recovered warning retains exact gameplay radius");
        ParticleSystem.ForceNoParticles=true;Step(.1f);
        Check(((MeshRenderer)Field(slot,"Renderer")).enabled,"Empty native emission cannot leave a shot without its single fallback");
        Check(((Renderer[])Field(visual,"Renderers")).All(r=>!r.enabled),"Fallback never overlaps native renderers");
        ParticleSystem.ForceNoParticles=false;Step(.02f);
        Check(!((MeshRenderer)Field(slot,"Renderer")).enabled&&((Renderer[])Field(visual,"Renderers")).Any(r=>r.enabled),"Native effect resumes exclusive rendering as soon as emission is ready");
        Check(((ParticleSystem[])Field(visual,"Systems")).Single().Restarts==1,"Fallback recovery continues the original clock without restarting the shot");
    }
    static void EpicToonChecks()
    {
        ExclusiveReplacement();ParticleLifecycle();RenderingParity();RealActionChecklist();NerokPreservation();TimmyPreservation();BackendAvailability();PresentationScaleAndContext();FootprintLimits();HealingStations();WeatherBudgets();
        EpicReset();var actor=Hero(HeroId.Veyr,0);
        Check(!actor.AddComponent<HeroAbilityReadabilityBoost>().enabled&&!actor.AddComponent<HeroAttackReadabilityPresentation>().enabled,"Legacy cooldown observers cannot add duplicate attacks");
        Resources.EpicLibrary=null;
    }
    static void TimmyPreservation()
    {
        EpicReset();var actor=Hero(HeroId.Timmy,0);HeroSkillVfx.Basic(actor,Vector3.forward);
        var slots=LiveSlots();
        Check(slots.Count(s=>(HeroFxShape)Field(s,"Shape")==HeroFxShape.Claw&&
            (HeroFxMotion)Field(s,"Motion")==HeroFxMotion.Sweep)==3&&slots.All(s=>Field(s,"Epic")==null),
            "Timmy's exact original three sweeping claw cuts are restored");
        Check(actor.AddComponent<HeroAbilityReadabilityBoost>().enabled&&actor.AddComponent<HeroAttackReadabilityPresentation>().enabled,
            "Timmy retains his original presentation observers");
        EpicReset();actor=Hero(HeroId.Timmy,0);
        HeroSkillVfx.TimmyEffect(actor,3,Vector3.zero,Vector3.forward*3,0,.4f,relay:false);
        slots=LiveSlots();Check(slots.All(s=>Field(s,"Epic")==null||(string)Field(Field(s,"Epic"),"Key")=="TimmyJet"),
            "Timmy jump replaces only its explicitly requested jet flames");
        Check(slots.Count(s=>(HeroFxShape)Field(s,"Shape")==HeroFxShape.Claw)==3,
            "Original Scratch retains all three attached claws");
    }

    static void FootprintLimits()
    {
        foreach(var id in new[]{HeroId.Veyr,HeroId.VoidKnight,HeroId.Kaelon,HeroId.Rook})
        {
            EpicReset();var actor=Hero(id,0);actor.transform.position=new Vector3(11,0,7);
            HeroSkillVfx.Basic(actor,actor.transform.position+Vector3.forward);
            foreach(var slot in LiveSlots().Where(s=>Field(s,"Epic")!=null))
            {
                var visual=Field(slot,"Epic");float limit=actor.GetComponent<HeroCombatProfile>().AttackRange;
                Check((float)Field(visual,"FootprintRadius")<=limit,"melee/native bounds cannot multiply basic reach "+id);
                Check((bool)Field(visual,"ClipCone")&&(bool)Field(visual,"ClipReach"),"native melee shader clips both radius and cone "+id);
                var origin=(Vector3)Field(visual,"Origin");Check((origin-actor.transform.position).sqrMagnitude<.001f,"melee footprint uses the actual caster world position "+id);
                Step(.08f);
                var root=(GameObject)Field(visual,"Root");var parent=(Transform)Field(slot,"Transform");
                float footprint=root.transform.localScale.x*parent.localScale.x*(float)Field(visual,"BoundsRadius");
                Check(footprint<=(float)Field(visual,"FootprintRadius")+.001f,"native bounds fit despite legacy slot and animation scale "+id);
            }
        }
        foreach(var id in new[]{HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Traveler})
        {
            EpicReset();var actor=Hero(id,0);HeroSkillVfx.Basic(actor,Vector3.forward*4,.4f);
            var visual=Field(LiveSlots().First(s=>Field(s,"Epic")!=null),"Epic");
            Check((float)Field(visual,"FootprintRadius")<=AimingSettings.Current.For(id,-1).projectileWidth,
                "small shot respects projectile width "+id);
            Check((bool)Field(visual,"ClipTrail"),"native tail keeps the actual flight segment "+id);
            var target=Hero(HeroId.Kaelon,1);target.transform.position=Vector3.forward*2;
            HeroSkillVfx.Impact(actor,target.GetComponent<Health>(),3);
            var hits=LiveSlots().Select(s=>Field(s,"Epic")).Where(v=>v!=null&&((string)Field(v,"Key")).EndsWith("_Hit")).ToArray();
            Check(hits.Length>0&&hits.All(v=>(float)Field(v,"FootprintRadius")<=.24f),"ultimate victim feedback stays compact "+id);
        }
        EpicReset();var traveler=Hero(HeroId.Traveler,0);HeroSkillVfx.Dash(traveler,Vector3.zero,Vector3.forward*3,1,.2f);
        foreach(var visual in LiveSlots().Select(s=>Field(s,"Epic")).Where(v=>v!=null))
            Check(Mathf.Abs((float)Field(visual,"RenderRadius")-.85f*1.5f)<.001f,"Spin Portal grows by 50 percent at both jump endpoints");
        EpicReset();BomberExplosionVfx.PlayBomber(Vector3.forward,3.24f);
        var bomber=LiveSlots().Single();var nuke=Field(bomber,"Epic");
        Check((string)Field(nuke,"Key")=="BomberNuke"&&(bool)Field(nuke,"NativePalette"),"bomber uses selected native fire/smoke palette once");
        Check((float)Field(nuke,"FootprintRadius")==3.24f,"bomber explosion stays inside the unchanged blast radius");
        EpicReset();HeroVfxPool.Emit(HeroFxShape.Orb,Vector3.zero,Vector3.forward,new Vector3(.15f,.15f,.15f),Color.white,.2f);
        var generic=Field(LiveSlots().Single(),"Epic");Check((float)Field(generic,"RenderRadius")<=.15f,"non-hero native shots cannot inherit the obsolete blanket enlargement");
        EpicReset();Camera.main=new GameObject("camera").AddComponent<Camera>();CombatImpactPool.Emit(Vector3.zero,Color.white,1);
        var quiet=LiveSlots().Single();var contact=Field(quiet,"Epic");
        Check((string)Field(contact,"Key")=="MiniHit"&&(float)Field(contact,"FootprintRadius")<=.16f,"non-hero contact uses a quiet purchased mini hit instead of an opaque comic star");
        Check(((Color)Field(quiet,"Color")).a<=.4f&&!(bool)Field(contact,"NativePalette"),"quiet contact retains the source tint with reduced opacity");
        EpicReset();var timmy=Hero(HeroId.Timmy,0);HeroSkillVfx.TimmyEffect(timmy,3,Vector3.zero,Vector3.forward*3,0,.3f,relay:false);
        Check(LiveSlots().Count(s=>Field(s,"Epic")!=null&&(string)Field(Field(s,"Epic"),"Key")=="TimmyJet")==2,"exactly one purchased flamethrower per nozzle");
        Check(LiveSlots().Count(s=>(HeroFxShape)Field(s,"Shape")==HeroFxShape.Claw&&Field(s,"Epic")==null)==3,"jet replacement preserves all original Scratch claws");
        // Several distinct heroes emit together. Essential shots/telegraphs remain present;
        // visual recognition itself still requires the native phone-sized review images.
        EpicReset();var ids=new[]{HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Rook,HeroId.Traveler,HeroId.VoidKnight};
        for(int i=0;i<ids.Length;i++){var hero=Hero(ids[i],i);hero.transform.position=Vector3.right*(i%4);HeroSkillVfx.Basic(hero,hero.transform.position+Vector3.forward*2,.3f);}
        Check(LiveSlots().Where(s=>Field(s,"Epic")!=null).Select(s=>Field(Field(s,"Epic"),"Key")).Distinct().Count()==8,"eight simultaneous heroes retain separate attack signatures");
    }
    static void WeatherBudgets()
    {
        foreach(ArenaVariant planet in Enum.GetValues(typeof(ArenaVariant)))
        {
            string[] keys=EpicPlanetWeather.LayersFor(planet);
            Check(keys.Length<=2,"weather never uses more than two layers "+planet);
            Check(keys.All(k=>TestLibrary().Find(k)!=null),"planet weather references converted purchased sources "+planet);
        }
        Check(EpicPlanetWeather.LayersFor(ArenaVariant.Moon).Length==0,"no precipitation in Moon vacuum");
        Check(EpicPlanetWeather.LayersFor(ArenaVariant.Kryon).Contains("WeatherSnow"),"cold Kryon uses snow");
        Check(EpicPlanetWeather.LayersFor(ArenaVariant.Mars).SequenceEqual(new[]{"WeatherWind"}),"dry Mars uses wind without rain");
        EpicReset();new GameObject("DF_MatchWorld");Camera.main=new GameObject("camera").AddComponent<Camera>();Camera.main.transform.position=Vector3.up*10;
        ArenaVariantRuntime.Current=ArenaVariant.Kryon;var weather=new GameObject("weather").AddComponent<EpicPlanetWeather>();GameObject.Invoke(weather,"Update");
        var layers=(GameObject[])Field(weather,"layers");Check(layers.Count(g=>g!=null)==2,"Kryon creates two bounded local weather layers");
        Check(layers.Where(g=>g!=null).SelectMany(g=>g.GetComponentsInChildren<ParticleSystem>(true)).Sum(p=>p.main.maxParticles)<=84,"weather has a separate maximum of 84 particles");
        Alterverse.Performance.AdaptiveVfxBudget.AllowDecorative=false;Step(.6f);GameObject.Invoke(weather,"Update");
        Check(layers.All(g=>g==null),"weather clears immediately at the next budget refresh under device pressure");
        Check(LiveSlots().Length==0,"weather cannot consume essential combat slots");
    }
    static void HealingStations()
    {
        EpicReset();var station=new GameObject("pod").AddComponent<HealingStationVfx>();station.Initialize("HealPod",1.5f);station.SetAvailable(true);
        GameObject.Invoke(station,"Update");Check(LiveSlots().Length==1,"available healing pod emits one idle layer");
        station.Pickup();Check(LiveSlots().All(s=>(HeroFxShape)Field(s,"Shape")!=HeroFxShape.Plus),"pickup clears idle healing immediately");
        Check(LiveSlots().Length==1&&(string)Field(Field(LiveSlots().Single(),"Epic"),"Key")=="HealPickup","pickup emits one distinct healing impulse");
        Step(1);GameObject.Invoke(station,"Update");Check(LiveSlots().Length==0,"unavailable pod does not emit idle healing");
        station.SetAvailable(true);GameObject.Invoke(station,"Update");Check(LiveSlots().Length==1,"respawn restores idle healing");
    }

    static void PresentationScaleAndContext()
    {
        EpicReset();var actor=Hero(HeroId.Veyr,0);
        HeroSkillVfx.Basic(actor,Vector3.forward);var visual=Field(LiveSlots().Single(),"Epic");
        Check((bool)Field(visual,"Bounded") && (float)Field(visual,"RenderRadius")<=(float)Field(visual,"FootprintRadius"),
            "new particles fit inside the actual attack footprint");
        EpicReset();actor=Hero(HeroId.Nyra,0);
        HeroVfxPool.Emit(HeroFxShape.Laser,Vector3.zero,Vector3.forward*4,new Vector3(.2f,.2f,1),Color.white,.4f,HeroFxMotion.LaserBeam,owner:actor);
        var beam=LiveSlots().Single();var mesh=((MeshFilter)Field(beam,"Filter")).sharedMesh;
        Check(Mathf.Abs(mesh.Vertices.Max(v=>Mathf.Abs(v.x))-.24f)<.001f&&mesh.Vertices.Min(v=>v.z)==0&&mesh.Vertices.Max(v=>v.z)==1,
            "beam width and authored endpoints are no longer enlarged");
        int published=0;Action<HeroVfxEvent> onCue=_=>published++;HeroVfxEvents.Emitted+=onCue;
        try
        {
            using(var outer=HeroVfxEvents.Begin(HeroVfxCue.Basic,actor))
            {
                using(var inner=HeroVfxEvents.UseAbility(actor,3,HeroVfxCue.Spell))
                    Check(HeroVfxEvents.AbilitySlot==3,"local signature context selects its ability");
                Check(HeroVfxEvents.AbilitySlot==-1&&HeroVfxEvents.AbilityCue==HeroVfxCue.Basic,
                    "nested local signature restores the outer action context");
            }
            Check(published==1&&HeroVfxEvents.AbilityActor==null,"local signature selection never emits an extra network cue");
        }
        finally{HeroVfxEvents.Emitted-=onCue;}
    }
}
