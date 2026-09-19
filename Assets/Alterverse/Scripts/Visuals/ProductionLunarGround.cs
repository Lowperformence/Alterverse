using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Alterverse.Match;

namespace Alterverse.Visuals
{
public sealed partial class ProductionLunarGround : MonoBehaviour
{
    readonly List<Material> materials=new();
    readonly List<Mesh> meshes=new();
    Transform root;
    Material regolith,darkRegolith,rimMat,zeroEnergy,lumenEnergy,alienMineral;
    Mesh rockMesh,rockMeshB,rockMeshC;
    Texture2D surfaceAlbedo,surfaceNormals;
    public Texture2D SurfaceAlbedo=>surfaceAlbedo;
    Vector3[] perimeter;

    void Start()
    {
        if(GetComponent<LunarLighting>()==null)gameObject.AddComponent<LunarLighting>();
        string rootName=ArenaVariantRuntime.IsMars?"DF_MarsGroundProduction":ArenaVariantRuntime.IsGreenPlanet?"DF_GreenPlanetGroundProduction":"DF_LunarGroundProduction";
        root=new GameObject(rootName).transform;
        root.SetParent(transform,false);
        bool mars=ArenaVariantRuntime.IsMars,green=ArenaVariantRuntime.IsGreenPlanet;
        regolith=Mat(mars?new Color(.34f,.13f,.075f,1):green?new Color(.16f,.245f,.13f,1):new Color(.105f,.12f,.145f,1),.02f,.12f);
        darkRegolith=Mat(mars?new Color(.16f,.055f,.035f,1):green?new Color(.055f,.09f,.055f,1):new Color(.055f,.065f,.082f,1),.01f,.08f);
        rimMat=Mat(mars?new Color(.43f,.19f,.105f,1):green?new Color(.26f,.34f,.18f,1):new Color(.19f,.205f,.225f,1),.03f,.11f);
        if(green)alienMineral=Mat(new Color(.35f,.48f,.16f,1),.02f,.18f,true);
        zeroEnergy=Mat(Color.Lerp(StylizedArtDirection.ZeroEnergy,Color.white,.05f),.05f,.32f,true);
        lumenEnergy=Mat(Color.Lerp(StylizedArtDirection.LumenEnergy,Color.white,.05f),.05f,.32f,true);
        BuildSurface();
        BuildCoreIntegration(-36f,true);
        BuildCoreIntegration(36f,false);
        BuildHealingIntegration(-27.5f);
        BuildHealingIntegration(27.5f);
        BuildTowerIntegration();
        // Deliberately omit dark ribbon/fracture overlays: from the gameplay camera they read as black debug lines.
        // Surface variation, crater relief, rocks and impact details carry the lunar geology instead.
        BuildRocks();
        if(ArenaVariantRuntime.IsGreenPlanet)BuildAlienMineralAccents();
        BuildDistributedCraterDebris();
        BuildBattlefieldAccessories();
        BuildSmallLandmarks();
        BuildDensityClusters();
        BuildReferenceSilhouettes();
        BuildEdgeCliffs();
        BuildEdgeFragments();
        BuildGeologicalSlab();
        BatchStaticDetails();
    }

