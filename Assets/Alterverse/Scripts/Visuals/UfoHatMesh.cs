using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Exact Blender geometry in normalized Unity head space: +Y up, +Z front.
    public static class UfoHatMesh
    {
        [Serializable] public sealed class Surface
        {public float r,g,b,metallic,smoothness,emission;}
        [Serializable] public sealed class Definition
        {public float[] vertices;public int[] triangles,submeshes;public Surface[] materials;}
        static Definition cached;

        public static bool TryBuild(Matrix4x4 toHead,Shader shader,out Mesh mesh,out Material[] materials,out string error)
        {
            mesh=null;materials=null;error=null;
            if(cached==null)
            {
                var resource=Resources.Load<TextAsset>("Headwear/UfoTestHat");
                if(resource==null){error="UFO Blender mesh resource missing.";return false;}
                Definition data;
                try{data=JsonUtility.FromJson<Definition>(resource.text);}
                catch(ArgumentException){error="UFO Blender mesh resource invalid.";return false;}
                if(!Valid(data)){error="UFO Blender mesh layout invalid.";return false;}
                cached=data;
            }
            if(shader==null){error="UFO material shader missing.";return false;}
            var vertices=new List<Vector3>(cached.vertices.Length/3);
            for(int i=0;i<cached.vertices.Length;i+=3)
                vertices.Add(toHead.MultiplyPoint3x4(new Vector3(cached.vertices[i],cached.vertices[i+1],cached.vertices[i+2])));
            var indices=new List<int>[cached.materials.Length];
            for(int i=0;i<indices.Length;i++)indices[i]=new List<int>();
            for(int i=0;i<cached.triangles.Length;i++)indices[cached.submeshes[i/3]].Add(cached.triangles[i]);
            mesh=new Mesh{name="DF_UfoTestHat_Blender",subMeshCount=indices.Length};mesh.SetVertices(vertices);
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            materials=new Material[cached.materials.Length];
            for(int i=0;i<materials.Length;i++)
            {
                var source=cached.materials[i];var color=new Color(source.r,source.g,source.b);
                var material=new Material(shader){name="DF_UfoTestHat_"+i};
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
                if(material.HasProperty("_Color"))material.SetColor("_Color",color);
                if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",source.metallic);
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",source.smoothness);
                if(source.emission>0&&material.HasProperty("_EmissionColor"))
                {material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(source.r*source.emission,source.g*source.emission,source.b*source.emission));}
                materials[i]=material;
            }
            return true;
        }
        static bool Valid(Definition data)
        {
            if(data?.vertices==null||data.triangles==null||data.submeshes==null||data.materials==null||
                data.vertices.Length==0||data.vertices.Length%3!=0||data.vertices.Length>18000||
                data.triangles.Length==0||data.triangles.Length%3!=0||data.triangles.Length>18000||
                data.submeshes.Length!=data.triangles.Length/3||data.materials.Length!=6)return false;
            foreach(float v in data.vertices)if(float.IsNaN(v)||float.IsInfinity(v)||Mathf.Abs(v)>10)return false;
            foreach(int index in data.triangles)if(index<0||index>=data.vertices.Length/3)return false;
            foreach(int section in data.submeshes)if(section<0||section>=data.materials.Length)return false;
            foreach(var material in data.materials)if(material==null)return false;
            return true;
        }
    }
}
