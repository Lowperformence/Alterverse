using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    // Cosmetic-only fractured blades. No collider, light, particles or gameplay components.
    public sealed class VoidWingPresentation:MonoBehaviour
    {
        readonly Transform[] wings=new Transform[2];
        Mesh mesh;Material core,edge;Vector3 lastPosition;float motion,pulseUntil;bool preview;
        public void Build(Transform visual,bool isPreview)
        {
            if(mesh!=null)return;preview=isPreview;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            core=new Material(shader){name="DF_VoidObsidian",color=new Color(.035f,.018f,.065f)};
            edge=new Material(shader){name="DF_VoidFractureEnergy",color=new Color(.43f,.08f,.75f)};
            if(core.HasProperty("_Smoothness"))core.SetFloat("_Smoothness",.6f);
            edge.EnableKeyword("_EMISSION");edge.SetColor("_EmissionColor",new Color(.52f,.08f,1f)*1.5f);
            // Three separated tapered fracture blades, double-sided with an opaque dark face.
            var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();
            for(int blade=0;blade<3;blade++)
            {
                float y=.18f-blade*.29f,x=.24f+blade*.06f;
                int n=v.Count;v.Add(new Vector3(x,y,0));v.Add(new Vector3(.9f-blade*.12f,1.05f-blade*.4f,-.16f));v.Add(new Vector3(.70f-blade*.1f,.37f-blade*.3f,.02f));v.Add(new Vector3(x+.10f,y-.12f,.02f));
                t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            // Separate back-face vertices keep opposing normals from cancelling.
            int frontCount=v.Count,frontIndices=t.Count;
            for(int i=0;i<frontCount;i++)v.Add(v[i]);
            for(int i=0;i<frontIndices;i+=3){t.Add(t[i+2]+frontCount);t.Add(t[i+1]+frontCount);t.Add(t[i]+frontCount);}
            mesh=new Mesh{name="DF_VoidWingFractures"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            for(int side=0;side<2;side++)
            {
                var wing=new GameObject(side==0?"DF_VoidWingLeft":"DF_VoidWingRight");wing.transform.SetParent(visual,false);wings[side]=wing.transform;
                wing.transform.localPosition=new Vector3(side==0?-.12f:.12f,.22f,-.27f);
                wing.transform.localScale=new Vector3(side==0?-1:1,1,1);
                wing.AddComponent<MeshFilter>().sharedMesh=mesh;var r=wing.AddComponent<MeshRenderer>();r.sharedMaterial=core;r.shadowCastingMode=ShadowCastingMode.Off;
                for(int blade=0;blade<3;blade++)
                {
                    var seam=new GameObject("VoidEnergyEdge");seam.transform.SetParent(wing.transform,false);
                    var line=seam.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=edge;line.positionCount=3;line.widthMultiplier=.026f;
                    int n=blade*4;line.SetPosition(0,v[n]);line.SetPosition(1,v[n+1]);line.SetPosition(2,v[n+2]);line.shadowCastingMode=ShadowCastingMode.Off;
                }
            }
            lastPosition=transform.position;
        }
        public void Pulse(){pulseUntil=Time.unscaledTime+.24f;}
        void LateUpdate()
        {
            if(mesh==null)return;
            float dt=preview?Time.unscaledDeltaTime:Time.deltaTime;if(dt<=0)return;
            Vector3 d=transform.position-lastPosition;lastPosition=transform.position;d.y=0;
            float speed=d.magnitude/Mathf.Max(.001f,dt);if(speed>20)speed=0; // Respawn is not running.
            motion=Mathf.Lerp(motion,Mathf.Clamp01(speed/6),1-Mathf.Exp(-dt*8));
            float time=preview?Time.unscaledTime:Time.time;
            float pulse=Mathf.Clamp01((pulseUntil-Time.unscaledTime)/.24f);
            for(int i=0;i<2;i++)if(wings[i]!=null)wings[i].localRotation=Quaternion.Euler(10+motion*12,(i==0?-1:1)*(12+motion*14+Mathf.Sin(time*1.8f)*3-pulse*8),0);
            if(edge!=null)edge.SetColor("_EmissionColor",new Color(.52f,.08f,1f)*(1.15f+Mathf.Sin(time*2)*.12f+pulse*.6f));
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(core!=null)Destroy(core);if(edge!=null)Destroy(edge);}
    }
}
