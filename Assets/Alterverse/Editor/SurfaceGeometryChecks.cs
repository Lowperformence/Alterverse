#if UNITY_EDITOR
using System;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class SurfaceGeometryChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Surface rounded geometry")]
        public static void Run()
        {
            foreach (Mesh mesh in new[] { ProductionMeshFactory.RoundedBox(), ProductionMeshFactory.BeveledBox() })
            {
                Require(mesh.vertexCount <= 384, "Shared box vertex budget exceeded.");
                Require(Vector3.Distance(mesh.bounds.size, Vector3.one) < .0001f, "Box dimensions changed.");
                Require(mesh.uv.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount,
                    "Rounded box lacks surface texture coordinates or tangents.");
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                foreach (Vector3 normal in normals) Require(Mathf.Abs(normal.magnitude - 1) < .001f, "Non-unit normal.");
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a=triangles[i], b=triangles[i+1], c=triangles[i+2];
                    Vector3 face=Vector3.Cross(vertices[b]-vertices[a], vertices[c]-vertices[a]);
                    Require(face.sqrMagnitude > 1e-12f && Vector3.Dot(face,normals[a]+normals[b]+normals[c]) > 0,
                        "Rounded box has a degenerate or reversed triangle.");
                }
            }
            GameObject world = new GameObject("DF_GeometryCheckWorld");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = RuntimeShaderLibrary.Create(Color.gray,false,.25f,.35f);
            try
            {
                cube.transform.SetParent(world.transform,false);
                MeshFilter filter=cube.GetComponent<MeshFilter>();
                Mesh original=filter.sharedMesh;
                Renderer renderer=cube.GetComponent<Renderer>();
                renderer.sharedMaterial=material;
                BoxCollider collider=cube.GetComponent<BoxCollider>();
                Vector3 size=collider.size;
                // Ground sheets and objects outside the live match retain their source meshes.
                cube.transform.localScale=new Vector3(50,.02f,50);
                SurfaceEdgeQuality.Apply(renderer,world.transform);
                Require(filter.sharedMesh==original,"Ground sheet rounded unexpectedly.");
                cube.transform.localScale=Vector3.one;
                SurfaceEdgeQuality.Apply(renderer,null);
                Require(filter.sharedMesh==original,"Non-match object rounded unexpectedly.");
                SurfaceEdgeQuality.Apply(renderer,world.transform);
                Require(filter.sharedMesh==ProductionMeshFactory.RoundedBox(),"Eligible arena cube was not rounded.");
                Require(collider.enabled && collider.size==size && cube.transform.localScale==Vector3.one &&
                    renderer.sharedMaterial==material,"Visual rounding changed collision, scale or material.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
                UnityEngine.Object.DestroyImmediate(world);
                if(material!=null)UnityEngine.Object.DestroyImmediate(material);
            }
            Debug.Log("Rounded geometry: bounds, texture coordinates, winding, vertex budget and collider isolation passed.");
        }
        private static void Require(bool valid,string message)
        { if(!valid)throw new InvalidOperationException(message); }
    }
}
#endif
