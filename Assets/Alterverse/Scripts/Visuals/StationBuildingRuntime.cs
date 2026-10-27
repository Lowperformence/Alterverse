using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Presentation adapter only. Existing structures retain every gameplay component and pivot.</summary>
    [DefaultExecutionOrder(1300)]
    public sealed class StationBuildingRuntime : MonoBehaviour
    {
        [Serializable] sealed class Group
        {
            public string material,mount;
            public float[] vertices,normals,uv;
            public int[] triangles;
        }
        [Serializable] sealed class Model {public string name;public Group[] groups;}
        [Serializable] sealed class Library {public int version;public Model[] models;}
        sealed class RenderGroup {public string material,mount;public Mesh mesh;}
        static readonly Dictionary<string,RenderGroup[]> models=new();
        static bool attempted;
        readonly HashSet<int> completed=new();
        GameObject world;
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach(var groups in models.Values)foreach(var group in groups)if(group.mesh!=null)Destroy(group.mesh);
            models.Clear();attempted=false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
#if !UNITY_SERVER
            if(DedicatedServerNetworkRuntime.Requested||Application.isBatchMode||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            if(FindAnyObjectByType<StationBuildingRuntime>()!=null)return;
            var host=new GameObject("DF_StationBuildingRuntime");DontDestroyOnLoad(host);host.AddComponent<StationBuildingRuntime>();
#endif
        }
        void Update()
        {
            if(DedicatedServerNetworkRuntime.Requested){enabled=false;return;}
            if(world==null||!world.activeInHierarchy)
            {
                var current=GameObject.Find("DF_MatchWorld");
                if(current!=world){world=current;completed.Clear();nextScan=0f;}
            }
            if(world!=null&&world.activeInHierarchy&&completed.Count>=15)return;
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.20f;
            if(world==null||!Load())return;
            // Only visual component owners are discovered. No actor, physics or network state is written.
            foreach(var source in world.GetComponentsInChildren<RealityCoreShell>(true))TryApply(source,"core","DF_RealityCoreProduction");
            foreach(var source in world.GetComponentsInChildren<ProductionBaseEnvironment>(true))TryApply(source,"deck",null);
            foreach(var source in world.GetComponentsInChildren<DefenseBunker>(true))TryApply(source,"bunker","DF_DefenseBunkerVisual");
            foreach(var source in world.GetComponentsInChildren<DefenseTrench>(true))TryApply(source,"trench","DF_DefenseTrenchVisual");
            foreach(var source in world.GetComponentsInChildren<HealingZoneMarker>(true))TryApply(source,"healpad","DF_SpawnHealStationProduction");
            foreach(var source in world.GetComponentsInChildren<CentralHealingPickup>(true))TryApply(source,"healnode","DF_HealStation");
        }

        static bool Load()
        {
            if(attempted)return models.Count>0;
            attempted=true;
            var source=Resources.Load<TextAsset>("StationKit/StationBuildings");
            if(source==null){Debug.LogWarning("DIMENSIONS FRONT: Station Kit mesh library missing; retaining existing building visuals.");return false;}
            var built=new List<Mesh>();
            try
            {
                using var bytes=new MemoryStream(source.bytes);
                using var gzip=new GZipStream(bytes,CompressionMode.Decompress);
                using var reader=new StreamReader(gzip);
                var library=JsonUtility.FromJson<Library>(reader.ReadToEnd());
                if(library==null||library.version!=1||library.models==null||library.models.Length!=6)throw new InvalidOperationException("Invalid Station Kit library");
                var prepared=new Dictionary<string,RenderGroup[]>();
                foreach(var model in library.models)
                {
                    if(string.IsNullOrEmpty(model.name)||model.groups==null||model.groups.Length==0)throw new InvalidOperationException("Missing model groups");
                    var groups=new RenderGroup[model.groups.Length];
                    for(int j=0;j<groups.Length;j++)
                    {
                        var part=model.groups[j];
                        if(part.vertices==null||part.vertices.Length%3!=0||part.normals==null||part.normals.Length!=part.vertices.Length||part.uv==null||part.uv.Length!=part.vertices.Length/3*2||part.triangles==null||part.triangles.Length%3!=0)throw new InvalidOperationException("Invalid mesh arrays");
                        if(part.material!="dark"&&part.material!="steel"&&part.material!="armor"&&part.material!="team"&&part.material!="heal")throw new InvalidOperationException("Unknown material");
                        if(part.mount!="static"&&part.mount!="turret"&&part.mount!="available")throw new InvalidOperationException("Unknown mount");
                        int count=part.vertices.Length/3;var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
                        for(int i=0;i<count;i++)
                        {
                            for(int axis=0;axis<3;axis++)if(float.IsNaN(part.vertices[i*3+axis])||float.IsInfinity(part.vertices[i*3+axis]))throw new InvalidOperationException("Non-finite vertex");
                            vertices[i]=new Vector3(part.vertices[i*3],part.vertices[i*3+1],part.vertices[i*3+2]);
                            normals[i]=new Vector3(part.normals[i*3],part.normals[i*3+1],part.normals[i*3+2]);uv[i]=new Vector2(part.uv[i*2],part.uv[i*2+1]);
                        }
                        foreach(int index in part.triangles)if(index<0||index>=count)throw new InvalidOperationException("Invalid triangle index");
                        var mesh=new Mesh{name="DF_StationKit_"+model.name+"_"+j};built.Add(mesh);
                        if(count>65535)mesh.indexFormat=IndexFormat.UInt32;
                        mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=part.triangles;mesh.RecalculateBounds();mesh.RecalculateTangents();
                        groups[j]=new RenderGroup{material=part.material,mount=part.mount,mesh=mesh};
                    }
                    prepared.Add(model.name,groups);
                }
                foreach(string name in new[]{"core","deck","bunker","trench","healpad","healnode"})if(!prepared.ContainsKey(name))throw new InvalidOperationException("Missing required building");
                foreach(var pair in prepared)models.Add(pair.Key,pair.Value);
                return true;
            }
            catch(Exception error)
            {
                foreach(var mesh in built)Destroy(mesh);models.Clear();
                Debug.LogWarning("DIMENSIONS FRONT: Station Kit invalid; retaining existing visuals. "+error.Message);return false;
            }
        }

        void TryApply(Component source,string model,string oldName)
        {
            if(completed.Contains(source.GetInstanceID()))return;
            Transform previous=null;
            if(oldName!=null)previous=source.transform.Find(oldName);
            else for(int i=0;i<source.transform.childCount;i++)
            {var child=source.transform.GetChild(i);if(child.name=="DF_ZeroProductionBase"||child.name=="DF_LumenProductionBase"){previous=child;break;}}
            // Healing modules build in Start. A missing old root is retried after startup, never marked complete.
            if(previous==null)return;
            var oldRenderers=previous.GetComponentsInChildren<Renderer>(true);
            if(oldRenderers.Length==0)return;
            Transform turret=model=="bunker"?previous.Find("DF_Bunker_TurretHead"):null;
            Transform signal=model=="healnode"?previous.Find("DF_HealingPlus"):null;
            if(model=="bunker"&&(turret==null||turret.Find("DF_Bunker_Muzzle")==null))return;
            if(model=="healnode"&&signal==null)return;
            completed.Add(source.GetInstanceID());
            GameObject root=null;
            var external=new List<GameObject>();
            var owned=new List<Material>();
            try
            {
                root=new GameObject("DF_StationKit_"+model);root.transform.SetParent(source.transform,false);
                if(model=="healpad"||model=="healnode")
                {
                    Vector3 ground=LunarSurfaceField.WorldSurface(source.transform.position.x,source.transform.position.z);
                    root.transform.position=new Vector3(source.transform.position.x,ground.y+.005f,source.transform.position.z);
                }
                var palette=new Dictionary<string,Material>();
                var team=source.GetComponent<TeamMember>();Color energy=team!=null&&team.Team==1?StylizedArtDirection.LumenEnergy:StylizedArtDirection.ZeroEnergy;
                // Reuse the source emission material: core vulnerability/hit pulses keep their existing writer.
                Material linkedEnergy=null;
                foreach(var renderer in oldRenderers)
                    if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.IsKeywordEnabled("_EMISSION")){linkedEnergy=renderer.sharedMaterial;break;}
                var available=new GameObject("DF_StationKit_Availability");available.transform.SetParent(root.transform,false);
                if(signal!=null){available.transform.SetParent(signal,false);external.Add(available);}
                foreach(var group in models[model])
                {
                    if(!palette.TryGetValue(group.material,out var material))
                    {
                        if(group.material=="team"&&linkedEnergy!=null)material=linkedEnergy;
                        else
                        {
                            Color color=group.material=="armor"?new Color(.80f,.84f,.82f):group.material=="steel"?new Color(.30f,.38f,.43f):group.material=="team"?energy:group.material=="heal"?new Color(.12f,.86f,.48f):new Color(.045f,.066f,.085f);
                            material=RuntimeShaderLibrary.Create(color,false,group.material=="dark"||group.material=="steel"?.65f:.15f,.42f,group.material=="team"||group.material=="heal"?color*1.5f:Color.black);
                            if(material==null)throw new InvalidOperationException("Runtime building shader missing");
                            material.name="DF_StationKit_"+group.material;owned.Add(material);
                        }
                        palette.Add(group.material,material);
                    }
                    Transform mount=group.mount=="turret"?turret:group.mount=="available"?available.transform:root.transform;
                    if(mount==null)throw new InvalidOperationException("Missing visual mount");
                    var part=new GameObject("DF_StationKit_"+group.material+"_"+group.mount);part.transform.SetParent(mount,false);
                    if(mount==turret)external.Add(part);
                    part.AddComponent<MeshFilter>().sharedMesh=group.mesh;
                    var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=model=="healpad"||model=="healnode"?ShadowCastingMode.Off:ShadowCastingMode.On;renderer.receiveShadows=true;
                }
                var owner=root.AddComponent<StationBuildingVisual>();owner.Configure(oldRenderers,owned.ToArray(),external.ToArray(),signal!=null?available.transform:null,signal);
            }
            catch(Exception error)
            {
                foreach(var obj in external)if(obj!=null)Destroy(obj);
                foreach(var material in owned)if(material!=null)Destroy(material);
                if(root!=null)Destroy(root);
                Debug.LogWarning("DIMENSIONS FRONT: Station Kit could not replace "+model+"; existing visuals retained. "+error.Message);
            }
        }
    }
}
