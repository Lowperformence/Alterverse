using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Shared visual concrete plinths; never edits terrain textures or gameplay colliders.</summary>
    public sealed class UniversalBuildingFoundation : MonoBehaviour
    {
        Transform world,root;
        Material concrete;
        readonly List<Mesh> meshes=new();
        readonly HashSet<Transform> built=new();
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Object.FindFirstObjectByType<UniversalBuildingFoundation>()!=null)return;
            var host=new GameObject("DF_UniversalBuildingFoundations");
            DontDestroyOnLoad(host);
            host.AddComponent<UniversalBuildingFoundation>();
        }

        void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.5f;
            var match=GameObject.Find("DF_MatchWorld");
            if(match==null){if(root!=null||concrete!=null)Clear();return;}
            if(world!=match.transform||root==null)
            {
                Clear();world=match.transform;
                root=new GameObject("DF_BuildingFoundations").transform;
                root.SetParent(world,false);
                concrete=RuntimeShaderLibrary.Create(new Color(.19f,.205f,.22f,1),false,.06f,.18f);
                if(concrete!=null)
                {
                    concrete.name="DF_UniversalDarkConcrete";
                    PlanetSceneryMaterials.Apply(concrete,Alterverse.Match.ArenaVariantRuntime.Current,SceneryFinish.Concrete);
                }
            }
            foreach(var bunker in world.GetComponentsInChildren<DefenseBunker>())Build(bunker.transform);
            foreach(var trench in world.GetComponentsInChildren<DefenseTrench>())Build(trench.transform);
            foreach(string name in new[]{"DF_ZeroRealityCore","DF_LumenRealityCore"})
            {
                var core=GameObject.Find(name);
                if(core!=null&&core.transform.IsChildOf(world))Build(core.transform);
            }
        }

        void Build(Transform building)
        {
            if(concrete==null||built.Contains(building))return;
            var renderers=building.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0)return;
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            // 40 cm exposed lip fits inside the scenery's 2 m clear apron.
            float hx=bounds.extents.x+.4f,hz=bounds.extents.z+.4f;
            float bevel=Mathf.Min(.35f,Mathf.Min(hx,hz)*.15f);
            Vector2[] ring={new(-hx+bevel,-hz),new(hx-bevel,-hz),new(hx,-hz+bevel),new(hx,hz-bevel),
                new(hx-bevel,hz),new(-hx+bevel,hz),new(-hx,hz-bevel),new(-hx,-hz+bevel)};
            float top=building.position.y+.015f;
            float bottom=top-.65f;
            foreach(var p in ring)bottom=Mathf.Min(bottom,LunarSurfaceField.Surface(bounds.center.x+p.x,bounds.center.z+p.y).y-.25f);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<ring.Length;i++)
            {
                Vector2 a=ring[i],b=ring[(i+1)%ring.Length];
                // Keep the exposed lip visible above uneven terrain without changing terrain textures.
                float ay=Mathf.Max(top,LunarSurfaceField.Surface(bounds.center.x+a.x,bounds.center.z+a.y).y+.035f);
                float by=Mathf.Max(top,LunarSurfaceField.Surface(bounds.center.x+b.x,bounds.center.z+b.y).y+.035f);
                Vector3 at=new(a.x,ay,a.y),bt=new(b.x,by,b.y),ab=new(a.x,bottom,a.y),bb=new(b.x,bottom,b.y);
                Face(new Vector3(0,top,0),bt,at);
                Face(at,bt,bb);Face(at,bb,ab);
                Face(new Vector3(0,bottom,0),ab,bb);
            }
            Mesh mesh=new(){name="DF_BeveledConcreteFoundation"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var go=new GameObject("DF_Foundation_"+building.name);go.transform.SetParent(root,false);
            go.transform.position=new Vector3(bounds.center.x,0,bounds.center.z);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=concrete;
            built.Add(building);
            void Face(Vector3 a,Vector3 b,Vector3 c){int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);}
        }

        void Clear()
        {
            if(root!=null)Destroy(root.gameObject);
            if(concrete!=null)Destroy(concrete);
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            meshes.Clear();built.Clear();root=null;world=null;concrete=null;
        }
        void OnDestroy()=>Clear();
    }
}
