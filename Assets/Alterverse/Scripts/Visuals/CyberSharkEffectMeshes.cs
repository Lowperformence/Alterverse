using System;
using UnityEngine;

namespace Alterverse.Visuals
{
    internal static class CyberSharkEffectMeshes
    {
        [Serializable] private sealed class Data
        {
            public float[] vertices,alpha;
            public int[] triangles;
        }
        internal static Mesh Load(string name)
        {
            TextAsset asset=Resources.Load<TextAsset>("CyberShark/"+name);
            if(asset==null)throw new InvalidOperationException("Missing Blender effect mesh: "+name);
            Data data=JsonUtility.FromJson<Data>(asset.text);
            if(data==null||data.vertices==null||data.vertices.Length%3!=0||data.alpha==null||
                data.alpha.Length!=data.vertices.Length/3||data.triangles==null||data.triangles.Length%3!=0)
                throw new InvalidOperationException("Invalid Blender effect mesh: "+name);
            var vertices=new Vector3[data.alpha.Length];var colors=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=new Vector3(data.vertices[i*3],data.vertices[i*3+1],data.vertices[i*3+2]);
                colors[i]=new Color(1,1,1,Mathf.Clamp01(data.alpha[i]));
            }
            foreach(int index in data.triangles)
                if(index<0||index>=vertices.Length)throw new InvalidOperationException("Invalid effect index: "+name);
            var mesh=new Mesh{name="DF_BlenderFx_"+name};
            mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=data.triangles;
            mesh.RecalculateBounds();return mesh;
        }
    }
}
