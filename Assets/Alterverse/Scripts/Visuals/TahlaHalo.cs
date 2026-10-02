using System;
using UnityEngine;

namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class TahlaHalo : MonoBehaviour
    {
        [Serializable] sealed class Geometry { public Vector3[] vertices; public int[] triangles; }
        Mesh ownedMesh;
        Material ownedMaterial;
        public static void Apply(Transform model)
        {
            if (model.GetComponent<TahlaHalo>() != null) return;
            Transform head = null;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == "Head") { head = bone; break; }
            if (head == null) return;
            var asset = Resources.Load<TextAsset>("HeroDetails/TahlaHalo");
            if (asset == null) return;
            Geometry geometry = JsonUtility.FromJson<Geometry>(asset.text);
            Bounds bounds = default; bool found = false;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = skin.sharedMesh;
                if (skin.name != "DF_Anatomy" || mesh == null || !mesh.isReadable) continue;
                int index = Array.IndexOf(skin.bones, head);
                if (index < 0 || index >= mesh.bindposes.Length) continue;
                var vertices = mesh.vertices; var weights = mesh.boneWeights;
                Matrix4x4 bind = mesh.bindposes[index];
                for (int i = 0; i < vertices.Length && i < weights.Length; i++)
                {
                    BoneWeight w = weights[i];
                    float weight = (w.boneIndex0 == index ? w.weight0 : 0) + (w.boneIndex1 == index ? w.weight1 : 0)
                        + (w.boneIndex2 == index ? w.weight2 : 0) + (w.boneIndex3 == index ? w.weight3 : 0);
                    if (weight < .5f) continue;
                    Vector3 point = model.InverseTransformPoint(head.TransformPoint(bind.MultiplyPoint3x4(vertices[i])));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
            }
            if (!found || bounds.size.y <= 0) return;
            var owner = model.gameObject.AddComponent<TahlaHalo>();
            float radius = bounds.size.y * .238f;
            Vector3 center = new Vector3(bounds.center.x, bounds.max.y + bounds.size.y * .10f, bounds.center.z);
            Vector3[] points = geometry.vertices;
            for (int i = 0; i < points.Length; i++) points[i] = head.InverseTransformPoint(model.TransformPoint(center + points[i] * radius));
            owner.ownedMesh = new Mesh { name = "DF_TahlaBlenderHalo" };
            owner.ownedMesh.vertices = points; owner.ownedMesh.triangles = geometry.triangles;
            owner.ownedMesh.RecalculateNormals(); owner.ownedMesh.RecalculateBounds();
            Color emerald = new Color(.10f, 1f, .32f, 1f);
            owner.ownedMaterial = RuntimeShaderLibrary.Create(emerald, false, .15f, .45f, emerald * 3f);
            if (owner.ownedMaterial == null) return;
            owner.ownedMaterial.name = "DF_TahlaHaloGlow";
            owner.ownedMaterial.EnableKeyword("_EMISSION");
            if (HeadAdornmentSkin.Merge(model, head, owner.ownedMesh, owner.ownedMaterial)) return;
            var halo = new GameObject("DF_TahlaHalo"); halo.layer = model.gameObject.layer;
            halo.transform.SetParent(head, false);
            halo.AddComponent<MeshFilter>().sharedMesh = owner.ownedMesh;
            halo.AddComponent<MeshRenderer>().sharedMaterial = owner.ownedMaterial;
        }
        void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }
}
