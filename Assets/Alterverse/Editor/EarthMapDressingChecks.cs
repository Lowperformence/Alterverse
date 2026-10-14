#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class EarthMapDressingChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Earth scenery kit (Play Mode)")]
        public static void Run()
        {
            Require(Application.isPlaying,"Enter Play Mode first.");
            ArenaVariant previous=ArenaVariantRuntime.Current;
            GameObject host=new("DF_EarthSceneryCheck");
            try
            {
                ArenaVariantRuntime.BeginCoopMatch(ArenaVariant.Earth);
                EarthMapDressing detail=host.AddComponent<EarthMapDressing>();detail.Build();
                MeshRenderer[] renderers=host.GetComponentsInChildren<MeshRenderer>();
                Require(renderers.Length>10&&renderers.Length<=120,"Missing scenery or excess material batches.");
                detail.Build();
                Require(host.GetComponentsInChildren<MeshRenderer>().Length==renderers.Length,"Duplicate scenery build.");
                Require(host.GetComponentsInChildren<Collider>(true).Length==0,"Scenery changed collision.");
                Require(host.GetComponentsInChildren<Rigidbody>(true).Length==0,"Unexpected scenery physics.");
                Require(host.GetComponentsInChildren<Light>(true).Length==0,"Unexpected scenery lights.");
                SceneryBuildingClearance clearance=new();int triangles=0,water=0;
                foreach(MeshRenderer renderer in renderers)
                {
                    Require(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.isSupported,"Missing material/shader.");
                    Mesh mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    Require(mesh!=null&&mesh.vertexCount>0,"Empty scenery mesh.");
                    Vector3[] vertices=mesh.vertices;int[] indices=mesh.triangles;
                    Require(mesh.uv.Length==vertices.Length&&mesh.normals.Length==vertices.Length,"Missing mesh channels.");
                    triangles+=indices.Length/3;
                    foreach(Vector3 v in vertices)
                        Require(float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z),"Non-finite mesh position.");
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        Vector3 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];
                        Require(Vector3.Cross(b-a,c-a).sqrMagnitude>1e-14f,"Degenerate scenery triangle.");
                        Bounds bounds=new(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                        Require(!clearance.Blocks(SceneryBuildingClearance.TransformBounds(bounds,renderer.localToWorldMatrix)),"Scenery inside a structure apron.");
                    }
                    if(renderer.sharedMaterial.name.EndsWith("Water"))
                    {
                        water++;Require(renderer.sharedMaterial.GetFloat("_AuthoredBanks")==1f,"Water bank channel missing.");
                        foreach(Vector3 v in vertices)Require(Mathf.Abs(v.y-.059f)<.0001f,"Water is not level.");
                    }
                }
                Require(water>=2&&triangles<320000,"Water missing or triangle budget exceeded.");
                Debug.Log($"DIMENSIONS FRONT: Earth scenery passed: {renderers.Length} batches, {triangles} triangles, collider-free; device visual review remains separate.");
            }
            finally
            {
                host.SetActive(false);UnityEngine.Object.Destroy(host);
                ArenaVariantRuntime.BeginCoopMatch(previous);
            }
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
