using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // The lower crown band is the head-seat plane (y=0); +Z faces forward.
    public static class BetaCapMesh
    {
        public static Mesh Build(Matrix4x4 toHead)
        {
            var vertices=new List<Vector3>();
            var indices=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
            {
                int n=vertices.Count;
                vertices.Add(toHead.MultiplyPoint3x4(a));vertices.Add(toHead.MultiplyPoint3x4(b));
                vertices.Add(toHead.MultiplyPoint3x4(c));vertices.Add(toHead.MultiplyPoint3x4(d));
                indices[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,int material)
            {
                int n=vertices.Count;vertices.Add(toHead.MultiplyPoint3x4(a));
                vertices.Add(toHead.MultiplyPoint3x4(b));vertices.Add(toHead.MultiplyPoint3x4(c));
                indices[material].AddRange(new[]{n,n+1,n+2});
            }
            float Radius(float t)=>1f-.08f*t-.92f*t*t*t*t;
            Vector3 Crown(float a,float t,float extra=0f)=>new Vector3(
                Mathf.Sin(a)*(Radius(t)+extra),t*1.05f,Mathf.Cos(a)*(Radius(t)+extra)*.94f);
            const int sides=36,rows=8;
            float turn=Mathf.PI*2;
            for(int side=0;side<sides;side++)
            {
                float a=side*turn/sides,b=(side+1)*turn/sides;
                bool front=side<6||side>=sides-6;
                for(int row=0;row<rows;row++)
                {
                    float low=row/(float)rows*.94f,high=(row+1)/(float)rows*.94f;
                    if(front)
                        Quad(Crown(a,low),Crown(b,low),Crown(b,high),Crown(a,high),2);
                    else
                    {
                        // Real openings in the trucker mesh, with an inner surface on each thread.
                        float rib=(b-a)*.20f,thread=(high-low)*.20f;
                        Quad(Crown(a,low),Crown(a+rib,low),Crown(a+rib,high),Crown(a,high),0);
                        Quad(Crown(a+rib,low),Crown(b,low),Crown(b,low+thread),Crown(a+rib,low+thread),0);
                        Quad(Crown(a,low,-.018f),Crown(a,high,-.018f),Crown(a+rib,high,-.018f),Crown(a+rib,low,-.018f),0);
                        Quad(Crown(a+rib,low,-.018f),Crown(a+rib,low+thread,-.018f),Crown(b,low+thread,-.018f),Crown(b,low,-.018f),0);
                    }
                }
                Triangle(Crown(a,.94f),Crown(b,.94f),new Vector3(0,1.05f,0),0);
                Quad(Crown(a,0,.01f),Crown(b,0,.01f),Crown(b,.06f,.01f),Crown(a,.06f,.01f),0);
                Quad(Crown(b,0,-.025f),Crown(a,0,-.025f),Crown(a,.06f,-.025f),Crown(b,.06f,-.025f),0);
                Quad(Crown(a,0,-.025f),Crown(a,0,.01f),Crown(b,0,.01f),Crown(b,0,-.025f),0);
            }
            foreach(float a in new[]{Mathf.PI/3,turn-Mathf.PI/3})
                for(int row=0;row<8;row++)
                    Quad(Crown(a-.012f,row*.115f,.012f),Crown(a+.012f,row*.115f,.012f),
                         Crown(a+.012f,(row+1)*.115f,.012f),Crown(a-.012f,(row+1)*.115f,.012f),1);
            Vector3 Bill(float angle,float t,float down=0)=>new Vector3(
                Mathf.Sin(angle)*(1f+t*.04f),-t*.10f-.055f*Mathf.Sin(angle)*Mathf.Sin(angle)*t-down,
                Mathf.Cos(angle)*(.94f+t*.84f));
            const int billSides=24;
            for(int side=0;side<billSides;side++)
            {
                float a=-Mathf.PI/3+side*(Mathf.PI*2/3)/billSides,b=a+(Mathf.PI*2/3)/billSides;
                Quad(Bill(a,0),Bill(a,1),Bill(b,1),Bill(b,0),0);
                Quad(Bill(b,0,.035f),Bill(b,1,.035f),Bill(a,1,.035f),Bill(a,0,.035f),0);
                Quad(Bill(a,1),Bill(a,1,.035f),Bill(b,1,.035f),Bill(b,1),1);
                Quad(Bill(a,.85f,-.002f),Bill(a,.87f,-.002f),Bill(b,.87f,-.002f),Bill(b,.85f,-.002f),1);
            }
            foreach(float angle in new[]{-Mathf.PI/3,Mathf.PI/3})
                Quad(Bill(angle,0),Bill(angle,0,.035f),Bill(angle,1,.035f),Bill(angle,1),0);
            Vector3 Front(float x,float y,float lift)=>new Vector3(x,y,
                Mathf.Sqrt(Mathf.Max(0,Radius(y/1.05f)*Radius(y/1.05f)-x*x))*.94f+lift);
            // Curved fabric patch follows the front panel rather than floating ahead of it.
            for(int row=0;row<4;row++)for(int col=0;col<10;col++)
            {
                float x=-.53f+col*.106f,y=.30f+row*.105f;
                Quad(Front(x,y,.009f),Front(x+.106f,y,.009f),
                     Front(x+.106f,y+.105f,.009f),Front(x,y+.105f,.009f),0);
            }
            string[] glyphs={"11110100011000111110100011000111110","11111100001000011110100001000011111",
                "11111001000010000100001000010000100","01110100011000111111100011000110001"};
            const float pixel=.039f;
            for(int letter=0;letter<4;letter++)for(int row=0;row<7;row++)for(int col=0;col<5;col++)
            {
                if(glyphs[letter][row*5+col]!='1')continue;
                float x=-23*pixel*.5f+(letter*6+col)*pixel,y=.66f-(row+1)*pixel;
                Quad(Front(x,y,.019f),Front(x+pixel*.82f,y,.019f),
                    Front(x+pixel*.82f,y+pixel*.82f,.019f),Front(x,y+pixel*.82f,.019f),2);
            }
            var mesh=new Mesh{name="DF_BetaTruckerCap",subMeshCount=3};
            mesh.SetVertices(vertices);
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }
}

