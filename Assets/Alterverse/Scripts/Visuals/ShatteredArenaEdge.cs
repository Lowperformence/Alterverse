using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Shared visual shoreline for every planet. No gameplay collision or navigation is changed.</summary>
    public static class ShatteredArenaEdge
    {
        // Outward-only radial deformation leaves the complete original play rectangle covered.
        public static Vector2 Warp(float x,float z)=>Warp(x,z,ArenaVariantRuntime.Current);
        public static Vector2 Warp(float x,float z,ArenaVariant variant)
        {
            Vector2 p=new(x,z);
            float radius=p.magnitude;
            if(radius<.001f)return p;
            float edge=Mathf.Max(Mathf.Abs(x)/42f,Mathf.Abs(z)/36f);
            float blend=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.88f,1f,edge));
            if(blend<=0f)return p;
            float angle=Mathf.Atan2(z,x)/(Mathf.PI*2f);
            if(angle<0f)angle+=1f;
            const int seed=19;
            float broad=Angular(angle,24,seed),chips=Angular(angle,80,seed+31);
            float slot=Mathf.Repeat(angle*14f+.17f,1f);
            float notch=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.065f,.20f,Mathf.Abs(slot-.5f)));
            float extension=.10f+(2.4f+broad*3f+chips*.7f)*notch;
            return p+p/radius*(extension*blend);
        }

        static float Angular(float angle,int count,int seed)
        {
            float position=angle*count;int index=Mathf.FloorToInt(position);
            return Mathf.Lerp(Hash(index%count,seed),Hash((index+1)%count,seed),position-index);
        }
        static float Hash(int index,int seed)
        {
            unchecked
            {
                uint n=(uint)(index*374761393+seed*668265263);
                n=(n^(n>>13))*1274126177u;
                return ((n^(n>>16))&0x00ffffff)/16777215f;
            }
        }

        public static Vector3[] Perimeter(Vector3[] grid,int xs,int zs)
        {
            var points=new List<Vector3>((xs+zs)*2);
            for(int x=0;x<xs;x++)points.Add(grid[x]);
            for(int z=0;z<zs;z++)points.Add(grid[z*(xs+1)+xs]);
            for(int x=xs;x>0;x--)points.Add(grid[zs*(xs+1)+x]);
            for(int z=zs;z>0;z--)points.Add(grid[z*(xs+1)]);
            return points.ToArray();
        }

        [System.Serializable]
        sealed class BorderProfile
        {
            public int version;
            public int sampleCount;
            public int ringCount;
            public float[] radialScale;
            public float[] depth;
        }
        static BorderProfile profile;
        public static void ValidateProfile(){LoadProfile();LoadDecor();}
        static BorderProfile LoadProfile()
        {
            if(profile!=null)return profile;
            var asset=Resources.Load<TextAsset>("PlanetSurfaces/UniversalBorder");
            if(asset==null)throw new System.InvalidOperationException("Missing Blender-authored universal border profile.");
            var data=JsonUtility.FromJson<BorderProfile>(asset.text);
            if(data==null||data.version!=1||data.sampleCount<4||data.ringCount<1||data.radialScale==null||data.depth==null||
                data.radialScale.Length!=data.sampleCount*data.ringCount||data.depth.Length!=data.radialScale.Length)
                throw new System.InvalidOperationException("Invalid universal border profile dimensions.");
            for(int ring=0;ring<data.ringCount;ring++)for(int i=0;i<data.sampleCount;i++)
            {
                int index=ring*data.sampleCount+i;
                if(float.IsNaN(data.radialScale[index])||float.IsInfinity(data.radialScale[index])||data.radialScale[index]<.5f||data.radialScale[index]>1.04f||
                    float.IsNaN(data.depth[index])||float.IsInfinity(data.depth[index])||
                    data.depth[index]>-.15f||(ring>0&&data.depth[index]>=data.depth[index-data.sampleCount]))
                    throw new System.InvalidOperationException("Invalid universal border profile geometry.");
            }
            return profile=data;
        }
        static float Sample(float[] values,int ring,int count,float angle)
        {
            float p=angle*count;int i=Mathf.FloorToInt(p);float blend=p-i;i%=count;
            return Mathf.Lerp(values[ring*count+i],values[ring*count+(i+1)%count],blend);
        }
        public static void Build(Transform parent,Vector3[] boundary,Material crust,Material middle,Material dark,List<Mesh> owned)
        {
            if(boundary==null||boundary.Length<4)return;
            var data=LoadProfile();Vector3[] upper=boundary;
            for(int layer=0;layer<data.ringCount;layer++)
            {
                var lower=new Vector3[boundary.Length];
                for(int i=0;i<lower.Length;i++)
                {
                    Vector3 p=boundary[i];float angle=Mathf.Atan2(p.z,p.x)/(Mathf.PI*2f);if(angle<0)angle+=1;
                    float scale=Sample(data.radialScale,layer,data.sampleCount,angle);
                    float depth=Sample(data.depth,layer,data.sampleCount,angle);
                    // Exact live upper boundary; terrain relief fades into the authored cliff depth.
                    lower[i]=new Vector3(p.x*scale,depth+p.y*Mathf.Clamp01(1f+depth/12f),p.z*scale);
                }
                var vertices=new List<Vector3>(boundary.Length*6);var triangles=new List<int>(boundary.Length*6);
                for(int i=0;i<upper.Length;i++)
                {
                    int j=(i+1)%upper.Length;
                    Face(vertices,triangles,upper[i],upper[j],lower[i]);
                    Face(vertices,triangles,upper[j],lower[j],lower[i]);
                }
                Emit(parent,"DF_UniversalPlanetCliff_"+layer,vertices,triangles,layer<2?crust:layer<4?middle:dark,owned);
                upper=lower;
            }
            var bottomV=new List<Vector3>();var bottomT=new List<int>();
            for(int i=0;i<upper.Length;i++)Face(bottomV,bottomT,new Vector3(0,-18f,0),upper[i],upper[(i+1)%upper.Length]);
            Emit(parent,"DF_UniversalPlanetUnderside",bottomV,bottomT,dark,owned);
            BuildDecor(parent,crust,dark,owned);
        }
        [System.Serializable] sealed class Crack { public float[] points;public float width; }
        [System.Serializable] sealed class Rock { public float x,z,size,rotation; }
        [System.Serializable] sealed class BorderDecor
        { public Crack[] cracks;public Rock[] rocks;public float[] rockVertices;public int[] rockTriangles; }
        static BorderDecor decor;
        static BorderDecor LoadDecor()
        {
            if(decor!=null)return decor;
            var asset=Resources.Load<TextAsset>("PlanetSurfaces/UniversalBorderDecor");
            if(asset==null)throw new System.InvalidOperationException("Missing authored border cracks and rocks.");
            var data=JsonUtility.FromJson<BorderDecor>(asset.text);
            if(data==null||data.cracks==null||data.rocks==null||data.rockVertices==null||data.rockVertices.Length%3!=0||data.rockTriangles==null||data.rockTriangles.Length%3!=0)
                throw new System.InvalidOperationException("Invalid authored border decoration.");
            return decor=data;
        }
        static Vector3 Floor(float x,float z)=>LunarSurfaceField.Surface(x,z)+Vector3.up*.045f;
        static void BuildDecor(Transform parent,Material stone,Material dark,List<Mesh> owned)
        {
            var data=LoadDecor();var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var crack in data.cracks)
            {
                if(crack.points==null||crack.points.Length<4||crack.points.Length%2!=0)continue;
                int count=crack.points.Length/2;
                for(int j=0;j<count-1;j++)
                {
                    Vector2 a=new(crack.points[j*2],crack.points[j*2+1]),b=new(crack.points[j*2+2],crack.points[j*2+3]);
                    Vector2 direction=(b-a).normalized;Vector2 side=new Vector2(-direction.y,direction.x)*crack.width;
                    float taper=Mathf.Lerp(1f,.16f,(float)j/(count-1));
                    Vector3 p=Floor(a.x+side.x*taper,a.y+side.y*taper),q=Floor(a.x-side.x*taper,a.y-side.y*taper);
                    Vector3 r=Floor(b.x+side.x*taper,b.y+side.y*taper),t=Floor(b.x-side.x*taper,b.y-side.y*taper);
                    Face(vertices,triangles,p,r,q);Face(vertices,triangles,q,r,t);
                }
            }
            Emit(parent,"DF_UniversalBorderGroundCracks",vertices,triangles,dark,owned);
            vertices=new List<Vector3>();triangles=new List<int>();
            foreach(var rock in data.rocks)
            {
                int start=vertices.Count;Vector3 center=Floor(rock.x,rock.z);Quaternion rotation=Quaternion.Euler(0,rock.rotation,0);
                for(int i=0;i<data.rockVertices.Length;i+=3)
                    vertices.Add(center+rotation*(new Vector3(data.rockVertices[i],data.rockVertices[i+1],data.rockVertices[i+2])*rock.size));
                for(int i=0;i<data.rockTriangles.Length;i++)triangles.Add(start+data.rockTriangles[i]);
            }
            Emit(parent,"DF_UniversalBorderAttachedRocks",vertices,triangles,stone,owned);
        }
        static void Face(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c)
        {int start=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(start);t.Add(start+1);t.Add(start+2);}
        static void Emit(Transform parent,string name,List<Vector3> v,List<int> t,Material material,List<Mesh> owned)
        {
            Mesh mesh=new(){name=name};if(v.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            GameObject go=new(name);go.transform.SetParent(parent,false);go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
        }
    }
}
