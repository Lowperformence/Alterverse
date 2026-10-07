using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Collider-free, low-poly scenery derived from Quaternius' Ultimate Space Kit (CC0 1.0).
    /// Large silhouettes stay outside the usable Hot Zone rectangle, so this layer is presentation only.
    /// </summary>
    public sealed partial class SpaceKitMapPolishRuntime : MonoBehaviour
    {
        private const string BootstrapName = "DF_SpaceKitMapPolishBootstrap";
        private const string RootName = "DF_SpaceKitMapPolish";

        private readonly List<Mesh> runtimeMeshes = new();
        private readonly List<Material> runtimeMaterials = new();

        private Mesh rockMesh;
        private Mesh domeMesh;
        private Mesh plantMesh;
        private Mesh spiralTreeMesh;
        private Mesh radarMesh;
        private Mesh antennaMesh;
        private Mesh solarPanelMesh;

        private Material rockMaterial;
        private Material structureMaterial;
        private Material floraMaterial;
        private Material accentMaterial;
        private Material darkMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameObject.Find(BootstrapName) != null || GameObject.Find(RootName) != null)
                return;

            GameObject host = new(BootstrapName);
            host.AddComponent<SpaceKitMapPolishRuntime>();
        }

        private IEnumerator Start()
        {
            ProductionLunarGround ground = null;
            for (int frame = 0; frame < 360 && ground == null; frame++)
            {
                ground = FindFirstObjectByType<ProductionLunarGround>();
                if (ground == null)
                    yield return null;
            }

            if (ground == null)
            {
                Destroy(gameObject);
                yield break;
            }

            // Allow the arena's own Start() pass to finish before the decorative layer is attached.
            yield return null;

            Transform existing = ground.transform.Find(RootName);
            if (existing != null)
            {
                Destroy(gameObject);
                yield break;
            }

            transform.SetParent(ground.transform, true);
            gameObject.name = RootName;
            BuildScenery();
        }

        private SceneryBuildingClearance clearance;

        private void BuildScenery()
        {
            clearance=new SceneryBuildingClearance();
            rockMesh = DecodeOwnedMesh("USK_Rock_Large_1", RockLarge1Data);
            domeMesh = DecodeOwnedMesh("USK_GeodesicDome", GeodesicDomeData);
            plantMesh = DecodeOwnedMesh("USK_Plant_2", Plant2Data);
            spiralTreeMesh = DecodeOwnedMesh("USK_Tree_Spiral_1", TreeSpiral1Data);
            radarMesh = DecodeOwnedMesh("USK_Roof_Radar", RadarData);
            antennaMesh = DecodeOwnedMesh("USK_Roof_Antenna", AntennaData);
            solarPanelMesh = DecodeOwnedMesh("USK_SolarPanel_Ground", SolarPanelData);

            BuildPalette();
            BuildOuterRockField();

            if (ArenaVariantRuntime.IsGreenPlanet)
                BuildGreenPlanetSet();
            else if (ArenaVariantRuntime.IsNoxara)
                BuildNoxaraSet();
            else if (ArenaVariantRuntime.IsMars)
                BuildMarsSet();
            else if (ArenaVariantRuntime.IsKryon)
                BuildKryonSet();
            else
                BuildMoonSet();
        }

        private void BuildPalette()
        {
            Color rock;
            Color structure;
            Color flora;
            Color accent;
            Color dark;

            if (ArenaVariantRuntime.IsMars)
            {
                rock = new Color(.43f, .19f, .10f, 1f);
                structure = new Color(.28f, .24f, .22f, 1f);
                flora = new Color(.34f, .20f, .085f, 1f);
                accent = new Color(.82f, .38f, .14f, 1f);
                dark = new Color(.12f, .065f, .045f, 1f);
            }
            else if (ArenaVariantRuntime.IsGreenPlanet)
            {
                rock = new Color(.18f, .25f, .16f, 1f);
                structure = new Color(.25f, .34f, .31f, 1f);
                flora = new Color(.23f, .60f, .21f, 1f);
                accent = new Color(.55f, .92f, .33f, 1f);
                dark = new Color(.055f, .095f, .065f, 1f);
            }
            else if (ArenaVariantRuntime.IsNoxara)
            {
                rock = new Color(.075f, .09f, .17f, 1f);
                structure = new Color(.14f, .12f, .25f, 1f);
                flora = new Color(.25f, .12f, .39f, 1f);
                accent = new Color(.58f, .31f, .92f, 1f);
                dark = new Color(.025f, .03f, .075f, 1f);
            }
            else if (ArenaVariantRuntime.IsKryon)
            {
                rock = new Color(.40f, .54f, .60f, 1f);
                structure = new Color(.54f, .68f, .73f, 1f);
                flora = new Color(.29f, .62f, .69f, 1f);
                accent = new Color(.35f, .88f, .98f, 1f);
                dark = new Color(.13f, .21f, .25f, 1f);
            }
            else
            {
                rock = new Color(.27f, .30f, .35f, 1f);
                structure = new Color(.39f, .45f, .52f, 1f);
                flora = new Color(.22f, .29f, .34f, 1f);
                accent = new Color(.38f, .68f, .88f, 1f);
                dark = new Color(.075f, .09f, .115f, 1f);
            }

            rockMaterial = CreateMaterial("USK_Rock", rock, .06f, .24f);
            structureMaterial = CreateMaterial("USK_Structure", structure, .48f, .43f);
            floraMaterial = CreateMaterial("USK_Flora", flora, .02f, .22f);
            accentMaterial = CreateMaterial("USK_Accent", accent, .18f, .52f);
            darkMaterial = CreateMaterial("USK_Dark", dark, .12f, .20f);
            var planet=Alterverse.Match.ArenaVariantRuntime.Current;
            PlanetSceneryMaterials.Apply(rockMaterial,planet,SceneryFinish.Stone);
            PlanetSceneryMaterials.Apply(structureMaterial,planet,SceneryFinish.Metal);
            PlanetSceneryMaterials.Apply(darkMaterial,planet,SceneryFinish.Metal);
            PlanetSceneryMaterials.Apply(accentMaterial,planet,
                planet==Alterverse.Match.ArenaVariant.Kryon?SceneryFinish.Ice:SceneryFinish.Metal);
            PlanetSceneryMaterials.Apply(floraMaterial,planet,
                planet==Alterverse.Match.ArenaVariant.Kryon?SceneryFinish.Ice:
                planet==Alterverse.Match.ArenaVariant.Mars?SceneryFinish.Stone:SceneryFinish.Leaf);
        }

        private void BuildOuterRockField()
        {
            Vector2[] sites =
            {
                new(-40.2f, 30.8f), new(-40.4f, -28.7f), new(-35.0f, 33.1f), new(-35.8f, -31.7f),
                new(-27.8f, 34.0f), new(-28.6f, -33.8f), new(40.1f, -30.7f), new(40.4f, 28.8f),
                new(35.1f, -33.0f), new(35.8f, 31.8f), new(27.9f, -34.0f), new(28.7f, 33.8f),
                new(-41.0f, 13.0f), new(-41.0f, -12.0f), new(41.0f, 12.0f), new(41.0f, -13.0f)
            };

            for (int i = 0; i < sites.Length; i++)
            {
                float s = .31f + (i % 4) * .075f;
                Spawn("USK_OuterRock", rockMesh, i % 5 == 0 ? darkMaterial : rockMaterial, sites[i],
                    new Vector3(s * (1f + (i % 3) * .12f), s * (.72f + (i % 2) * .13f), s),
                    i * 47f + 11f, -.03f, true, i % 2 == 0 ? 7f : -5f, i % 3 == 0 ? 4f : -3f);
            }
        }

        private void BuildMoonSet()
        {
            Spawn("USK_MoonResearchDome_A", domeMesh, structureMaterial, new Vector2(-37.0f, 28.7f),
                new Vector3(.72f, .66f, .72f), 24f, -.03f, true);
            Spawn("USK_MoonResearchDome_B", domeMesh, structureMaterial, new Vector2(37.0f, -28.8f),
                new Vector3(.64f, .59f, .64f), 205f, -.03f, true);

            Spawn("USK_MoonRadar", radarMesh, structureMaterial, new Vector2(-31.5f, 31.6f),
                new Vector3(.72f, .72f, .72f), 32f, -.02f, true);
            Spawn("USK_MoonAntenna", antennaMesh, accentMaterial, new Vector2(32.2f, 31.3f),
                new Vector3(.58f, .58f, .58f), 212f, -.02f, true);
            Spawn("USK_MoonSolar_A", solarPanelMesh, structureMaterial, new Vector2(-32.5f, -32.1f),
                new Vector3(.78f, .78f, .78f), 28f, -.02f, true);
            Spawn("USK_MoonSolar_B", solarPanelMesh, structureMaterial, new Vector2(32.8f, -31.8f),
                new Vector3(.76f, .76f, .76f), 204f, -.02f, true);

            BuildPlantCluster(new Vector2(-35.7f, -29.2f), 6, .56f, accentMaterial, 14);
            BuildPlantCluster(new Vector2(35.8f, 29.0f), 6, .52f, accentMaterial, 41);
            BuildPlantCluster(new Vector2(-40.0f, 3.0f), 4, .43f, darkMaterial, 71);
            BuildPlantCluster(new Vector2(40.0f, -3.5f), 4, .43f, darkMaterial, 96);
        }

        private void BuildMarsSet()
        {
            Spawn("USK_MarsAbandonedDome", domeMesh, darkMaterial, new Vector2(-37.4f, -29.0f),
                new Vector3(.66f, .54f, .72f), 31f, -.05f, true, 4f, -3f);
            Spawn("USK_MarsRadarWreck", radarMesh, darkMaterial, new Vector2(-34.2f, 30.5f),
                new Vector3(.60f, .60f, .60f), 17f, -.05f, true, 11f, -7f);
            Spawn("USK_MarsSolarWreck", solarPanelMesh, structureMaterial, new Vector2(34.0f, -32.2f),
                new Vector3(.70f, .70f, .70f), 197f, -.04f, true, -9f, 5f);
            Spawn("USK_MarsAntenna", antennaMesh, structureMaterial, new Vector2(-40.0f, 6.0f),
                new Vector3(.50f, .50f, .50f), 62f, -.04f, true, 6f, 4f);

            Vector2[] spires =
            {
                new(-34.5f, 31.5f), new(-26.5f, 33.0f), new(36.0f, 30.5f), new(29.0f, -33.0f),
                new(-39.0f, 16.5f), new(39.2f, -17.0f)
            };
            for (int i = 0; i < spires.Length; i++)
            {
                float s = .58f + (i % 3) * .11f;
                Spawn("USK_MarsWeatheredSpire", spiralTreeMesh, floraMaterial, spires[i],
                    new Vector3(s * .78f, s * (1.05f + (i % 2) * .12f), s * .78f), i * 61f, -.04f, true,
                    i % 2 == 0 ? 5f : -4f, 0f);
            }

            BuildPlantCluster(new Vector2(-39.0f, -7.0f), 5, .52f, accentMaterial, 23);
            BuildPlantCluster(new Vector2(39.0f, 8.0f), 5, .48f, accentMaterial, 58);
        }

        private void BuildGreenPlanetSet()
        {
            Vector2[] trees =
            {
                new(-38.8f, 27.8f), new(-34.3f, 32.2f), new(-26.3f, 32.8f), new(-39.5f, -20.0f),
                new(-34.0f, -31.0f), new(-25.8f, -32.8f), new(38.7f, -27.8f), new(34.2f, -32.0f),
                new(26.2f, -32.8f), new(39.4f, 20.0f), new(34.0f, 31.0f), new(25.8f, 32.8f)
            };

            for (int i = 0; i < trees.Length; i++)
            {
                float s = .76f + (i % 4) * .09f;
                Spawn("USK_XenoSpiralTree", spiralTreeMesh, i % 5 == 0 ? accentMaterial : floraMaterial, trees[i],
                    new Vector3(s, s * (1.04f + (i % 2) * .08f), s), i * 37f, -.02f, true,
                    i % 3 == 0 ? 3f : 0f, i % 4 == 0 ? -3f : 0f);
            }

            BuildPlantCluster(new Vector2(-37.0f, 8.5f), 8, .68f, floraMaterial, 17);
            BuildPlantCluster(new Vector2(-33.5f, -28.5f), 8, .62f, accentMaterial, 37);
            BuildPlantCluster(new Vector2(37.2f, -8.0f), 8, .68f, floraMaterial, 59);
            BuildPlantCluster(new Vector2(33.4f, 28.2f), 8, .62f, accentMaterial, 83);

            Spawn("USK_GreenResearchDome", domeMesh, structureMaterial, new Vector2(-37.0f, -27.8f),
                new Vector3(.55f, .50f, .55f), 18f, -.03f, true);
            Spawn("USK_GreenAntenna", antennaMesh, accentMaterial, new Vector2(34.0f, -31.0f),
                new Vector3(.45f, .45f, .45f), 193f, -.02f, true);
        }

        private void BuildNoxaraSet()
        {
            Vector2[] trees =
            {
                new(-39.0f, 24.0f), new(-33.5f, 31.7f), new(-27.0f, 33.1f), new(-39.2f, -18.0f),
                new(-31.0f, -32.2f), new(39.0f, -24.0f), new(33.5f, -31.7f), new(27.0f, -33.1f),
                new(39.2f, 18.0f), new(31.0f, 32.2f)
            };

            for (int i = 0; i < trees.Length; i++)
            {
                float s = .72f + (i % 3) * .10f;
                Spawn("USK_NoxaraSpiralGrowth", spiralTreeMesh, i % 3 == 0 ? accentMaterial : floraMaterial, trees[i],
                    new Vector3(s * .86f, s * 1.16f, s * .86f), i * 49f + 15f, -.03f, true,
                    i % 2 == 0 ? 6f : -5f, i % 4 == 0 ? 4f : 0f);
            }

            BuildPlantCluster(new Vector2(-40.0f, 5.0f), 9, .61f, floraMaterial, 29);
            BuildPlantCluster(new Vector2(-34.0f, -29.0f), 7, .70f, accentMaterial, 53);
            BuildPlantCluster(new Vector2(40.0f, -5.0f), 9, .61f, floraMaterial, 79);
            BuildPlantCluster(new Vector2(34.0f, 29.0f), 7, .70f, accentMaterial, 103);

            Spawn("USK_NoxaraRuinedDome", domeMesh, darkMaterial, new Vector2(37.4f, 27.7f),
                new Vector3(.52f, .44f, .58f), 211f, -.06f, true, -5f, 4f);
            Spawn("USK_NoxaraRadarWreck", radarMesh, darkMaterial, new Vector2(-35.0f, -30.8f),
                new Vector3(.52f, .52f, .52f), 48f, -.05f, true, 13f, 8f);
        }

        private void BuildKryonSet()
        {
            Spawn("USK_KryonDome_A", domeMesh, structureMaterial, new Vector2(-37.2f, 28.8f),
                new Vector3(.67f, .61f, .67f), 22f, -.04f, true);
            Spawn("USK_KryonDome_B", domeMesh, structureMaterial, new Vector2(37.2f, -28.8f),
                new Vector3(.60f, .55f, .60f), 202f, -.04f, true);
            Spawn("USK_KryonRadar", radarMesh, accentMaterial, new Vector2(-32.0f, 31.5f),
                new Vector3(.56f, .56f, .56f), 29f, -.02f, true);
            Spawn("USK_KryonSolar", solarPanelMesh, structureMaterial, new Vector2(32.0f, -31.7f),
                new Vector3(.64f, .64f, .64f), 208f, -.02f, true);
            Spawn("USK_KryonAntenna", antennaMesh, accentMaterial, new Vector2(40.0f, 1.0f),
                new Vector3(.46f, .46f, .46f), 185f, -.02f, true);

            BuildPlantCluster(new Vector2(-40.0f, -5.0f), 8, .58f, accentMaterial, 31);
            BuildPlantCluster(new Vector2(-31.5f, -31.0f), 7, .54f, floraMaterial, 57);
            BuildPlantCluster(new Vector2(40.0f, 5.0f), 8, .58f, accentMaterial, 81);
            BuildPlantCluster(new Vector2(31.5f, 31.0f), 7, .54f, floraMaterial, 109);
        }

        private void BuildPlantCluster(Vector2 center, int count, float scale, Material material, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (seed * 19f + i * 137.5f) * Mathf.Deg2Rad;
                float radius = .55f + (i % 4) * .43f;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float s = scale * (.82f + (i % 3) * .11f);
                Spawn("USK_AlienPlant", plantMesh, material, p, new Vector3(s, s * (1f + (i % 2) * .13f), s),
                    seed * 11f + i * 73f, -.01f, false, i % 3 == 0 ? 4f : 0f, 0f);
            }
        }

        private GameObject Spawn(string objectName, Mesh mesh, Material material, Vector2 site, Vector3 scale,
            float yaw, float verticalOffset, bool castShadow, float tiltX = 0f, float tiltZ = 0f)
        {
            GameObject go = new(objectName);
            go.transform.SetParent(transform, false);
            Vector3 world = LunarSurfaceField.Surface(site.x, site.y);
            world.y += verticalOffset;
            go.transform.position = world;
            go.transform.rotation = Quaternion.Euler(tiltX, yaw, tiltZ);
            go.transform.localScale = scale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            clearance.RemoveIfBlocked(go);
            return go;
        }

        private Material CreateMaterial(string materialName, Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Universal Render Pipeline/Simple Lit") ??
                            Shader.Find("Standard");
            Material material = new(shader) { name = materialName, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            runtimeMaterials.Add(material);
            return material;
        }

#if UNITY_EDITOR
        public static void ValidateEmbeddedGeometry()
        {
            string[] names={"USK_Rock_Large_1","USK_GeodesicDome","USK_Plant_2","USK_Tree_Spiral_1",
                "USK_Roof_Radar","USK_Roof_Antenna","USK_SolarPanel_Ground"};
            string[] data={RockLarge1Data,GeodesicDomeData,Plant2Data,TreeSpiral1Data,
                RadarData,AntennaData,SolarPanelData};
            for(int i=0;i<data.Length;i++)
            {
                Mesh mesh=DecodeMesh(names[i],data[i]);
                try
                {
                    if(mesh.vertexCount==0||!float.IsFinite(mesh.bounds.size.sqrMagnitude))
                        throw new InvalidDataException("Invalid embedded map mesh: "+names[i]);
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }
#endif

        private Mesh DecodeOwnedMesh(string meshName, string encoded)
        {
            Mesh mesh=DecodeMesh(meshName,encoded);
            runtimeMeshes.Add(mesh); // Own each allocation even if a later asset fails to decode.
            return mesh;
        }

        private static Mesh DecodeMesh(string meshName, string encoded)
        {
            byte[] compressed = Convert.FromBase64String(encoded);
            using MemoryStream source = new(compressed);
            using GZipStream gzip = new(source, CompressionMode.Decompress);
            using BinaryReader reader = new(gzip);

            int vertexCount = reader.ReadUInt16();
            int indexCount = reader.ReadUInt16();
            if(vertexCount==0||indexCount==0||indexCount%3!=0)
                throw new InvalidDataException("Invalid embedded map geometry: "+meshName);
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[indexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 vertex=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                if(!float.IsFinite(vertex.x)||!float.IsFinite(vertex.y)||!float.IsFinite(vertex.z))
                    throw new InvalidDataException("Non-finite embedded map vertex: "+meshName);
                vertices[i]=vertex;
            }
            for (int i = 0; i < indexCount; i++)
            {
                triangles[i]=reader.ReadUInt16();
                if(triangles[i]>=vertexCount)
                    throw new InvalidDataException("Embedded map index out of range: "+meshName);
            }
            // Read through the gzip footer before allocating a native Unity mesh.
            if(gzip.ReadByte()!=-1)
                throw new InvalidDataException("Trailing embedded map geometry: "+meshName);

            Mesh mesh = new() { name = meshName, indexFormat = IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            if(meshName=="USK_Plant_2"||meshName=="USK_Tree_Spiral_1")
                SceneryMeshQuality.SmoothPlantNormals(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeMaterials.Count; i++)
                if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
            for (int i = 0; i < runtimeMeshes.Count; i++)
                if (runtimeMeshes[i] != null) Destroy(runtimeMeshes[i]);
        }
    }
}



