using System;
using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Authored Earth scenery, batched by region/material. Presentation only.</summary>
    [DisallowMultipleComponent]
    public sealed class EarthMapDressing : MonoBehaviour
    {
        [Serializable] public sealed class PartData
        {
            public int material;
            public float[] vertices, uv;
            public int[] triangles;
        }
        [Serializable] public sealed class AssetData { public string name; public PartData[] parts; }
        [Serializable] public sealed class MaterialData { public string name, finish; public float[] color; }
        [Serializable] public sealed class Placement { public string asset; public float x,z,yaw,scale; }
        [Serializable] public sealed class SurfaceData { public string name; public bool water; public PartData[] parts; }
        [Serializable] public sealed class DressingData
        {
            public int version;
            public MaterialData[] materials;
            public AssetData[] assets;
            public Placement[] placements;
            public SurfaceData[] surfaces;
        }

        sealed class AssetMesh { public Mesh[] parts; public int[] materials; public Bounds bounds; }
        sealed class Batch
        {
            public readonly List<Vector3> vertices=new();
            public readonly List<Vector2> uv=new();
            public readonly List<int> triangles=new();
        }
        readonly List<Mesh> ownedMeshes=new();
        readonly List<Material> ownedMaterials=new();
        readonly Dictionary<int,Batch> batches=new();
        Transform scenery;
        SceneryBuildingClearance clearance;

        public void Build()
        {
            if(scenery!=null||ArenaVariantRuntime.Current!=ArenaVariant.Earth)return;
            TextAsset source=Resources.Load<TextAsset>("EarthMap/Dressing");
            if(source==null)throw new InvalidOperationException("Earth scenery kit is missing.");
            DressingData data=JsonUtility.FromJson<DressingData>(source.text);
            if(data==null||data.version!=1||data.materials==null||data.assets==null||
                data.placements==null||data.surfaces==null)throw new InvalidOperationException("Invalid Earth scenery kit.");
            scenery=new GameObject("DF_EarthMapDressing").transform;
            scenery.SetParent(transform,false);
            clearance=new SceneryBuildingClearance();
            BuildMaterials(data.materials);
            Dictionary<string,AssetMesh> kit=BuildKit(data.assets);
            foreach(Placement site in data.placements)
            {
                AssetMesh asset=kit[site.asset];
                Matrix4x4 matrix=Matrix4x4.TRS(LunarSurfaceField.Surface(site.x,site.z),
                    Quaternion.Euler(0,site.yaw,0),Vector3.one*site.scale);
                Bounds bounds=SceneryBuildingClearance.TransformBounds(asset.bounds,matrix);
                // Protect the main combat corridor and the centre, including the full silhouette.
                if(bounds.min.z<7f&&bounds.max.z>-7f)continue;
                if(bounds.min.x<11f&&bounds.max.x>-11f&&bounds.min.z<11f&&bounds.max.z>-11f)continue;
                if(clearance.Blocks(SceneryBuildingClearance.TransformBounds(bounds,scenery.localToWorldMatrix)))continue;
                for(int part=0;part<asset.parts.Length;part++)
                    Append(asset.parts[part],asset.materials[part],matrix);
            }
            foreach(SurfaceData surface in data.surfaces)
                foreach(PartData part in surface.parts)AppendSurface(part,surface.water,surface.name.Contains("Bank"));
            Flush(data.materials.Length);
            // Baked batches are self-contained; discard library input geometry immediately.
            foreach(AssetMesh asset in kit.Values)
                foreach(Mesh mesh in asset.parts){ownedMeshes.Remove(mesh);Destroy(mesh);}
            batches.Clear();
        }

        void BuildMaterials(MaterialData[] palette)
        {
            foreach(MaterialData entry in palette)
            {
                Color color=new(entry.color[0],entry.color[1],entry.color[2],entry.color[3]);
                Material material=RuntimeShaderLibrary.Create(color,false,0f,entry.name=="Window"?.58f:.14f,Color.black);
                if(material==null)throw new InvalidOperationException("Earth scenery shader unavailable.");
                ownedMaterials.Add(material);
                material.name="DF_EarthAuthored_"+entry.name;
                if(entry.name=="Water")
                {
                    PlanetWaterMaterials.Apply(material);
                    if(material.HasProperty("_AuthoredBanks"))material.SetFloat("_AuthoredBanks",1f);
                    if(material.HasProperty("_ShallowColor"))material.SetColor("_ShallowColor",new Color(.16f,.30f,.25f,1));
                }
                else PlanetSceneryMaterials.Apply(material,ArenaVariant.Earth,(SceneryFinish)Enum.Parse(typeof(SceneryFinish),entry.finish));
            }
        }

        Dictionary<string,AssetMesh> BuildKit(AssetData[] assets)
        {
            Dictionary<string,AssetMesh> result=new();
            foreach(AssetData data in assets)
            {
                AssetMesh item=new(){parts=new Mesh[data.parts.Length],materials=new int[data.parts.Length]};
                for(int i=0;i<data.parts.Length;i++)
                {
                    PartData part=data.parts[i];
                    Mesh mesh=MeshFromData(data.name+"_"+part.material,part);
                    ownedMeshes.Add(mesh);item.parts[i]=mesh;item.materials[i]=part.material;
                    if(i==0)item.bounds=mesh.bounds;else item.bounds.Encapsulate(mesh.bounds);
                }
                result.Add(data.name,item);
            }
            return result;
        }

        static Mesh MeshFromData(string name,PartData data)
        {
            Vector3[] vertices=new Vector3[data.vertices.Length/3];
            Vector2[] uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=new Vector3(data.vertices[i*3],data.vertices[i*3+1],data.vertices[i*3+2]);
                uv[i]=new Vector2(data.uv[i*2],data.uv[i*2+1]);
            }
            Mesh mesh=new(){name="DF_EarthKit_"+name};
            if(vertices.Length>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=data.triangles;
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        void Append(Mesh mesh,int material,Matrix4x4 matrix)
        {
            Vector3[] vertices=mesh.vertices;Vector2[] uv=mesh.uv;int[] triangles=mesh.triangles;
            Vector3 center=matrix.MultiplyPoint3x4(mesh.bounds.center);
            Batch batch=GetBatch(center,material);
            int start=batch.vertices.Count;
            foreach(Vector3 vertex in vertices)batch.vertices.Add(matrix.MultiplyPoint3x4(vertex));
            batch.uv.AddRange(uv);
            foreach(int triangle in triangles)batch.triangles.Add(start+triangle);
        }

        void AppendSurface(PartData data,bool water,bool bank)
        {
            Vector3[] vertices=new Vector3[data.vertices.Length/3];
            Vector2[] uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                float x=data.vertices[i*3],z=data.vertices[i*3+2],lift=data.vertices[i*3+1];
                vertices[i]=LunarSurfaceField.Surface(x,z);
                uv[i]=new Vector2(data.uv[i*2],data.uv[i*2+1]);
                // Flat, calm water; soil and road strips conform to the unmodified rendered ground.
                if(water)vertices[i].y=lift;
                else if(bank)vertices[i].y=Mathf.Lerp(.051f,vertices[i].y+lift,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1f,uv[i].x)));
                else vertices[i].y+=lift;
            }
            for(int i=0;i<data.triangles.Length;i+=3)
            {
                int a=data.triangles[i],b=data.triangles[i+1],c=data.triangles[i+2];
                Bounds bounds=new(vertices[a],Vector3.zero);bounds.Encapsulate(vertices[b]);bounds.Encapsulate(vertices[c]);
                if(clearance.Blocks(SceneryBuildingClearance.TransformBounds(bounds,scenery.localToWorldMatrix)))continue;
                Batch batch=GetBatch(bounds.center,data.material);
                int start=batch.vertices.Count;
                foreach(int index in new[]{a,b,c}){batch.vertices.Add(vertices[index]);batch.uv.Add(uv[index]);}
                batch.triangles.Add(start);batch.triangles.Add(start+1);batch.triangles.Add(start+2);
            }
        }

        Batch GetBatch(Vector3 center,int material)
        {
            int x=Mathf.Clamp(Mathf.FloorToInt((center.x+42f)/21f),0,3);
            int z=Mathf.Clamp(Mathf.FloorToInt((center.z+36f)/24f),0,2);
            int key=(z*4+x)*ownedMaterials.Count+material;
            if(!batches.TryGetValue(key,out Batch batch)){batch=new Batch();batches.Add(key,batch);}
            return batch;
        }

        void Flush(int materialCount)
        {
            Dictionary<int,Transform> regions=new();
            foreach(KeyValuePair<int,Batch> entry in batches)
            {
                int region=entry.Key/materialCount,material=entry.Key%materialCount;
                if(!regions.TryGetValue(region,out Transform parent))
                {
                    parent=new GameObject("EarthRegion_"+region).transform;
                    parent.SetParent(scenery,false);regions.Add(region,parent);
                }
                Batch batch=entry.Value;
                Mesh mesh=new(){name="DF_EarthRegion_"+region+"_"+material};
                if(batch.vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(batch.vertices);mesh.SetUVs(0,batch.uv);mesh.SetTriangles(batch.triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
                GameObject go=new(mesh.name);go.transform.SetParent(parent,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=ownedMaterials[material];
                renderer.receiveShadows=true;
                renderer.shadowCastingMode=ownedMaterials[material].name.EndsWith("Water")?ShadowCastingMode.Off:ShadowCastingMode.On;
            }
        }

        void OnDestroy()
        {
            // Meshes/materials are owned by this theme instance, including interrupted builds.
            foreach(Mesh mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);
            foreach(Material material in ownedMaterials)if(material!=null)Destroy(material);
            ownedMeshes.Clear();ownedMaterials.Clear();batches.Clear();
        }
    }
}
