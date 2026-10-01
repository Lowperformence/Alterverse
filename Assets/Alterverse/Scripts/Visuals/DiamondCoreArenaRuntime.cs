using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only runtime for DIAMOND CORE. The existing gameplay floor/colliders stay untouched;
    /// this replaces only the planet surface visuals and adds the transparent crystal world around them.
    /// </summary>
    public sealed class DiamondCoreArenaRuntime : MonoBehaviour
    {
        private readonly List<Material> materials=new();
        private readonly List<Mesh> meshes=new();
        private Transform root;
        private Mesh crystalMesh;
        private Material floorMaterial,seamMaterial,clearCrystal,blackCrystal,pulsarMaterial;
        private Renderer pulsarRenderer;
        private Light pulsarLight;
        private EntityId appliedWorldId;
        private float nextMonsterScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if(FindAnyObjectByType<DiamondCoreArenaRuntime>()==null)
                new GameObject("DF_DiamondCoreRuntime").AddComponent<DiamondCoreArenaRuntime>();
        }

        private void Update()
        {
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null)
            {
                if(root!=null||materials.Count>0||meshes.Count>0)ClearRuntime();
                appliedWorldId=default;
                return;
            }
            if(!ArenaVariantRuntime.IsDiamondCore)
            {
                if(root!=null||materials.Count>0||meshes.Count>0)ClearRuntime();
                appliedWorldId=default;return;
            }

            EntityId worldId=world.GetEntityId();
            if(appliedWorldId!=worldId)
            {
                ClearRuntime();
                appliedWorldId=worldId;
                Build(world.transform);
            }

            AnimatePulsar();
            if(Time.unscaledTime>=nextMonsterScan)
            {
                nextMonsterScan=Time.unscaledTime+.25f;
                SkinHotZoneMonsters();
            }
        }

        private void Build(Transform world)
        {
            GameObject lunar=GameObject.Find("DF_ProductionLunarGround");
            if(lunar!=null&&lunar.transform.IsChildOf(world))lunar.SetActive(false);

            root=new GameObject("DF_DiamondCoreArena").transform;
            root.SetParent(world,false);
            crystalMesh=CreateMapCrystalMesh();
            meshes.Add(crystalMesh);

            floorMaterial=CreateTransparentCrystal(new Color(.64f,.91f,1f,.32f),.08f,.96f,new Color(.16f,.72f,1f,1f)*.22f);
            seamMaterial=Mat(new Color(.69f,.94f,1f,1f),true,0f,.82f,new Color(.30f,.84f,1f,1f)*1.75f);
            clearCrystal=CreateTransparentCrystal(new Color(.77f,.94f,1f,.72f),.12f,.98f,new Color(.25f,.72f,1f,1f)*.42f);
            blackCrystal=Mat(new Color(.018f,.025f,.045f,1f),false,.16f,.88f,new Color(.03f,.12f,.20f,1f)*.28f);
            pulsarMaterial=Mat(new Color(.93f,.98f,1f,1f),true,0f,.95f,new Color(.62f,.90f,1f,1f)*2.2f);

            floorMaterial.SetTexture("_SurfaceData",Resources.Load<Texture2D>("PlanetSurfaces/DiamondCore/Ground"));
            floorMaterial.SetFloat("_SurfaceRelief",.22f);
            floorMaterial.SetFloat("_SurfaceScale",.25f);
            BuildTransparentSurface();
            BuildFractureSeams();
            BuildPerimeterCrystalFields();
            BuildShatteredCanyon();
            BuildUnderfloorDiamondHeart();
            BuildUnderfloorCosmos();
            BuildFloatingShards();
            BuildPulsar();
            BuildLighting();
            BuildCrystalPlateaus();
        }

        private void BuildTransparentSurface()
        {
            const int xSegments=84,zSegments=72;
            Vector3[] vertices=new Vector3[(xSegments+1)*(zSegments+1)];
            Vector2[] uv=new Vector2[vertices.Length];
            int[] triangles=new int[xSegments*zSegments*6];
            for(int z=0;z<=zSegments;z++)
            for(int x=0;x<=xSegments;x++)
            {
                int i=z*(xSegments+1)+x;
                float px=-42f+84f*x/xSegments;
                float pz=-36f+72f*z/zSegments;
                if(x>0&&x<xSegments&&z>0&&z<zSegments)
                {
                    px+=Mathf.Sin(x*8.91f+z*2.73f)*.22f;
                    pz+=Mathf.Sin(x*3.17f-z*7.21f)*.22f;
                }
                vertices[i]=FrontierPlanetLayout.Surface(px,pz)+Vector3.up*.006f;
                uv[i]=new Vector2((px+42f)/84f,(pz+36f)/72f);
            }
            int t=0;
            for(int z=0;z<zSegments;z++)
            for(int x=0;x<xSegments;x++)
            {
                int a=z*(xSegments+1)+x,b=a+1,c=a+xSegments+1,d=c+1;
                bool flip=((x+z)&1)==0;
                if(flip)
                {
                    triangles[t++]=a;triangles[t++]=c;triangles[t++]=d;
                    triangles[t++]=a;triangles[t++]=d;triangles[t++]=b;
                }
                else
                {
                    triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;
                    triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;
                }
            }
            Mesh mesh=new(){name="DF_DiamondCoreTransparentSurface",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject surface=new("DF_DiamondCoreCrystalFloor");surface.transform.SetParent(root,false);
            surface.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=floorMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            ShatteredArenaEdge.Build(root,ShatteredArenaEdge.Perimeter(vertices,xSegments,zSegments),clearCrystal,blackCrystal,blackCrystal,meshes);
        }

        private void BuildCrystalPlateaus()
        {
            // Paired faceted terraces frame the central crystalline court.
            for(int side=-1;side<=1;side+=2)for(int i=0;i<5;i++)
            {
                float x=side*(8f+i*6.4f),z=side*(29f+(i%2)*2.3f);
                GameObject shelf=Crystal("DF_PSRJ1719_CrystalTerrace",new Vector3(x,-.8f,z),
                    new Vector3(3.6f,1.4f,2.8f),blackCrystal);
                shelf.transform.localRotation=Quaternion.Euler(0,i*27f,side*7f);
                for(int j=0;j<3;j++)
                {
                    GameObject prism=Crystal("DF_PSRJ1719_TerracePrism",new Vector3(x+(j-1)*1.25f,.75f,z),
                        new Vector3(.40f,1.6f+(j%2)*.65f,.40f),clearCrystal);
                    prism.transform.localRotation=Quaternion.Euler(6f,i*33f+j*19f,side*(j-1)*9f);
                }
            }
        }

        private void BuildFractureSeams()
        {
            Vector2[] starts={new(-39,-28),new(-34,15),new(-27,-8),new(-19,28),new(-12,-23),new(-5,11),new(4,-31),new(10,24),new(17,-14),new(24,31),new(31,-24),new(38,7)};
            Vector2[] ends={new(-24,-16),new(-20,4),new(-10,-2),new(-4,17),new(4,-12),new(13,3),new(18,-18),new(25,12),new(33,-5),new(39,20),new(19,-15),new(24,15)};
            for(int i=0;i<starts.Length;i++)
            {
                Beam("DF_DiamondFracture",starts[i],ends[i],i%3==0?.075f:.045f,seamMaterial,.12f);
                Vector2 mid=Vector2.Lerp(starts[i],ends[i],.52f);
                Vector2 direction=(ends[i]-starts[i]).normalized;
                Vector2 normal=new(-direction.y,direction.x);
                Beam("DF_DiamondFractureBranch",mid,mid+normal*(1.6f+(i%4)*.55f),.032f,seamMaterial,.125f);
            }
        }

        private void BuildPerimeterCrystalFields()
        {
            Color[] colors={
                new(.28f,.88f,1f,1f),new(.76f,.30f,1f,1f),new(.24f,1f,.68f,1f),
                new(1f,.67f,.22f,1f),new(.88f,.96f,1f,1f)};
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI*2f/24f;
                float x=Mathf.Cos(a)*(45.5f+(i%3)*1.1f);
                float z=Mathf.Sin(a)*(39f+(i%4)*.75f);
                float height=2.4f+(i%6)*.72f;
                Material material=i%5==0?blackCrystal:Gem(colors[i%colors.Length],.34f);
                GameObject shard=Crystal("DF_DiamondPerimeterSpire",new Vector3(x,height*.63f,z),new Vector3(.65f+(i%3)*.16f,height,.65f+(i%2)*.2f),material);
                shard.transform.rotation=Quaternion.Euler(i%2==0?7f:-9f,i*47f,i%3==0?12f:-6f);
            }
        }

        private void BuildShatteredCanyon()
        {
            for(int i=0;i<11;i++)
            {
                float z=-30f+i*6f;
                float h=2.2f+(i%4)*1.05f;
                GameObject dark=Crystal("DF_BlackCrystalCanyon",new Vector3(-44.6f,h*.58f,z),new Vector3(1.05f,h,1.25f),blackCrystal);
                dark.transform.rotation=Quaternion.Euler(i%2==0?11f:-8f,i*31f,i%2==0?-14f:9f);
                if((i&1)==0)
                {
                    GameObject accent=Crystal("DF_BlackCanyonPrism",new Vector3(-42.9f,1.15f,z+1.35f),new Vector3(.34f,2.15f,.34f),Gem(i%4==0?new Color(.55f,.24f,1f):new Color(.12f,.84f,1f),.48f));
                    accent.transform.rotation=Quaternion.Euler(8f,i*51f,-11f);
                }
            }
        }

        private void BuildUnderfloorDiamondHeart()
        {
            if(clearCrystal!=null)clearCrystal.SetFloat("_FacetDetail",1f);
            GameObject heart=Crystal("DF_DiamondHeart",new Vector3(0,-7.6f,0),new Vector3(7.2f,8.7f,7.2f),clearCrystal);
            heart.transform.rotation=Quaternion.Euler(18f,34f,12f);
            Color[] spectrum={new(.2f,.86f,1f),new(.73f,.24f,1f),new(.16f,1f,.62f),new(1f,.58f,.16f),new(.98f,.42f,.72f),new(.62f,.78f,1f)};
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2f/12f;
                float r=10.5f+(i%3)*1.15f;
                Vector3 p=new(Mathf.Cos(a)*r,-5.7f-(i%3)*1.1f,Mathf.Sin(a)*r);
                GameObject shard=Crystal("DF_DiamondHeartOrbit",p,new Vector3(.75f+(i%2)*.3f,2.6f+(i%4)*.65f,.7f),Gem(spectrum[i%spectrum.Length],.52f));
                shard.transform.rotation=Quaternion.Euler(18f+i*5f,-a*Mathf.Rad2Deg,15f-i*2f);
            }
        }

        private void BuildUnderfloorCosmos()
        {
            BuildStarCarpet();
            BuildGalaxy(new Vector3(-25f,-13f,18f),10f,.42f,new Color(.52f,.30f,1f,1f),17);
            BuildGalaxy(new Vector3(25f,-16f,-18f),12f,.36f,new Color(.18f,.68f,1f,1f),31);
            BuildGalaxy(new Vector3(8f,-20f,29f),8f,.50f,new Color(1f,.42f,.72f,1f),47);
        }

        private void BuildStarCarpet()
        {
            const int count=210;
            Vector3[] vertices=new Vector3[count*4];Vector2[] uv=new Vector2[count*4];int[] triangles=new int[count*6];
            for(int i=0;i<count;i++)
            {
                float x=-48f+(i*37%97);
                float z=-42f+(i*61%85);
                float y=-11f-(i%7)*1.35f;
                float size=.055f+(i%5)*.025f;
                int v=i*4,q=i*6;
                vertices[v]=new Vector3(x-size,y,z-size);vertices[v+1]=new Vector3(x+size,y,z-size);
                vertices[v+2]=new Vector3(x+size,y,z+size);vertices[v+3]=new Vector3(x-size,y,z+size);
                uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.one;uv[v+3]=Vector2.up;
                triangles[q]=v;triangles[q+1]=v+2;triangles[q+2]=v+1;triangles[q+3]=v;triangles[q+4]=v+3;triangles[q+5]=v+2;
            }
            Mesh mesh=new(){name="DF_DiamondUnderfloorStars",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject stars=new("DF_DiamondUnderfloorStars");stars.transform.SetParent(root,false);stars.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=stars.AddComponent<MeshRenderer>();renderer.sharedMaterial=Mat(new Color(.88f,.95f,1f,1f),true,0f,0f,new Color(.7f,.9f,1f)*1.4f);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private void BuildGalaxy(Vector3 center,float radius,float flatten,Color color,int seed)
        {
            const int points=96;
            Vector3[] vertices=new Vector3[points*4];Vector2[] uv=new Vector2[points*4];int[] triangles=new int[points*6];
            for(int i=0;i<points;i++)
            {
                int arm=i%3;float t=(i/3)/31f;float a=arm*Mathf.PI*2f/3f+t*Mathf.PI*3.15f+seed*.07f;
                float r=.6f+t*radius+((i*seed)%11)*.035f;
                Vector3 p=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*flatten);
                float s=.08f+(i%5)*.025f;int v=i*4,q=i*6;
                vertices[v]=p+new Vector3(-s,0,-s);vertices[v+1]=p+new Vector3(s,0,-s);vertices[v+2]=p+new Vector3(s,0,s);vertices[v+3]=p+new Vector3(-s,0,s);
                uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.one;uv[v+3]=Vector2.up;
                triangles[q]=v;triangles[q+1]=v+2;triangles[q+2]=v+1;triangles[q+3]=v;triangles[q+4]=v+3;triangles[q+5]=v+2;
            }
            Mesh mesh=new(){name="DF_DiamondUnderfloorGalaxy",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject galaxy=new("DF_DiamondUnderfloorGalaxy");galaxy.transform.SetParent(root,false);galaxy.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=galaxy.AddComponent<MeshRenderer>();renderer.sharedMaterial=Mat(color,true,0f,0f,color*1.35f);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private void BuildFloatingShards()
        {
            Color[] colors={new(.35f,.88f,1f),new(.76f,.34f,1f),new(.28f,1f,.64f),new(1f,.64f,.22f)};
            for(int i=0;i<18;i++)
            {
                float a=i*2.39996323f;float r=31f+(i%5)*3.4f;
                Vector3 p=new(Mathf.Cos(a)*r,2.4f+(i%6)*.58f,Mathf.Sin(a)*r);
                if(Mathf.Abs(p.x)<29f&&Mathf.Abs(p.z)<27f)continue;
                GameObject shard=Crystal("DF_FloatingDiamondShard",p,new Vector3(.18f+(i%3)*.09f,.72f+(i%4)*.25f,.18f+(i%2)*.08f),Gem(colors[i%colors.Length],.38f));
                shard.transform.rotation=Quaternion.Euler(i*13f,i*41f,18f-i*3f);
                shard.AddComponent<DiamondShardDrift>().Configure(i);
            }
        }

        private void BuildPulsar()
        {
            GameObject star=GameObject.CreatePrimitive(PrimitiveType.Sphere);star.name="DF_DiamondPulsar";star.transform.SetParent(root,false);star.transform.position=new Vector3(0,18f,72f);star.transform.localScale=Vector3.one*5.4f;
            DisableCollider(star);pulsarRenderer=star.GetComponent<Renderer>();pulsarRenderer.sharedMaterial=pulsarMaterial;pulsarRenderer.shadowCastingMode=ShadowCastingMode.Off;
            Ring("DF_PulsarRingA",star.transform.position,9f,7.8f,seamMaterial,new Vector3(72f,0,0));
            Ring("DF_PulsarRingB",star.transform.position,13f,12.15f,clearCrystal,new Vector3(76f,18f,0));
            GameObject lightObject=new("DF_DiamondPulsarLight");lightObject.transform.SetParent(root,false);lightObject.transform.position=star.transform.position;
            pulsarLight=lightObject.AddComponent<Light>();pulsarLight.type=LightType.Point;pulsarLight.range=115f;pulsarLight.intensity=1.0f;pulsarLight.color=new Color(.54f,.84f,1f);pulsarLight.shadows=LightShadows.None;
        }

        private void BuildLighting()
        {
            GameObject key=new("DF_DiamondKeyLight");key.transform.SetParent(root,false);key.transform.rotation=Quaternion.Euler(48f,-32f,0f);
            Light light=key.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(.72f,.86f,1f);light.intensity=.58f;light.shadows=LightShadows.None;
            GameObject core=new("DF_DiamondHeartGlow");core.transform.SetParent(root,false);core.transform.position=new Vector3(0,-4f,0);
            Light coreLight=core.AddComponent<Light>();coreLight.type=LightType.Point;coreLight.color=new Color(.18f,.72f,1f);coreLight.intensity=.72f;coreLight.range=38f;coreLight.shadows=LightShadows.None;
        }

        private void AnimatePulsar()
        {
            if(root==null)return;
            float cycle=Mathf.Repeat(Time.unscaledTime,24f);
            float burst=cycle<1.6f?Mathf.Pow(1f-cycle/1.6f,3f):0f;
            float shimmer=.5f+.5f*Mathf.Sin(Time.unscaledTime*1.8f);
            float intensity=.65f+shimmer*.16f+burst*2.65f;
            if(pulsarLight!=null)pulsarLight.intensity=intensity;
            if(pulsarRenderer!=null&&pulsarRenderer.sharedMaterial!=null&&pulsarRenderer.sharedMaterial.HasProperty("_EmissionColor"))
                pulsarRenderer.sharedMaterial.SetColor("_EmissionColor",new Color(.58f,.88f,1f)*(.8f+burst*3.4f));
            root.localScale=Vector3.one;
        }

        private void SkinHotZoneMonsters()
        {
            // The world-space guardian is the active presentation. Avoid building a hidden giant.
            DiamondCoreGuardianWorldVisual activeVisual=FindAnyObjectByType<DiamondCoreGuardianWorldVisual>();
            if(activeVisual!=null&&activeVisual.isActiveAndEnabled)return;
            IReadOnlyList<HotZoneTank> active=HotZoneTank.Active;
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank tank=active[i];
                if(tank==null||tank.GetComponent<DiamondCoreHotZoneSkin>()!=null)continue;
                tank.gameObject.AddComponent<DiamondCoreHotZoneSkin>();
            }
        }

        private void Beam(string name,Vector2 a,Vector2 b,float width,Material material,float y)
        {
            Vector2 d=b-a;float length=d.magnitude;if(length<.01f)return;
            Vector2 mid=(a+b)*.5f;
            GameObject beam=GameObject.CreatePrimitive(PrimitiveType.Cube);beam.name=name;beam.transform.SetParent(root,false);beam.transform.position=new Vector3(mid.x,y,mid.y);beam.transform.localScale=new Vector3(width,.018f,length);
            beam.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg,0);DisableCollider(beam);Renderer renderer=beam.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        private GameObject Crystal(string name,Vector3 position,Vector3 scale,Material material)
        {
            GameObject go=new(name);go.transform.SetParent(root,false);go.transform.position=position;go.transform.localScale=scale;go.AddComponent<MeshFilter>().sharedMesh=crystalMesh;
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go;
        }

        private void Ring(string name,Vector3 position,float outer,float inner,Material material,Vector3 rotation)
        {
            const int segments=48;Vector3[] vertices=new Vector3[segments*2];int[] triangles=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2f/segments;float ca=Mathf.Cos(a),sa=Mathf.Sin(a);vertices[i*2]=new Vector3(ca*inner,0,sa*inner);vertices[i*2+1]=new Vector3(ca*outer,0,sa*outer);
                int n=(i+1)%segments,t=i*6;triangles[t]=i*2;triangles[t+1]=n*2;triangles[t+2]=i*2+1;triangles[t+3]=i*2+1;triangles[t+4]=n*2;triangles[t+5]=n*2+1;
            }
            Mesh mesh=new(){name=name,vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject go=new(name);go.transform.SetParent(root,false);go.transform.position=position;go.transform.rotation=Quaternion.Euler(rotation);go.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private Material Gem(Color color,float emission)
        {
            color.a=.56f;
            Material gem=CreateTransparentCrystal(color,.14f,.94f,color*emission*.55f);
            if(gem!=null)gem.SetFloat("_FacetDetail",1f);
            return gem;
        }

        private Material Mat(Color color,bool unlit,float metallic,float smoothness,Color emission)
        {
            Material material=RuntimeShaderLibrary.Create(color,unlit,metallic,smoothness,emission);
            if(material!=null)materials.Add(material);
            return material;
        }

        private Material CreateTransparentCrystal(Color color,float metallic,float smoothness,Color emission)
        {
            Shader shader=Resources.Load<Shader>("DF_DiamondCrystal");
            if(shader==null)
            {
                Debug.LogError("DIMENSIONS FRONT: Diamond Core crystal shader is missing from Resources.");
                return null;
            }
            Material material=new(shader);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",emission);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",Mathf.Clamp01(smoothness));
            if(material.HasProperty("_FresnelPower"))material.SetFloat("_FresnelPower",Mathf.Lerp(2.4f,3.8f,Mathf.Clamp01(smoothness)));
            if(material.HasProperty("_FresnelStrength"))material.SetFloat("_FresnelStrength",Mathf.Lerp(.52f,.82f,Mathf.Clamp01(metallic+smoothness*.5f)));
            if(material.HasProperty("_CoreAlpha"))material.SetFloat("_CoreAlpha",Mathf.Clamp(color.a*.34f,.08f,.30f));
            materials.Add(material);
            return material;
        }

        internal static Mesh CreateMapCrystalMesh()
        {
            const int sides=12;
            float[] heights={1.2f,.90f,.38f,.12f,.04f,-.42f,-.82f,-1f};
            float[] radii={0f,.28f,.56f,.70f,.72f,.52f,.22f,0f};
            var vertices=new List<Vector3>();var normals=new List<Vector3>();
            var colors=new List<Color>();var indices=new List<int>();
            Vector3 Point(int ring,int side)
            {
                float angle=side*Mathf.PI*2/sides+((ring&1)==0?0:Mathf.PI/sides*.45f);
                return new Vector3(Mathf.Cos(angle)*radii[ring],heights[ring],Mathf.Sin(angle)*radii[ring]);
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                Vector3 normal=Vector3.Cross(b-a,c-a);
                if(normal.sqrMagnitude<.000001f)return;
                Vector3 center=(a+b+c)/3;
                if(Vector3.Dot(normal,new Vector3(center.x,center.y-.1f,center.z))<0){(b,c)=(c,b);normal=-normal;}
                int index=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                normals.Add(normal.normalized);normals.Add(normal.normalized);normals.Add(normal.normalized);
                // Barycentric coordinates identify each actual cut edge in the crystal shader.
                colors.Add(new Color(1,0,0));colors.Add(new Color(0,1,0));colors.Add(new Color(0,0,1));
                indices.Add(index);indices.Add(index+1);indices.Add(index+2);
            }
            for(int ring=0;ring<heights.Length-1;ring++)
                for(int side=0;side<sides;side++)
                {
                    Vector3 a=Point(ring,side),b=Point(ring,side+1);
                    Vector3 c=Point(ring+1,side+1),d=Point(ring+1,side);
                    Triangle(a,b,c);Triangle(a,c,d);
                }
            var mesh=new Mesh{name="DF_MapDiamondBrilliantCut"};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);
            mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }

        internal static Mesh CreateCrystalMesh()
        {
            Vector3[] v={new(0,1.2f,0),new(0,-1f,0),new(.72f,.08f,0),new(0,.08f,.72f),new(-.72f,.08f,0),new(0,.08f,-.72f)};
            int[] t={0,2,3,0,3,4,0,4,5,0,5,2,1,3,2,1,4,3,1,5,4,1,2,5};
            Mesh mesh=new(){name="DF_FacetedDiamond",vertices=v,triangles=t};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static void DisableCollider(GameObject go)
        {
            Collider collider=go.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}
        }

        private void ClearRuntime()
        {
            if(root!=null)Destroy(root.gameObject);root=null;pulsarRenderer=null;pulsarLight=null;
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Destroy(materials[i]);materials.Clear();
            for(int i=0;i<meshes.Count;i++)if(meshes[i]!=null)Destroy(meshes[i]);meshes.Clear();crystalMesh=null;
        }

        private void OnDestroy(){ClearRuntime();}
    }

    internal sealed class DiamondShardDrift : MonoBehaviour
    {
        private Vector3 origin;private float phase,amplitude,speed;
        public void Configure(int seed){origin=transform.localPosition;phase=seed*.73f;amplitude=.08f+(seed%4)*.025f;speed=.45f+(seed%3)*.11f;}
        private void Update(){transform.localPosition=origin+Vector3.up*Mathf.Sin(Time.unscaledTime*speed+phase)*amplitude;transform.Rotate(0,Time.unscaledDeltaTime*(4f+phase),0,Space.Self);}
    }

    /// <summary>Diamond-planet appearance for the existing HotZoneTank AI, health and hitboxes.</summary>
    internal sealed class DiamondCoreHotZoneSkin : MonoBehaviour
    {
        private readonly List<Material> materials=new();
        private Mesh crystalMesh;
        private Transform visualRoot,leftArm,rightArm,leftLeg,rightLeg,head;
        private Material coreMaterial;
        private Vector3 lastPosition,bodyRest;
        private float moveBlend;

        private void Start()
        {
            Renderer[] oldRenderers=GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<oldRenderers.Length;i++)oldRenderers[i].enabled=false;
            Build();lastPosition=transform.position;
        }

        private void Build()
        {
            crystalMesh=DiamondCoreArenaRuntime.CreateCrystalMesh();
            visualRoot=new GameObject("DF_DiamondGiantVisual").transform;visualRoot.SetParent(transform,false);visualRoot.localPosition=new Vector3(0,-.78f,0);bodyRest=visualRoot.localPosition;
            Material cyan=Gem(new Color(.20f,.88f,1f),.72f),violet=Gem(new Color(.72f,.28f,1f),.66f),emerald=Gem(new Color(.18f,1f,.58f),.60f),amber=Gem(new Color(1f,.61f,.14f),.65f),pink=Gem(new Color(1f,.32f,.72f),.62f),clear=Gem(new Color(.82f,.96f,1f),.52f),dark=Gem(new Color(.035f,.045f,.075f),.20f);
            coreMaterial=Gem(new Color(.46f,.94f,1f),1.35f);

            Crystal("DF_DiamondGiantPelvis",visualRoot,new Vector3(0,.78f,0),new Vector3(.90f,.62f,.62f),dark);
            Crystal("DF_DiamondGiantTorso",visualRoot,new Vector3(0,1.62f,0),new Vector3(1.08f,1.12f,.72f),clear);
            Crystal("DF_DiamondGiantChestCore",visualRoot,new Vector3(0,1.72f,-.56f),new Vector3(.48f,.58f,.30f),coreMaterial);
            Crystal("DF_DiamondGiantShoulderL",visualRoot,new Vector3(-1.02f,1.88f,0),new Vector3(.62f,.72f,.65f),violet);
            Crystal("DF_DiamondGiantShoulderR",visualRoot,new Vector3(1.02f,1.88f,0),new Vector3(.62f,.72f,.65f),cyan);

            head=new GameObject("DF_DiamondGiantHeadPivot").transform;head.SetParent(visualRoot,false);head.localPosition=new Vector3(0,2.72f,0);
            Crystal("DF_DiamondGiantHead",head,Vector3.zero,new Vector3(.63f,.68f,.56f),emerald);
            Crystal("DF_DiamondGiantCrownL",head,new Vector3(-.34f,.58f,.02f),new Vector3(.18f,.56f,.18f),violet);
            Crystal("DF_DiamondGiantCrownC",head,new Vector3(0,.72f,.01f),new Vector3(.20f,.72f,.20f),clear);
            Crystal("DF_DiamondGiantCrownR",head,new Vector3(.34f,.58f,.02f),new Vector3(.18f,.56f,.18f),amber);

            leftArm=LimbPivot("DF_DiamondGiantLeftArm",new Vector3(-1.18f,1.80f,0));
            Crystal("DF_DiamondGiantLeftUpperArm",leftArm,new Vector3(-.26f,-.43f,0),new Vector3(.38f,.66f,.38f),violet);
            Crystal("DF_DiamondGiantLeftForearm",leftArm,new Vector3(-.39f,-1.08f,-.03f),new Vector3(.42f,.72f,.42f),pink);
            Crystal("DF_DiamondGiantLeftFist",leftArm,new Vector3(-.43f,-1.67f,-.08f),new Vector3(.52f,.48f,.52f),amber);

            rightArm=LimbPivot("DF_DiamondGiantRightArm",new Vector3(1.18f,1.80f,0));
            Crystal("DF_DiamondGiantRightUpperArm",rightArm,new Vector3(.26f,-.43f,0),new Vector3(.38f,.66f,.38f),cyan);
            Crystal("DF_DiamondGiantRightForearm",rightArm,new Vector3(.39f,-1.08f,-.03f),new Vector3(.42f,.72f,.42f),emerald);
            Crystal("DF_DiamondGiantRightFist",rightArm,new Vector3(.43f,-1.67f,-.08f),new Vector3(.52f,.48f,.52f),clear);

            leftLeg=LimbPivot("DF_DiamondGiantLeftLeg",new Vector3(-.48f,.68f,0));
            Crystal("DF_DiamondGiantLeftThigh",leftLeg,new Vector3(0,-.46f,0),new Vector3(.48f,.72f,.48f),emerald);
            Crystal("DF_DiamondGiantLeftShin",leftLeg,new Vector3(0,-1.08f,.02f),new Vector3(.40f,.68f,.40f),cyan);
            Crystal("DF_DiamondGiantLeftFoot",leftLeg,new Vector3(0,-1.57f,-.28f),new Vector3(.50f,.38f,.68f),dark);

            rightLeg=LimbPivot("DF_DiamondGiantRightLeg",new Vector3(.48f,.68f,0));
            Crystal("DF_DiamondGiantRightThigh",rightLeg,new Vector3(0,-.46f,0),new Vector3(.48f,.72f,.48f),amber);
            Crystal("DF_DiamondGiantRightShin",rightLeg,new Vector3(0,-1.08f,.02f),new Vector3(.40f,.68f,.40f),violet);
            Crystal("DF_DiamondGiantRightFoot",rightLeg,new Vector3(0,-1.57f,-.28f),new Vector3(.50f,.38f,.68f),dark);

            for(int i=0;i<7;i++)
            {
                float a=i*Mathf.PI*2f/7f;float r=.72f;
                Crystal("DF_DiamondGiantBackPrism",visualRoot,new Vector3(Mathf.Cos(a)*r,1.56f+Mathf.Sin(a)*.22f,.50f),new Vector3(.17f,.60f+(i%3)*.16f,.17f),i%2==0?violet:cyan).transform.rotation=Quaternion.Euler(18f,i*51f,Mathf.Cos(a)*18f);
            }
        }

        private Transform LimbPivot(string name,Vector3 localPosition)
        {
            Transform pivot=new GameObject(name).transform;pivot.SetParent(visualRoot,false);pivot.localPosition=localPosition;return pivot;
        }

        private GameObject Crystal(string name,Transform parent,Vector3 localPosition,Vector3 scale,Material material)
        {
            GameObject go=new(name);go.transform.SetParent(parent,false);go.transform.localPosition=localPosition;go.transform.localScale=scale;go.AddComponent<MeshFilter>().sharedMesh=crystalMesh;MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go;
        }

        private Material Gem(Color color,float emission)
        {
            Material material=RuntimeShaderLibrary.Create(color,false,.14f,.93f,color*emission);if(material!=null)materials.Add(material);return material;
        }

        private void Update()
        {
            if(visualRoot==null)return;
            float dt=Mathf.Max(Time.unscaledDeltaTime,.0001f);float speed=(transform.position-lastPosition).magnitude/dt;lastPosition=transform.position;
            moveBlend=Mathf.MoveTowards(moveBlend,Mathf.Clamp01(speed/2.25f),Time.unscaledDeltaTime*4f);
            float gait=Mathf.Sin(Time.unscaledTime*5.8f)*moveBlend;
            leftLeg.localRotation=Quaternion.Euler(gait*21f,0,gait*4f);rightLeg.localRotation=Quaternion.Euler(-gait*21f,0,-gait*4f);
            leftArm.localRotation=Quaternion.Euler(-gait*14f,0,-8f);rightArm.localRotation=Quaternion.Euler(gait*14f,0,8f);
            visualRoot.localPosition=bodyRest+Vector3.up*(Mathf.Abs(gait)*.055f+Mathf.Sin(Time.unscaledTime*1.7f)*.022f);
            head.localRotation=Quaternion.Euler(Mathf.Sin(Time.unscaledTime*1.15f)*2.2f,Mathf.Sin(Time.unscaledTime*.72f)*4f,0);
            if(coreMaterial!=null&&coreMaterial.HasProperty("_EmissionColor"))
            {
                float pulse=.9f+(.5f+.5f*Mathf.Sin(Time.unscaledTime*3.2f))*.75f;
                coreMaterial.SetColor("_EmissionColor",new Color(.32f,.88f,1f)*pulse);
            }
        }

        private void OnDestroy()
        {
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Destroy(materials[i]);
            if(crystalMesh!=null)Destroy(crystalMesh);
        }
    }
}

