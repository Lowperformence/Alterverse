using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Visual-only presentation layer for PSR J1719-1438 b.
    /// Existing terrain, colliders, navigation and HotZoneTank gameplay remain untouched.
    /// </summary>
    public sealed class DiamondCoreMapPolish : MonoBehaviour
    {
        readonly List<Material> ownedMaterials=new();
        Transform detailRoot;
        Transform builtWorld;
        Mesh facetGroundMesh;
        Mesh crystalMesh;
        float nextBossScan;

        // Floor stays close to the arena's original colour. The small value/alpha differences
        // make the polygon/prism structure readable without turning the map into a rainbow.
        static readonly Color[] FloorPalette=
        {
            new(.58f,.63f,.66f,.060f),
            new(.66f,.70f,.72f,.050f),
            new(.52f,.58f,.62f,.070f),
            new(.72f,.75f,.76f,.045f),
            new(.61f,.66f,.69f,.055f),
            new(.55f,.61f,.65f,.065f)
        };

        // Map crystals keep the supplied gemstone palette and transparent fluorescent look.
        static readonly Color[] CrystalPalette=
        {
            new(.82f,.95f,1.00f,.24f),
            new(.08f,.78f,1.00f,.25f),
            new(.12f,.40f,.95f,.23f),
            new(.58f,.28f,.90f,.22f),
            new(.94f,.32f,.66f,.20f),
            new(1.00f,.82f,.36f,.18f)
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
                nextBossScan=Time.unscaledTime+.25f;
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

            Material[] floorMaterials=new Material[FloorPalette.Length];
            for(int i=0;i<floorMaterials.Length;i++)
                floorMaterials[i]=MakeFloorFacetMaterial("DF_PSRJ1719_FloorFacet_"+i,FloorPalette[i],.86f);

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
            // Larger irregular facets make the prism structure visible from the gameplay camera
            // while leaving the original ground colour clearly dominant underneath.
            const float minX=-42f,maxX=42f,minZ=-36f,maxZ=36f,step=7.5f;
            List<Vector3> vertices=new();
            List<Vector3> normals=new();
            List<int>[] triangles=new List<int>[FloorPalette.Length];
            for(int i=0;i<triangles.Length;i++)triangles[i]=new List<int>();

            int cell=0;
            for(float z=minZ;z<maxZ-.1f;z+=step)
            for(float x=minX;x<maxX-.1f;x+=step)
            {
                float jitterX=((cell*37)%11-5)*.09f;
                float jitterZ=((cell*53)%13-6)*.075f;
                Vector3 a=Surface(x+jitterX,z+jitterZ,.035f);
                Vector3 b=Surface(x+step+jitterZ,z-jitterX,.035f);
                Vector3 c=Surface(x+step-jitterX,z+step+jitterZ,.035f);
                Vector3 d=Surface(x-jitterZ,z+step-jitterX,.035f);

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
                palette[i]=MakeCrystalMaterial("DF_PSRJ1719_Crystal_"+i,CrystalPalette[i],.96f,1.35f+(i%3)*.28f);

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

        Material MakeFloorFacetMaterial(string name,Color color,float smoothness)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material m=new(shader){name=name};
            ownedMaterials.Add(m);
            DiamondCoreCrystalVisuals.ConfigureTransparentMaterial(m,color,smoothness,0f,.015f);
            return m;
        }

        Material MakeCrystalMaterial(string name,Color color,float smoothness,float emission)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material m=new(shader){name=name};
            ownedMaterials.Add(m);
            DiamondCoreCrystalVisuals.ConfigureTransparentMaterial(m,color,smoothness,emission,.08f);
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

    /// <summary>
    /// Replaces the visible Diamond Core HotZoneTank body with one large floating faceted gemstone.
    /// Gameplay root, AI, health, attacks and colliders are untouched.
    /// </summary>
    public sealed class DiamondCoreCrystalGuardianVisualOverride : MonoBehaviour
    {
        readonly List<Renderer> hiddenOriginals=new();
        readonly List<Material> ownedMaterials=new();
        Transform visualRoot;
        Mesh gemMesh;
        float phase;
        float nextHideScan;

        void Awake()
        {
            if(!ArenaVariantRuntime.IsDiamondCore){enabled=false;return;}
            phase=Mathf.Abs(gameObject.name.GetHashCode()%1000)*.00628f;
            DisableOriginalRenderers();
            BuildVisual();
        }

        void BuildVisual()
        {
            GameObject root=new("DF_PSRJ1719_CrystalGuardianVisual");
            visualRoot=root.transform;
            visualRoot.SetParent(transform,false);
            visualRoot.localPosition=new Vector3(0f,2.55f,0f);

            gemMesh=DiamondCoreCrystalVisuals.BuildGuardianGemMesh();

            // Smoky transparent outer stone inspired by the supplied hand-held gemstone reference.
            Material smoky=NewMaterial("DF_PSRJ1719_GuardianSmokyShell",new Color(.24f,.20f,.23f,.58f),.94f,.35f,.07f);
            Material clear=NewMaterial("DF_PSRJ1719_GuardianClearFacet",new Color(.78f,.88f,.96f,.22f),.98f,.80f,.05f);
            Material core=NewMaterial("DF_PSRJ1719_GuardianCore",new Color(.08f,.72f,1f,.48f),.96f,3.6f,.04f);

            // One unmistakable large diamond body. No arms, legs, head or tank pieces are created.
            CreateGem("MainFloatingDiamond",visualRoot,Vector3.zero,new Vector3(2.25f,3.05f,1.72f),Quaternion.Euler(-3f,18f,4f),smoky);

            // A slightly offset transparent inner cut gives depth/refraction without changing silhouette.
            CreateGem("InnerPrismaticFacet",visualRoot,new Vector3(.04f,.02f,.03f),new Vector3(1.72f,2.48f,1.28f),Quaternion.Euler(180f,-9f,2f),clear);
            CreateGem("FluorescentCore",visualRoot,new Vector3(-.03f,-.10f,0f),new Vector3(.78f,1.62f,.58f),Quaternion.Euler(180f,14f,0f),core);
        }

        void LateUpdate()
        {
            if(visualRoot==null)return;

            // Some legacy planet-specific visual scripts can create/render parts after Awake.
            // Re-hide any such renderer so the guardian remains a pure diamond silhouette.
            if(Time.unscaledTime>=nextHideScan)
            {
                nextHideScan=Time.unscaledTime+.20f;
                DisableOriginalRenderers();
            }

            float t=Time.time+phase;
            visualRoot.localPosition=new Vector3(0f,2.55f+Mathf.Sin(t*.95f)*.17f,0f);
            visualRoot.localRotation=Quaternion.Euler(
                Mathf.Sin(t*.43f)*1.2f,
                t*7.5f,
                Mathf.Sin(t*.37f)*1.5f);
        }

        void DisableOriginalRenderers()
        {
            Renderer[] renderers=GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer r=renderers[i];
                if(r==null)continue;
                if(visualRoot!=null&&(r.transform==visualRoot||r.transform.IsChildOf(visualRoot)))continue;
                if(!hiddenOriginals.Contains(r))hiddenOriginals.Add(r);
                r.enabled=false;
            }
        }

        Material NewMaterial(string name,Color color,float smoothness,float emission,float metallic)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material m=new(shader){name=name};
            ownedMaterials.Add(m);
            DiamondCoreCrystalVisuals.ConfigureTransparentMaterial(m,color,smoothness,emission,metallic);
            return m;
        }

        GameObject CreateGem(string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {
            GameObject go=new(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localScale=scale;
            go.transform.localRotation=rotation;
            MeshFilter mf=go.AddComponent<MeshFilter>();
            mf.sharedMesh=gemMesh;
            MeshRenderer mr=go.AddComponent<MeshRenderer>();
            mr.sharedMaterial=material;
            mr.shadowCastingMode=ShadowCastingMode.Off;
            mr.receiveShadows=false;
            return go;
        }

        void OnDestroy()
        {
            for(int i=0;i<hiddenOriginals.Count;i++)
                if(hiddenOriginals[i]!=null)hiddenOriginals[i].enabled=true;
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

        public static void ConfigureTransparentMaterial(Material m,Color color,float smoothness,float emission,float metallic)
        {
            if(m==null)return;
            if(m.HasProperty(BaseColor))m.SetColor(BaseColor,color);
            if(m.HasProperty(ColorId))m.SetColor(ColorId,color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);

            if(emission>0f)
            {
                m.EnableKeyword("_EMISSION");
                Color e=new(color.r,color.g,color.b,1f);
                if(m.HasProperty(EmissionColor))m.SetColor(EmissionColor,e*emission);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                if(m.HasProperty(EmissionColor))m.SetColor(EmissionColor,Color.black);
            }

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
            // Tall cut gemstone: pointed crown, broad faceted body and narrowed lower cut.
            float[] y={1.58f,1.02f,.26f,-.62f,-1.25f,-1.48f};
            float[] r={.055f,.54f,.76f,.67f,.31f,.10f};
            const int sides=10;
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
                    Vector3 a=Point(y[ring],r[ring],s,sides,offsetA);
                    Vector3 b=Point(y[ring],r[ring],next,sides,offsetA);
                    Vector3 c=Point(y[ring+1],r[ring+1],next,sides,offsetB);
                    Vector3 d=Point(y[ring+1],r[ring+1],s,sides,offsetB);
                    Triangle(vertices,normals,indices,a,b,c);
                    Triangle(vertices,normals,indices,a,c,d);
                }
            }

            Mesh mesh=new(){name="DF_PSRJ1719_GuardianDiamondMesh"};
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Point(float y,float radius,int side,int sides,float offset)
        {
            float a=side*Mathf.PI*2f/sides+offset;
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
