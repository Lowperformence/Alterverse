using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Final Pyraxis presentation layer. Replaces the old straight lava streaks with irregular
    /// pools, adds mirrored cosmetic volcanoes for both teams, and upgrades the Hot Zone scorpion
    /// to a connected articulated body with an eight-leg gait. Everything in this file is visual:
    /// no colliders, damage, navigation blockers or team-specific gameplay advantages are added.
    /// </summary>
    public sealed class PyraxisArenaPolishRuntime : MonoBehaviour
    {
        const string ManagerName="DF_PyraxisArenaPolishRuntime";
        const string RootName="DF_PyraxisArenaPolish";
        readonly List<Material> ownedMaterials=new();
        Transform arenaRoot,projectileRoot;
        Transform activeWorld;
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find(ManagerName)!=null)return;
            GameObject go=new(ManagerName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PyraxisArenaPolishRuntime>();
        }

        void Update()
        {
            GameObject worldObject=GameObject.Find("DF_MatchWorld");
            bool active=worldObject!=null&&ArenaVariantRuntime.Current==ArenaVariant.Pyraxis;
            if(!active)
            {
                if(arenaRoot!=null)ClearArena();
                return;
            }

            Transform world=worldObject.transform;
            if(arenaRoot==null||activeWorld!=world)
            {
                ClearArena();
                activeWorld=world;
                BuildArena(world);
            }

            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.35f;
            RemoveLegacyLavaStreams(world);
            UpgradeScorpions();
        }

        void BuildArena(Transform world)
        {
            GameObject root=new(RootName);
            arenaRoot=root.transform;
            arenaRoot.SetParent(world,false);
            projectileRoot=new GameObject("DF_PyraxisMeteorProjectiles").transform;
            projectileRoot.SetParent(arenaRoot,false);

            Material basalt=Material("DF_PyraxisPolish_Basalt",new Color(.025f,.018f,.016f,1f),.16f,.20f);
            Material basaltHot=Material("DF_PyraxisPolish_HotBasalt",new Color(.10f,.025f,.010f,1f),.10f,.24f);
            Material lava=Material("DF_PyraxisPolish_Lava",new Color(1f,.12f,.008f,1f),0f,.58f,true,4.8f);
            Material lavaHot=Material("DF_PyraxisPolish_LavaHot",new Color(1f,.38f,.018f,1f),0f,.64f,true,5.8f);
            Material meteor=Material("DF_PyraxisPolish_Meteor",new Color(1f,.035f,.010f,1f),0f,.42f,true,6.5f);

            // Three locations are mirrored across X. Each team therefore receives the same number,
            // footprint and distance profile of lava pools.
            Vector3[] halfPools={new(28f,0,19f),new(24f,0,-23f),new(37f,0,-2f)};
            for(int i=0;i<halfPools.Length;i++)
            {
                Vector3 source=halfPools[i];
                BuildLavaPool(new Vector3(-source.x,0,source.z),i,basalt,basaltHot,lava,lavaHot);
                BuildLavaPool(new Vector3(source.x,0,source.z),i,basalt,basaltHot,lava,lavaHot);
            }

            // Two volcanoes per team at exactly mirrored coordinates. Mirrored partners share the
            // same firing phase and cadence, so the spectacle is symmetrical as well as placement.
            Vector3[] halfVolcanoes={new(39f,0,25f),new(39f,0,-25f)};
            for(int pair=0;pair<halfVolcanoes.Length;pair++)
            {
                Vector3 source=halfVolcanoes[pair];
                BuildVolcano(new Vector3(-source.x,0,source.z),-1f,pair,basalt,basaltHot,lavaHot,meteor);
                BuildVolcano(new Vector3(source.x,0,source.z),1f,pair,basalt,basaltHot,lavaHot,meteor);
            }
        }

        void BuildLavaPool(Vector3 location,int seed,Material basalt,Material basaltHot,Material lava,Material lavaHot)
        {
            Vector3 center=Surface(location.x,location.z,.028f);
            Transform pool=new GameObject("DF_Pyraxis_LavaPool").transform;
            pool.SetParent(arenaRoot,false);
            pool.localPosition=center;
            pool.localRotation=Quaternion.Euler(0,seed*23f,0);

            // Overlapping discs avoid the old artificial straight-line look and read as one broken,
            // naturally pooled lava basin from the gameplay camera.
            Part(PrimitiveType.Cylinder,"LavaPoolCore",pool,Vector3.zero,new Vector3(4.4f,.055f,2.75f),Quaternion.identity,lava);
            Part(PrimitiveType.Cylinder,"LavaPoolLobeA",pool,new Vector3(1.75f,.006f,.42f),new Vector3(2.55f,.052f,1.85f),Quaternion.Euler(0,27f,0),lavaHot);
            Part(PrimitiveType.Cylinder,"LavaPoolLobeB",pool,new Vector3(-1.55f,.004f,-.48f),new Vector3(2.25f,.050f,1.60f),Quaternion.Euler(0,-31f,0),lava);
            Part(PrimitiveType.Cylinder,"LavaPoolHotCenter",pool,new Vector3(.25f,.012f,-.08f),new Vector3(1.70f,.046f,1.05f),Quaternion.Euler(0,18f,0),lavaHot);

            const int rimCount=12;
            for(int r=0;r<rimCount;r++)
            {
                float a=(r/(float)rimCount)*Mathf.PI*2f;
                float wobble=1f+Mathf.Sin(r*2.31f+seed)*.10f;
                Vector3 p=new(Mathf.Cos(a)*4.65f*wobble,.18f,Mathf.Sin(a)*2.95f*wobble);
                Vector3 scale=new(.72f+(r%3)*.14f,.34f+(r%2)*.12f,.58f+(r%4)*.08f);
                Part(PrimitiveType.Sphere,"BasaltPoolRim",pool,p,scale,Quaternion.Euler((r%3)*7f,r*31f,8f-(r%2)*13f),r%4==0?basaltHot:basalt);
            }
        }

        void BuildVolcano(Vector3 location,float side,int pairIndex,Material basalt,Material basaltHot,Material lavaHot,Material meteor)
        {
            Vector3 basePosition=Surface(location.x,location.z,0f);
            Transform volcano=new GameObject("DF_Pyraxis_Volcano").transform;
            volcano.SetParent(arenaRoot,false);
            volcano.localPosition=basePosition;
            volcano.localRotation=Quaternion.Euler(0,pairIndex*31f,0);

            Part(PrimitiveType.Sphere,"VolcanoBase",volcano,new Vector3(0,1.00f,0),new Vector3(3.7f,1.65f,3.45f),Quaternion.identity,basalt);
            Part(PrimitiveType.Sphere,"VolcanoMid",volcano,new Vector3(.20f,2.05f,-.10f),new Vector3(2.75f,1.55f,2.55f),Quaternion.Euler(0,22f,0),basalt);
            Part(PrimitiveType.Sphere,"VolcanoPeak",volcano,new Vector3(-.12f,3.05f,.10f),new Vector3(1.85f,1.18f,1.72f),Quaternion.Euler(0,-18f,0),basaltHot);

            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2f/10f;
                Vector3 p=new(Mathf.Cos(a)*1.48f,3.56f+Mathf.Sin(i*1.9f)*.08f,Mathf.Sin(a)*1.35f);
                Part(PrimitiveType.Sphere,"CraterRim",volcano,p,new Vector3(.62f,.31f,.54f),Quaternion.Euler(i*4f,i*37f,0),i%3==0?basaltHot:basalt);
            }
            Part(PrimitiveType.Cylinder,"CraterLava",volcano,new Vector3(0,3.52f,0),new Vector3(1.30f,.050f,1.16f),Quaternion.identity,lavaHot);

            Transform muzzle=new GameObject("MeteorMuzzle").transform;
            muzzle.SetParent(volcano,false);
            muzzle.localPosition=new Vector3(0,3.80f,0);

            PyraxisVolcanoMeteorEmitter emitter=volcano.gameObject.AddComponent<PyraxisVolcanoMeteorEmitter>();
            emitter.Configure(muzzle,projectileRoot,meteor,side,pairIndex,location.z);
        }

        void RemoveLegacyLavaStreams(Transform world)
        {
            Transform[] all=world.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(t!=null&&t.name=="LavaStream")Object.Destroy(t.gameObject);
            }
        }

        void UpgradeScorpions()
        {
            HotZoneTank[] monsters=Object.FindObjectsByType<HotZoneTank>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<monsters.Length;i++)
            {
                HotZoneTank monster=monsters[i];
                if(monster==null)continue;
                // Wait until the original Pyraxis replacement has initialized; this guarantees that
                // its legacy HideExistingMeshes pass cannot turn off the new polished renderers.
                if(monster.GetComponent<PyraxisScorpionVisualOverride>()==null)continue;
                if(monster.GetComponent<PyraxisScorpionPolish>()==null)
                    monster.gameObject.AddComponent<PyraxisScorpionPolish>();
            }
        }

        Vector3 Surface(float x,float z,float y)
        {
            Vector3 p=LunarSurfaceField.Surface(x,z);
            p.y+=y;
            return p;
        }

        Material Material(string name,Color color,float metallic,float smoothness,bool emission=false,float emissionPower=1f)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material material=new(shader){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(emission)
            {
                material.EnableKeyword("_EMISSION");
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*emissionPower);
            }
            ownedMaterials.Add(material);
            return material;
        }

        static GameObject Part(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Quaternion localRotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            go.transform.localRotation=localRotation;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null)Object.Destroy(collider);
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            return go;
        }

        void ClearArena()
        {
            if(arenaRoot!=null)Object.Destroy(arenaRoot.gameObject);
            arenaRoot=null;
            projectileRoot=null;
            activeWorld=null;
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)Object.Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }

        void OnDestroy()=>ClearArena();
    }

    /// <summary>Cosmetic, synchronized Pyraxis eruption. It never applies gameplay damage.</summary>
    public sealed class PyraxisVolcanoMeteorEmitter : MonoBehaviour
    {
        Transform muzzle,projectileParent;
        Material meteorMaterial;
        float side,zSign,nextShot;
        int pairIndex,shotSerial;

        public void Configure(Transform source,Transform parent,Material material,float teamSide,int mirroredPair,float sourceZ)
        {
            muzzle=source;
            projectileParent=parent;
            meteorMaterial=material;
            side=Mathf.Sign(teamSide);
            zSign=Mathf.Sign(sourceZ);
            pairIndex=mirroredPair;
            // Both members of a mirrored pair receive the exact same start delay.
            nextShot=Time.time+1.5f+pairIndex*1.35f;
        }

        void Update()
        {
            if(muzzle==null||projectileParent==null||ArenaVariantRuntime.Current!=ArenaVariant.Pyraxis)return;
            if(Time.time<nextShot)return;
            Fire();
            nextShot=Time.time+5.2f;
        }

        void Fire()
        {
            float wave=Mathf.Sin(shotSerial*1.73f+pairIndex*.82f);
            float vertical=9.8f+Mathf.Abs(wave)*1.3f;
            Vector3 velocity=new(-side*(2.15f+.45f*wave),vertical,-zSign*(1.05f-.25f*wave));
            shotSerial++;

            GameObject meteor=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor.name="DF_Pyraxis_RedMeteor";
            meteor.transform.SetParent(projectileParent,true);
            meteor.transform.position=muzzle.position;
            meteor.transform.localScale=new Vector3(.34f,.34f,.48f);
            Collider collider=meteor.GetComponent<Collider>();
            if(collider!=null)Object.Destroy(collider);
            Renderer renderer=meteor.GetComponent<Renderer>();
            if(renderer!=null&&meteorMaterial!=null)renderer.sharedMaterial=meteorMaterial;

            GameObject tail=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tail.name="MeteorTail";
            tail.transform.SetParent(meteor.transform,false);
            tail.transform.localPosition=new Vector3(0,-.70f,0);
            tail.transform.localScale=new Vector3(.13f,.65f,.13f);
            Collider tailCollider=tail.GetComponent<Collider>();
            if(tailCollider!=null)Object.Destroy(tailCollider);
            Renderer tailRenderer=tail.GetComponent<Renderer>();
            if(tailRenderer!=null&&meteorMaterial!=null)tailRenderer.sharedMaterial=meteorMaterial;

            meteor.AddComponent<PyraxisVolcanicMeteor>().Launch(velocity);
        }
    }

    public sealed class PyraxisVolcanicMeteor : MonoBehaviour
    {
        Vector3 velocity;
        float born;

        public void Launch(Vector3 initialVelocity)
        {
            velocity=initialVelocity;
            born=Time.time;
        }

        void Update()
        {
            float dt=Time.deltaTime;
            velocity+=Vector3.down*(5.6f*dt);
            transform.position+=velocity*dt;
            transform.Rotate(210f*dt,145f*dt,90f*dt,Space.Self);
            float age=Time.time-born;
            float pulse=1f+Mathf.Sin(age*15f)*.08f;
            transform.localScale=new Vector3(.34f,.34f,.48f)*pulse;
            if(age>2.55f)Object.Destroy(gameObject);
        }
    }

    /// <summary>
    /// Connected Pyraxis scorpion visual. The gameplay object remains HotZoneTank; this class only
    /// supplies an articulated model and movement sampled from the real tank transform.
    /// </summary>
    public sealed class PyraxisScorpionPolish : MonoBehaviour
    {
        readonly List<Material> ownedMaterials=new();
        readonly Transform[] hips=new Transform[8];
        readonly Transform[] knees=new Transform[8];
        readonly Quaternion[] hipBase=new Quaternion[8];
        readonly Quaternion[] kneeBase=new Quaternion[8];
        readonly List<Transform> tailJoints=new();
        Transform visualRoot,tailRoot,leftClaw,rightClaw;
        public Transform Muzzle { get; private set; }
        Vector3 rootBase,lastWorldPosition;
        float moveBlend,gaitPhase;

        void Awake()
        {
            HideLegacyVisuals();
            Material shell=CreateMaterial("DF_PyraxisScorpionPolishedShell",new Color(.030f,.012f,.008f,1f),.34f,.27f);
            Material plate=CreateMaterial("DF_PyraxisScorpionPolishedPlate",new Color(.13f,.026f,.008f,1f),.28f,.31f);
            Material joint=CreateMaterial("DF_PyraxisScorpionPolishedJoint",new Color(.075f,.016f,.007f,1f),.20f,.23f);
            Material glow=CreateMaterial("DF_PyraxisScorpionPolishedGlow",new Color(1f,.105f,.005f,1f),0f,.62f,true,5.0f);

            visualRoot=new GameObject("DF_PyraxisScorpionPolishedVisual").transform;
            visualRoot.SetParent(transform,false);
            visualRoot.localPosition=new Vector3(0,.02f,0);
            rootBase=visualRoot.localPosition;
            lastWorldPosition=transform.position;

            BuildBody(shell,plate,glow);
            BuildLegs(shell,plate,joint,glow);
            leftClaw=BuildClaw(-1f,shell,plate,joint,glow);
            rightClaw=BuildClaw(1f,shell,plate,joint,glow);
            BuildTail(shell,plate,joint,glow);
            ResourceCreatureMesh.Apply(visualRoot, "MechanicalScorpion", new[] {
                new Color(.021f,.033f,.039f), new Color(.052f,.112f,.14f),
                new Color(.38f,.45f,.45f), new Color(.76f,.021f,.024f), new Color(.22f,.15f,.074f)
            });
        }

        void BuildBody(Material shell,Material plate,Material glow)
        {
            Part(PrimitiveType.Sphere,"Abdomen",visualRoot,new Vector3(0,.72f,-.30f),new Vector3(1.42f,.58f,1.62f),Quaternion.identity,shell);
            Part(PrimitiveType.Sphere,"Cephalothorax",visualRoot,new Vector3(0,.73f,.95f),new Vector3(1.25f,.54f,1.12f),Quaternion.identity,plate);
            Part(PrimitiveType.Sphere,"Head",visualRoot,new Vector3(0,.72f,1.72f),new Vector3(.88f,.43f,.70f),Quaternion.identity,shell);

            for(int i=0;i<5;i++)
            {
                float z=.28f-i*.42f;
                Part(PrimitiveType.Cube,"BackPlate",visualRoot,new Vector3(0,1.05f,z),new Vector3(1.00f-i*.055f,.08f,.31f),Quaternion.Euler(0,0,0),i%2==0?plate:shell);
            }
            Part(PrimitiveType.Sphere,"EyeL",visualRoot,new Vector3(-.27f,.86f,2.06f),new Vector3(.115f,.115f,.105f),Quaternion.identity,glow);
            Part(PrimitiveType.Sphere,"EyeR",visualRoot,new Vector3(.27f,.86f,2.06f),new Vector3(.115f,.115f,.105f),Quaternion.identity,glow);
        }

        void BuildLegs(Material shell,Material plate,Material joint,Material glow)
        {
            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                for(int leg=0;leg<4;leg++)
                {
                    int index=sideIndex*4+leg;
                    float z=1.28f-leg*.72f;
                    float spread=27f-leg*18f;

                    Transform hip=new GameObject($"LegHip_{sideIndex}_{leg}").transform;
                    hip.SetParent(visualRoot,false);
                    hip.localPosition=new Vector3(side*.84f,.68f,z);
                    hip.localRotation=Quaternion.Euler(0,side*spread,side*3f);
                    hips[index]=hip;
                    hipBase[index]=hip.localRotation;

                    // Coxa visibly bridges the shell to the first long segment.
                    Part(PrimitiveType.Sphere,"Coxa",hip,new Vector3(side*.08f,0,0),new Vector3(.25f,.20f,.24f),Quaternion.identity,joint);
                    Part(PrimitiveType.Cube,"UpperLeg",hip,new Vector3(side*.51f,0,0),new Vector3(1.02f,.15f,.17f),Quaternion.identity,plate);
                    Part(PrimitiveType.Sphere,"KneeJoint",hip,new Vector3(side*1.02f,0,0),new Vector3(.22f,.19f,.21f),Quaternion.identity,glow);

                    Transform knee=new GameObject($"LegKnee_{sideIndex}_{leg}").transform;
                    knee.SetParent(hip,false);
                    knee.localPosition=new Vector3(side*1.02f,0,0);
                    knee.localRotation=Quaternion.Euler(0,0,-side*(26f+leg*1.8f));
                    knees[index]=knee;
                    kneeBase[index]=knee.localRotation;

                    Part(PrimitiveType.Cube,"LowerLeg",knee,new Vector3(side*.48f,0,0),new Vector3(.96f,.125f,.145f),Quaternion.identity,shell);
                    Part(PrimitiveType.Sphere,"AnkleJoint",knee,new Vector3(side*.96f,0,0),new Vector3(.17f,.15f,.17f),Quaternion.identity,joint);

                    Transform ankle=new GameObject("Ankle").transform;
                    ankle.SetParent(knee,false);
                    ankle.localPosition=new Vector3(side*.96f,0,0);
                    ankle.localRotation=Quaternion.Euler(0,0,side*24f);
                    Part(PrimitiveType.Cube,"Foot",ankle,new Vector3(side*.24f,0,.04f),new Vector3(.48f,.085f,.15f),Quaternion.Euler(0,side*(leg-1.5f)*3f,0),shell);
                }
            }
        }

        Transform BuildClaw(float side,Material shell,Material plate,Material joint,Material glow)
        {
            Transform shoulder=new GameObject(side<0?"LeftClawRoot":"RightClawRoot").transform;
            shoulder.SetParent(visualRoot,false);
            shoulder.localPosition=new Vector3(side*.62f,.73f,1.58f);
            shoulder.localRotation=Quaternion.Euler(0,side*14f,0);

            Part(PrimitiveType.Sphere,"ClawShoulder",shoulder,new Vector3(side*.08f,0,0),new Vector3(.31f,.25f,.29f),Quaternion.identity,joint);
            Part(PrimitiveType.Cube,"ClawUpperArm",shoulder,new Vector3(side*.48f,0,.20f),new Vector3(.90f,.20f,.22f),Quaternion.Euler(0,side*18f,0),plate);

            Transform elbow=new GameObject("ClawElbow").transform;
            elbow.SetParent(shoulder,false);
            elbow.localPosition=new Vector3(side*.88f,0,.43f);
            Part(PrimitiveType.Sphere,"ClawElbowJoint",elbow,Vector3.zero,new Vector3(.30f,.25f,.28f),Quaternion.identity,joint);
            Part(PrimitiveType.Cube,"ClawForearm",elbow,new Vector3(side*.40f,0,.32f),new Vector3(.76f,.19f,.22f),Quaternion.Euler(0,side*29f,0),plate);

            Transform palm=new GameObject("PincerPalmRoot").transform;
            palm.SetParent(elbow,false);
            palm.localPosition=new Vector3(side*.71f,.01f,.66f);
            Part(PrimitiveType.Sphere,"PincerPalm",palm,Vector3.zero,new Vector3(.63f,.31f,.58f),Quaternion.identity,shell);
            Part(PrimitiveType.Cube,"PincerUpper",palm,new Vector3(side*.40f,.13f,.34f),new Vector3(.72f,.14f,.21f),Quaternion.Euler(0,side*23f,side*12f),shell);
            Part(PrimitiveType.Cube,"PincerLower",palm,new Vector3(side*.40f,-.13f,.34f),new Vector3(.72f,.14f,.21f),Quaternion.Euler(0,side*23f,-side*12f),glow);
            return shoulder;
        }

        void BuildTail(Material shell,Material plate,Material joint,Material glow)
        {
            tailRoot=new GameObject("TailRoot").transform;
            tailRoot.SetParent(visualRoot,false);
            tailRoot.localPosition=new Vector3(0,.82f,-1.42f);
            Transform parent=tailRoot;
            for(int i=0;i<7;i++)
            {
                Transform pivot=new GameObject("TailJoint_"+i).transform;
                pivot.SetParent(parent,false);
                pivot.localPosition=i==0?Vector3.zero:new Vector3(0,.32f,-.22f);
                pivot.localRotation=Quaternion.Euler(7f,0,0);
                tailJoints.Add(pivot);
                float size=Mathf.Lerp(.48f,.25f,i/6f);
                Part(PrimitiveType.Sphere,"TailJointShell",pivot,new Vector3(0,.14f,-.09f),new Vector3(size,size*.72f,size*1.05f),Quaternion.identity,i%2==0?plate:shell);
                Part(PrimitiveType.Cube,"TailBridge",pivot,new Vector3(0,.28f,-.18f),new Vector3(size*.58f,.34f,size*.58f),Quaternion.Euler(28f,0,0),joint);
                parent=pivot;
            }
            Muzzle=new GameObject("DF_MechanicalScorpionMuzzle").transform;
            Muzzle.SetParent(parent,false);Muzzle.localPosition=new Vector3(0,.48f,.59f);
            Part(PrimitiveType.Capsule,"Stinger",parent,new Vector3(0,.47f,-.17f),new Vector3(.21f,.42f,.21f),Quaternion.Euler(24f,0,0),glow);
        }

        void Update()
        {
            if(visualRoot==null)return;
            HideLegacyVisuals();

            float dt=Mathf.Max(Time.deltaTime,.0001f);
            Vector3 current=transform.position;
            Vector3 delta=current-lastWorldPosition;
            delta.y=0;
            float speed=delta.magnitude/dt;
            lastWorldPosition=current;
            float targetMove=Mathf.Clamp01((speed-.035f)/.70f);
            moveBlend=Mathf.MoveTowards(moveBlend,targetMove,dt*4.8f);
            gaitPhase+=dt*Mathf.Lerp(1.4f,6.0f,moveBlend);

            float idle=1f-moveBlend;
            float bob=Mathf.Abs(Mathf.Sin(gaitPhase))*moveBlend*.070f+Mathf.Sin(Time.time*1.45f)*idle*.018f;
            visualRoot.localPosition=rootBase+Vector3.up*bob;
            visualRoot.localRotation=Quaternion.Euler(0,Mathf.Sin(gaitPhase*.5f)*moveBlend*2.4f,Mathf.Sin(gaitPhase)*moveBlend*1.4f);

            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                for(int leg=0;leg<4;leg++)
                {
                    int index=sideIndex*4+leg;
                    Transform hip=hips[index],knee=knees[index];
                    if(hip==null||knee==null)continue;
                    float phase=gaitPhase+(((leg+sideIndex)&1)==0?0f:Mathf.PI)+leg*.18f;
                    float wave=Mathf.Sin(phase);
                    float lift=Mathf.Max(0f,Mathf.Sin(phase+Mathf.PI*.40f));
                    float idleWave=Mathf.Sin(Time.time*1.05f+index*.47f)*idle;
                    hip.localRotation=hipBase[index]*Quaternion.Euler(
                        0,
                        wave*(17f*moveBlend)+idleWave*1.2f,
                        side*(lift*10f*moveBlend+idleWave*.8f));
                    knee.localRotation=kneeBase[index]*Quaternion.Euler(
                        0,0,-side*(lift*18f*moveBlend+Mathf.Max(0f,-wave)*5f*moveBlend));
                }
            }

            float clawPulse=Mathf.Sin(Time.time*2.25f)*3.0f;
            if(leftClaw!=null)leftClaw.localRotation=Quaternion.Euler(0,-14f-clawPulse,Mathf.Sin(gaitPhase)*moveBlend*2f);
            if(rightClaw!=null)rightClaw.localRotation=Quaternion.Euler(0,14f+clawPulse,-Mathf.Sin(gaitPhase)*moveBlend*2f);

            if(tailRoot!=null)tailRoot.localRotation=Quaternion.Euler(8f+Mathf.Sin(Time.time*1.55f)*4f,Mathf.Sin(Time.time*1.1f)*5f,0);
            for(int i=0;i<tailJoints.Count;i++)
            {
                Transform tail=tailJoints[i];
                if(tail==null)continue;
                tail.localRotation=Quaternion.Euler(7f+Mathf.Sin(Time.time*1.7f+i*.38f)*3.0f,Mathf.Sin(Time.time*1.25f+i*.52f)*2.4f,0);
            }
        }

        void HideLegacyVisuals()
        {
            Transform legacy=transform.Find("DF_PyraxisScorpionVisual");
            if(legacy!=null&&legacy.gameObject.activeSelf)legacy.gameObject.SetActive(false);
        }

        Material CreateMaterial(string name,Color color,float metallic,float smoothness,bool emission=false,float power=1f)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material material=new(shader){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(emission)
            {
                material.EnableKeyword("_EMISSION");
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*power);
            }
            ownedMaterials.Add(material);
            return material;
        }

        static GameObject Part(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Quaternion localRotation,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            go.transform.localRotation=localRotation;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null)Object.Destroy(collider);
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null&&material!=null)renderer.sharedMaterial=material;
            return go;
        }

        void OnDestroy()
        {
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)Object.Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }
    }
}

