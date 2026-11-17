using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Sculpted carbon-weave finish for textile & tactical undersuits, never dark armor.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroUndersuitFabric : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();

        private static bool IsUndersuit(Material material)
        {
            if (material == null) return false;
            string name = material.name.Replace(" (Instance)", "");
            // Explicit allowlist: black mechanical cores, seals, armor, cables,
            // weapons and ornaments must not acquire a fabric finish.
            // Nerok's pirate-leather torso is his undersuit-equivalent, so it uses
            // the same woven hero finish while shark skin and naval armor stay intact.
            return name == "Obsidian fabric" ||
                   name == "DF_Nyra_Cyber undersuit" ||
                   name == "DF_Traveler_Cyber undersuit" ||
                   name == "DF_Nerok_Pirate leather" ||
                   name == "DF_Elyra_Sealed black core";
        }

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<HeroUndersuitFabric>() != null) return;
            Shader shader = Resources.Load<Shader>("HeroArt/HeroUndersuitFabric");
            if (shader == null) { Debug.LogError("DF: undersuit fabric shader is missing."); return; }
            var owner = model.gameObject.AddComponent<HeroUndersuitFabric>();
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "DF_Anatomy" || renderer.sharedMesh == null) continue;
                Material[] slots = renderer.sharedMaterials;
                bool hasFabric = false;
                foreach (Material material in slots) hasFabric |= IsUndersuit(material);
                if (!hasFabric) continue;
                if (!renderer.sharedMesh.isReadable)
                {
                    Debug.LogWarning("DF: undersuit mesh requires Read/Write: " + renderer.sharedMesh.name, renderer);
                    continue;
                }
                Mesh mesh = Instantiate(renderer.sharedMesh);
                mesh.name = renderer.sharedMesh.name + "_WovenUndersuit";
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Matrix4x4 toModel = model.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Matrix4x4 toNormal = toModel.inverse.transpose;
                var rest = new List<Vector3>(vertices.Length);
                var restNormals = new List<Vector3>(vertices.Length);
                for (int i = 0; i < vertices.Length; i++)
                {
                    rest.Add(toModel.MultiplyPoint3x4(vertices[i]));
                    restNormals.Add(toNormal.MultiplyVector(normals[i]).normalized);
                }
                // Bind-pose coordinates follow skinning without texture swimming.
                // UV0 (maps), UV1 and UV2 (skin ornaments) remain intact.
                mesh.SetUVs(3, rest);
                mesh.SetUVs(4, restNormals);
                owner.meshes.Add(mesh);
                renderer.sharedMesh = mesh;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material original = slots[i];
                    if (!IsUndersuit(original)) continue;
                    // Rich titanium-anthracite tone so muscle contours and carbon-weave crests read clearly.
                    Color color = new Color(.082f, .094f, .114f, 1f);
                    var copy = new Material(original) { shader = shader, name = original.name + "_MatteWeave" };
                    copy.SetColor("_BaseColor", color);
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", color);
                    copy.SetFloat("_Metallic", .22f);
                    copy.SetFloat("_Smoothness", .34f);
                    copy.SetFloat("_BumpScale", 0f);
                    if (original.HasProperty("_BaseMap") && original.GetTexture("_BaseMap") != null)
                        copy.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
                    else if (original.HasProperty("_MainTex") && original.GetTexture("_MainTex") != null)
                    {
                        copy.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
                        copy.SetTextureScale("_BaseMap", original.GetTextureScale("_MainTex"));
                        copy.SetTextureOffset("_BaseMap", original.GetTextureOffset("_MainTex"));
                    }
                    owner.materials.Add(copy);
                    slots[i] = copy;
                }
                renderer.sharedMaterials = slots;
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
