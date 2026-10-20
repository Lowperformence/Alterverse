using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    // Cosmetic-only dimensional wings. No collider, light, particles or gameplay components.
    public sealed partial class VoidWingPresentation:MonoBehaviour
    {
        readonly Transform[] wings=new Transform[2];
        const float WingSize=.70f;
        Mesh mesh;Material core,edge,facet;Vector3 lastPosition;float motion,pulseUntil;bool preview,betaSupporter,plague,deluxe;
        Color energyColor;

        public void Build(Transform visual,bool isPreview)
        {
            if(mesh!=null)return;preview=isPreview;betaSupporter=false;plague=false;
            Shader coreShader=WingCoreShader(),edgeShader=WingEnergyShader();if(coreShader==null||edgeShader==null)return;
            core=CreateWingMaterial(coreShader,"DF_VoidObsidian",new Color(.035f,.018f,.065f));
            edge=CreateWingMaterial(edgeShader,"DF_VoidFractureEnergy",new Color(.43f,.08f,.75f));
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
                wing.transform.localScale=new Vector3(side==0?-1:1,1,1)*WingSize;
                wing.AddComponent<MeshFilter>().sharedMesh=mesh;var r=wing.AddComponent<MeshRenderer>();r.sharedMaterial=core;r.shadowCastingMode=ShadowCastingMode.Off;
                AddEnergyEdges(wing.transform,v,3,.026f,"VoidEnergyEdge");
            }
            lastPosition=transform.position;
        }

        public void BuildBetaSupporter(Transform visual,bool isPreview,HeroId hero)
        {
            if(mesh!=null)return;preview=isPreview;betaSupporter=true;plague=false;
            Shader coreShader=WingCoreShader(),edgeShader=WingEnergyShader();if(coreShader==null||edgeShader==null)return;
            core=CreateWingMaterial(coreShader,"DF_BetaSupporterWingCore",new Color(.055f,.008f,.012f));
            edge=CreateWingMaterial(edgeShader,"DF_BetaSupporterWingEnergy",new Color(.72f,.015f,.02f));
            if(core.HasProperty("_Metallic"))core.SetFloat("_Metallic",.26f);
            if(core.HasProperty("_Smoothness"))core.SetFloat("_Smoothness",.54f);
            energyColor=new Color(1f,.018f,.025f);
            edge.EnableKeyword("_EMISSION");edge.SetColor("_EmissionColor",energyColor*2.05f);

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

            float scale=HeroScale(hero,1.06f,.94f,.96f,1.02f,1.15f,1.18f)*WingSize;
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

        public void BuildPlague(Transform visual,bool isPreview,HeroId hero)
        {
            if(mesh!=null)return;preview=isPreview;betaSupporter=false;plague=true;
            Shader coreShader=WingCoreShader(),edgeShader=WingEnergyShader();if(coreShader==null||edgeShader==null)return;

            // Intentionally simpler and less prestigious than the Beta Supporter wings:
            // moss-green diseased plates with restrained toxic seams, tied to Green Planet.
            core=CreateWingMaterial(coreShader,"DF_PlagueWingCore",new Color(.13f,.24f,.065f));
            edge=CreateWingMaterial(edgeShader,"DF_PlagueWingToxin",new Color(.34f,.72f,.10f));
            if(core.HasProperty("_Metallic"))core.SetFloat("_Metallic",.04f);
            if(core.HasProperty("_Smoothness"))core.SetFloat("_Smoothness",.22f);
            energyColor=new Color(.43f,.90f,.09f);
            edge.EnableKeyword("_EMISSION");edge.SetColor("_EmissionColor",energyColor*1.28f);

            // Three torn pest leaves only: smaller, narrower and visibly less heroic than Beta Wings.
            // Their irregular shoulders read as rotten organic plates rather than premium energy blades.
            var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();
            float[] tipX={1.02f,1.22f,1.08f};
            float[] tipY={1.12f,.38f,-.48f};
            float[] shoulderDrop={.34f,.18f,.31f};
            for(int blade=0;blade<3;blade++)
            {
                float rootX=.16f+blade*.055f;
                float rootY=.32f-blade*.30f;
                int n=v.Count;
                v.Add(new Vector3(rootX,rootY,0));
                v.Add(new Vector3(tipX[blade],tipY[blade],-.09f));
                v.Add(new Vector3(tipX[blade]-.30f,tipY[blade]-shoulderDrop[blade],.025f));
                v.Add(new Vector3(rootX+.12f,rootY-.17f,.018f));
                t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            AddBackFaces(v,t);
            mesh=new Mesh{name="DF_PlagueWingLeaves"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();

            float scale=HeroScale(hero,.88f,.80f,.82f,.85f,.96f,.99f)*WingSize;
            float height=hero==HeroId.Rook?.27f:hero==HeroId.Kaelon?.25f:.21f;
            for(int side=0;side<2;side++)
            {
                var wing=new GameObject(side==0?"DF_PlagueWingLeft":"DF_PlagueWingRight");wing.transform.SetParent(visual,false);wings[side]=wing.transform;
                wing.transform.localPosition=new Vector3(side==0?-.13f:.13f,height,-.33f);
                wing.transform.localScale=new Vector3((side==0?-1:1)*scale,scale,scale);
                wing.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=wing.AddComponent<MeshRenderer>();r.sharedMaterial=core;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                AddEnergyEdges(wing.transform,v,3,.022f,"PlagueToxinVein");

                // One restrained toxin node per wing keeps the diseased identity without making it flashy.
                var toxicNode=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                toxicNode.name="DF_PlagueToxinNode";
                toxicNode.transform.SetParent(wing.transform,false);
                toxicNode.transform.localPosition=new Vector3(.31f,.08f,.025f);
                toxicNode.transform.localScale=Vector3.one*.060f;
                var collider=toxicNode.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}
                var nr=toxicNode.GetComponent<MeshRenderer>();nr.sharedMaterial=edge;nr.shadowCastingMode=ShadowCastingMode.Off;nr.receiveShadows=false;
            }
            lastPosition=transform.position;
        }


        public void BuildDeluxe(Transform visual,bool isPreview,HeroId hero)
        {
            if(mesh!=null)return;preview=isPreview;deluxe=true;betaSupporter=false;plague=false;
            // Keep white elements luminous on desktop previews and Android alike.
            Shader surface=WingCoreShader(),energy=RuntimeShaderLibrary.Unlit;if(surface==null||energy==null)return;
            core=CreateWingMaterial(surface,"DF_DeluxeSilver",new Color(.16f,.21f,.26f));
            facet=CreateWingMaterial(surface,"DF_DeluxePolishedCrystal",new Color(.92f,.97f,1f));
            edge=CreateWingMaterial(energy,"DF_DeluxeWhiteFractures",Color.white);
            foreach(Material material in new[]{core,facet})
            {
                if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.82f);
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.86f);
            }
            energyColor=Color.white;
            edge.EnableKeyword("_EMISSION");edge.SetColor("_EmissionColor",energyColor*1.25f);
            var vertices=new System.Collections.Generic.List<Vector3>();
            var triangles=new[]{new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>()};
            // Layered, closed crystal feathers: raised silver ridges, dark recesses and
            // sparse fluorescent white facets. The swept crown and long tapered tips follow the reference.
            int crystal=0;
            for(int row=0;row<3;row++)for(int column=0;column<7;column++)
            {
                float t=column/6f;
                Vector3 root=new Vector3(.26f+.09f*column+.015f*row,
                    .50f+Mathf.Sin(t*Mathf.PI)*.72f-.23f*row,-.035f*row);
                Vector3 tip=new Vector3(.40f+.085f*column-.05f*row,
                    root.y-.42f-.055f*column-.12f*row,-.11f-.035f*row);
                AddCrystal(vertices,triangles,root,tip,.10f-.009f*row,.075f,crystal++);
            }
            for(int column=0;column<7;column++)
            {
                float t=column/6f;
                AddCrystal(vertices,triangles,new Vector3(.18f+.08f*column,.35f+.06f*column,.025f),
                    new Vector3(.38f+.075f*column,.70f+Mathf.Sin(t*Mathf.PI*.8f)*.70f,-.055f),
                    .12f,.09f,crystal++);
            }
            mesh=new Mesh{name="DF_DeluxeFacetedCrystalWings"};
            mesh.SetVertices(vertices);mesh.subMeshCount=3;
            for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i,false);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            float scale=HeroScale(hero,1f,.92f,.94f,.98f,1.10f,1.12f)*WingSize;
            for(int side=0;side<2;side++)
            {
                var wing=new GameObject(side==0?"DF_DeluxeWingLeft":"DF_DeluxeWingRight");
                wing.transform.SetParent(visual,false);wings[side]=wing.transform;
                wing.transform.localPosition=new Vector3(side==0?-.13f:.13f,.25f,-.34f);
                wing.transform.localScale=new Vector3((side==0?-1:1)*scale,scale,scale);
                wing.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=wing.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{core,facet,edge};
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            lastPosition=transform.position;
        }

        private static void AddCrystal(System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<int>[] triangles,Vector3 root,Vector3 tip,float width,float depth,int crystal)
        {
            Vector3 axis=(tip-root).normalized;
            Vector3 across=new Vector3(-axis.y,axis.x,0).normalized*width;
            Vector3 shoulder=Vector3.Lerp(root,tip,.32f);
            Vector3 ridge=Vector3.Lerp(root,tip,.40f);
            Vector3[] points={root,shoulder+across,tip,shoulder-across,
                ridge+Vector3.forward*depth,ridge-Vector3.forward*depth};
            int[] faces={0,1,4,1,2,4,2,3,4,3,0,4,1,0,5,2,1,5,3,2,5,0,3,5};
            // Separate vertices keep each triangular facet flat instead of rounding crystals.
            for(int face=0;face<8;face++)
            {
                int material=face==1&&crystal%3==0?2:(face+crystal)%2;
                for(int corner=0;corner<3;corner++)
                {triangles[material].Add(vertices.Count);vertices.Add(points[faces[face*3+corner]]);}
                // Mirroring the left wing reverses winding; isolated back faces keep both
                // sides visible with correct flat normals under either transform sign.
                for(int corner=2;corner>=0;corner--)
                {triangles[material].Add(vertices.Count);vertices.Add(points[faces[face*3+corner]]);}
            }
        }

        private static Shader WingCoreShader()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Resources-backed shader is guaranteed into release builds; Shader.Find variants can be stripped.
            return RuntimeShaderLibrary.Lit;
