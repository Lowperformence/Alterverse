using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Reference-shaped round-toe ankle boots on every hero's existing leg rig.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroBootPresentation : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();

        struct Fit
        {
            public Vector3 Origin;
            public float Width, Length, Height;
            public Transform Foot, Shin;
        }

        public static void Apply(Transform model, HeroId hero)
        {
            if (model == null || model.GetComponent<HeroBootPresentation>() != null) return;
            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var fits = new Fit[2];
            for (int side = 0; side < 2; side++)
            {
                string suffix = side == 0 ? "L" : "R";
                Transform foot = Array.Find(bones, b => b.name == "Foot" + suffix);
                Transform shin = Array.Find(bones, b => b.name == "Shin" + suffix);
                if (foot == null || shin == null || !TryFit(model, skins, foot, shin, out fits[side]))
                {
                    Debug.LogWarning("DF: reference boots need readable footwear and both foot bones on " + hero, model);
                    return; // Keep the complete original pair if a rig cannot be fitted.
                }
            }
            var owner = model.gameObject.AddComponent<HeroBootPresentation>();
            if (!owner.CreateMaterials(hero)) { Destroy(owner); return; }
            var geometry = new Geometry();
            for (int side = 0; side < 2; side++)
            {
                Fit fit = fits[side];
                geometry.Boot(fit.Width, fit.Length, fit.Height, fit.Origin, side * 2, side * 2 + 1);
            }
            Mesh mesh = geometry.Mesh("DF_" + hero + "_ReferenceBoots");
            owner.ownedMeshes.Add(mesh);
            Transform[] shoeBones = { fits[0].Foot, fits[0].Shin, fits[1].Foot, fits[1].Shin };
            var bind = new Matrix4x4[shoeBones.Length];
            for (int i = 0; i < bind.Length; i++) bind[i] = shoeBones[i].worldToLocalMatrix * model.localToWorldMatrix;
            mesh.bindposes = bind;
            var go = new GameObject("DF_ReferenceBoots");
            go.layer = model.gameObject.layer;
            go.transform.SetParent(model, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = owner.ownedMaterials.ToArray();
            renderer.bones = shoeBones; renderer.rootBone = model;
            renderer.quality = SkinQuality.Bone2;
            // The lightweight paired mesh follows the same culling policy as the body.
            renderer.updateWhenOffscreen = skins.Length > 0 && skins[0].updateWhenOffscreen;
            Bounds bounds = mesh.bounds; bounds.Expand(new Vector3(.15f, .35f, .35f));
            renderer.localBounds = bounds;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            foreach (SkinnedMeshRenderer skin in skins) owner.RemoveOldFeet(model, skin, fits);
        }

        static bool TryFit(Transform model, SkinnedMeshRenderer[] skins, Transform foot, Transform shin, out Fit fit)
        {
            fit = default;
            Vector3 ankle = model.InverseTransformPoint(foot.position);
            float calf = Vector3.Distance(ankle, model.InverseTransformPoint(shin.position));
            if (calf < .025f) return false;
            bool found = false; Bounds bounds = default;
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Mesh mesh = skin.sharedMesh;
                if (!skin.enabled || !skin.gameObject.activeInHierarchy || mesh == null || !mesh.isReadable) continue;
                int bone = Array.IndexOf(skin.bones, foot);
                if (bone < 0) continue;
                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                Matrix4x4[] bind = mesh.bindposes;
                if (weights.Length != vertices.Length || bone >= bind.Length) continue;
                Matrix4x4 toModel = model.worldToLocalMatrix * foot.localToWorldMatrix * bind[bone];
                var visible = new bool[vertices.Length];
                foreach (int index in mesh.triangles) visible[index] = true;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!visible[i] || Influence(weights[i], bone) < .95f) continue;
                    Vector3 p = toModel.MultiplyPoint3x4(vertices[i]);
                    // Decorative outliers must not inflate a shoe or shift its sole.
                    if (Mathf.Abs(p.x - ankle.x) > calf * .65f || Mathf.Abs(p.z - ankle.z) > calf * 1.4f ||
                        p.y < ankle.y - calf * .5f || p.y > ankle.y + calf * .38f) continue;
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (!found || bounds.size.x < .005f || bounds.size.z < .01f) return false;
            float width = Mathf.Clamp(bounds.size.x, calf * .30f, calf * .64f);
            float length = Mathf.Clamp(width * 2.05f, calf * .72f, calf * 1.10f);
            float height = Mathf.Clamp(length * .82f, calf * .48f, calf * .80f);
            fit = new Fit { Foot = foot, Shin = shin, Width = width, Length = length, Height = height,
                Origin = new Vector3(ankle.x, bounds.min.y, ankle.z - length * .26f) };
            return true;
        }

        void RemoveOldFeet(Transform model, SkinnedMeshRenderer skin, Fit[] fits)
        {
            Mesh source = skin.sharedMesh;
            if (!skin.enabled || source == null || !source.isReadable) return;
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length != source.vertexCount) return;
            int left = Array.IndexOf(skin.bones, fits[0].Foot), right = Array.IndexOf(skin.bones, fits[1].Foot);
            if (left < 0 && right < 0) return;
            Vector3[] vertices = source.vertices;
            Matrix4x4[] bind = source.bindposes;
            var toModel = new Matrix4x4[2];
            var top = new float[2];
            for (int side = 0; side < 2; side++)
            {
                int bone = side == 0 ? left : right;
                if (bone < 0 || bone >= bind.Length) continue;
                Fit fit = fits[side];
                toModel[side] = model.worldToLocalMatrix * fit.Foot.localToWorldMatrix * bind[bone];
                Vector3 ankle = model.InverseTransformPoint(fit.Foot.position);
                top[side] = ankle.y + Vector3.Distance(ankle, model.InverseTransformPoint(fit.Shin.position)) * .9f;
            }
            var remove = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                for (int side = 0; side < 2; side++)
                {
                    int bone = side == 0 ? left : right;
                    if (bone < 0 || bone >= bind.Length || Influence(weights[i], bone) < .5f) continue;
                    Fit fit = fits[side];
                    Vector3 p = toModel[side].MultiplyPoint3x4(vertices[i]);
                    if (Mathf.Abs(p.x - fit.Origin.x) < fit.Width * 1.8f &&
                        p.z > fit.Origin.z - fit.Length * .5f && p.z < fit.Origin.z + fit.Length * 1.5f &&
                        p.y < top[side] && p.y > fit.Origin.y - fit.Height * .3f)
                        remove[i] = true;
                }
            var kept = new List<int>[source.subMeshCount]; bool changed = false;
            for (int sub = 0; sub < kept.Length; sub++)
            {
                kept[sub] = new List<int>(); int[] triangles = source.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    if (remove[a] && remove[b] && remove[c]) { changed = true; continue; }
                    kept[sub].Add(a); kept[sub].Add(b); kept[sub].Add(c);
                }
            }
            if (!changed) return;
            Mesh mesh = Instantiate(source); mesh.name = source.name + "_WithoutOldFeet";
            for (int sub = 0; sub < kept.Length; sub++) mesh.SetTriangles(kept[sub], sub, false);
            skin.sharedMesh = mesh; ownedMeshes.Add(mesh);
        }

        internal static float Influence(BoneWeight w, int bone)
        {
            if (bone < 0) return 0f;
            return (w.boneIndex0 == bone ? w.weight0 : 0f) + (w.boneIndex1 == bone ? w.weight1 : 0f) +
                (w.boneIndex2 == bone ? w.weight2 : 0f) + (w.boneIndex3 == bone ? w.weight3 : 0f);
        }

        public static void ApplyProcedural(Transform root, HeroId hero)
        {
            if (root == null || root.GetComponent<HeroBootPresentation>() != null) return;
            Transform left = root.Find("DF_BootL"), right = root.Find("DF_BootR");
            if (left == null || right == null) return;
            var owner = root.gameObject.AddComponent<HeroBootPresentation>();
            if (!owner.CreateMaterials(hero)) { Destroy(owner); return; }
            foreach (Transform boot in new[] { left, right })
            {
                MeshFilter old = boot.GetComponent<MeshFilter>();
                if (old == null || old.sharedMesh == null) continue;
                Bounds bounds = old.sharedMesh.bounds;
                float width = bounds.size.x, length = width * 2.05f, height = length * .78f;
                var geometry = new Geometry();
                geometry.Boot(width, length, height,
                    new Vector3(bounds.center.x, bounds.min.y, bounds.center.z - length * .32f), 0, 1);
                Mesh mesh = geometry.Mesh("DF_" + hero + "_ReferenceBootFallback");
                owner.ownedMeshes.Add(mesh);
                foreach (Renderer oldRenderer in boot.GetComponentsInChildren<Renderer>(true)) oldRenderer.enabled = false;
                var go = new GameObject("DF_ReferenceBoot"); go.layer = boot.gameObject.layer;
                go.transform.SetParent(boot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = owner.ownedMaterials.ToArray();
            }
        }

        bool CreateMaterials(HeroId hero)
        {
            // Dark uppers, rubber soles and restrained palette accents keep the shape readable.
            Color body = hero switch
            {
                HeroId.Veyr => new Color(.065f,.055f,.085f),
                HeroId.Kaelon => new Color(.19f,.21f,.15f),
                HeroId.Nyra => new Color(.095f,.12f,.14f),
                HeroId.Elyra => new Color(.20f,.21f,.25f),
                HeroId.Mira => new Color(.18f,.26f,.22f),
                HeroId.Rook => new Color(.22f,.18f,.12f),
                HeroId.Traveler => new Color(.13f,.12f,.19f),
                HeroId.Nerok => new Color(.17f,.22f,.25f),
                _ => new Color(.13f,.15f,.18f)
            };
            Color trim = Color.Lerp(StylizedArtDirection.HeroAccent(hero), new Color(.42f,.43f,.45f), .55f);
            ownedMaterials.Add(RuntimeShaderLibrary.Create(body, false, .12f, .30f));
            ownedMaterials.Add(RuntimeShaderLibrary.Create(new Color(.035f,.04f,.045f), false, 0f, .18f));
            ownedMaterials.Add(RuntimeShaderLibrary.Create(trim, false, .42f, .36f));
            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                if (ownedMaterials[i] == null) return false;
                ownedMaterials[i].name = "DF_" + hero + (i == 0 ? "_BootUpper" : i == 1 ? "_BootSole" : "_BootTrim");
            }
            return true;
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
        }

        // Horizontal sections form a rounded toe, rising vamp and slim ankle shaft.
        // A separate heel and forefoot outsole leave the reference's visible arch recess.
        internal sealed class Geometry
        {
            const int Sides = 32;
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<BoneWeight> weights = new List<BoneWeight>();
            readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
            Vector3 origin; float width, length, height; int foot, shin;

            internal void Boot(float w, float l, float h, Vector3 o, int footBone, int shinBone)
            {
                width = w; length = l; height = h; origin = o; foot = footBone; shin = shinBone;
                // Welt runs around the complete footprint; outsole has a modest raised arch.
                Loft(new[]{.075f,.09f,.125f,.14f}, new[]{.96f,1.02f,1.02f,.96f},
                    new[]{.02f,0f,0f,.02f}, new[]{.98f,1f,1f,.98f}, 1, true);
                Loft(new[]{0f,.018f,.08f,.095f}, new[]{.75f,.82f,.87f,.88f},
                    new[]{.015f,0f,0f,0f}, new[]{.29f,.31f,.33f,.34f}, 1, true);
                Loft(new[]{.025f,.04f,.085f,.095f}, new[]{.86f,.94f,.98f,.98f},
                    new[]{.44f,.42f,.40f,.40f}, new[]{.97f,1f,1f,1f}, 1, true);
                Loft(new[]{.13f,.17f,.23f,.30f,.39f,.51f,.66f,.85f,.91f},
                    new[]{.91f,.94f,.93f,.90f,.85f,.76f,.70f,.73f,.73f},
                    new[]{.03f,.035f,.045f,.06f,.08f,.095f,.105f,.095f,.095f},
                    new[]{.98f,.97f,.94f,.91f,.74f,.60f,.51f,.50f,.50f}, 0, false);
                // Hollow padded collar: no flat cylinder lid across the leg.
                Loft(new[]{.91f,.94f,.98f,1f}, new[]{.73f,.79f,.80f,.77f},
                    new[]{.095f,.085f,.08f,.09f}, new[]{.50f,.515f,.52f,.51f}, 0, false);
                Collar();
                // Small lower-ankle band and a rear pull tab echo the reference construction.
                Loft(new[]{.53f,.55f,.575f,.60f}, new[]{.80f,.82f,.82f,.77f},
                    new[]{.075f,.07f,.07f,.075f}, new[]{.62f,.625f,.61f,.57f}, 2, false);
                Tab();
            }

            Vector3 Point(float y, float span, float back, float front, int side)
            {
                float a = side * Mathf.PI * 2f / Sides;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), .72f) * width * span * .5f;
                float z = (back + front) * .5f + Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), .78f) * (front - back) * .5f;
                return origin + new Vector3(x, y * height, z * length);
            }

            int Vertex(Vector3 p)
            {
                int index = vertices.Count; vertices.Add(p);
                uv.Add(new Vector2((p.x-origin.x)/width+.5f, (p.z-origin.z)/length+(p.y-origin.y)/height));
                float blend = Mathf.InverseLerp(height * .55f, height * .98f, p.y-origin.y) * .7f;
                weights.Add(new BoneWeight { boneIndex0 = foot, weight0 = 1f-blend, boneIndex1 = shin, weight1 = blend });
                return index;
            }

            void Loft(float[] y, float[] span, float[] back, float[] front, int slot, bool capTop)
            {
                int start = vertices.Count;
                for (int row = 0; row < y.Length; row++)
                    for (int side = 0; side < Sides; side++) Vertex(Point(y[row],span[row],back[row],front[row],side));
                for (int row = 0; row < y.Length-1; row++)
                    for (int side = 0; side < Sides; side++)
                    {
                        int a = start+row*Sides+side, d = start+row*Sides+(side+1)%Sides;
                        Quad(a,a+Sides,d+Sides,d,slot);
                    }
                if (capTop) Cap(start,slot,false);
                if (capTop) Cap(start+(y.Length-1)*Sides,slot,true);
            }

            void Collar()
            {
                int start = vertices.Count;
                for (int row = 0; row < 3; row++)
                    for (int side = 0; side < Sides; side++)
                        Vertex(Point(row==2?.78f:1f, row==0?.77f:.65f, row==0?.09f:.115f, row==0?.51f:.485f,side));
                for (int side = 0; side < Sides; side++)
                {
                    int a=start+side,b=start+(side+1)%Sides;
                    Quad(a,b,b+Sides,a+Sides,0);
                    Quad(a+Sides,b+Sides,b+Sides*2,a+Sides*2,0);
                }
            }

            void Tab()
            {
                // Thin rounded rectangular loop silhouette, following the upper shin skinning.
                int start = vertices.Count;
                for (int row = 0; row < 4; row++)
                    for (int side = 0; side < Sides; side++)
                        Vertex(Point(row==0?.85f:row==1?1.04f:row==2?1.085f:1.10f,
                            row==0?.20f:row==1?.22f:row==2?.19f:.14f, .07f, .13f, side));
                for(int row=0;row<3;row++) for(int side=0;side<Sides;side++)
                {
                    int a=start+row*Sides+side,d=start+row*Sides+(side+1)%Sides;
                    Quad(a,a+Sides,d+Sides,d,0);
                }
                Cap(start,0,false); Cap(start+3*Sides,0,true);
            }

            void Cap(int edge, int slot, bool top)
            {
                Vector3 center = Vector3.zero;
                for(int i=0;i<Sides;i++) center += vertices[edge+i];
                int c=Vertex(center/Sides);
                // Duplicate cap vertices retain the sole's crisp flat edge.
                int copy=vertices.Count;
                for(int i=0;i<Sides;i++) Vertex(vertices[edge+i]);
                for(int i=0;i<Sides;i++)
                {
                    int a=copy+i,b=copy+(i+1)%Sides;
                    triangles[slot].Add(c);triangles[slot].Add(top?b:a);triangles[slot].Add(top?a:b);
                }
            }

            void Quad(int a,int b,int c,int d,int slot)
            { triangles[slot].AddRange(new[]{a,b,c,a,c,d}); }

            internal Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name}; mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
                mesh.boneWeights=weights.ToArray();mesh.subMeshCount=triangles.Length;
                for(int sub=0;sub<triangles.Length;sub++) mesh.SetTriangles(triangles[sub],sub);
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
