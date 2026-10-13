using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Approved Blender armor, staff and palm focus on Tahla's existing imported rig.</summary>
    [DisallowMultipleComponent]
    public sealed class TahlaEquipmentAppearance : MonoBehaviour
    {
        [Serializable] sealed class Submesh { public int[] triangles; }
        [Serializable] sealed class Surface
        { public string name; public float[] color; public float metallic, smoothness; }
        [Serializable] sealed class Assembly
        {
            public string name;
            public float[] vertices, normals, uv;
            public int[] boneIndices;
            public Submesh[] submeshes;
        }
        [Serializable] sealed class Geometry
        {
            public int version;
            public string revision;
            public string[] boneNames;
            public float[] pivot, muzzle, staffHandle;
            public float referenceRigSpan;
            public Surface[] materials;
            public Assembly[] assemblies;
        }
        const string Revision = "tahla-approved-equipment-20261013";
        public bool IsReady { get; private set; }
        static Geometry cached;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<RendererState> previousRenderers = new List<RendererState>();
        SkinnedMeshRenderer anatomy;
        Mesh previousAnatomy;
        Material[] previousAnatomyMaterials;
        Transform previousMuzzle;
        string previousMuzzleName;

        sealed class RendererState
        { public Renderer renderer; public string name; public bool enabled; }

        public static void Apply(Transform model, HeroId hero)
        {
            if (hero != HeroId.Mira || model == null || model.GetComponent<TahlaEquipmentAppearance>() != null) return;
            Geometry data = Load();
            if (data == null) return;
            SkinnedMeshRenderer anatomy = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == "DF_Anatomy") { anatomy = skin; break; }
            if (anatomy == null || anatomy.sharedMesh == null || !anatomy.sharedMesh.isReadable)
            { Debug.LogError("DF: Tahla equipment requires readable anatomy.", model); return; }
            var owner = model.gameObject.AddComponent<TahlaEquipmentAppearance>();
            try { owner.Build(model, anatomy, data); }
            catch (Exception error)
            {
                owner.Restore(); owner.Release(); Destroy(owner);
                Debug.LogError("DF: Tahla equipment could not be attached: " + error.Message, model);
            }
        }

        static Geometry Load()
        {
            if (cached != null) return cached;
            TextAsset asset = Resources.Load<TextAsset>("TahlaEquipment/Model");
            if (asset == null) { Debug.LogError("DF: TahlaEquipment/Model is missing."); return null; }
            try
            {
                Geometry data;
                using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                using (var reader = new StreamReader(stream)) data = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
                if (!Valid(data)) throw new InvalidDataException("Tahla equipment contract is invalid.");
                cached = data; return data;
            }
            catch (Exception error) { Debug.LogError("DF: Could not load Tahla equipment: " + error.Message); return null; }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool FiniteArray(float[] values, int length = -1)
        {
            if (values == null || length >= 0 && values.Length != length) return false;
            foreach (float value in values) if (!Finite(value)) return false;
            return true;
        }
        static bool Valid(Geometry data)
        {
            if (data == null || data.version != 1 || data.revision != Revision ||
                !FiniteArray(data.pivot, 3) || !FiniteArray(data.muzzle, 3) || !FiniteArray(data.staffHandle, 3) ||
                !Finite(data.referenceRigSpan) || data.referenceRigSpan <= 0f || data.boneNames == null ||
                data.boneNames.Length == 0 || data.materials == null || data.materials.Length != 5 ||
                data.assemblies == null || data.assemblies.Length != 3) return false;
            var names = new HashSet<string>();
            foreach (string name in data.boneNames)
                if (string.IsNullOrEmpty(name) || !names.Add(name)) return false;
            foreach (Surface surface in data.materials)
                if (surface == null || !FiniteArray(surface.color, 3) || !Finite(surface.metallic) || !Finite(surface.smoothness)) return false;
            names.Clear();
            foreach (Assembly assembly in data.assemblies)
            {
                if (assembly == null || !names.Add(assembly.name) ||
                    (assembly.name != "DF_TahlaArmor" && assembly.name != "DF_WeaponR" && assembly.name != "DF_WeaponL") ||
                    !FiniteArray(assembly.vertices) || assembly.vertices.Length == 0 || assembly.vertices.Length % 3 != 0 ||
                    !FiniteArray(assembly.normals, assembly.vertices.Length) ||
                    !FiniteArray(assembly.uv, assembly.vertices.Length / 3 * 2) || assembly.boneIndices == null ||
                    assembly.boneIndices.Length != assembly.vertices.Length / 3 || assembly.submeshes == null ||
                    assembly.submeshes.Length != data.materials.Length) return false;
                foreach (int index in assembly.boneIndices) if (index < 0 || index >= data.boneNames.Length) return false;
                foreach (Submesh sub in assembly.submeshes)
                {
                    if (sub == null || sub.triangles == null || sub.triangles.Length % 3 != 0) return false;
                    foreach (int index in sub.triangles) if (index < 0 || index >= assembly.vertices.Length / 3) return false;
                }
            }
            return true;
        }
        static Matrix4x4 RelativeMatrix(Transform part, Transform ancestor)
        {
            Matrix4x4 result = Matrix4x4.identity;
            for (Transform current = part; current != ancestor; current = current.parent)
            {
                if (current == null) throw new InvalidOperationException("Tahla part is outside the model hierarchy.");
                result = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * result;
            }
            return result;
        }
        static Vector3 Point(float[] data) => new Vector3(data[0], data[1], data[2]);

        void Build(Transform model, SkinnedMeshRenderer body, Geometry data)
        {
            anatomy = body; previousAnatomy = body.sharedMesh; previousAnatomyMaterials = body.sharedMaterials;
            var sources = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            int hips = Array.FindIndex(body.bones, b => b != null && b.name == "Hips");
            int spine = Array.FindIndex(body.bones, b => b != null && b.name == "Spine");
            var nativeBind = body.sharedMesh.bindposes;
            if (hips < 0 || spine < 0 || hips >= nativeBind.Length || spine >= nativeBind.Length)
                throw new InvalidOperationException("Tahla Hips/Spine bind poses are missing.");
            Matrix4x4 toModel = RelativeMatrix(body.transform, model);
            if (!Finite(toModel.determinant) || Mathf.Abs(toModel.determinant) < 1e-12f)
                throw new InvalidOperationException("Invalid Tahla anatomy transform.");
            Matrix4x4 toMesh = toModel.inverse;
            Vector3 nativeHips = nativeBind[hips].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 nativeSpine = nativeBind[spine].inverse.MultiplyPoint3x4(Vector3.zero);
            float units = toModel.MultiplyVector(nativeSpine - nativeHips).magnitude / data.referenceRigSpan;
            if (!Finite(units) || units <= 0f) throw new InvalidOperationException("Invalid Tahla imported units.");
            Vector3 pivot = Point(data.pivot);
            var bones = new Transform[data.boneNames.Length];
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                string name = data.boneNames[i];
                bones[i] = Array.Find(transforms, bone => bone.name == name);
                if (bones[i] == null) throw new InvalidOperationException("Tahla bone is missing: " + name);
                bool found = false;
                // Prefer exact imported bind poses, including the staff's original WeaponGripR cluster.
                for (Transform ancestor = bones[i]; ancestor != null && ancestor != model && !found; ancestor = ancestor.parent)
                    foreach (var skin in sources)
                    {
                        if (skin.sharedMesh == null) continue;
                        int index = Array.IndexOf(skin.bones, ancestor);
                        Matrix4x4[] sourceBind = skin.sharedMesh.bindposes;
                        if (index < 0 || index >= sourceBind.Length) continue;
                        bind[i] = RelativeMatrix(bones[i], ancestor).inverse * sourceBind[index] *
                            RelativeMatrix(skin.transform, model).inverse * toModel;
                        found = true; break;
                    }
                if (!found) throw new InvalidOperationException("Tahla imported binding is missing: " + name);
            }
            foreach (Surface surface in data.materials)
            {
                Color color = new Color(surface.color[0], surface.color[1], surface.color[2], 1f);
                Color emission = surface.name == "Emerald crystal" ? color * 1.2f : Color.black;
                Material material = RuntimeShaderLibrary.Create(color, false, surface.metallic, surface.smoothness, emission);
                if (material == null) throw new InvalidOperationException("Tahla equipment shader is missing.");
                material.name = "DF_Tahla_" + surface.name;
                if (surface.name == "Emerald crystal") material.EnableKeyword("_EMISSION");
                materials.Add(material);
            }
            var generated = new List<SkinnedMeshRenderer>();
            foreach (Assembly assembly in data.assemblies)
            {
                int count = assembly.vertices.Length / 3;
                var points = new Vector3[count]; var normals = new Vector3[count];
                var uv = new Vector2[count]; var weights = new BoneWeight[count];
                Matrix4x4 normalMatrix = toMesh.inverse.transpose;
                for (int i = 0; i < count; i++)
                {
                    int p = i * 3;
                    points[i] = nativeHips + toMesh.MultiplyVector(new Vector3(assembly.vertices[p], assembly.vertices[p + 1], assembly.vertices[p + 2]) - pivot) * units;
                    normals[i] = normalMatrix.MultiplyVector(new Vector3(assembly.normals[p], assembly.normals[p + 1], assembly.normals[p + 2])).normalized;
                    uv[i] = new Vector2(assembly.uv[i * 2], assembly.uv[i * 2 + 1]);
                    weights[i] = new BoneWeight { boneIndex0 = assembly.boneIndices[i], weight0 = 1f };
                    if (!Finite(points[i].x) || !Finite(points[i].y) || !Finite(points[i].z))
                        throw new InvalidOperationException("Invalid converted Tahla vertex.");
                }
                var mesh = new Mesh { name = assembly.name + "_Approved", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                meshes.Add(mesh); mesh.vertices = points; mesh.normals = normals; mesh.uv = uv;
                mesh.bindposes = bind; mesh.boneWeights = weights; mesh.subMeshCount = assembly.submeshes.Length;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int[] indices = (int[])assembly.submeshes[sub].triangles.Clone();
                    if (toMesh.determinant < 0f)
                        for (int i = 0; i < indices.Length; i += 3)
                        { int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
                    mesh.SetTriangles(indices, sub, false);
                }
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                var go = new GameObject(assembly.name) { layer = body.gameObject.layer }; objects.Add(go);
                go.transform.SetParent(body.transform, false);
                var renderer = go.AddComponent<SkinnedMeshRenderer>(); renderer.enabled = false;
                renderer.sharedMesh = mesh; renderer.sharedMaterials = materials.ToArray();
                renderer.bones = bones; renderer.rootBone = body.rootBone; renderer.quality = SkinQuality.Bone1;
                Bounds bounds = body.localBounds; bounds.Encapsulate(mesh.bounds.min); bounds.Encapsulate(mesh.bounds.max);
                renderer.localBounds = bounds; renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                generated.Add(renderer);
            }
            PrepareAnatomy(body);
            foreach (var renderer in sources)
            {
                if (renderer.name != "DF_WeaponR" && renderer.name != "DF_WeaponL" &&
                    renderer.name != "DF_ArticulatedArmorDetails") continue;
                previousRenderers.Add(new RendererState { renderer = renderer, name = renderer.name, enabled = renderer.enabled });
                renderer.enabled = false; renderer.name += "_Legacy";
            }
            foreach (Transform part in transforms)
                if (part.name == "DF_Muzzle") { previousMuzzle = part; previousMuzzleName = part.name; part.name += "_Legacy"; break; }
            int grip = Array.IndexOf(data.boneNames, "WeaponGripR");
            if (grip < 0) throw new InvalidOperationException("Tahla staff grip is missing.");
            var muzzle = new GameObject("DF_Muzzle") { layer = body.gameObject.layer }; objects.Add(muzzle);
            muzzle.transform.SetParent(bones[grip], false);
            Vector3 muzzlePoint = nativeHips + toMesh.MultiplyVector(Point(data.muzzle) - pivot) * units;
            muzzle.transform.localPosition = bind[grip].MultiplyPoint3x4(muzzlePoint);
            foreach (var renderer in generated) renderer.enabled = true;
            IsReady = true;
            HeroVisualDiagnostics.Trace("DF: Tahla approved armor, staff and focus attached to existing rig.", model);
        }

        static float Influence(BoneWeight weight, int bone) => bone < 0 ? 0f :
            (weight.boneIndex0 == bone ? weight.weight0 : 0f) + (weight.boneIndex1 == bone ? weight.weight1 : 0f) +
            (weight.boneIndex2 == bone ? weight.weight2 : 0f) + (weight.boneIndex3 == bone ? weight.weight3 : 0f);
        static bool Skin(string name) => name.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("Flesh", StringComparison.OrdinalIgnoreCase);
        static int BoneIndex(SkinnedMeshRenderer body, string name)
            => Array.FindIndex(body.bones, bone => bone != null && bone.name == name);
        static int FindRoot(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }
        static void Join(int[] parent, int a, int b)
        { a = FindRoot(parent, a); b = FindRoot(parent, b); if (a != b) parent[b] = a; }
        void PrepareAnatomy(SkinnedMeshRenderer body)
        {
            Mesh source = body.sharedMesh;
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length != source.vertexCount) throw new InvalidOperationException("Tahla anatomical weights are missing.");
            
            int head = BoneIndex(body, "Head"), footL = BoneIndex(body, "FootL"), footR = BoneIndex(body, "FootR");
            int[] exposed = { head, BoneIndex(body, "UpperArmL"), BoneIndex(body, "UpperArmR"), BoneIndex(body, "ForearmL"), BoneIndex(body, "ForearmR"), BoneIndex(body, "HandL"), BoneIndex(body, "HandR") };
            var slots = new List<Material>(body.sharedMaterials);
            Material ivory = RuntimeShaderLibrary.Create(new Color(.77f, .80f, .76f), false, 0f, .18f, Color.black);
            if (ivory == null) throw new InvalidOperationException("Tahla undersuit shader is missing.");
            ivory.name = "DF_Tahla_IvoryUndersuit"; materials.Add(ivory);
            int clothSlot = slots.Count; slots.Add(ivory);
            var cloth = new List<int>(); var kept = new List<int>[source.subMeshCount];
            int[] parent = new int[source.vertexCount];
            bool[] selected = new bool[source.vertexCount];
            Vector3[] vertices = source.vertices;
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                string name = sub < body.sharedMaterials.Length && body.sharedMaterials[sub] != null ? body.sharedMaterials[sub].name : "";
                if (!Skin(name) && !name.StartsWith("Obsidian fabric", StringComparison.OrdinalIgnoreCase)) continue;
                int[] triangles = source.GetTriangles(sub);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    selected[a] = selected[b] = selected[c] = true; Join(parent, a, b); Join(parent, a, c);
                }
            }
            var seams = new Dictionary<Vector3Int, int>();
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!selected[i]) continue;
                Vector3 p = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000f), Mathf.RoundToInt(p.y * 100000f), Mathf.RoundToInt(p.z * 100000f));
                if (seams.TryGetValue(key, out int other)) Join(parent, i, other); else seams[key] = i;
            }
            int main = -1, largest = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (!selected[i]) continue;
                int root = FindRoot(parent, i); counts.TryGetValue(root, out int value); counts[root] = ++value;
                if (value > largest) { largest = value; main = root; }
            }
            if (main < 0) throw new InvalidOperationException("Tahla anatomical surface is missing.");
            for (int sub = 0; sub < kept.Length; sub++)
            {
                kept[sub] = new List<int>(); int[] triangles = source.GetTriangles(sub);
                string name = sub < body.sharedMaterials.Length && body.sharedMaterials[sub] != null ? body.sharedMaterials[sub].name.ToLowerInvariant() : "";
                bool skin = Skin(name), fabric = name.StartsWith("obsidian fabric", StringComparison.Ordinal);
                bool face = name.StartsWith("tahla white swept hair", StringComparison.Ordinal) || skin || name.Contains("eye") || name.Contains("lip") || name.StartsWith("energy", StringComparison.Ordinal);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    bool headOnly = true, feetOnly = true, clothed = true;
                    for (int j = 0; j < 3; j++)
                    {
                        BoneWeight weight = weights[triangles[t + j]];
                        headOnly &= Influence(weight, head) >= .5f;
                        feetOnly &= Influence(weight, footL) + Influence(weight, footR) >= .95f;
                        float bare = 0f; foreach (int bone in exposed) bare += Influence(weight, bone);
                        clothed &= bare < .15f;
                    }
                    if (!(skin || fabric && FindRoot(parent, triangles[t]) == main || headOnly && face || feetOnly)) continue;
                    var target = clothed || feetOnly ? cloth : kept[sub];
                    target.Add(triangles[t]); target.Add(triangles[t + 1]); target.Add(triangles[t + 2]);
                }
            }
            Mesh mesh = Instantiate(source); meshes.Add(mesh); mesh.name = source.name + "_TahlaApprovedBase";
            mesh.subMeshCount = kept.Length + 1;
            for (int sub = 0; sub < kept.Length; sub++) mesh.SetTriangles(kept[sub], sub, false);
            mesh.SetTriangles(cloth, kept.Length, false); mesh.RecalculateBounds();
            body.sharedMesh = mesh; body.sharedMaterials = slots.ToArray();
        }
        void Restore()
        {
            IsReady = false;
            if (anatomy != null) { anatomy.sharedMesh = previousAnatomy; anatomy.sharedMaterials = previousAnatomyMaterials; }
            foreach (var state in previousRenderers)
                if (state.renderer != null) { state.renderer.name = state.name; state.renderer.enabled = state.enabled; }
            if (previousMuzzle != null) previousMuzzle.name = previousMuzzleName;
        }
        void Release()
        {
            foreach (var go in objects) if (go != null) { go.SetActive(false); Destroy(go); }
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
            objects.Clear(); meshes.Clear(); materials.Clear();
        }
        void OnDestroy() => Release();
    }
}
