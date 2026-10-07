using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    public static class SceneryMeshQuality
    {
        public static Mesh RoundedBeam()
        {
            Mesh mesh=Object.Instantiate(ProductionMeshFactory.RoundedBox());
            mesh.name="DF_RoundedIndustrialBeam";
            // Match the original four-sided profile: diamond section, from y=0 to y=1.
            Matrix4x4 shape=Matrix4x4.TRS(Vector3.up*.5f,Quaternion.Euler(0,45,0),new Vector3(.7071068f,1,.7071068f));
            Matrix4x4 normalMatrix=shape.inverse.transpose;
            Vector3[] vertices=mesh.vertices,normals=mesh.normals;
            for(int i=0;i<vertices.Length;i++)
            {vertices[i]=shape.MultiplyPoint3x4(vertices[i]);normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;}
            mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }

        // Smooth continuous sides, separate planar caps. UV seams retain matching normals.
        public static Mesh SoftProfile(string name,int sides,Vector2[] rings)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();
            var uv=new List<Vector2>();var triangles=new List<int>();
            int stride=sides+1;
            for(int ring=0;ring<rings.Length;ring++)
            {
                Vector2 delta=rings[Mathf.Min(ring+1,rings.Length-1)]-rings[Mathf.Max(0,ring-1)];
                for(int side=0;side<=sides;side++)
                {
                    float a=side*Mathf.PI*2/sides;
                    vertices.Add(new Vector3(Mathf.Cos(a)*rings[ring].x,rings[ring].y,Mathf.Sin(a)*rings[ring].x));
                    normals.Add(new Vector3(Mathf.Cos(a)*delta.y,-delta.x,Mathf.Sin(a)*delta.y).normalized);
                    uv.Add(new Vector2(side/(float)sides,Mathf.InverseLerp(rings[0].y,rings[^1].y,rings[ring].y)));
                }
            }
            for(int ring=0;ring<rings.Length-1;ring++)for(int side=0;side<sides;side++)
            {
                int a=ring*stride+side,b=a+1,c=a+stride,d=c+1;
                triangles.AddRange(new[]{a,c,d,a,d,b});
            }
            Cap(0,-1);Cap(rings.Length-1,1);
            Mesh mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);
            mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;

            void Cap(int ring,int direction)
            {
                int center=vertices.Count;
                vertices.Add(new Vector3(0,rings[ring].y,0));normals.Add(Vector3.up*direction);uv.Add(new Vector2(.5f,.5f));
                for(int side=0;side<=sides;side++)
                {
                    Vector3 point=vertices[ring*stride+side];vertices.Add(point);normals.Add(Vector3.up*direction);
                    uv.Add(new Vector2(point.x,point.z)/(rings[ring].x*2)+new Vector2(.5f,.5f));
                }
                for(int side=0;side<sides;side++)
                    triangles.AddRange(direction>0?new[]{center,center+side+2,center+side+1}:
                        new[]{center,center+side+1,center+side+2});
            }
        }

        public static Mesh CurvedFrond()
        {
            const int rows=10,widthSteps=4,stride=widthSteps+1;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            vertices.Add(Vector3.zero);
            for(int row=1;row<rows;row++)for(int side=0;side<=widthSteps;side++)
            {
                float t=row/(float)rows,cross=side*2f/widthSteps-1;
                vertices.Add(new Vector3(cross*Mathf.Sin(t*Mathf.PI)*.24f,
                    Mathf.Sin(t*Mathf.PI*.8f)*.55f-.04f*cross*cross,t));
            }
            int tip=vertices.Count;vertices.Add(new Vector3(0,Mathf.Sin(Mathf.PI*.8f)*.55f,1));
            for(int side=0;side<widthSteps;side++)triangles.AddRange(new[]{0,1+side,2+side});
            for(int row=0;row<rows-2;row++)for(int side=0;side<widthSteps;side++)
            {
                int a=1+row*stride+side,b=a+1,c=a+stride,d=c+1;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            for(int side=0;side<widthSteps;side++)
            {
                int a=1+(rows-2)*stride+side;triangles.AddRange(new[]{a,tip,a+1});
            }
            // Separate underside vertices preserve the silhouette without double-sided shader work.
            int count=vertices.Count,indices=triangles.Count;
            for(int i=0;i<count;i++)vertices.Add(vertices[i]);
            for(int i=0;i<indices;i+=3)triangles.AddRange(new[]{triangles[i+2]+count,triangles[i+1]+count,triangles[i]+count});
            Mesh mesh=new Mesh{name="DF_SmoothCurvedFrond"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        // Smoothing imported plant normals preserves every vertex, triangle and authored silhouette.
        public static void SmoothPlantNormals(Mesh mesh,float crease=75f)
        {
            Vector3[] vertices=mesh.vertices,normals=mesh.normals;
            if(normals.Length!=vertices.Length)return;
            var groups=new Dictionary<Vector3Int,List<int>>();
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=vertices[i]*100000;
                var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(!groups.TryGetValue(key,out var group)){group=new List<int>();groups.Add(key,group);}group.Add(i);
            }
            Vector3[] result=new Vector3[normals.Length];float threshold=Mathf.Cos(crease*Mathf.Deg2Rad);
            foreach(var group in groups.Values)foreach(int i in group)
            {
                Vector3 sum=Vector3.zero;
                foreach(int j in group)if(Vector3.Dot(normals[i],normals[j])>=threshold)sum+=normals[j];
                result[i]=sum.sqrMagnitude>.000001f?sum.normalized:normals[i];
            }
            mesh.normals=result;
        }
    }
}
