#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Editor
{
    public static class NerokPresentationRegression
    {
        [MenuItem("DIMENSIONS FRONT/Validate/Nerok skin and anchor poses")]
        public static void Validate()
        {
            GameObject prefab = Resources.Load<GameObject>("AuthoredHeroes/Rook");
            if (prefab == null) throw new InvalidOperationException("Nerok's shared rig is missing.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            var baked = new Mesh();
            try
            {
                CyberSharkAppearance.Apply(instance.transform);
                Transform Find(string name) => Array.Find(instance.GetComponentsInChildren<Transform>(true), t => t.name == name);
                SkinnedMeshRenderer anatomy = Find("DF_Anatomy")?.GetComponent<SkinnedMeshRenderer>();
                SkinnedMeshRenderer anchor = Find("DF_GauntletR")?.GetComponent<SkinnedMeshRenderer>();
                Transform tail = Find("DF_Nerok_Tail");
                if (anatomy == null || anchor == null || tail == null || anatomy.sharedMesh == null || anchor.sharedMesh == null)
                    throw new InvalidOperationException("Nerok skin, anchor or animated tail did not load.");
                if (anchor.transform.parent != anatomy.transform || anchor.transform.localScale != Vector3.one || anchor.rootBone != anatomy.rootBone)
                    throw new InvalidOperationException("Anchor inherited a legacy gauntlet render transform.");
                Shader headShader = Resources.Load<Shader>("CyberShark/SharkSkin");
                if (headShader == null || ShaderUtil.ShaderHasError(headShader))
                    throw new InvalidOperationException("Nerok detailed head shader is missing or has compiler errors.");
                foreach (string materialName in new[] {
                    "DF_Nerok_DetailedSharkHead", "DF_Nerok_Natural shark mouth and gums",
                    "DF_Nerok_Ivory shark teeth", "DF_Nerok_Subtle bronze shark iris" })
                    if (!Array.Exists(anatomy.sharedMaterials, m => m != null && m.name == materialName))
                        throw new InvalidOperationException("Nerok head material missing: " + materialName);
                var restCoordinates = new List<Vector3>();
                anatomy.sharedMesh.GetUVs(2, restCoordinates);
                if (restCoordinates.Count != anatomy.sharedMesh.vertexCount)
                    throw new InvalidOperationException("Nerok skin rest coordinates are incomplete.");
                float unit = Vector3.Distance(Find("Chest").position, Find("Head").position);
                if (unit < .0001f) throw new InvalidOperationException("Invalid shared rig dimensions.");
                Transform hips = Find("Hips"), arm = Find("UpperArmR");
                Quaternion armRest = arm.localRotation, tailRest = tail.localRotation;
                foreach (float angle in new[] { -60f, 0f, 60f })
                {
                    arm.localRotation = armRest * Quaternion.Euler(angle, 0, angle * .4f);
                    foreach (float sweep in new[] { -45f, 0f, 45f })
                    {
                        tail.localRotation = Quaternion.AngleAxis(sweep, hips.InverseTransformDirection(instance.transform.up)) * tailRest;
                        foreach (var skin in new[] { anatomy, anchor })
                        {
                            skin.BakeMesh(baked);
                            foreach (Vector3 p in baked.vertices)
                            {
                                Vector3 world = skin.transform.TransformPoint(p);
                                if (float.IsNaN(world.x) || float.IsNaN(world.y) || float.IsNaN(world.z) ||
                                    Vector3.Distance(world, hips.position) > unit * 20f)
                                    throw new InvalidOperationException("Oversized/deformed Nerok mesh: " + skin.name);
                            }
                        }
                    }
                }
                Debug.Log("DF: Nerok detailed head shader/materials, skin coordinates, body/anchor skin and nine arm/tail poses validated.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
#endif

