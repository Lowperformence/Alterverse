using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Blender-authored surfaces fitted inside the original rigid rig parts.
    /// Does not touch transforms, colliders, movement, animation pivots or combat.</summary>
    public sealed class AuthoredHotZoneArt : MonoBehaviour
    {
        [Serializable] sealed class Part { public string name; public float[] vertices,normals,uv; public int[] triangles; }
        [Serializable] sealed class Inset { public float inset; public float[] barycentric; }
        [Serializable] sealed class Library { public int version; public Part[] parts; public Inset surfaceInset; }
        static Library library;
        static readonly Dictionary<string,Part> parts=new();
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        readonly List<Material> sourceMaterials=new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){library=null;parts.Clear();}

        public static void Apply(Transform host,string family)
        {
            if(host==null||host.GetComponent<AuthoredHotZoneArt>()!=null)return;
            if(library==null)
            {
                TextAsset asset=Resources.Load<TextAsset>("AuthoredHotZone/Parts");
                if(asset==null)return;
                library=JsonUtility.FromJson<Library>(asset.text);
                if(library==null||library.version!=1||library.parts==null){library=null;return;}
                foreach(Part part in library.parts)parts[part.name]=part;
            }
            host.gameObject.AddComponent<AuthoredHotZoneArt>().Build(host,family);
        }

        void Build(Transform host,string family)
        {
            var meshCache=new Dictionary<Mesh,Mesh>();
            var materialCache=new Dictionary<Material,Material>();
            Texture2D surface=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Surface");
            Texture2D normal=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Normal");
            foreach(MeshFilter filter in host.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh original=filter.sharedMesh;MeshRenderer renderer=filter.GetComponent<MeshRenderer>();
                if(original==null||renderer==null||!renderer.enabled||filter.transform==host)continue;
                // Multi-material authored objects retain their existing submesh layout.
                if(original.subMeshCount==1)
                {
                    if(!meshCache.TryGetValue(original,out Mesh replacement))
                    {
                        string shape=Classify(original.name);
                        replacement=shape!=null&&parts.TryGetValue(family+"_"+shape,out Part part)
                            ?Fit(part,original.bounds):InsetOriginal(original);
                        meshes.Add(replacement);meshCache.Add(original,replacement);
                    }
                    filter.sharedMesh=replacement;
                }
                Material[] slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    Material originalMat=slots[i];if(originalMat==null)continue;
                    // Preserve clear crystal layers and pure energy effects.
                    if(originalMat.HasProperty("_Surface")&&originalMat.GetFloat("_Surface")>.5f)continue;
                    string name=originalMat.name.ToLowerInvariant();
                    if(name.Contains("energy")||name.Contains("glow")||name.Contains("core")||
                        (originalMat.HasProperty("_EmissionColor")&&originalMat.GetColor("_EmissionColor").maxColorComponent>.8f))continue;
                    if(!materialCache.TryGetValue(originalMat,out Material updated))
                    {
                        updated=new Material(originalMat){name=originalMat.name+"_BlenderSurface"};
                        if(surface!=null&&updated.HasProperty("_BaseMap")&&
                            (updated.GetTexture("_BaseMap")==null||updated.GetTexture("_BaseMap").name=="white"||updated.GetTexture("_BaseMap").name=="Default-White"))
                            updated.SetTexture("_BaseMap",surface);
                        if(normal!=null&&updated.HasProperty("_BumpMap")&&updated.GetTexture("_BumpMap")==null)
                        {updated.SetTexture("_BumpMap",normal);updated.SetFloat("_BumpScale",.32f);
                            if(updated.HasProperty("_NormalEncoding"))updated.SetFloat("_NormalEncoding",1f);
                            updated.EnableKeyword("_NORMALMAP");}
                        materialCache.Add(originalMat,updated);materials.Add(updated);sourceMaterials.Add(originalMat);
                    }
                    slots[i]=updated;
                }
                renderer.sharedMaterials=slots;
            }
        }

        public static void ConfigureSurface(Material material,string family)
        {
            if(material==null||string.IsNullOrEmpty(family))return;
            Texture2D surface=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Surface");
            Texture2D normal=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Normal");
            if(surface!=null&&material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",surface);
            if(normal!=null&&material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap",normal);
                if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",.32f);
                if(material.HasProperty("_NormalEncoding"))material.SetFloat("_NormalEncoding",1f);
                material.EnableKeyword("_NORMALMAP");
            }
        }

        static string Classify(string name)
        {
            if(name=="Cube"||name=="DF_SharedBeveledMachineBox")return "Box";
            if(name=="Sphere")return "Sphere";
            if(name=="Capsule")return "Capsule";
            if(name=="Cylinder")return "Cylinder";
            if(name.StartsWith("DF_FracturedLunarRock_",StringComparison.Ordinal))return "Rock";
            // Custom plates, teeth, rings and diamonds keep their authored outline.
            return null;
        }

        static Mesh Fit(Part part,Bounds original)
        {
            int count=part.vertices.Length/3;var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
            Vector3 size=original.size;
            for(int i=0;i<count;i++)
            {
                vertices[i]=original.center+Vector3.Scale(new Vector3(part.vertices[i*3],part.vertices[i*3+1],part.vertices[i*3+2]),size);
                normals[i]=new Vector3(part.normals[i*3]/Mathf.Max(size.x,.0001f),part.normals[i*3+1]/Mathf.Max(size.y,.0001f),part.normals[i*3+2]/Mathf.Max(size.z,.0001f)).normalized;
                uv[i]=new Vector2(part.uv[i*2],part.uv[i*2+1]);
            }
            Mesh mesh=new(){name="DF_BlenderHotZone_"+part.name,vertices=vertices,normals=normals,uv=uv,triangles=part.triangles};
            mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }

        static Mesh InsetOriginal(Mesh source)
        {
            // The Blender-authored inset fan keeps every original triangle edge and extremum.
            Vector3[] sourceVertices=source.vertices;int[] sourceTriangles=source.triangles;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();Bounds bounds=source.bounds;
            float depth=Mathf.Min(bounds.size.x,Mathf.Min(bounds.size.y,bounds.size.z))*library.surfaceInset.inset;
            float[] bary=library.surfaceInset.barycentric;
            for(int i=0;i<sourceTriangles.Length;i+=3)
            {
                Vector3 a=sourceVertices[sourceTriangles[i]],b=sourceVertices[sourceTriangles[i+1]],c=sourceVertices[sourceTriangles[i+2]];
                Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                Vector3 center=a*bary[0]+b*bary[1]+c*bary[2]-n*depth;
                center=new Vector3(Mathf.Clamp(center.x,bounds.min.x,bounds.max.x),Mathf.Clamp(center.y,bounds.min.y,bounds.max.y),Mathf.Clamp(center.z,bounds.min.z,bounds.max.z));
                foreach(Vector3[] face in new[]{new[]{a,b,center},new[]{b,c,center},new[]{c,a,center}})
                {
                    int first=vertices.Count;
                    foreach(Vector3 point in face)
                    {
                        vertices.Add(point);Vector3 p=point-bounds.min;
                        // Planar UVs follow the original face, including its inset fan.
                        if(Mathf.Abs(n.x)>Mathf.Abs(n.y)&&Mathf.Abs(n.x)>Mathf.Abs(n.z))uv.Add(new Vector2(p.z/Mathf.Max(bounds.size.z,.0001f),p.y/Mathf.Max(bounds.size.y,.0001f)));
                        else if(Mathf.Abs(n.y)>Mathf.Abs(n.z))uv.Add(new Vector2(p.x/Mathf.Max(bounds.size.x,.0001f),p.z/Mathf.Max(bounds.size.z,.0001f)));
                        else uv.Add(new Vector2(p.x/Mathf.Max(bounds.size.x,.0001f),p.y/Mathf.Max(bounds.size.y,.0001f)));
                    }
                    triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
                }
            }
            Mesh mesh=new(){name=source.name+"_BlenderInset"};
            if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }

        void LateUpdate()
        {
            // Original controllers still drive damage tint, wind-up and energy pulses.
            for(int i=0;i<materials.Count;i++)
            {
                Material source=sourceMaterials[i],target=materials[i];if(source==null||target==null)continue;
                if(source.HasProperty("_BaseColor"))target.SetColor("_BaseColor",source.GetColor("_BaseColor"));
                if(source.HasProperty("_Color"))target.SetColor("_Color",source.GetColor("_Color"));
                if(source.HasProperty("_EmissionColor"))target.SetColor("_EmissionColor",source.GetColor("_EmissionColor"));
            }
        }

        void OnDestroy()
        {
            foreach(Mesh mesh in meshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
            foreach(Material material in materials)if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        }
    }
}

