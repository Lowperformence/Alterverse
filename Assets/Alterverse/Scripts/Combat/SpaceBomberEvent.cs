using System;
using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>
    /// Neutral recurring battlefield event. Every 60 match-seconds one compact bomber
    /// crosses the arena on a varied route and drops readable, dodgeable bomb hazards.
    /// </summary>
    public sealed class SpaceBomberDirector : MonoBehaviour
    {
        public const float FlyoverInterval=60f;
        private const float BomberAltitude=7.4f;
        private const float BomberSpeed=12.5f;

        private static SpaceBomberDirector owner;
        private float elapsed,nextFlyover=FlyoverInterval;
        private int passIndex;
        private SpaceBomberFlyover activeFlyover;
        private Alterverse.Match.DimensionsFront front;
        private System.Random rng;
        private Material armorMaterial,darkMaterial,energyMaterial,warningMaterial,bombMaterial;
        private AudioClip engineLoop;
        private bool materialsReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()=>owner=null;

        private void Awake()
        {
            if(owner!=null&&owner!=this){enabled=false;Destroy(gameObject);return;}
            owner=this;
            front=FindAnyObjectByType<Alterverse.Match.DimensionsFront>();
            // Unity 6.6 marks GetInstanceID() obsolete. GetHashCode() is sufficient here
            // because this value is only used to decorrelate the local visual-event RNG seed.
            rng=new System.Random(unchecked(Environment.TickCount^GetHashCode()));
            BuildSharedMaterials();
            engineLoop=BuildBomberEngineLoop();
            if(!materialsReady)
            {
                Debug.LogError("DIMENSIONS FRONT: Space Bomber disabled because required shaders are unavailable.");
                enabled=false;
            }
        }

        private void Update()
        {
            front??=FindAnyObjectByType<Alterverse.Match.DimensionsFront>();
            if(front!=null&&front.HasEnded)
            {
                if(activeFlyover!=null)Destroy(activeFlyover.gameObject);
                activeFlyover=null;
                return;
            }
            if(Time.timeScale<=0f)return;

            elapsed+=Time.deltaTime;
            if(activeFlyover!=null)return;
            if(elapsed<nextFlyover)return;

            // Never "catch up" with multiple ships after a long pause or unusually long pass.
            do nextFlyover+=FlyoverInterval; while(nextFlyover<=elapsed);
            SpawnFlyover();
        }

        private void SpawnFlyover()
        {
            passIndex++;
            CreateRoute(out Vector3 start,out Vector3 end);
            GameObject root=new("DF_SpaceBomber");
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;
            if(world!=null)root.transform.SetParent(world,true);
            activeFlyover=root.AddComponent<SpaceBomberFlyover>();
            activeFlyover.Initialize(this,start,end,BomberAltitude,BomberSpeed,passIndex,
                armorMaterial,darkMaterial,energyMaterial,warningMaterial,bombMaterial,engineLoop,front,rng);
        }

        private void CreateRoute(out Vector3 start,out Vector3 end)
        {
            int route=rng.Next(0,4);
            float laneOffset=Next(-23f,23f);
            float slant=Next(-7f,7f);
            switch(route)
            {
                case 0:
                    start=new Vector3(-48f,BomberAltitude,laneOffset);
                    end=new Vector3(48f,BomberAltitude,Mathf.Clamp(laneOffset+slant,-27f,27f));
                    break;
                case 1:
                    start=new Vector3(48f,BomberAltitude,laneOffset);
                    end=new Vector3(-48f,BomberAltitude,Mathf.Clamp(laneOffset+slant,-27f,27f));
                    break;
                case 2:
                    start=new Vector3(-47f,BomberAltitude,Next(20f,29f));
                    end=new Vector3(47f,BomberAltitude,Next(-29f,-20f));
                    break;
                default:
                    start=new Vector3(-47f,BomberAltitude,Next(-29f,-20f));
                    end=new Vector3(47f,BomberAltitude,Next(20f,29f));
                    if(rng.Next(0,2)==1){Vector3 swap=start;start=end;end=swap;}
                    break;
            }
        }

        private float Next(float min,float max)=>(float)(min+(max-min)*rng.NextDouble());

        internal void FlyoverFinished(SpaceBomberFlyover flyover)
        {
            if(activeFlyover==flyover)activeFlyover=null;
        }

        private void BuildSharedMaterials()
        {
            Shader lit=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Shader unlit=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");
            if(lit==null||unlit==null)return;

            armorMaterial=CreateMaterial(lit,"DF_BomberArmor",new Color(.20f,.235f,.28f),.66f,.34f,Color.black);
            darkMaterial=CreateMaterial(lit,"DF_BomberDark",new Color(.035f,.045f,.065f),.48f,.22f,Color.black);
            energyMaterial=CreateMaterial(lit,"DF_BomberEnergy",new Color(.16f,.88f,1f),.18f,.72f,new Color(.16f,.88f,1f)*3.0f);
            bombMaterial=CreateMaterial(lit,"DF_BombShell",new Color(.14f,.15f,.17f),.72f,.26f,new Color(.95f,.22f,.05f)*.35f);
            warningMaterial=new Material(unlit){name="DF_BombWarning"};
            materialsReady=armorMaterial!=null&&darkMaterial!=null&&energyMaterial!=null&&bombMaterial!=null&&warningMaterial!=null;
        }

        private static AudioClip CreateJetWhine()
        {
            const int rate=44100;
            const float duration=2f;
            int length=Mathf.CeilToInt(rate*duration);
            float[] samples=new float[length];
            uint noiseState=0x91E10DA5u;
            float previousNoise=0f;

            for(int i=0;i<length;i++)
            {
                float t=i/(float)rate;
                float fm=.52f*Mathf.Sin(t*Mathf.PI*2f*3f)+.16f*Mathf.Sin(t*Mathf.PI*2f*7f);
                float carrier=Mathf.Sin(t*Mathf.PI*2f*920f+fm);
                float upper=Mathf.Sin(t*Mathf.PI*2f*1840f+fm*1.25f+.65f);
                float whistle=Mathf.Sin(t*Mathf.PI*2f*1370f+.24f*Mathf.Sin(t*Mathf.PI*2f*2f));

                noiseState^=noiseState<<13;noiseState^=noiseState>>17;noiseState^=noiseState<<5;
                float raw=((noiseState&0xFFFF)/32767.5f)-1f;
                float hiss=raw-previousNoise*.82f;previousNoise=raw;

                float breathe=.90f+.10f*Mathf.Sin(t*Mathf.PI*2f*2f);
                float mixed=(carrier*.50f+upper*.18f+whistle*.15f+hiss*.075f)*breathe*.44f;
                samples[i]=Mathf.Clamp(mixed,-.82f,.82f);
            }

            // Blend the loop seam so the continuous turbine whine never clicks or "revs" like a car engine.
            int fade=Mathf.Min(1024,length/8);
            for(int i=0;i<fade;i++)
            {
                float a=i/(float)(fade-1);
                int tail=length-fade+i;
                samples[tail]=Mathf.Lerp(samples[tail],samples[i],a);
            }

            AudioClip clip=AudioClip.Create("DF_SpaceBomber_JetWhine",length,1,rate,false);
            clip.SetData(samples,0);
            return clip;
        }

        private static Material CreateMaterial(Shader shader,string name,Color color,float metallic,float smoothness,Color emission)
        {
            Material m=new(shader){name=name,color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
            if(emission.maxColorComponent>0f&&m.HasProperty("_EmissionColor"))
            {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}
            return m;
        }

        private static AudioClip BuildBomberEngineLoop()
        {
            const int rate=44100;
            const float seconds=9.0f;
            int length=Mathf.CeilToInt(rate*seconds);
            float[] samples=new float[length];

            uint noiseState=0xA511E9B3u;
            float whinePhase=0f,harmonicPhase=0f,airState=0f;

            for(int i=0;i<length;i++)
            {
                float t=i/(float)rate;

                noiseState^=noiseState<<13;
                noiseState^=noiseState>>17;
                noiseState^=noiseState<<5;
                float white=((noiseState&0xFFFF)/32767.5f)-1f;

                // Continuous high ion/jet howl: intentionally no low piston/turbine rhythm,
                // so it reads as a fast spacecraft rather than a car engine.
                float whineHz=1080f
                    +Mathf.Sin(t*Mathf.PI*2f*.23f)*38f
                    +Mathf.Sin(t*Mathf.PI*2f*.71f)*14f;
                whinePhase+=Mathf.PI*2f*whineHz/rate;

                float harmonicHz=1680f+Mathf.Sin(t*Mathf.PI*2f*.31f)*52f;
                harmonicPhase+=Mathf.PI*2f*harmonicHz/rate;

                float carrier=Mathf.Sin(whinePhase)*.46f;
                float overtone=Mathf.Sin(harmonicPhase+.18f*Mathf.Sin(whinePhase*.5f))*.21f;

                // Narrow airy hiss adds speed without turning into a roaring vehicle bed.
                airState+=(white-airState)*.18f;
                float hiss=(white-airState)*.11f;

                // Very slow amplitude breathing keeps the howl alive while remaining continuous.
                float breath=.93f+.07f*Mathf.Sin(t*Mathf.PI*2f*.36f);
                float mixed=(carrier+overtone+hiss)*breath;

                // Conservative saturation: aggressive tone, deliberately modest loudness.
                samples[i]=Mathf.Clamp(mixed/(1f+Mathf.Abs(mixed)*.62f),-1f,1f);
            }

            AudioClip clip=AudioClip.Create("DF_BomberHighIonHowl",length,1,rate,false);
            clip.SetData(samples,0);
            return clip;
        }

        private void OnDestroy()
        {
            if(owner==this)owner=null;
            if(armorMaterial!=null)Destroy(armorMaterial);
            if(darkMaterial!=null)Destroy(darkMaterial);
            if(energyMaterial!=null)Destroy(energyMaterial);
            if(warningMaterial!=null)Destroy(warningMaterial);
            if(bombMaterial!=null)Destroy(bombMaterial);
            if(engineLoop!=null)Destroy(engineLoop);
        }
    }

    public sealed class SpaceBomberFlyover : MonoBehaviour
    {
        private const float FirstDropDistance=10f;
        private const float DropSpacing=8.5f;
        private const float EndMargin=9f;
        private const float BombTelegraph=1.28f;
        private const float BombDamage=12f;

        private SpaceBomberDirector director;
        private Vector3 start,end;
        private float speed,length,travelled,nextDrop;
        private int passIndex;
        private Transform visual,engineGlowL,engineGlowR;
        private Material warningMaterial,bombMaterial,energyMaterial;
        private AudioSource jetAudio;
        private AudioSource engineAudio;
        private Alterverse.Match.DimensionsFront front;
        private System.Random rng;
        private bool completed;

        public void Initialize(SpaceBomberDirector source,Vector3 routeStart,Vector3 routeEnd,float altitude,float flightSpeed,int pass,
            Material armor,Material dark,Material energy,Material warning,Material bomb,AudioClip engineClip,Alterverse.Match.DimensionsFront matchFront,System.Random random)
        {
            director=source;start=routeStart;end=routeEnd;start.y=end.y=altitude;
            speed=Mathf.Max(2f,flightSpeed);length=Vector3.Distance(start,end);passIndex=pass;
            warningMaterial=warning;bombMaterial=bomb;energyMaterial=energy;front=matchFront;rng=random;
            transform.position=start;
            Vector3 direction=end-start;direction.y=0;
            if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
            nextDrop=FirstDropDistance;
            BuildVisual(armor,dark,energy);
            BuildEngineAudio(engineClip);
            PrototypeVfx.Burst(start+Vector3.down*.35f,new Color(.16f,.88f,1f),.45f,4);
        }

        private void Update()
        {
            if(completed)return;
            if(front==null)front=FindAnyObjectByType<Alterverse.Match.DimensionsFront>();
            if(front!=null&&front.HasEnded){Finish();return;}
            if(Time.timeScale<=0f)
            {
                if(engineAudio!=null&&engineAudio.isPlaying)engineAudio.Pause();
                return;
            }
            if(engineAudio!=null&&!engineAudio.isPlaying)engineAudio.UnPause();

            travelled=Mathf.Min(length,travelled+speed*Time.deltaTime);
            float t=length>.01f?travelled/length:1f;
            transform.position=Vector3.Lerp(start,end,t);

            if(visual!=null)
            {
                float bank=Mathf.Sin((t*2.2f+passIndex*.37f)*Mathf.PI*2f)*2.2f;
                visual.localRotation=Quaternion.Euler(-3.5f,0,bank);
            }
            PulseEngines();
            UpdateEngineAudio(t);

            while(nextDrop<=travelled&&nextDrop<length-EndMargin)
            {
                float dropT=nextDrop/length;
                Vector3 routePoint=Vector3.Lerp(start,end,dropT);
                DropBomb(routePoint);
                nextDrop+=DropSpacing;
            }

            if(travelled>=length)Finish();
        }

        private void DropBomb(Vector3 routePoint)
        {
            Vector3 forward=end-start;forward.y=0;
            if(forward.sqrMagnitude<.01f)return;
            Vector3 lateral=Vector3.Cross(Vector3.up,forward.normalized);
            float jitter=(float)(rng.NextDouble()*2.2-1.1);
            float x=Mathf.Clamp(routePoint.x+lateral.x*jitter,-39.5f,39.5f);
            float z=Mathf.Clamp(routePoint.z+lateral.z*jitter,-32.5f,32.5f);
            Vector3 ground=LunarSurfaceField.Surface(x,z);
            SpaceBombStrike.Create(ground,routePoint.y-.55f,BombTelegraph,BombDamage,warningMaterial,bombMaterial,energyMaterial,front);
        }

        private void BuildEngineAudio(AudioClip clip)
        {
            if(clip==null)return;
            engineAudio=gameObject.AddComponent<AudioSource>();
            engineAudio.clip=clip;
            engineAudio.loop=false;
            engineAudio.playOnAwake=false;
            engineAudio.spatialBlend=.62f;
            engineAudio.dopplerLevel=0f;
            engineAudio.rolloffMode=AudioRolloffMode.Linear;
            engineAudio.minDistance=8f;
            engineAudio.maxDistance=58f;
            engineAudio.spread=24f;
            engineAudio.priority=108;
            engineAudio.volume=.22f*PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;
            engineAudio.pitch=1f;
            engineAudio.Play();
        }

        private void UpdateEngineAudio(float routeProgress)
        {
            if(engineAudio==null)return;

            // Keep the pitch almost fixed. A large pitch sweep was reading like a revving car.
            // The generated clip already contains the sci-fi modulation; route position only changes loudness.
            engineAudio.pitch=1f+Mathf.Sin(Time.time*.82f)*.008f;
            float centerPresence=1f-Mathf.Abs(routeProgress*2f-1f);
            float gain=Mathf.Lerp(.14f,.24f,Mathf.Clamp01(centerPresence));
            engineAudio.volume=gain*PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;
        }

        private void StartJetAudio(AudioClip clip)
        {
            if(clip==null)return;
            jetAudio=gameObject.AddComponent<AudioSource>();
            jetAudio.clip=clip;jetAudio.loop=true;jetAudio.playOnAwake=false;
            jetAudio.spatialBlend=.18f;jetAudio.dopplerLevel=0f;jetAudio.priority=140;
            jetAudio.rolloffMode=AudioRolloffMode.Linear;jetAudio.minDistance=8f;jetAudio.maxDistance=75f;
            jetAudio.volume=.14f*PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;
            AudioHighPassFilter high=gameObject.AddComponent<AudioHighPassFilter>();high.cutoffFrequency=420f;high.highpassResonanceQ=1.1f;
            AudioLowPassFilter low=gameObject.AddComponent<AudioLowPassFilter>();low.cutoffFrequency=4300f;low.lowpassResonanceQ=1.05f;
            jetAudio.Play();
        }

        private void PulseEngines()
        {
            float pulse=.92f+Mathf.Sin(Time.time*11f)*.08f;
            if(engineGlowL!=null)engineGlowL.localScale=new Vector3(.18f,.18f,.34f*pulse);
            if(engineGlowR!=null)engineGlowR.localScale=new Vector3(.18f,.18f,.34f*pulse);
        }

        private void Finish()
        {
            if(completed)return;
            completed=true;
            if(engineAudio!=null)engineAudio.Stop();
            director?.FlyoverFinished(this);
            Destroy(gameObject);
        }

        private void BuildVisual(Material armor,Material dark,Material energy)
        {
            visual=new GameObject("DF_BomberVisual").transform;
            visual.SetParent(transform,false);
            visual.localScale=Vector3.one*.78f;

            // Compact layered silhouette: narrow fuselage and swept wings keep combat readable below.
            Part("Fuselage",PrimitiveType.Capsule,visual,new Vector3(0,0,.05f),new Vector3(.58f,1.52f,.58f),armor,Quaternion.Euler(90,0,0),true);
            Part("Nose",PrimitiveType.Sphere,visual,new Vector3(0,.02f,1.70f),new Vector3(.48f,.34f,.78f),armor,Quaternion.identity,true);
            Part("Spine",PrimitiveType.Cube,visual,new Vector3(0,.22f,.10f),new Vector3(.42f,.20f,2.42f),dark,Quaternion.identity,true);
            Part("Canopy",PrimitiveType.Sphere,visual,new Vector3(0,.37f,.74f),new Vector3(.42f,.20f,.58f),energy,Quaternion.identity,false);

            Part("WingL",PrimitiveType.Cube,visual,new Vector3(-1.10f,0,-.05f),new Vector3(1.62f,.10f,.92f),armor,Quaternion.Euler(0,-24f,-3f),true);
            Part("WingR",PrimitiveType.Cube,visual,new Vector3(1.10f,0,-.05f),new Vector3(1.62f,.10f,.92f),armor,Quaternion.Euler(0,24f,3f),true);
            Part("WingEdgeL",PrimitiveType.Cube,visual,new Vector3(-1.66f,.02f,-.34f),new Vector3(.68f,.07f,.18f),energy,Quaternion.Euler(0,-24f,0),false);
            Part("WingEdgeR",PrimitiveType.Cube,visual,new Vector3(1.66f,.02f,-.34f),new Vector3(.68f,.07f,.18f),energy,Quaternion.Euler(0,24f,0),false);

            Part("EngineL",PrimitiveType.Cylinder,visual,new Vector3(-.54f,.02f,-1.23f),new Vector3(.30f,.58f,.30f),dark,Quaternion.Euler(90,0,0),true);
            Part("EngineR",PrimitiveType.Cylinder,visual,new Vector3(.54f,.02f,-1.23f),new Vector3(.30f,.58f,.30f),dark,Quaternion.Euler(90,0,0),true);
            engineGlowL=Part("ThrusterL",PrimitiveType.Sphere,visual,new Vector3(-.54f,.02f,-1.78f),new Vector3(.18f,.18f,.34f),energy,Quaternion.identity,false);
            engineGlowR=Part("ThrusterR",PrimitiveType.Sphere,visual,new Vector3(.54f,.02f,-1.78f),new Vector3(.18f,.18f,.34f),energy,Quaternion.identity,false);

            Part("TailL",PrimitiveType.Cube,visual,new Vector3(-.44f,.40f,-1.28f),new Vector3(.12f,.72f,.66f),armor,Quaternion.Euler(-8,0,-12),true);
            Part("TailR",PrimitiveType.Cube,visual,new Vector3(.44f,.40f,-1.28f),new Vector3(.12f,.72f,.66f),armor,Quaternion.Euler(-8,0,12),true);
            Part("BombBayL",PrimitiveType.Cube,visual,new Vector3(-.25f,-.34f,-.05f),new Vector3(.20f,.08f,1.18f),dark,Quaternion.identity,false);
            Part("BombBayR",PrimitiveType.Cube,visual,new Vector3(.25f,-.34f,-.05f),new Vector3(.20f,.08f,1.18f),dark,Quaternion.identity,false);
            Part("ArmorCheekL",PrimitiveType.Cube,visual,new Vector3(-.43f,.08f,.58f),new Vector3(.18f,.18f,.78f),dark,Quaternion.Euler(0,-7f,0),true);
            Part("ArmorCheekR",PrimitiveType.Cube,visual,new Vector3(.43f,.08f,.58f),new Vector3(.18f,.18f,.78f),dark,Quaternion.Euler(0,7f,0),true);
            Part("EngineCollarL",PrimitiveType.Cylinder,visual,new Vector3(-.54f,.02f,-1.60f),new Vector3(.34f,.10f,.34f),armor,Quaternion.Euler(90,0,0),true);
            Part("EngineCollarR",PrimitiveType.Cylinder,visual,new Vector3(.54f,.02f,-1.60f),new Vector3(.34f,.10f,.34f),armor,Quaternion.Euler(90,0,0),true);
            Part("WingTipL",PrimitiveType.Cube,visual,new Vector3(-1.95f,.01f,-.45f),new Vector3(.24f,.12f,.42f),dark,Quaternion.Euler(0,-24f,-5f),true);
            Part("WingTipR",PrimitiveType.Cube,visual,new Vector3(1.95f,.01f,-.45f),new Vector3(.24f,.12f,.42f),dark,Quaternion.Euler(0,24f,5f),true);
            Part("DorsalSensor",PrimitiveType.Cube,visual,new Vector3(0,.50f,-.22f),new Vector3(.18f,.28f,.42f),armor,Quaternion.Euler(-12f,0,0),true);
            Part("StatusLight",PrimitiveType.Sphere,visual,new Vector3(0,.60f,-.15f),new Vector3(.11f,.08f,.11f),energy,Quaternion.identity,false);
        }

        private static Transform Part(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material material,Quaternion rotation,bool shadows)
        {
            GameObject p=GameObject.CreatePrimitive(type);p.name="DF_Bomber_"+name;p.transform.SetParent(parent,false);
            p.transform.localPosition=pos;p.transform.localScale=scale;p.transform.localRotation=rotation;
            Collider c=p.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=p.GetComponent<Renderer>();
            if(r!=null){r.sharedMaterial=material;r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=false;}
            return p.transform;
        }

        private void OnDestroy()
        {
            if(!completed)director?.FlyoverFinished(this);
        }
    }

    public sealed class SpaceBombStrike : MonoBehaviour
    {
        private const float ImpactRadius=1.8f;
        private const int HitBufferSize=32;
        private readonly Collider[] hitBuffer=new Collider[HitBufferSize];

        private float hitAt,spawnAt,startHeight,damage;
        private Vector3 target;
        private GameObject warning,bomb;
        private Transform blink,finRoot;
        private LineRenderer warningLine;
        private Alterverse.Match.DimensionsFront front;
        private bool resolved;

        public static void Create(Vector3 ground,float height,float delay,float dmg,Material warningMaterial,Material bombMaterial,Material energyMaterial,Alterverse.Match.DimensionsFront matchFront)
        {
            GameObject g=new("DF_SpaceBombStrike");
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;
            if(world!=null)g.transform.SetParent(world,true);
            SpaceBombStrike strike=g.AddComponent<SpaceBombStrike>();
            strike.target=ground;strike.transform.position=ground;strike.startHeight=Mathf.Max(5.5f,height);
            strike.spawnAt=Time.time;strike.hitAt=Time.time+Mathf.Max(.55f,delay);strike.damage=Mathf.Max(1f,dmg);strike.front=matchFront;
            strike.BuildVisuals(warningMaterial,bombMaterial,energyMaterial,world);
        }

        private void BuildVisuals(Material warningMaterial,Material bombMaterial,Material energyMaterial,Transform world)
        {
            if(warningMaterial!=null)
            {
                warning=new GameObject("DF_BombWarning");warning.transform.SetParent(world,false);
                warningLine=warning.AddComponent<LineRenderer>();warningLine.sharedMaterial=warningMaterial;warningLine.loop=true;
                warningLine.useWorldSpace=true;warningLine.positionCount=36;warningLine.widthMultiplier=.065f;
                warningLine.startColor=warningLine.endColor=new Color(1f,.22f,.07f,.95f);
                warningLine.shadowCastingMode=ShadowCastingMode.Off;warningLine.receiveShadows=false;
                for(int i=0;i<36;i++)
                {
                    float angle=i*Mathf.PI*2f/36f;
                    warningLine.SetPosition(i,new Vector3(target.x+Mathf.Cos(angle)*ImpactRadius,target.y+.09f,target.z+Mathf.Sin(angle)*ImpactRadius));
                }
            }

            bomb=new GameObject("DF_FallingBomb");bomb.transform.SetParent(world,false);
            bomb.transform.position=new Vector3(target.x,startHeight,target.z);
            Transform body=Primitive("Body",PrimitiveType.Capsule,bomb.transform,Vector3.zero,new Vector3(.24f,.44f,.24f),bombMaterial,Quaternion.identity);
            Primitive("Nose",PrimitiveType.Sphere,bomb.transform,new Vector3(0,-.43f,0),new Vector3(.22f,.28f,.22f),bombMaterial,Quaternion.identity);
            finRoot=new GameObject("DF_BombFins").transform;finRoot.SetParent(bomb.transform,false);finRoot.localPosition=new Vector3(0,.35f,0);
            Primitive("FinX",PrimitiveType.Cube,finRoot,Vector3.zero,new Vector3(.58f,.06f,.16f),bombMaterial,Quaternion.identity);
            Primitive("FinZ",PrimitiveType.Cube,finRoot,Vector3.zero,new Vector3(.16f,.06f,.58f),bombMaterial,Quaternion.identity);
            blink=Primitive("WarningCore",PrimitiveType.Cylinder,bomb.transform,new Vector3(0,.05f,0),new Vector3(.285f,.045f,.285f),energyMaterial,Quaternion.identity);
            if(body!=null)body.localRotation=Quaternion.Euler(0,0,2f);
        }

        private void Update()
        {
            if(resolved)return;
            if(front==null)front=FindAnyObjectByType<Alterverse.Match.DimensionsFront>();
            if(front!=null&&front.HasEnded){Cleanup();return;}
            if(Time.timeScale<=0f)return;

            float duration=Mathf.Max(.01f,hitAt-spawnAt);
            float t=Mathf.Clamp01((Time.time-spawnAt)/duration);
            if(Time.time<hitAt)
            {
                if(bomb!=null)
                {
                    float eased=t*t;
                    float y=Mathf.Lerp(startHeight,target.y+.30f,eased);
                    bomb.transform.position=new Vector3(target.x,y,target.z);
                    bomb.transform.Rotate(80f*Time.deltaTime,32f*Time.deltaTime,15f*Time.deltaTime,Space.Self);
                }
                if(finRoot!=null)finRoot.Rotate(0,120f*Time.deltaTime,0,Space.Self);
                if(blink!=null)
                {
                    float p=.82f+Mathf.Sin(Time.time*17f)*.18f;
                    blink.localScale=new Vector3(.285f*p,.045f,.285f*p);
                }
                if(warningLine!=null)warningLine.widthMultiplier=.055f+.035f*t;
                return;
            }

            ResolveImpact();
        }

        private void ResolveImpact()
        {
            if(resolved)return;resolved=true;
            Vector3 impact=new(target.x,target.y+.25f,target.z);
            CombatImpactPool.Emit(impact,new Color(1f,.34f,.10f),1);
            PrototypeVfx.Shockwave(impact,new Color(1f,.28f,.08f),ImpactRadius*1.05f);

            int hits=Physics.OverlapSphereNonAlloc(target,ImpactRadius,hitBuffer);
            for(int i=0;i<hits;i++)
            {
                Collider c=hitBuffer[i];if(c==null)continue;
                Health h=c.GetComponentInParent<Health>();
                if(h==null||h.IsDead)continue;

                bool duplicate=false;
                for(int j=0;j<i;j++)
                {
                    Collider prior=hitBuffer[j];
                    if(prior!=null&&prior.GetComponentInParent<Health>()==h){duplicate=true;break;}
                }
                if(duplicate)continue;
                if(h.GetComponent<RealityCore>()!=null||h.GetComponent<LaneTower>()!=null||h.GetComponent<Alterverse.Match.DimensionPlanet>()!=null)continue;
                if(h.GetComponent<TeamMember>()==null&&h.GetComponent<LaneMinion>()==null)continue;
                h.TakeDamage(damage,gameObject);
            }
            Cleanup();
        }

        private void Cleanup()
        {
            if(warning!=null)Destroy(warning);
            if(bomb!=null)Destroy(bomb);
            Destroy(gameObject);
        }

        private static Transform Primitive(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material material,Quaternion rotation)
        {
            GameObject p=GameObject.CreatePrimitive(type);p.name="DF_Bomb_"+name;p.transform.SetParent(parent,false);
            p.transform.localPosition=pos;p.transform.localScale=scale;p.transform.localRotation=rotation;
            Collider c=p.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=p.GetComponent<Renderer>();if(r!=null){r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            return p.transform;
        }

        private void OnDestroy()
        {
            if(warning!=null)Destroy(warning);
            if(bomb!=null)Destroy(bomb);
        }
    }
}
