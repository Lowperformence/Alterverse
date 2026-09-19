using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Small low-poly runtime meshes used only by the Green Planet Xeno Titan.</summary>
    public static class XenoTitanMeshFactory
    {
        public static Mesh Plate(string name,Vector2[] profile,float depth)
        {
            int n=profile.Length;
            Vector3[] v=new Vector3[n*2];
            for(int i=0;i<n;i++)
            {
                v[i]=new Vector3(profile[i].x,profile[i].y,-depth*.5f);
                v[i+n]=new Vector3(profile[i].x,profile[i].y,depth*.5f);
            }
            int[] t=new int[(n-2)*6+n*6];
            int q=0;
            for(int i=1;i<n-1;i++)
            {
                t[q++]=0;t[q++]=i+1;t[q++]=i;
                t[q++]=n;t[q++]=n+i;t[q++]=n+i+1;
            }
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                t[q++]=i;t[q++]=j;t[q++]=n+j;
                t[q++]=i;t[q++]=n+j;t[q++]=n+i;
            }
            Mesh m=new(){name=name,vertices=v,triangles=t};
            m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        public static Mesh HeadShell(string name)
        {
            const int sides=8,sections=4;
            Vector3[] v=new Vector3[sides*sections];
            float[] z={-.72f,-.18f,.38f,.82f};
            float[] rx={.72f,.82f,.62f,.38f};
            float[] ry={.42f,.50f,.43f,.30f};
            for(int s=0;s<sections;s++)for(int i=0;i<sides;i++)
            {
                float a=(i+.5f)*Mathf.PI*2f/sides;
                float x=Mathf.Cos(a)*rx[s];
                float y=Mathf.Sin(a)*ry[s]+(s==3?-.04f:0f);
                v[s*sides+i]=new Vector3(x,y,z[s]);
            }
            int[] t=new int[(sections-1)*sides*6+(sides-2)*6];
            int q=0;
            for(int s=0;s<sections-1;s++)for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides,a=s*sides+i,b=s*sides+j,c=(s+1)*sides+i,d=(s+1)*sides+j;
                t[q++]=a;t[q++]=c;t[q++]=d;t[q++]=a;t[q++]=d;t[q++]=b;
            }
            for(int i=1;i<sides-1;i++)
            {
                t[q++]=0;t[q++]=i;t[q++]=i+1;
                int back=(sections-1)*sides;t[q++]=back;t[q++]=back+i+1;t[q++]=back+i;
            }
            Mesh m=new(){name=name,vertices=v,triangles=t};
            m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        public static Mesh ClawBlade(string name,float length,float width,float thickness,float hook)
        {
            float l=Mathf.Max(.2f,length),w=width*.5f,d=thickness*.5f;
            Vector3[] v={
                new(-w,-d,0),new(w,-d,0),new(w,d,0),new(-w,d,0),
                new(-w*.62f,-d*.72f,l*.62f),new(w*.62f,-d*.72f,l*.62f),new(w*.62f,d*.72f,l*.62f),new(-w*.62f,d*.72f,l*.62f),
                new(0,-d*.35f,l),new(0,d*.35f,l)
            };
            v[8]+=Vector3.down*hook;v[9]+=Vector3.down*hook;
            int[] t={
                0,2,1,0,3,2,
                0,1,5,0,5,4, 3,7,6,3,6,2,
                0,4,7,0,7,3, 1,2,6,1,6,5,
                4,5,8,5,9,8,5,6,9,6,7,9,7,8,9,7,4,8
            };
            Mesh m=new(){name=name,vertices=v,triangles=t};
            m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        public static Mesh Spine(string name,float length,float width,float depth)
        {
            float w=width*.5f,d=depth*.5f;
            Vector3[] v={
                new(-w,0,-d),new(w,0,-d),new(w,0,d),new(-w,0,d),
                new(0,length,-d*.25f),new(0,length,d*.25f)
            };
            int[] t={
                0,2,1,0,3,2,
                0,1,4,1,2,5,1,5,4,
                2,3,5,3,0,4,3,4,5,
                4,5,2,4,2,1
            };
            Mesh m=new(){name=name,vertices=v,triangles=t};
            m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
}