    void BuildSurface()
    {
        const int xSeg=84,zSeg=72;
        Vector3[] v=new Vector3[(xSeg+1)*(zSeg+1)];Vector2[] uv=new Vector2[v.Length];
        int[] t=new int[xSeg*zSeg*6];
        for(int z=0;z<=zSeg;z++)for(int x=0;x<=xSeg;x++)
        {
            int i=z*(xSeg+1)+x;float px=-42+x,pz=-36+z;
            // Small interior jitter breaks regular triangulation while boundary samples remain shared with the cliff.
            if(x>0&&x<xSeg&&z>0&&z<zSeg){px+=Mathf.Sin(x*13.1f+z*7.7f)*.16f;pz+=Mathf.Sin(x*5.3f-z*11.9f)*.16f;}
            v[i]=LunarSurfaceField.Surface(px,pz);uv[i]=new Vector2((v[i].x+48)/96,(v[i].z+42)/84);
        }
        int q=0;
        for(int z=0;z<zSeg;z++)for(int x=0;x<xSeg;x++)
        {int a=z*(xSeg+1)+x,b=a+1,c=a+xSeg+1,d=c+1;t[q++]=a;t[q++]=c;t[q++]=b;t[q++]=b;t[q++]=c;t[q++]=d;}
        Mesh m=new(){name="DF_ContinuousRegolith",vertices=v,uv=uv,triangles=t};m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();meshes.Add(m);
        LunarSurfaceField.Bake(out surfaceAlbedo,out surfaceNormals);
        regolith.color=Color.white;regolith.SetColor("_BaseColor",Color.white);
        regolith.SetTexture("_BaseMap",surfaceAlbedo);regolith.SetTexture("_MainTex",surfaceAlbedo);
        regolith.SetTexture("_BumpMap",surfaceNormals);regolith.SetFloat("_BumpScale",.8f);regolith.EnableKeyword("_NORMALMAP");
        string surfaceName=ArenaVariantRuntime.IsMars?"DF_MarsRegolith":ArenaVariantRuntime.IsGreenPlanet?"DF_GreenPlanetRegolith":"DF_LunarRegolith";
        MeshObject(surfaceName,m,regolith,Vector3.zero);
        var edge=new List<Vector3>((xSeg+zSeg)*2);
        for(int x=0;x<xSeg;x++)edge.Add(v[x]);
        for(int z=0;z<zSeg;z++)edge.Add(v[z*(xSeg+1)+xSeg]);
        for(int x=xSeg;x>0;x--)edge.Add(v[zSeg*(xSeg+1)+x]);
        for(int z=zSeg;z>0;z--)edge.Add(v[z*(xSeg+1)]);
        perimeter=edge.ToArray();
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

    void BuildTowerIntegration()
    {
        foreach(float z in new[]{-16f,16f})
        foreach(float x in new[]{-33.5f,-21f,21f,33.5f})
        {
            bool zero=x<0;Material energy=zero?zeroEnergy:lumenEnergy;
            Disc("DF_BastionGroundSeat",new Vector3(x,.058f,z),2.15f,24,darkRegolith);
            Ring("DF_BastionGroundTrim",new Vector3(x,.077f,z),1.82f,2.08f,24,rimMat);
            float towardCenter=zero?1f:-1f;
            Strip("DF_BastionBuriedFeed",new Vector2(x+towardCenter*1.25f,z),new Vector2(x+towardCenter*2.65f,z),.055f,.087f,energy);
        }
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

    void BuildRealityTransitionScars()
    {
        // Near the contested middle, faction color almost disappears into neutral lunar stone.
        for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
        {
            float z=-24f+i*16f;Vector2 a=new(side*13.5f,z),b=new(side*5.8f,z+(i%2==0?1.1f:-1.1f));
            Material faint=side<0?zeroEnergy:lumenEnergy;
            Vector2 mid=Vector2.Lerp(a,b,.58f)+new Vector2(0,side<0?.7f:.18f);
            Strip("DF_NeutralSeam",a,mid,.09f,.089f,darkRegolith);
            Strip("DF_NeutralSeam",mid,b,.065f,.089f,darkRegolith);
            Strip("DF_FadingRealitySeam",Vector2.Lerp(a,mid,.35f),Vector2.Lerp(a,mid,.55f),.035f,.094f,faint);
            Strip("DF_NeutralCollisionScar",new Vector2(side*6.4f,z+(i%2==0?.35f:-.35f)),new Vector2(side*2.8f,z),.065f,.087f,darkRegolith);
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

    Mesh CreateRockMesh()=>LunarFragmentGeometry.Create(7);

    Mesh CreateRockMeshB()=>LunarFragmentGeometry.Create(19);

    Mesh CreateRockMeshC()=>LunarFragmentGeometry.Create(31);

    void BuildAlienMineralAccents()
    {
        if(alienMineral==null||rockMeshB==null)return;
        Vector3[] sites={
            new(-37,.12f,27),new(-29,.10f,-30),new(-12,.08f,31),new(14,.08f,-31),
            new(30,.10f,29),new(38,.12f,-25),new(-40,.10f,-8),new(40,.10f,10)
        };
        for(int i=0;i<sites.Length;i++)
        {
            GameObject g=MeshObject("DF_XenoMineral",rockMeshB,alienMineral,sites[i]);
            float s=.28f+(i%3)*.09f;
            g.transform.localScale=new Vector3(s*.75f,s*1.65f,s*.65f);
            g.transform.localRotation=Quaternion.Euler(i*9f,i*53f,i%2==0?13f:-9f);
        }
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

    void BuildEdgeFragments()
    {
        Vector3[] sites={new(-43,.15f,24),new(-43,.12f,-20),new(43,.14f,19),new(43,.12f,-25),new(-25,.12f,36),new(27,.12f,36),new(-30,.12f,-36),new(23,.12f,-36)};
        for(int i=0;i<sites.Length;i++)
        {
            Mesh shape=i%3==0?rockMesh:i%3==1?rockMeshB:rockMeshC;
            GameObject g=MeshObject("DF_BrokenMoonEdge",shape,darkRegolith,sites[i]);
            g.transform.localScale=new Vector3(1.3f+(i%3)*.35f,.8f+(i%2)*.3f,1.1f+(i%4)*.22f);
            g.transform.localRotation=Quaternion.Euler(i%2==0?11f:-8f,i*57f,i%3==0?8f:-5f);
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
        for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;v[i+1]=c+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);int q=i*3;t[q]=0;t[q+1]=(i+1)%segments+1;t[q+2]=i+1;}
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

    void OnDestroy(){if(surfaceAlbedo!=null)Destroy(surfaceAlbedo);if(surfaceNormals!=null)Destroy(surfaceNormals);foreach(Material m in materials)if(m!=null)Destroy(m);foreach(Mesh m in meshes)if(m!=null)Destroy(m);}
}
}