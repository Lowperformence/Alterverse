#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    /// <summary>Actual Unity import checks for the instance-only fist replacement.</summary>
    public static class BrakkFistRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Brakk Forge Grip V2")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            { Debug.LogWarning("DF: Start Play Mode before running Brakk Forge Grip checks."); return; }
            GameObject asset = Resources.Load<GameObject>("AuthoredHeroes/Rook");
            Require(asset != null, "Rook resource is missing.");
            var sourceMeshes = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .ToDictionary(r => r.name, r => r.sharedMesh);
            GameObject brak = UnityEngine.Object.Instantiate(asset);
            GameObject nerok = UnityEngine.Object.Instantiate(asset);
            try
            {
                MethodInfo restore = typeof(AuthoredHeroPresentation).GetMethod("RestoreImportedBindPose", BindingFlags.Static | BindingFlags.NonPublic);
                Require(restore != null, "Bind-pose restoration entry point is missing.");
                restore.Invoke(null, new object[] { brak.transform, HeroId.Rook });
                restore.Invoke(null, new object[] { nerok.transform, HeroId.Nerok });
                BrakkForearmTrim.Apply(brak.transform, HeroId.Rook);
                BrakkFistAppearance.Apply(brak.transform, HeroId.Rook);
                BrakkFistAppearance.Apply(nerok.transform, HeroId.Nerok);
                Require(brak.GetComponent<BrakkFistAppearance>() != null, "Replacement failed.");
                Require(nerok.GetComponent<BrakkFistAppearance>() == null, "Nerok was changed.");
                foreach (string side in new[] { "L", "R" })
                {
                    Renderer legacy = brak.GetComponentsInChildren<Renderer>(true)
                        .Single(r => r.name == "DF_LegacyGauntlet" + side);
                    MeshRenderer fist = brak.GetComponentsInChildren<MeshRenderer>(true)
                        .Single(r => r.name == "DF_Gauntlet" + side);
                    Require(!legacy.enabled && fist.enabled, "The old/new renderer state is wrong.");
                    Require(fist.transform.parent.name == "Hand" + side, "Fist is not on the anatomical hand.");
                    Require(fist.sharedMaterials.Length == 5, "Material slot contract changed.");
                    Require(fist.GetComponent<Collider>() == null, "Fist unexpectedly changes gameplay collision.");
                    Mesh mesh = fist.GetComponent<MeshFilter>().sharedMesh;
                    Require(mesh.vertexCount > 0 && mesh.subMeshCount == 5, "Fist mesh is empty.");
                    Require(mesh.vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z)), "Non-finite geometry.");
                    Transform hand = fist.transform.parent;
                    Quaternion rotation = hand.localRotation;
                    Vector3 contact = mesh.bounds.center;
                    hand.localRotation *= Quaternion.Euler(21f, -16f, 8f);
                    Require(Vector3.Distance(fist.transform.TransformPoint(contact), hand.TransformPoint(contact)) < 1e-6f,
                        "Fist does not follow the animated hand.");
                    hand.localRotation = rotation;
                    var nerokGauntlet = nerok.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Single(r => r.name == "DF_Gauntlet" + side);
                    Require(nerokGauntlet.enabled && nerokGauntlet.sharedMesh == sourceMeshes["DF_Gauntlet" + side],
                        "Shared Rook/Nerok source mesh was modified.");
                }
                int count = brak.GetComponentsInChildren<MeshRenderer>(true).Length;
                BrakkFistAppearance.Apply(brak.transform, HeroId.Rook);
                Require(brak.GetComponentsInChildren<MeshRenderer>(true).Length == count, "Duplicate fists on repeated Apply.");
                foreach (var renderer in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Require(renderer.sharedMesh == sourceMeshes[renderer.name], "Imported asset identity changed.");
                Debug.Log("DF: Brakk Forge Grip V2 passed: both fists, hand binding, idempotence, shared Nerok source and no gameplay colliders.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(brak);
                UnityEngine.Object.DestroyImmediate(nerok);
            }
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Brakk Forge Grip: " + message); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
#endif
