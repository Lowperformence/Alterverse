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
            int seed=(int)variant*73+19;
            float broad=Angular(angle,32,seed),chips=Angular(angle,112,seed+31);
            float extension=.45f+broad*2.25f+chips*.95f;
            if(variant==ArenaVariant.Kryon||variant==ArenaVariant.DiamondCore)extension*=1.12f;
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

        public static void Build(Transform parent,Vector3[] boundary,Material crust,Material middle,Material dark,List<Mesh> owned)
        {
            if(boundary==null||boundary.Length<4)return;
            Vector3[] upper=boundary;
            float[] depths={-.8f,-3.1f,-7.3f,-10.0f};
            float[] scales={1.014f,.973f,.855f,.69f};
            int seed=(int)ArenaVariantRuntime.Current*73+19;
            for(int layer=0;layer<depths.Length;layer++)
            {
                var lower=new Vector3[boundary.Length];
                for(int i=0;i<lower.Length;i++)
                {
                    Vector3 p=boundary[i];
                    // Angular cuts and differently recessed strata create ledges, chips and undercuts.
                    float a=Mathf.Atan2(p.z,p.x)/(Mathf.PI*2f);if(a<0)a+=1;
                    float cut=Angular(a,48,seed+layer*13);
                    float scale=scales[layer]+(cut-.5f)*.022f;
                    lower[i]=new Vector3(p.x*scale,depths[layer]-(cut-.5f)*(.75f+layer*.55f),p.z*scale);
                }
                var vertices=new List<Vector3>(boundary.Length*6);var triangles=new List<int>(boundary.Length*6);
                for(int i=0;i<upper.Length;i++)
                {
                    int j=(i+1)%upper.Length;
                    Face(vertices,triangles,upper[i],upper[j],lower[i]);
                    Face(vertices,triangles,upper[j],lower[j],lower[i]);
                }
                Emit(parent,"DF_ShatteredPlanetCliff_"+layer,vertices,triangles,layer==0?crust:layer==1?middle:dark,owned);
                upper=lower;
            }
            var bottomV=new List<Vector3>();var bottomT=new List<int>();
            for(int i=0;i<upper.Length;i++)Face(bottomV,bottomT,new Vector3(0,-11.5f,0),upper[i],upper[(i+1)%upper.Length]);
            Emit(parent,"DF_ShatteredPlanetUnderside",bottomV,bottomT,dark,owned);

            // Detached splinters are one batched mesh. All sit beyond the original play rectangle.
            var chipsV=new List<Vector3>();var chipsT=new List<int>();
            for(int i=3;i<boundary.Length;i+=9)
            {
                Vector3 p=boundary[i];Vector3 outward=new Vector3(p.x,0,p.z).normalized;
                Vector3 tangent=Vector3.Cross(Vector3.up,outward);
                float a=Hash(i,seed),b=Hash(i,seed+11);
                Vector3 center=p+outward*(1.9f+a*2.8f)+Vector3.down*(.45f+b*2.7f);
                float width=.30f+a*.85f,depth=.25f+b*.65f,height=.6f+Hash(i,seed+23)*1.9f;
                Vector3[] piece={center-tangent*width-outward*depth,center+tangent*width-outward*depth,
                    center+tangent*width*.6f+outward*depth+Vector3.up*.18f,
                    center-tangent*width*.8f+outward*depth,
                    center+outward*.15f-Vector3.up*height};
                WedgeFace(chipsV,chipsT,piece,0,1,2);WedgeFace(chipsV,chipsT,piece,0,2,3);
                WedgeFace(chipsV,chipsT,piece,0,4,1);WedgeFace(chipsV,chipsT,piece,1,4,2);
                WedgeFace(chipsV,chipsT,piece,2,4,3);WedgeFace(chipsV,chipsT,piece,3,4,0);
            }
            Emit(parent,"DF_ShatteredPlanetSplinters",chipsV,chipsT,crust,owned);
        }
        static void WedgeFace(List<Vector3> v,List<int> t,Vector3[] p,int a,int b,int c)
        {
            Vector3 center=(p[0]+p[1]+p[2]+p[3]+p[4])/5;
            if(Vector3.Dot(Vector3.Cross(p[b]-p[a],p[c]-p[a]),(p[a]+p[b]+p[c])/3-center)<0)
                Face(v,t,p[a],p[c],p[b]);
            else Face(v,t,p[a],p[b],p[c]);
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
