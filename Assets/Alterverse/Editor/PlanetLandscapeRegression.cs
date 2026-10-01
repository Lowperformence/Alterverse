#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class PlanetLandscapeRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/All planet landscape details (Play Mode)")]
        public static void Run()
        {
            Require(Application.isPlaying,"Enter Play Mode first.");
            ArenaVariant previous=ArenaVariantRuntime.Current;
            try
            {
                foreach(ArenaVariant variant in Enum.GetValues(typeof(ArenaVariant)))
                {
                    GameObject host=new("DF_LandscapeRegression_"+variant);
                    try
                    {
                        ArenaVariantRuntime.BeginCoopMatch(variant);
                        PlanetLandscapeDetail detail=host.AddComponent<PlanetLandscapeDetail>();
                        detail.Build();
                        int count=host.GetComponentsInChildren<MeshRenderer>().Length;
                        Require(count>=16&&count<=32,variant+": batch budget or missing pockets");
                        detail.Build();
                        Require(host.GetComponentsInChildren<MeshRenderer>().Length==count,variant+": duplicate build");
                        Require(host.GetComponentsInChildren<Collider>().Length==0,variant+": unexpected collider");
                        Require(host.GetComponentsInChildren<Light>().Length==0,variant+": unexpected light");
                        Require(host.GetComponentsInChildren<LODGroup>().Length==8,variant+": regional culling missing");
                        foreach(MeshFilter filter in host.GetComponentsInChildren<MeshFilter>())
                        {
                            Mesh mesh=filter.sharedMesh;
                            Require(mesh!=null&&mesh.vertexCount>0&&mesh.vertexCount<65535,variant+": invalid mesh");
                            Require(mesh.subMeshCount==1,variant+": unexpected material split");
                            Material material=filter.GetComponent<Renderer>().sharedMaterial;
                            Require(material!=null&&material.shader!=null&&material.shader.isSupported,variant+": shader unavailable");
                            foreach(Vector3 vertex in mesh.vertices)
                            {
                                Vector3 world=filter.transform.TransformPoint(vertex);
                                Require(float.IsFinite(world.x)&&float.IsFinite(world.y)&&float.IsFinite(world.z),variant+": non-finite vertex");
                                Require(Mathf.Abs(world.x)<36&&Mathf.Abs(world.z)>27&&Mathf.Abs(world.z)<36,variant+": scenery left its peripheral pocket");
                                Require(world.y<3,variant+": excessive scenery height");
                            }
                        }
                    }
                    finally { host.SetActive(false); UnityEngine.Object.Destroy(host); }
                }
                Debug.Log("DIMENSIONS FRONT: all planet landscape geometry, bounds, shader, duplicate-build and batch checks passed. Visual/device approval is separate.");
            }
            finally { ArenaVariantRuntime.BeginCoopMatch(previous); }
        }
        static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException(message); }
    }
}
#endif
