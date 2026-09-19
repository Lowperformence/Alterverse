using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    // Hero-only death drop. Team-neutral pickup: any living hero may claim it once.
    // Rewards are deliberately small so the drop creates a short skirmish without deciding the match by itself.
    public sealed class FractureCorePickup : MonoBehaviour
    {
        public const float HealFraction=.05f;
        public const float ShieldFraction=.20f;
        public const float ShieldDuration=2f;
        public const float FrontInfluence=.20f;
        public const float PickupDelay=.40f;
        public const float Lifetime=8f;
        const float PickupRadius=1.35f;
        const float ScanInterval=.07f;
        const float CollectTravel=.14f;

        static Material coreMaterial,zeroMaterial,lumenMaterial,metalMaterial;
        static Mesh ringMesh;

        Transform visual,innerCore,ringA,ringB,zeroShard,lumenShard;
        DimensionsFront front;
        Vector3 groundPosition;
        float spawnedAt,nextScan,collectStarted;
        bool claimed;
        Transform collector;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSharedVisuals()
        {
            coreMaterial=null;zeroMaterial=null;lumenMaterial=null;metalMaterial=null;ringMesh=null;
        }

        public static void Spawn(Vector3 deathPosition)
        {
            DimensionsFront front=FindAnyObjectByType<DimensionsFront>();
            if(front!=null&&front.HasEnded)return;

            GameObject go=new("DF_FractureCore");
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)go.transform.SetParent(world.transform,true);

            // Heroes in the prototype stand about one metre above the floor.
            // Keep the drop close to the regolith while preserving the visible launch from the body.
            Vector3 ground=deathPosition;
            ground.y=Mathf.Max(.16f,deathPosition.y-.88f);
            go.transform.position=ground;
            FractureCorePickup pickup=go.AddComponent<FractureCorePickup>();
            pickup.groundPosition=ground;
            pickup.BuildVisual();
        }

        void BuildVisual()
        {
            EnsureSharedVisuals();
            spawnedAt=Time.time;
            nextScan=spawnedAt+PickupDelay;
            front=FindAnyObjectByType<DimensionsFront>();

            visual=new GameObject("DF_FractureCoreVisual").transform;
            visual.SetParent(transform,false);

            innerCore=Primitive("DF_FractureCoreInner",PrimitiveType.Sphere,visual,Vector3.zero,new Vector3(.38f,.38f,.38f),coreMaterial);
            Transform shell=Primitive("DF_FractureCoreShell",PrimitiveType.Sphere,visual,Vector3.zero,new Vector3(.60f,.60f,.60f),metalMaterial);
            Renderer shellRenderer=shell.GetComponent<Renderer>();
            if(shellRenderer!=null)shadowOff(shellRenderer);

            ringA=MeshPart("DF_FractureRingZero",visual,ringMesh,zeroMaterial,Vector3.zero,new Vector3(.86f,.86f,.86f));
            ringA.localRotation=Quaternion.Euler(18,0,12);
            ringB=MeshPart("DF_FractureRingLumen",visual,ringMesh,lumenMaterial,Vector3.zero,new Vector3(.70f,.70f,.70f));
            ringB.localRotation=Quaternion.Euler(72,15,0);

            zeroShard=Primitive("DF_FractureShardZero",PrimitiveType.Cube,visual,new Vector3(.48f,.05f,0),new Vector3(.10f,.30f,.10f),zeroMaterial);
            zeroShard.localRotation=Quaternion.Euler(18,35,28);
            lumenShard=Primitive("DF_FractureShardLumen",PrimitiveType.Cube,visual,new Vector3(-.48f,-.03f,0),new Vector3(.10f,.30f,.10f),lumenMaterial);
            lumenShard.localRotation=Quaternion.Euler(-16,-28,22);
        }

        static void EnsureSharedVisuals()
        {
            if(coreMaterial!=null)return;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            coreMaterial=Material(shader,new Color(.78f,.94f,1f,1f),3.4f);
            zeroMaterial=Material(shader,Color.Lerp(StylizedArtDirection.ZeroEnergy,Color.white,.12f),2.0f);
            lumenMaterial=Material(shader,Color.Lerp(StylizedArtDirection.LumenEnergy,Color.white,.10f),2.0f);
            metalMaterial=Material(shader,new Color(.20f,.23f,.30f,1f),.25f);
            ringMesh=ProductionMeshFactory.Ring(.52f,.035f,18,6);
            ringMesh.name="DF_SharedFractureCoreRing";
        }

        static Material Material(Shader shader,Color color,float emission)
        {
            Material material=new(shader){color=color};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.15f);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.48f);
            if(emission>0&&material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",color*emission);
            }
            return material;
        }

        static Transform Primitive(string name,PrimitiveType type,Transform parent,Vector3 localPosition,Vector3 scale,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;go.transform.localScale=scale;
            Collider collider=go.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
            Renderer renderer=go.GetComponent<Renderer>();if(renderer!=null){renderer.sharedMaterial=material;shadowOff(renderer);}
            return go.transform;
        }

        static Transform MeshPart(string name,Transform parent,Mesh mesh,Material material,Vector3 localPosition,Vector3 scale)
        {
            GameObject go=ProductionMeshFactory.MeshObject(name,parent,mesh,material);
            go.transform.localPosition=localPosition;go.transform.localScale=scale;
            Renderer renderer=go.GetComponent<Renderer>();if(renderer!=null)shadowOff(renderer);
            return go.transform;
        }

        static void shadowOff(Renderer renderer)
        {
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        void Update()
        {
            if(Time.timeScale<=0f)return;
            float age=Time.time-spawnedAt;

            if(front!=null&&front.HasEnded){Destroy(gameObject);return;}

            if(claimed)
            {
                if(collector==null){Destroy(gameObject);return;}
                float t=Mathf.Clamp01((Time.time-collectStarted)/CollectTravel);
                Vector3 target=collector.position+Vector3.up*.75f;
                transform.position=Vector3.Lerp(transform.position,target,1-Mathf.Exp(-Time.deltaTime*28f));
                if(visual!=null)visual.localScale=Vector3.one*(1-t*.82f);
                if(t>=1f)Destroy(gameObject);
                return;
            }

            if(age>=Lifetime){Destroy(gameObject);return;}

            // Launch, settle, then hover. No Rigidbody is needed and no gameplay physics are changed.
            float launch=Mathf.Clamp01(age/.22f);
            float settle=Mathf.Clamp01((age-.22f)/.38f);
            float height=age<.22f?Mathf.Lerp(.42f,1.22f,1-Mathf.Pow(1-launch,2)):
                Mathf.Lerp(1.22f,.72f,settle);
            if(age>.60f)height=.72f+Mathf.Sin((age-.6f)*3.2f)*.07f;
            transform.position=groundPosition+Vector3.up*height;

            float pulse=1f+Mathf.Sin(age*4.4f)*.075f;
            if(visual!=null){visual.localScale=Vector3.one*pulse;visual.Rotate(0,Time.deltaTime*38f,0,Space.Self);}
            if(innerCore!=null)innerCore.Rotate(Time.deltaTime*35f,Time.deltaTime*58f,0,Space.Self);
            if(ringA!=null)ringA.Rotate(0,Time.deltaTime*78f,Time.deltaTime*22f,Space.Self);
            if(ringB!=null)ringB.Rotate(Time.deltaTime*-31f,Time.deltaTime*-66f,0,Space.Self);
            if(zeroShard!=null)zeroShard.Rotate(0,Time.deltaTime*105f,Time.deltaTime*28f,Space.Self);
            if(lumenShard!=null)lumenShard.Rotate(0,Time.deltaTime*-92f,Time.deltaTime*-24f,Space.Self);

            if(age<PickupDelay||Time.time<nextScan)return;
            nextScan=Time.time+ScanInterval;
            TryClaim();
        }

        void TryClaim()
        {
            float radiusSqr=PickupRadius*PickupRadius;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health health=Health.Active[i];
                if(health==null||health.IsDead||!health.isActiveAndEnabled)continue;
                HeroIdentity identity=health.GetComponent<HeroIdentity>();
                TeamMember teamMember=health.GetComponent<TeamMember>();
                if(identity==null||teamMember==null)continue;

                Vector3 delta=health.transform.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude>radiusSqr)continue;
                Claim(health,teamMember.Team);
                return;
            }
        }

        void Claim(Health health,int team)
        {
            if(claimed||health==null||health.IsDead)return;
            claimed=true;
            collector=health.transform;
            collectStarted=Time.time;

            health.Heal(health.Max*HealFraction,false);
            health.AddShield(health.Max*ShieldFraction,ShieldDuration,false);
            HeroSkillVfx.Support(health.gameObject,health,true,ShieldDuration);

            if(front==null)front=FindAnyObjectByType<DimensionsFront>();
            if(front!=null&&!front.HasEnded)front.Push(team,FrontInfluence);

            // Clean, visual-only absorption cue; intentionally no repeated beep or looping audio.
            PrototypeVfx.Ring(health.transform.position,Color.Lerp(StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,.5f),1.25f,.24f);
            PrototypeVfx.Burst(health.transform.position,Color.Lerp(StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,.5f),.65f,5);
        }
    }
}
