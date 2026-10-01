using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Curved hero surfaces with shared normals, authored silhouettes and no extra colliders.</summary>
    internal static class HeroSculptGeometry
    {
        internal static Mesh RoundedBox()
        {
            const int steps=6;
            const float radius=.14f,inner=.5f-radius;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3,start=vertices.Count;
                for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {
                    Vector3 p=Vector3.zero;p[axis]=sign*.5f;p[u]=x/(float)steps-.5f;p[v]=y/(float)steps-.5f;
                    Vector3 core=new(Mathf.Clamp(p.x,-inner,inner),Mathf.Clamp(p.y,-inner,inner),Mathf.Clamp(p.z,-inner,inner));
                    Vector3 normal=(p-core).normalized;vertices.Add(core+normal*radius);normals.Add(normal);
                }
                for(int y=0;y<steps;y++)for(int x=0;x<steps;x++)
                {
                    int a=start+y*(steps+1)+x,b=a+1,c=a+steps+1,d=c+1;
                    Triangle(vertices,triangles,a,b,d);Triangle(vertices,triangles,a,d,c);
                }
            }
            return Mesh("DF_HeroRoundedArmor",vertices,triangles,normals);
        }

        internal static Mesh ArmorPanel(string name,Vector2[] outline,float depth)
        {
            var contour=new List<Vector2>(outline);
            float area=0;
            for(int i=0;i<contour.Count;i++){Vector2 a=contour[i],b=contour[(i+1)%contour.Count];area+=a.x*b.y-b.x*a.y;}
            if(area<0)contour.Reverse();
            // Two corner-cutting passes retain each authored plate's shape while rounding its rim.
            for(int pass=0;pass<2;pass++)
            {
                var next=new List<Vector2>(contour.Count*2);
                for(int i=0;i<contour.Count;i++)
                {
                    Vector2 a=contour[i],b=contour[(i+1)%contour.Count];
                    next.Add(Vector2.Lerp(a,b,.16f));next.Add(Vector2.Lerp(a,b,.84f));
                }
                contour=next;
            }
            Vector2 center=Vector2.zero;foreach(Vector2 p in contour)center+=p;center/=contour.Count;
            float bulge=Mathf.Min(depth*.28f,.035f);
            float[] scales={.12f,.62f,.88f,1f,1f,.88f,.12f};
            float[] heights={depth*.5f+bulge,depth*.5f+bulge*.65f,depth*.5f,depth*.28f,-depth*.28f,-depth*.5f,-depth*.5f};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            int count=contour.Count;
            for(int ring=0;ring<scales.Length;ring++)foreach(Vector2 p in contour)
            {
                Vector2 q=Vector2.Lerp(center,p,scales[ring]);vertices.Add(new Vector3(q.x,q.y,heights[ring]));
            }
            for(int ring=0;ring<scales.Length-1;ring++)for(int i=0;i<count;i++)
            {
                int next=(i+1)%count,a=ring*count+i,b=ring*count+next,c=(ring+1)*count+i,d=(ring+1)*count+next;
                triangles.Add(a);triangles.Add(c);triangles.Add(d);triangles.Add(a);triangles.Add(d);triangles.Add(b);
            }
            int front=vertices.Count;vertices.Add(new Vector3(center.x,center.y,heights[0]));
            int back=vertices.Count;vertices.Add(new Vector3(center.x,center.y,-depth*.5f));
            for(int i=0;i<count;i++)
            {
                int next=(i+1)%count;triangles.Add(front);triangles.Add(i);triangles.Add(next);
                triangles.Add(back);triangles.Add((scales.Length-1)*count+next);triangles.Add((scales.Length-1)*count+i);
            }
            return Mesh("DF_CurvedContour_"+name,vertices,triangles);
        }

        internal static Mesh DrapedRibbon(string name,Vector3[] centers,float[] widths,float thickness)
        {
            const int subdivisions=4,across=6;
            int rows=(centers.Length-1)*subdivisions+1,cols=across+1;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int face=0;face<2;face++)for(int row=0;row<rows;row++)
            {
                int section=Mathf.Min(row/subdivisions,centers.Length-2);
                float t=(row-section*subdivisions)/(float)subdivisions;
                Vector3 p=Curve(centers[Mathf.Max(0,section-1)],centers[section],centers[section+1],centers[Mathf.Min(centers.Length-1,section+2)],t);
                float width=Mathf.Lerp(widths[section],widths[section+1],t);
                for(int x=0;x<=across;x++)
                {
                    float u=x/(float)across*2f-1f;
                    float fold=Mathf.Sin(u*Mathf.PI*2.0f+row*.16f)*width*.055f+(1f-u*u)*width*.10f;
                    vertices.Add(p+new Vector3(u*width,0,fold+(face==0?0:-thickness)));
                }
            }
            int faceCount=rows*cols;
            for(int face=0;face<2;face++)for(int row=0;row<rows-1;row++)for(int x=0;x<across;x++)
            {
                int a=face*faceCount+row*cols+x,b=a+1,c=a+cols,d=c+1;
                if(face==0){triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);}
                else{triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);}
            }
            void Bridge(int a,int b)
            {
                triangles.Add(a);triangles.Add(b);triangles.Add(b+faceCount);
                triangles.Add(a);triangles.Add(b+faceCount);triangles.Add(a+faceCount);
            }
            for(int x=0;x<across;x++){Bridge(x,x+1);Bridge((rows-1)*cols+x+1,(rows-1)*cols+x);}
            for(int row=0;row<rows-1;row++){Bridge((row+1)*cols,row*cols);Bridge(row*cols+across,(row+1)*cols+across);}
            return Mesh(name+"_Draped",vertices,triangles);
        }

        static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
            =>.5f*((2f*b)+(-a+c)*t+(2f*a-5f*b+4f*c-d)*t*t+(-a+3f*b-3f*c+d)*t*t*t);
        static void Triangle(List<Vector3> vertices,List<int> triangles,int a,int b,int c)
        {
            if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),vertices[a]+vertices[b]+vertices[c])<0)(b,c)=(c,b);
            triangles.Add(a);triangles.Add(b);triangles.Add(c);
        }
        static Mesh Mesh(string name,List<Vector3> vertices,List<int> triangles,List<Vector3> normals=null)
        {
            var mesh=new UnityEngine.Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            if(normals==null)mesh.RecalculateNormals();else mesh.SetNormals(normals);
            mesh.RecalculateBounds();return mesh;
        }
    }
}
