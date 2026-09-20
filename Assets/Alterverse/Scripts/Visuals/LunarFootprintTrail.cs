using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only lunar footprints. No physics, movement, navigation or gameplay writes.
    /// Footprints remain solid for two seconds, then fade out softly and return to a small pool.
    /// </summary>
    public sealed class LunarFootprintTrail : MonoBehaviour
    {
        private const float StepDistance=.72f;
        private const float HoldSeconds=2f;
        private const float FadeSeconds=.85f;
        private const int PoolSize=44;

        private Health health;
        private Transform bootL,bootR;
        private Vector3 lastPosition;
        private float traveled;
        private bool leftNext;

        private static FootprintPool pool;

        private void Start()
        {
            if(ArenaVariantRuntime.Current!=ArenaVariant.Moon){enabled=false;return;}
            health=GetComponent<Health>();
            if(health==null){enabled=false;return;}

            Transform root=transform.Find("DF_VisualRoot");
            if(root==null){enabled=false;return;}
            bootL=root.Find("DF_BootL");
            bootR=root.Find("DF_BootR");
            if(bootL==null||bootR==null){enabled=false;return;}

            pool=FootprintPool.GetOrCreate();
            lastPosition=transform.position;
        }

        private void LateUpdate()
        {
            if(pool==null||health==null||health.IsDead||Time.timeScale<=0f)return;

            Vector3 now=transform.position;
            Vector3 delta=now-lastPosition;delta.y=0f;
            lastPosition=now;

            float distance=delta.magnitude;
            if(distance>.001f&&distance<3f)traveled+=distance;
            else if(distance>=3f){traveled=0f;return;} // respawn/recall relocation

            while(traveled>=StepDistance)
            {
                traveled-=StepDistance;
                Spawn(leftNext?bootL:bootR);
                leftNext=!leftNext;
            }
        }

        private void Spawn(Transform boot)
        {
            if(boot==null)return;
            Vector3 p=boot.position;
            LunarSurfaceField.Sample(p.x,p.z,out float relief,out _,out _);
            p.y=.015f+relief+.012f;

            Vector3 forward=transform.forward;forward.y=0f;
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            Quaternion rotation=Quaternion.LookRotation(forward.normalized,Vector3.up)*Quaternion.Euler(90f,0f,0f);
            pool.Emit(p,rotation);
        }

        private sealed class FootprintPool:MonoBehaviour
        {
            private sealed class Mark
            {
                public GameObject GameObject;
                public Renderer Renderer;
                public float Born;
                public bool Active;
            }

            private readonly List<Mark> marks=new(PoolSize);
            private Mesh footprintMesh;
            private Material footprintMaterial;
            private MaterialPropertyBlock block;
            private int cursor;
            private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
            private static readonly int ColorId=Shader.PropertyToID("_Color");

            public static FootprintPool GetOrCreate()
            {
                if(pool!=null)return pool;
                GameObject owner=GameObject.Find("DF_LunarFootprintPool");
                if(owner==null)
                {
                    owner=new GameObject("DF_LunarFootprintPool");
                    GameObject world=GameObject.Find("DF_MatchWorld");
                    if(world!=null)owner.transform.SetParent(world.transform,false);
                }
                pool=owner.GetComponent<FootprintPool>()??owner.AddComponent<FootprintPool>();
                return pool;
            }

            private void Awake()
            {
                footprintMesh=BuildFootprintMesh();
                footprintMaterial=BuildMaterial();
                block=new MaterialPropertyBlock();
                for(int i=0;i<PoolSize;i++)marks.Add(CreateMark(i));
            }

            public void Emit(Vector3 position,Quaternion rotation)
            {
                if(marks.Count==0)return;
                Mark mark=marks[cursor++%marks.Count];
                mark.GameObject.transform.SetPositionAndRotation(position,rotation);
                mark.GameObject.transform.localScale=Vector3.one;
                mark.Born=Time.unscaledTime;
                mark.Active=true;
                mark.GameObject.SetActive(true);
                Apply(mark,.52f);
            }

            private void Update()
            {
                float now=Time.unscaledTime;
                for(int i=0;i<marks.Count;i++)
                {
                    Mark mark=marks[i];
                    if(!mark.Active)continue;
                    float age=now-mark.Born;
                    if(age<HoldSeconds)continue;
                    float fade=Mathf.Clamp01((age-HoldSeconds)/FadeSeconds);
                    if(fade>=1f)
                    {
                        mark.Active=false;
                        mark.GameObject.SetActive(false);
                        continue;
                    }
                    Apply(mark,.52f*(1f-Mathf.SmoothStep(0f,1f,fade)));
                }
            }

            private void Apply(Mark mark,float alpha)
            {
                if(mark.Renderer==null)return;
                block.Clear();
                Color c=new(.025f,.03f,.038f,alpha);
                block.SetColor(BaseColor,c);
                block.SetColor(ColorId,c);
                mark.Renderer.SetPropertyBlock(block);
            }

            private Mark CreateMark(int index)
            {
                GameObject go=new("DF_LunarFootprint_"+index);
                go.transform.SetParent(transform,false);
                MeshFilter filter=go.AddComponent<MeshFilter>();
                filter.sharedMesh=footprintMesh;
                MeshRenderer renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=footprintMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                go.SetActive(false);
                return new Mark{GameObject=go,Renderer=renderer};
            }

            private static Mesh BuildFootprintMesh()
            {
                // Slightly asymmetric low-poly sole shape so it reads as a boot print, not a square decal.
                Vector3[] v={
                    new(-.105f,-.24f,0),new(.105f,-.24f,0),
                    new(.135f,-.11f,0),new(.125f,.10f,0),
                    new(.085f,.27f,0),new(-.085f,.27f,0),
                    new(-.125f,.10f,0),new(-.135f,-.11f,0)
                };
                int[] t={0,1,2,0,2,7,7,2,3,7,3,6,6,3,4,6,4,5};
                Mesh mesh=new(){name="DF_LunarFootprintMesh",vertices=v,triangles=t};
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }

            private static Material BuildMaterial()
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Transparent");
                if(shader==null)shader=Shader.Find("Sprites/Default");
                Material material=new(shader){name="DF_LunarFootprintMaterial",renderQueue=(int)RenderQueue.Transparent};
                Color baseColor=new(.025f,.03f,.038f,.52f);
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",baseColor);
                if(material.HasProperty("_Color"))material.SetColor("_Color",baseColor);
                if(material.HasProperty("_Surface"))material.SetFloat("_Surface",1f);
                if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                if(material.HasProperty("_ZWrite"))material.SetFloat("_ZWrite",0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                return material;
            }

            private void OnDestroy()
            {
                if(footprintMesh!=null)Destroy(footprintMesh);
                if(footprintMaterial!=null)Destroy(footprintMaterial);
                if(pool==this)pool=null;
            }
        }
    }
}
