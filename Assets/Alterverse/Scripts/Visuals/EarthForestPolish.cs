using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Extra Earth-only visual dressing: denser mixed forest, pointed conifers in several sizes,
    /// deliberately flat blue water pools, and worn brown soil around gameplay structures.
    /// All generated dressing is collider-free and presentation-only.
    /// </summary>
    public sealed class EarthForestPolish : MonoBehaviour
    {
        const string ManagerName="DF_EarthForestPolish";
        const string ForestRootName="DF_EarthForestExtra";
        const string SoilRootName="DF_EarthBuildingSoil";

        static readonly Vector2[] WaterCenters=
        {
            new(-28f,17f),new(27f,-18f),new(-17f,-27f),new(18f,27f)
        };

        static readonly Vector2[] WaterRadii=
        {
            new(5.7f,4.1f),new(5.0f,3.8f),new(4.4f,3.4f),new(4.2f,3.3f)
        };

        static readonly Vector2[] TreeSites=
        {
            new(-42,29),new(-39,21),new(-41,8),new(-40,-6),new(-42,-19),new(-39,-30),
            new(42,29),new(39,21),new(41,8),new(40,-6),new(42,-19),new(39,-30),
            new(-34,31),new(-31,25),new(-35,4),new(-34,-12),new(-32,-23),new(-30,-32),
            new(34,31),new(31,25),new(35,4),new(34,-12),new(32,-23),new(30,-32),
            new(-25,8),new(-24,-8),new(-21,23),new(-20,-20),new(-14,31),new(-12,-16),
            new(25,8),new(24,-8),new(21,23),new(20,-20),new(14,31),new(12,-16),
            new(-7,30),new(-6,-30),new(-8,20),new(-9,-21),new(7,30),new(6,-30),
            new(8,20),new(9,-21),new(-18,3),new(-17,-6),new(18,3),new(17,-6),
            new(-29,-2),new(29,2),new(-36,16),new(36,-16),new(-27,-15),new(27,15)
        };

        static bool installed;
        Transform forestRoot;
        Transform soilRoot;
        readonly List<Material> materials=new();
        readonly List<Mesh> meshes=new();
        readonly HashSet<Vector2Int> soilSites=new();
        Material waterMaterial;
        Material soilDark,soilMid,soilLight;
        float nextWaterRefresh;
        float nextSoilRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){installed=false;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(installed)return;
            installed=true;
            GameObject go=new(ManagerName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<EarthForestPolish>();
        }

        void Update()
        {
            bool earth=ArenaVariantRuntime.Current==ArenaVariant.Earth;
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(!earth||world==null)
            {
                if(forestRoot!=null)ClearForest();
                return;
            }

            if(forestRoot==null&&GameObject.Find("DF_PlanetTheme_Earth")!=null)
                BuildForest(world.transform);

            if(Time.unscaledTime>=nextWaterRefresh)
            {
                nextWaterRefresh=Time.unscaledTime+.75f;
                ApplyFlatWater(world.transform);
            }

            if(soilRoot!=null&&Time.unscaledTime>=nextSoilRefresh)
            {
                nextSoilRefresh=Time.unscaledTime+.75f;
                RefreshBuildingSoil(world.transform);
            }
        }

        SceneryBuildingClearance clearance;

        void BuildForest(Transform world)
        {
            clearance=new SceneryBuildingClearance();
            ClearForest();
            GameObject root=new(ForestRootName);
            forestRoot=root.transform;
            forestRoot.SetParent(world,false);

            GameObject soil=new(SoilRootName);
            soilRoot=soil.transform;
            soilRoot.SetParent(forestRoot,false);

            Material bark=Mat("DF_EarthExtra_Bark",new Color(.17f,.075f,.026f,1f),.04f,.18f);
            Material barkLight=Mat("DF_EarthExtra_BirchBark",new Color(.72f,.68f,.55f,1f),.02f,.24f);
            Material broadDark=Mat("DF_EarthExtra_BroadDark",new Color(.045f,.25f,.035f,1f),0f,.16f);
            Material broadLight=Mat("DF_EarthExtra_BroadLight",new Color(.12f,.43f,.055f,1f),0f,.18f);
            Material pineDark=Mat("DF_EarthExtra_PineDark",new Color(.025f,.16f,.045f,1f),0f,.13f);
            Material pineLight=Mat("DF_EarthExtra_PineLight",new Color(.055f,.28f,.07f,1f),0f,.15f);

            // Strong, visibly brown earth. Three close tones are layered so the ground reads as
            // compacted/worn soil rather than one artificial-looking painted circle.
            soilDark=Mat("DF_Earth_Soil_Dark",new Color(.22f,.070f,.020f,1f),0f,.08f);
            soilMid=Mat("DF_Earth_Soil_Mid",new Color(.34f,.125f,.030f,1f),0f,.10f);
            soilLight=Mat("DF_Earth_Soil_Worn",new Color(.44f,.205f,.055f,1f),0f,.12f);

            for(int i=0;i<TreeSites.Length;i++)
            {
                Vector2 site=TreeSites[i];
                if(OverWater(site))continue;
                Vector3 p=LunarSurfaceField.Surface(site.x,site.y);
                float size=.78f+(i%7)*.085f;
                switch(i%5)
                {
                    case 0: BuildPine(p,size*1.28f,bark,pineDark,pineLight); break;
                    case 1: BuildBroadleaf(p,size*.92f,bark,broadDark,broadLight); break;
                    case 2: BuildBirch(p,size*.88f,barkLight,broadLight,broadDark); break;
                    case 3: BuildPine(p,size*.78f,bark,pineLight,pineDark); break;
                    default: BuildBroadleaf(p,size*1.12f,bark,broadLight,broadDark); break;
                }
            }

            // Small forest groups add density without creating a uniform grid.
            Vector2[] clusterCenters={new(-37,-25),new(-24,14),new(-13,12),new(13,-12),new(24,-14),new(37,25)};
            for(int c=0;c<clusterCenters.Length;c++)
            {
                for(int j=0;j<4;j++)
                {
                    float a=(c*41+j*91)*Mathf.Deg2Rad;
                    float r=1.2f+j*.48f;
                    Vector2 q=clusterCenters[c]+new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                    if(OverWater(q))continue;
                    Vector3 p=LunarSurfaceField.Surface(q.x,q.y);
                    float size=.58f+j*.09f;
                    if((c+j)%2==0)BuildPine(p,size,bark,pineDark,pineLight);
                    else BuildBroadleaf(p,size,bark,broadDark,broadLight);
                }
            }

            for(int i=0;i<forestRoot.childCount;i++)clearance.RemoveIfBlocked(forestRoot.GetChild(i).gameObject);
            RefreshBuildingSoil(world);
        }

        void RefreshBuildingSoil(Transform world)
        {
            if(soilRoot==null||soilDark==null||soilMid==null||soilLight==null)return;

            // Towers can be created after the environment pass, therefore this is refreshed at
            // runtime and each world-space site is added only once.
            TowerVisualShell[] towers=FindObjectsByType<TowerVisualShell>(FindObjectsSortMode.None);
            for(int i=0;i<towers.Length;i++)
            {
                TowerVisualShell tower=towers[i];
                if(tower==null||!IsUnderWorld(tower.transform,world))continue;
                AddSoilSite(tower.transform.position,8.2f,7.2f,"Tower");
            }

            // Reality cores are the large team-base structures, so their worn soil area is wider.
            RealityCoreShell[] cores=FindObjectsByType<RealityCoreShell>(FindObjectsSortMode.None);
            for(int i=0;i<cores.Length;i++)
            {
                RealityCoreShell core=cores[i];
                if(core==null||!IsUnderWorld(core.transform,world))continue;
                AddSoilSite(core.transform.position,12.0f,10.2f,"Base");
            }
        }

        static bool IsUnderWorld(Transform item,Transform world)
        {
            return item!=null&&world!=null&&(item==world||item.IsChildOf(world));
        }

        void AddSoilSite(Vector3 worldPosition,float width,float depth,string label)
        {
            Vector2Int key=new(Mathf.RoundToInt(worldPosition.x*2f),Mathf.RoundToInt(worldPosition.z*2f));
            if(!soilSites.Add(key))return;

            Vector3 surfaceWorld=LunarSurfaceField.Surface(worldPosition.x,worldPosition.z);
            Vector3 center=soilRoot.InverseTransformPoint(surfaceWorld);
            float angle=Mathf.Repeat(worldPosition.x*17.3f+worldPosition.z*9.7f,360f);
            string suffix=key.x+"_"+key.y;

            // Broad dark edge establishes a strong brown footprint around the structure.
            GameObject outer=Primitive(PrimitiveType.Cylinder,label+"SoilOuter_"+suffix,soilRoot,
                center+Vector3.up*.018f,new Vector3(width,.010f,depth),soilDark);
            outer.transform.localRotation=Quaternion.Euler(0,angle,0);

            // Offset lobes break the perfect ellipse and make the patch look organically worn.
            GameObject lobeA=Primitive(PrimitiveType.Cylinder,label+"SoilLobeA_"+suffix,soilRoot,
                center+new Vector3(width*.11f,.030f,depth*.055f),new Vector3(width*.76f,.009f,depth*.70f),soilMid);
            lobeA.transform.localRotation=Quaternion.Euler(0,angle+23f,0);

            GameObject lobeB=Primitive(PrimitiveType.Cylinder,label+"SoilLobeB_"+suffix,soilRoot,
                center+new Vector3(-width*.12f,.034f,-depth*.07f),new Vector3(width*.67f,.008f,depth*.62f),soilMid);
            lobeB.transform.localRotation=Quaternion.Euler(0,angle-31f,0);

            // Brighter compacted center directly under the building gives the soil visual depth.
            GameObject worn=Primitive(PrimitiveType.Cylinder,label+"SoilWorn_"+suffix,soilRoot,
                center+Vector3.up*.043f,new Vector3(width*.48f,.007f,depth*.44f),soilLight);
            worn.transform.localRotation=Quaternion.Euler(0,angle+11f,0);
        }

        bool OverWater(Vector2 p)
        {
            for(int i=0;i<WaterCenters.Length;i++)
            {
                Vector2 d=p-WaterCenters[i];
                Vector2 r=WaterRadii[i];
                float ellipse=d.x*d.x/(r.x*r.x)+d.y*d.y/(r.y*r.y);
                if(ellipse<1f)return true;
            }
            return false;
        }

        void BuildPine(Vector3 basePos,float size,Material bark,Material dark,Material light)
        {
            Transform root=Node("Pine",basePos);
            float h=3.5f*size;
            Primitive(PrimitiveType.Cylinder,"PineTrunk",root,new Vector3(0,h*.32f,0),new Vector3(.18f*size,h*.32f,.18f*size),bark);
            Cone("PineLower",root,new Vector3(0,h*.40f,0),1.20f*size,1.65f*size,14,dark);
            Cone("PineMid",root,new Vector3(0,h*.66f,0),.92f*size,1.48f*size,14,light);
            Cone("PineTop",root,new Vector3(0,h*.88f,0),.62f*size,1.20f*size,14,dark);
        }

        void BuildBroadleaf(Vector3 basePos,float size,Material bark,Material dark,Material light)
        {
            Transform root=Node("Broadleaf",basePos);
            float h=2.75f*size;
            Primitive(PrimitiveType.Cylinder,"BroadTrunk",root,new Vector3(0,h*.48f,0),new Vector3(.22f*size,h*.48f,.22f*size),bark);
            Primitive(PrimitiveType.Sphere,"BroadCrown",root,new Vector3(0,h+.58f*size,0),new Vector3(1.28f,1.02f,1.18f)*size,dark);
            Primitive(PrimitiveType.Sphere,"BroadCrownL",root,new Vector3(-.62f*size,h+.32f*size,.10f),new Vector3(.82f,.70f,.78f)*size,light);
            Primitive(PrimitiveType.Sphere,"BroadCrownR",root,new Vector3(.58f*size,h+.38f*size,-.12f),new Vector3(.76f,.66f,.72f)*size,light);
        }

        void BuildBirch(Vector3 basePos,float size,Material bark,Material light,Material dark)
        {
            Transform root=Node("Birch",basePos);
            float h=3.2f*size;
            Primitive(PrimitiveType.Cylinder,"BirchTrunk",root,new Vector3(0,h*.50f,0),new Vector3(.13f*size,h*.50f,.13f*size),bark);
            Primitive(PrimitiveType.Sphere,"BirchCrownTop",root,new Vector3(0,h+.38f*size,0),new Vector3(.72f,.92f,.67f)*size,light);
            Primitive(PrimitiveType.Sphere,"BirchCrownSideA",root,new Vector3(-.43f*size,h-.30f*size,.05f),new Vector3(.60f,.68f,.56f)*size,dark);
            Primitive(PrimitiveType.Sphere,"BirchCrownSideB",root,new Vector3(.39f*size,h-.06f*size,-.08f),new Vector3(.57f,.63f,.53f)*size,light);
        }

        Transform Node(string name,Vector3 position)
        {
            GameObject go=new(name);
            go.transform.SetParent(forestRoot,false);
            go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(0,(position.x*13.7f+position.z*7.9f)%360f,0);
            return go.transform;
        }

        static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            Collider col=go.GetComponent<Collider>();
            if(col!=null)Object.Destroy(col);
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null)renderer.sharedMaterial=material;
            return go;
        }

        void Cone(string name,Transform parent,Vector3 localPosition,float radius,float height,int segments,Material material)
        {
            GameObject go=new(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            MeshFilter filter=go.AddComponent<MeshFilter>();
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;

            Mesh mesh=new(){name="DF_EarthPineCone"};
            Vector3[] vertices=new Vector3[segments+2];
            vertices[0]=Vector3.zero;
            vertices[segments+1]=new Vector3(0,height,0);
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2f/segments;
                vertices[i+1]=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
            }
            int[] triangles=new int[segments*6];
            int t=0;
            for(int i=0;i<segments;i++)
            {
                int a=i+1;
                int b=((i+1)%segments)+1;
                triangles[t++]=0;triangles[t++]=b;triangles[t++]=a;
                triangles[t++]=a;triangles[t++]=b;triangles[t++]=segments+1;
            }
            mesh.vertices=vertices;
            mesh.triangles=triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh=mesh;
            meshes.Add(mesh);
        }

        void ApplyFlatWater(Transform world)
        {
            if(waterMaterial==null)waterMaterial=BuildWaterMaterial();
            MeshRenderer[] renderers=world.GetComponentsInChildren<MeshRenderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                MeshRenderer r=renderers[i];
                if(r==null||r.gameObject.name!="WaterPool")continue;
                r.sharedMaterial=waterMaterial;
            }
        }

        Material BuildWaterMaterial()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material m=new(shader){name="DF_Earth_Water_FlatUntextured"};
            Color blue=new(.02f,.25f,.62f,1f);
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",blue);
            if(m.HasProperty("_Color"))m.SetColor("_Color",blue);
            if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",null);
            if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",null);
            if(m.HasProperty("_BumpMap"))m.SetTexture("_BumpMap",null);
            if(m.HasProperty("_MetallicGlossMap"))m.SetTexture("_MetallicGlossMap",null);
            if(m.HasProperty("_OcclusionMap"))m.SetTexture("_OcclusionMap",null);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",.04f);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.90f);
            m.DisableKeyword("_NORMALMAP");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return m;
        }

        Material Mat(string name,Color color,float metallic,float smoothness)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material m=new(shader){name=name};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Color"))m.SetColor("_Color",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
            materials.Add(m);
            return m;
        }

        void ClearForest()
        {
            if(forestRoot!=null)Object.Destroy(forestRoot.gameObject);
            forestRoot=null;
            soilRoot=null;
            soilSites.Clear();
            soilDark=null;soilMid=null;soilLight=null;
            for(int i=0;i<meshes.Count;i++)if(meshes[i]!=null)Object.Destroy(meshes[i]);
            meshes.Clear();
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Object.Destroy(materials[i]);
            materials.Clear();
            if(waterMaterial!=null){Object.Destroy(waterMaterial);waterMaterial=null;}
        }

        void OnDestroy()=>ClearForest();
    }
}

