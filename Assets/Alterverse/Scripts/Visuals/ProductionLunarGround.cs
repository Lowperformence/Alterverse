using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
public sealed class ProductionLunarGround : MonoBehaviour
{
    readonly List<Material> materials=new();
    readonly List<Mesh> meshes=new();
    Transform root;
    Material regolith,darkRegolith,laneMat,rimMat,zeroEnergy,lumenEnergy;
    Mesh rockMesh,rockMeshB,rockMeshC;

    void Start()
    {
        root=new GameObject("DF_LunarGroundProduction").transform;
        root.SetParent(transform,false);
        regolith=Mat(new Color(.105f,.12f,.145f,1),.02f,.12f);
        darkRegolith=Mat(new Color(.055f,.065f,.082f,1),.01f,.08f);
        laneMat=Mat(new Color(.145f,.16f,.18f,1),.04f,.16f);
        rimMat=Mat(new Color(.19f,.205f,.225f,1),.03f,.11f);
        zeroEnergy=Mat(Color.Lerp(StylizedArtDirection.ZeroEnergy,Color.white,.05f),.05f,.32f,true);
        lumenEnergy=Mat(Color.Lerp(StylizedArtDirection.LumenEnergy,Color.white,.05f),.05f,.32f,true);
        BuildSurface();
        BuildLanes();
        BuildCoreIntegration(-36f,true);
        BuildCoreIntegration(36f,false);
        BuildHealingIntegration(-27.5f);
        BuildHealingIntegration(27.5f);
        BuildCraters();
        BuildFractures();
        BuildRocks();
        BuildEdgeCliffs();
    }

    void BuildSurface()
    {
        const int xSeg=20,zSeg=18;const float width=84f,depth=72f;
        Vector3[] v=new Vector3[(xSeg+1)*(zSeg+1)];
        Vector2[] uv=new Vector2[v.Length];
        int[] t=new int[xSeg*zSeg*6];
        for(int z=0;z<=zSeg;z++)for(int x=0;x<=xSeg;x++)
        {
            int i=z*(xSeg+1)+x;float px=-width*.5f+width*x/xSeg,pz=-depth*.5f+depth*z/zSeg;
            float y=.012f+NoiseHeight(px,pz);
            v[i]=new Vector3(px,y,pz);uv[i]=new Vector2((float)x/xSeg,(float)z/zSeg);
        }
        int q=0;
        for(int z=0;z<zSeg;z++)for(int x=0;x<xSeg;x++)
        {
            int a=z*(xSeg+1)+x,b=a+1,c=a+xSeg+1,d=c+1;
            t[q++]=a;t[q++]=c;t[q++]=b;t[q++]=b;t[q++]=c;t[q++]=d;
        }
        Mesh m=new(){name="DF_LunarRegolithMesh",vertices=v,uv=uv,triangles=t};m.RecalculateNormals();m.RecalculateBounds();meshes.Add(m);
        MeshObject("DF_LunarRegolith",m,regolith,Vector3.zero);
    }

    static float NoiseHeight(float x,float z)
    {
        // Very shallow visual relief: gameplay continues on the existing flat collider.
        float n=Mathf.PerlinNoise(x*.075f+12.3f,z*.075f+7.1f)-.5f;
        float fine=Mathf.PerlinNoise(x*.19f+31f,z*.19f+17f)-.5f;
        return n*.055f+fine*.018f;
    }

    void BuildLanes()
    {
        foreach(float z in new[]{-16f,16f})
        {
            Strip("DF_LunarLane",new Vector2(-30,z),new Vector2(30,z),4.55f,.085f,laneMat);
            Strip("DF_LaneWearA",new Vector2(-29,z-.72f),new Vector2(29,z-.72f),.12f,.094f,rimMat);
            Strip("DF_LaneWearB",new Vector2(-29,z+.82f),new Vector2(29,z+.82f),.08f,.094f,rimMat);
            Strip("DF_ZeroLaneEnergy",new Vector2(-30,z-2.18f),new Vector2(-4,z-2.18f),.055f,.103f,zeroEnergy);
            Strip("DF_ZeroLaneEnergy",new Vector2(-30,z+2.18f),new Vector2(-4,z+2.18f),.055f,.103f,zeroEnergy);
            Strip("DF_LumenLaneEnergy",new Vector2(4,z-2.18f),new Vector2(30,z-2.18f),.055f,.103f,lumenEnergy);
            Strip("DF_LumenLaneEnergy",new Vector2(4,z+2.18f),new Vector2(30,z+2.18f),.055f,.103f,lumenEnergy);
        }
    }

