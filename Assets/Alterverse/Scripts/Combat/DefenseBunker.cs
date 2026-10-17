using System.Collections.Generic;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>
    /// Permanent flank defense bunker for each team. It is intentionally not a Health owner:
    /// gameplay targeting therefore cannot attack or destroy it, while the structure collider
    /// still provides a solid bunker footprint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefenseBunker : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float attackRange = 15.5f;
        [SerializeField, Min(0f)] private float attackDamage = 5f;
        [SerializeField, Min(.2f)] private float attackCooldown = 1f;
        [SerializeField, Min(.05f)] private float scanInterval = .16f;

        private const float GameplayColliderWidth=6.45f;
        private const float GameplayColliderHeight=2.15f;
        private const float GameplayColliderDepth=5.15f;
        private const float GameplayColliderCornerRadius=1.35f;
        private static readonly Vector3 GameplayColliderCenter=new(0f,1.02f,-.05f);

        private static readonly List<DefenseBunker> active = new();
        private readonly List<Material> materials = new();
        private readonly List<Mesh> meshes = new();

        private Health currentTarget;
        private Transform visualRoot, turretHead, muzzle;
        private int team;
        private float nextAttack, nextScan;
        private Color energy;

        public static IReadOnlyList<DefenseBunker> Active => active;
        public int Team => team;
        public float AttackRange => attackRange;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public Health CurrentTarget => IsValidTarget(currentTarget) ? currentTarget : null;

        public static GameObject Spawn(int teamId, Vector3 position, string flank)
        {
            int clampedTeam = Mathf.Clamp(teamId, 0, 1);
            Vector3 surface = LunarSurfaceField.WorldSurface(position.x, position.z);
            position.y = surface.y;

            GameObject bunker = new GameObject($"DF_{(clampedTeam == 0 ? "Zero" : "Lumen")}_{flank}_DefenseBunker");
            bunker.layer = GameplayCollision.Structures;
            bunker.transform.position = position;
            bunker.transform.rotation = Quaternion.LookRotation(clampedTeam == 0 ? Vector3.right : Vector3.left, Vector3.up);

            GameObject world = GameObject.Find("DF_MatchWorld");
            if (world != null) bunker.transform.SetParent(world.transform, true);

            bunker.AddComponent<TeamMember>().Initialize(clampedTeam);

            DefenseBunker defense = bunker.AddComponent<DefenseBunker>();
            defense.Initialize(clampedTeam);
            defense.BuildGameplayCollider();
            defense.BuildVisual(flank == "North" ? 0 : 1);
            return bunker;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()=>active.Clear();
        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable(){active.Remove(this);}

        private void Initialize(int teamId)
        {
            team = Mathf.Clamp(teamId, 0, 1);
            energy = team == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            nextAttack = Time.time + .45f + Mathf.Abs(transform.position.z) * .007f;
            nextScan = 0f;
        }

        private void BuildGameplayCollider()
        {
            // Rounded horizontal corners keep the same bunker footprint but prevent capsule-driven
            // heroes and bots from pinning themselves against a sharp 90-degree corner.
            Mesh mesh=GameplayCollision.CreateRoundedPrismMesh(
                GameplayColliderCenter,GameplayColliderWidth,GameplayColliderHeight,
                GameplayColliderDepth,GameplayColliderCornerRadius);
            meshes.Add(mesh);
            MeshCollider solid=gameObject.AddComponent<MeshCollider>();
            solid.sharedMesh=mesh;
            solid.convex=true;
        }

        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if (Time.timeScale <= 0f) return;

            if (currentTarget != null && !IsValidTarget(currentTarget)) currentTarget = null;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + scanInterval;
                currentTarget = FindTarget();
            }

            Health target = CurrentTarget;
            if (target == null) return;

            AimAt(target.transform.position);
            if (Time.time < nextAttack) return;

            nextAttack = Time.time + attackCooldown;
            Fire(target);
        }

        private Health FindTarget()
        {
            Health best = null;
            float bestScore = float.MaxValue;
            float maxSqr = attackRange * attackRange;

            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health candidate = Health.Active[i];
                if (candidate == null || candidate.IsDead || !candidate.isActiveAndEnabled) continue;
                if (!IsEnemyUnit(candidate)) continue;

                Vector3 delta = candidate.transform.position - transform.position;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr > maxSqr) continue;

                // Keep a target briefly to avoid noisy turret snapping. Otherwise shoot the closest threat.
                float score = sqr * (candidate == currentTarget ? .68f : 1f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private bool IsValidTarget(Health candidate)
        {
            if (candidate == null || candidate.IsDead || !candidate.isActiveAndEnabled || !IsEnemyUnit(candidate)) return false;
            Vector3 delta = candidate.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= attackRange * attackRange * 1.08f;
        }

        private bool IsEnemyUnit(Health candidate)
        {
            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            if (minion != null) return minion.Team != team;

            TeamMember member = candidate.GetComponent<TeamMember>();
            if (member == null || member.Team == team) return false;

            // Cores/towers are not bunker targets; the bunker exists to deny enemy units entering the flank.
            if (candidate.GetComponent<RealityCore>() != null || candidate.GetComponent<LaneTower>() != null) return false;
            return true;
        }

        private void AimAt(Vector3 targetPosition)
        {
            if (turretHead == null) return;
            Vector3 direction = targetPosition - turretHead.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction, Vector3.up);
            turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, wanted, 190f * Time.deltaTime);
        }

        private void Fire(Health target)
        {
            if (target == null || target.IsDead) return;

            float before=target.Current+target.Shield;
            target.TakeDamage(attackDamage, gameObject);
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 2.65f;
            PrototypeVfx.ProjectileLine(origin - Vector3.up * .65f, target.transform.position, energy, .105f);
            PrototypeVfx.Burst(origin, Color.Lerp(energy, Color.white, .28f), .20f, 3);

            Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.TowerShot, origin, .34f);
            if(target.Current+target.Shield<before)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.TowerImpact, target.transform.position, .28f);
        }

        private void BuildVisual(int variant)
        {
            visualRoot = new GameObject("DF_DefenseBunkerVisual").transform;
            visualRoot.SetParent(transform, false);

            Color rawConcrete = new(.43f, .455f, .48f, 1f);
            Color lightConcrete = new(.55f, .565f, .58f, 1f);
            Color darkConcrete = new(.255f, .275f, .30f, 1f);
            Color metalColor = new(.105f, .125f, .145f, 1f);
            Color recessColor = new(.018f, .023f, .032f, 1f);
            Color dustColor = new(.18f, .19f, .205f, 1f);

            Material concrete = Mat(rawConcrete, .03f, .17f);
            Material concreteLight = Mat(lightConcrete, .025f, .16f);
            Material concreteDark = Mat(darkConcrete, .04f, .14f);
            Material metal = Mat(metalColor, .66f, .29f);
            Material recess = Mat(recessColor, .12f, .07f);
            Material glow = Mat(energy, .10f, .68f, true);
            Material dust = Mat(dustColor, .01f, .08f);

            // Buried foundation and heavy tapered concrete shell.
            Slab("DF_Bunker_RegolithSeat", visualRoot, new Vector3(0, -.16f, .08f), 9.2f, 7.7f, .34f, .74f, dust);
            Slab("DF_Bunker_Foundation", visualRoot, new Vector3(0, .03f, 0), 8.45f, 7.05f, .48f, .64f, concreteDark);
            TaperedSlab("DF_Bunker_MainShell", visualRoot, new Vector3(0, .34f, -.02f), 7.95f, 6.58f, 7.38f, 6.02f, 1.32f, .56f, concrete);
            Slab("DF_Bunker_UpperBand", visualRoot, new Vector3(0, 1.48f, -.02f), 7.76f, 6.34f, .27f, .58f, concreteDark);
            Slab("DF_Bunker_RoofArmor", visualRoot, new Vector3(0, 1.69f, -.08f), 8.62f, 7.18f, .48f, .72f, concreteLight);

            BuildFrontArmor(concrete, concreteDark, recess, glow);
            BuildRoofPlates(concrete, concreteLight, concreteDark, recess, variant);
            BuildVentBanks(metal, recess);
            BuildTurret(metal, concreteDark, recess, glow);
            BuildAntennae(metal, glow, variant);
            BuildFactionTrim(concreteDark, glow);
            BuildDamageAndDust(concreteDark, dust, variant);
            AuthoredBunkerDetails.Apply(visualRoot,turretHead,concrete,concreteLight,concreteDark,metal,recess);
        }

        private void BuildFrontArmor(Material concrete, Material darkConcrete, Material recess, Material glow)
        {
            // Deep protected firing aperture.
            Part("DF_Bunker_FrontRecess", PrimitiveType.Cube, visualRoot, new Vector3(0, .83f, 3.20f), new Vector3(2.22f, .62f, .18f), recess);
            Part("DF_Bunker_FrontLintel", PrimitiveType.Cube, visualRoot, new Vector3(0, 1.34f, 3.10f), new Vector3(2.88f, .38f, .48f), darkConcrete);
            Part("DF_Bunker_FrontEnergySight", PrimitiveType.Cube, visualRoot, new Vector3(0, .89f, 3.305f), new Vector3(.82f, .10f, .045f), glow, Quaternion.identity, false);

            // Three massive outward-sloped buttresses inspired by the reference bunker.
            float[] x = { -2.55f, 0f, 2.55f };
            for (int i = 0; i < x.Length; i++)
            {
                float yaw = i == 0 ? -5f : i == 2 ? 5f : 0f;
                Part($"DF_Bunker_FrontButtress_{i}", PrimitiveType.Cube, visualRoot,
                    new Vector3(x[i], .69f, 3.36f), new Vector3(1.20f, 1.72f, 1.62f), concrete,
                    Quaternion.Euler(-16f, yaw, 0f));
                Part($"DF_Bunker_ButtressFoot_{i}", PrimitiveType.Cube, visualRoot,
                    new Vector3(x[i] * 1.04f, .24f, 4.03f), new Vector3(1.46f, .48f, 1.18f), darkConcrete,
                    Quaternion.Euler(0f, yaw, 0f));
            }

            // Side cheek armor gives the front a bunker silhouette instead of a simple box.
            Part("DF_Bunker_LeftCheek", PrimitiveType.Cube, visualRoot, new Vector3(-3.58f, .91f, 2.55f),
                new Vector3(.72f, 1.25f, 1.66f), concrete, Quaternion.Euler(0f, -8f, -3f));
            Part("DF_Bunker_RightCheek", PrimitiveType.Cube, visualRoot, new Vector3(3.58f, .91f, 2.55f),
                new Vector3(.72f, 1.25f, 1.66f), concrete, Quaternion.Euler(0f, 8f, 3f));
        }

        private void BuildRoofPlates(Material concrete, Material light, Material dark, Material recess, int variant)
        {
            // Raised segmented roof armor makes the top readable from the MOBA camera.
            Vector3[] positions =
            {
                new(-2.86f,2.06f, 2.18f), new(0f,2.08f,2.34f), new(2.86f,2.06f,2.18f),
                new(-3.02f,2.05f,-2.20f), new(0f,2.07f,-2.42f), new(3.02f,2.05f,-2.20f),
                new(-3.23f,2.04f,0f), new(3.23f,2.04f,0f)
            };
            Vector3[] scales =
            {
                new(1.82f,.20f,1.48f),new(2.25f,.22f,1.18f),new(1.82f,.20f,1.48f),
                new(1.68f,.18f,1.30f),new(2.05f,.20f,1.05f),new(1.68f,.18f,1.30f),
                new(1.08f,.18f,1.58f),new(1.08f,.18f,1.58f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                Quaternion r = Quaternion.Euler(0, (i % 3 - 1) * (variant == 0 ? 1.8f : -1.6f), 0);
                Part($"DF_Bunker_RoofPlate_{i}", PrimitiveType.Cube, visualRoot, positions[i], scales[i], i % 3 == 1 ? light : concrete, r);
            }

            // Recessed panel seams and service hatches.
            Part("DF_Bunker_RoofSeamLong", PrimitiveType.Cube, visualRoot, new Vector3(0, 2.175f, .02f),
                new Vector3(.075f, .025f, 5.20f), recess);
            Part("DF_Bunker_RoofSeamCross", PrimitiveType.Cube, visualRoot, new Vector3(0, 2.176f, .10f),
                new Vector3(5.65f, .025f, .075f), recess);
            Part("DF_Bunker_ServiceHatch", PrimitiveType.Cube, visualRoot, new Vector3(-1.38f, 2.18f, -.18f),
                new Vector3(1.15f, .06f, .86f), dark, Quaternion.Euler(0, variant == 0 ? -5f : 6f, 0));
        }

        private void BuildVentBanks(Material metal, Material recess)
        {
            BuildVentBank(new Vector3(-2.48f, 2.20f, -.66f), 3, metal, recess);
            BuildVentBank(new Vector3(2.35f, 2.20f, -.74f), 4, metal, recess);
        }

        private void BuildVentBank(Vector3 center, int count, Material metal, Material recess)
        {
            float width = count == 3 ? 1.55f : 1.92f;
            Part("DF_Bunker_VentRecess", PrimitiveType.Cube, visualRoot, center + new Vector3(0, -.06f, 0),
                new Vector3(width, .10f, 1.18f), recess);

            for (int i = 0; i < count; i++)
            {
                float col = i % 2 == 0 ? -.34f : .34f;
                float row = count <= 2 ? 0f : (i / 2 == 0 ? -.27f : .27f);
                Vector3 p = center + new Vector3(col, .08f, row);
                Part("DF_Bunker_VentCollar", PrimitiveType.Cylinder, visualRoot, p,
                    new Vector3(.31f, .10f, .31f), metal);
                Part("DF_Bunker_VentDark", PrimitiveType.Cylinder, visualRoot, p + Vector3.up * .115f,
                    new Vector3(.205f, .035f, .205f), recess, Quaternion.identity, false);
            }
        }

        private void BuildTurret(Material metal, Material armor, Material recess, Material glow)
        {
            Mesh baseMesh = ProductionMeshFactory.Disc(.84f, .11f, 24);
            meshes.Add(baseMesh);
            MeshPart("DF_Bunker_TurretBase", visualRoot, baseMesh, armor, new Vector3(0, 2.20f, .22f));

            Mesh ringMesh = ProductionMeshFactory.Ring(.63f, .07f, 24, 7);
            meshes.Add(ringMesh);
            MeshPart("DF_Bunker_TurretEnergyRing", visualRoot, ringMesh, glow, new Vector3(0, 2.36f, .22f), Quaternion.identity, false);

            turretHead = new GameObject("DF_Bunker_TurretHead").transform;
            turretHead.SetParent(visualRoot, false);
            turretHead.localPosition = new Vector3(0, 2.38f, .22f);

            Part("DF_Bunker_TurretHousing", PrimitiveType.Cube, turretHead, new Vector3(0, .17f, .05f),
                new Vector3(1.18f, .38f, .78f), metal);
            Part("DF_Bunker_TurretFace", PrimitiveType.Cube, turretHead, new Vector3(0, .18f, .47f),
                new Vector3(.72f, .20f, .12f), recess);

            TubePart("DF_Bunker_BarrelL", turretHead, new Vector3(-.22f, .17f, .42f), new Vector3(-.22f, .17f, 1.08f), .065f, metal);
            TubePart("DF_Bunker_BarrelR", turretHead, new Vector3(.22f, .17f, .42f), new Vector3(.22f, .17f, 1.08f), .065f, metal);
            Part("DF_Bunker_TurretSensor", PrimitiveType.Sphere, turretHead, new Vector3(0, .22f, .52f),
                new Vector3(.17f, .17f, .12f), glow, Quaternion.identity, false);

            muzzle = new GameObject("DF_Bunker_Muzzle").transform;
            muzzle.SetParent(turretHead, false);
            muzzle.localPosition = new Vector3(0, .17f, 1.16f);
        }

        private void BuildAntennae(Material metal, Material glow, int variant)
        {
            // Thin communication rods echo the reference without copying its exact arrangement.
            TubePart("DF_Bunker_AntennaMain", visualRoot, new Vector3(.88f, 2.25f, -.55f),
                new Vector3(.92f + (variant == 0 ? .12f : -.08f), 4.10f, -.62f), .035f, metal);
            TubePart("DF_Bunker_AntennaShortA", visualRoot, new Vector3(-.54f, 2.25f, -.50f),
                new Vector3(-.66f, 3.30f, -.58f), .028f, metal);
            TubePart("DF_Bunker_AntennaShortB", visualRoot, new Vector3(.18f, 2.25f, -.74f),
                new Vector3(.30f, 3.03f, -.82f), .025f, metal);
            Part("DF_Bunker_AntennaBeacon", PrimitiveType.Sphere, visualRoot,
                new Vector3(.92f + (variant == 0 ? .12f : -.08f), 4.11f, -.62f),
                new Vector3(.105f, .105f, .105f), glow, Quaternion.identity, false);
        }

        private void BuildFactionTrim(Material dark, Material glow)
        {
            Part("DF_Bunker_FactionTrimL", PrimitiveType.Cube, visualRoot, new Vector3(-3.76f, 1.24f, .10f),
                new Vector3(.10f, .17f, 2.65f), glow, Quaternion.identity, false);
            Part("DF_Bunker_FactionTrimR", PrimitiveType.Cube, visualRoot, new Vector3(3.76f, 1.24f, .10f),
                new Vector3(.10f, .17f, 2.65f), glow, Quaternion.identity, false);
            Part("DF_Bunker_RearTechPanel", PrimitiveType.Cube, visualRoot, new Vector3(0, .92f, -3.08f),
                new Vector3(2.30f, .76f, .16f), dark);
            Part("DF_Bunker_RearStatus", PrimitiveType.Cube, visualRoot, new Vector3(0, 1.00f, -3.175f),
                new Vector3(1.24f, .13f, .035f), glow, Quaternion.identity, false);
        }

        private void BuildDamageAndDust(Material crack, Material dust, int variant)
        {
            // Controlled damage marks. They are geometry-thin so the large silhouette remains dominant.
            Part("DF_Bunker_RoofCrackA", PrimitiveType.Cube, visualRoot,
                new Vector3(variant == 0 ? -2.0f : 2.1f, 2.188f, 1.05f),
                new Vector3(.055f, .018f, 1.02f), crack, Quaternion.Euler(0, variant == 0 ? 28f : -31f, 0), false);
            Part("DF_Bunker_RoofCrackB", PrimitiveType.Cube, visualRoot,
                new Vector3(variant == 0 ? 2.82f : -2.72f, 2.187f, -1.70f),
                new Vector3(.045f, .018f, .76f), crack, Quaternion.Euler(0, variant == 0 ? -22f : 24f, 0), false);

            Vector3[] rubble =
            {
                new(-4.12f,.03f,2.65f),new(-3.78f,.04f,-3.28f),new(4.16f,.04f,2.32f),
                new(3.78f,.02f,-3.35f),new(-2.15f,.02f,3.86f),new(2.28f,.03f,-3.82f),
                new(-4.18f,.02f,.45f),new(4.22f,.025f,-.62f)
            };
            for (int i = 0; i < rubble.Length; i++)
            {
                Vector3 p = rubble[i];
                if (variant == 1) p.x *= -1f;
                Part($"DF_Bunker_RegolithChunk_{i}", PrimitiveType.Sphere, visualRoot, p,
                    new Vector3(.26f + (i % 3) * .08f, .15f + (i % 2) * .07f, .22f + (i % 4) * .05f),
                    dust, Quaternion.Euler(i * 11f, i * 47f, i * 7f));
            }
        }

        private void Slab(string name, Transform parent, Vector3 position, float width, float depth, float height, float bevel, Material material)
        {
            TaperedSlab(name, parent, position, width, depth, width, depth, height, bevel, material);
        }

        private void TaperedSlab(string name, Transform parent, Vector3 position,
            float bottomWidth, float bottomDepth, float topWidth, float topDepth, float height, float bevel, Material material)
        {
            Mesh mesh = CreateTaperedBeveledBox(bottomWidth, bottomDepth, topWidth, topDepth, height, bevel);
            meshes.Add(mesh);
            MeshPart(name, parent, mesh, material, position);
        }

        private void TubePart(string name, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            Mesh mesh = ProductionMeshFactory.Tube(from, to, radius, 8);
            if (mesh == null) return;
            meshes.Add(mesh);
            MeshPart(name, parent, mesh, material, Vector3.zero);
        }

        private void MeshPart(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition,
            Quaternion? localRotation = null, bool castShadows = true)
        {
            GameObject part = ProductionMeshFactory.MeshObject(name, parent, mesh, material);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = castShadows;
            }
        }

        private static void Part(string name, PrimitiveType type, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, Quaternion? localRotation = null, bool castShadows = true)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = castShadows;
            }
        }

        private Material Mat(Color color, float metallic, float smoothness, bool emissive = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { color = color };
            materials.Add(material);

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.15f);
            }
            return material;
        }

        private static Mesh CreateTaperedBeveledBox(float bottomWidth, float bottomDepth,
            float topWidth, float topDepth, float height, float bevel)
        {
            Vector3[] bottom = Ring(bottomWidth, bottomDepth, Mathf.Min(bevel, Mathf.Min(bottomWidth, bottomDepth) * .28f), 0f);
            Vector3[] top = Ring(topWidth, topDepth, Mathf.Min(bevel, Mathf.Min(topWidth, topDepth) * .28f), height);

            var vertices = new List<Vector3>(50);
            var triangles = new List<int>(96);

            // Bottom and top faces use independent vertices so their normals remain hard.
            int bottomCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            for (int i = 0; i < 8; i++) vertices.Add(bottom[i]);

            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0, height, 0));
            for (int i = 0; i < 8; i++) vertices.Add(top[i]);

            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                triangles.Add(bottomCenter); triangles.Add(1 + i); triangles.Add(1 + j);
                triangles.Add(topCenter); triangles.Add(topCenter + 1 + j); triangles.Add(topCenter + 1 + i);
            }

            // Each side quad owns its vertices, preserving the concrete panel facets.
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                int q = vertices.Count;
                vertices.Add(bottom[i]);
                vertices.Add(top[i]);
                vertices.Add(top[j]);
                vertices.Add(bottom[j]);
                triangles.Add(q); triangles.Add(q + 1); triangles.Add(q + 2);
                triangles.Add(q); triangles.Add(q + 2); triangles.Add(q + 3);
            }

            Mesh mesh = new Mesh { name = "DF_DefenseBunkerBeveledSlab" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] Ring(float width, float depth, float bevel, float y)
        {
            float x = width * .5f, z = depth * .5f;
            return new[]
            {
                new Vector3( x - bevel, y,  z),
                new Vector3(-x + bevel, y,  z),
                new Vector3(-x, y,  z - bevel),
                new Vector3(-x, y, -z + bevel),
                new Vector3(-x + bevel, y, -z),
                new Vector3( x - bevel, y, -z),
                new Vector3( x, y, -z + bevel),
                new Vector3( x, y,  z - bevel)
            };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = team == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < materials.Count; i++) if (materials[i] != null) Destroy(materials[i]);
            for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Destroy(meshes[i]);
        }
    }
}


