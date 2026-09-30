using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Visual-only presentation layer for the Diamond Core arena (PSR J1719-1438 b).
    /// The original arena geometry, colliders, navigation and HotZoneTank gameplay remain untouched.
    /// </summary>
    public sealed class DiamondCoreMapPolish : MonoBehaviour
    {
        readonly List<Material> ownedMaterials=new();
        Transform detailRoot;
        Transform builtWorld;
        Mesh facetGroundMesh;
        Mesh crystalMesh;
        float nextBossScan;

        static readonly Color[] CrystalPalette=
        {
            new(.76f,.96f,1.00f,.22f),
            new(.08f,.84f,1.00f,.27f),
            new(.08f,.38f,1.00f,.25f),
            new(.60f,.28f,.96f,.23f),
            new(1.00f,.24f,.67f,.21f),
            new(1.00f,.84f,.30f,.18f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_DiamondCoreMapPolish")!=null)return;
            GameObject go=new("DF_DiamondCoreMapPolish");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DiamondCoreMapPolish>();
        }

        void Update()
        {
            bool active=ArenaVariantRuntime.IsDiamondCore;
            GameObject world=GameObject.Find("DF_MatchWorld");

            if(!active||world==null)
            {
                if(detailRoot!=null||ownedMaterials.Count>0)ClearMapVisuals();
                builtWorld=null;
                RemoveGuardianOverrides();
                return;
            }

            if(detailRoot==null||builtWorld!=world.transform)
                BuildMapVisuals(world.transform);

            if(Time.unscaledTime>=nextBossScan)
            {
                nextBossScan=Time.unscaledTime+.30f;
                ApplyGuardianOverrides();
            }
        }

        void BuildMapVisuals(Transform world)
        {
            ClearMapVisuals();
            builtWorld=world;

            GameObject root=new("DF_PSRJ1719_CrystalPolish");
            detailRoot=root.transform;
            detailRoot.SetParent(world,false);

            Material[] floorMaterials=new Material[CrystalPalette.Length];
            for(int i=0;i<floorMaterials.Length;i++)
            {
                Color c=CrystalPalette[i];
                c.a=Mathf.Min(c.a,.16f);
                floorMaterials[i]=MakeCrystalMaterial("DF_PSRJ1719_FloorFacet_"+i,c,.92f,.18f);
            }

            facetGroundMesh=BuildFacetGroundMesh();
            GameObject floor=new("FacetedCrystalGroundOverlay");
            floor.transform.SetParent(detailRoot,false);
            MeshFilter floorFilter=floor.AddComponent<MeshFilter>();
            MeshRenderer floorRenderer=floor.AddComponent<MeshRenderer>();
            floorFilter.sharedMesh=facetGroundMesh;
            floorRenderer.sharedMaterials=floorMaterials;
            floorRenderer.shadowCastingMode=ShadowCastingMode.Off;
            floorRenderer.receiveShadows=false;

            crystalMesh=BuildGemMesh("DF_PSRJ1719_FacetedGem",8);
            BuildPerimeterCrystals();
        }

        Mesh BuildFacetGroundMesh()
        {
            const float minX=-42f,maxX=42f,minZ=-36f,maxZ=36f,step=6f;
            List<Vector3> vertices=new();
            List<Vector3> normals=new();
            List<int>[] triangles=new List<int>[CrystalPalette.Length];
            for(int i=0;i<triangles.Length;i++)triangles[i]=new List<int>();

            int cell=0;
            for(float z=minZ;z<maxZ-.1f;z+=step)
            for(float x=minX;x<maxX-.1f;x+=step)
            {
                Vector3 a=Surface(x,z,.045f);
                Vector3 b=Surface(x+step,z,.045f);
                Vector3 c=Surface(x+step,z+step,.045f);
                Vector3 d=Surface(x,z+step,.045f);

                if((cell&1)==0)
                {
                    AddTriangle(vertices,normals,triangles[(cell*3+1)%triangles.Length],a,b,c);
                    AddTriangle(vertices,normals,triangles[(cell*5+3)%triangles.Length],a,c,d);
                }
                else
                {
                    AddTriangle(vertices,normals,triangles[(cell*7+2)%triangles.Length],a,b,d);
                    AddTriangle(vertices,normals,triangles[(cell*11+4)%triangles.Length],b,c,d);
                }
                cell++;
            }

            Mesh mesh=new(){name="DF_PSRJ1719_FacetedGroundMesh"};
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddTriangle(List<Vector3> vertices,List<Vector3> normals,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {
            if(Vector3.Cross(b-a,c-a).y<0f)(b,c)=(c,b);
            Vector3 n=Vector3.Cross(b-a,c-a).normalized;
            int start=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);
            normals.Add(n);normals.Add(n);normals.Add(n);
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);
        }

        void BuildPerimeterCrystals()
        {
            Material[] palette=new Material[CrystalPalette.Length];
            for(int i=0;i<palette.Length;i++)
                palette[i]=MakeCrystalMaterial("DF_PSRJ1719_Crystal_"+i,CrystalPalette[i],.96f,1.45f+(i%3)*.30f);

            const int count=34;
            for(int i=0;i<count;i++)
            {
                float t=(i+.18f*Mathf.Sin(i*2.71f))/count;
                float angle=t*Mathf.PI*2f;
                float radius=34.5f+((i*17)%8)*1.05f;
                float x=Mathf.Cos(angle)*radius;
                float z=Mathf.Sin(angle)*radius*.84f;
                Vector3 p=Surface(x,z,0f);

                float height=1.65f+((i*13)%7)*.29f;
                float width=.48f+((i*19)%5)*.11f;
                Quaternion rot=Quaternion.Euler(-5f+(i%3)*5f,i*47f,((i*29)%13)-6f);
                CreateMeshObject("FacetedCrystal_"+i,detailRoot,p+Vector3.up*(height*.67f),
                    new Vector3(width,height*.72f,width*.82f),rot,crystalMesh,palette[i%palette.Length]);

                if(i%3==0)
                {
                    float side=i%2==0?1f:-1f;
                    Vector3 q=Surface(x+Mathf.Cos(angle+1.4f)*1.05f,z+Mathf.Sin(angle+1.4f)*.85f,0f);
                    CreateMeshObject("FacetedCrystalSatellite_"+i,detailRoot,q+Vector3.up*(height*.36f),
                        new Vector3(width*.58f,height*.40f,width*.52f),Quaternion.Euler(12f,i*53f,side*18f),
                        crystalMesh,palette[(i+2)%palette.Length]);
                }
            }
        }

        void ApplyGuardianOverrides()
        {
            HotZoneTank[] tanks=Object.FindObjectsByType<HotZoneTank>(FindObjectsSortMode.None);
            for(int i=0;i<tanks.Length;i++)
            {
                if(tanks[i]==null||tanks[i].GetComponent<DiamondCoreCrystalGuardianVisualOverride>()!=null)continue;
                tanks[i].gameObject.AddComponent<DiamondCoreCrystalGuardianVisualOverride>();
            }
        }

        static void RemoveGuardianOverrides()
        {
            DiamondCoreCrystalGuardianVisualOverride[] overrides=Object.FindObjectsByType<DiamondCoreCrystalGuardianVisualOverride>(FindObjectsSortMode.None);
            for(int i=0;i<overrides.Length;i++)if(overrides[i]!=null)Object.Destroy(overrides[i]);
        }

        Material MakeCrystalMaterial(string name,Color color,float smoothness,float emission)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material m=new(shader){name=name};
            ownedMaterials.Add(m);
            DiamondCoreCrystalVisuals.ConfigureCrystalMaterial(m,color,smoothness,emission);
            return m;
        }

        static Vector3 Surface(float x,float z,float lift)
        {
            Vector3 p=LunarSurfaceField.Surface(x,z);
            p.y+=lift;
            return p;
        }

        static Mesh BuildGemMesh(string name,int sides)
        {
            float[] y={1.42f,.78f,-.14f,-.78f,-1.32f};
            float[] r={.06f,.48f,.68f,.42f,.055f};
            List<Vector3> vertices=new();
            List<Vector3> normals=new();
            List<int> indices=new();

            for(int ring=0;ring<y.Length-1;ring++)
            {
                float offsetA=(ring&1)==0?0f:Mathf.PI/sides;
                float offsetB=((ring+1)&1)==0?0f:Mathf.PI/sides;
                for(int s=0;s<sides;s++)
                {
                    int next=(s+1)%sides;
                    Vector3 a=GemPoint(y[ring],r[ring],s,sides,offsetA);
                    Vector3 b=GemPoint(y[ring],r[ring],next,sides,offsetA);
                    Vector3 c=GemPoint(y[ring+1],r[ring+1],next,sides,offsetB);
                    Vector3 d=GemPoint(y[ring+1],r[ring+1],s,sides,offsetB);
                    AddGemTriangle(vertices,normals,indices,a,b,c);
                    AddGemTriangle(vertices,normals,indices,a,c,d);
                }
            }

            Mesh mesh=new(){name=name};
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 GemPoint(float y,float radius,int side,int sides,float offset)
        {
            float a=side*Mathf.PI*2f/sides+offset;
            return new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);
        }

        static void AddGemTriangle(List<Vector3> vertices,List<Vector3> normals,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 n=Vector3.Cross(b-a,c-a).normalized;
            int start=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);
            normals.Add(n);normals.Add(n);normals.Add(n);
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);
        }

        static GameObject CreateMeshObject(string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Mesh mesh,Material material)
        {
            GameObject go=new(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localRotation=rotation;
            go.transform.localScale=scale;
            MeshFilter filter=go.AddComponent<MeshFilter>();
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();
            filter.sharedMesh=mesh;
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            return go;
        }

        void ClearMapVisuals()
        {
            if(detailRoot!=null)Destroy(detailRoot.gameObject);
            detailRoot=null;
            builtWorld=null;
            if(facetGroundMesh!=null)Destroy(facetGroundMesh);
            if(crystalMesh!=null)Destroy(crystalMesh);
            facetGroundMesh=null;
            crystalMesh=null;
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }

        void OnDestroy()
        {
            ClearMapVisuals();
            RemoveGuardianOverrides();
        }
    }

    public sealed class DiamondCoreCrystalGuardianVisualOverride : MonoBehaviour
    {
        readonly List<Renderer> hiddenOriginals=new();
        readonly List<Material> ownedMaterials=new();
        readonly List<Transform> shards=new();
        Transform visualRoot;
        Mesh gemMesh;
        float phase;

        void Awake()
        {
            if(!ArenaVariantRuntime.IsDiamondCore){enabled=false;return;}

            MeshRenderer[] meshes=GetComponentsInChildren<MeshRenderer>(true);
            for(int i=0;i<meshes.Length;i++)
            {
                if(meshes[i]==null)continue;
                hiddenOriginals.Add(meshes[i]);
                meshes[i].enabled=false;
            }
            SkinnedMeshRenderer[] skinned=GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for(int i=0;i<skinned.Length;i++)
            {
                if(skinned[i]==null)continue;
                hiddenOriginals.Add(skinned[i]);
                skinned[i].enabled=false;
            }

            // Unity 6.6 deprecates Object.GetInstanceID. A name hash is sufficient here because
            // it is used only to offset the cosmetic hover animation phase.
            phase=Mathf.Abs(gameObject.name.GetHashCode()%1000)*.00628f;
            BuildVisual();
        }

        void BuildVisual()
        {
            GameObject root=new("DF_PSRJ1719_CrystalGuardianVisual");
            visualRoot=root.transform;
            visualRoot.SetParent(transform,false);
            visualRoot.localPosition=new Vector3(0f,2.72f,0f);

            gemMesh=DiamondCoreCrystalVisuals.BuildGuardianGemMesh();
            Material shell=NewMaterial("DF_PSRJ1719_GuardianShell",new Color(.72f,.97f,1f,.34f),.98f,1.75f);
            Material core=NewMaterial("DF_PSRJ1719_GuardianCore",new Color(.02f,.67f,1f,.64f),.94f,4.4f);
            Material violet=NewMaterial("DF_PSRJ1719_GuardianViolet",new Color(.66f,.20f,1f,.38f),.95f,2.9f);
            Material pink=NewMaterial("DF_PSRJ1719_GuardianPink",new Color(1f,.20f,.68f,.34f),.93f,2.5f);
            Material gold=NewMaterial("DF_PSRJ1719_GuardianGold",new Color(1f,.80f,.24f,.28f),.96f,2.2f);

            CreateGem("OuterFacetedShell",visualRoot,Vector3.zero,new Vector3(1.55f,1.48f,1.08f),Quaternion.Euler(0,22.5f,0),shell);
            CreateGem("FluorescentCore",visualRoot,new Vector3(0,-.12f,0),new Vector3(.72f,1.05f,.54f),Quaternion.Euler(0,0,180f),core);

            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2f/10f;
                GameObject piece=GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name="CrownFacet_"+i;
                piece.transform.SetParent(visualRoot,false);
                piece.transform.localPosition=new Vector3(Mathf.Cos(a)*.46f,.91f,.73f+Mathf.Sin(a)*.045f);
                piece.transform.localRotation=Quaternion.Euler(0,0,-a*Mathf.Rad2Deg);
                piece.transform.localScale=new Vector3(.20f,.075f,.065f);
                Collider col=piece.GetComponent<Collider>();if(col!=null)Destroy(col);
                MeshRenderer mr=piece.GetComponent<MeshRenderer>();mr.sharedMaterial=i%2==0?core:shell;
                mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
            }

            for(int i=0;i<7;i++)
            {
                float a=i*Mathf.PI*2f/7f;
                float radius=1.85f+(i%2)*.18f;
                Vector3 p=new(Mathf.Cos(a)*radius,(i%3-.8f)*.22f,Mathf.Sin(a)*radius*.78f);
                Material mat=i%4==0?pink:i%4==1?violet:i%4==2?core:gold;
                GameObject shard=CreateGem("OrbitShard_"+i,visualRoot,p,
                    new Vector3(.24f,.58f,.20f)*(1f+(i%3)*.10f),Quaternion.Euler(i*19f,i*47f,i*11f),mat);
                shards.Add(shard.transform);
            }
        }

        void Update()
        {
            if(visualRoot==null)return;
            float t=Time.time+phase;
            visualRoot.localPosition=new Vector3(0f,2.72f+Mathf.Sin(t*1.25f)*.13f,0f);
            visualRoot.localRotation=Quaternion.Euler(0f,t*10f,Mathf.Sin(t*.72f)*1.8f);

            for(int i=0;i<shards.Count;i++)
            {
                Transform s=shards[i];if(s==null)continue;
                float a=t*(.34f+(i%2)*.06f)+i*Mathf.PI*2f/shards.Count;
                float r=1.85f+(i%2)*.18f;
                s.localPosition=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(t*1.1f+i)*.24f,Mathf.Sin(a)*r*.78f);
                s.localRotation=Quaternion.Euler(t*(12f+i),-t*(18f+i*2f),i*31f);
            }
        }

        Material NewMaterial(string name,Color color,float smoothness,float emission)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material m=new(shader){name=name};
            ownedMaterials.Add(m);
            DiamondCoreCrystalVisuals.ConfigureCrystalMaterial(m,color,smoothness,emission);
            return m;
        }

        GameObject CreateGem(string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {
            GameObject go=new(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localScale=scale;
            go.transform.localRotation=rotation;
            MeshFilter mf=go.AddComponent<MeshFilter>();mf.sharedMesh=gemMesh;
            MeshRenderer mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=material;
            mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
            return go;
        }

        void OnDestroy()
        {
            for(int i=0;i<hiddenOriginals.Count;i++)if(hiddenOriginals[i]!=null)hiddenOriginals[i].enabled=true;
            hiddenOriginals.Clear();
            if(visualRoot!=null)Destroy(visualRoot.gameObject);
            if(gemMesh!=null)Destroy(gemMesh);
            for(int i=0;i<ownedMaterials.Count;i++)if(ownedMaterials[i]!=null)Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
        }
    }

    static class DiamondCoreCrystalVisuals
    {
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        static readonly int ColorId=Shader.PropertyToID("_Color");
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");

        public static void ConfigureCrystalMaterial(Material m,Color color,float smoothness,float emission)
        {
            if(m==null)return;
            if(m.HasProperty(BaseColor))m.SetColor(BaseColor,color);
            if(m.HasProperty(ColorId))m.SetColor(ColorId,color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",.08f);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);

            m.EnableKeyword("_EMISSION");
            Color emissionColor=new(color.r,color.g,color.b,1f);
            if(m.HasProperty(EmissionColor))m.SetColor(EmissionColor,emissionColor*Mathf.Max(1f,emission));

            m.SetOverrideTag("RenderType","Transparent");
            if(m.HasProperty("_Surface"))m.SetFloat("_Surface",1f);
            if(m.HasProperty("_Blend"))m.SetFloat("_Blend",0f);
            if(m.HasProperty("_SrcBlend"))m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            if(m.HasProperty("_DstBlend"))m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            if(m.HasProperty("_ZWrite"))m.SetFloat("_ZWrite",0f);
            if(m.HasProperty("_Cull"))m.SetFloat("_Cull",(float)CullMode.Off);
            if(m.HasProperty("_Mode"))m.SetFloat("_Mode",3f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue=(int)RenderQueue.Transparent;
        }

        public static Mesh BuildGuardianGemMesh()
        {
            float[] y={1.42f,.78f,-.14f,-.78f,-1.32f};
            float[] r={.06f,.48f,.68f,.42f,.055f};
            const int sides=8;
            List<Vector3> vertices=new();
            List<Vector3> normals=new();
            List<int> indices=new();

            for(int ring=0;ring<y.Length-1;ring++)
            {
                float offsetA=(ring&1)==0?0f:Mathf.PI/sides;
                float offsetB=((ring+1)&1)==0?0f:Mathf.PI/sides;
                for(int s=0;s<sides;s++)
                {
                    int next=(s+1)%sides;
                    Vector3 a=Point(y[ring],r[ring],s,offsetA);
                    Vector3 b=Point(y[ring],r[ring],next,offsetA);
                    Vector3 c=Point(y[ring+1],r[ring+1],next,offsetB);
                    Vector3 d=Point(y[ring+1],r[ring+1],s,offsetB);
                    Triangle(vertices,normals,indices,a,b,c);
                    Triangle(vertices,normals,indices,a,c,d);
                }
            }

            Mesh mesh=new(){name="DF_PSRJ1719_GuardianGemMesh"};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Point(float y,float radius,int side,float offset)
        {
            float a=side*Mathf.PI*2f/8f+offset;
            return new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);
        }

        static void Triangle(List<Vector3> vertices,List<Vector3> normals,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 n=Vector3.Cross(b-a,c-a).normalized;
            int start=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);
            normals.Add(n);normals.Add(n);normals.Add(n);
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);
        }
    }
}
