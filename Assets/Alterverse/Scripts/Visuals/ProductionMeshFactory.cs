using UnityEngine;
namespace Alterverse.Visuals
{
public static class ProductionMeshFactory
{
private static Mesh beveledBox;
// Shared visual-only geometry for mechanical enemies. The original gameplay colliders stay intact.
public static Mesh BeveledBox()
{
    if(beveledBox!=null)return beveledBox;
    const int segments=4;const float bevel=.12f;
    Vector3[] face={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
    Vector3[] u={Vector3.back,Vector3.forward,Vector3.right,Vector3.right,Vector3.right,Vector3.left};
    Vector3[] v={Vector3.up,Vector3.up,Vector3.back,Vector3.forward,Vector3.up,Vector3.up};
    int stride=(segments+1)*(segments+1);
    Vector3[] vertices=new Vector3[stride*6],normals=new Vector3[vertices.Length];
    int[] triangles=new int[segments*segments*6*6];int ti=0;
    for(int f=0;f<6;f++)
    {
        int offset=f*stride;
        for(int y=0;y<=segments;y++)for(int x=0;x<=segments;x++)
        {
            Vector3 p=face[f]*.5f+u[f]*(x/(float)segments-.5f)+v[f]*(y/(float)segments-.5f);
            Vector3 inner=new Vector3(Mathf.Clamp(p.x,-.5f+bevel,.5f-bevel),Mathf.Clamp(p.y,-.5f+bevel,.5f-bevel),Mathf.Clamp(p.z,-.5f+bevel,.5f-bevel));
            Vector3 normal=(p-inner).normalized;int i=offset+y*(segments+1)+x;
            vertices[i]=inner+normal*bevel;normals[i]=normal;
        }
        for(int y=0;y<segments;y++)for(int x=0;x<segments;x++)
        {
            int a=offset+y*(segments+1)+x,b=a+1,c=a+segments+1,d=c+1;
            triangles[ti++]=a;triangles[ti++]=b;triangles[ti++]=c;
            triangles[ti++]=b;triangles[ti++]=d;triangles[ti++]=c;
        }
    }
    beveledBox=new Mesh{name="DF_SharedBeveledMachineBox",vertices=vertices,normals=normals,triangles=triangles};
    beveledBox.RecalculateBounds();return beveledBox;
}
public static Mesh Ring(float radius,float tube,int radial=28,int sides=8){radial=Mathf.Max(8,radial);sides=Mathf.Max(4,sides);var v=new Vector3[radial*sides];var n=new Vector3[v.Length];var uv=new Vector2[v.Length];var t=new int[radial*sides*6];for(int r=0;r<radial;r++){float a=r*Mathf.PI*2f/radial;Vector3 center=new(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);Vector3 outward=new(Mathf.Cos(a),0,Mathf.Sin(a));for(int s=0;s<sides;s++){float b=s*Mathf.PI*2f/sides;int i=r*sides+s;Vector3 normal=outward*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b);v[i]=center+normal*tube;n[i]=normal;uv[i]=new Vector2((float)r/radial,(float)s/sides);int rn=((r+1)%radial)*sides;int sn=(s+1)%sides;int q=(r*sides+s)*6;t[q]=i;t[q+1]=rn+s;t[q+2]=rn+sn;t[q+3]=i;t[q+4]=rn+sn;t[q+5]=r*sides+sn;}}Mesh m=new(){name="DF_ProductionRing"};m.vertices=v;m.normals=n;m.uv=uv;m.triangles=t;m.RecalculateBounds();return m;}
public static Mesh Tube(Vector3 from,Vector3 to,float radius,int sides=8){Vector3 axis=to-from;float len=axis.magnitude;if(len<.001f)return null;Vector3 f=axis/len;Vector3 right=Vector3.Cross(f,Mathf.Abs(Vector3.Dot(f,Vector3.up))>.9f?Vector3.right:Vector3.up).normalized;Vector3 up=Vector3.Cross(right,f).normalized;var v=new Vector3[sides*2];var n=new Vector3[v.Length];var t=new int[sides*6];for(int s=0;s<sides;s++){float a=s*Mathf.PI*2f/sides;Vector3 off=(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius;v[s]=from+off;v[s+sides]=to+off;n[s]=n[s+sides]=off.normalized;int sn=(s+1)%sides,q=s*6;t[q]=s;t[q+1]=s+sides;t[q+2]=sn+sides;t[q+3]=s;t[q+4]=sn+sides;t[q+5]=sn;}Mesh m=new(){name="DF_ProductionTube"};m.vertices=v;m.normals=n;m.triangles=t;m.RecalculateBounds();return m;}
public static Mesh Disc(float radius,float halfHeight,int segments=32){segments=Mathf.Max(12,segments);var v=new Vector3[segments*2+2];var n=new Vector3[v.Length];var t=new int[segments*12];for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;float x=Mathf.Cos(a)*radius,z=Mathf.Sin(a)*radius;v[i]=new Vector3(x,-halfHeight,z);v[i+segments]=new Vector3(x,halfHeight,z);n[i]=n[i+segments]=new Vector3(x,0,z).normalized;}v[segments*2]=new Vector3(0,-halfHeight,0);v[segments*2+1]=new Vector3(0,halfHeight,0);for(int i=0;i<segments;i++){int j=(i+1)%segments,q=i*12;t[q]=i;t[q+1]=i+segments;t[q+2]=j+segments;t[q+3]=i;t[q+4]=j+segments;t[q+5]=j;t[q+6]=segments*2;t[q+7]=i;t[q+8]=j;t[q+9]=segments*2+1;t[q+10]=j+segments;t[q+11]=i+segments;}Mesh m=new(){name="DF_ProductionDisc"};m.vertices=v;m.normals=n;m.triangles=t;m.RecalculateNormals();m.RecalculateBounds();return m;}
public static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material){GameObject g=new(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;}
}
}
