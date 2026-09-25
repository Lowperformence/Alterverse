using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
    public static class LunarFragmentGeometry
    {
        // Broad fracture planes and a chipped shoulder, closed on every side. No Unity primitive mesh.
        public static Mesh Create(int seed)
        {
            const int sides=11;Vector3[] rings=new Vector3[sides*4];
            for(int ring=0;ring<4;ring++)for(int i=0;i<sides;i++)
            {
                float a=(i+.075f*Mathf.Sin(i*7+seed))*Mathf.PI*2/sides;
                float radius=(ring==0?.73f:ring==1?1.02f:ring==2?.78f:.39f)*(1+.19f*Mathf.Sin(i*13.4f+seed)+.09f*Mathf.Sin(i*5.1f+seed*2.3f));
                rings[ring*sides+i]=new Vector3(Mathf.Cos(a)*radius+(ring>=2?.13f:0),
                    ring==0?-.38f:ring==1?.03f+.13f*Mathf.Sin(i*3+seed):
                    ring==2?.39f+.16f*Mathf.Sin(i*2+seed):.78f+.10f*Mathf.Sin(i*4+seed),
                    Mathf.Sin(a)*radius);
            }
            var vertices=new List<Vector3>();var indices=new List<int>();
            for(int ring=0;ring<3;ring++)for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides,a=ring*sides+i,b=ring*sides+j,c=a+sides,d=b+sides;
                Face(vertices,indices,rings[a],rings[c],rings[d]);Face(vertices,indices,rings[a],rings[d],rings[b]);
            }
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;
                Face(vertices,indices,new Vector3(0,-.38f,0),rings[i],rings[j]);
                Face(vertices,indices,new Vector3(.13f,.81f,0),rings[3*sides+j],rings[3*sides+i]);
            }
            Mesh mesh=new(){name="DF_FracturedLunarRock_"+seed};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Face(List<Vector3> vertices,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(i);indices.Add(i+1);indices.Add(i+2);}
    }
}
