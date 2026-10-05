#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    public static class VeyrBladeVisibilityRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Zhaev blade geometry (Play mode)")]
        private static void Check()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run this check in Play mode.");
            GameObject near = null, far = null;
            try
            {
                near = Create(Vector3.zero);
                far = Create(Vector3.one * 12000f);
                var a = near.GetComponent<AuthoredHeroPresentation>().WeaponRenderers;
                var b = far.GetComponent<AuthoredHeroPresentation>().WeaponRenderers;
                if (a.Count != 2 || b.Count != 2) throw new InvalidOperationException("Zhaev must have two visible blades.");
                for (int side = 0; side < 2; side++)
                {
                    if (!(a[side] is MeshRenderer) || !(b[side] is MeshRenderer) || !a[side].enabled || !b[side].enabled)
                        throw new InvalidOperationException("Blade conversion failed or renderer disabled.");
                    Mesh x = a[side].GetComponent<MeshFilter>().sharedMesh;
                    Mesh y = b[side].GetComponent<MeshFilter>().sharedMesh;
                    if (x.vertexCount == 0 || x.vertexCount != y.vertexCount || x.subMeshCount != y.subMeshCount)
                        throw new InvalidOperationException("Blade geometry is missing.");
                    Vector3[] xv = x.vertices, yv = y.vertices;
                    for (int v = 0; v < xv.Length; v++)
                        if (!Finite(xv[v]) || !Finite(yv[v]) || Vector3.Distance(xv[v], yv[v]) > .00001f)
                            throw new InvalidOperationException("Menu world position changed blade-local geometry.");
                    if (x.bounds.size.magnitude < .1f || x.bounds.size.magnitude > 2f)
                        throw new InvalidOperationException("Blade geometry collapsed or is out of scale.");
                    int triangles = 0;
                    for (int sub = 0; sub < x.subMeshCount; sub++) triangles += x.GetTriangles(sub).Length;
                    if (triangles < 3 || a[side].sharedMaterials.Length < x.subMeshCount)
                        throw new InvalidOperationException("Blade triangles or materials missing.");
                }
                Debug.Log("DF: Zhaev blade geometry passed at origin and portrait stage (12000,12000,12000).");
            }
            finally
            {
                if (near != null) UnityEngine.Object.Destroy(near);
                if (far != null) UnityEngine.Object.Destroy(far);
            }
        }
        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private static GameObject Create(Vector3 position)
        {
            var actor = new GameObject("DF_ZhaevBladeRegression");
            actor.transform.position = position;
            actor.AddComponent<HeroIdentity>().Configure(HeroId.Veyr);
            if (!AuthoredHeroPresentation.TryCreate(actor, HeroId.Veyr, out _))
            { UnityEngine.Object.Destroy(actor); throw new InvalidOperationException("Zhaev asset missing."); }
            return actor;
        }
    }
}
#endif
