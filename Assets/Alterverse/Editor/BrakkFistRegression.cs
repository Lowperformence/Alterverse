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
    /// <summary>Checks the actual Unity import at the origin and the remote menu portrait stage.</summary>
    public static class BrakkFistRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Brakk Forge Grip V2")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            { Debug.LogWarning("DF: Start Play Mode before running Brakk Forge Grip checks."); return; }
            GameObject asset = Resources.Load<GameObject>("AuthoredHeroes/Rook");
            Require(asset != null, "Rook resource is missing.");
            var importedMeshes = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .ToDictionary(r => r.name, r => r.sharedMesh);
            GameObject origin = UnityEngine.Object.Instantiate(asset);
            GameObject stage = new GameObject("DF_BrakkFistCheckPortraitStage");
            stage.transform.position = new Vector3(12000f, 12000f, 12000f);
            stage.transform.rotation = Quaternion.Euler(0f, 28f, 0f);
            stage.transform.localScale = new Vector3(1.22f, 1.32f, 1.22f);
            GameObject portrait = UnityEngine.Object.Instantiate(asset, stage.transform, false);
            GameObject nerok = UnityEngine.Object.Instantiate(asset);
            try
            {
                Prepare(origin);
                Prepare(portrait);
                CheckReplacement(origin);
                CheckReplacement(portrait);
                foreach (string side in new[] { "L", "R" })
                {
                    Mesh a = Fist(origin, side).sharedMesh, b = Fist(portrait, side).sharedMesh;
                    Require(a.vertexCount == b.vertexCount, "Portrait vertex count changed.");
                    float tolerance = a.bounds.size.magnitude * 2e-6f;
                    Vector3[] av = a.vertices, bv = b.vertices;
                    for (int i = 0; i < av.Length; i++)
                        Require(Vector3.Distance(av[i], bv[i]) <= tolerance,
                            "Portrait stage world position or parent scale changed local fist geometry.");
                }
                BrakkFistAppearance.Apply(nerok.transform, HeroId.Nerok);
                Require(nerok.GetComponent<BrakkFistAppearance>() == null, "Nerok was changed.");
                foreach (string side in new[] { "L", "R" })
                    Require(Fist(nerok, side).sharedMesh == importedMeshes["DF_Gauntlet" + side],
                        "Shared Nerok mesh changed.");
                foreach (SkinnedMeshRenderer renderer in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Require(renderer.sharedMesh == importedMeshes[renderer.name], "Imported source asset changed.");
                Debug.Log("DF: Brakk Forge Grip passed: original weapon objects/positions/rig retained, " +
                    "visible hand skins, remote portrait stage, idempotence and unchanged Nerok source.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(origin);
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(nerok);
            }
        }
        static void Prepare(GameObject model)
        {
            MethodInfo restore = typeof(AuthoredHeroPresentation).GetMethod("RestoreImportedBindPose", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo size = typeof(AuthoredHeroPresentation).GetMethod("ApplyHandSize", BindingFlags.Static | BindingFlags.NonPublic);
            Require(restore != null && size != null, "Imported rig preparation entry points are missing.");
            restore.Invoke(null, new object[] { model.transform, HeroId.Rook });
            size.Invoke(null, new object[] { model.transform, HeroId.Rook });
            BrakkForearmTrim.Apply(model.transform, HeroId.Rook);
        }
        static void CheckReplacement(GameObject model)
        {
            string[] sides = { "L", "R" };
            SkinnedMeshRenderer[] old = sides.Select(s => Fist(model, s)).ToArray();
            Mesh[] sources = old.Select(r => r.sharedMesh).ToArray();
            Transform[][] bones = old.Select(r => r.bones).ToArray();
            Transform[] roots = old.Select(r => r.rootBone).ToArray();
            Vector3[] positions = old.Select(r => r.transform.localPosition).ToArray();
            Quaternion[] rotations = old.Select(r => r.transform.localRotation).ToArray();
            Vector3[] scales = old.Select(r => r.transform.localScale).ToArray();
            int renderers = model.GetComponentsInChildren<Renderer>(true).Length;
            BrakkFistAppearance.Apply(model.transform, HeroId.Rook);
            Require(model.GetComponent<BrakkFistAppearance>() != null, "Replacement failed.");
            Require(model.GetComponentsInChildren<Renderer>(true).Length == renderers, "Replacement added detached renderers.");
            for (int i = 0; i < sides.Length; i++)
            {
                SkinnedMeshRenderer fist = Fist(model, sides[i]);
                Require(fist == old[i], "Original weapon renderer was replaced or moved.");
                Require(fist.transform.localPosition == positions[i] && fist.transform.localRotation == rotations[i] &&
                    fist.transform.localScale == scales[i], "Original weapon transform changed.");
                Require(fist.bones.SequenceEqual(bones[i]) && fist.rootBone == roots[i], "Original weapon rig changed.");
                Require(fist.enabled && fist.updateWhenOffscreen && fist.sharedMaterials.Length == 5,
                    "Replacement renderer/material state is wrong.");
                Require(fist.GetComponent<Collider>() == null, "Fist unexpectedly changes gameplay collision.");
                Mesh mesh = fist.sharedMesh;
                Require(mesh != sources[i] && mesh.vertexCount > 0 && mesh.subMeshCount == 5, "New mesh is missing.");
                Require(mesh.vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z)), "Non-finite geometry.");
                Bounds previous = VertexBounds(sources[i].vertices);
                Require(Vector3.Distance(mesh.bounds.center, previous.center) <= previous.size.magnitude * 2e-6f,
                    "Fist centre differs from the old weapon position.");
                float ratio = mesh.bounds.size.magnitude / previous.size.magnitude;
                Require(ratio >= .35f && ratio <= 3f, "FBX unit conversion produced a tiny or oversized fist.");
                Matrix4x4[] before = sources[i].bindposes, after = mesh.bindposes;
                Require(before.Length == after.Length, "Bind pose count changed.");
                for (int bone = 0; bone < before.Length; bone++)
                    for (int element = 0; element < 16; element++)
                        Require(before[bone][element] == after[bone][element], "Original bind pose changed.");
                int hand = Array.FindIndex(fist.bones, b => b != null && b.name == "Hand" + sides[i]);
                Require(hand >= 0 && mesh.boneWeights.All(w => w.boneIndex0 == hand && w.weight0 == 1f &&
                    w.weight1 == 0f && w.weight2 == 0f && w.weight3 == 0f), "Fist is not rigidly bound to its original hand.");
            }
            Mesh[] applied = old.Select(r => r.sharedMesh).ToArray();
            BrakkFistAppearance.Apply(model.transform, HeroId.Rook);
            Require(old.Select(r => r.sharedMesh).SequenceEqual(applied), "Repeated Apply generated duplicate meshes.");
        }
        static SkinnedMeshRenderer Fist(GameObject model, string side) =>
            model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "DF_Gauntlet" + side);
        static Bounds VertexBounds(Vector3[] vertices)
        {
            Require(vertices.Length > 0, "Original mesh is empty.");
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
            return bounds;
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Brakk Forge Grip: " + message); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
#endif