    void BuildCoreIntegration(float x,bool zero)
    {
        Disc("DF_CoreImpactBed",new Vector3(x,.055f,0),5.0f,32,darkRegolith);
        Ring("DF_CoreBuriedRim",new Vector3(x,.073f,0),4.35f,4.72f,32,rimMat);
        Material energy=zero?zeroEnergy:lumenEnergy;
        for(int i=0;i<7;i++)
        {
            float a=(i*51f+(zero?9f:22f))*Mathf.Deg2Rad;
            Vector2 from=new Vector2(x+Mathf.Cos(a)*2.25f,Mathf.Sin(a)*2.25f);
            Vector2 to=new Vector2(x+Mathf.Cos(a)*(3.25f+(i%3)*.35f),Mathf.Sin(a)*(3.25f+(i%3)*.35f));
            Strip("DF_CoreGroundFracture",from,to,i%2==0?.075f:.045f,.086f,i==0?energy:rimMat);
        }
    }

    void BuildHealingIntegration(float x)
    {
        Disc("DF_HealingGroundInset",new Vector3(x,.062f,0),2.55f,28,darkRegolith);
        Ring("DF_HealingGroundRim",new Vector3(x,.078f,0),2.35f,2.52f,28,rimMat);
    }

    void BuildCraters()
    {
        Vector3[] p={new(-18,.05f,5),new(-11,.05f,-7),new(17,.05f,-6),new(10,.05f,7),new(-8,.05f,29),new(9,.05f,-29),new(-34,.05f,27),new(34,.05f,-27),new(-2,.05f,31),new(3,.05f,-31)};
        float[] r={1.65f,1.1f,1.45f,.9f,2.1f,1.8f,1.55f,1.65f,1.25f,1.05f};
        for(int i=0;i<p.Length;i++)IrregularCrater(p[i],r[i],i);
    }

    void IrregularCrater(Vector3 center,float radius,int seed)
    {
        const int segments=24;
        Vector3[] outer=new Vector3[segments],inner=new Vector3[segments];
        for(int i=0;i<segments;i++)
        {
            float a=i*Mathf.PI*2/segments;
            float wobble=1f+Mathf.Sin(a*(3+seed%3)+seed*.71f)*.09f+Mathf.Sin(a*7f+seed)*.035f;
            Vector3 d=new(Mathf.Cos(a),0,Mathf.Sin(a));
            outer[i]=center+d*(radius*1.18f*wobble);
            inner[i]=center+d*(radius*.82f*(1f+Mathf.Sin(a*5f+seed*.4f)*.055f));
        }
        Vector3[] v=new Vector3[segments*2];int[] t=new int[segments*6];
        for(int i=0;i<segments;i++){v[i*2]=inner[i]-Vector3.up*.018f;v[i*2+1]=outer[i]+Vector3.up*(.035f+(i%4)*.008f);int j=(i+1)%segments,q=i*6;t[q]=i*2;t[q+1]=j*2;t[q+2]=j*2+1;t[q+3]=i*2;t[q+4]=j*2+1;t[q+5]=i*2+1;}
        Mesh rim=new(){name="DF_IrregularCraterRim",vertices=v,triangles=t};rim.RecalculateNormals();rim.RecalculateBounds();meshes.Add(rim);MeshObject("DF_LunarCraterRim",rim,rimMat,Vector3.zero);
        Mesh floor=IrregularDisc(center-Vector3.up*.024f,radius*.82f,segments,seed);meshes.Add(floor);MeshObject("DF_LunarCraterFloor",floor,darkRegolith,Vector3.zero);
        for(int j=0;j<3;j++){float a=(seed*47+j*113)*Mathf.Deg2Rad;Vector2 from=new(center.x+Mathf.Cos(a)*radius*1.05f,center.z+Mathf.Sin(a)*radius*1.05f);Vector2 to=from+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(.45f+j*.18f);Strip("DF_CraterRadialCrack",from,to,.028f,.071f,darkRegolith);}
    }

