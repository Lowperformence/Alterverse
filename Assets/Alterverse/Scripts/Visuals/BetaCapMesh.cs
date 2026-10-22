using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Small, real 3D geometry. The BETA lettering needs no font or downloaded texture.
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
            Vector3 Crown(float angle,float elevation,float radius=1f) =>
                new Vector3(Mathf.Cos(angle)*Mathf.Cos(elevation)*radius,
                    Mathf.Sin(elevation)*.78f,Mathf.Sin(angle)*Mathf.Cos(elevation)*.95f*radius);
            const int sides=24,rings=6;
            for(int row=0;row<rings;row++)for(int side=0;side<sides;side++)
            {
                float a=side*Mathf.PI*2/sides,b=(side+1)*Mathf.PI*2/sides;
                float low=row*Mathf.PI*.5f/(rings+1),high=(row+1)*Mathf.PI*.5f/(rings+1);
                Quad(Crown(a,low),Crown(a,high),Crown(b,high),Crown(b,low),0);
            }
            for(int side=0;side<sides;side++)
            {
                float a=side*Mathf.PI*2/sides,b=(side+1)*Mathf.PI*2/sides;
                int n=vertices.Count;
                vertices.Add(toHead.MultiplyPoint3x4(Crown(a,rings*Mathf.PI*.5f/(rings+1))));
                vertices.Add(toHead.MultiplyPoint3x4(new Vector3(0,.78f,0)));
                vertices.Add(toHead.MultiplyPoint3x4(Crown(b,rings*Mathf.PI*.5f/(rings+1))));
                indices[0].AddRange(new[]{n,n+1,n+2});
            }
            // Thin fitted lower band and six stitched panel seams.
            for(int side=0;side<sides;side++)
            {
                float a=side*Mathf.PI*2/sides,b=(side+1)*Mathf.PI*2/sides;
                Quad(Crown(a,.018f,1.012f),Crown(a,.045f,1.012f),
                    Crown(b,.045f,1.012f),Crown(b,.018f,1.012f),1);
            }
            for(int seam=0;seam<6;seam++)for(int row=0;row<5;row++)
            {
                float angle=seam*Mathf.PI/3,low=.08f+row*.25f,high=low+.17f;
                Quad(Crown(angle-.004f,low,1.009f),Crown(angle-.004f,high,1.009f),
                    Crown(angle+.004f,high,1.009f),Crown(angle+.004f,low,1.009f),1);
            }
            Vector3 Bill(float angle,float t,float down=0) => new Vector3(
                Mathf.Sin(angle)*(1.01f+t*.18f),-.025f-t*.09f-down,
                Mathf.Cos(angle)*(.88f+t*.85f));
            const int billSides=16;
            for(int side=0;side<billSides;side++)
            {
                float a=-Mathf.PI*.5f+side*Mathf.PI/billSides,b=a+Mathf.PI/billSides;
                Quad(Bill(a,0),Bill(a,1),Bill(b,1),Bill(b,0),0);
                Quad(Bill(b,0,.035f),Bill(b,1,.035f),Bill(a,1,.035f),Bill(a,0,.035f),0);
                Quad(Bill(a,1),Bill(a,1,.035f),Bill(b,1,.035f),Bill(b,1),1);
            }
            Quad(Bill(-Mathf.PI*.5f,0),Bill(-Mathf.PI*.5f,0,.035f),Bill(-Mathf.PI*.5f,1,.035f),Bill(-Mathf.PI*.5f,1),0);
            Quad(Bill(Mathf.PI*.5f,1),Bill(Mathf.PI*.5f,1,.035f),Bill(Mathf.PI*.5f,0,.035f),Bill(Mathf.PI*.5f,0),0);
            // Raised, legible stitch blocks on the front panel.
            string[] glyphs={"11110100011000111110100011000111110","11111100001000011110100001000011111",
                "11111001000010000100001000010000100","01110100011000111111100011000110001"};
            const float pixel=.035f;
            Vector3 Stitch(float x,float y)=>new Vector3(x,y,
                Mathf.Sqrt(Mathf.Max(0f,1f-x*x-(y/.78f)*(y/.78f)))*.95f+.006f);
            for(int letter=0;letter<4;letter++)for(int row=0;row<7;row++)for(int col=0;col<5;col++)
            {
                if(glyphs[letter][row*5+col]!='1')continue;
                float x=-23*pixel*.5f+(letter*6+col)*pixel,y=.40f-(row+1)*pixel;
                Quad(Stitch(x,y),Stitch(x+pixel*.82f,y),
                    Stitch(x+pixel*.82f,y+pixel*.82f),Stitch(x,y+pixel*.82f),2);
            }
            var mesh=new Mesh{name="DF_BetaCap",subMeshCount=3};
            mesh.SetVertices(vertices);
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }
}
