using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Places head ornaments in the existing body renderer and weights them to the head.</summary>
    public sealed class HeadAdornmentSkin : MonoBehaviour
    {
        Mesh ownedMesh;
        public static bool Merge(Transform model,Transform head,Mesh ornament,Material material)
        {
            SkinnedMeshRenderer skin=null;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(r.name=="DF_Anatomy"){skin=r;break;}
            if(skin==null||skin.sharedMesh==null||!skin.sharedMesh.isReadable)return false;
            Mesh source=skin.sharedMesh;
            int bone=Array.FindIndex(skin.bones,b=>b==head);
            if(bone<0||bone>=source.bindposes.Length)return false;
            int offset=source.vertexCount;
            var vertices=new List<Vector3>(source.vertices);var normals=new List<Vector3>(source.normals);
            var weights=new List<BoneWeight>(source.boneWeights);
            var colors=new List<Color>(source.colors);var uv=new List<Vector2>();source.GetUVs(0,uv);
            var finish=new List<Vector2>();source.GetUVs(1,finish);
            while(normals.Count<offset)normals.Add(Vector3.up);
            while(weights.Count<offset)weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});
            while(colors.Count<offset)colors.Add(Color.white);
            while(uv.Count<offset)uv.Add(Vector2.zero);
            while(finish.Count<offset)finish.Add(Vector2.zero);
            Matrix4x4 convert=source.bindposes[bone].inverse;
            Matrix4x4 normal=convert.inverse.transpose;
            Vector3[] p=ornament.vertices,n=ornament.normals;
            for(int i=0;i<p.Length;i++)
            {
                vertices.Add(convert.MultiplyPoint3x4(p[i]));normals.Add(normal.MultiplyVector(n[i]).normalized);
                weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});colors.Add(Color.white);
                uv.Add(Vector2.zero);finish.Add(Vector2.zero);
            }
            Mesh merged=Instantiate(source);merged.name=source.name+"_HeadAdornment";
            if(vertices.Count>65535)merged.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            merged.SetVertices(vertices);merged.SetNormals(normals);merged.SetColors(colors);
            merged.SetUVs(0,uv);merged.SetUVs(1,finish);merged.boneWeights=weights.ToArray();
            merged.subMeshCount=source.subMeshCount+1;
            int[] triangles=ornament.triangles;
            for(int i=0;i<triangles.Length;i++)triangles[i]+=offset;
            merged.SetTriangles(triangles,source.subMeshCount);merged.RecalculateBounds();
            var materials=new List<Material>(skin.sharedMaterials);
            while(materials.Count<source.subMeshCount)materials.Add(material);
            if(materials.Count>source.subMeshCount)materials.RemoveRange(source.subMeshCount,materials.Count-source.subMeshCount);
            materials.Add(material);skin.sharedMesh=merged;skin.sharedMaterials=materials.ToArray();
            Bounds bounds=skin.localBounds;bounds.Encapsulate(merged.bounds);skin.localBounds=bounds;
            model.gameObject.AddComponent<HeadAdornmentSkin>().ownedMesh=merged;
            return true;
        }
        void OnDestroy(){if(ownedMesh!=null)Destroy(ownedMesh);}
    }
}
