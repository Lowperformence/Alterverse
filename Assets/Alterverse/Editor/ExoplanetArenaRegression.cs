#if UNITY_EDITOR
using System;
using System.Reflection;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class ExoplanetArenaRegression
    {
        static readonly ArenaVariant[] Variants={ArenaVariant.Wasp76b,ArenaVariant.Trappist1e};

        [MenuItem("DIMENSIONS FRONT/Checks/Exoplanet catalogue and Moon geometry")]
        public static void RunData()
        {
            ArenaVariant previous=ArenaVariantRuntime.Current;
            try
            {
                Require((int)ArenaVariant.GJ504b==10&&(int)ArenaVariant.Wasp76b==11&&(int)ArenaVariant.Trappist1e==12,"Stable arena IDs changed");
                foreach(ArenaVariant variant in Variants)
                {
                    int entries=0;foreach(var item in ArenaShopCatalog.Items)if(item.Variant==variant)entries++;
                    Require(entries==1&&ArenaShopCatalog.For(variant).Price==0&&ArenaVariantRuntime.CanSelect(variant),"Catalogue/selection missing: "+variant);
                    _=PlanetSurfaceFinishProfile.For(variant);_=PlanetSurfaceMicroProfile.Ground(variant);
                    for(int layer=0;layer<4;layer++)_=PlanetSceneryMaterials.LandscapeFinish(variant,layer);
                    foreach(string suffix in new[]{"Macro","Ground","Stone"})
                        Require(Resources.Load<Texture2D>("PlanetSurfaces/"+ExoplanetArenaProfile.SurfaceResource(variant)+"/"+suffix)!=null,"Surface dependency missing");
                    Require(DailyQuestProgress.AddWonMap(0,true,variant)==1<<(int)variant,"New map win not tracked");
                    for(int x=-40;x<=40;x+=8)for(int z=-32;z<=32;z+=8)
                    {
                        ArenaVariantRuntime.BeginCoopMatch(ArenaVariant.Moon);
                        Vector3 moon=LunarSurfaceField.Surface(x,z);
                        ArenaVariantRuntime.BeginCoopMatch(variant);
                        Require((moon-LunarSurfaceField.Surface(x,z)).sqrMagnitude<.00000001f,"Moon geometry changed: "+variant);
                        Color a=ExoplanetArenaProfile.GroundColor(variant,x,z,.4f),b=ExoplanetArenaProfile.GroundColor(variant,-x,z,.4f);
                        Require(a==b,"Biome bands differ between the two team ends");
                    }
                }
                Color warm=ExoplanetArenaProfile.GroundColor(ArenaVariant.Trappist1e,12,32,.4f);
                Color cold=ExoplanetArenaProfile.GroundColor(ArenaVariant.Trappist1e,12,-32,.4f);
                Require(cold.b>warm.b&&cold.g>warm.g,"Warm/frozen habitat bands missing");
                Debug.Log("DIMENSIONS FRONT: exoplanet IDs, selection, surface resources, win tracking and Moon geometry passed.");
            }
            finally{ArenaVariantRuntime.BeginCoopMatch(previous);}
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Exoplanet monster bodies and attack binding (Play Mode)")]
        public static void RunBodies()
        {
            Require(Application.isPlaying,"Enter Play Mode first.");
            foreach(ArenaVariant variant in Variants)for(int team=0;team<2;team++)
            {
                GameObject host=new("DF_ExoplanetBodyRegression");
                try
                {
                    host.AddComponent<Health>().Configure(680,true);
                    var visual=host.AddComponent<ExoplanetHotZoneVisual>();visual.Build(variant,team);
                    int count=host.GetComponentsInChildren<MeshRenderer>().Length;
                    Require(count>0&&count<=24&&visual.Muzzle!=null,"Monster body/socket missing or over batch budget");
                    visual.Build(variant,team);
                    Require(host.GetComponentsInChildren<MeshRenderer>().Length==count,"Duplicate visual build");
                    Require(host.GetComponentsInChildren<Collider>().Length==0&&host.GetComponentsInChildren<Light>().Length==0,"Visual changed gameplay collision or lighting");
                    int vertices=0;
                    foreach(var filter in host.GetComponentsInChildren<MeshFilter>())
                    {
                        Mesh mesh=filter.sharedMesh;vertices+=mesh.vertexCount;
                        Require(mesh.subMeshCount==1&&mesh.normals.Length==mesh.vertexCount,"Unfinished body mesh");
                        foreach(Vector3 normal in mesh.normals)
                            Require(float.IsFinite(normal.x)&&float.IsFinite(normal.y)&&float.IsFinite(normal.z)&&normal.sqrMagnitude>.5f,"Invalid body normal");
                        Material material=filter.GetComponent<Renderer>().sharedMaterial;
                        Require(material!=null&&material.shader!=null&&material.shader.isSupported,"Body shader unavailable");
                    }
                    Require(vertices<24000,"Monster vertex budget exceeded");
                    var gesture=host.AddComponent<CreatureAttackMotion>();gesture.Begin(.22f,Vector3.forward*4);
                    Require(gesture.WindingUp&&typeof(CreatureAttackMotion).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(gesture)!=null,"Attack event cannot bind creature body");
                }
                finally{host.SetActive(false);UnityEngine.Object.Destroy(host);}
            }
            Debug.Log("DIMENSIONS FRONT: both exoplanet teams' bodies, batch budget, normals, shaders, duplicate guard and attack binding passed.");
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Exoplanet habitat guards (Play Mode)")]
        public static void RunGuards()
        {
            Require(Application.isPlaying,"Enter Play Mode first.");
            ArenaVariant previous=ArenaVariantRuntime.Current;
            try
            {
                foreach(ArenaVariant variant in Variants)for(int team=0;team<2;team++)
                {
                    ArenaVariantRuntime.BeginCoopMatch(variant);
                    GameObject host=HealingGuardMech.Spawn(team,new Vector3(team==0?-27.5f:27.5f,.14f,0));
                    try
                    {
                        Require(host.GetComponent<Health>().Max==520f,"Guard health changed");
                        CapsuleCollider collider=host.GetComponent<CapsuleCollider>();
                        Require(collider.radius==.72f&&collider.height==2.05f,"Guard hitbox changed");
                        Require(host.GetComponent<LaneUnitVisualMotion>()!=null,"Guard locomotion/attack rig missing");
                        var appearance=host.GetComponent<ExoplanetGuardAppearance>();
                        Require(appearance!=null&&appearance.Variant==variant,"Habitat guard appearance missing");
                        int count=host.GetComponentsInChildren<MeshRenderer>().Length;
                        ExoplanetGuardAppearance.Apply(host.transform,variant,team);
                        Require(host.GetComponentsInChildren<MeshRenderer>().Length==count,"Duplicate habitat details");
                        int activeColliders=0;foreach(var c in host.GetComponentsInChildren<Collider>())if(c.enabled)activeColliders++;
                        Require(activeColliders==1&&host.GetComponentsInChildren<Light>().Length==0,"Guard art adds collision or lights");
                        int habitatBatches=0;
                        foreach(var filter in host.GetComponentsInChildren<MeshFilter>())
                        {
                            if(!filter.name.StartsWith("DF_IronGuardHeatShroud_")&&!filter.name.StartsWith("DF_TwilightGuardGillArmor_")&&!filter.name.StartsWith("DF_"+variant+"GuardHabitatSeams_"))continue;
                            habitatBatches++;
                            Require(filter.sharedMesh!=null&&filter.sharedMesh.vertexCount<2000,"Guard detail batch invalid");
                            Require(filter.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"Guard detail shader unsupported");
                        }
                        Require(habitatBatches==4,"Guard shrouds/gills or arm seams missing");
                    }
                    finally{host.SetActive(false);UnityEngine.Object.Destroy(host);}
                }
                Debug.Log("DIMENSIONS FRONT: both habitat guards preserve health, hitbox, articulated motion and their four detail batches.");
            }
            finally{ArenaVariantRuntime.BeginCoopMatch(previous);}
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
