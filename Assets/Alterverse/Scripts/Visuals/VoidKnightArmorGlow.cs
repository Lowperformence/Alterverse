using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Self-lit violet inlays and a compact skinned halo, including LDR menu portraits.</summary>
    [DisallowMultipleComponent]
    public sealed class VoidKnightArmorGlow : MonoBehaviour
    {
        private readonly List<Material> ownedMaterials = new();
        private Mesh haloMesh;

        public static void Apply(Transform model)
        {
#if UNITY_SERVER
            return;
#else
            if (model == null || model.GetComponent<VoidKnightArmorGlow>() != null) return;
            Shader core = Resources.Load<Shader>("HeroArt/VoidArmorEnergy");
            Shader halo = Resources.Load<Shader>("HeroArt/VoidArmorHalo");
            if (core == null || halo == null)
            {
                Debug.LogError("DF: Void Knight armor glow shaders are missing.", model);
                return;
            }
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Weapon colorways have their own presentation; never tint the sword or armor metal.
                if (skin.name != "DF_Anatomy" || skin.sharedMesh == null || !skin.sharedMesh.isReadable) continue;
                Material[] slots = skin.sharedMaterials;
                var energySlots = new List<int>();
                for (int i = 0; i < slots.Length && i < skin.sharedMesh.subMeshCount; i++)
                    if (slots[i] != null && slots[i].name.StartsWith("Void ") &&
                        slots[i].name.ToLowerInvariant().Contains("energy")) energySlots.Add(i);
                if (energySlots.Count == 0) continue;

                var owner = model.gameObject.AddComponent<VoidKnightArmorGlow>();
                foreach (int index in energySlots)
                {
                    Material source = slots[index];
                    bool bright = source.name.ToLowerInvariant().Contains("core");
                    Color violet = bright ? new Color(.64f, .13f, 1f, 1f) : new Color(.40f, .035f, .95f, 1f);
                    Material material = new Material(core) { name = source.name + " [Self Lit]" };
                    material.SetColor("_BaseColor", violet);
                    material.SetColor("_EmissionColor", violet * 2.4f);
                    owner.ownedMaterials.Add(material);
                    slots[index] = material;
                }
                skin.sharedMaterials = slots;

                owner.haloMesh = BuildHaloMesh(skin.sharedMesh, energySlots);
                Material glow = new Material(halo) { name = "Void armor local violet halo" };
                glow.SetColor("_BaseColor", new Color(.48f, .045f, 1f, .32f));
                glow.SetFloat("_HaloWidth", .018f);
                owner.ownedMaterials.Add(glow);
                var child = new GameObject("DF_VoidArmorHalo");
                child.layer = skin.gameObject.layer;
                child.transform.SetParent(skin.transform, false);
                var renderer = child.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = owner.haloMesh;
                renderer.sharedMaterial = glow;
                renderer.bones = skin.bones;
                renderer.rootBone = skin.rootBone;
                Bounds bounds = skin.localBounds;
                bounds.Expand(.05f);
                renderer.localBounds = bounds;
                renderer.quality = skin.quality;
                renderer.updateWhenOffscreen = skin.updateWhenOffscreen;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return;
            }
#endif
        }

        internal static Mesh BuildHaloMesh(Mesh source, List<int> energySlots)
        {
            Vector3[] positions = source.vertices, normals = source.normals;
            BoneWeight[] weights = source.boneWeights;
            var remap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var vertexNormals = new List<Vector3>();
            var boneWeights = new List<BoneWeight>();
            var triangles = new List<int>();
            foreach (int slot in energySlots)
                foreach (int index in source.GetTriangles(slot))
                {
                    if (!remap.TryGetValue(index, out int compact))
                    {
                        compact = vertices.Count; remap.Add(index, compact);
                        vertices.Add(positions[index]); vertexNormals.Add(normals[index]);
                        boneWeights.Add(weights[index]);
                    }
                    triangles.Add(compact);
                }
            // Three nested shells in one submesh/draw. The shader expands each by its
            // vertex color, so there is no CPU skin baking or per-frame allocation.
            int count = vertices.Count;
            var expanded = new Vector3[count * 3];
            var expandedNormals = new Vector3[count * 3];
            var expandedWeights = new BoneWeight[count * 3];
            var colors = new Color[count * 3];
            var indices = new int[triangles.Count * 3];
            float[] widths = { .24f, .58f, 1f }, opacity = { 1f, .38f, .12f };
            for (int layer = 0; layer < 3; layer++)
            {
                for (int i = 0; i < count; i++)
                {
                    int offset = layer * count + i;
                    expanded[offset] = vertices[i]; expandedNormals[offset] = vertexNormals[i];
                    expandedWeights[offset] = boneWeights[i];
                    colors[offset] = new Color(widths[layer], 0f, 0f, opacity[layer]);
                }
                for (int i = 0; i < triangles.Count; i++)
                    indices[layer * triangles.Count + i] = triangles[i] + layer * count;
            }
            var mesh = new Mesh { name = "DF_VoidArmorCompactHalo" };
            if (expanded.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = expanded; mesh.normals = expandedNormals; mesh.colors = colors;
            mesh.boneWeights = expandedWeights; mesh.bindposes = source.bindposes;
            mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Material material in ownedMaterials) Release(material);
            Release(haloMesh);
        }
        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
