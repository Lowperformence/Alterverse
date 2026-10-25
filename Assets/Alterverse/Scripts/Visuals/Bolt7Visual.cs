using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
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
            if(health!=null)Bolt7Vfx.Warm();Play(health!=null?"Spawn":"Idle");
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
        private void OnDeath(){dead=true;Play("Death");}
        private void OnHealth(float current,float max){if(health.IsDead&&!dead){OnDeath();return;}if(dead&&current>0){dead=false;Play("Spawn");}}
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
            float now=health==null?Time.unscaledTime:Time.time;float dt=Mathf.Max(.001f,health==null?Time.unscaledDeltaTime:Time.deltaTime);Vector3 delta=transform.position-previous;delta.y=0;previous=transform.position;
            string name;float time;
            if(dead){name="Death";time=Mathf.Min(now-actionStarted,clips.TryGetValue("Death",out var d)?d.length:0);}
            else if(action!=null&&now<actionUntil){name=action;time=now-actionStarted;}
            else {name=Time.time<fireUntil?"Fire_Loop":delta.sqrMagnitude/(dt*dt)>.15f?"Walk":"Idle";walkTime+=dt;time=walkTime;}
            if(clips.TryGetValue(name,out var clip)&&clip.length>0)rig.Sample(clip,dead?time:time%clip.length);
        }
    }
}
