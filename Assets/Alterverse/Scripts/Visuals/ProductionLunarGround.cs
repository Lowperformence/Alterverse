using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Alterverse.Match;
using Alterverse.Combat;

namespace Alterverse.Visuals
{
public sealed partial class ProductionLunarGround : MonoBehaviour
{
    readonly List<Material> materials=new();
    readonly List<Mesh> meshes=new();
    Transform root;
    Material regolith,darkRegolith,rimMat,zeroEnergy,lumenEnergy,alienMineral,cryoIce;
    Mesh rockMesh,rockMeshB,rockMeshC;
    Texture2D surfaceAlbedo,surfaceNormals;
    PlanetSurfaceMaterials surfaceMaterials;
    public void ApplySurfaceMaterial(Material material,bool crater=false) => surfaceMaterials?.Apply(material,false,crater);
    public Texture2D SurfaceAlbedo=>surfaceAlbedo;
    Vector3[] perimeter;

    void Start()
    {
        if(GetComponent<LunarLighting>()==null)gameObject.AddComponent<LunarLighting>();
        string rootName=ArenaVariantRuntime.IsExoplanet?"DF_"+ArenaVariantRuntime.Current+"GroundProduction":ArenaVariantRuntime.IsMars?"DF_MarsGroundProduction":
            ArenaVariantRuntime.IsGreenPlanet?"DF_GreenPlanetGroundProduction":
            ArenaVariantRuntime.IsNoxara?"DF_NoxaraGroundProduction":
            ArenaVariantRuntime.IsKryon?"DF_KryonGroundProduction":"DF_LunarGroundProduction";
        root=new GameObject(rootName).transform;
        root.SetParent(transform,false);
        bool mars=ArenaVariantRuntime.IsMars,green=ArenaVariantRuntime.IsGreenPlanet,noxara=ArenaVariantRuntime.IsNoxara,kryon=ArenaVariantRuntime.IsKryon;
        regolith=Mat(mars?new Color(.34f,.13f,.075f,1):green?new Color(.27f,.34f,.28f,1):noxara?new Color(.045f,.075f,.19f,1):kryon?new Color(.62f,.75f,.80f,1):new Color(.105f,.12f,.145f,1),.02f,kryon?.34f:.12f);
        darkRegolith=Mat(mars?new Color(.16f,.055f,.035f,1):green?new Color(.12f,.19f,.20f,1):noxara?new Color(.015f,.026f,.075f,1):kryon?new Color(.16f,.24f,.29f,1):new Color(.055f,.065f,.082f,1),.01f,kryon?.22f:.08f);
        rimMat=Mat(mars?new Color(.43f,.19f,.105f,1):green?new Color(.40f,.47f,.39f,1):noxara?new Color(.09f,.15f,.34f,1):kryon?new Color(.55f,.78f,.86f,1):new Color(.19f,.205f,.225f,1),.03f,kryon?.38f:.11f);
        if(ArenaVariantRuntime.IsExoplanet)
        {
            bool iron=ArenaVariantRuntime.IsWasp76b;
            Color tint=ExoplanetArenaProfile.RockTint(ArenaVariantRuntime.Current);
            darkRegolith.color=tint*.52f;darkRegolith.SetColor("_BaseColor",darkRegolith.color);
            rimMat.color=tint;rimMat.SetColor("_BaseColor",tint);
            foreach(Material material in new[]{regolith,darkRegolith,rimMat})
            {material.SetFloat("_Metallic",iron?.48f:.02f);material.SetFloat("_Smoothness",iron?.34f:.30f);}
        }
        if(green)alienMineral=Mat(new Color(.30f,.55f,.57f,1),.02f,.18f,true);
        if(kryon)cryoIce=Mat(new Color(.26f,.90f,1f,1),.08f,.62f,true);
        zeroEnergy=Mat(Color.Lerp(StylizedArtDirection.ZeroEnergy,Color.white,.05f),.05f,.32f,true);
        lumenEnergy=Mat(Color.Lerp(StylizedArtDirection.LumenEnergy,Color.white,.05f),.05f,.32f,true);
        BuildSurface();
        // Deliberately omit dark ribbon/fracture overlays: from the gameplay camera they read as black debug lines.
        // Surface variation, crater relief, rocks and impact details carry the lunar geology instead.
        var clearance=new SceneryBuildingClearance(ArenaLayout.LengthScale);
        int decorationStart=root.childCount;
        BuildRocks();
        if(ArenaVariantRuntime.IsGreenPlanet)BuildAlienMineralAccents();
        if(ArenaVariantRuntime.IsKryon)BuildKryonIceAccents();
        BuildDistributedCraterDebris();
        BuildBattlefieldAccessories();
        BuildSmallLandmarks();
        BuildDensityClusters();
        BuildReferenceSilhouettes();
        // Filter before combining meshes; keep the floor and cliff shell intact.
        for(int i=decorationStart;i<root.childCount;i++)clearance.RemoveIfBlocked(root.GetChild(i).gameObject);
        // The Blender-authored border now owns the small edge fragments and cliff rocks.
        BuildGeologicalSlab();
        BatchStaticDetails();
        ArenaLayout.StretchTerrain(root);
    }