    Mesh IrregularDisc(Vector3 c,float radius,int segments,int seed)
    {
        Vector3[] v=new Vector3[segments+1];int[] t=new int[segments*3];v[0]=c;
        for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments,w=1f+Mathf.Sin(a*5f+seed*.7f)*.06f;v[i+1]=c+new Vector3(Mathf.Cos(a)*radius*w,0,Mathf.Sin(a)*radius*w);int q=i*3;t[q]=0;t[q+1]=i+1;t[q+2]=(i+1)%segments+1;}
        Mesh m=new(){name="DF_IrregularLunarDisc",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }

    void BuildFractures()
    {
        Vector2[] starts={new(-37,-8),new(-31,8),new(-22,24),new(-20,-25),new(37,8),new(31,-8),new(22,-24),new(20,25)};
        for(int i=0;i<starts.Length;i++)
        {
            float side=starts[i].x<0?-1f:1f;
            Vector2 a=starts[i],b=a+new Vector2(-side*(2.2f+(i%3)*.55f),(i%2==0?1.3f:-1.5f));
            Strip("DF_LunarFracture",a,b,.055f,.076f,darkRegolith);
            Vector2 mid=Vector2.Lerp(a,b,.55f);
            Strip("DF_LunarFractureBranch",mid,mid+new Vector2(-side*.85f,i%2==0?-.72f:.72f),.035f,.077f,darkRegolith);
        }
    }

    void BuildRocks()
    {
        rockMesh=CreateRockMesh();rockMeshB=CreateRockMeshB();rockMeshC=CreateRockMeshC();meshes.Add(rockMesh);meshes.Add(rockMeshB);meshes.Add(rockMeshC);
        Vector3[] sites={new(-39,.13f,30),new(-38,.11f,-29),new(-20,.09f,29),new(-17,.08f,-29),new(39,.13f,30),new(38,.11f,-29),new(20,.09f,29),new(17,.08f,-29),new(-40,.08f,9),new(40,.08f,-9),new(-33,.08f,33),new(32,.08f,-33)};
        for(int i=0;i<sites.Length;i++)
        {
            Mesh shape=i%3==0?rockMesh:i%3==1?rockMeshB:rockMeshC;
            GameObject g=MeshObject("DF_LunarRock",shape,i%3==0?darkRegolith:rimMat,sites[i]);
            float s=.55f+(i%4)*.19f;g.transform.localScale=new Vector3(s, .55f+(i%3)*.2f, s*(.8f+(i%2)*.25f));
            g.transform.localRotation=Quaternion.Euler(i*7f,i*47f,i*5f);
        }
    }

