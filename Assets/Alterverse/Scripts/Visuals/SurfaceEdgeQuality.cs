using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Render-only edge rounding for existing opaque arena cube props.</summary>
    public static class SurfaceEdgeQuality
    {
        public static void Apply(Renderer renderer, Transform matchWorld)
        {
            if (matchWorld == null || !(renderer is MeshRenderer) ||
                !renderer.transform.IsChildOf(matchWorld)) return;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh source = filter != null ? filter.sharedMesh : null;
            // Only Unity's built-in cube; imported kit meshes and authored hero skins retain their geometry.
            if (source == null || source.name != "Cube" || source.vertexCount != 24 ||
                source.subMeshCount != 1) return;
            Vector3 scale = renderer.transform.lossyScale;
            float smallest = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            float largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            // Keep flat ground sheets, lane markings and long thin energy strips exact.
            if (smallest < .025f || largest > smallest * 12f) return;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null || material.renderQueue >= 2450) return;
                string shader = material.shader.name;
                if (shader != "Alterverse/RuntimeLit" && shader != "Alterverse/PlanetSurface" &&
                    shader != "Alterverse/ScenerySurface" &&
                    shader != "Universal Render Pipeline/Lit") return;
            }
            // Do not change transforms, BoxColliders, navigation, material slots or dimensions.
            filter.sharedMesh = ProductionMeshFactory.RoundedBox();
        }
    }
}
