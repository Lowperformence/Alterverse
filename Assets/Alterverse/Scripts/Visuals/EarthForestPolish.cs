using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Earth tower soil dressing; authored woodland is owned by EarthMapDressing.
    /// Spawn cores retain the normal terrain.
    /// All generated dressing is collider-free and presentation-only.
    /// </summary>
    public sealed class EarthForestPolish : MonoBehaviour
    {
        const string ManagerName="DF_EarthForestPolish";
        const string ForestRootName="DF_EarthForestExtra";
        const string SoilRootName="DF_EarthBuildingSoil";

        static bool installed;
        Transform forestRoot;
        Transform soilRoot;
        readonly List<Material> materials=new();
        readonly HashSet<Vector2Int> soilSites=new();
        Material soilDark,soilMid,soilLight;
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

            if(soilRoot!=null&&Time.unscaledTime>=nextSoilRefresh)
            {
                nextSoilRefresh=Time.unscaledTime+.75f;
                RefreshBuildingSoil(world.transform);
            }
        }


        void BuildForest(Transform world)
        {
            ClearForest();
            GameObject root=new(ForestRootName);
            forestRoot=root.transform;
            forestRoot.SetParent(world,false);

            GameObject soil=new(SoilRootName);
            soilRoot=soil.transform;
            soilRoot.SetParent(forestRoot,false);

            // Strong, visibly brown earth. Three close tones are layered so the ground reads as
            // compacted/worn soil rather than one artificial-looking painted circle.
            soilDark=Mat("DF_Earth_Soil_Dark",new Color(.22f,.070f,.020f,1f),0f,.08f);
            soilMid=Mat("DF_Earth_Soil_Mid",new Color(.34f,.125f,.030f,1f),0f,.10f);
            soilLight=Mat("DF_Earth_Soil_Worn",new Color(.44f,.205f,.055f,1f),0f,.12f);

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

            // Both spawn cores keep the normal Earth terrain without a brown soil overlay.
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

        static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            Collider col=go.GetComponent<Collider>();
            if(col!=null){col.enabled=false;Object.Destroy(col);}
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null)renderer.sharedMaterial=material;
            return go;
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
            SceneryFinish finish=name.Contains("Bark")?SceneryFinish.Bark:
                name.Contains("Soil")?SceneryFinish.Soil:SceneryFinish.Leaf;
            PlanetSceneryMaterials.Apply(m,ArenaVariant.Earth,finish);
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
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Object.Destroy(materials[i]);
            materials.Clear();
        }

        void OnDestroy()=>ClearForest();
    }
}
