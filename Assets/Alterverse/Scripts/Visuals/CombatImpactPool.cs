using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    // Bounded, opaque impact marks. Reuse renderers/property blocks; never allocate one material per hit.
    public sealed class CombatImpactPool:MonoBehaviour
    {
        private const int Capacity=24;
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
            // Four tapered impact rays, one mesh. No collider, transparency, light or shadow.
            Vector3[] vertices=new Vector3[12];int[] triangles=new int[24];
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f+.35f;Vector3 axis=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),side=new Vector3(-axis.y,axis.x,0);
                int k=i*3;vertices[k]=axis*.13f-side*.09f;vertices[k+1]=axis*(i%2==0?1:.7f);vertices[k+2]=axis*.13f+side*.09f;
                int t=i*6;triangles[t]=k;triangles[t+1]=k+1;triangles[t+2]=k+2;triangles[t+3]=k+2;triangles[t+4]=k+1;triangles[t+5]=k;
            }
            mesh=new Mesh{name="DF_ImpactRays"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();
            // Explicit front/back faces also support the opaque fallback shader.
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
            mark.Started=Time.time;mark.Until=Time.time+.16f;mark.Size=Mathf.Lerp(.24f,.55f,Mathf.Clamp01(strength));
            mark.Transform.position=point;mark.Transform.rotation=camera.transform.rotation;
            mark.Transform.localScale=Vector3.one*mark.Size;
            mark.Block.SetColor(BaseColor,color);mark.Block.SetColor(ColorId,color);mark.Renderer.SetPropertyBlock(mark.Block);mark.Renderer.enabled=true;
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0)return;
            for(int i=0;i<Capacity;i++)
            {
                Mark mark=marks[i];if(mark==null||!mark.Renderer.enabled)continue;
                if(Time.time>=mark.Until){mark.Renderer.enabled=false;continue;}
                float t=Mathf.Clamp01((Time.time-mark.Started)/.16f);
                mark.Transform.localScale=Vector3.one*(mark.Size*(1-t)*(.7f+2*t));
                if(view!=null)mark.Transform.rotation=view.transform.rotation;
            }
        }
        private void OnDestroy(){if(instance==this)instance=null;if(material!=null)Destroy(material);if(mesh!=null)Destroy(mesh);}
    }
}
