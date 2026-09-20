using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    // Cosmetic-only dimensional wings. No collider, light, particles or gameplay components.
    public sealed class VoidWingPresentation:MonoBehaviour
    {
        readonly Transform[] wings=new Transform[2];
        Mesh mesh;Material core,edge;Vector3 lastPosition;float motion,pulseUntil;bool preview,betaSupporter;
        Color energyColor;

        public void Build(Transform visual,bool isPreview)
        {
            if(mesh!=null)return;preview=isPreview;betaSupporter=false;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            core=new Material(shader){name="DF_VoidObsidian",color=new Color(.035f,.018f,.065f)};
            edge=new Material(shader){name="DF_VoidFractureEnergy",color=new Color(.43f,.08f,.75f)};
            if(core.HasProperty("_Smoothness"))core.SetFloat("_Smoothness",.6f);
            energyColor=new Color(.52f,.08f,1f);
            edge.EnableKeyword("_EMISSION");edge.SetColor("_EmissionColor",energyColor*1.5f);
            var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();
            for(int blade=0;blade<3;blade++)
            {
                float y=.18f-blade*.29f,x=.24f+blade*.06f;
                int n=v.Count;v.Add(new Vector3(x,y,0));v.Add(new Vector3(.9f-blade*.12f,1.05f-blade*.4f,-.16f));v.Add(new Vector3(.70f-blade*.1f,.37f-blade*.3f,.02f));v.Add(new Vector3(x+.10f,y-.12f,.02f));
                t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            AddBackFaces(v,t);
            mesh=new Mesh{name="DF_VoidWingFractures"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            for(int side=0;side<2;side++)
            {
                var wing=new GameObject(side==0?"DF_VoidWingLeft":"DF_VoidWingRight");wing.transform.SetParent(visual,false);wings[side]=wing.transform;
                wing.transform.localPosition=new Vector3(side==0?-.12f:.12f,.22f,-.27f);
                wing.transform.localScale=new Vector3(side==0?-1:1,1,1);
                wing.AddComponent<MeshFilter>().sharedMesh=mesh;var r=wing.AddComponent<MeshRenderer>();r.sharedMaterial=core;r.shadowCastingMode=ShadowCastingMode.Off;
                AddEnergyEdges(wing.transform,v,3,.026f,"VoidEnergyEdge");
            }
            lastPosition=transform.position;
        }

        public void BuildBetaSupporter(Transform visual,bool isPreview,HeroId hero)
        {
            if(mesh!=null)return;preview=isPreview;betaSupporter=true;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            core=new Material(shader){name="DF_BetaSupporterWingCore",color=new Color(.055f,.008f,.012f)};
            edge=new Material(shader){name="DF_BetaSupporterWingEnergy",color=new Color(.72f,.015f,.02f)};
            if(core.HasProperty("_Metallic"))core.SetFloat("_Metallic",.26f);
            if(core.HasProperty("_Smoothness"))core.SetFloat("_Smoothness",.54f);
            energyColor=new Color(1f,.018f,.025f);
            edge.EnableKeyword("_EMISSION");edge.SetColor("_EmissionColor",energyColor*2.05f);

            // Four long, aggressive dimensional blades form a broad red silhouette without transparent planes.
            var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();
            for(int blade=0;blade<4;blade++)
            {
                float rootY=.34f-blade*.27f;
                float rootX=.18f+blade*.055f;
                float tipX=blade==0?1.46f:blade==1?1.72f:blade==2?1.62f:1.34f;
                float tipY=blade==0?1.52f:blade==1?.82f:blade==2?.10f:-.58f;
                float shoulderX=tipX-(blade<2?.34f:.28f);
                float shoulderY=tipY-(blade==0?.48f:.25f);
                int n=v.Count;
                v.Add(new Vector3(rootX,rootY,0));
                v.Add(new Vector3(tipX,tipY,-.13f));
                v.Add(new Vector3(shoulderX,shoulderY,.015f));
                v.Add(new Vector3(rootX+.12f,rootY-.17f,.02f));
                t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            AddBackFaces(v,t);
            mesh=new Mesh{name="DF_BetaSupporterWingBlades"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();

            float scale=hero switch
            {
                HeroId.Kaelon=>1.15f,
                HeroId.Rook=>1.18f,
                HeroId.Nyra=>.94f,
                HeroId.Mira=>.96f,
                HeroId.Elyra=>1.02f,
                _=>1.06f
            };
            float height=hero==HeroId.Rook?.31f:hero==HeroId.Kaelon?.28f:.25f;
            for(int side=0;side<2;side++)
            {
                var wing=new GameObject(side==0?"DF_BetaSupporterWingLeft":"DF_BetaSupporterWingRight");wing.transform.SetParent(visual,false);wings[side]=wing.transform;
                wing.transform.localPosition=new Vector3(side==0?-.16f:.16f,height,-.34f);
                wing.transform.localScale=new Vector3((side==0?-1:1)*scale,scale,scale);
                wing.AddComponent<MeshFilter>().sharedMesh=mesh;var r=wing.AddComponent<MeshRenderer>();r.sharedMaterial=core;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                AddEnergyEdges(wing.transform,v,4,.034f,"BetaSupporterEnergyEdge");
            }
            lastPosition=transform.position;
        }

        private void AddEnergyEdges(Transform wing,System.Collections.Generic.List<Vector3> vertices,int bladeCount,float width,string label)
        {
            for(int blade=0;blade<bladeCount;blade++)
            {
                var seam=new GameObject(label);seam.transform.SetParent(wing,false);
                var line=seam.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=edge;line.positionCount=3;line.widthMultiplier=width;
                int n=blade*4;line.SetPosition(0,vertices[n]);line.SetPosition(1,vertices[n+1]);line.SetPosition(2,vertices[n+2]);line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            }
        }

        private static void AddBackFaces(System.Collections.Generic.List<Vector3> vertices,System.Collections.Generic.List<int> triangles)
        {
            int frontCount=vertices.Count,frontIndices=triangles.Count;
            for(int i=0;i<frontCount;i++)vertices.Add(vertices[i]);
            for(int i=0;i<frontIndices;i+=3){triangles.Add(triangles[i+2]+frontCount);triangles.Add(triangles[i+1]+frontCount);triangles.Add(triangles[i]+frontCount);}
        }

        public void Pulse(){pulseUntil=Time.unscaledTime+.24f;}
        void LateUpdate()
        {
            if(mesh==null)return;
            float dt=preview?Time.unscaledDeltaTime:Time.deltaTime;if(dt<=0)return;
            Vector3 d=transform.position-lastPosition;lastPosition=transform.position;d.y=0;
            float speed=d.magnitude/Mathf.Max(.001f,dt);if(speed>20)speed=0;
            motion=Mathf.Lerp(motion,Mathf.Clamp01(speed/6),1-Mathf.Exp(-dt*8));
            float time=preview?Time.unscaledTime:Time.time;
            float pulse=Mathf.Clamp01((pulseUntil-Time.unscaledTime)/.24f);
            for(int i=0;i<2;i++)if(wings[i]!=null)
            {
                float side=i==0?-1:1;
                if(betaSupporter)
                    wings[i].localRotation=Quaternion.Euler(5+motion*7,side*(18+motion*10+Mathf.Sin(time*1.35f)*2.4f-pulse*5),side*(5+Mathf.Sin(time*.85f)*2));
                else
                    wings[i].localRotation=Quaternion.Euler(10+motion*12,side*(12+motion*14+Mathf.Sin(time*1.8f)*3-pulse*8),0);
            }
            if(edge!=null)
            {
                float baseIntensity=betaSupporter?1.72f:1.15f;
                float wave=betaSupporter?Mathf.Sin(time*1.6f)*.16f:Mathf.Sin(time*2)*.12f;
                edge.SetColor("_EmissionColor",energyColor*(baseIntensity+wave+pulse*(betaSupporter?.75f:.6f)));
            }
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(core!=null)Destroy(core);if(edge!=null)Destroy(edge);}
    }
}
