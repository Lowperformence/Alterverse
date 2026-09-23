using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    // Bounded, opaque impact marks. Reuse renderers/property blocks; never allocate one material per hit.
    public sealed class CombatImpactPool:MonoBehaviour
    {
        private const int Capacity=24;
        private const float Lifetime=.18f;
        private sealed class Mark
        {public Transform Transform;public MeshRenderer Renderer;public readonly MaterialPropertyBlock Block=new();public float Until,Started,Size;}
        private static CombatImpactPool instance;
        private readonly Mark[] marks=new Mark[Capacity];
        private Mesh mesh;
        private Material material;
        private int cursor;
        private Camera view;
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
        public static void Warm(Transform matchRoot)
        {
            if(instance!=null)return;
            var obj=new GameObject("DF_CombatImpactPool");if(matchRoot!=null)obj.transform.SetParent(matchRoot,false);
            obj.AddComponent<CombatImpactPool>();
        }
        public static void Emit(Vector3 point,Color color,float strength)
        {
            Camera camera=Camera.main;if(camera==null)return;
            Vector3 screen=camera.WorldToViewportPoint(point);
            if(screen.z<=0||screen.x<-.05f||screen.x>1.05f||screen.y<-.05f||screen.y>1.05f)return;
            if(instance==null)Warm(null);
            if(instance!=null)instance.Show(point,color,strength,camera);
        }
        private void Awake()
        {
            instance=this;
            Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)shader=Shader.Find("Unlit/Color");
            if(shader==null){enabled=false;return;}
            material=new Material(shader){name="DF_SharedImpactInk"};

            const int rayCount=6;
            Vector3[] vertices=new Vector3[rayCount*3+4];
            int[] triangles=new int[rayCount*6+12];
            for(int i=0;i<rayCount;i++)
            {
                float a=i*Mathf.PI*2f/rayCount+.23f;
                Vector3 axis=new(Mathf.Cos(a),Mathf.Sin(a),0);
                Vector3 side=new(-axis.y,axis.x,0);
                float length=i%2==0?1f:.72f;
                float width=i%2==0?.075f:.055f;
                int k=i*3;
                vertices[k]=axis*.16f-side*width;
                vertices[k+1]=axis*length;
                vertices[k+2]=axis*.16f+side*width;
                int t=i*6;
                triangles[t]=k;triangles[t+1]=k+1;triangles[t+2]=k+2;
                triangles[t+3]=k+2;triangles[t+4]=k+1;triangles[t+5]=k;
            }
            int c=rayCount*3,ti=rayCount*6;
            vertices[c]=new Vector3(0,.16f,0);
            vertices[c+1]=new Vector3(.16f,0,0);
            vertices[c+2]=new Vector3(0,-.16f,0);
            vertices[c+3]=new Vector3(-.16f,0,0);
            int[] center={c,c+1,c+2,c,c+2,c+3,c+2,c+1,c,c+3,c+2,c};
            for(int i=0;i<center.Length;i++)triangles[ti+i]=center[i];

            mesh=new Mesh{name="DF_ImpactBurst"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();
            for(int i=0;i<Capacity;i++)
            {
                GameObject obj=new GameObject("Impact");obj.transform.SetParent(transform,false);
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                MeshRenderer renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
                marks[i]=new Mark{Transform=obj.transform,Renderer=renderer};
            }
        }
        private void Show(Vector3 point,Color color,float strength,Camera camera)
        {
            if(material==null)return;view=camera;Mark mark=marks[cursor];cursor=(cursor+1)%Capacity;
            mark.Started=Time.time;mark.Until=Time.time+Lifetime;mark.Size=Mathf.Lerp(.26f,.62f,Mathf.Clamp01(strength));
            mark.Transform.position=point;mark.Transform.rotation=camera.transform.rotation;
            mark.Transform.localScale=Vector3.one*(mark.Size*.78f);
            mark.Block.SetColor(BaseColor,color);mark.Block.SetColor(ColorId,color);mark.Renderer.SetPropertyBlock(mark.Block);mark.Renderer.enabled=true;
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0)return;
            for(int i=0;i<Capacity;i++)
            {
                Mark mark=marks[i];if(mark==null||!mark.Renderer.enabled)continue;
                if(Time.time>=mark.Until){mark.Renderer.enabled=false;continue;}
                float t=Mathf.Clamp01((Time.time-mark.Started)/Lifetime);
                float punch=t<.22f?Mathf.Lerp(.78f,1.12f,t/.22f):Mathf.Lerp(1.12f,.08f,(t-.22f)/.78f);
                mark.Transform.localScale=Vector3.one*(mark.Size*punch);
                if(view!=null)mark.Transform.rotation=view.transform.rotation;
            }
        }
        private void OnDestroy(){if(instance==this)instance=null;if(material!=null)Destroy(material);if(mesh!=null)Destroy(mesh);}
    }
}
