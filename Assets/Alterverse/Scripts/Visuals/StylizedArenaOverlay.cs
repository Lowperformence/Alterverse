using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    // Quiet, grounded lane surfaces keep combat silhouettes and warning rings readable.
    public sealed class StylizedArenaOverlay : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();

        private void Start()
        {
            Material paving = Surface(new Color(.18f, .21f, .28f));
            Material zeroTrim = Surface(new Color(.38f, .22f, .52f));
            Material lumenTrim = Surface(new Color(.16f, .48f, .53f));
            if (paving == null) return;
            // Each strip is one mesh, rather than hundreds of individually allocated primitives.
            foreach (float z in new[] { -16f, 16f })
            {
                BuildStrip("DF_LanePaving", z, -35f, 35f, 2.0f, .085f, paving);
                BuildStrip("DF_ZeroLaneBorder", z - 2.15f, -35f, -.5f, .10f, .09f, zeroTrim);
                BuildStrip("DF_ZeroLaneBorder", z + 2.15f, -35f, -.5f, .10f, .09f, zeroTrim);
                BuildStrip("DF_LumenLaneBorder", z - 2.15f, .5f, 35f, .10f, .09f, lumenTrim);
                BuildStrip("DF_LumenLaneBorder", z + 2.15f, .5f, 35f, .10f, .09f, lumenTrim);
            }
            // Keep the center open: no fake objective markers and no luminous lines across attacks.
        }

        private Material Surface(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .1f);
            materials.Add(material);
            return material;
        }

        private void BuildStrip(string label, float z, float from, float to, float halfWidth, float y, Material material)
        {
            if (material == null) return;
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            for (float x = from; x < to; x += 3.5f)
            {
                float left = x + .045f, right = Mathf.Min(x + 3.5f, to) - .045f;
                int n = vertices.Count;
                vertices.Add(new Vector3(left, y, z - halfWidth));
                vertices.Add(new Vector3(left, y, z + halfWidth));
                vertices.Add(new Vector3(right, y, z + halfWidth));
                vertices.Add(new Vector3(right, y, z - halfWidth));
                indices.Add(n); indices.Add(n + 1); indices.Add(n + 2);
                indices.Add(n); indices.Add(n + 2); indices.Add(n + 3);
            }
            Mesh mesh = new Mesh { name = label };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            GameObject surface = new GameObject(label);
            surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
