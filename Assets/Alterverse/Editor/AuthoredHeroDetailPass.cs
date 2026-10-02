#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.EditorTools
{
    // Runs on fresh FBX data, never on a previously modified runtime mesh.
    internal static class AuthoredHeroDetailPass
    {
        internal static void Apply(GameObject root, string hero)
        {
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (hero == "Traveler" && skin.name == "DF_WeaponR")
                {
                    RemoveOrbHandle(skin);
                    CenterOrbMuzzle(root, skin);
                }
                if (skin.name != "DF_Anatomy") continue;
                RefineSilhouette(skin, hero);
                AddArticulatedDetails(root, skin, hero);
            }
        }

        private static int Bone(SkinnedMeshRenderer skin, string name)
            => Array.FindIndex(skin.bones, b => b != null && b.name == name);

        private static float Weight(BoneWeight w, int bone)
            => (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0)
             + (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);

        private static Vector3 Origin(Mesh mesh, int bone)
            => mesh.bindposes[bone].inverse.MultiplyPoint3x4(Vector3.zero);

        private static void RefineSilhouette(SkinnedMeshRenderer skin, string hero)
        {
            Mesh mesh = skin.sharedMesh;
            if (mesh == null || mesh.boneWeights.Length != mesh.vertexCount) return;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            // Preserve the user's skeleton, lengths, hand grips and authored head proportions.
            // Lightly balance limb cross-sections; heavy classes retain their heavier silhouette.
            bool heavy = hero == "Rook" || hero == "Kaelon";
            float thigh = heavy ? 1.04f : hero == "Veyr" || hero == "Traveler" ? 1.025f : 1.01f;
            float forearm = heavy ? .97f : 1.015f;
            foreach (string side in new[] { "L", "R" })
            {
                Adjust("Thigh" + side, "Shin" + side, thigh);
                Adjust("Forearm" + side, "Hand" + side, forearm);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            void Adjust(string name, string child, float factor)
            {
                int bone = Bone(skin, name), tip = Bone(skin, child);
                if (bone < 0 || tip < 0) return;
                Vector3 start = Origin(mesh, bone), axis = Origin(mesh, tip) - start;
                float length2 = axis.sqrMagnitude;
                if (length2 < 1e-10f) return;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float weight = Weight(weights[i], bone);
                    if (weight <= 0) continue;
                    float t = Vector3.Dot(vertices[i] - start, axis) / length2;
                    if (t <= 0 || t >= 1) continue;
                    Vector3 radial = vertices[i] - (start + axis * t);
                    vertices[i] += radial * ((factor - 1) * Mathf.Sin(t * Mathf.PI) * weight);
                }
            }
        }

        private static void AddArticulatedDetails(GameObject root, SkinnedMeshRenderer skin, string hero)
        {
            Mesh source = skin.sharedMesh;
            Vector3[] body = source.vertices;
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length != body.Length) return;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var skinning = new List<BoneWeight>();
            bool heavy = hero == "Kaelon" || hero == "Rook";
            foreach (string side in new[] { "L", "R" })
            {
                AddPlates("Forearm" + side, "Hand" + side, heavy ? 3 : 2, false);
                AddPlates("Hand" + side, null, 4, true);
                AddPlates("Shin" + side, "Foot" + side, 2, false);
            }
            if (vertices.Count == 0) return;
            var detail = new Mesh { name = "DF_ArticulatedArmorDetails_" + hero };
            detail.SetVertices(vertices);
            detail.SetTriangles(triangles, 0);
            detail.boneWeights = skinning.ToArray();
            detail.bindposes = source.bindposes;
            detail.RecalculateNormals();
            detail.RecalculateBounds();
            var go = new GameObject("DF_ArticulatedArmorDetails");
            go.transform.SetParent(skin.transform, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = detail;
            renderer.bones = skin.bones;
            renderer.rootBone = skin.rootBone;
            renderer.localBounds = skin.localBounds;
            renderer.quality = skin.quality;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            var material = new Material(shader) { name = "DF_MachinedDetail_" + hero };
            Color color = hero == "Mira" ? new Color(.48f,.39f,.20f)
                : hero == "Rook" ? new Color(.32f,.25f,.21f)
                : hero == "Veyr" ? new Color(.24f,.22f,.31f)
                : new Color(.32f,.38f,.43f);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", .72f);
            material.SetFloat("_Smoothness", .48f);
            renderer.sharedMaterial = material;

            void AddPlates(string name, string tipName, int count, bool knuckles)
            {
                int bone = Bone(skin, name);
                if (bone < 0) return;
                int tip = tipName == null ? -1 : Bone(skin, tipName);
                Matrix4x4 rest = source.bindposes[bone].inverse;
                Vector3 origin = Origin(source, bone);
                Vector3 axis = tip >= 0 ? Origin(source, tip) - origin : rest.MultiplyVector(Vector3.up);
                if (axis.sqrMagnitude < 1e-10f) return;
                axis.Normalize();
                Vector3 forward = skin.transform.InverseTransformDirection(root.transform.forward);
                Vector3 normal = Vector3.ProjectOnPlane(forward, axis).normalized;
                if (normal.sqrMagnitude < .1f) return;
                Vector3 across = Vector3.Cross(axis, normal).normalized;
                var points = new List<Vector3>();
                float low = float.MaxValue, high = float.MinValue;
                for (int i = 0; i < body.Length; i++)
                {
                    if (Weight(weights[i], bone) < .7f) continue;
                    Vector3 p = body[i] - origin;
                    Vector3 q = new Vector3(Vector3.Dot(p, across), Vector3.Dot(p, axis), Vector3.Dot(p, normal));
                    points.Add(q); low = Mathf.Min(low, q.y); high = Mathf.Max(high, q.y);
                }
                if (points.Count < 12 || high - low < 1e-6f) return;
                float span = high - low;
                for (int part = 0; part < count; part++)
                {
                    float along = knuckles ? Mathf.Lerp(low, high, .52f) : Mathf.Lerp(low, high, (part + 1f) / (count + 1f));
                    float halfLength = span * (knuckles ? .18f : .095f);
                    float left = float.MaxValue, right = float.MinValue, front = float.MinValue;
                    foreach (Vector3 p in points)
                    {
                        if (Mathf.Abs(p.y - along) > span * .14f) continue;
                        left = Mathf.Min(left, p.x); right = Mathf.Max(right, p.x); front = Mathf.Max(front, p.z);
                    }
                    if (left == float.MaxValue || right - left < 1e-6f) continue;
                    float width = right - left;
                    float x = knuckles ? Mathf.Lerp(left, right, (part + 1f) / (count + 1f)) : (left + right) * .5f;
                    float halfWidth = width * (knuckles ? .085f : heavy ? .36f : .29f);
                    float thickness = span * .018f;
                    Vector3 center = origin + across * x + axis * along + normal * (front + thickness * .3f);
                    ChamferPlate(center, across * halfWidth, axis * halfLength, normal * thickness, bone);
                }
            }

            void ChamferPlate(Vector3 center, Vector3 x, Vector3 y, Vector3 depth, int bone)
            {
                // Eight bevelled corners, small inset top and closed side faces.
                var outline = new[] { new Vector2(-.72f,-1),new Vector2(.72f,-1),new Vector2(1,-.72f),
                    new Vector2(1,.72f),new Vector2(.72f,1),new Vector2(-.72f,1),new Vector2(-1,.72f),new Vector2(-1,-.72f) };
                int first = vertices.Count;
                for (int level = 0; level < 2; level++)
                    foreach (Vector2 p in outline)
                    {
                        vertices.Add(center + (x * p.x + y * p.y) * (level == 0 ? 1 : .82f) + depth * level);
                        skinning.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                    }
                for (int i = 1; i < 7; i++) Face(first + 8, first + 8 + i, first + 9 + i);
                for (int i = 0; i < 8; i++)
                {
                    int j = (i + 1) % 8;
                    Face(first + i, first + j, first + 8 + j);
                    Face(first + i, first + 8 + j, first + 8 + i);
                }
            }
            void Face(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
        }

        private static void CenterOrbMuzzle(GameObject root, SkinnedMeshRenderer skin)
        {
            Mesh mesh=skin.sharedMesh;
            int grip=Bone(skin,"WeaponGripR");
            if(mesh==null||grip<0)return;
            Transform muzzle=null;
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(t.name=="DF_Muzzle") { muzzle=t;break; }
            if(muzzle==null)return;
            Material[] materials=skin.sharedMaterials;
            Vector3 center=Vector3.zero;int count=0;
            Vector3[] points=mesh.vertices;
            for(int sub=0;sub<Mathf.Min(mesh.subMeshCount,materials.Length);sub++)
            {
                if(materials[sub]==null||!materials[sub].name.ToLowerInvariant().Contains("chronolight"))continue;
                var unique=new HashSet<int>(mesh.GetTriangles(sub));
                foreach(int index in unique) { center+=points[index];count++; }
            }
            if(count==0)return;
            muzzle.SetParent(skin.bones[grip],false);
            muzzle.localPosition=mesh.bindposes[grip].MultiplyPoint3x4(center/count);
        }

        internal static void RemoveOrbHandle(SkinnedMeshRenderer skin)
        {
            Mesh mesh = skin.sharedMesh;
            if (mesh == null) return;
            Vector3[] vertices = mesh.vertices;
            var parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            void Join(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
            // Weld duplicated UV/normal seam vertices for component classification only.
            float epsilon = Mathf.Max(mesh.bounds.size.magnitude * 1e-6f, 1e-8f);
            var welded = new Dictionary<Vector3Int,int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i] / epsilon;
                var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (welded.TryGetValue(key, out int other)) Join(i, other); else welded.Add(key, i);
            }
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] indices = mesh.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3) { Join(indices[i],indices[i+1]); Join(indices[i],indices[i+2]); }
            }
            var groups = new Dictionary<int,List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            { int key=Find(i); if(!groups.TryGetValue(key,out var list))groups.Add(key,list=new List<int>());list.Add(i); }
            var remove = new HashSet<int>();
            foreach (var group in groups)
            {
                // The known handle is the only isolated straight tube. The orb and its
                // three closed orbit rings have two similarly large principal extents.
                // Principal-axis measurement is independent of the FBX rest rotation.
                var ids=group.Value;
                if(ids.Count<12)continue;
                Vector3 center=Vector3.zero;
                foreach(int i in ids)center+=vertices[i];
                center/=ids.Count;
                // Principal axis, not a diagonal through the cylinder caps.
                Vector3 axis=Vector3.one.normalized;
                for(int iteration=0;iteration<24;iteration++)
                {
                    Vector3 next=Vector3.zero;
                    foreach(int i in ids)
                    { Vector3 p=vertices[i]-center;next+=p*Vector3.Dot(p,axis); }
                    if(next.sqrMagnitude<1e-20f)break;
                    axis=next.normalized;
                }
                float low=float.MaxValue,high=float.MinValue,radial=0;
                foreach(int i in ids)
                {
                    Vector3 p=vertices[i]-center;float t=Vector3.Dot(p,axis);
                    low=Mathf.Min(low,t);high=Mathf.Max(high,t);
                    radial=Mathf.Max(radial,Vector3.ProjectOnPlane(p,axis).magnitude);
                }
                float length=high-low;
                if(length>radial*7f)remove.Add(group.Key);
            }
            if(remove.Count==0)return; // Already rebuilt without a handle.
            if(remove.Count!=1) { Debug.LogError("DF: Traveler handle topology is ambiguous; no geometry removed."); return; }
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                int[] indices=mesh.GetTriangles(sub);var kept=new List<int>();
                for(int i=0;i<indices.Length;i+=3)
                    if(!remove.Contains(Find(indices[i]))) { kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]); }
                mesh.SetTriangles(kept,sub,false);
            }
            mesh.RecalculateBounds();
        }
    }
}
#endif
