using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Planet-specific, surface-anchored scenery in paired peripheral pockets.
    /// No gameplay components, random-state changes, extra lights or per-frame work.</summary>
    [DisallowMultipleComponent]
    public sealed class PlanetLandscapeDetail : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new();
        readonly List<Material> ownedMaterials = new();
        readonly List<CombineInstance>[] batches = { new(), new(), new(), new() };
        Mesh rock, column, crystal, leaf, beam;
        Material[] palette;
        ArenaVariant variant;
        Vector3 center;
        Quaternion orientation;
        Transform scenery;

        // Paired through the origin: identical sight-line treatment for both teams.
        // All geometry stays in the outer strip, clear of lanes, bases and supply drops.
        public static readonly Vector2[] DetailSites = {
            new(-31,32), new(-18,32), new(-5,32), new(8,32),
            new(31,-32), new(18,-32), new(5,-32), new(-8,-32)
        };

        void Start()=>Build();

        public void Build()
        {
            if(scenery!=null)return;
            variant = ArenaVariantRuntime.Current;
            scenery = new GameObject("DF_PlanetLandscape_" + variant).transform;
            scenery.SetParent(transform, false);
            rock = Own(LunarFragmentGeometry.Create(83));
            column = Own(Revolved("ErodedColumn", 7, new[] {
                new Vector2(.48f,0), new Vector2(.51f,.12f), new Vector2(.37f,.68f), new Vector2(.30f,1) }));
            crystal = Own(Revolved("CutPrism", 6, new[] {
                new Vector2(.18f,0), new Vector2(.43f,.20f), new Vector2(.35f,.78f), new Vector2(.025f,1) }));
            leaf = Own(Leaf());
            beam = Own(Revolved("SquareBeam", 4, new[] {new Vector2(.5f,0),new Vector2(.5f,1)}));
            palette = BuildPalette();
            foreach(Material material in palette)
                if(material == null) { enabled = false; return; }
            for(int site = 0; site < DetailSites.Length; site++)
            {
                center = new Vector3(DetailSites[site].x,0,DetailSites[site].y);
                orientation = Quaternion.Euler(0,site < 4 ? 0 : 180,0);
                BuildPocket(site % 4);
                Flush(site);
            }
            // Input meshes are no longer needed after baking the regional batches.
            foreach(Mesh input in new[]{rock,column,crystal,leaf,beam})
            { ownedMeshes.Remove(input); Destroy(input); }
        }

        Material[] BuildPalette()
        {
            Color stone, edge, accent, organic;
            float polish = .18f, metal = .02f, glow = .12f;
            switch(variant)
            {
                case ArenaVariant.Mars:
                    stone=new(.30f,.105f,.055f);edge=new(.62f,.30f,.14f);accent=new(.76f,.48f,.24f);organic=new(.18f,.085f,.055f);glow=0;break;
                case ArenaVariant.GreenPlanet:
                    stone=new(.075f,.15f,.085f);edge=new(.19f,.31f,.15f);accent=new(.52f,.72f,.12f);organic=new(.13f,.38f,.22f);break;
                case ArenaVariant.Noxara:
                    stone=new(.035f,.055f,.15f);edge=new(.11f,.19f,.37f);accent=new(.34f,.40f,.85f);organic=new(.085f,.12f,.28f);polish=.52f;break;
                case ArenaVariant.Kryon:
                    stone=new(.13f,.26f,.33f);edge=new(.67f,.82f,.88f);accent=new(.24f,.61f,.72f);organic=new(.36f,.52f,.62f);polish=.72f;break;
                case ArenaVariant.Earth:
                    stone=new(.20f,.18f,.13f);edge=new(.40f,.37f,.25f);accent=new(.42f,.53f,.21f);organic=new(.10f,.26f,.075f);glow=0;break;
                case ArenaVariant.Pyraxis:
                    stone=new(.035f,.025f,.023f);edge=new(.16f,.095f,.07f);accent=new(1f,.22f,.025f);organic=new(.065f,.046f,.038f);glow=.75f;break;
                case ArenaVariant.Neptune:
                    stone=new(.018f,.07f,.13f);edge=new(.045f,.21f,.28f);accent=new(.10f,.70f,.72f);organic=new(.13f,.12f,.30f);polish=.55f;glow=.30f;break;
                case ArenaVariant.Dark00:
                    stone=new(.015f,.018f,.021f);edge=new(.055f,.065f,.075f);accent=new(.76f,.82f,.88f);organic=new(.085f,.07f,.055f);metal=.72f;polish=.38f;glow=.30f;break;
                case ArenaVariant.DiamondCore:
                    stone=new(.055f,.095f,.13f);edge=new(.43f,.66f,.75f);accent=new(.58f,.84f,.93f);organic=new(.18f,.27f,.34f);polish=.88f;glow=.18f;break;
                case ArenaVariant.GJ504b:
                    stone=new(.29f,.065f,.18f);edge=new(.58f,.19f,.36f);accent=new(.76f,.36f,.49f);organic=new(.17f,.045f,.12f);glow=0;break;
                default:
                    stone=new(.12f,.14f,.16f);edge=new(.31f,.33f,.35f);accent=new(.43f,.46f,.47f);organic=new(.075f,.085f,.10f);glow=0;break;
            }
            return new[]{Mat("Bedrock",stone,metal,polish),Mat("ExposedEdges",edge,metal,polish),
                Mat("MineralAccents",accent,metal,polish,accent*glow),Mat("Secondary",organic,metal,.22f)};
        }

        void BuildPocket(int seed)
        {
            switch(variant)
            {
                case ArenaVariant.Moon: ImpactPocket(seed); break;
                case ArenaVariant.Mars: SedimentaryPocket(seed,false); break;
                case ArenaVariant.GJ504b: SedimentaryPocket(seed,true); break;
                case ArenaVariant.GreenPlanet: AlienPocket(seed); break;
                case ArenaVariant.Noxara: NoxaraPocket(seed); break;
                case ArenaVariant.Kryon: IcePocket(seed); break;
                case ArenaVariant.Earth: EarthPocket(seed); break;
                case ArenaVariant.Pyraxis: VolcanicPocket(seed); break;
                case ArenaVariant.Neptune: AbyssPocket(seed); break;
                case ArenaVariant.Dark00: IndustrialPocket(seed); break;
                case ArenaVariant.DiamondCore: DiamondPocket(seed); break;
            }
            // Fine scale debris ties the larger silhouettes back into the ground.
            for(int i=0;i<14;i++)
            {
                float a=(i*137.5f+seed*23)*Mathf.Deg2Rad,r=.75f+(i%5)*.40f;
                float size=.08f+(i%4)*.035f;
                Add(rock,i%2,new Vector3(Mathf.Cos(a)*r,.025f,Mathf.Sin(a)*r*.68f),
                    new Vector3(size,.08f,size*1.3f),Quaternion.Euler(0,i*47,8));
            }
        }

        void ImpactPocket(int seed)
        {
            // Broken, low ejecta rim; no painted disc on the combat surface.
            for(int i=0;i<11;i++)
            {
                float a=(i*31f+seed*19)*Mathf.Deg2Rad;
                Add(rock,i%3==0?1:0,new Vector3(Mathf.Cos(a)*1.65f,.06f,Mathf.Sin(a)*1.12f),
                    new Vector3(.46f,.20f+(i%3)*.04f,.29f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,7));
            }
            Add(rock,1,new Vector3(.3f,.10f,.2f),new Vector3(.65f,.33f,.43f),Quaternion.Euler(12,seed*47,22));
        }

        void SedimentaryPocket(int seed,bool magenta)
        {
            // Broad, visibly layered wind-eroded mesas; GJ gets hoodoos, Mars gets slabs.
            for(int tier=0;tier<5;tier++)
            {
                float width=1.15f-tier*.14f;
                Add(rock,tier%2,new Vector3(-.55f,tier*.21f,0),new Vector3(width,.21f,width*.62f),Quaternion.Euler(0,seed*27+8*tier,0));
            }
            if(magenta)
            {
                Add(column,0,new Vector3(1.1f,0,.3f),new Vector3(.56f,1.1f,.55f),Quaternion.Euler(0,15,4));
                Add(rock,1,new Vector3(1.18f,1.1f,.3f),new Vector3(.67f,.16f,.43f),Quaternion.Euler(0,34,0));
            }
            else Add(rock,3,new Vector3(1.25f,.12f,.3f),new Vector3(.72f,.30f,.42f),Quaternion.Euler(0,42,16));
            for(int i=0;i<6;i++)
                Add(rock,2,new Vector3(-1.7f+i*.58f,.01f,-1.25f+(i%2)*.11f),new Vector3(.30f,.035f,.12f),Quaternion.Euler(0,20,0));
        }

        void AlienPocket(int seed)
        {
            Add(rock,0,Vector3.zero,new Vector3(1.2f,.30f,.8f),Quaternion.identity);
            for(int i=0;i<5;i++)
            {
                float a=i*72f+seed*11;
                Quaternion q=Quaternion.Euler(0,a,0);
                Add(leaf,3,q*new Vector3(0,.12f,.28f),new Vector3(.7f,1.05f,.85f),q);
                Vector3 p=q*new Vector3(0,0,.95f);
                Add(column,1,p,new Vector3(.20f,.65f+(i%2)*.25f,.20f),Quaternion.Euler(0,a,12));
                Add(crystal,2,p+Vector3.up*.65f,new Vector3(.23f,.26f,.23f),q);
            }
        }

        void NoxaraPocket(int seed)
        {
            // Tilted slate fans with narrow cool mineral inserts.
            for(int i=0;i<5;i++)
            {
                Vector3 p=new((i-2)*.48f,0,(i%2)*.3f);
                Add(column,i%2,p,new Vector3(.65f,.8f+(i%3)*.32f,.23f),Quaternion.Euler(-14,seed*17,20-i*8));
                Add(crystal,2,p+new Vector3(.12f,.08f,.15f),new Vector3(.10f,.54f,.10f),Quaternion.Euler(0,12,15));
            }
        }

        void IcePocket(int seed)
        {
            for(int i=0;i<4;i++)
            {
                Vector3 p=new((i-1.5f)*.62f,.08f,(i%2)*.3f);
                Quaternion q=Quaternion.Euler(12,seed*23+i*14,(i-2)*12);
                Add(crystal,i%2==0?2:3,p,new Vector3(.90f,.95f+(i%2)*.50f,.43f),q);
                Add(rock,1,p+Vector3.up*.15f,new Vector3(.56f,.12f,.39f),Quaternion.Euler(0,i*23,0));
            }
            Add(rock,1,new Vector3(-.4f,.035f,-.95f),new Vector3(1.5f,.09f,.45f),Quaternion.Euler(0,-12,0));
        }

        void EarthPocket(int seed)
        {
            Add(rock,0,new Vector3(-.5f,.1f,.2f),new Vector3(1,.40f,.66f),Quaternion.Euler(0,seed*41,9));
            Add(rock,3,new Vector3(-.5f,.30f,.2f),new Vector3(.82f,.15f,.55f),Quaternion.Euler(0,seed*41,9));
            // Fern rosettes, individual curved leaflets and a fallen weathered trunk.
            Add(column,0,new Vector3(-1.4f,.13f,-.7f),new Vector3(.28f,2.1f,.28f),Quaternion.Euler(0,78,85));
            for(int plant=0;plant<3;plant++)for(int frond=0;frond<7;frond++)
            {
                Vector3 p=new((plant-1)*1.05f,.03f,plant%2==0?.75f:-.65f);
                float angle=frond*51.4f+seed*25;
                Add(leaf,frond%3==0?2:3,p,new Vector3(.32f,.65f,.55f),Quaternion.Euler(0,angle,0));
            }
        }

        void VolcanicPocket(int seed)
        {
            // Tight basalt column groups and thin hot joints beneath the crust.
            for(int i=0;i<7;i++)
            {
                float a=i*137.5f*Mathf.Deg2Rad,r=i*.17f;
                Vector3 p=new(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                Add(column,i%3==0?1:0,p,new Vector3(.65f,.45f+(i%4)*.30f,.65f),Quaternion.Euler(0,seed*19,0));
            }
            for(int i=0;i<4;i++)
                Add(rock,2,new Vector3(-1.2f+i*.65f,.035f,-.8f+(i%2)*.10f),new Vector3(.40f,.035f,.12f),Quaternion.Euler(0,12,0));
        }

        void AbyssPocket(int seed)
        {
            Add(rock,0,Vector3.zero,new Vector3(1.2f,.25f,.8f),Quaternion.identity);
            // Branching coral rather than another crystal formation.
            for(int stem=0;stem<4;stem++)
            {
                Vector3 p=new((stem-1.5f)*.6f,.06f,(stem%2)*.48f);
                float h=.6f+stem*.18f;
                Add(column,3,p,new Vector3(.21f,h,.21f),Quaternion.Euler(0,0,stem*4-6));
                for(int arm=-1;arm<=1;arm+=2)
                {
                    Add(column,1,p+new Vector3(0,h*.50f,0),new Vector3(.12f,h*.55f,.12f),Quaternion.Euler(0,seed*20,arm*38));
                    Add(rock,2,p+new Vector3(arm*.24f,h*.89f,0),new Vector3(.10f,.09f,.10f),Quaternion.identity);
                }
            }
        }

        void IndustrialPocket(int seed)
        {
            Add(beam,0,new Vector3(-1.3f,0,-.6f),new Vector3(3.0f,.16f,1.7f),Quaternion.Euler(0,45,0));
            for(int i=0;i<3;i++)
            {
                Vector3 p=new(-.65f+i*.65f,.12f,0);
                Add(beam,1,p,new Vector3(.6f,.65f,.85f),Quaternion.Euler(0,45,0));
                for(int rib=0;rib<4;rib++)
                    Add(beam,0,p+new Vector3(0,.13f+rib*.12f,.36f),new Vector3(.54f,.045f,.06f),Quaternion.Euler(0,45,0));
                Add(beam,2,p+new Vector3(.15f,.68f,.05f),new Vector3(.08f,.045f,.36f),Quaternion.Euler(0,45,0));
            }
        }

        void DiamondPocket(int seed)
        {
            // Opaque mineral bed; existing transparent crystal/guardian systems remain authoritative.
            Add(rock,0,Vector3.zero,new Vector3(1.35f,.19f,.92f),Quaternion.identity);
            for(int i=0;i<5;i++)
            {
                Vector3 p=new((i-2)*.47f,.05f,(i%2)*.40f);
                Add(crystal,i%2==0?1:2,p,new Vector3(.43f,.55f+(i%3)*.43f,.43f),Quaternion.Euler(0,seed*29+i*17,(i-2)*12));
            }
            for(int i=0;i<4;i++)
                Add(crystal,3,new Vector3(-1.3f+i*.70f,.10f,-.75f),new Vector3(.20f,.55f,.20f),Quaternion.Euler(68,i*47,12));
        }

        void Add(Mesh mesh,int material,Vector3 offset,Vector3 scale,Quaternion rotation)
        {
            Vector3 world=center+orientation*new Vector3(offset.x,0,offset.z);
            Vector3 position=orientation*offset;
            position.y+=LunarSurfaceField.Surface(world.x,world.z).y;
            batches[material].Add(new CombineInstance { mesh=mesh,transform=Matrix4x4.TRS(position,orientation*rotation,scale) });
        }

        void Flush(int site)
        {
            Transform pocket=new GameObject("DF_"+variant+"_LandscapePocket_"+site).transform;
            pocket.SetParent(scenery,false);pocket.position=center;
            var renderers=new List<Renderer>();
            for(int i=0;i<batches.Length;i++)
            {
                if(batches[i].Count==0)continue;
                Mesh mesh=Own(new Mesh { name="DF_"+variant+"_SceneryBatch_"+site+"_"+i });
                mesh.CombineMeshes(batches[i].ToArray(),true,true);mesh.RecalculateBounds();
                var go=new GameObject(palette[i].name);go.transform.SetParent(pocket,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=palette[i];
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderers.Add(renderer);batches[i].Clear();
            }
            // Regional batches preserve frustum culling. No hundreds of prop GameObjects.
            var lod=pocket.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[]{new LOD(.008f,renderers.ToArray())});lod.RecalculateBounds();
        }

        Material Mat(string name,Color color,float metallic,float smoothness,Color emission=default)
        {
            Material m=RuntimeShaderLibrary.Create(color,false,metallic,smoothness,emission);
            if(m!=null){m.name="DF_"+variant+"_Landscape_"+name;ownedMaterials.Add(m);}return m;
        }
        Mesh Own(Mesh mesh){ownedMeshes.Add(mesh);return mesh;}

        static Mesh Revolved(string name,int sides,Vector2[] rings)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Vector3 Point(int ring,int side)
            {float a=side*Mathf.PI*2/sides;return new Vector3(Mathf.Cos(a)*rings[ring].x,rings[ring].y,Mathf.Sin(a)*rings[ring].x);}
            for(int ring=0;ring<rings.Length-1;ring++)for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;
                Face(vertices,triangles,Point(ring,side),Point(ring+1,side),Point(ring+1,next));
                Face(vertices,triangles,Point(ring,side),Point(ring+1,next),Point(ring,next));
            }
            for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides,last=rings.Length-1;
                Face(vertices,triangles,new Vector3(0,rings[0].y,0),Point(0,side),Point(0,next));
                Face(vertices,triangles,new Vector3(0,rings[last].y,0),Point(last,next),Point(last,side));
            }
            return Finish(name,vertices,triangles);
        }

        static Mesh Leaf()
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<5;i++)
            {
                float a=i/5f,b=(i+1)/5f;
                Vector3 ca=new(0,Mathf.Sin(a*Mathf.PI*.8f)*.55f,a),cb=new(0,Mathf.Sin(b*Mathf.PI*.8f)*.55f,b);
                float wa=Mathf.Sin(a*Mathf.PI)*.24f,wb=Mathf.Sin(b*Mathf.PI)*.24f;
                foreach(int side in new[]{-1,1})
                {
                    Vector3 ea=ca+new Vector3(side*wa,-.04f,0),eb=cb+new Vector3(side*wb,-.04f,0);
                    Face(v,t,ca,cb,eb);Face(v,t,ca,eb,ea);
                    Face(v,t,eb,cb,ca);Face(v,t,ea,eb,ca);
                }
            }
            return Finish("CurvedDoubleSidedFrond",v,t);
        }
        static void Face(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c)
        {int n=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(n);t.Add(n+1);t.Add(n+2);}
        static Mesh Finish(string name,List<Vector3> vertices,List<int> triangles)
        {var m=new Mesh{name=name};m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
        void OnDestroy()
        {
            if(scenery!=null)Destroy(scenery.gameObject);
            foreach(Mesh mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);
            foreach(Material material in ownedMaterials)if(material!=null)Destroy(material);
        }
    }
}
