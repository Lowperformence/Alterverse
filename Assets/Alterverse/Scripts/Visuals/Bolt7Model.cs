using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    // Exact Blender geometry and local bone poses. No FBX unit conversion or root-motion sampling.
    internal sealed class Bolt7Model
    {
        [Serializable] private sealed class Bone {public string name;public int parent;public float[] pose;}
        [Serializable] private sealed class Submesh {public int[] triangles;}
        [Serializable] internal sealed class Clip
        {
            public string name;public int fps,frames;public float[] poses;
            public float length=>(frames-1)/(float)fps;
        }
        [Serializable] private sealed class Data
        {
            public int version;public Bone[] bones,sockets;public string[] materials;
            public float[] vertices,normals,uvs;public int[] boneIndices;public Submesh[] submeshes;public Clip[] clips;
        }
        private static Data data;
        private static Mesh mesh;
        private readonly Transform[] bones;
        internal GameObject Root {get;}
        internal Clip[] Clips=>data.clips;
        internal string[] MaterialNames=>data.materials;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=null;data=null;}
        private Bolt7Model(GameObject root,Transform[] bones){Root=root;this.bones=bones;}
        private static Vector3 V(float[] a,int i=0)=>new Vector3(a[i],a[i+1],a[i+2]);
        private static Quaternion Q(float[] a,int i=0)=>new Quaternion(a[i+3],a[i+4],a[i+5],a[i+6]);
        private static bool Finite(float[] a){if(a==null)return false;foreach(float f in a)if(float.IsNaN(f)||float.IsInfinity(f))return false;return true;}
        private static bool Valid(Data d)
        {
            if(d==null||d.version!=1||d.bones==null||d.bones.Length!=19||d.vertices==null||d.vertices.Length==0||d.vertices.Length%3!=0||d.vertices.Length>2000000||d.normals==null||d.normals.Length!=d.vertices.Length||d.uvs==null||d.uvs.Length!=d.vertices.Length/3*2||d.boneIndices==null||d.boneIndices.Length!=d.vertices.Length/3||d.materials==null||d.submeshes==null||d.materials.Length!=d.submeshes.Length||d.clips==null||d.clips.Length!=15||d.sockets==null)return false;
            if(!Finite(d.vertices)||!Finite(d.normals)||!Finite(d.uvs))return false;
            for(int i=0;i<d.bones.Length;i++){var b=d.bones[i];if(b==null||string.IsNullOrEmpty(b.name)||b.parent< -1||b.parent>=i||b.pose==null||b.pose.Length!=7||!Finite(b.pose))return false;}
            foreach(var b in d.sockets)if(b==null||b.parent< -1||b.parent>=d.bones.Length||b.pose==null||b.pose.Length!=7||!Finite(b.pose))return false;
            foreach(int b in d.boneIndices)if(b<0||b>=d.bones.Length)return false;
            foreach(var sub in d.submeshes){if(sub?.triangles==null||sub.triangles.Length%3!=0)return false;foreach(int i in sub.triangles)if(i<0||i>=d.boneIndices.Length)return false;}
            foreach(var clip in d.clips)if(clip==null||string.IsNullOrEmpty(clip.name)||clip.fps<=0||clip.frames<2||clip.poses==null||clip.poses.Length!=clip.frames*d.bones.Length*7||!Finite(clip.poses))return false;
            return true;
        }
        private static bool Load()
        {
            if(mesh!=null&&data!=null)return true;
            var source=Resources.Load<TextAsset>("Bolt7/Model");
            if(source==null){Debug.LogError("DF BOLT-7: missing Resources/Bolt7/Model.bytes. Pull the BOLT-7 runtime assets.");return false;}
            try
            {
                using(var zip=new GZipStream(new MemoryStream(source.bytes),CompressionMode.Decompress))
                using(var reader=new StreamReader(zip))data=JsonUtility.FromJson<Data>(reader.ReadToEnd());
                if(!Valid(data))throw new InvalidDataException("invalid mesh, skeleton or authored pose data");
                int count=data.boneIndices.Length;var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var weights=new BoneWeight[count];
                for(int i=0;i<count;i++){vertices[i]=V(data.vertices,i*3);normals[i]=V(data.normals,i*3);uv[i]=new Vector2(data.uvs[i*2],data.uvs[i*2+1]);weights[i]=new BoneWeight{boneIndex0=data.boneIndices[i],weight0=1f};}
                var rest=new Matrix4x4[data.bones.Length];var bind=new Matrix4x4[rest.Length];
                for(int i=0;i<rest.Length;i++){var b=data.bones[i];var local=Matrix4x4.TRS(V(b.pose),Q(b.pose),Vector3.one);rest[i]=b.parent>=0?rest[b.parent]*local:local;bind[i]=rest[i].inverse;}
                mesh=new Mesh{name="BOLT7_AuthoredRuntimeMesh",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.subMeshCount=data.submeshes.Length;
                for(int i=0;i<data.submeshes.Length;i++)mesh.SetTriangles(data.submeshes[i].triangles,i,false);
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                Debug.Log("DF BOLT-7: authored mesh ready, "+count+" vertices, 19 bones, 15 clips; explicit Unity coordinates.");return true;
            }
            catch(Exception error){Debug.LogError("DF BOLT-7 model: "+error.Message);if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=null;data=null;return false;}
        }
        private static Transform Part(Bone b,Transform parent,int layer)
        {
            var go=new GameObject(b.name);go.layer=layer;go.transform.SetParent(parent,false);go.transform.localPosition=V(b.pose);go.transform.localRotation=Q(b.pose);go.transform.localScale=Vector3.one;return go.transform;
        }
        internal static Bolt7Model Create(Transform parent)
        {
            if(!Load())return null;
            var root=new GameObject("BOLT7_AuthoredModel");root.layer=parent.gameObject.layer;root.transform.SetParent(parent,false);
            var bones=new Transform[data.bones.Length];
            for(int i=0;i<bones.Length;i++){var b=data.bones[i];bones[i]=Part(b,b.parent>=0?bones[b.parent]:root.transform,root.layer);}
            foreach(var b in data.sockets)Part(b,b.parent>=0?bones[b.parent]:root.transform,root.layer);
            var body=new GameObject("BOLT7_LOD0");body.layer=root.layer;body.transform.SetParent(root.transform,false);
            var skin=body.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=bones[0];skin.quality=SkinQuality.Bone1;skin.updateWhenOffscreen=true;
            skin.localBounds=new Bounds(new Vector3(0,1.5f,0),Vector3.one*6f);
            skin.sharedMaterials=new Material[data.materials.Length];
            return new Bolt7Model(root,bones);
        }
        internal void Sample(Clip clip,float seconds)
        {
            float frame=Mathf.Clamp(seconds*clip.fps,0,clip.frames-1);int a=Mathf.FloorToInt(frame),b=Mathf.Min(a+1,clip.frames-1);float t=frame-a;
            for(int i=0;i<bones.Length;i++)
            {
                int from=(a*bones.Length+i)*7,to=(b*bones.Length+i)*7;
                bones[i].localPosition=Vector3.Lerp(V(clip.poses,from),V(clip.poses,to),t);
                bones[i].localRotation=Quaternion.Slerp(Q(clip.poses,from),Q(clip.poses,to),t);
            }
        }
    }
}
