using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Shared ribbed pumpkin mesh and jack-o'-lantern face; presentation has no pickup authority/colliders.</summary>
    public sealed class HalloweenPumpkinVisual : MonoBehaviour
    {
        static Mesh body,face,eyes;
        static Material orange,dark,glow,stem;
        Transform art;
        float phase;

        public static GameObject Create(Transform parent,Vector3 ground,int id)
        {
            EnsureAssets();
            GameObject root=new("DF_HalloweenPumpkin_"+id);
            root.transform.SetParent(parent,false);root.transform.position=ground;
            var view=root.AddComponent<HalloweenPumpkinVisual>();view.phase=id*.71f;
            view.art=new GameObject("JackOLantern").transform;view.art.SetParent(root.transform,false);
            Part("RibbedPumpkin",view.art,body,orange);
            Part("CarvedFace",view.art,face,dark);
            Part("Candlelight",view.art,eyes,glow);
            GameObject stalk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stalk.name="PumpkinStem";stalk.transform.SetParent(view.art,false);
            stalk.transform.localPosition=new Vector3(0,.47f,0);
            stalk.transform.localScale=new Vector3(.13f,.105f,.13f);
            stalk.transform.localRotation=Quaternion.Euler(0,0,-16);
            Collider collider=stalk.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
            stalk.GetComponent<Renderer>().sharedMaterial=stem;
            return root;
        }
        void Update()
        {
            if (art==null) return;
            art.localPosition=Vector3.up*(.48f+Mathf.Sin(Time.time*2.2f+phase)*.055f);
            art.localRotation=Quaternion.Euler(0,Time.time*13f+phase*25f,0);
        }
        static void Part(string name,Transform parent,Mesh mesh,Material material)
        {
            GameObject go=new(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        static Material Make(Color color,float emission=0)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material result=new(shader){color=color};
            if(result.HasProperty("_BaseColor"))result.SetColor("_BaseColor",color);
            if(result.HasProperty("_Smoothness"))result.SetFloat("_Smoothness",.30f);
            if(emission>0 && result.HasProperty("_EmissionColor"))
            {result.EnableKeyword("_EMISSION");result.SetColor("_EmissionColor",color*emission);}
            return result;
        }
        static void EnsureAssets()
        {
            if(body!=null)return;
            orange=Make(new Color(.94f,.235f,.025f));dark=Make(new Color(.075f,.027f,.008f));
            glow=Make(new Color(1f,.57f,.07f),3.5f);stem=Make(new Color(.16f,.20f,.045f));
            const int sides=40,rings=18;
            Vector3[] vertices=new Vector3[(rings+1)*(sides+1)];int[] triangles=new int[rings*sides*6];
            for(int r=0;r<=rings;r++) for(int s=0;s<=sides;s++)
            {
                float phi=Mathf.PI*r/rings,theta=2*Mathf.PI*s/sides;
                float radius=.55f*Mathf.Sin(phi)*(1+.065f*Mathf.Cos(theta*10));
                vertices[r*(sides+1)+s]=new Vector3(radius*Mathf.Cos(theta),.43f*Mathf.Cos(phi),radius*Mathf.Sin(theta));
            }
            int t=0;
            for(int r=0;r<rings;r++)for(int s=0;s<sides;s++)
            {
                int a=r*(sides+1)+s,b=a+sides+1;
                triangles[t++]=a;triangles[t++]=a+1;triangles[t++]=b;
                triangles[t++]=a+1;triangles[t++]=b+1;triangles[t++]=b;
            }
            body=new Mesh{name="DF_SharedHalloweenPumpkin"};body.vertices=vertices;body.triangles=triangles;body.RecalculateNormals();body.RecalculateBounds();
            Vector3[] features={new(-.32f,.16f,-.49f),new(-.08f,.11f,-.555f),new(-.18f,-.035f,-.545f),
                new(.08f,.11f,-.555f),new(.32f,.16f,-.49f),new(.18f,-.035f,-.545f),
                new(-.27f,-.12f,-.51f),new(0,-.21f,-.53f),new(.27f,-.12f,-.51f),
                new(.20f,-.29f,-.47f),new(0,-.34f,-.46f),new(-.20f,-.29f,-.47f)};
            face=new Mesh{name="DF_PumpkinCarvedFace"};face.vertices=features;
            face.triangles=new[]{0,1,2,3,4,5,6,7,11,7,10,11,7,8,9,7,9,10};face.RecalculateNormals();face.RecalculateBounds();
            Vector3[] light=new Vector3[features.Length];
            for(int i=0;i<features.Length;i++){light[i]=features[i]*.76f;light[i].z=features[i].z-.009f;}
            eyes=new Mesh{name="DF_PumpkinCandlelight"};eyes.vertices=light;eyes.triangles=face.triangles;eyes.RecalculateNormals();eyes.RecalculateBounds();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared()
        {
            if(body!=null)Destroy(body);if(face!=null)Destroy(face);if(eyes!=null)Destroy(eyes);
            if(orange!=null)Destroy(orange);if(dark!=null)Destroy(dark);if(glow!=null)Destroy(glow);if(stem!=null)Destroy(stem);
            body=null;face=null;eyes=null;orange=null;dark=null;glow=null;stem=null;
        }
    }
}
