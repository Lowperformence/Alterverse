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
    public static class Toi715bArenaRegression
    {
        const ArenaVariant Variant=ArenaVariant.Toi715b;

        [MenuItem("DIMENSIONS FRONT/Checks/TOI-715 b - catalogue and surface")]
        public static void RunData()
        {
            ArenaVariant previous=ArenaVariantRuntime.Current;
            try
            {
                Require((int)ArenaVariant.Trappist1e==12&&(int)Variant==13,"Saved/network map IDs changed");
                int entries=0;foreach(var item in ArenaShopCatalog.Items)if(item.Variant==Variant)entries++;
                Require(entries==1&&ArenaShopCatalog.For(Variant).Price==0&&ArenaVariantRuntime.CanSelect(Variant),"Free PLAY selection missing");
                Require(ExoplanetArenaProfile.MonsterName(Variant)=="RUBROX","Hotzone HUD name missing");
                Require(DailyQuestProgress.AddWonMap(0,true,Variant)==8192,"TOI map win missing");
                Require(DailyQuestProgress.CountWonMaps(16383)==14,"All-map mask incorrect");
                _=PlanetSurfaceFinishProfile.For(Variant);_=PlanetSurfaceMicroProfile.Ground(Variant);
                for(int layer=0;layer<4;layer++)_=PlanetSceneryMaterials.LandscapeFinish(Variant,layer);
                foreach(string suffix in new[]{"Macro","Ground","Stone"})
                    Require(Resources.Load<Texture2D>("PlanetSurfaces/"+ExoplanetArenaProfile.SurfaceResource(Variant)+"/"+suffix)!=null,"Surface resource missing");
                for(int x=-40;x<=40;x+=8)for(int z=-32;z<=32;z+=8)
                {
                    ArenaVariantRuntime.BeginCoopMatch(ArenaVariant.Moon);
                    Vector3 moon=LunarSurfaceField.Surface(x,z);
                    ArenaVariantRuntime.BeginCoopMatch(Variant);
                    Require(ArenaVariantRuntime.IsExoplanet,"Shared map/monster dispatch missing");
                    Require((moon-LunarSurfaceField.Surface(x,z)).sqrMagnitude<.00000001f,"Shared geometry differs");
                    Color a=ExoplanetArenaProfile.GroundColor(Variant,x,z,.4f),b=ExoplanetArenaProfile.GroundColor(Variant,-x,z,.4f);
                    Require(a==b&&a.r>a.b,"Rust biome or mirrored team treatment missing");
                }
                Debug.Log("TOI-715 b: selection, stable IDs, resources, win mask and shared surface passed.");
            }
            finally{ArenaVariantRuntime.BeginCoopMatch(previous);}
        }

        [MenuItem("DIMENSIONS FRONT/Checks/TOI-715 b - art and attack binding (Play Mode)")]
        public static void RunArt()
        {
            Require(Application.isPlaying,"Enter Play Mode first.");
            ArenaVariant previous=ArenaVariantRuntime.Current;
            GameObject landscape=null;
            try
            {
                ArenaVariantRuntime.BeginCoopMatch(Variant);
                for(int team=0;team<2;team++)
                {
                    GameObject host=new("DF_Toi715bMonsterCheck");
                    try
                    {
                        host.AddComponent<Health>().Configure(476,true);
                        var visual=host.AddComponent<ExoplanetHotZoneVisual>();visual.Build(Variant,team);
                        Require(MonsterAttackVfx.StyleFor(host)==MonsterAttackStyle.Bio,"Rubrox received a machine-gun attack style");
                        int count=host.GetComponentsInChildren<MeshRenderer>().Length;
                        Require(visual.Muzzle!=null&&visual.Muzzle.name=="DF_RubroxMuzzle"&&count>0&&count<=24,"Rubrox body/socket/budget invalid");
                        visual.Build(Variant,team);
                        Require(host.GetComponentsInChildren<MeshRenderer>().Length==count,"Duplicate monster build");
                        CheckVisual(host,24000);
                        var attack=host.AddComponent<CreatureAttackMotion>();attack.Begin(.22f,Vector3.forward*4);
                        Transform body=(Transform)typeof(CreatureAttackMotion).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(attack);
                        Require(attack.WindingUp&&body!=null&&body.name=="DF_RubroxBody","Rubrox attack event not bound");
                    }
                    finally{host.SetActive(false);UnityEngine.Object.Destroy(host);}
                }
                landscape=new GameObject("DF_Toi715bLandscapeCheck");
                var scenery=landscape.AddComponent<PlanetLandscapeDetail>();scenery.Build();
                int batches=landscape.GetComponentsInChildren<MeshRenderer>().Length;
                Require(batches>0&&batches<=32,"Vegetation batches missing/over budget");
                scenery.Build();Require(landscape.GetComponentsInChildren<MeshRenderer>().Length==batches,"Duplicate scenery build");
                CheckVisual(landscape,100000);
                Debug.Log("TOI-715 b: both team monsters, sockets, attack binding, vegetation meshes and visual-only budgets passed.");
            }
            finally
            {
                if(landscape!=null){landscape.SetActive(false);UnityEngine.Object.Destroy(landscape);}
                ArenaVariantRuntime.BeginCoopMatch(previous);
            }
        }

        static void CheckVisual(GameObject host,int budget)
        {
            Require(host.GetComponentsInChildren<Collider>().Length==0&&host.GetComponentsInChildren<Light>().Length==0,"Art added gameplay colliders/lights");
            int vertices=0;
            foreach(var filter in host.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh=filter.sharedMesh;Require(mesh!=null&&mesh.vertexCount>0,"Empty visual mesh");vertices+=mesh.vertexCount;
                Require(mesh.normals.Length==mesh.vertexCount,"Incomplete normals");
                foreach(Vector3 n in mesh.normals)Require(float.IsFinite(n.x)&&float.IsFinite(n.y)&&float.IsFinite(n.z)&&n.sqrMagnitude>.5f,"Invalid normal");
                Material m=filter.GetComponent<Renderer>().sharedMaterial;
                Require(m!=null&&m.shader!=null&&m.shader.isSupported,"Missing/unsupported shader");
            }
            Require(vertices<budget,"Visual vertex budget exceeded");
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
