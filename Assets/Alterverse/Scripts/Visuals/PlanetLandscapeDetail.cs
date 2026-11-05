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
        Mesh rock, column, crystal, leaf, beam, canopy, pool, alienCap;
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

        SceneryBuildingClearance clearance;
        void Start()=>Build();

        public void Build()
        {
            if(scenery!=null)return;
            variant = ArenaVariantRuntime.Current;
            clearance=new SceneryBuildingClearance();
            scenery = new GameObject("DF_PlanetLandscape_" + variant).transform;
            scenery.SetParent(transform, false);
            rock = Own(LunarFragmentGeometry.Create(83));
            Vector2[] columnProfile={new(.48f,0),new(.51f,.12f),new(.37f,.68f),new(.30f,1)};
            column = Own(variant==ArenaVariant.Pyraxis||variant==ArenaVariant.Noxara
                ?Revolved("FracturedColumn",7,columnProfile)
                :SceneryMeshQuality.SoftProfile("ErodedColumn",16,columnProfile));
            crystal = Own(Revolved("CutPrism", 6, new[] {
                new Vector2(.18f,0), new Vector2(.43f,.20f), new Vector2(.35f,.78f), new Vector2(.025f,1) }));
            leaf = Own(SceneryMeshQuality.CurvedFrond());
            beam = Own(SceneryMeshQuality.RoundedBeam());
            if(variant==ArenaVariant.Trappist1e)pool=Own(ProductionMeshFactory.Disc(.5f,.012f,24));
            canopy = Own(SceneryMeshQuality.SoftProfile("RoundedCanopy", 16, new[] {
                new Vector2(.16f,0), new Vector2(.38f,.10f), new Vector2(.50f,.36f),
                new Vector2(.37f,.72f), new Vector2(.16f,.94f), new Vector2(.015f,1f) }));
            if(variant==ArenaVariant.GreenPlanet)
                alienCap=Own(SceneryMeshQuality.SoftProfile("AlienSporeCrown",20,new[]{
                    new Vector2(.06f,0),new Vector2(.17f,.10f),new Vector2(.53f,.18f),
                    new Vector2(.58f,.26f),new Vector2(.31f,.40f),new Vector2(.015f,.48f)}));
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
            foreach(Mesh input in new[]{rock,column,crystal,leaf,beam,canopy,pool,alienCap})
            { if(input==null)continue;ownedMeshes.Remove(input); Destroy(input); }
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
                    stone=new(.16f,.23f,.22f);edge=new(.38f,.45f,.31f);accent=new(.37f,.67f,.73f);organic=new(.18f,.38f,.31f);glow=.08f;break;
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
                    stone=new(.31f,.075f,.20f);edge=new(.58f,.20f,.38f);accent=new(.88f,.26f,.54f);organic=new(.19f,.048f,.13f);polish=.28f;glow=.26f;break;
                case ArenaVariant.Wasp76b:
                    stone=new(.21f,.23f,.25f);edge=new(.56f,.30f,.14f);accent=new(.95f,.42f,.07f);organic=new(.28f,.19f,.13f);metal=.62f;polish=.34f;glow=.42f;break;
                case ArenaVariant.Trappist1e:
                    stone=new(.095f,.21f,.23f);edge=new(.54f,.69f,.75f);accent=new(.13f,.78f,.68f);organic=new(.22f,.18f,.34f);polish=.40f;glow=.22f;break;
                case ArenaVariant.Toi715b:
                    stone=new(.45f,.15f,.09f);edge=new(.20f,.075f,.055f);accent=new(.84f,.77f,.62f);organic=new(.16f,.12f,.19f);polish=.20f;glow=0;break;
                default:
                    stone=new(.12f,.14f,.16f);edge=new(.31f,.33f,.35f);accent=new(.43f,.46f,.47f);organic=new(.075f,.085f,.10f);glow=0;break;
            }
            Material[] result={Mat("Bedrock",stone,metal,polish),Mat("ExposedEdges",edge,metal,polish),
                Mat("MineralAccents",accent,metal,polish,accent*glow),Mat("Secondary",organic,metal,.22f)};
            for(int i=0;i<result.Length;i++)
                PlanetSceneryMaterials.Apply(result[i],variant,PlanetSceneryMaterials.LandscapeFinish(variant,i));
            if(variant==ArenaVariant.Trappist1e)result[0].SetFloat("_Smoothness",.72f);
            return result;
        }

        void BuildPocket(int seed)
        {
            BuildGroundTransition(seed);
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
                case ArenaVariant.Wasp76b: IronPocket(seed); break;
                case ArenaVariant.Trappist1e: TwilightPocket(seed); break;
                case ArenaVariant.Toi715b: IvoryGrovePocket(seed); break;
            }
            // Fine scale debris ties the larger silhouettes back into the ground.
            for(int i=0;i<14;i++)
            {
                float a=(i*137.5f+seed*23)*Mathf.Deg2Rad,r=1.05f+(i%5)*.43f;
                float size=.12f+(i%4)*.04f;
                // Keep rubble mineral: layer 1 is bark on the forest variants.
                int material=(variant==ArenaVariant.Earth||variant==ArenaVariant.Toi715b)?0:i%2;
                Add(rock,material,new Vector3(Mathf.Cos(a)*r,.025f,Mathf.Sin(a)*r*.68f),
                    new Vector3(size,.09f,size*1.3f),Quaternion.Euler(0,i*47,8));
            }
        }

        // Low overlapping shelves connect props to the surface instead of leaving isolated
        // silhouettes on a flat floor. Each piece uses the same terrain sampler and clearance
        // as the larger props, and joins an existing opaque batch (no decals or extra draws).
        void BuildGroundTransition(int seed)
        {
            for(int shelf=0;shelf<6;shelf++)
            {
                float angle=(shelf*60f+seed*23f)*Mathf.Deg2Rad;
                float radius=1.08f+(shelf%2)*.25f;
                Add(rock,0,new Vector3(Mathf.Cos(angle)*radius,-.025f,Mathf.Sin(angle)*radius*.62f),
                    new Vector3(.72f+(shelf%3)*.12f,.12f+(shelf%2)*.045f,.42f),
                    Quaternion.Euler(0,-angle*Mathf.Rad2Deg+seed*11f,(shelf%2==0?1:-1)*4f));
            }
            // Broken mineral ribbons remain low and sparse, readable from the gameplay camera.
            // Organic and industrial variants get their own root/panel treatment below.
            bool organic=variant==ArenaVariant.Earth||variant==ArenaVariant.GreenPlanet||
                variant==ArenaVariant.Neptune||variant==ArenaVariant.Trappist1e||variant==ArenaVariant.Toi715b;
            if(organic||variant==ArenaVariant.Dark00)return;
            int layer=variant==ArenaVariant.Pyraxis||variant==ArenaVariant.Wasp76b?2:1;
            for(int segment=0;segment<5;segment++)
            {
                float x=-1.7f+segment*.61f;
                float z=-1.20f+Mathf.Sin(segment*1.9f+seed)*.14f;
                Add(rock,layer,new Vector3(x,.018f,z),new Vector3(.42f,.055f,.16f),
                    Quaternion.Euler(0,seed*13f+segment*19f,0));
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
            // Three unfamiliar growth forms in each paired outer pocket. No gameplay components.
            Add(rock,0,Vector3.zero,new Vector3(1.2f,.15f,.8f),Quaternion.identity);
            for(int plant=0;plant<3;plant++)
            {
                Vector3 p=new Vector3((plant-1)*1.05f,.07f,plant==1?.48f:-.42f);
                float yaw=seed*37+plant*71;
                switch((plant+seed)%3)
                {
                    case 0:
                        // Corkscrew tendril with a suspended blue seed lantern.
                        Vector3 tip=p;
                        for(int step=0;step<9;step++)
                        {
                            float a=(yaw+step*43)*Mathf.Deg2Rad,b=(yaw+(step+1)*43)*Mathf.Deg2Rad;
                            Vector3 from=p+new Vector3(Mathf.Cos(a)*.16f,step*.16f,Mathf.Sin(a)*.16f);
                            Vector3 to=p+new Vector3(Mathf.Cos(b)*.16f,(step+1)*.16f,Mathf.Sin(b)*.16f);
                            Vector3 direction=to-from;
                            Add(column,3,from,new Vector3(.09f,direction.magnitude,.09f),Quaternion.FromToRotation(Vector3.up,direction));
                            if(step%3==0)Add(leaf,3,from,new Vector3(.38f,.50f,.56f),Quaternion.Euler(-22,yaw+step*43,32));
                            tip=to;
                        }
                        Add(canopy,2,tip,new Vector3(.60f,.72f,.60f),Quaternion.Euler(0,yaw,12));
                        break;
                    case 1:
                        // Split fan rosette: broad curled leaves with pale seed beads.
                        for(int frond=0;frond<7;frond++)
                        {
                            Quaternion q=Quaternion.Euler(-18+(frond%2)*14,yaw+frond*137.5f,(frond%2==0?1:-1)*18);
                            Add(leaf,3,p+Vector3.up*(frond%3)*.13f,new Vector3(.85f,1.1f,1.05f),q);
                            if(frond%2==0)Add(canopy,2,p+q*new Vector3(0,.35f,.66f),new Vector3(.17f,.23f,.17f),q);
                        }
                        break;
                    default:
                        // Cluster of flared spore crowns, with hanging fronds instead of Earth shrubs.
                        for(int stalk=0;stalk<3;stalk++)
                        {
                            Vector3 basePoint=p+new Vector3((stalk-1)*.26f,0,(stalk%2)*.20f);
                            float height=.68f+stalk*.22f;
                            Add(column,3,basePoint,new Vector3(.15f,height,.15f),Quaternion.Euler(0,yaw,(stalk-1)*8));
                            Add(alienCap,stalk%2==0?3:2,basePoint+Vector3.up*height,new Vector3(.70f,.75f,.70f),Quaternion.Euler(0,yaw,4));
                            Add(leaf,3,basePoint+Vector3.up*.35f,new Vector3(.36f,.65f,.62f),Quaternion.Euler(38,yaw+stalk*115,28));
                        }
                        break;
                }
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
            // Compact forest pockets: tapered trunks, angled limbs and layered crowns read as
            // Earth trees from the gameplay camera while leaving the lanes and bases open.
            Add(rock,0,new Vector3(-.5f,.04f,.2f),new Vector3(1.3f,.12f,.8f),Quaternion.Euler(0,seed*41,4));
            for(int tree=0;tree<3;tree++)
            {
                float x=(tree-1)*.92f;
                float z=tree==1?.22f:-.22f;
                float height=tree==1?2.45f:1.95f+(tree%2)*.16f;
                float yaw=seed*29+tree*47;
                Add(column,1,new Vector3(x,.03f,z),new Vector3(.22f,height,.22f),Quaternion.Euler(0,yaw,tree==0?-2:2));
                // Broad buttress roots attach each trunk to the ground without another renderer.
                for(int root=0;root<3;root++)
                {
                    float a=(yaw+root*120f)*Mathf.Deg2Rad;
                    Vector3 end=new Vector3(x+Mathf.Cos(a)*.49f,.025f,z+Mathf.Sin(a)*.49f);
                    Vector3 direction=new Vector3(x,.29f,z)-end;
                    Add(column,1,end,new Vector3(.14f,direction.magnitude,.14f),Quaternion.FromToRotation(Vector3.up,direction));
                }

                for(int branch=0;branch<3;branch++)
                {
                    float angle=(yaw+branch*120f)*Mathf.Deg2Rad;
                    Vector3 direction=new(Mathf.Sin(angle)*.55f,.84f,Mathf.Cos(angle)*.55f);
                    Vector3 pivot=new Vector3(x,.70f+height*.22f,z);
                    Add(column,1,pivot,new Vector3(.085f,.72f,.085f),Quaternion.FromToRotation(Vector3.up,direction));
                }

                int foliage=tree==1?2:3;
                Add(canopy,foliage,new Vector3(x,.91f+height*.12f,z),new Vector3(.78f,.84f,.73f),Quaternion.Euler(0,yaw,2));
                Add(canopy,3,new Vector3(x-.18f,1.30f+height*.12f,z+.04f),new Vector3(.61f,.72f,.58f),Quaternion.Euler(0,yaw+53,-4));
                Add(canopy,foliage,new Vector3(x+.18f,1.28f+height*.12f,z-.06f),new Vector3(.60f,.70f,.57f),Quaternion.Euler(0,yaw-61,4));

                // A few broad, curved leaf sprays break up the crown silhouette at close range.
                for(int frond=0;frond<4;frond++)
                    Add(leaf,foliage,new Vector3(x,.96f+height*.12f,z),new Vector3(.23f,.54f,.48f),Quaternion.Euler(-10, yaw+frond*90f, (frond%2==0?1:-1)*24f));
            }

            // Low native shrubs blend the trunks into the existing Earth ground palette.
            for(int shrub=0;shrub<5;shrub++)
            {
                float x=-1.65f+shrub*.82f;
                if(shrub==2)continue;
                Add(canopy,3,new Vector3(x,.02f,shrub%2==0?.96f:-.92f),
                    new Vector3(.38f,.43f,.34f),Quaternion.Euler(0,seed*17+shrub*71,0));
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
            // Staggered service plates and exposed rails break up the monolithic base.
            for(int plate=0;plate<4;plate++)
            {
                Vector3 p=new(-1.05f+plate*.62f,.025f,-1.05f+(plate%2)*.13f);
                Add(beam,1,p,new Vector3(.57f,.055f,.44f),Quaternion.Euler(0,45,0));
                Add(beam,0,p+new Vector3(0,.065f,.12f),new Vector3(.51f,.035f,.045f),Quaternion.Euler(0,45,0));
            }
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

        void IvoryGrovePocket(int seed)
        {
            // Low rust shelves, ivory fan crowns and dark ground cover.
            // The existing Add() anchors every plant and filters building clearance.
            for(int tier=0;tier<3;tier++)
                Add(rock,0,new Vector3(-.60f,tier*.16f,.30f),new Vector3(1.12f-tier*.18f,.18f,.68f),Quaternion.Euler(0,seed*19+tier*13,0));
            for(int plant=0;plant<5;plant++)
            {
                float a=(plant*137.5f+seed*31f)*Mathf.Deg2Rad;
                Vector3 p=new(Mathf.Cos(a)*1.50f,.025f,Mathf.Sin(a)*.92f);
                float height=.55f+(plant%3)*.20f;
                Add(column,1,p,new Vector3(.18f,height,.18f),Quaternion.Euler(0,plant*41,8));
                for(int frond=0;frond<5;frond++)
                    Add(leaf,2,p+Vector3.up*height*.73f,new Vector3(.56f,.70f,.50f),
                        Quaternion.Euler(-28,plant*41+frond*72,18));
                // Dark lobed shrubs beneath the brighter canopy.
                for(int lobe=0;lobe<3;lobe++)
                    Add(canopy,3,p+new Vector3((lobe-1)*.17f,0,-.28f),new Vector3(.30f,.25f+(lobe%2)*.10f,.27f),Quaternion.Euler(0,plant*31+lobe*47,0));
            }
        }

        void IronPocket(int seed)
        {
            Add(rock,0,Vector3.zero,new Vector3(1.35f,.24f,.90f),Quaternion.Euler(0,seed*27,0));
            for(int i=0;i<4;i++)
            {
                Vector3 p=new((i-1.5f)*.66f,.04f,(i%2)*.25f);
                Add(crystal,i%2==0?0:1,p,new Vector3(.49f,.72f+(i%3)*.32f,.42f),Quaternion.Euler(0,seed*31+i*23,(i-2)*9));
                Add(beam,3,p+Vector3.up*.25f,new Vector3(.59f,.07f,.45f),Quaternion.Euler(0,seed*31+i*23,8));
                Add(rock,2,p+new Vector3(.15f,.015f,-.53f),new Vector3(.37f,.045f,.10f),Quaternion.Euler(0,i*31,0));
            }
        }

        void TwilightPocket(int seed)
        {
            bool cold=center.z<0;
            Add(rock,0,Vector3.zero,new Vector3(1.40f,.15f,.94f),Quaternion.identity);
            Add(pool,cold?1:0,new Vector3(-.82f,.065f,-.78f),new Vector3(2.10f,1f,1.26f),Quaternion.Euler(0,seed*19,0));
            for(int i=0;i<5;i++)
            {
                Quaternion q=Quaternion.Euler(0,i*72+seed*17,0);
                Vector3 p=q*new Vector3(0,.05f,.66f);
                if(cold)
                    Add(crystal,1,p,new Vector3(.32f,.55f+(i%3)*.25f,.32f),q*Quaternion.Euler(9,0,12));
                else
                {
                    Add(leaf,3,p,new Vector3(.72f,.90f+(i%2)*.20f,.72f),q);
                    Add(canopy,3,p+Vector3.up*.10f,new Vector3(.45f,.62f,.45f),q);
                }
                Add(canopy,2,p+Vector3.up*.09f,new Vector3(.17f,.35f,.17f),q);
            }
            Add(rock,1,new Vector3(-.65f,.03f,-.95f),new Vector3(.83f,.07f,.38f),Quaternion.Euler(0,23,0));
        }

        void Add(Mesh mesh,int material,Vector3 offset,Vector3 scale,Quaternion rotation)
        {
            Vector3 world=center+orientation*new Vector3(offset.x,0,offset.z);
            Vector3 position=orientation*offset;
            position.y+=LunarSurfaceField.Surface(world.x,world.z).y;
            Matrix4x4 placement=Matrix4x4.TRS(position,orientation*rotation,scale);
            if(clearance.Blocks(mesh,Matrix4x4.Translate(center)*placement))return;
            batches[material].Add(new CombineInstance { mesh=mesh,transform=placement });
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
                // The existing regional batches now receive the arena key and ground contact shading.
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
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