    Mesh CreateRockMesh()
    {
        Vector3[] v={new(-.72f,0,-.48f),new(.55f,0,-.62f),new(.78f,0,.22f),new(.18f,0,.68f),new(-.62f,0,.51f),new(-.22f,.82f,-.12f),new(.31f,.58f,.19f)};
        int[] t={0,1,5,1,6,5,1,2,6,2,3,6,3,4,5,3,5,6,4,0,5,0,4,3,0,3,2,0,2,1};
        Mesh m=new(){name="DF_LowPolyLunarRock",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }

    Mesh CreateRockMeshB()
    {
        Vector3[] v={new(-.9f,0,-.25f),new(.42f,0,-.72f),new(.86f,0,.1f),new(.48f,0,.64f),new(-.55f,0,.72f),new(-.48f,.52f,-.1f),new(.12f,1.05f,-.18f),new(.4f,.48f,.28f)};
        int[] t={0,1,5,1,6,5,1,2,6,2,7,6,2,3,7,3,4,7,4,5,7,4,0,5,5,6,7,0,4,3,0,3,2,0,2,1};
        Mesh m=new(){name="DF_LunarRockTall",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    Mesh CreateRockMeshC()
    {
        Vector3[] v={new(-.8f,0,-.62f),new(.7f,0,-.48f),new(.92f,0,.42f),new(-.12f,0,.8f),new(-.88f,0,.3f),new(-.34f,.42f,-.28f),new(.42f,.38f,-.16f),new(.08f,.63f,.32f)};
        int[] t={0,1,5,1,6,5,1,2,6,2,7,6,2,3,7,3,4,7,4,5,7,4,0,5,5,6,7,0,4,3,0,3,2,0,2,1};
        Mesh m=new(){name="DF_LunarRockBroad",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }

    void BuildEdgeCliffs()
    {
        Vector3[] sites={new(-41,.02f,34),new(-31,.02f,35),new(-18,.02f,35),new(-5,.02f,35),new(9,.02f,35),new(23,.02f,35),new(37,.02f,34),new(-40,.02f,-34),new(-27,.02f,-35),new(-13,.02f,-35),new(2,.02f,-35),new(17,.02f,-35),new(31,.02f,-35),new(41,.02f,-33)};
        for(int i=0;i<sites.Length;i++)
        {
            Mesh shape=i%3==0?rockMesh:i%3==1?rockMeshB:rockMeshC;
            GameObject g=MeshObject("DF_LunarEdgeFormation",shape,i%4==0?darkRegolith:rimMat,sites[i]);
            g.transform.localScale=new Vector3(2.2f+(i%3)*.45f,1.25f+(i%4)*.28f,1.7f+(i%2)*.5f);
            g.transform.localRotation=Quaternion.Euler(0,i*43f,i%2==0?6f:-5f);
        }
    }

    void Strip(string n,Vector2 a,Vector2 b,float width,float y,Material mat)
    {
        Vector2 d=(b-a).normalized,normal=new Vector2(-d.y,d.x)*width*.5f;
        Vector3[] v={P(a-normal,y),P(a+normal,y),P(b+normal,y),P(b-normal,y)};
        Mesh m=new(){name=n+"Mesh",vertices=v,triangles=new[]{0,1,2,0,2,3}};m.RecalculateNormals();m.RecalculateBounds();meshes.Add(m);MeshObject(n,m,mat,Vector3.zero);
    }

    void Disc(string n,Vector3 c,float radius,int segments,Material mat)
    {
        Vector3[] v=new Vector3[segments+1];int[] t=new int[segments*3];v[0]=c;
        for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;v[i+1]=c+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);int q=i*3;t[q]=0;t[q+1]=i+1;t[q+2]=(i+1)%segments+1;}
        Mesh m=new(){name=n+"Mesh",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();meshes.Add(m);MeshObject(n,m,mat,Vector3.zero);
    }

    void Ring(string n,Vector3 c,float inner,float outer,int segments,Material mat)
    {
        Vector3[] v=new Vector3[segments*2];int[] t=new int[segments*6];
        for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;Vector3 d=new(Mathf.Cos(a),0,Mathf.Sin(a));v[i*2]=c+d*inner;v[i*2+1]=c+d*outer;int j=(i+1)%segments,q=i*6;t[q]=i*2;t[q+1]=j*2;t[q+2]=j*2+1;t[q+3]=i*2;t[q+4]=j*2+1;t[q+5]=i*2+1;}
        Mesh m=new(){name=n+"Mesh",vertices=v,triangles=t};m.RecalculateNormals();m.RecalculateBounds();meshes.Add(m);MeshObject(n,m,mat,Vector3.zero);
    }

    static Vector3 P(Vector2 p,float y)=>new(p.x,y,p.y);

    GameObject MeshObject(string n,Mesh mesh,Material mat,Vector3 p)
    {
        GameObject g=new(n);g.transform.SetParent(root,false);g.transform.localPosition=p;
        g.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
        r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return g;
    }

    Material Mat(Color c,float metallic,float smooth,bool emission=false)
    {
        Shader sh=Shader.Find("Universal Render Pipeline/Lit");if(sh==null)sh=Shader.Find("Standard");
        Material m=new(sh){color=c};materials.Add(m);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);
        if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);
        if(emission&&m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*1.25f);}return m;
    }

    void OnDestroy(){foreach(Material m in materials)if(m!=null)Destroy(m);foreach(Mesh m in meshes)if(m!=null)Destroy(m);}
}
}