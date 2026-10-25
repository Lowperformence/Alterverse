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
        private static GameObject prefab;
        private static AnimationClip[] bank;
        private static Texture2D portrait;public static Texture2D Portrait=>portrait!=null?portrait:(portrait=Resources.Load<Texture2D>("Bolt7/Portrait"));
        private static readonly Dictionary<string,Material> materials=new();
        private readonly Dictionary<string,AnimationClip> clips=new();
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
        private static void ResetStatics(){prefab=null;bank=null;portrait=null;materials.Clear();}
        private void Awake()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null){enabled=false;return;}
            prefab??=Resources.Load<GameObject>("Bolt7/BOLT7_animated");
            if(prefab==null){Debug.LogError("DF BOLT-7: model missing at Resources/Bolt7/BOLT7_animated.fbx.",this);enabled=false;return;}
            var root=new GameObject("DF_Bolt7VisualRoot").transform;root.SetParent(transform,false);
            root.localPosition=Vector3.down*.98f;root.localScale=Vector3.one*.85f;
            model=Instantiate(prefab,root,false);model.name="BOLT7_AuthoredModel";
            foreach(Animator animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            bank??=Resources.LoadAll<AnimationClip>("Bolt7/BOLT7_animated");
            foreach(var clip in bank)
            {
                int start=clip.name.LastIndexOf("BOLT7_",System.StringComparison.Ordinal);
                if(start>=0)clips[clip.name.Substring(start+6)]=clip;
            }
            foreach(var t in model.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="socket_gatling_muzzle")gatling=t;
                if(t.name=="socket_grenade_muzzle")launcher=t;
            }
            if(gatling!=null&&launcher!=null)
            {
                Vector3 forward=root.InverseTransformDirection((gatling.position+launcher.position)*.5f-root.position);
                forward.y=0;if(forward.sqrMagnitude>.001f)model.transform.localRotation=Quaternion.FromToRotation(forward.normalized,Vector3.forward);
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var native=renderer.sharedMaterials;
                for(int i=0;i<native.Length;i++)native[i]=MaterialFor(native[i]!=null?native[i].name:new[]{"Mechanism_Graphite","Accent_Gold","Eyes_Amber","Steel","Armor_Ivory","Visor"}[i%6]);
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
            var texture=Resources.Load<Texture2D>("Bolt7/BOLT7_BaseColor_2K");
            if(texture!=null){if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",texture);if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",texture);}
            var normal=Resources.Load<Texture2D>("Bolt7/BOLT7_Normal_2K");
            if(normal!=null&&m.HasProperty("_BumpMap")){m.SetTexture("_BumpMap",normal);m.EnableKeyword("_NORMALMAP");}
            if(eyes)m.EnableKeyword("_EMISSION");materials[name]=m;return m;
        }
        private void OnDeath(){dead=true;Play("Death");}
        private void OnHealth(float current,float max){if(health.IsDead&&!dead){OnDeath();return;}if(dead&&current>0){dead=false;Play("Spawn");}}
        private void OnDestroy(){if(health!=null){health.Died-=OnDeath;health.Changed-=OnHealth;}}
        public void Shot(){fireUntil=Time.time+.28f;}
        public void Skill(int slot)=>Play(slot==0?"Grenade":slot==1?"Heavy_Ram":"Ultimate");
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
            if(clips.TryGetValue(name,out var clip)&&clip.length>0)clip.SampleAnimation(model,dead?time:time%clip.length);
        }
    }
}
