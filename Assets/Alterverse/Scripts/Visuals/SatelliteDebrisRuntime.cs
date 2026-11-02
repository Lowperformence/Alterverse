using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Adds purely decorative satellite wreckage to the arena.
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
            if ((Application.isMobilePlatform || !Alterverse.Performance.MobilePerformanceProfile.AutoModeEnabled) &&
                Alterverse.Performance.MobilePerformanceProfile.Current == Alterverse.Performance.MobileQuality.Battery)
            {
                ClearDebris();
                currentGround = null;
                return;
            }

            ProductionLunarGround ground = Object.FindFirstObjectByType<ProductionLunarGround>();
            if (ground == currentGround && debrisRoot != null) return;

            ClearDebris();
            currentGround = ground;
            if (currentGround != null) BuildDebris(currentGround.transform);
        }

        void BuildDebris(Transform parent)
        {
            var clearance=new SceneryBuildingClearance();
            // Authored pockets stay away from bases, heal stations and the central objective.
            // On the Moon a few pieces are large enough to read as a crashed satellite from
            // the normal gameplay camera. They remain visual-only and use one combined mesh.
            Vector2[] pockets =
            {
                new(-31f, 22f), new(-25f, -23f), new(-15f, 27f), new(-10f, -24f),
                new(10f, 25f), new(16f, -27f), new(25f, 22f), new(31f, -20f),
                new(-35f, -12f), new(35f, 13f), new(-20f, 15f), new(21f, -14f)
            };

            bool moon = !ArenaVariantRuntime.IsMars && !ArenaVariantRuntime.IsGreenPlanet &&
                        !ArenaVariantRuntime.IsNoxara && !ArenaVariantRuntime.IsKryon;
            var vertices = new List<Vector3>(pockets.Length * 32);
            var triangles = new List<int>(pockets.Length * 144);

            for (int pocket = 0; pocket < pockets.Length; pocket++)
            {
                int pieces = moon && pocket % 4 == 0 ? 4 : (pocket % 3 == 0 ? 3 : 2);
                for (int piece = 0; piece < pieces; piece++)
                {
                    float phase = pocket * 1.731f + piece * 2.417f;
                    float spread = moon ? .46f : .28f;
                    Vector2 offset = new(Mathf.Sin(phase) * (spread + piece * .09f),
                                         Mathf.Cos(phase * 1.19f) * (spread * .8f + piece * .07f));
                    Vector2 site = pockets[pocket] + offset;
                    Vector3 surface = LunarSurfaceField.Surface(site.x, site.y);

                    int type = (pocket + piece) % 3;
                    float lunarScale = moon ? 1.45f : 1f;
                    Vector3 size = type == 0 ? new Vector3(.42f, .028f, .18f) * lunarScale :
                                   type == 1 ? new Vector3(.045f, .045f, .43f) * lunarScale :
                                               new Vector3(.22f, .075f, .16f) * lunarScale;
                    if (moon && piece == 3) size = new Vector3(.64f, .055f, .30f);

                    float yaw = pocket * 37f + piece * 113f;
                    float pitch = type == 1 ? 11f : (pocket % 2 == 0 ? 5f : -6f);
                    Quaternion rotation = Quaternion.Euler(pitch, yaw, type == 0 ? 8f : -4f);
                    Vector3 center = surface + Vector3.up * (size.y * .5f + .014f);
                    if(clearance.Blocks(SceneryBuildingClearance.TransformBounds(new Bounds(Vector3.zero,size),Matrix4x4.TRS(center,rotation,Vector3.one))))continue;
                    AddBox(vertices, triangles, center, size, rotation);
                }
            }

            debrisMesh = new Mesh { name = "DF_SatelliteDebrisMesh" };
            debrisMesh.SetVertices(vertices);
            debrisMesh.SetTriangles(triangles, 0);
            debrisMesh.RecalculateNormals();
            debrisMesh.RecalculateBounds();

            Color steel = new(.29f, .31f, .33f, 1f);
            if (ArenaVariantRuntime.IsMars) steel = Color.Lerp(steel, new Color(.35f, .17f, .10f, 1f), .42f);
            else if (ArenaVariantRuntime.IsGreenPlanet) steel = Color.Lerp(steel, new Color(.18f, .25f, .16f, 1f), .30f);
            else if (ArenaVariantRuntime.IsNoxara) steel = Color.Lerp(steel, new Color(.07f, .11f, .24f, 1f), .38f);
            else if (ArenaVariantRuntime.IsKryon) steel = Color.Lerp(steel, new Color(.48f, .60f, .66f, 1f), .36f);

            debrisMaterial = RuntimeShaderLibrary.Create(steel, false, Color.black, .72f, .18f);
            if (debrisMaterial == null)
            {
                Debug.LogError("DIMENSIONS FRONT: could not create Android-safe satellite debris material.");
                return;
            }
            debrisMaterial.name = "DF_SatelliteDebrisMaterial";

            debrisRoot = new GameObject("DF_SatelliteDebris");
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

