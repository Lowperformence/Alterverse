using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Contoured anatomical armor plates, bevel trims, glowing energy conduits and fasteners on hero limbs & back.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroLimbTechDetails : MonoBehaviour
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<BoneWeight> weights = new List<BoneWeight>();
        readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
        readonly List<Material> materials = new List<Material>();
        Mesh mesh;
        static Texture2D sharedArmorNormal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => sharedArmorNormal = null;

        static Texture2D GetArmorNormalMap()
        {
            if (sharedArmorNormal != null) return sharedArmorNormal;
            const int size = 64;
            sharedArmorNormal = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "DF_AnodizedArmorNormal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    // Micro-brushed metal grain + subtle hexagonal panel bevels
                    float brush = Mathf.Cos((u * 18f + Mathf.Sin(v * 6.2831853f) * 0.15f) * 6.2831853f) * 0.14f;
                    float grooveU = (u > 0.08f && u < 0.12f) ? -0.45f : (u > 0.88f && u < 0.92f) ? 0.45f : 0f;
                    float grooveV = (v > 0.10f && v < 0.14f) ? -0.40f : (v > 0.86f && v < 0.90f) ? 0.40f : 0f;
                    Vector3 n = new Vector3(brush + grooveU, grooveV, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            sharedArmorNormal.SetPixels(pixels);
            sharedArmorNormal.Apply(false, false);
            return sharedArmorNormal;
        }

        public static void Apply(Transform model, HeroId hero)
        {
            if (model == null || model.GetComponent<HeroLimbTechDetails>() != null) return;
            SkinnedMeshRenderer anatomy = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == "DF_Anatomy") { anatomy = skin; break; }
            if (anatomy == null || anatomy.sharedMesh == null || !anatomy.sharedMesh.isReadable) return;
            var detail = model.gameObject.AddComponent<HeroLimbTechDetails>();
            detail.Build(model, anatomy, hero);
        }

        void Build(Transform model, SkinnedMeshRenderer anatomy, HeroId hero)
        {
            Mesh source = anatomy.sharedMesh;
            Vector3[] points = source.vertices;
            BoneWeight[] skinWeights = source.boneWeights;
            Matrix4x4[] bind = source.bindposes;
            Matrix4x4 modelToMesh = anatomy.transform.worldToLocalMatrix * model.localToWorldMatrix;
            Vector3 meshForward = modelToMesh.MultiplyVector(Vector3.forward).normalized;
            if (meshForward.sqrMagnitude < .01f) meshForward = Vector3.forward;

            bool hasComponentFront =
                (hero == HeroId.Nyra && model.GetComponent<CyberRangerAppearance>() != null) ||
                (hero == HeroId.Elyra && model.GetComponent<UmbralMageAppearance>() != null) ||
                (hero == HeroId.Traveler && model.GetComponent<ChronoTravelerAppearance>() != null) ||
                (hero == HeroId.Mira && model.GetComponent<HealerLegArmorAppearance>() != null);

            // Dorsal Torso & Lumbar Back Armor Plates (contoured around the spine/ribcage)
            if (hero != HeroId.Mira)
            {
                Panel("Chest", "Spine", .38f, .40f, -1f, .52f);
                Panel("Spine", "Hips", .48f, .42f, -1f, .44f);
                Panel("Hips", "Spine", .18f, .30f, -1f, .40f);
            }

            foreach (string side in new[] { "L", "R" })
            {
                if (!hasComponentFront)
                {
                    if (hero != HeroId.Rook)
                    {
                        Panel("UpperArm" + side, "Forearm" + side, .62f, .20f, 1f, .42f);
                        Panel("Forearm" + side, "Hand" + side, .52f, .34f, 1f, .42f);
                    }
                    Panel("Thigh" + side, "Shin" + side, .56f, .24f, 1f, .42f);
                    Panel("Shin" + side, "Foot" + side, .54f, .38f, 1f, .42f);
                }

                // Contoured Rear Limb Armor (Tricep, Rear Forearm, Hamstring, and Calf plates)
                if (hero != HeroId.Mira)
                {
                    if (hero != HeroId.Rook)
                    {
                        Panel("UpperArm" + side, "Forearm" + side, .52f, .24f, -1f, .42f);
                        Panel("Forearm" + side, "Hand" + side, .48f, .32f, -1f, .40f);
                    }
                    Panel("Thigh" + side, "Shin" + side, .38f, .22f, -1f, .44f);
                    Panel("Thigh" + side, "Shin" + side, .68f, .20f, -1f, .40f);
                    Panel("Shin" + side, "Foot" + side, .36f, .24f, -1f, .42f);
                    Panel("Shin" + side, "Foot" + side, .66f, .22f, -1f, .38f);
                }
            }

            if (vertices.Count == 0) return;

            Color accent = hero == HeroId.Nyra ? new Color(.025f, .72f, .40f) : StylizedArtDirection.HeroAccent(hero);
            Color plateColor = hero switch
            {
                HeroId.Elyra => new Color(.24f, .20f, .33f),
                HeroId.Kaelon => new Color(.42f, .39f, .25f),
                HeroId.Mira => new Color(.20f, .33f, .28f),
                HeroId.Nerok => new Color(.45f, .50f, .53f),
                HeroId.Nyra => new Color(.095f, .135f, .142f),
                HeroId.Traveler => new Color(.25f, .29f, .33f),
                HeroId.Veyr => new Color(.078f, .068f, .112f),
                _ => new Color(.115f, .135f, .160f)
            };

            Material metal = RuntimeShaderLibrary.Create(plateColor, false,
                hero == HeroId.Nyra ? .80f : hero == HeroId.Rook ? .68f : .74f,
                hero == HeroId.Nyra ? .66f : hero == HeroId.Rook ? .48f : .62f);
            Material circuits = RuntimeShaderLibrary.Create(Color.Lerp(accent, Color.white, .18f), false, .25f, .62f, accent * 1.15f);
            Color fastenerColor = hero == HeroId.Rook || hero == HeroId.Kaelon || hero == HeroId.Mira
                ? new Color(.48f, .40f, .26f)
                : hero == HeroId.Nyra ? new Color(.52f, .63f, .63f)
                : hero == HeroId.Veyr ? new Color(.36f, .32f, .44f)
                : new Color(.48f, .52f, .56f);
            Material screws = RuntimeShaderLibrary.Create(fastenerColor, false, .84f, .64f);

            Texture2D normalTex = GetArmorNormalMap();
            foreach (Material mat in new[] { metal, screws })
            {
                if (mat == null || normalTex == null) continue;
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normalTex);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", .22f);
                if (mat.HasProperty("_NormalEncoding")) mat.SetFloat("_NormalEncoding", 0f);
            }
            if (circuits != null) circuits.EnableKeyword("_EMISSION");

            materials.Add(metal); materials.Add(circuits); materials.Add(screws);
            if (metal == null || circuits == null || screws == null) return;
            metal.name = "DF_LimbMicroArmor"; circuits.name = "DF_LimbCircuitAccents"; screws.name = "DF_LimbFasteners";

            mesh = new Mesh { name = "DF_" + hero + "_LimbMicroDetails" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray(); mesh.bindposes = bind;
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();

            var go = new GameObject("DF_LimbTechDetails"); go.layer = anatomy.gameObject.layer;
            go.transform.SetParent(anatomy.transform, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterials = materials.ToArray();
            renderer.bones = anatomy.bones; renderer.rootBone = anatomy.rootBone;
            Bounds bounds = anatomy.localBounds; bounds.Encapsulate(mesh.bounds); renderer.localBounds = bounds;
            renderer.quality = anatomy.quality; renderer.updateWhenOffscreen = anatomy.updateWhenOffscreen;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            vertices.Clear(); normals.Clear(); uvs.Clear(); weights.Clear(); foreach (var list in triangles) list.Clear();

            void Panel(string boneName, string nextName, float fraction, float span, float facingSign, float widthFactor)
            {
                int bone = Array.FindIndex(anatomy.bones, b => b != null && b.name == boneName);
                int next = Array.FindIndex(anatomy.bones, b => b != null && b.name == nextName);
                if (bone < 0 || next < 0 || bone >= bind.Length || next >= bind.Length) return;
                Matrix4x4 toBone = bind[bone];
                Vector3 end = toBone.MultiplyPoint3x4(bind[next].inverse.MultiplyPoint3x4(Vector3.zero));
                float length = end.magnitude; if (length < .01f) return;
                Vector3 axis = end / length;
                Vector3 front = Vector3.ProjectOnPlane(toBone.MultiplyVector(meshForward * facingSign), axis).normalized;
                if (front.sqrMagnitude < .01f) front = Vector3.ProjectOnPlane(toBone.MultiplyVector(Vector3.forward * facingSign), axis).normalized;
                if (front.sqrMagnitude < .01f) front = Vector3.ProjectOnPlane(toBone.MultiplyVector(Vector3.right * facingSign), axis).normalized;
                Vector3 across = Vector3.Cross(axis, front).normalized;
                float along = length * fraction;
                float face = float.NegativeInfinity, left = float.PositiveInfinity, right = float.NegativeInfinity;
                int samples = 0;
                for (int pass = 0; pass < 2 && samples < 3; pass++)
                {
                    face = float.NegativeInfinity; left = float.PositiveInfinity; right = float.NegativeInfinity; samples = 0;
                    for (int i = 0; i < points.Length && i < skinWeights.Length; i++)
                    {
                        var w = skinWeights[i];
                        float influence = (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0)
                            + (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);
                        if (influence < .45f) continue;
                        Vector3 p = toBone.MultiplyPoint3x4(points[i]);
                        if (Mathf.Abs(Vector3.Dot(p, axis) - along) > length * (pass == 0 ? .18f : .52f)) continue;
                        face = Mathf.Max(face, Vector3.Dot(p, front));
                        float horizontal = Vector3.Dot(p, across); left = Mathf.Min(left, horizontal); right = Mathf.Max(right, horizontal); samples++;
                    }
                }
                if (samples < 3 || right - left < .005f || face <= 0f) return;
                float width = Mathf.Clamp((right - left) * widthFactor, length * .11f, length * .42f);
                float height = length * span, depth = length * .014f;
                float wrapRadius = Mathf.Max(face, width * .62f);
                Vector3 center = axis * along + across * ((left + right) * .5f) + front * (face + depth * .32f);
                Matrix4x4 toMesh = bind[bone].inverse;
                Matrix4x4 normalMatrix = toMesh.inverse.transpose;

                // Base contoured armor shell + chamfered upper trim plate
                Box(center, new Vector3(width, height, depth), 0);
                Box(center + front * depth * .46f, new Vector3(width * .78f, height * .76f, depth * .28f), 2);
                for (int j = 0; j < 3; j++)
                {
                    float y = (j - 1) * height * .24f;
                    Box(center + axis * y + front * depth * .66f, new Vector3(width * .56f, height * .055f, depth * .20f), 1);
                }
                foreach (float sx in new[] { -.33f, .33f }) foreach (float sy in new[] { -.33f, .33f })
                    Box(center + across * (width * sx) + axis * (height * sy) + front * depth * .68f,
                        new Vector3(width * .09f, width * .09f, depth * .24f), 2);

                void Box(Vector3 c, Vector3 size, int material)
                {
                    const int cornerSteps = 4, contourCount = cornerSteps * 4;
                    float radius = Mathf.Min(size.x, size.y) * .25f, bevel = Mathf.Min(radius * .48f, size.z * .42f);
                    Vector3 arcOrigin = c - front * wrapRadius;
                    int start = vertices.Count;
                    for (int ring = 0; ring < 4; ring++)
                    {
                        float angle = ring < 2 ? 0f : (ring == 2 ? Mathf.PI * .25f : Mathf.PI * .5f);
                        float inset = bevel * (1f - Mathf.Cos(angle));
                        float z = ring == 0 ? -size.z * .5f : size.z * .5f - bevel + bevel * Mathf.Sin(angle);
                        for (int corner = 0; corner < 4; corner++) for (int step = 0; step < cornerSteps; step++)
                        {
                            float a = (corner * 90f + step * 90f / (cornerSteps - 1)) * Mathf.Deg2Rad;
                            Vector2 outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            float sx = corner == 0 || corner == 3 ? 1f : -1f, sy = corner < 2 ? 1f : -1f;
                            Vector2 xy = new Vector2(sx * (size.x * .5f - radius), sy * (size.y * .5f - radius)) + outward * (radius - inset);

                            // Wrap xy.x smoothly around the cylindrical limb radius so edges hug the anatomy
                            float theta = Mathf.Clamp(xy.x / wrapRadius, -1.05f, 1.05f);
                            float sinT = Mathf.Sin(theta), cosT = Mathf.Cos(theta);
                            Vector3 radialDir = front * cosT + across * sinT;
                            Vector3 radialTan = across * cosT - front * sinT;
                            float axialTaper = 1f - 0.04f * Mathf.Clamp01((xy.y * xy.y) / Mathf.Max(1e-5f, height * height * .25f));
                            Vector3 p = arcOrigin + radialDir * (wrapRadius * axialTaper + z) + axis * xy.y;
                            Vector3 n = (radialTan * outward.x + axis * outward.y) * Mathf.Cos(angle) + radialDir * Mathf.Sin(angle);

                            vertices.Add(toMesh.MultiplyPoint3x4(p));
                            normals.Add(normalMatrix.MultiplyVector(n).normalized);
                            uvs.Add(new Vector2(xy.x / size.x + .5f, xy.y / size.y + .5f));
                            weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                        }
                    }
                    for (int ring = 0; ring < 3; ring++) for (int j = 0; j < contourCount; j++)
                    {
                        int a = start + ring * contourCount + j, b = start + ring * contourCount + (j + 1) % contourCount;
                        int d = a + contourCount, e = b + contourCount;
                        triangles[material].AddRange(new[] { a, b, d, b, e, d });
                    }
                    Cap(3, 1); Cap(0, -1);
                    void Cap(int ring, int direction)
                    {
                        int cap = vertices.Count;
                        Vector3 center = arcOrigin + front * (wrapRadius + direction * size.z * .5f);
                        Vector3 normal = normalMatrix.MultiplyVector(front * direction).normalized;
                        vertices.Add(toMesh.MultiplyPoint3x4(center)); normals.Add(normal); uvs.Add(new Vector2(.5f, .5f));
                        weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                        for (int j = 0; j < contourCount; j++)
                        {
                            int edge = start + ring * contourCount + j;
                            vertices.Add(vertices[edge]); normals.Add(normals[edge]); uvs.Add(uvs[edge]);
                            weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                        }
                        for (int j = 0; j < contourCount; j++)
                        {
                            int a = cap + 1 + j, b = cap + 1 + (j + 1) % contourCount;
                            triangles[material].AddRange(direction > 0 ? new[] { cap, a, b } : new[] { cap, b, a });
                        }
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