#else
            return Shader.Find("Universal Render Pipeline/Lit")??RuntimeShaderLibrary.Lit??Shader.Find("Standard");
#endif
        }

        private static Shader WingEnergyShader()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Energy seams must stay bright even without scene lighting on mobile.
            return RuntimeShaderLibrary.Unlit;
#else
            return Shader.Find("Universal Render Pipeline/Lit")??RuntimeShaderLibrary.Unlit??Shader.Find("Standard");
#endif
        }

        private static Material CreateWingMaterial(Shader shader,string name,Color color)
        {
            if(shader==null)return null;
            var material=new Material(shader){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            else if(!material.HasProperty("_BaseColor"))material.color=color;
            return material;
        }

        private static float HeroScale(HeroId hero,float standard,float marksman,float support,float mage,float tank,float bruiser)
        {
            return hero switch
            {
                HeroId.Kaelon=>tank,
                HeroId.Rook=>bruiser,
                HeroId.Nyra=>marksman,
                HeroId.Mira=>support,
                HeroId.Elyra=>mage,
                _=>standard
            };
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
                if(angel)
                    wings[i].localRotation=Quaternion.Euler(4+motion*4,side*(10+motion*7+Mathf.Sin(time*1.05f)*4-pulse*3),side*(3+Mathf.Sin(time*.8f)*1.5f));
                else if(rgbRift)
                    wings[i].localRotation=Quaternion.Euler(5+motion*5,side*(12+motion*8+Mathf.Sin(time*1.1f)*2-pulse*4),side*(4+Mathf.Sin(time*.8f)*1.5f));
                else if(deluxe)
                    wings[i].localRotation=Quaternion.Euler(5+motion*5,side*(14+motion*8+Mathf.Sin(time*1.1f)*2-pulse*4),side*(4+Mathf.Sin(time*.8f)*1.5f));
                else if(plague)
                    wings[i].localRotation=Quaternion.Euler(7+motion*6+Mathf.Sin(time*1.15f)*1.4f,side*(16+motion*9+Mathf.Sin(time*1.05f)*3.2f-pulse*5),side*(7+Mathf.Sin(time*.72f)*2.8f));
                else if(betaSupporter)
                    wings[i].localRotation=Quaternion.Euler(5+motion*7,side*(18+motion*10+Mathf.Sin(time*1.35f)*2.4f-pulse*5),side*(5+Mathf.Sin(time*.85f)*2));
                else
                    wings[i].localRotation=Quaternion.Euler(10+motion*12,side*(12+motion*14+Mathf.Sin(time*1.8f)*3-pulse*8),0);
            }
            if(rgbRift&&edge!=null&&facet!=null)
            {
                float hue=Mathf.Repeat(time/6f,1f);
                edge.SetFloat("_HueShift",hue);
                edge.SetFloat("_Intensity",1.8f+Mathf.Sin(time*1.4f)*.15f+pulse*.5f);
            }
            else if(edge!=null)
            {
                float baseIntensity=plague?1.12f:betaSupporter?1.72f:1.15f;
                float wave=plague?Mathf.Sin(time*1.15f)*.10f:betaSupporter?Mathf.Sin(time*1.6f)*.16f:Mathf.Sin(time*2)*.12f;
                float pulseBoost=plague?.42f:betaSupporter?.75f:.6f;
                edge.SetColor("_EmissionColor",energyColor*(baseIntensity+wave+pulse*pulseBoost));
            }
        }
        void OnDestroy(){if(mountMesh!=null)Destroy(mountMesh);if(mesh!=null)Destroy(mesh);if(core!=null)Destroy(core);if(edge!=null)Destroy(edge);if(facet!=null)Destroy(facet);}
    }
}


