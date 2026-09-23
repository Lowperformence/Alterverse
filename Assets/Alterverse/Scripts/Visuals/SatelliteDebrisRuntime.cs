using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Adds tiny, purely decorative satellite wreckage to every arena variant.
    /// The debris is rendered as one lightweight mesh and deliberately has no
    /// Collider, Rigidbody or NavMeshObstacle, so gameplay movement/pathing is untouched.
    /// </summary>
    public sealed class SatelliteDebrisRuntime : MonoBehaviour
    {
        ProductionLunarGround currentGround;
        GameObject debrisRoot;
        Mesh debrisMesh;
        Material debrisMaterial;
        int probeFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Object.FindFirstObjectByType<SatelliteDebrisRuntime>() != null) return;
            GameObject host = new("DF_SatelliteDebrisRuntime");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<SatelliteDebrisRuntime>();
        }

        void Update()
        {
            // The arena is assembled at runtime, so probe cheaply and rebuild only when
            // a different ProductionLunarGround instance becomes active.
            if ((probeFrame++ % 30) != 0) return;

            ProductionLunarGround ground = Object.FindFirstObjectByType<ProductionLunarGround>();
            if (ground == currentGround && debrisRoot != null) return;

            ClearDebris();
            currentGround = ground;
            if (currentGround != null) BuildDebris(currentGround.transform);
        }

        void BuildDebris(Transform parent)
        {
            // Small authored pockets stay away from bases, heal stations and the central objective.
            // Each pocket contains only 2-3 fragments so the result reads as old orbital wreckage,
            // not as a second gameplay obstacle layer.
            Vector2[] pockets =
            {
                new(-31f, 22f), new(-25f, -23f), new(-15f, 27f), new(-10f, -24f),
                new(10f, 25f), new(16f, -27f), new(25f, 22f), new(31f, -20f),
                new(-35f, -12f), new(35f, 13f), new(-20f, 15f), new(21f, -14f)
            };

            var vertices = new List<Vector3>(pockets.Length * 24);
            var triangles = new List<int>(pockets.Length * 108);

            for (int pocket = 0; pocket < pockets.Length; pocket++)
            {
                int pieces = pocket % 3 == 0 ? 3 : 2;
                for (int piece = 0; piece < pieces; piece++)
                {
                    float phase = pocket * 1.731f + piece * 2.417f;
                    Vector2 offset = new(Mathf.Sin(phase) * (.22f + piece * .07f),
                                         Mathf.Cos(phase * 1.19f) * (.18f + piece * .06f));
                    Vector2 site = pockets[pocket] + offset;
                    Vector3 surface = LunarSurfaceField.Surface(site.x, site.y);

                    // Three tiny silhouettes: broken solar-panel plate, antenna strut and housing shard.
                    int type = (pocket + piece) % 3;
                    Vector3 size = type == 0 ? new Vector3(.28f, .025f, .12f) :
                                   type == 1 ? new Vector3(.035f, .035f, .31f) :
                                               new Vector3(.15f, .055f, .11f);
                    float yaw = pocket * 37f + piece * 113f;
                    float pitch = type == 1 ? 8f : (pocket % 2 == 0 ? 3f : -4f);
                    Quaternion rotation = Quaternion.Euler(pitch, yaw, type == 0 ? 6f : -3f);
                    Vector3 center = surface + Vector3.up * (size.y * .5f + .012f);
                    AddBox(vertices, triangles, center, size, rotation);
                }
            }

            debrisMesh = new Mesh { name = "DF_MicroSatelliteDebrisMesh" };
            debrisMesh.SetVertices(vertices);
            debrisMesh.SetTriangles(triangles, 0);
            debrisMesh.RecalculateNormals();
            debrisMesh.RecalculateBounds();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Color steel = new(.29f, .31f, .33f, 1f);
            if (ArenaVariantRuntime.IsMars) steel = Color.Lerp(steel, new Color(.35f, .17f, .10f, 1f), .42f);
            else if (ArenaVariantRuntime.IsGreenPlanet) steel = Color.Lerp(steel, new Color(.18f, .25f, .16f, 1f), .30f);
            else if (ArenaVariantRuntime.IsNoxara) steel = Color.Lerp(steel, new Color(.07f, .11f, .24f, 1f), .38f);
            else if (ArenaVariantRuntime.IsKryon) steel = Color.Lerp(steel, new Color(.48f, .60f, .66f, 1f), .36f);

            debrisMaterial = new Material(shader) { name = "DF_MicroSatelliteDebrisMaterial", color = steel };
            if (debrisMaterial.HasProperty("_BaseColor")) debrisMaterial.SetColor("_BaseColor", steel);
            if (debrisMaterial.HasProperty("_Metallic")) debrisMaterial.SetFloat("_Metallic", .72f);
            if (debrisMaterial.HasProperty("_Smoothness")) debrisMaterial.SetFloat("_Smoothness", .18f);

            debrisRoot = new GameObject("DF_MicroSatelliteDebris");
            debrisRoot.transform.SetParent(parent, false);
            debrisRoot.isStatic = true;
            debrisRoot.AddComponent<MeshFilter>().sharedMesh = debrisMesh;
            MeshRenderer renderer = debrisRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = debrisMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            // Intentionally no Collider, Rigidbody or NavMeshObstacle components.
        }

        static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size, Quaternion rotation)
        {
            int start = vertices.Count;
            Vector3 h = size * .5f;
            Vector3[] local =
            {
                new(-h.x,-h.y,-h.z), new(h.x,-h.y,-h.z), new(h.x,-h.y,h.z), new(-h.x,-h.y,h.z),
                new(-h.x,h.y,-h.z),  new(h.x,h.y,-h.z),  new(h.x,h.y,h.z),  new(-h.x,h.y,h.z)
            };
            for (int i = 0; i < local.Length; i++) vertices.Add(center + rotation * local[i]);

            int[] faces =
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                1,2,6, 1,6,5,
                2,3,7, 2,7,6,
                3,0,4, 3,4,7
            };
            for (int i = 0; i < faces.Length; i++) triangles.Add(start + faces[i]);
        }

        void ClearDebris()
        {
            if (debrisRoot != null) Destroy(debrisRoot);
            if (debrisMesh != null) Destroy(debrisMesh);
            if (debrisMaterial != null) Destroy(debrisMaterial);
            debrisRoot = null;
            debrisMesh = null;
            debrisMaterial = null;
        }

        void OnDestroy() => ClearDebris();
    }
}