    void BuildSurface()
    {
        // Denser Mars silhouette supports the broad dunes without changing collision or pathing.
        int xSeg=ArenaVariantRuntime.IsMars?192:144;
        int zSeg=ArenaVariantRuntime.IsMars?160:128;
        Vector3[] v=new Vector3[(xSeg+1)*(zSeg+1)];Vector2[] uv=new Vector2[v.Length];
        int[] t=new int[xSeg*zSeg*6];
        for(int z=0;z<=zSeg;z++)for(int x=0;x<=xSeg;x++)
        {
            int i=z*(xSeg+1)+x;float px=-42+84f*x/xSeg,pz=-36+72f*z/zSeg;
            // Small interior jitter breaks regular triangulation while boundary samples remain shared with the cliff.
            if(x>0&&x<xSeg&&z>0&&z<zSeg){px+=Mathf.Sin(x*13.1f+z*7.7f)*.16f;pz+=Mathf.Sin(x*5.3f-z*11.9f)*.16f;}
            v[i]=LunarSurfaceField.Surface(px,pz);uv[i]=new Vector2((v[i].x+48)/96,(v[i].z+42)/84);
        }
        int q=0;
        for(int z=0;z<zSeg;z++)for(int x=0;x<xSeg;x++)
        {int a=z*(xSeg+1)+x,b=a+1,c=a+xSeg+1,d=c+1;t[q++]=a;t[q++]=c;t[q++]=b;t[q++]=b;t[q++]=c;t[q++]=d;}
        Mesh m=new(){name="DF_ContinuousRegolith",vertices=v,uv=uv,triangles=t};m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();meshes.Add(m);
        LunarSurfaceField.Bake(out surfaceAlbedo,out surfaceNormals);
        surfaceMaterials=new PlanetSurfaceMaterials(surfaceAlbedo);
        surfaceMaterials.Apply(regolith,true);
        surfaceMaterials.Apply(darkRegolith);
        surfaceMaterials.Apply(rimMat);
        regolith.color=Color.white;regolith.SetColor("_BaseColor",Color.white);
        regolith.SetTexture("_BaseMap",surfaceAlbedo);
        regolith.SetTexture("_BumpMap",surfaceNormals);
        string surfaceName=ArenaVariantRuntime.IsExoplanet?"DF_"+ArenaVariantRuntime.Current+"Crust":ArenaVariantRuntime.IsMars?"DF_MarsRegolith":
            ArenaVariantRuntime.IsGreenPlanet?"DF_GreenPlanetRegolith":
            ArenaVariantRuntime.IsNoxara?"DF_NoxaraRegolith":
            ArenaVariantRuntime.IsKryon?"DF_KryonIceRegolith":"DF_LunarRegolith";
        MeshObject(surfaceName,m,regolith,Vector3.zero);
        var edge=new List<Vector3>((xSeg+zSeg)*2);
        for(int x=0;x<xSeg;x++)edge.Add(v[x]);
        for(int z=0;z<zSeg;z++)edge.Add(v[z*(xSeg+1)+xSeg]);
        for(int x=xSeg;x>0;x--)edge.Add(v[zSeg*(xSeg+1)+x]);
        for(int z=zSeg;z>0;z--)edge.Add(v[z*(xSeg+1)]);
        perimeter=edge.ToArray();
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

    void BuildKryonIceAccents()
    {
        if(cryoIce==null||rockMeshB==null)return;

        // Emissive fissures and small ice shards are presentation only: no new colliders,
        // pathing or arena geometry are introduced.
        Vector2[] cracks={
            new(-31,25),new(-18,-27),new(-7,19),new(8,-20),
            new(20,27),new(33,-24),new(-35,-11),new(35,12)
        };
        for(int i=0;i<cracks.Length;i++)
        {
            float dir=i%2==0?1f:-1f;
            Vector2 a=cracks[i];
            Vector2 b=a+new Vector2((1.45f+(i%3)*.35f)*dir,(i%2==0?.85f:-.95f));
            Strip("DF_KryonCryoFissure",a,b,.045f,.091f,cryoIce);
        }

        Vector3[] shards={
            new(-39,.10f,27),new(-30,.09f,-31),new(-14,.08f,32),new(15,.08f,-32),
            new(31,.10f,30),new(39,.10f,-27),new(-41,.09f,-7),new(41,.09f,8)
        };
        for(int i=0;i<shards.Length;i++)
        {
            GameObject g=MeshObject("DF_KryonIceShard",rockMeshB,cryoIce,shards[i]);
            float s=.22f+(i%3)*.07f;
            g.transform.localScale=new Vector3(s*.62f,s*1.70f,s*.58f);
            g.transform.localRotation=Quaternion.Euler(i*11f,i*49f,i%2==0?10f:-8f);
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
        Color glow=emission?c*1.25f:Color.black;
        Material m=RuntimeShaderLibrary.Create(c,false,glow,metallic,smooth);
        if(m==null)
        {
            Debug.LogError("DIMENSIONS FRONT: could not create Android-safe lunar material.");
            return null;
        }
        m.name="DF_LunarRuntimeMaterial";
        materials.Add(m);
        return m;
    }

    void OnDestroy(){surfaceMaterials?.Dispose();if(surfaceAlbedo!=null)Destroy(surfaceAlbedo);if(surfaceNormals!=null)Destroy(surfaceNormals);foreach(Material m in materials)if(m!=null)Destroy(m);foreach(Mesh m in meshes)if(m!=null)Destroy(m);}
}
}




