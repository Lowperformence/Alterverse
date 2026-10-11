using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Preserves Brakk's arm surfaces while removing all added arm and shoulder geometry.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkForearmTrim : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedSkinMaterials = new List<Material>();
        Material goldForearms;
        static readonly Color SuitGold = new Color(.82f, .56f, .18f, 1f);
        static readonly Color ReactorRed = new Color(1f, .025f, .012f, 1f);
        const float ReactorEmissionStrength = 6f;

        public static void Apply(Transform model, HeroId hero)
        {
            // Nerok uses the same imported Rook asset: only alter Brakk's instance.
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkForearmTrim>() != null) return;
            BrakkForearmTrim owner = model.gameObject.AddComponent<BrakkForearmTrim>();
            SlimCalves(model, owner);
            RecolorSuit(model, owner);
            SplitRigidArmAttachments(model, owner);
            // Clone skin slots on this instance; Nerok shares the imported asset.
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "DF_Anatomy") continue;
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material original = slots[i];
                    if (original == null || !original.name.StartsWith("Skin", StringComparison.OrdinalIgnoreCase)) continue;
                    var copy = new Material(original) { name = original.name + "_BrakkLighter" };
                    Color color = new Color(.68f, .43f, .31f, 1f);
                    if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", color);
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", color);
                    slots[i] = copy;
                    owner.ownedSkinMaterials.Add(copy);
                }
                renderer.sharedMaterials = slots;
            }
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool anatomy = skin.name.StartsWith("DF_Anatomy", StringComparison.Ordinal);
                bool details = skin.name.StartsWith("DF_ArticulatedArmorDetails", StringComparison.Ordinal) ||
                    skin.sharedMesh != null && skin.sharedMesh.name.StartsWith("DF_ArticulatedArmorDetails", StringComparison.Ordinal);
                bool techDetails = skin.name.StartsWith("DF_LimbTechDetails", StringComparison.Ordinal) ||
                    skin.sharedMesh != null && skin.sharedMesh.name.IndexOf("LimbMicroDetails", StringComparison.Ordinal) >= 0;
                if (!anatomy && !details && !techDetails) continue;
                Mesh source = skin.sharedMesh;
                if (source == null) continue;
                if (!source.isReadable)
                {
                    Debug.LogWarning("DF: Brakk arm cleanup could not read " + skin.name +
                        ". Reimport AuthoredHeroes/Rook.fbx with Read/Write enabled.", model);
                    continue;
                }
                int left = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmL");
                int right = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmR");
                int upperL = Array.FindIndex(skin.bones, b => b != null && b.name == "UpperArmL");
                int upperR = Array.FindIndex(skin.bones, b => b != null && b.name == "UpperArmR");
                int handL = Array.FindIndex(skin.bones, b => b != null && b.name == "HandL");
                int handR = Array.FindIndex(skin.bones, b => b != null && b.name == "HandR");
                if (left < 0 && right < 0 && (!details || handL < 0 && handR < 0 && upperL < 0 && upperR < 0)) continue;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != source.vertexCount) continue;

                var kept = new List<int>[source.subMeshCount];
                var goldForearm = new List<int>();
                int removed = 0;
                Material[] materials = skin.sharedMaterials;
                for (int sub = 0; sub < kept.Length; sub++)
                {
                    kept[sub] = new List<int>();
                    int[] triangles = source.GetTriangles(sub);
                    // Every added arm/shoulder surface is removed regardless of color.
                    // Underlying fabric and skin retain their material slots.
                    bool bracer = sub < materials.Length && ArmOverlay(materials[sub]);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        bool onForearm = (Attached(a, left) && Attached(b, left) && Attached(c, left)) ||
                            (Attached(a, right) && Attached(b, right) && Attached(c, right));
                        bool onUpperArm = (Attached(a, upperL) && Attached(b, upperL) && Attached(c, upperL)) ||
                            (Attached(a, upperR) && Attached(b, upperR) && Attached(c, upperR));
                        bool onHand = (Attached(a, handL) && Attached(b, handL) && Attached(c, handL)) ||
                            (Attached(a, handR) && Attached(b, handR) && Attached(c, handR));
                        // Remove all six imported bracers, not the underlying fabric
                        // anatomy. Wrist/knuckle plates are generated on hand bones.
                        // The four bronze overlays are the detail pass's two upper-arm
                        // plates per side. Remove their imported copies as well.
                        // An imported bronze shell can be assigned to UpperArm even
                        // when the neutral guard pose places it over the forearm.
                        if ((anatomy && bracer && (onForearm || onUpperArm)) || (details && (onUpperArm || onForearm || onHand)) ||
                            (techDetails && (onUpperArm || onForearm)))
                        {
                            removed++;
                            continue;
                        }
                        List<int> target = anatomy && onForearm ? goldForearm : kept[sub];
                        target.Add(a); target.Add(b); target.Add(c);
                    }
                }
                if (removed == 0 && goldForearm.Count == 0) continue;
                if (owner == null)
                {
                    owner = model.gameObject.AddComponent<BrakkForearmTrim>();
                }
                if (goldForearm.Count > 0 && owner.goldForearms == null)
                {
                    owner.goldForearms = RuntimeShaderLibrary.Create(SuitGold, metallic: .65f, smoothness: .32f);
                    if (owner.goldForearms == null) { Debug.LogError("DF: Brakk forearm gold shader is missing.", model); return; }
                    owner.goldForearms.name = "DF_Brakk_GoldForearms";
                }
                Mesh recolored = Instantiate(source);
                recolored.name = source.name + "_BrakkWithoutGoldPlates";
                recolored.subMeshCount = kept.Length + (goldForearm.Count > 0 ? 1 : 0);
                for (int sub = 0; sub < kept.Length; sub++) recolored.SetTriangles(kept[sub], sub, false);
                if (goldForearm.Count > 0) recolored.SetTriangles(goldForearm, kept.Length, false);
                recolored.RecalculateBounds();
                if (goldForearm.Count > 0)
                {
                    Array.Resize(ref materials, kept.Length + 1);
                    materials[kept.Length] = owner.goldForearms;
                    skin.sharedMaterials = materials;
                }
                skin.sharedMesh = recolored;
                owner.ownedMeshes.Add(recolored);
                Debug.Log("DF: Brakk removed " + removed + " plate triangles and colored " +
                    (goldForearm.Count / 3) + " remaining forearm triangles gold on " + skin.name, model);

                bool Attached(int vertex, int bone)
                {
                    if (bone < 0) return false;
                    BoneWeight w = weights[vertex];
                    float influence = (w.boneIndex0 == bone ? w.weight0 : 0f) +
                        (w.boneIndex1 == bone ? w.weight1 : 0f) +
                        (w.boneIndex2 == bone ? w.weight2 : 0f) +
                        (w.boneIndex3 == bone ? w.weight3 : 0f);
                    return influence > .95f;
                }
            }
            RemoveRemainingArmAttachments(model, owner);
            RemoveRigidArmAttachments(model);
            LightSpinalReactor(model, owner);
        }

        // The source FBX merges the rear reactor, chest relic and eye into one
        // Energy slot. Give only the chest-bound rear geometry its own emission.
        static void LightSpinalReactor(Transform model, BrakkForearmTrim owner)
        {
            Shader shader = RuntimeShaderLibrary.Lit;
            if (shader == null) return;
            int litTriangles = 0;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source = skin.sharedMesh;
                if (source == null || !source.isReadable ||
                    !skin.name.StartsWith("DF_Anatomy", StringComparison.Ordinal)) continue;
                int chest = Array.FindIndex(skin.bones, b => b != null && b.name == "Chest");
                Vector3[] points = source.vertices;
                BoneWeight[] weights = source.boneWeights;
                if (chest < 0 || weights.Length != points.Length) continue;
                Matrix4x4 toModel = model.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                Material[] slots = skin.sharedMaterials;
                var materials = new List<Material>();
                for (int sub = 0; sub < source.subMeshCount; sub++)
                    materials.Add(sub < slots.Length ? slots[sub] : null);
                var triangles = new List<int[]>();
                var reactors = new List<int[]>();
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] original = source.GetTriangles(sub);
                    Material material = sub < slots.Length ? slots[sub] : null;
                    string name = material == null ? "" : material.name.ToLowerInvariant();
                    if (!name.StartsWith("energy") && !name.Contains("spinal reactor"))
                    { triangles.Add(original); continue; }
                    var kept = new List<int>();
                    var reactor = new List<int>();
                    for (int t = 0; t < original.Length; t += 3)
                    {
                        int a = original[t], b = original[t + 1], c = original[t + 2];
                        var target = RearReactor(a) && RearReactor(b) && RearReactor(c) ? reactor : kept;
                        target.Add(a); target.Add(b); target.Add(c);
                    }
                    if (reactor.Count == 0) { triangles.Add(original); continue; }
                    var glow = new Material(material) { shader = shader, name = "DF_Brakk_SpinalReactor_FluorescentRed" };
                    glow.SetColor("_BaseColor", ReactorRed);
                    glow.SetColor("_EmissionColor", ReactorRed * ReactorEmissionStrength);
                    glow.SetFloat("_Metallic", .1f);
                    glow.SetFloat("_Smoothness", .4f);
                    glow.EnableKeyword("_EMISSION");
                    owner.ownedSkinMaterials.Add(glow);
                    materials.Add(glow);
                    triangles.Add(kept.ToArray()); reactors.Add(reactor.ToArray());
                    litTriangles += reactor.Count / 3;
                }
                if (reactors.Count == 0) continue;
                // Clone once; preserve vertices, UVs, weights, bind poses and bounds.
                Mesh mesh = Instantiate(source); mesh.name = source.name + "_GlowingSpinalReactor";
                mesh.subMeshCount = triangles.Count + reactors.Count;
                for (int sub = 0; sub < triangles.Count; sub++) mesh.SetTriangles(triangles[sub], sub, false);
                for (int sub = 0; sub < reactors.Count; sub++) mesh.SetTriangles(reactors[sub], triangles.Count + sub, false);
                skin.sharedMesh = mesh; skin.sharedMaterials = materials.ToArray(); owner.ownedMeshes.Add(mesh);

                bool RearReactor(int vertex) => Influence(weights[vertex], chest) > .95f &&
                    toModel.MultiplyPoint3x4(points[vertex]).z < -.04f;
            }
            if (litTriangles == 0) Debug.LogWarning("DF: Brakk's rear spinal reactor could not be identified in the imported model.", model);
        }

        // Delete complete attachment components, including renamed/silver pieces and
        // blended shoulder/wrist weights. The actual fabric/skin stays in its own slots.
        static void RemoveRemainingArmAttachments(Transform model, BrakkForearmTrim owner)
        {
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source = skin.sharedMesh;
                if (source == null || !source.isReadable) continue;
                Vector3[] points = source.vertices;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != points.Length) continue;
                bool anatomy = skin.name.StartsWith("DF_Anatomy", StringComparison.Ordinal) ||
                    source.name.StartsWith("DF_Anatomy", StringComparison.Ordinal);
                int[] arms = new int[4];
                string[] names = { "UpperArmL", "UpperArmR", "ForearmL", "ForearmR" };
                for (int i = 0; i < arms.Length; i++)
                    arms[i] = Array.FindIndex(skin.bones, b => b != null && b.name == names[i]);
                int[] parent = new int[points.Length];
                bool[] selected = new bool[points.Length];
                var extraSlot = new bool[source.subMeshCount];
                Material[] materials = skin.sharedMaterials;
                for (int i = 0; i < parent.Length; i++) parent[i] = i;
                int Find(int i)
                {
                    while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
                    return i;
                }
                void Join(int a, int b) { int x = Find(a), y = Find(b); if (x != y) parent[y] = x; }
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    Material material = sub < materials.Length ? materials[sub] : null;
                    extraSlot[sub] = !anatomy || !BodySurface(material);
                    if (!extraSlot[sub]) continue;
                    int[] triangles = source.GetTriangles(sub);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                        selected[a] = selected[b] = selected[c] = true;
                        Join(a, b); Join(a, c);
                    }
                }
                // Weld lookup indices across normal/UV seams; leave source vertices,
                // UVs, normals and skin weights untouched, including retained surfaces.
                var seams = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < points.Length; i++)
                {
                    if (!selected[i]) continue;
                    Vector3 p = points[i];
                    var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000f),
                        Mathf.RoundToInt(p.y * 100000f), Mathf.RoundToInt(p.z * 100000f));
                    if (seams.TryGetValue(key, out int other)) Join(i, other); else seams[key] = i;
                }
                var remove = new HashSet<int>();
                for (int i = 0; i < points.Length; i++)
                {
                    if (!selected[i]) continue;
                    float arm = 0f;
                    foreach (int bone in arms) arm += Influence(weights[i], bone);
                    if (arm >= .2f) remove.Add(Find(i));
                }
                if (remove.Count == 0) continue;
                var kept = new List<int>[source.subMeshCount];
                int removed = 0;
                for (int sub = 0; sub < kept.Length; sub++)
                {
                    kept[sub] = new List<int>(); int[] triangles = source.GetTriangles(sub);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                        if (extraSlot[sub] && remove.Contains(Find(a))) { removed++; continue; }
                        kept[sub].Add(a); kept[sub].Add(b); kept[sub].Add(c);
                    }
                }
                if (removed == 0) continue;
                Mesh mesh = Instantiate(source); mesh.name = source.name + "_BrakkBareArms";
                for (int sub = 0; sub < kept.Length; sub++) mesh.SetTriangles(kept[sub], sub, false);
                skin.sharedMesh = mesh; owner.ownedMeshes.Add(mesh);
                Debug.Log("DF: Brakk deleted " + removed + " remaining arm/shoulder attachment triangles on " + skin.name, model);
            }
        }

        static bool BodySurface(Material material)
        {
            if (material == null) return false;
            string name = material.name.ToLowerInvariant();
            return Fabric(material) || name.StartsWith("skin") || name.StartsWith("flesh");
        }

        static void RemoveRigidArmAttachments(Transform model)
        {
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                for (Transform bone = renderer.transform; bone != null && bone != model; bone = bone.parent)
                {
                    // Hand-held weapons and their grip children are outside the requested arm regions.
                    if (bone.name == "HandL" || bone.name == "HandR" ||
                        bone.name == "WeaponGripL" || bone.name == "WeaponGripR") break;
                    if (bone.name == "UpperArmL" || bone.name == "UpperArmR" ||
                        bone.name == "ForearmL" || bone.name == "ForearmR")
                    {
                        renderer.enabled = false;
                        break;
                    }
                }
            }
        }

        static float Influence(BoneWeight w, int bone) => bone < 0 ? 0f :
            (w.boneIndex0 == bone ? w.weight0 : 0f) + (w.boneIndex1 == bone ? w.weight1 : 0f) +
            (w.boneIndex2 == bone ? w.weight2 : 0f) + (w.boneIndex3 == bone ? w.weight3 : 0f);

        static bool Fabric(Material material) => material != null &&
            (material.name.IndexOf("fabric", StringComparison.OrdinalIgnoreCase) >= 0 ||
             material.name.StartsWith("DF_Brakk_GoldForearms", StringComparison.Ordinal));

        // Color is not a removal criterion: silver, grey, dark and bronze additions
        // all go. Actual arm skin/fabric and its existing material copies stay.
        static bool ArmOverlay(Material material) => !BodySurface(material);

        static void SplitRigidArmAttachments(Transform model, BrakkForearmTrim owner)
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "DF_Anatomy") continue;
                Mesh source = renderer.sharedMesh;
                if (source == null || !source.isReadable) continue;
                Vector3[] vertices = source.vertices;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != vertices.Length) continue;
                int upperL = Array.FindIndex(renderer.bones, b => b != null && b.name == "UpperArmL");
                int upperR = Array.FindIndex(renderer.bones, b => b != null && b.name == "UpperArmR");
                int foreL = Array.FindIndex(renderer.bones, b => b != null && b.name == "ForearmL");
                int foreR = Array.FindIndex(renderer.bones, b => b != null && b.name == "ForearmR");
                if (upperL < 0 && upperR < 0 && foreL < 0 && foreR < 0) continue;

                // Cages/straps can share the suit's fabric material. Separate rigid
                // arm components into a removable slot before filtering them out;
                // the connected anatomical body keeps its surface and texture.
                int[] parent = new int[vertices.Length];
                var referenced = new bool[vertices.Length];
                var triangles = new int[source.subMeshCount][];
                for (int i = 0; i < parent.Length; i++) parent[i] = i;
                int Find(int index)
                {
                    while (parent[index] != index)
                    { parent[index] = parent[parent[index]]; index = parent[index]; }
                    return index;
                }
                void Join(int a, int b)
                {
                    int first = Find(a), second = Find(b);
                    if (first != second) parent[second] = first;
                }
                for (int sub = 0; sub < triangles.Length; sub++)
                {
                    triangles[sub] = source.GetTriangles(sub);
                    int[] indices = triangles[sub];
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                        referenced[a] = referenced[b] = referenced[c] = true;
                        Join(a, b); Join(a, c);
                    }
                }
                // FBX normals and UV seams can split indices along an otherwise
                // connected part. Weld only the lookup, leaving all mesh data intact.
                var coincident = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!referenced[i]) continue;
                    Vector3 p = vertices[i];
                    var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000f),
                        Mathf.RoundToInt(p.y * 100000f), Mathf.RoundToInt(p.z * 100000f));
                    if (coincident.TryGetValue(key, out int other)) Join(i, other);
                    else coincident.Add(key, i);
                }
                float Influence(BoneWeight w, int bone) => bone < 0 ? 0f :
                    (w.boneIndex0 == bone ? w.weight0 : 0f) + (w.boneIndex1 == bone ? w.weight1 : 0f) +
                    (w.boneIndex2 == bone ? w.weight2 : 0f) + (w.boneIndex3 == bone ? w.weight3 : 0f);
                int[] componentArm = new int[vertices.Length];
                for (int i = 0; i < componentArm.Length; i++) componentArm[i] = -2;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!referenced[i]) continue;
                    int root = Find(i);
                    int arm = Influence(weights[i], upperL) > .95f ? upperL :
                        Influence(weights[i], upperR) > .95f ? upperR :
                        Influence(weights[i], foreL) > .95f ? foreL :
                        Influence(weights[i], foreR) > .95f ? foreR : -1;
                    if (componentArm[root] == -2) componentArm[root] = arm;
                    else if (componentArm[root] != arm) componentArm[root] = -1;
                }
                Material[] materials = renderer.sharedMaterials;
                var kept = new List<int>[triangles.Length];
                var grey = new List<int>();
                for (int sub = 0; sub < triangles.Length; sub++)
                {
                    kept[sub] = new List<int>();
                    bool suit = sub < materials.Length && materials[sub] != null &&
                        materials[sub].name.StartsWith("Obsidian fabric", StringComparison.OrdinalIgnoreCase);
                    int[] indices = triangles[sub];
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        List<int> target = suit && componentArm[Find(indices[i])] >= 0 ? grey : kept[sub];
                        target.Add(indices[i]); target.Add(indices[i + 1]); target.Add(indices[i + 2]);
                    }
                }
                if (grey.Count == 0) continue;
                Material shoulder = RuntimeShaderLibrary.Create(new Color(.43f, .47f, .50f, 1f),
                    metallic: .55f, smoothness: .30f);
                if (shoulder == null) { Debug.LogError("DF: Brakk shoulder grey shader is missing.", model); continue; }
                shoulder.name = "DF_Brakk_GreyShoulderPlates";
                owner.ownedSkinMaterials.Add(shoulder);
                Mesh mesh = Instantiate(source);
                mesh.name = source.name + "_BrakkGreyShoulders";
                mesh.subMeshCount = kept.Length + 1;
                for (int sub = 0; sub < kept.Length; sub++) mesh.SetTriangles(kept[sub], sub, false);
                mesh.SetTriangles(grey, kept.Length, false);
                Array.Resize(ref materials, kept.Length + 1);
                materials[kept.Length] = shoulder;
                renderer.sharedMesh = mesh; renderer.sharedMaterials = materials;
                owner.ownedMeshes.Add(mesh);
            }
        }

        static void RecolorSuit(Transform model, BrakkForearmTrim owner)
        {
            // Work on this Brakk instance only; Nerok shares the Rook FBX.
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "DF_Anatomy") continue;
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material original = slots[i];
                    if (original == null) continue;
                    string name = original.name.Replace(" (Instance)", "");
                    // Preserve red enamel, skin, silver armor and mechanical details.
                    if (name != "Obsidian fabric" && name != "Obsidian fabric_MatteWeave") continue;
                    var copy = new Material(original) { name = original.name + "_BrakkGold" };
                    if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", SuitGold);
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", SuitGold);
                    slots[i] = copy;
                    owner.ownedSkinMaterials.Add(copy);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = slots;
            }
        }

        static void SlimCalves(Transform model, BrakkForearmTrim owner)
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source = renderer.sharedMesh;
                if (source == null || !source.isReadable) continue;
                Vector3[] vertices = source.vertices;
                BoneWeight[] weights = source.boneWeights;
                Matrix4x4[] bind = source.bindposes;
                if (weights.Length != vertices.Length) continue;
                bool changed = false;
                foreach (string side in new[] { "L", "R" })
                {
                    int shin = Array.FindIndex(renderer.bones, b => b != null && b.name == "Shin" + side);
                    int foot = Array.FindIndex(renderer.bones, b => b != null && b.name == "Foot" + side);
                    if (shin < 0 || foot < 0 || shin >= bind.Length || foot >= bind.Length) continue;
                    Vector3 knee = bind[shin].inverse.MultiplyPoint3x4(Vector3.zero);
                    Vector3 axis = bind[foot].inverse.MultiplyPoint3x4(Vector3.zero) - knee;
                    if (axis.sqrMagnitude < 1e-10f) continue;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        BoneWeight w = weights[i];
                        float influence = (w.boneIndex0 == shin ? w.weight0 : 0f) +
                            (w.boneIndex1 == shin ? w.weight1 : 0f) +
                            (w.boneIndex2 == shin ? w.weight2 : 0f) +
                            (w.boneIndex3 == shin ? w.weight3 : 0f);
                        if (influence <= 0f) continue;
                        float t = Vector3.Dot(vertices[i] - knee, axis) / axis.sqrMagnitude;
                        if (t <= 0f || t >= 1f) continue;
                        // Preserve joint seams and length; narrow the calf and its fitted armor together.
                        float taper = Mathf.Clamp01(Mathf.Min(t, 1f - t) / .10f);
                        Vector3 radial = vertices[i] - (knee + axis * t);
                        vertices[i] -= radial * (.20f * influence * taper);
                        changed = true;
                    }
                }
                if (!changed) continue;
                Mesh mesh = Instantiate(source);
                mesh.name = source.name + "_BrakkSlimCalves";
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                renderer.sharedMesh = mesh;
                Bounds bounds = renderer.localBounds;
                bounds.Encapsulate(mesh.bounds);
                renderer.localBounds = bounds;
                owner.ownedMeshes.Add(mesh);
            }
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in ownedSkinMaterials) if (material != null) Destroy(material);
            if (goldForearms != null) Destroy(goldForearms);
        }
    }
}


