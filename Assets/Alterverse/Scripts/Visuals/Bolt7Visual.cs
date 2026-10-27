using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Samples only BOLT-7's authored clips. The existing motor remains the movement authority.
    [DefaultExecutionOrder(1100)]
    public sealed class Bolt7Visual:MonoBehaviour
    {
        private Bolt7Model rig;
        private static Texture2D portrait;public static Texture2D Portrait=>portrait!=null?portrait:(portrait=Bolt7Assets.Texture("Portrait"));
        private static readonly Dictionary<string,Material> materials=new();
        private readonly Dictionary<string,Bolt7Model.Clip> clips=new();
        private GameObject model;
        private Transform gatling,launcher;
        private Health health;
        private Vector3 previous;
        private Transform visualRoot;
        private Vector3 basePosition;
        private CoopHeroSlot replicaSlot;
        private readonly HeroPresentationBuffer presentation=new();
        private float displayedSpeed;
        private bool RemoteReplica=>replicaSlot!=null&&CoopAuthority.Active&&
            !CoopAuthority.LocalHasServerAuthority&&!replicaSlot.IsLocalHuman;
        public bool HasNetworkPresentation=>isActiveAndEnabled&&visualRoot!=null&&RemoteReplica&&presentation.Ready;
        public Vector3 PresentedPosition {get;private set;}
        public Quaternion PresentedRotation {get;private set;}=Quaternion.identity;
        public void ReceivePresentationSnapshot(Vector3 position,Quaternion rotation,bool discontinuity,bool forcedMoving,float sampledAt=float.NaN)
        {
            replicaSlot??=GetComponent<CoopHeroSlot>();
            if(!RemoteReplica)return;
            bool reset=discontinuity||!presentation.Ready;
            if(float.IsNaN(sampledAt))presentation.Push(position,rotation,Time.unscaledTime,reset);
            else presentation.PushRemote(position,rotation,sampledAt,Time.unscaledTime,reset);
            if(reset){PresentedPosition=previous=position;PresentedRotation=rotation;displayedSpeed=walkTime=0;}
        }
        private string action;
        private float actionStarted,actionUntil,fireUntil,walkTime;
        private bool dead;
        public Vector3 Gatling=>gatling!=null?gatling.position:transform.position+transform.forward*.45f;
        public Vector3 Launcher=>launcher!=null?launcher.position:transform.position+transform.forward*.45f;
        public static void Create(GameObject actor)
        {
            if(actor.GetComponent<Bolt7Visual>()!=null)return;
            actor.AddComponent<Bolt7Visual>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){portrait=null;materials.Clear();}
        private void Awake()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null){enabled=false;return;}
            var root=new GameObject("DF_VisualRoot").transform;root.SetParent(transform,false);root.gameObject.layer=gameObject.layer;
            root.localPosition=Vector3.down*.98f;root.localScale=Vector3.one*.85f;
            visualRoot=root;basePosition=root.localPosition;replicaSlot=GetComponent<CoopHeroSlot>();
            rig=Bolt7Model.Create(root);
            if(rig==null){enabled=false;return;}
            model=rig.Root;
            foreach(var clip in rig.Clips)clips[clip.name]=clip;
            if(clips.TryGetValue("Idle",out var idle))rig.Sample(idle,0f);
            foreach(var t in model.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="socket_gatling_muzzle")gatling=t;
                if(t.name=="socket_grenade_muzzle")launcher=t;
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var native=new Material[rig.MaterialNames.Length];
                for(int i=0;i<native.Length;i++)native[i]=MaterialFor(rig.MaterialNames[i]);
                renderer.sharedMaterials=native;
            }
            health=GetComponent<Health>();previous=transform.position;
            if(health!=null){health.Died+=OnDeath;health.Changed+=OnHealth;}
            if(health!=null)Bolt7Vfx.Warm();GroundedIdle();
            // BOLT-7 returns before StylizedHeroVisual's shared authored-hero path.
            // Fit once the real mesh/bind frame exists; the hat then follows its Head bone.
            HeadwearPresentation.Configure(gameObject,root,HeroId.Bolt7,GetComponent<Alterverse.Shop.SkinPreviewSelection>());
        }
        private static Material MaterialFor(string name)
        {
            if(materials.TryGetValue(name,out var m)&&m!=null)return m;
            bool eyes=name.Contains("Eyes");
            m=RuntimeShaderLibrary.Create(Color.white,false,(name.Contains("Steel")||name.Contains("Graphite")) ? .35f : .08f,.42f,
                eyes?new Color(1,.66f,.12f)*1.5f:Color.black);
            if(m==null)return null;m.name="BOLT7_"+name;
            var texture=Bolt7Assets.Texture("BOLT7_BaseColor_2K");
            if(texture!=null){if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",texture);if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",texture);}
            var normal=Bolt7Assets.Texture("BOLT7_Normal_2K",true);
            if(normal!=null&&m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap",normal);m.EnableKeyword("_NORMALMAP");
                if(m.HasProperty("_NormalEncoding"))m.SetFloat("_NormalEncoding",0f);
                if(m.HasProperty("_BumpScale"))m.SetFloat("_BumpScale",.6f);
            }
            if(eyes)m.EnableKeyword("_EMISSION");materials[name]=m;return m;
        }
        private void GroundedIdle()
        {
            // The authored Spawn clip deliberately drops the root from above. Match entry
            // and respawn use the grounded pose immediately, including before the next render.
            action=null;actionUntil=fireUntil=walkTime=displayedSpeed=0;previous=transform.position;
            presentation.Clear();
            PresentedPosition=transform.position;PresentedRotation=transform.rotation;
            if(visualRoot!=null){visualRoot.localPosition=basePosition;visualRoot.localRotation=Quaternion.identity;}
            if(clips.TryGetValue("Idle",out var idle))rig.Sample(idle,0f);
            // Idle is the fallback pose, not an action that locks out the first walk cycle.
        }
        private void OnDeath(){dead=true;Play("Death");}
        private void OnHealth(float current,float max){if(health.IsDead&&!dead){OnDeath();return;}if(dead&&current>0){dead=false;GroundedIdle();}}
        private void OnDestroy(){if(health!=null){health.Died-=OnDeath;health.Changed-=OnHealth;}}
        public void Shot(){fireUntil=Time.time+.28f;}
        public void Skill(int slot)=>Play(slot==0?"Grenade":slot==1?"Heavy_Ram":slot==2?"Grenade":"Ultimate");
        private void Play(string name)
        {
            if(!clips.TryGetValue(name,out var clip))return;
            float now=health==null?Time.unscaledTime:Time.time;action=name;actionStarted=now;actionUntil=now+clip.length;
        }
        private void LateUpdate()
        {
            if(model==null)return;
            float now=health==null?Time.unscaledTime:Time.time;float dt=Mathf.Max(.001f,health==null?Time.unscaledDeltaTime:Time.deltaTime);
            Vector3 presented=transform.position;Quaternion rotation=transform.rotation;
            if(RemoteReplica&&presentation.Ready)presentation.Evaluate(Time.unscaledTime,out presented,out rotation);
            PresentedPosition=presented;PresentedRotation=rotation;
            visualRoot.localPosition=basePosition+transform.InverseTransformVector(presented-transform.position);
            visualRoot.rotation=rotation;
            Vector3 delta=presented-previous;delta.y=0;previous=presented;
            float speed=delta.magnitude/dt;
            // Snapshot gaps must not alternate Walk and Idle every render frame.
            displayedSpeed=RemoteReplica?Mathf.Lerp(displayedSpeed,speed,1f-Mathf.Exp(-12f*dt)):speed;
            string name;float time;
            if(dead){name="Death";time=Mathf.Min(now-actionStarted,clips.TryGetValue("Death",out var d)?d.length:0);}
            else if(action!=null&&now<actionUntil){name=action;time=now-actionStarted;}
            else
            {
                bool moving=displayedSpeed*displayedSpeed>.15f;
                bool firing=Time.time<fireUntil;
                walkTime+=dt;time=walkTime;
                if(moving&&firing&&clips.TryGetValue("Walk",out var walk)&&walk.length>0&&
                    clips.TryGetValue("Fire_Loop",out var fire)&&fire.length>0)
                {
                    rig.Sample(walk,time%walk.length);
                    rig.Sample(fire,time%fire.length,true);
                    return;
                }
                name=firing?"Fire_Loop":moving?"Walk":"Idle";
            }
            if(clips.TryGetValue(name,out var clip)&&clip.length>0)rig.Sample(clip,dead?time:time%clip.length);
        }
    }
}
