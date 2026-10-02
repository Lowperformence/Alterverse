using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Blender-authored concrete, access doors and service hardware for DefenseBunker.</summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredBunkerDetails : MonoBehaviour
    {
        [Serializable] sealed class Part { public string name,target,mount; public int material; public float[] vertices,normals,uv; public int[] triangles; }
        [Serializable] sealed class Library { public int version; public float roofPanelLift; public Part[] parts; public string[] hide; }
        static Library library;
        readonly List<Mesh> ownedMeshes=new();
        Material trim;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLibrary(){library=null;}

        public static void Apply(Transform root,Transform turret,Material concrete,Material lightConcrete,Material darkConcrete,Material metal,Material recess)
        {
            if(root==null||root.GetComponent<AuthoredBunkerDetails>()!=null)return;
            if(library==null)
            {
                TextAsset source=Resources.Load<TextAsset>("AuthoredBunker/BunkerDetails");if(source==null)return;
                library=JsonUtility.FromJson<Library>(source.text);
                if(library==null||library.version!=1||library.parts==null){library=null;return;}
            }
            root.gameObject.AddComponent<AuthoredBunkerDetails>().Build(root,turret,concrete,lightConcrete,darkConcrete,metal,recess);
        }

        void Build(Transform root,Transform turret,Material concrete,Material lightConcrete,Material darkConcrete,Material metal,Material recess)
        {
            var transforms=new Dictionary<string,Transform>();
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))transforms[child.name]=child;
            // Legacy panel tops were buried in the 2.17 m roof slab.
            foreach(var entry in transforms)
                if(entry.Key.StartsWith("DF_Bunker_RoofPlate_",StringComparison.Ordinal))
                    entry.Value.localPosition+=Vector3.up*library.roofPanelLift;
            trim=new Material(metal){name="DF_Bunker_AuthoredMetalTrim"};
            Color color=Color.Lerp(metal.color,lightConcrete.color,.2f);trim.color=color;if(trim.HasProperty("_BaseColor"))trim.SetColor("_BaseColor",color);
            Material[] materials={concrete,metal,recess,trim};
            ApplySurface(concrete,"Concrete");ApplySurface(lightConcrete,"Concrete");ApplySurface(darkConcrete,"Concrete");ApplySurface(metal,"Metal");ApplySurface(trim,"Metal");
            foreach(Part part in library.parts)
            {
                Mesh mesh=CreateMesh(part);ownedMeshes.Add(mesh);
                if(!string.IsNullOrEmpty(part.target))
                {
                    if(!transforms.TryGetValue(part.target,out Transform target))continue;
                    MeshFilter filter=target.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                    // Preserve the original local envelope and the original part transform.
                    Bounds old=filter.sharedMesh.bounds,authored=mesh.bounds;Vector3[] vertices=mesh.vertices;Vector3 size=authored.size;
                    for(int i=0;i<vertices.Length;i++)
                    {Vector3 p=vertices[i]-authored.center;vertices[i]=old.center+new Vector3(p.x*old.size.x/Mathf.Max(size.x,.0001f),p.y*old.size.y/Mathf.Max(size.y,.0001f),p.z*old.size.z/Mathf.Max(size.z,.0001f));}
                    mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();filter.sharedMesh=mesh;
                }
                else
                {
                    Transform parent=part.mount=="turret"?turret:root;if(parent==null)continue;
                    GameObject detail=new("DF_Bunker_Blender_"+part.name);detail.transform.SetParent(parent,false);
                    detail.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer renderer=detail.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[Mathf.Clamp(part.material,0,materials.Length-1)];renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                }
            }
            // Add planar UVs only where the original generated slabs had none.
            foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source=filter.sharedMesh;if(source==null||source.uv.Length==source.vertexCount)continue;
                Mesh mesh=Instantiate(source);mesh.name=source.name+"_BunkerSurfaceUV";Vector3[] vertices=mesh.vertices,normals=mesh.normals;var uv=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 p=vertices[i],n=i<normals.Length?normals[i]:Vector3.up;
                    uv[i]=Mathf.Abs(n.y)>Mathf.Abs(n.x)&&Mathf.Abs(n.y)>Mathf.Abs(n.z)?new Vector2(p.x,p.z):Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
                }
                mesh.uv=uv;mesh.RecalculateTangents();ownedMeshes.Add(mesh);filter.sharedMesh=mesh;
            }
            if(library.hide!=null)foreach(string name in library.hide)
                if(transforms.TryGetValue(name,out Transform target)&&target.TryGetComponent<MeshRenderer>(out var renderer))renderer.enabled=false;
        }

        static void ApplySurface(Material material,string name)
        {
            if(material==null)return;
            Texture2D albedo=Resources.Load<Texture2D>("AuthoredBunker/"+name+"Surface"),normal=Resources.Load<Texture2D>("AuthoredBunker/"+name+"Normal");
            if(albedo!=null&&material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",albedo);
            if(normal!=null&&material.HasProperty("_BumpMap")){material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.30f);material.EnableKeyword("_NORMALMAP");}
        }
        static Mesh CreateMesh(Part part)
        {
            int count=part.vertices.Length/3;var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
            for(int i=0;i<count;i++){vertices[i]=new Vector3(part.vertices[i*3],part.vertices[i*3+1],part.vertices[i*3+2]);normals[i]=new Vector3(part.normals[i*3],part.normals[i*3+1],part.normals[i*3+2]);uv[i]=new Vector2(part.uv[i*2],part.uv[i*2+1]);}
            Mesh mesh=new(){name="DF_Bunker_Blender_"+part.name};if(count>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=part.triangles;mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        void OnDestroy()
        {
            foreach(Mesh mesh in ownedMeshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
            if(trim!=null){if(Application.isPlaying)Destroy(trim);else DestroyImmediate(trim);}
        }
    }
}
