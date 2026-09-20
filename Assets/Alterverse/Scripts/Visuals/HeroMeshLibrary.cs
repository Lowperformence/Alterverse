using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Authored cross-sections, generated once, shared by every instance. No primitive GameObjects/colliders.
    internal static class HeroMeshLibrary
    {
        private static readonly Dictionary<string,Mesh> meshes=new();
        private static readonly Dictionary<(Color,int),Material> materials=new();
        private static readonly Dictionary<(Mesh,Color,bool),Mesh> painted=new();
        private static Material heroSurface;
        internal static Mesh Painted(Mesh mesh,Color color,bool energy)
        {
            var key=(mesh,color,energy);if(painted.TryGetValue(key,out Mesh result)&&result!=null)return result;
            result=UnityEngine.Object.Instantiate(mesh);result.name=mesh.name+"_Palette";
            Paint(result,color,energy);painted[key]=result;return result;
        }
        internal static void Paint(Mesh mesh,Color color,bool energy)
        {
            var colors=new Color[mesh.vertexCount];color.a=energy?1:0;
            for(int i=0;i<colors.Length;i++)colors[i]=color;mesh.colors=colors;
        }
        internal static Mesh Loft(string key,float[] y,float[] width,float[] depth,float[] forward=null,int sides=12)
        {
            if(meshes.TryGetValue(key,out Mesh cached)&&cached!=null)return cached;
            var v=new List<Vector3>();var t=new List<int>();
            Vector3 Point(int ring,int side)
            {
                float angle=(side+.5f)*Mathf.PI*2/sides;
                return new Vector3(Mathf.Cos(angle)*width[ring],y[ring],Mathf.Sin(angle)*depth[ring]+(forward==null?0:forward[ring]));
            }
            for(int ring=0;ring<y.Length-1;ring++)for(int s=0;s<sides;s++)
                Face(v,t,Point(ring,s),Point(ring+1,s),Point(ring+1,(s+1)%sides),Point(ring,(s+1)%sides));
            for(int cap=0;cap<2;cap++)
            {
                int ring=cap==0?0:y.Length-1;
                for(int s=1;s<sides-1;s++)
                    if(cap==0)Face(v,t,Point(ring,0),Point(ring,s),Point(ring,s+1));
                    else Face(v,t,Point(ring,0),Point(ring,s+1),Point(ring,s));
            }
            Mesh mesh=new Mesh{name=key};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            meshes[key]=mesh;return mesh;
        }
        internal static Mesh Ribbon(string key,Vector3[] centers,float[] widths,float thickness)
        {
            if(meshes.TryGetValue(key,out Mesh cached)&&cached!=null)return cached;
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<centers.Length-1;i++)
            {
                Vector3 a=centers[i],b=centers[i+1],left=Vector3.left,right=Vector3.right;
                Vector3 al=a+left*widths[i],ar=a+right*widths[i],bl=b+left*widths[i+1],br=b+right*widths[i+1];
                Vector3 z=Vector3.forward*thickness;
                Face(v,t,al,ar,br,bl);Face(v,t,bl-z,br-z,ar-z,al-z);
                Face(v,t,al-z,al,bl,bl-z);Face(v,t,ar,ar-z,br-z,br);
            }
            Mesh mesh=new Mesh{name=key};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            meshes[key]=mesh;return mesh;
        }
        internal static Mesh Ring(string key,float radius,float tube)
        {
            if(meshes.TryGetValue(key,out Mesh m)&&m!=null)return m;
            m=ProductionMeshFactory.Ring(radius,tube,24,5);m.name=key;meshes[key]=m;return m;
        }
        private static void Face(List<Vector3> v,List<int> t,params Vector3[] p)
        {int start=v.Count;v.AddRange(p);for(int i=1;i<p.Length-1;i++){t.Add(start);t.Add(start+i);t.Add(start+i+1);}}
        internal static Material Surface(Color color,int finish)
        {
            if(heroSurface==null)heroSurface=Resources.Load<Material>("HeroArt/HeroSurface");
            var key=(color,finish);if(materials.TryGetValue(key,out Material cached)&&cached!=null)return cached;
            if(heroSurface!=null)
            {
                // The custom hero shader reads color from vertex data, so only three shared finish
                // variants are needed. Previously every part returned the exact same material and
                // armor/fabric lost their visual separation.
                var sharedKey=(Color.clear,finish);
                if(materials.TryGetValue(sharedKey,out cached)&&cached!=null)return cached;
                Material variant=new Material(heroSurface){name="DF_HeroSurface_Finish"+finish,enableInstancing=true};
                if(variant.HasProperty("_SurfaceFinish"))variant.SetFloat("_SurfaceFinish",finish==1?1f:finish==2?.75f:0f);
                materials[sharedKey]=variant;
                return variant;
            }
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return null;
            Material m=new Material(shader){name="DF_SharedHeroSurface",color=color,enableInstancing=true};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            m.SetFloat("_Metallic",finish==1?.65f:0);m.SetFloat("_Smoothness",finish==1?.42f:finish==2?.5f:.16f);
            if(finish==2){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.85f);}
            materials[key]=m;return m;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach(var mesh in meshes.Values)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            foreach(var mat in materials.Values)if(mat!=null)UnityEngine.Object.Destroy(mat);
            foreach(var mesh in painted.Values)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            meshes.Clear();materials.Clear();painted.Clear();heroSurface=null;
        }
    }
}
