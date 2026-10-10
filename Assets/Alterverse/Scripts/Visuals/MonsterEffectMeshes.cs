using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Small, shared projectile meshes. Built once by the combat pool, never per attack.
    internal static class MonsterEffectMeshes
    {
        internal static Mesh Build(HeroFxShape shape)
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,Color tint)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                colors.Add(tint);colors.Add(tint);colors.Add(tint);
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);
            }
            if(shape==HeroFxShape.MonsterBeam)
            {
                // Local Z=0 is the muzzle; Z=1 is contact, including when the endpoint moves.
                foreach(Vector3 side in new[]{Vector3.right*.5f,Vector3.up*.5f})
                {
                    Tri(-side,side,Vector3.forward+side*.55f,Color.white);
                    Tri(-side,Vector3.forward+side*.55f,Vector3.forward-side*.55f,Color.white);
                }
            }
            else if(shape==HeroFxShape.MonsterSlime)
            {
                const int rings=8,sides=12;
                Vector3 Point(int ring,int side)
                {
                    float lat=-Mathf.PI*.5f+ring*Mathf.PI/rings,a=side*Mathf.PI*2/sides;
                    float z=Mathf.Sin(lat)*.55f,r=Mathf.Cos(lat)*.48f;
                    // Rounded front and a drawn-out droplet at the rear.
                    if(z<0){r*=.78f;z*=1.35f;}
                    return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z);
                }
                for(int ring=0;ring<rings;ring++)for(int side=0;side<sides;side++)
                {
                    Vector3 a=Point(ring,side),b=Point(ring+1,side),c=Point(ring+1,side+1),d=Point(ring,side+1);
                    if(ring<rings-1)Tri(a,c,b,Color.white);
                    if(ring>0)Tri(a,d,c,Color.white);
                }
            }
            else
            {
                bool diamond=shape==HeroFxShape.MonsterDiamond;
                const int sides=7;
                Vector3 Point(int i,bool upper)
                {
                    float a=i*Mathf.PI*2/sides+(upper?.10f:-.08f);
                    float r=diamond?.40f:.44f+Mathf.Sin((i%sides)*2.7f)*.075f;
                    return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,upper?.16f:-.22f);
                }
                Vector3 top=new(diamond?.04f:.13f,diamond?0f:.06f,diamond?.82f:.51f);
                Vector3 bottom=new(diamond?-.02f:-.10f,0,diamond?-.58f:-.46f);
                for(int i=0;i<sides;i++)
                {
                    Vector3 a=Point(i,true),b=Point(i+1,true),c=Point(i+1,false),d=Point(i,false);
                    Color tint=Color.white*(.72f+(i%3)*.14f);tint.a=1;
                    Tri(a,b,top,tint);Tri(d,b,a,tint);Tri(d,c,b,tint);Tri(c,d,bottom,tint);
                }
            }
            var mesh=new Mesh{name="DF_MonsterFx_"+shape};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();
            if(shape==HeroFxShape.MonsterSlime)
            {
                var normals=new List<Vector3>(vertices.Count);
                foreach(var p in vertices)normals.Add(new Vector3(p.x,p.y,p.z*.60f).normalized);
                mesh.SetNormals(normals);
            }
            mesh.RecalculateBounds();return mesh;
        }
    }
}
