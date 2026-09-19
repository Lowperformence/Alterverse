using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    internal enum HeroFxShape { Ring,Hex,Slash,Rift,Orb,Bolt,Shield,Plus,Shard,Cracks }
    internal enum HeroFxMotion { Hold,Expand,Contract,Travel,Orbit,Sweep }
    // Presentation-only scheduler. No damage, collision, targeting, coroutines, or runtime mesh rebuilds.
    public sealed class HeroVfxPool:MonoBehaviour
    {
        private const int Capacity=192,WarningReserve=32;
        private sealed class Slot
        {
            public Transform Transform;public MeshRenderer Renderer;public MeshFilter Filter;
            public readonly MaterialPropertyBlock Block=new();
            public Vector3 From,To,Size,Offset;public Quaternion Rotation;
            public Color Color;public float Start,Duration;public bool Active,Warning;
            public HeroFxMotion Motion;public Transform Follow;public bool Follows;
            public GameObject Owner;public Health OwnerHealth,Shield;public bool ShieldBound;
        }
        private static HeroVfxPool instance;
        private readonly Slot[] slots=new Slot[Capacity];
        private readonly Mesh[] shapes=new Mesh[10];
        private Material material;
        private bool ownsMaterial;
        private int cursor;
        private static readonly int Tint=Shader.PropertyToID("_Tint"),BaseColor=Shader.PropertyToID("_BaseColor");
        public static void Warm()
        {if(instance==null)new GameObject("DF_HeroVfxPool").AddComponent<HeroVfxPool>();}
        private void Awake()
        {
            instance=this;material=Resources.Load<Material>("CombatVfx/HeroEnergy");
            if(material==null)
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
                if(shader==null){enabled=false;return;}
                material=new Material(shader){name="DF_VfxFallback"};ownsMaterial=true;
                Debug.LogWarning("DF: HeroEnergy material missing; reimport CombatVfx assets.");
            }
            for(int i=0;i<shapes.Length;i++)shapes[i]=BuildShape((HeroFxShape)i);
            for(int i=0;i<Capacity;i++)
            {
                var go=new GameObject("Hero effect");go.transform.SetParent(transform,false);
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
                slots[i]=new Slot{Transform=go.transform,Renderer=renderer,Filter=go.AddComponent<MeshFilter>()};
            }
        }
        internal static void Emit(HeroFxShape shape,Vector3 from,Vector3 to,Vector3 size,Color color,float duration,
            HeroFxMotion motion=HeroFxMotion.Expand,Quaternion rotation=default,GameObject owner=null,
            bool warning=false,Transform follow=null,Health shield=null,float delay=0)
        {
            Warm();if(instance==null||instance.material==null)return;
            instance.Show(shape,from,to,size,color,duration,motion,rotation,owner,warning,follow,shield,delay);
        }
        private void Show(HeroFxShape shape,Vector3 from,Vector3 to,Vector3 size,Color color,float duration,
            HeroFxMotion motion,Quaternion rotation,GameObject owner,bool warning,Transform follow,Health shield,float delay)
        {
            int begin=warning?Capacity-WarningReserve:0,end=warning?Capacity:Capacity-WarningReserve;
            Slot slot=null;
            for(int n=0;n<end-begin;n++)
            {
                int i=begin+(cursor+n)%(end-begin);
                if(!slots[i].Active){slot=slots[i];cursor=(i-begin+1)%(end-begin);break;}
            }
            // Never steal a visible warning; optional flourishes are dropped at saturation.
            if(slot==null)return;
            slot.Active=true;slot.Warning=warning;slot.Owner=owner;slot.OwnerHealth=owner!=null?owner.GetComponent<Health>():null;
            slot.Follow=follow;slot.Follows=follow!=null;slot.Offset=slot.Follows?from-follow.position:Vector3.zero;
            slot.Shield=shield;slot.ShieldBound=shield!=null;
            slot.From=from;slot.To=to;slot.Size=size;slot.Color=color;slot.Motion=motion;
            slot.Rotation=rotation==default?Quaternion.identity:rotation;
            slot.Start=Time.time+Mathf.Max(0,delay);slot.Duration=Mathf.Max(.04f,duration);
            slot.Filter.sharedMesh=shapes[(int)shape];slot.Renderer.enabled=false;
            Draw(slot,0);if(delay>0)slot.Renderer.enabled=false;
        }
        public static void CancelWarnings(GameObject actor)
        {
            if(instance==null)return;
            foreach(Slot slot in instance.slots)if(slot!=null&&slot.Active&&slot.Warning&&slot.Owner==actor)instance.Hide(slot);
        }
        private void Hide(Slot s){s.Active=false;s.Renderer.enabled=false;s.Follow=null;s.Owner=null;s.OwnerHealth=null;s.Shield=null;}
        private void Update()
        {
            if(Time.timeScale<=0)return;
            for(int i=0;i<slots.Length;i++)
            {
                Slot s=slots[i];if(s==null||!s.Active)continue;
                if(s.Warning&&(s.Owner==null||!s.Owner.activeInHierarchy||s.OwnerHealth!=null&&s.OwnerHealth.IsDead)||
                    s.Follows&&s.Follow==null||s.ShieldBound&&(s.Shield==null||s.Shield.IsDead||s.Shield.Shield<=0))
                {Hide(s);continue;}
                float age=Time.time-s.Start;if(age<0)continue;
                if(age>=s.Duration){Hide(s);continue;}Draw(s,age/s.Duration);
            }
        }
        private void Draw(Slot s,float t)
        {
            Vector3 point=s.Follows?s.Follow.position+s.Offset:s.From;
            float scale=1;Quaternion rotation=s.Rotation;
            switch(s.Motion)
            {
                case HeroFxMotion.Expand:scale=Mathf.Lerp(.32f,1,t);break;
                case HeroFxMotion.Contract:scale=Mathf.Lerp(1,.12f,t*t);break;
                case HeroFxMotion.Travel:point=Vector3.Lerp(s.From,s.To,t);break;
                case HeroFxMotion.Orbit:rotation*=Quaternion.Euler(0,t*180,0);scale=1-t*.22f;break;
                case HeroFxMotion.Sweep:rotation*=Quaternion.Euler(0,Mathf.Lerp(-35,35,t),0);scale=.88f+.12f*Mathf.Sin(t*Mathf.PI);break;
            }
            s.Transform.SetPositionAndRotation(point,rotation);s.Transform.localScale=s.Size*scale;
            float fade=s.Warning?1:Mathf.Clamp01((1-t)*4);
            Color color=s.Color;color.a*=fade;
            s.Block.SetColor(Tint,color);s.Block.SetColor(BaseColor,color);s.Renderer.SetPropertyBlock(s.Block);s.Renderer.enabled=true;
        }
        private void OnDestroy()
        {
            if(instance==this)instance=null;
            foreach(Mesh m in shapes)if(m!=null)Destroy(m);
            if(ownsMaterial&&material!=null)Destroy(material);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){instance=null;}

        private static Mesh BuildShape(HeroFxShape shape)
        {
            var v=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,float alpha=1)
            {int i=v.Count;v.Add(a);v.Add(b);v.Add(c);colors.Add(new Color(1,1,1,alpha));colors.Add(new Color(1,1,1,alpha));colors.Add(new Color(1,1,1,alpha));triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float alpha=1){Tri(a,b,c,alpha);Tri(a,c,d,alpha);}
            void Band(float start,float end,float inner,float outer,int count,float y=0)
            {
                for(int i=0;i<count;i++)
                {
                    float a=Mathf.Lerp(start,end,(float)i/count),b=Mathf.Lerp(start,end,(float)(i+1)/count);
                    Vector3 pa=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),pb=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
                    float taper=shape==HeroFxShape.Slash?Mathf.Sin((i+.5f)/count*Mathf.PI):1;
                    Quad(pa*inner+Vector3.up*y,pa*Mathf.Lerp(inner,outer,taper)+Vector3.up*y,pb*Mathf.Lerp(inner,outer,taper)+Vector3.up*y,pb*inner+Vector3.up*y);
                }
            }
            switch(shape)
            {
                case HeroFxShape.Ring:Band(0,Mathf.PI*2,.92f,1,48);break;
                case HeroFxShape.Hex:Band(0,Mathf.PI*2,.89f,1,6);break;
                case HeroFxShape.Slash:Band(-1.35f,1.35f,.62f,1,24);break;
                case HeroFxShape.Rift:
                    for(int i=0;i<5;i++)
                    {
                        float y=-.8f+i*.32f,x=i%2==0?-.06f:.07f;
                        Quad(new Vector3(x-.08f,y,0),new Vector3(x+.055f,y,0),new Vector3(-x+.09f,y+.33f,0),new Vector3(-x-.05f,y+.33f,0));
                    }break;
                case HeroFxShape.Bolt:
                    Tri(new Vector3(-.15f,0,-.35f),new Vector3(0,.10f,1),new Vector3(.15f,0,-.35f));
                    Tri(new Vector3(0,-.1f,-.35f),new Vector3(.15f,0,-.35f),new Vector3(0,.1f,1));
                    Tri(new Vector3(-.15f,0,-.35f),new Vector3(0,-.1f,-.35f),new Vector3(0,.1f,1));break;
                case HeroFxShape.Orb:
                case HeroFxShape.Shard:
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                        Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.5f,q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*.5f;
                        Tri(p,Vector3.up*(shape==HeroFxShape.Shard?1:.6f),q);Tri(q,Vector3.down*.6f,p);
                    }break;
                case HeroFxShape.Shield:
                    for(int i=0;i<6;i++)
                    {
                        float a=i*Mathf.PI/3+.08f,b=(i+1)*Mathf.PI/3-.08f;
                        Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        Quad(p*.84f,q*.84f,q*.72f+Vector3.up*1.2f,p*.72f+Vector3.up*1.2f,.16f);
                        Quad(p*.84f,p*.88f,p*.76f+Vector3.up*1.2f,p*.72f+Vector3.up*1.2f,.9f);
                    }
                    Band(0,Mathf.PI*2,.79f,.84f,6);Band(0,Mathf.PI*2,.67f,.72f,6,1.2f);break;
                case HeroFxShape.Plus:
                    Quad(new Vector3(-.15f,0,-.5f),new Vector3(.15f,0,-.5f),new Vector3(.15f,0,.5f),new Vector3(-.15f,0,.5f));
                    Quad(new Vector3(-.5f,0,-.15f),new Vector3(.5f,0,-.15f),new Vector3(.5f,0,.15f),new Vector3(-.5f,0,.15f));break;
                case HeroFxShape.Cracks:
                    for(int i=0;i<7;i++)
                    {
                        float a=i*Mathf.PI*2/7;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),s=Vector3.Cross(Vector3.up,d);
                        Tri(d*.16f-s*.04f,d*.85f+s*.13f,d*.35f+s*.035f);
                        Tri(d*.35f+s*.035f,d*.85f+s*.13f,d*1.1f+s*.21f);
                    }break;
            }
            Mesh mesh=new Mesh{name="DF_AuthoredFx_"+shape};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
