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
    /// Populates both the outer perimeter and the midfield/flank zones of all non-Earth maps
    /// with planet-specific landmarks, flora, outcrops, and outposts while keeping combat lanes clear.
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
            if ((Application.isMobilePlatform || !Alterverse.Performance.MobilePerformanceProfile.AutoModeEnabled) &&
                Alterverse.Performance.MobilePerformanceProfile.Current == Alterverse.Performance.MobileQuality.Battery)
            {
                Destroy(gameObject);
                yield break;
            }

            Transform hostParent = null;
            for (int frame = 0; frame < 360 && hostParent == null; frame++)
            {
                if (ArenaVariantRuntime.Current == ArenaVariant.Earth)
                {
                    Destroy(gameObject);
                    yield break;
                }
                ProductionLunarGround ground = FindFirstObjectByType<ProductionLunarGround>();
                if (ground != null)
                {
                    hostParent = ground.transform;
                    break;
                }
                GJ504bArenaLandscape gj = FindFirstObjectByType<GJ504bArenaLandscape>();
                if (gj != null)
                {
                    hostParent = gj.transform;
                    break;
                }
                GameObject matchWorld = GameObject.Find("DF_MatchWorld");
                if (matchWorld != null && (ArenaVariantRuntime.IsDiamondCore || ArenaVariantRuntime.IsGJ504b))
                {
                    hostParent = matchWorld.transform;
                    break;
                }
                yield return null;
            }

            if (hostParent == null || ArenaVariantRuntime.Current == ArenaVariant.Earth)
            {
                Destroy(gameObject);
                yield break;
            }

            // Allow the arena's own Start() pass to finish before the decorative layer is attached.
            yield return null;

            Transform existing = hostParent.Find(RootName);
            if (existing != null)
            {
                Destroy(gameObject);
                yield break;
            }

            transform.SetParent(hostParent, true);
            gameObject.name = RootName;
            BuildScenery();
        }

        private SceneryBuildingClearance clearance;

        private void BuildScenery()
        {
            clearance = new SceneryBuildingClearance();
            rockMesh = DecodeOwnedMesh("USK_Rock_Large_1", RockLarge1Data);
            domeMesh = DecodeOwnedMesh("USK_GeodesicDome", GeodesicDomeData);
            plantMesh = DecodeOwnedMesh("USK_Plant_2", Plant2Data);
            spiralTreeMesh = DecodeOwnedMesh("USK_Tree_Spiral_1", TreeSpiral1Data);
            radarMesh = DecodeOwnedMesh("USK_Roof_Radar", RadarData);
            antennaMesh = DecodeOwnedMesh("USK_Roof_Antenna", AntennaData);
            solarPanelMesh = DecodeOwnedMesh("USK_SolarPanel_Ground", SolarPanelData);

            BuildPalette();
            BuildOuterRockField();
            BuildMidfieldRockClusters();

            switch (ArenaVariantRuntime.Current)
            {
                case ArenaVariant.Wasp76b:
                    BuildIronrainSet();
                    break;
                case ArenaVariant.Trappist1e:
                    BuildTwilightSet();
                    break;
                case ArenaVariant.GreenPlanet:
                    BuildGreenPlanetSet();
                    break;
                case ArenaVariant.Noxara:
                    BuildNoxaraSet();
                    break;
                case ArenaVariant.Mars:
                    BuildMarsSet();
                    break;
                case ArenaVariant.Kryon:
                    BuildKryonSet();
                    break;
                case ArenaVariant.Pyraxis:
                    BuildPyraxisSet();
                    break;
                case ArenaVariant.Neptune:
                    BuildNeptuneSet();
                    break;
                case ArenaVariant.Dark00:
                    BuildDark00Set();
                    break;
                case ArenaVariant.DiamondCore:
                    BuildDiamondCoreSet();
                    break;
                case ArenaVariant.GJ504b:
                    BuildGJ504bSet();
                    break;
                case ArenaVariant.Toi715b:
                    BuildToi715bSet();
                    break;
                default:
                    BuildMoonSet();
                    break;
            }
            StaticBatchingUtility.Combine(gameObject);
        }

        private void BuildPalette()
        {
            Color rock;
            Color structure;
            Color flora;
            Color accent;
            Color dark;

            switch (ArenaVariantRuntime.Current)
            {
                case ArenaVariant.Wasp76b:
                    rock = new Color(.30f, .31f, .32f);
                    structure = new Color(.43f, .26f, .15f);
                    flora = new Color(.27f, .21f, .17f);
                    accent = new Color(.95f, .44f, .10f);
                    dark = new Color(.11f, .13f, .15f);
                    break;
                case ArenaVariant.Trappist1e:
                    rock = new Color(.13f, .27f, .29f);
                    structure = new Color(.25f, .30f, .37f);
                    flora = new Color(.19f, .46f, .42f);
                    accent = new Color(.18f, .82f, .70f);
                    dark = new Color(.53f, .68f, .76f);
                    break;
                case ArenaVariant.Mars:
                    rock = new Color(.43f, .19f, .10f, 1f);
                    structure = new Color(.30f, .24f, .21f, 1f);
                    flora = new Color(.36f, .18f, .085f, 1f);
                    accent = new Color(.82f, .40f, .15f, 1f);
                    dark = new Color(.14f, .065f, .045f, 1f);
                    break;
                case ArenaVariant.GreenPlanet:
                    rock = new Color(.18f, .25f, .16f, 1f);
                    structure = new Color(.25f, .34f, .31f, 1f);
                    flora = new Color(.22f, .62f, .24f, 1f);
                    accent = new Color(.48f, .92f, .36f, 1f);
                    dark = new Color(.055f, .095f, .065f, 1f);
                    break;
                case ArenaVariant.Noxara:
                    rock = new Color(.075f, .09f, .18f, 1f);
                    structure = new Color(.14f, .13f, .26f, 1f);
                    flora = new Color(.26f, .14f, .42f, 1f);
                    accent = new Color(.56f, .34f, .94f, 1f);
                    dark = new Color(.025f, .035f, .085f, 1f);
                    break;
                case ArenaVariant.Kryon:
                    rock = new Color(.40f, .54f, .60f, 1f);
                    structure = new Color(.54f, .68f, .73f, 1f);
                    flora = new Color(.29f, .64f, .72f, 1f);
                    accent = new Color(.36f, .89f, .98f, 1f);
                    dark = new Color(.13f, .22f, .27f, 1f);
                    break;
                case ArenaVariant.Pyraxis:
                    rock = new Color(.055f, .038f, .032f, 1f);
                    structure = new Color(.16f, .095f, .075f, 1f);
                    flora = new Color(.22f, .075f, .035f, 1f);
                    accent = new Color(1f, .28f, .035f, 1f);
                    dark = new Color(.025f, .018f, .016f, 1f);
                    break;
                case ArenaVariant.Neptune:
                    rock = new Color(.025f, .09f, .18f, 1f);
                    structure = new Color(.065f, .22f, .32f, 1f);
                    flora = new Color(.05f, .38f, .52f, 1f);
                    accent = new Color(.08f, .78f, .92f, 1f);
                    dark = new Color(.012f, .045f, .11f, 1f);
                    break;
                case ArenaVariant.Dark00:
                    rock = new Color(.018f, .021f, .025f, 1f);
                    structure = new Color(.055f, .062f, .072f, 1f);
                    flora = new Color(.09f, .10f, .12f, 1f);
                    accent = new Color(.84f, .90f, .96f, 1f);
                    dark = new Color(.010f, .012f, .015f, 1f);
                    break;
                case ArenaVariant.DiamondCore:
                    rock = new Color(.08f, .14f, .20f, 1f);
                    structure = new Color(.24f, .42f, .54f, 1f);
                    flora = new Color(.38f, .68f, .82f, 1f);
                    accent = new Color(.62f, .90f, .98f, 1f);
                    dark = new Color(.025f, .042f, .068f, 1f);
                    break;
                case ArenaVariant.GJ504b:
                    rock = new Color(.34f, .085f, .22f, 1f);
                    structure = new Color(.48f, .18f, .33f, 1f);
                    flora = new Color(.62f, .20f, .40f, 1f);
                    accent = new Color(.92f, .28f, .58f, 1f);
                    dark = new Color(.18f, .045f, .12f, 1f);
                    break;
                case ArenaVariant.Toi715b:
                    rock = new Color(.46f, .16f, .095f, 1f);
                    structure = new Color(.32f, .14f, .10f, 1f);
                    flora = new Color(.18f, .13f, .22f, 1f);
                    accent = new Color(.86f, .79f, .64f, 1f);
                    dark = new Color(.22f, .08f, .055f, 1f);
                    break;
                default:
                    rock = new Color(.27f, .30f, .35f, 1f);
                    structure = new Color(.39f, .45f, .52f, 1f);
                    flora = new Color(.22f, .29f, .34f, 1f);
                    accent = new Color(.38f, .68f, .88f, 1f);
                    dark = new Color(.075f, .09f, .115f, 1f);
                    break;
            }

            rockMaterial = CreateMaterial("USK_Rock", rock, .06f, .24f);
            structureMaterial = CreateMaterial("USK_Structure", structure, .48f, .43f);
            floraMaterial = CreateMaterial("USK_Flora", flora, .02f, .22f);
            accentMaterial = CreateMaterial("USK_Accent", accent, .18f, .52f);
            darkMaterial = CreateMaterial("USK_Dark", dark, .12f, .20f);
            var planet = ArenaVariantRuntime.Current;
            PlanetSceneryMaterials.Apply(rockMaterial, planet,
                ArenaVariantRuntime.IsWasp76b || planet == ArenaVariant.Dark00 ? SceneryFinish.Metal :
                ArenaVariantRuntime.IsTrappist1e || planet == ArenaVariant.Neptune ? SceneryFinish.WetStone :
                planet == ArenaVariant.DiamondCore ? SceneryFinish.Ice : SceneryFinish.Stone);
            PlanetSceneryMaterials.Apply(structureMaterial, planet, SceneryFinish.Metal);
            PlanetSceneryMaterials.Apply(darkMaterial, planet,
                ArenaVariantRuntime.IsTrappist1e || planet == ArenaVariant.Kryon || planet == ArenaVariant.DiamondCore ? SceneryFinish.Ice :
                planet == ArenaVariant.Toi715b ? SceneryFinish.Stone : SceneryFinish.Metal);
            PlanetSceneryMaterials.Apply(accentMaterial, planet,
                ArenaVariantRuntime.IsTrappist1e || planet == ArenaVariant.GreenPlanet || planet == ArenaVariant.Toi715b || planet == ArenaVariant.Neptune ? SceneryFinish.Leaf :
                planet == ArenaVariant.Kryon || planet == ArenaVariant.DiamondCore ? SceneryFinish.Ice : SceneryFinish.Metal);
            PlanetSceneryMaterials.Apply(floraMaterial, planet,
                planet == ArenaVariant.Kryon || planet == ArenaVariant.DiamondCore ? SceneryFinish.Ice :
                planet == ArenaVariant.Mars || planet == ArenaVariant.Pyraxis || planet == ArenaVariant.GJ504b ? SceneryFinish.Stone :
                planet == ArenaVariant.Dark00 || planet == ArenaVariant.Wasp76b ? SceneryFinish.Metal : SceneryFinish.Leaf);

            if (ArenaVariantRuntime.IsExoplanet || planet == ArenaVariant.Pyraxis || planet == ArenaVariant.Neptune ||
                planet == ArenaVariant.Noxara || planet == ArenaVariant.Kryon || planet == ArenaVariant.GreenPlanet ||
                planet == ArenaVariant.Dark00)
            {
                Color glow = ArenaVariantRuntime.IsExoplanet ? ExoplanetArenaProfile.Energy(planet) * .28f : accent * .32f;
                if (planet == ArenaVariant.Toi715b) glow = Color.black;
                if (accentMaterial.HasProperty("_EmissionColor"))
                {
                    accentMaterial.EnableKeyword("_EMISSION");
                    accentMaterial.SetColor("_EmissionColor", glow);
                }
                if (ArenaVariantRuntime.IsWasp76b || planet == ArenaVariant.Dark00)
                    rockMaterial.SetFloat("_Metallic", .58f);
            }
        }

        private void BuildOuterRockField()
        {
            Vector2[] sites =
            {
                new(-40.2f, 30.8f), new(-40.4f, -28.7f), new(-35.0f, 33.1f), new(-35.8f, -31.7f),
                new(-27.8f, 34.0f), new(-28.6f, -33.8f), new(40.1f, -30.7f), new(40.4f, 28.8f),
                new(35.1f, -33.0f), new(35.8f, 31.8f), new(27.9f, -34.0f), new(28.7f, 33.8f),
                new(-41.0f, 13.0f), new(-41.0f, -12.0f), new(41.0f, 12.0f), new(41.0f, -13.0f),
                new(-19.5f, 33.4f), new(19.5f, -33.4f), new(-19.5f, -33.4f), new(19.5f, 33.4f),
                new(-10.5f, 33.6f), new(10.5f, -33.6f), new(-10.5f, -33.6f), new(10.5f, 33.6f)
            };

            for (int i = 0; i < sites.Length; i++)
            {
                float s = .32f + (i % 4) * .08f;
                Material mat = ArenaVariantRuntime.IsTrappist1e
                    ? (sites[i].y < 0 ? darkMaterial : rockMaterial)
                    : (i % 4 == 0 ? darkMaterial : rockMaterial);
                Spawn("USK_OuterRock", rockMesh, mat, sites[i],
                    new Vector3(s * (1f + (i % 3) * .14f), s * (.74f + (i % 2) * .16f), s),
                    i * 47f + 11f, -.03f, true, i % 2 == 0 ? 7f : -5f, i % 3 == 0 ? 4f : -3f);
            }
        }

        // Populates the midfield flanks, lane shoulders, and Hot Zone outer perimeter across all non-Earth maps.
        private void BuildMidfieldRockClusters()
        {
            Vector2[] midfieldSites =
            {
                // Hot Zone outer 4 corners
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-13.8f, -14.2f), new(13.8f, 14.2f),
                // Inner midfield basins
                new(-16.5f, 21.5f), new(16.5f, -21.5f), new(-16.5f, -21.5f), new(16.5f, 21.5f),
                // Side lane shoulders
                new(-21.5f, 12.6f), new(21.5f, -12.6f), new(-21.5f, -12.6f), new(21.5f, 12.6f),
                // Outer midfield flanks
                new(-27.5f, 19.5f), new(27.5f, -19.5f), new(-27.5f, -19.5f), new(27.5f, 19.5f),
                // North/South central midfield ridges
                new(-7.0f, 21.5f), new(7.0f, -21.5f), new(-7.0f, -21.5f), new(7.0f, 21.5f),
                new(0.0f, 24.5f), new(0.0f, -24.5f),
                // Base flank shoulders
                new(-31.5f, 13.5f), new(31.5f, -13.5f), new(-31.5f, -13.5f), new(31.5f, 13.5f)
            };

            for (int i = 0; i < midfieldSites.Length; i++)
            {
                Vector2 center = midfieldSites[i];
                bool coldSide = ArenaVariantRuntime.IsTrappist1e && center.y < 0f;
                Material primaryMat = coldSide ? darkMaterial : (i % 3 == 0 ? darkMaterial : rockMaterial);
                Material secondaryMat = coldSide ? rockMaterial : (i % 2 == 0 ? rockMaterial : darkMaterial);
                float baseScale = .26f + (i % 3) * .065f;
                float yaw = i * 53f + 19f;

                Spawn("USK_MidfieldRock_Primary", rockMesh, primaryMat, center,
                    new Vector3(baseScale * 1.18f, baseScale * (.78f + (i % 2) * .20f), baseScale),
                    yaw, -.03f, true, (i % 2 == 0 ? 6f : -5f), (i % 3 == 0 ? 4f : -4f));

                Vector2 dir = new(Mathf.Cos(yaw * Mathf.Deg2Rad), Mathf.Sin(yaw * Mathf.Deg2Rad));
                Spawn("USK_MidfieldRock_Shoulder", rockMesh, secondaryMat, center + dir * .95f,
                    new Vector3(baseScale * .76f, baseScale * .54f, baseScale * .72f),
                    yaw + 67f, -.04f, false, -4f, 6f);
            }
        }

        private void BuildMoonSet()
        {
            // Outer and midfield lunar research domes
            Spawn("USK_MoonResearchDome_A", domeMesh, structureMaterial, new Vector2(-37.0f, 28.7f),
                new Vector3(.72f, .66f, .72f), 24f, -.03f, true);
            Spawn("USK_MoonResearchDome_B", domeMesh, structureMaterial, new Vector2(37.0f, -28.8f),
                new Vector3(.64f, .59f, .64f), 205f, -.03f, true);
            Spawn("USK_MoonMidDome_NW", domeMesh, structureMaterial, new Vector2(-24.8f, 20.8f),
                new Vector3(.56f, .50f, .56f), 38f, -.03f, true);
            Spawn("USK_MoonMidDome_SE", domeMesh, structureMaterial, new Vector2(24.8f, -20.8f),
                new Vector3(.56f, .50f, .56f), 218f, -.03f, true);
            Spawn("USK_MoonMidDome_NE", domeMesh, darkMaterial, new Vector2(25.2f, 20.4f),
                new Vector3(.50f, .44f, .50f), 142f, -.04f, true, 4f, -3f);
            Spawn("USK_MoonMidDome_SW", domeMesh, darkMaterial, new Vector2(-25.2f, -20.4f),
                new Vector3(.50f, .44f, .50f), 322f, -.04f, true, -4f, 3f);

            // Radars, antennas, and solar arrays across perimeter and midfield
            Spawn("USK_MoonRadar", radarMesh, structureMaterial, new Vector2(-31.5f, 31.6f),
                new Vector3(.72f, .72f, .72f), 32f, -.02f, true);
            Spawn("USK_MoonAntenna", antennaMesh, accentMaterial, new Vector2(32.2f, 31.3f),
                new Vector3(.58f, .58f, .58f), 212f, -.02f, true);
            Spawn("USK_MoonMidRadar_A", radarMesh, accentMaterial, new Vector2(-17.5f, 23.0f),
                new Vector3(.54f, .54f, .54f), 48f, -.02f, true);
            Spawn("USK_MoonMidRadar_B", radarMesh, accentMaterial, new Vector2(17.5f, -23.0f),
                new Vector3(.54f, .54f, .54f), 228f, -.02f, true);
            Spawn("USK_MoonMidAntenna_A", antennaMesh, structureMaterial, new Vector2(-14.2f, -15.0f),
                new Vector3(.48f, .48f, .48f), 19f, -.02f, true);
            Spawn("USK_MoonMidAntenna_B", antennaMesh, structureMaterial, new Vector2(14.2f, 15.0f),
                new Vector3(.48f, .48f, .48f), 199f, -.02f, true);

            Spawn("USK_MoonSolar_A", solarPanelMesh, structureMaterial, new Vector2(-32.5f, -32.1f),
                new Vector3(.78f, .78f, .78f), 28f, -.02f, true);
            Spawn("USK_MoonSolar_B", solarPanelMesh, structureMaterial, new Vector2(32.8f, -31.8f),
                new Vector3(.76f, .76f, .76f), 204f, -.02f, true);
            Spawn("USK_MoonMidSolar_A", solarPanelMesh, structureMaterial, new Vector2(-28.2f, 13.8f),
                new Vector3(.62f, .62f, .62f), 18f, -.02f, true);
            Spawn("USK_MoonMidSolar_B", solarPanelMesh, structureMaterial, new Vector2(28.2f, -13.8f),
                new Vector3(.62f, .62f, .62f), 198f, -.02f, true);
            Spawn("USK_MoonMidSolar_C", solarPanelMesh, structureMaterial, new Vector2(0.0f, 22.8f),
                new Vector3(.58f, .58f, .58f), 90f, -.02f, true);
            Spawn("USK_MoonMidSolar_D", solarPanelMesh, structureMaterial, new Vector2(0.0f, -22.8f),
                new Vector3(.58f, .58f, .58f), 270f, -.02f, true);

            BuildPlantCluster(new Vector2(-35.7f, -29.2f), 6, .56f, accentMaterial, 14);
            BuildPlantCluster(new Vector2(35.8f, 29.0f), 6, .52f, accentMaterial, 41);
            BuildPlantCluster(new Vector2(-23.5f, 19.2f), 6, .48f, accentMaterial, 53);
            BuildPlantCluster(new Vector2(23.5f, -19.2f), 6, .48f, accentMaterial, 67);
            BuildPlantCluster(new Vector2(-15.2f, -20.5f), 5, .46f, floraMaterial, 79);
            BuildPlantCluster(new Vector2(15.2f, 20.5f), 5, .46f, floraMaterial, 93);
            BuildPlantCluster(new Vector2(-40.0f, 3.0f), 4, .43f, darkMaterial, 71);
            BuildPlantCluster(new Vector2(40.0f, -3.5f), 4, .43f, darkMaterial, 96);
        }

        private void BuildIronrainSet()
        {
            foreach (int side in new[] { -1, 1 })
            {
                Spawn("USK_IronrainObservationPod", domeMesh, structureMaterial, new Vector2(side * 37f, side * 28.7f),
                    new Vector3(.65f, .56f, .65f), side < 0 ? 24f : 204f, -.03f, true);
                Spawn("USK_IronrainMidPod", domeMesh, structureMaterial, new Vector2(side * 25.5f, -side * 20.2f),
                    new Vector3(.54f, .48f, .54f), side < 0 ? 42f : 222f, -.03f, true);
                Spawn("USK_IronrainWeatherSensor", radarMesh, structureMaterial, new Vector2(side * 34f, -side * 31.2f),
                    Vector3.one * .55f, side * 32f, -.02f, true);
                Spawn("USK_IronrainMidRadar", radarMesh, accentMaterial, new Vector2(side * 17.5f, side * 22.0f),
                    Vector3.one * .52f, side * 48f, -.02f, true);
                Spawn("USK_IronrainSolarShield", solarPanelMesh, darkMaterial, new Vector2(side * 28.0f, side * 13.6f),
                    Vector3.one * .60f, side < 0 ? 20f : 200f, -.02f, true);
                Spawn("USK_IronrainRelay", antennaMesh, accentMaterial, new Vector2(side * 14.0f, -side * 14.6f),
                    Vector3.one * .48f, side * 37f, -.02f, true);

                for (int i = 0; i < 4; i++)
                    Spawn("USK_IronrainMetalStack", rockMesh, i % 3 == 0 ? accentMaterial : rockMaterial,
                        new Vector2(side * (39f - i * .55f), -22f + i * 14.5f),
                        new Vector3(.26f, .44f + (i % 2) * .12f, .28f), i * 61f + side * 17f, -.03f, true, side * 8f, 5f);
            }

            Vector2[] slagTowers =
            {
                new(-24.5f, 18.8f), new(24.5f, -18.8f), new(-24.5f, -18.8f), new(24.5f, 18.8f),
                new(-15.5f, 20.5f), new(15.5f, -20.5f), new(-15.5f, -20.5f), new(15.5f, 20.5f),
                new(-21.0f, 13.0f), new(21.0f, -13.0f), new(0.0f, 22.0f), new(0.0f, -22.0f)
            };
            for (int i = 0; i < slagTowers.Length; i++)
            {
                float s = .56f + (i % 3) * .10f;
                Spawn("USK_IronrainSlagSpire", spiralTreeMesh, i % 3 == 0 ? accentMaterial : darkMaterial, slagTowers[i],
                    new Vector3(s * .76f, s * 1.12f, s * .76f), i * 49f, -.03f, true, (i % 2 == 0 ? 5f : -5f), 0f);
            }

            BuildPlantCluster(new Vector2(-23.0f, 18.0f), 6, .52f, accentMaterial, 21);
            BuildPlantCluster(new Vector2(23.0f, -18.0f), 6, .52f, accentMaterial, 43);
            BuildPlantCluster(new Vector2(-14.2f, 14.5f), 5, .48f, floraMaterial, 65);
            BuildPlantCluster(new Vector2(14.2f, -14.5f), 5, .48f, floraMaterial, 87);
        }

        private void BuildTwilightSet()
        {
            foreach (int side in new[] { -1, 1 })
            {
                // Southern cold side: cryo pods, radars, and frozen glacial outcrops
                Spawn("USK_TwilightObservationPod", domeMesh, structureMaterial, new Vector2(side * 37f, -28.8f),
                    new Vector3(.55f, .49f, .55f), side * 24f, -.03f, true);
                Spawn("USK_TwilightMidColdPod", domeMesh, structureMaterial, new Vector2(side * 25.0f, -19.8f),
                    new Vector3(.50f, .45f, .50f), side * 38f, -.03f, true);
                Spawn("USK_TwilightColdRadar", radarMesh, darkMaterial, new Vector2(side * 16.5f, -22.0f),
                    Vector3.one * .52f, side * 51f, -.02f, true);

                // Northern warm side: lush bioluminescent spiral trees & alien flora across perimeter and midfield
                for (int i = 0; i < 3; i++)
                    Spawn("USK_TwilightSpiralGrowth", spiralTreeMesh, i == 1 ? accentMaterial : floraMaterial,
                        new Vector2(side * (38.8f - i * 5.9f), 29f + i * 1.4f),
                        new Vector3(.64f, .73f, .64f), i * 43f + side * 21f, -.03f, true);

                Vector2[] warmMidTrees =
                {
                    new(side * 25.5f, 19.2f), new(side * 16.5f, 20.8f),
                    new(side * 21.5f, 13.0f), new(side * 13.8f, 14.5f), new(side * 7.5f, 21.5f)
                };
                for (int i = 0; i < warmMidTrees.Length; i++)
                {
                    float s = .62f + (i % 3) * .09f;
                    Spawn("USK_TwilightWarmMidTree", spiralTreeMesh, i % 2 == 0 ? floraMaterial : accentMaterial,
                        warmMidTrees[i], new Vector3(s, s * 1.08f, s), i * 57f + side * 19f, -.02f, true);
                }

                BuildPlantCluster(new Vector2(side * 38.3f, 10f), 5, .48f, floraMaterial, side < 0 ? 17 : 59);
                BuildPlantCluster(new Vector2(side * 34.5f, 28.8f), 5, .48f, accentMaterial, side < 0 ? 37 : 83);
                BuildPlantCluster(new Vector2(side * 24.0f, 18.5f), 7, .54f, floraMaterial, side < 0 ? 23 : 61);
                BuildPlantCluster(new Vector2(side * 15.0f, 19.5f), 6, .52f, accentMaterial, side < 0 ? 31 : 73);
                BuildPlantCluster(new Vector2(side * 13.5f, 14.0f), 5, .46f, floraMaterial, side < 0 ? 41 : 89);

                // Southern cold side: frozen crystal spires and glacial outcrops in the midfield
                for (int i = 0; i < 3; i++)
                    Spawn("USK_TwilightFrozenOutcrop", rockMesh, darkMaterial,
                        new Vector2(side * (39f - i * 5f), -30f - i * .7f),
                        new Vector3(.36f, .31f, .39f), side * 31f + i * 51f, -.03f, true);

                Vector2[] coldMidSpires =
                {
                    new(side * 25.5f, -19.2f), new(side * 16.5f, -20.8f),
                    new(side * 21.5f, -13.0f), new(side * 13.8f, -14.5f), new(side * 7.5f, -21.5f)
                };
                for (int i = 0; i < coldMidSpires.Length; i++)
                {
                    float s = .56f + (i % 3) * .08f;
                    Spawn("USK_TwilightColdMidSpire", spiralTreeMesh, darkMaterial, coldMidSpires[i],
                        new Vector3(s * .78f, s * 1.12f, s * .78f), i * 63f + side * 29f, -.03f, true, 6f, -4f);
                }
                BuildPlantCluster(new Vector2(side * 23.5f, -18.5f), 6, .50f, darkMaterial, side < 0 ? 19 : 47);
                BuildPlantCluster(new Vector2(side * 14.5f, -19.5f), 5, .48f, accentMaterial, side < 0 ? 29 : 67);
            }
        }

        private void BuildMarsSet()
        {
            Spawn("USK_MarsAbandonedDome", domeMesh, darkMaterial, new Vector2(-37.4f, -29.0f),
                new Vector3(.66f, .54f, .72f), 31f, -.05f, true, 4f, -3f);
            Spawn("USK_MarsAbandonedDome_B", domeMesh, darkMaterial, new Vector2(37.4f, 29.0f),
                new Vector3(.66f, .54f, .72f), 211f, -.05f, true, -4f, 3f);
            Spawn("USK_MarsMidDome_A", domeMesh, structureMaterial, new Vector2(-27.2f, 20.8f),
                new Vector3(.54f, .46f, .58f), 42f, -.04f, true, 3f, -2f);
            Spawn("USK_MarsMidDome_B", domeMesh, structureMaterial, new Vector2(27.2f, -20.8f),
                new Vector3(.54f, .46f, .58f), 222f, -.04f, true, -3f, 2f);

            Spawn("USK_MarsRadarWreck", radarMesh, darkMaterial, new Vector2(-34.2f, 30.5f),
                new Vector3(.60f, .60f, .60f), 17f, -.05f, true, 11f, -7f);
            Spawn("USK_MarsSolarWreck", solarPanelMesh, structureMaterial, new Vector2(34.0f, -32.2f),
                new Vector3(.70f, .70f, .70f), 197f, -.04f, true, -9f, 5f);
            Spawn("USK_MarsMidSolar_A", solarPanelMesh, structureMaterial, new Vector2(-18.5f, -22.5f),
                new Vector3(.58f, .58f, .58f), 34f, -.04f, true, 7f, -5f);
            Spawn("USK_MarsMidSolar_B", solarPanelMesh, structureMaterial, new Vector2(18.5f, 22.5f),
                new Vector3(.58f, .58f, .58f), 214f, -.04f, true, -7f, 5f);
            Spawn("USK_MarsAntenna", antennaMesh, structureMaterial, new Vector2(-40.0f, 6.0f),
                new Vector3(.50f, .50f, .50f), 62f, -.04f, true, 6f, 4f);
            Spawn("USK_MarsAntenna_B", antennaMesh, structureMaterial, new Vector2(40.0f, -6.0f),
                new Vector3(.50f, .50f, .50f), 242f, -.04f, true, -6f, -4f);

            Vector2[] spires =
            {
                new(-34.5f, 31.5f), new(-26.5f, 33.0f), new(36.0f, 30.5f), new(29.0f, -33.0f),
                new(-39.0f, 16.5f), new(39.2f, -17.0f),
                // Midfield canyon hoodoo spires
                new(-25.0f, 19.0f), new(25.0f, -19.0f), new(-25.0f, -19.0f), new(25.0f, 19.0f),
                new(-16.0f, 21.0f), new(16.0f, -21.0f), new(-16.0f, -21.0f), new(16.0f, 21.0f),
                new(-13.8f, 14.5f), new(13.8f, -14.5f), new(-21.2f, 12.8f), new(21.2f, -12.8f),
                new(0.0f, 22.5f), new(0.0f, -22.5f)
            };
            for (int i = 0; i < spires.Length; i++)
            {
                float s = .58f + (i % 3) * .12f;
                Spawn("USK_MarsWeatheredSpire", spiralTreeMesh, i % 4 == 0 ? accentMaterial : floraMaterial, spires[i],
                    new Vector3(s * .80f, s * (1.08f + (i % 2) * .14f), s * .80f), i * 61f, -.04f, true,
                    i % 2 == 0 ? 5f : -4f, 0f);
            }

            BuildPlantCluster(new Vector2(-39.0f, -7.0f), 5, .52f, accentMaterial, 23);
            BuildPlantCluster(new Vector2(39.0f, 8.0f), 5, .48f, accentMaterial, 58);
            BuildPlantCluster(new Vector2(-24.0f, 18.5f), 6, .50f, accentMaterial, 31);
            BuildPlantCluster(new Vector2(24.0f, -18.5f), 6, .50f, accentMaterial, 47);
            BuildPlantCluster(new Vector2(-15.0f, 20.0f), 6, .48f, floraMaterial, 63);
            BuildPlantCluster(new Vector2(15.0f, -20.0f), 6, .48f, floraMaterial, 79);
            BuildPlantCluster(new Vector2(-14.0f, -14.5f), 5, .46f, accentMaterial, 91);
            BuildPlantCluster(new Vector2(14.0f, 14.5f), 5, .46f, accentMaterial, 107);
        }

        private void BuildGreenPlanetSet()
        {
            Vector2[] trees =
            {
                new(-38.8f, 27.8f), new(-34.3f, 32.2f), new(-26.3f, 32.8f), new(-39.5f, -20.0f),
                new(-34.0f, -31.0f), new(-25.8f, -32.8f), new(38.7f, -27.8f), new(34.2f, -32.0f),
                new(26.2f, -32.8f), new(39.4f, 20.0f), new(34.0f, 31.0f), new(25.8f, 32.8f),
                // Midfield alien jungle groves
                new(-26.0f, 18.8f), new(26.0f, -18.8f), new(-26.0f, -18.8f), new(26.0f, 18.8f),
                new(-19.5f, 22.5f), new(19.5f, -22.5f), new(-19.5f, -22.5f), new(19.5f, 22.5f),
                new(-14.5f, 19.5f), new(14.5f, -19.5f), new(-14.5f, -19.5f), new(14.5f, 19.5f),
                new(-22.2f, 12.8f), new(22.2f, -12.8f), new(-22.2f, -12.8f), new(22.2f, 12.8f),
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-13.8f, -14.2f), new(13.8f, 14.2f),
                new(-6.8f, 20.8f), new(6.8f, -20.8f), new(0.0f, 23.5f), new(0.0f, -23.5f)
            };

            for (int i = 0; i < trees.Length; i++)
            {
                float s = .74f + (i % 4) * .09f;
                Spawn("USK_XenoSpiralTree", spiralTreeMesh, i % 4 == 0 ? accentMaterial : floraMaterial, trees[i],
                    new Vector3(s, s * (1.06f + (i % 2) * .09f), s), i * 37f, -.02f, true,
                    i % 3 == 0 ? 3f : 0f, i % 4 == 0 ? -3f : 0f);
            }

            Vector2[] plantGroves =
            {
                new(-37.0f, 8.5f), new(-33.5f, -28.5f), new(37.2f, -8.0f), new(33.4f, 28.2f),
                new(-25.0f, 18.2f), new(25.0f, -18.2f), new(-25.0f, -18.2f), new(25.0f, 18.2f),
                new(-15.2f, 19.8f), new(15.2f, -19.8f), new(-15.2f, -19.8f), new(15.2f, 19.8f),
                new(-13.8f, 14.5f), new(13.8f, -14.5f), new(-21.5f, 13.0f), new(21.5f, -13.0f),
                new(0.0f, 22.0f), new(0.0f, -22.0f)
            };
            for (int i = 0; i < plantGroves.Length; i++)
            {
                BuildPlantCluster(plantGroves[i], 8, .64f, i % 2 == 0 ? floraMaterial : accentMaterial, 17 + i * 19);
            }

            Spawn("USK_GreenResearchDome", domeMesh, structureMaterial, new Vector2(-37.0f, -27.8f),
                new Vector3(.55f, .50f, .55f), 18f, -.03f, true);
            Spawn("USK_GreenResearchDome_B", domeMesh, structureMaterial, new Vector2(37.0f, 27.8f),
                new Vector3(.55f, .50f, .55f), 198f, -.03f, true);
            Spawn("USK_GreenMidDome_A", domeMesh, structureMaterial, new Vector2(-28.2f, 20.5f),
                new Vector3(.48f, .44f, .48f), 42f, -.03f, true);
            Spawn("USK_GreenMidDome_B", domeMesh, structureMaterial, new Vector2(28.2f, -20.5f),
                new Vector3(.48f, .44f, .48f), 222f, -.03f, true);
            Spawn("USK_GreenAntenna", antennaMesh, accentMaterial, new Vector2(34.0f, -31.0f),
                new Vector3(.45f, .45f, .45f), 193f, -.02f, true);
        }

        private void BuildNoxaraSet()
        {
            Vector2[] trees =
            {
                new(-39.0f, 24.0f), new(-33.5f, 31.7f), new(-27.0f, 33.1f), new(-39.2f, -18.0f),
                new(-31.0f, -32.2f), new(39.0f, -24.0f), new(33.5f, -31.7f), new(27.0f, -33.1f),
                new(39.2f, 18.0f), new(31.0f, 32.2f),
                // Midfield dark twilight spires
                new(-25.5f, 19.0f), new(25.5f, -19.0f), new(-25.5f, -19.0f), new(25.5f, 19.0f),
                new(-16.0f, 20.8f), new(16.0f, -20.8f), new(-16.0f, -20.8f), new(16.0f, 20.8f),
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-21.5f, 12.8f), new(21.5f, -12.8f),
                new(0.0f, 22.2f), new(0.0f, -22.2f)
            };

            for (int i = 0; i < trees.Length; i++)
            {
                float s = .70f + (i % 3) * .10f;
                Spawn("USK_NoxaraSpiralGrowth", spiralTreeMesh, i % 3 == 0 ? accentMaterial : floraMaterial, trees[i],
                    new Vector3(s * .86f, s * 1.16f, s * .86f), i * 49f + 15f, -.03f, true,
                    i % 2 == 0 ? 6f : -5f, i % 4 == 0 ? 4f : 0f);
            }

            Vector2[] floraSites =
            {
                new(-40.0f, 5.0f), new(-34.0f, -29.0f), new(40.0f, -5.0f), new(34.0f, 29.0f),
                new(-24.5f, 18.5f), new(24.5f, -18.5f), new(-24.5f, -18.5f), new(24.5f, 18.5f),
                new(-15.0f, 20.0f), new(15.0f, -20.0f), new(-13.8f, -14.5f), new(13.8f, 14.5f),
                new(0.0f, 21.5f), new(0.0f, -21.5f)
            };
            for (int i = 0; i < floraSites.Length; i++)
            {
                BuildPlantCluster(floraSites[i], 8, .62f, i % 2 == 0 ? accentMaterial : floraMaterial, 29 + i * 17);
            }

            Spawn("USK_NoxaraRuinedDome", domeMesh, darkMaterial, new Vector2(37.4f, 27.7f),
                new Vector3(.52f, .44f, .58f), 211f, -.06f, true, -5f, 4f);
            Spawn("USK_NoxaraMidDome_A", domeMesh, structureMaterial, new Vector2(-27.5f, -20.5f),
                new Vector3(.50f, .44f, .52f), 38f, -.04f, true, 4f, -3f);
            Spawn("USK_NoxaraMidDome_B", domeMesh, structureMaterial, new Vector2(27.5f, 20.5f),
                new Vector3(.50f, .44f, .52f), 218f, -.04f, true, -4f, 3f);
            Spawn("USK_NoxaraRadarWreck", radarMesh, darkMaterial, new Vector2(-35.0f, -30.8f),
                new Vector3(.52f, .52f, .52f), 48f, -.05f, true, 13f, 8f);
            Spawn("USK_NoxaraMidRadar_A", radarMesh, accentMaterial, new Vector2(-18.0f, 22.8f),
                new Vector3(.50f, .50f, .50f), 64f, -.03f, true, 8f, -5f);
            Spawn("USK_NoxaraMidRadar_B", radarMesh, accentMaterial, new Vector2(18.0f, -22.8f),
                new Vector3(.50f, .50f, .50f), 244f, -.03f, true, -8f, 5f);
        }

        private void BuildKryonSet()
        {
            Spawn("USK_KryonDome_A", domeMesh, structureMaterial, new Vector2(-37.2f, 28.8f),
                new Vector3(.67f, .61f, .67f), 22f, -.04f, true);
            Spawn("USK_KryonDome_B", domeMesh, structureMaterial, new Vector2(37.2f, -28.8f),
                new Vector3(.60f, .55f, .60f), 202f, -.04f, true);
            Spawn("USK_KryonMidDome_A", domeMesh, structureMaterial, new Vector2(-26.2f, -20.2f),
                new Vector3(.54f, .48f, .54f), 34f, -.03f, true);
            Spawn("USK_KryonMidDome_B", domeMesh, structureMaterial, new Vector2(26.2f, 20.2f),
                new Vector3(.54f, .48f, .54f), 214f, -.03f, true);
            Spawn("USK_KryonRadar", radarMesh, accentMaterial, new Vector2(-32.0f, 31.5f),
                new Vector3(.56f, .56f, .56f), 29f, -.02f, true);
            Spawn("USK_KryonMidRadar_A", radarMesh, accentMaterial, new Vector2(-17.8f, 22.2f),
                new Vector3(.52f, .52f, .52f), 45f, -.02f, true);
            Spawn("USK_KryonMidRadar_B", radarMesh, accentMaterial, new Vector2(17.8f, -22.2f),
                new Vector3(.52f, .52f, .52f), 225f, -.02f, true);
            Spawn("USK_KryonSolar", solarPanelMesh, structureMaterial, new Vector2(32.0f, -31.7f),
                new Vector3(.64f, .64f, .64f), 208f, -.02f, true);
            Spawn("USK_KryonMidSolar_A", solarPanelMesh, structureMaterial, new Vector2(-28.2f, 13.5f),
                new Vector3(.58f, .58f, .58f), 22f, -.02f, true);
            Spawn("USK_KryonMidSolar_B", solarPanelMesh, structureMaterial, new Vector2(28.2f, -13.5f),
                new Vector3(.58f, .58f, .58f), 202f, -.02f, true);
            Spawn("USK_KryonAntenna", antennaMesh, accentMaterial, new Vector2(40.0f, 1.0f),
                new Vector3(.46f, .46f, .46f), 185f, -.02f, true);

            // Tall glacial ice spires across the midfield
            Vector2[] iceSpires =
            {
                new(-25.0f, 19.0f), new(25.0f, -19.0f), new(-25.0f, -19.0f), new(25.0f, 19.0f),
                new(-15.8f, 20.5f), new(15.8f, -20.5f), new(-15.8f, -20.5f), new(15.8f, 20.5f),
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-21.2f, 12.8f), new(21.2f, -12.8f),
                new(0.0f, 22.5f), new(0.0f, -22.5f)
            };
            for (int i = 0; i < iceSpires.Length; i++)
            {
                float s = .62f + (i % 3) * .10f;
                Spawn("USK_KryonGlacierSpire", spiralTreeMesh, i % 2 == 0 ? accentMaterial : floraMaterial, iceSpires[i],
                    new Vector3(s * .82f, s * 1.18f, s * .82f), i * 53f, -.03f, true, (i % 2 == 0 ? 5f : -5f), 0f);
            }

            BuildPlantCluster(new Vector2(-40.0f, -5.0f), 8, .58f, accentMaterial, 31);
            BuildPlantCluster(new Vector2(-31.5f, -31.0f), 7, .54f, floraMaterial, 57);
            BuildPlantCluster(new Vector2(40.0f, 5.0f), 8, .58f, accentMaterial, 81);
            BuildPlantCluster(new Vector2(31.5f, 31.0f), 7, .54f, floraMaterial, 109);
            BuildPlantCluster(new Vector2(-24.0f, 18.5f), 7, .56f, accentMaterial, 43);
            BuildPlantCluster(new Vector2(24.0f, -18.5f), 7, .56f, accentMaterial, 67);
            BuildPlantCluster(new Vector2(-15.0f, 19.8f), 6, .52f, floraMaterial, 89);
            BuildPlantCluster(new Vector2(15.0f, -19.8f), 6, .52f, floraMaterial, 113);
        }

        private void BuildPyraxisSet()
        {
            Vector2[] chimneys =
            {
                new(-25.5f, 19.2f), new(25.5f, -19.2f), new(-25.5f, -19.2f), new(25.5f, 19.2f),
                new(-16.0f, 20.8f), new(16.0f, -20.8f), new(-16.0f, -20.8f), new(16.0f, 20.8f),
                new(-13.8f, 14.5f), new(13.8f, -14.5f), new(-21.5f, 12.8f), new(21.5f, -12.8f),
                new(0.0f, 22.5f), new(0.0f, -22.5f), new(-34.0f, 30.5f), new(34.0f, -30.5f)
            };
            for (int i = 0; i < chimneys.Length; i++)
            {
                float s = .60f + (i % 3) * .11f;
                Spawn("USK_PyraxisVolcanicSpire", spiralTreeMesh, i % 3 == 0 ? accentMaterial : darkMaterial, chimneys[i],
                    new Vector3(s * .85f, s * 1.15f, s * .85f), i * 47f, -.04f, true, (i % 2 == 0 ? 6f : -6f), 0f);
            }

            foreach (int side in new[] { -1, 1 })
            {
                Spawn("USK_PyraxisThermalDome", domeMesh, structureMaterial, new Vector2(side * 27.5f, -side * 20.5f),
                    new Vector3(.52f, .45f, .52f), side * 35f, -.03f, true);
                Spawn("USK_PyraxisSeismicRadar", radarMesh, darkMaterial, new Vector2(side * 18.0f, side * 22.5f),
                    Vector3.one * .52f, side * 52f, -.02f, true);
                BuildPlantCluster(new Vector2(side * 24.5f, side * 18.5f), 7, .54f, accentMaterial, side < 0 ? 19 : 53);
                BuildPlantCluster(new Vector2(side * 15.2f, -side * 19.8f), 6, .50f, floraMaterial, side < 0 ? 37 : 79);
                BuildPlantCluster(new Vector2(side * 13.8f, side * 14.2f), 5, .48f, accentMaterial, side < 0 ? 61 : 97);
            }
        }

        private void BuildNeptuneSet()
        {
            Vector2[] coralTowers =
            {
                new(-25.5f, 19.0f), new(25.5f, -19.0f), new(-25.5f, -19.0f), new(25.5f, 19.0f),
                new(-16.0f, 20.8f), new(16.0f, -20.8f), new(-16.0f, -20.8f), new(16.0f, 20.8f),
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-13.8f, -14.2f), new(13.8f, 14.2f),
                new(-21.5f, 12.8f), new(21.5f, -12.8f), new(0.0f, 22.5f), new(0.0f, -22.5f),
                new(-34.0f, 30.2f), new(34.0f, -30.2f), new(-34.0f, -30.2f), new(34.0f, 30.2f)
            };
            for (int i = 0; i < coralTowers.Length; i++)
            {
                float s = .68f + (i % 4) * .09f;
                Spawn("USK_NeptuneCoralTower", spiralTreeMesh, i % 3 == 0 ? accentMaterial : floraMaterial, coralTowers[i],
                    new Vector3(s * .88f, s * 1.14f, s * .88f), i * 43f, -.03f, true, (i % 2 == 0 ? 5f : -4f), 0f);
            }

            foreach (int side in new[] { -1, 1 })
            {
                Spawn("USK_NeptuneBathysphere", domeMesh, structureMaterial, new Vector2(side * 27.2f, -side * 20.5f),
                    new Vector3(.54f, .48f, .54f), side * 41f, -.03f, true);
                Spawn("USK_NeptuneSonarArray", radarMesh, accentMaterial, new Vector2(side * 18.0f, side * 22.5f),
                    Vector3.one * .52f, side * 57f, -.02f, true);
                BuildPlantCluster(new Vector2(side * 24.5f, side * 18.5f), 8, .62f, accentMaterial, side < 0 ? 23 : 59);
                BuildPlantCluster(new Vector2(side * 24.5f, -side * 18.5f), 7, .58f, floraMaterial, side < 0 ? 31 : 71);
                BuildPlantCluster(new Vector2(side * 15.2f, side * 19.8f), 7, .56f, floraMaterial, side < 0 ? 43 : 83);
                BuildPlantCluster(new Vector2(side * 13.8f, -side * 14.2f), 6, .52f, accentMaterial, side < 0 ? 53 : 97);
            }
        }

        private void BuildDark00Set()
        {
            foreach (int side in new[] { -1, 1 })
            {
                Spawn("USK_Dark00StealthDome_A", domeMesh, darkMaterial, new Vector2(side * 26.5f, side * 20.0f),
                    new Vector3(.58f, .50f, .58f), side < 0 ? 28f : 208f, -.03f, true);
                Spawn("USK_Dark00StealthDome_B", domeMesh, structureMaterial, new Vector2(side * 26.5f, -side * 20.0f),
                    new Vector3(.54f, .46f, .54f), side < 0 ? 148f : 328f, -.03f, true);
                Spawn("USK_Dark00Radar", radarMesh, accentMaterial, new Vector2(side * 16.5f, side * 21.5f),
                    Vector3.one * .56f, side * 42f, -.02f, true);
                Spawn("USK_Dark00Antenna_A", antennaMesh, accentMaterial, new Vector2(side * 14.0f, -side * 14.5f),
                    Vector3.one * .52f, side * 31f, -.02f, true);
                Spawn("USK_Dark00Antenna_B", antennaMesh, accentMaterial, new Vector2(side * 22.0f, side * 13.0f),
                    Vector3.one * .50f, side * 67f, -.02f, true);
                Spawn("USK_Dark00SolarArray", solarPanelMesh, structureMaterial, new Vector2(side * 28.5f, side * 13.5f),
                    Vector3.one * .64f, side < 0 ? 18f : 198f, -.02f, true);
                Spawn("USK_Dark00MidSolar", solarPanelMesh, structureMaterial, new Vector2(side * 19.0f, -side * 23.0f),
                    Vector3.one * .60f, side < 0 ? 45f : 225f, -.02f, true);

                BuildPlantCluster(new Vector2(side * 24.5f, side * 18.8f), 6, .50f, accentMaterial, side < 0 ? 19 : 53);
                BuildPlantCluster(new Vector2(side * 15.2f, -side * 20.0f), 6, .48f, darkMaterial, side < 0 ? 37 : 79);
            }
        }

        private void BuildDiamondCoreSet()
        {
            Vector2[] prismTowers =
            {
                new(-25.5f, 19.0f), new(25.5f, -19.0f), new(-25.5f, -19.0f), new(25.5f, 19.0f),
                new(-16.0f, 20.8f), new(16.0f, -20.8f), new(-16.0f, -20.8f), new(16.0f, 20.8f),
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-21.5f, 12.8f), new(21.5f, -12.8f),
                new(0.0f, 22.5f), new(0.0f, -22.5f)
            };
            for (int i = 0; i < prismTowers.Length; i++)
            {
                float s = .62f + (i % 3) * .10f;
                Spawn("USK_DiamondCoreSpire", spiralTreeMesh, i % 2 == 0 ? accentMaterial : floraMaterial, prismTowers[i],
                    new Vector3(s * .80f, s * 1.18f, s * .80f), i * 51f, -.03f, true, (i % 2 == 0 ? 5f : -5f), 0f);
            }

            foreach (int side in new[] { -1, 1 })
            {
                Spawn("USK_DiamondCoreHarmonicDome", domeMesh, structureMaterial, new Vector2(side * 27.2f, -side * 20.5f),
                    new Vector3(.54f, .48f, .54f), side * 36f, -.03f, true);
                Spawn("USK_DiamondCoreBeacon", antennaMesh, accentMaterial, new Vector2(side * 18.0f, side * 22.5f),
                    Vector3.one * .52f, side * 49f, -.02f, true);
                BuildPlantCluster(new Vector2(side * 24.0f, side * 18.5f), 7, .56f, accentMaterial, side < 0 ? 23 : 61);
                BuildPlantCluster(new Vector2(side * 15.0f, -side * 19.8f), 6, .52f, floraMaterial, side < 0 ? 41 : 83);
            }
        }

        private void BuildGJ504bSet()
        {
            Vector2[] canyonSpires =
            {
                new(-25.5f, 19.0f), new(25.5f, -19.0f), new(-25.5f, -19.0f), new(25.5f, 19.0f),
                new(-16.0f, 20.8f), new(16.0f, -20.8f), new(-16.0f, -20.8f), new(16.0f, 20.8f),
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-21.5f, 12.8f), new(21.5f, -12.8f),
                new(0.0f, 22.5f), new(0.0f, -22.5f)
            };
            for (int i = 0; i < canyonSpires.Length; i++)
            {
                float s = .62f + (i % 3) * .10f;
                Spawn("USK_GJ504bCanyonHoodoo", spiralTreeMesh, i % 3 == 0 ? accentMaterial : floraMaterial, canyonSpires[i],
                    new Vector3(s * .84f, s * 1.15f, s * .84f), i * 47f, -.03f, true, (i % 2 == 0 ? 5f : -4f), 0f);
            }

            foreach (int side in new[] { -1, 1 })
            {
                Spawn("USK_GJ504bSurveyDome", domeMesh, structureMaterial, new Vector2(side * 27.5f, -side * 20.5f),
                    new Vector3(.54f, .48f, .54f), side * 38f, -.03f, true);
                Spawn("USK_GJ504bRadar", radarMesh, accentMaterial, new Vector2(side * 18.0f, side * 22.5f),
                    Vector3.one * .52f, side * 53f, -.02f, true);
                BuildPlantCluster(new Vector2(side * 24.5f, side * 18.5f), 7, .56f, accentMaterial, side < 0 ? 29 : 67);
                BuildPlantCluster(new Vector2(side * 15.2f, -side * 19.8f), 6, .52f, floraMaterial, side < 0 ? 43 : 89);
            }
        }

        private void BuildToi715bSet()
        {
            Vector2[] ivoryTrees =
            {
                new(-38.5f, 27.5f), new(-33.5f, 31.8f), new(38.5f, -27.5f), new(33.5f, -31.8f),
                new(-38.5f, -27.5f), new(-33.5f, -31.8f), new(38.5f, 27.5f), new(33.5f, 31.8f),
                // Midfield ivory groves
                new(-25.5f, 19.0f), new(25.5f, -19.0f), new(-25.5f, -19.0f), new(25.5f, 19.0f),
                new(-16.0f, 20.8f), new(16.0f, -20.8f), new(-16.0f, -20.8f), new(16.0f, 20.8f),
                new(-13.8f, 14.2f), new(13.8f, -14.2f), new(-13.8f, -14.2f), new(13.8f, 14.2f),
                new(-21.5f, 12.8f), new(21.5f, -12.8f), new(0.0f, 22.5f), new(0.0f, -22.5f)
            };
            for (int i = 0; i < ivoryTrees.Length; i++)
            {
                float s = .70f + (i % 4) * .09f;
                Spawn("USK_Toi715bIvoryTree", spiralTreeMesh, i % 3 == 0 ? floraMaterial : accentMaterial, ivoryTrees[i],
                    new Vector3(s, s * 1.10f, s), i * 41f, -.02f, true, (i % 2 == 0 ? 4f : -3f), 0f);
            }

            foreach (int side in new[] { -1, 1 })
            {
                Spawn("USK_Toi715bSurveyDome", domeMesh, structureMaterial, new Vector2(side * 27.5f, -side * 20.5f),
                    new Vector3(.54f, .48f, .54f), side * 34f, -.03f, true);
                Spawn("USK_Toi715bAntenna", antennaMesh, accentMaterial, new Vector2(side * 18.0f, side * 22.5f),
                    Vector3.one * .48f, side * 51f, -.02f, true);
                BuildPlantCluster(new Vector2(side * 24.5f, side * 18.5f), 8, .62f, floraMaterial, side < 0 ? 19 : 59);
                BuildPlantCluster(new Vector2(side * 24.5f, -side * 18.5f), 7, .58f, accentMaterial, side < 0 ? 31 : 73);
                BuildPlantCluster(new Vector2(side * 15.2f, side * 19.8f), 7, .56f, floraMaterial, side < 0 ? 43 : 89);
                BuildPlantCluster(new Vector2(side * 13.8f, -side * 14.2f), 6, .52f, accentMaterial, side < 0 ? 53 : 101);
            }
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
            // Protect the central combat lane and the central Hot Zone boss court.
            if (Mathf.Abs(site.y) < 8.2f && Mathf.Abs(site.x) < 35.5f) return null;
            if (Mathf.Abs(site.x) < 12.2f && Mathf.Abs(site.y) < 12.2f) return null;

            GameObject go = new(objectName) { isStatic = true };
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
            bool allowShadows = castShadow &&
                Alterverse.Performance.MobilePerformanceProfile.Current == Alterverse.Performance.MobileQuality.Performance;
            renderer.shadowCastingMode = allowShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            clearance.RemoveIfBlocked(go);
            return go;
        }

        private Material CreateMaterial(string materialName, Color color, float metallic, float smoothness)
        {
            Shader shader = ArenaVariantRuntime.IsExoplanet ? RuntimeShaderLibrary.Lit :
                            Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Universal Render Pipeline/Simple Lit") ??
                            Shader.Find("Standard");
            Material material = new(shader) { name = materialName, hideFlags = HideFlags.DontSave, enableInstancing = true };
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
            string[] names = { "USK_Rock_Large_1", "USK_GeodesicDome", "USK_Plant_2", "USK_Tree_Spiral_1",
                "USK_Roof_Radar", "USK_Roof_Antenna", "USK_SolarPanel_Ground" };
            string[] data = { RockLarge1Data, GeodesicDomeData, Plant2Data, TreeSpiral1Data,
                RadarData, AntennaData, SolarPanelData };
            for (int i = 0; i < data.Length; i++)
            {
                Mesh mesh = DecodeMesh(names[i], data[i]);
                try
                {
                    if (mesh.vertexCount == 0 || !float.IsFinite(mesh.bounds.size.sqrMagnitude))
                        throw new InvalidDataException("Invalid embedded map mesh: " + names[i]);
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }
#endif

        private Mesh DecodeOwnedMesh(string meshName, string encoded)
        {
            Mesh mesh = DecodeMesh(meshName, encoded);
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
            if (vertexCount == 0 || indexCount == 0 || indexCount % 3 != 0)
                throw new InvalidDataException("Invalid embedded map geometry: " + meshName);
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[indexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 vertex = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z))
                    throw new InvalidDataException("Non-finite embedded map vertex: " + meshName);
                vertices[i] = vertex;
            }
            for (int i = 0; i < indexCount; i++)
            {
                triangles[i] = reader.ReadUInt16();
                if (triangles[i] >= vertexCount)
                    throw new InvalidDataException("Embedded map index out of range: " + meshName);
            }
            // Read through the gzip footer before allocating a native Unity mesh.
            if (gzip.ReadByte() != -1)
                throw new InvalidDataException("Trailing embedded map geometry: " + meshName);

            Mesh mesh = new() { name = meshName, indexFormat = IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            if (meshName == "USK_Plant_2" || meshName == "USK_Tree_Spiral_1")
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
