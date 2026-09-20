using System.Collections.Generic;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>
    /// Indestructible close-range defensive trench. It deliberately owns no Health component,
    /// so normal combat targeting cannot select, damage or destroy it. Hostile units that
    /// enter the immediate perimeter take steady area damage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefenseTrench : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damagePerSecond = 5f;
        [SerializeField, Min(1f)] private float damageRadius = 5.25f;
        [SerializeField, Min(.05f)] private float damageTick = .25f;

        private static readonly List<DefenseTrench> active = new();
        private readonly List<Material> materials = new();
        private readonly List<Mesh> meshes = new();

        private Transform visualRoot;
        private int team;
        private float nextDamageTick;
        private Color teamEnergy;
        private Color hazardEnergy;

        public static IReadOnlyList<DefenseTrench> Active => active;
        public int Team => team;
        public float DamagePerSecond => damagePerSecond;
        public float DamageRadius => damageRadius;
        public float DamageTick => damageTick;

        public static GameObject Spawn(int teamId, Vector3 position, string flank)
        {
            int clampedTeam = Mathf.Clamp(teamId, 0, 1);
            Vector3 surface = LunarSurfaceField.Surface(position.x, position.z);
            position.y = surface.y;

            GameObject trench = new GameObject($"DF_{(clampedTeam == 0 ? "Zero" : "Lumen")}_{flank}_DefenseTrench");
            trench.layer = GameplayCollision.Structures;
            trench.transform.position = position;
            // The authored trench opens along local +Z. Keep that open side toward the owning
            // base by rotating the previous arena-facing orientation 180 degrees.
            Quaternion arenaFacing = Quaternion.LookRotation(clampedTeam == 0 ? Vector3.right : Vector3.left, Vector3.up);
            trench.transform.rotation = arenaFacing * Quaternion.Euler(0f, 180f, 0f);

            GameObject world = GameObject.Find("DF_MatchWorld");
            if (world != null) trench.transform.SetParent(world.transform, true);

            // A single stable gameplay footprint avoids decorative collider noise and bot snagging.
            BoxCollider solid = trench.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, .63f, -.12f);
            solid.size = new Vector3(7.25f, 1.35f, 5.10f);

            trench.AddComponent<TeamMember>().Initialize(clampedTeam);

            DefenseTrench defense = trench.AddComponent<DefenseTrench>();
            defense.Initialize(clampedTeam);
            defense.BuildVisual(clampedTeam == 0 ? 0 : 1);
            return trench;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()=>active.Clear();
        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable(){active.Remove(this);}

        private void Initialize(int teamId)
        {
            team = Mathf.Clamp(teamId, 0, 1);
            teamEnergy = team == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            hazardEnergy = new Color(.96f, .26f, .055f, 1f);
            nextDamageTick = Time.time + damageTick;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;

            if (Time.time < nextDamageTick) return;

            float elapsed = damageTick;
            nextDamageTick = Time.time + damageTick;
            ApplyAreaDamage(damagePerSecond * elapsed);
        }

        private void ApplyAreaDamage(float amount)
        {
            if (amount <= 0f) return;

            float maxSqr = damageRadius * damageRadius;
            bool hitAny = false;

            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health candidate = Health.Active[i];
                if (candidate == null || candidate.IsDead || !candidate.isActiveAndEnabled) continue;
                if (!IsEnemyUnit(candidate)) continue;

                Vector3 delta = candidate.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > maxSqr) continue;

                candidate.TakeDamage(amount, gameObject);

                // Make the trench attack readable in combat. Damage stays exactly the same;
                // this is presentation only: a short, bright team-energy laser traces each hit.
                Vector3 beamOrigin = transform.position + Vector3.up * 1.15f;
                Vector3 beamTarget = candidate.transform.position + Vector3.up * .45f;
                PrototypeVfx.ProjectileLine(beamOrigin, beamTarget, teamEnergy, .12f);
                PrototypeVfx.Burst(beamTarget, Color.Lerp(teamEnergy, Color.white, .25f), .12f, 2);
                hitAny = true;
            }

            if (hitAny && Random.value < .42f)
            {
                PrototypeVfx.Burst(transform.position + Vector3.up * .42f, hazardEnergy, .11f, 2);
            }
        }

        private bool IsEnemyUnit(Health candidate)
        {
            // Defensive trenches punish combat units, not enemy objectives/buildings.
            if (candidate.GetComponent<RealityCore>() != null || candidate.GetComponent<LaneTower>() != null) return false;

            TeamMember member = candidate.GetComponent<TeamMember>();
            if (member != null) return member.Team != team;

            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            return minion != null && minion.Team != team;
        }

        private void BuildVisual(int variant)
        {
            visualRoot = new GameObject("DF_DefenseTrenchVisual").transform;
            visualRoot.SetParent(transform, false);

            Color regolith = new(.19f, .18f, .17f, 1f);
            Color deepRegolith = new(.075f, .072f, .070f, 1f);
            Color concrete = new(.34f, .35f, .36f, 1f);
            Color concreteLight = new(.46f, .47f, .48f, 1f);
            Color metalColor = new(.095f, .11f, .12f, 1f);
            Color recessColor = new(.016f, .020f, .024f, 1f);
            Color sandbagColor = new(.31f, .285f, .245f, 1f);

            Material soil = Mat(regolith, .01f, .08f);
            Material deep = Mat(deepRegolith, .01f, .05f);
            Material armor = Mat(concrete, .07f, .18f);
            Material armorLight = Mat(concreteLight, .06f, .17f);
            Material metal = Mat(metalColor, .63f, .28f);
            Material recess = Mat(recessColor, .10f, .05f);
            Material sandbag = Mat(sandbagColor, .01f, .13f);
            Material teamGlow = Mat(teamEnergy, .08f, .62f, true);

            BuildExcavation(soil, deep);
            BuildArmoredWalls(armor, armorLight, metal, recess, variant);
            BuildSandbags(sandbag, variant);
            BuildRearShelter(armor, armorLight, metal, recess, teamGlow, variant);
            BuildFieldDetails(soil, metal, recess, sandbag, variant);
            BuildFactionDetails(metal, teamGlow);
        }

        private void BuildExcavation(Material soil, Material deep)
        {
            // The lunar ground is not cut at runtime, so the trench is sold with a dark recessed floor
            // and raised regolith shoulders that read as an excavation from the gameplay camera.
            Part("DF_Trench_ShadowCut", PrimitiveType.Cube, visualRoot, new Vector3(0f, .055f, .10f),
                new Vector3(5.05f, .10f, 3.50f), deep, Quaternion.identity, false);
            Part("DF_Trench_Floor", PrimitiveType.Cube, visualRoot, new Vector3(0f, .12f, .10f),
                new Vector3(4.45f, .08f, 3.05f), soil, Quaternion.identity, false);

            Part("DF_Trench_LeftEarthBank", PrimitiveType.Cube, visualRoot, new Vector3(-3.30f, .32f, -.06f),
                new Vector3(1.55f, .62f, 5.35f), soil, Quaternion.Euler(0f, 0f, -6f));
            Part("DF_Trench_RightEarthBank", PrimitiveType.Cube, visualRoot, new Vector3(3.30f, .32f, -.06f),
                new Vector3(1.55f, .62f, 5.35f), soil, Quaternion.Euler(0f, 0f, 6f));
            Part("DF_Trench_RearEarthBank", PrimitiveType.Cube, visualRoot, new Vector3(0f, .30f, -2.53f),
                new Vector3(6.15f, .58f, 1.42f), soil, Quaternion.Euler(-4f, 0f, 0f));
        }

        private void BuildArmoredWalls(Material armor, Material armorLight, Material metal, Material recess, int variant)
        {
            // Segmented side revetments keep the silhouette irregular and reference the layered field fortification.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float z = -1.45f + i * 1.48f;
                    float yaw = side * ((i - 1) * 1.8f + (variant == 0 ? .8f : -.8f));
                    Part($"DF_Trench_Wall_{side}_{i}", PrimitiveType.Cube, visualRoot,
                        new Vector3(side * 2.63f, .70f, z),
                        new Vector3(.54f, 1.18f, 1.34f), i == 1 ? armorLight : armor,
                        Quaternion.Euler(0f, yaw, side * -2.5f));

                    Part($"DF_Trench_WallInset_{side}_{i}", PrimitiveType.Cube, visualRoot,
                        new Vector3(side * 2.335f, .72f, z),
                        new Vector3(.075f, .46f, .70f), recess, Quaternion.identity, false);

                    Part($"DF_Trench_WallBrace_{side}_{i}", PrimitiveType.Cube, visualRoot,
                        new Vector3(side * 2.92f, .72f, z + .50f),
                        new Vector3(.16f, 1.20f, .16f), metal,
                        Quaternion.Euler(0f, 0f, side * 8f));
                }
            }

            // Rear retaining wall and protected central niche.
            for (int i = -2; i <= 2; i++)
            {
                Part($"DF_Trench_RearPanel_{i}", PrimitiveType.Cube, visualRoot,
                    new Vector3(i * 1.08f, .72f, -2.18f),
                    new Vector3(.98f, 1.20f, .48f), i == 0 ? armorLight : armor,
                    Quaternion.Euler(i % 2 == 0 ? 0f : 1.5f, 0f, i * .8f));
            }

            Part("DF_Trench_RearRecess", PrimitiveType.Cube, visualRoot, new Vector3(0f, .67f, -1.905f),
                new Vector3(1.62f, .54f, .09f), recess, Quaternion.identity, false);
            Part("DF_Trench_RearBraceL", PrimitiveType.Cube, visualRoot, new Vector3(-2.50f, .73f, -2.35f),
                new Vector3(.18f, 1.28f, .74f), metal, Quaternion.Euler(0f, 0f, -8f));
            Part("DF_Trench_RearBraceR", PrimitiveType.Cube, visualRoot, new Vector3(2.50f, .73f, -2.35f),
                new Vector3(.18f, 1.28f, .74f), metal, Quaternion.Euler(0f, 0f, 8f));

            // Front cheek armor leaves a central open-looking firing throat.
            Part("DF_Trench_FrontCheekL", PrimitiveType.Cube, visualRoot, new Vector3(-2.02f, .58f, 2.20f),
                new Vector3(1.70f, .92f, .55f), armor, Quaternion.Euler(0f, -7f, -2f));
            Part("DF_Trench_FrontCheekR", PrimitiveType.Cube, visualRoot, new Vector3(2.02f, .58f, 2.20f),
                new Vector3(1.70f, .92f, .55f), armor, Quaternion.Euler(0f, 7f, 2f));
        }

        private void BuildSandbags(Material sandbag, int variant)
        {
            int id = 0;

            // Side parapets.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 2; row++)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float z = -1.75f + i * .88f + row * .09f;
                        float y = 1.30f + row * .31f;
                        float x = side * (2.74f - row * .04f);
                        float angle = ((i + row + variant) % 3 - 1) * 3.5f;
                        Sandbag($"DF_Trench_Sandbag_{id++}", new Vector3(x, y, z), true,
                            Quaternion.Euler(90f, angle, side * 2f), sandbag);
                    }
                }
            }

            // Rear parapet.
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 6; i++)
                {
                    float x = -2.18f + i * .87f + (row == 1 ? .08f : 0f);
                    float y = 1.31f + row * .31f;
                    float roll = ((i + variant) % 2 == 0 ? -2.5f : 2.5f);
                    Sandbag($"DF_Trench_Sandbag_{id++}", new Vector3(x, y, -2.31f), false,
                        Quaternion.Euler(0f, 0f, 90f + roll), sandbag);
                }
            }

            // Front shoulder stacks preserve an open center and give the trench its field-built character.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = side * (1.58f + i * .45f);
                    float y = .98f + (i % 2) * .22f;
                    float z = 2.42f + (i % 2) * .10f;
                    Sandbag($"DF_Trench_Sandbag_{id++}", new Vector3(x, y, z), false,
                        Quaternion.Euler(0f, side * (5f + i * 2f), 90f), sandbag);
                }
            }
        }

        private void BuildRearShelter(Material armor, Material light, Material metal, Material recess, Material teamGlow, int variant)
        {
            // Compact overhead shelter inspired by the reference image, kept low for MOBA readability.
            Part("DF_Trench_ShelterPostL", PrimitiveType.Cube, visualRoot, new Vector3(-1.28f, 1.58f, -1.52f),
                new Vector3(.20f, 1.45f, .20f), metal, Quaternion.Euler(0f, 0f, -2f));
            Part("DF_Trench_ShelterPostR", PrimitiveType.Cube, visualRoot, new Vector3(1.28f, 1.58f, -1.52f),
                new Vector3(.20f, 1.45f, .20f), metal, Quaternion.Euler(0f, 0f, 2f));
            Part("DF_Trench_ShelterRoof", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.19f, -1.55f),
                new Vector3(3.25f, .28f, 2.14f), light,
                Quaternion.Euler(variant == 0 ? -1.5f : 1.5f, 0f, variant == 0 ? -1.2f : 1.2f));
            Part("DF_Trench_ShelterUnder", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.02f, -1.55f),
                new Vector3(2.80f, .08f, 1.70f), recess, Quaternion.identity, false);

            Part("DF_Trench_TechBox", PrimitiveType.Cube, visualRoot, new Vector3(.72f, .68f, -1.62f),
                new Vector3(.82f, .88f, .42f), metal);
            Part("DF_Trench_TechScreen", PrimitiveType.Cube, visualRoot, new Vector3(.72f, .75f, -1.395f),
                new Vector3(.48f, .22f, .035f), teamGlow, Quaternion.identity, false);

            Part("DF_Trench_AmmoCrateA", PrimitiveType.Cube, visualRoot, new Vector3(-.92f, .38f, -1.42f),
                new Vector3(.86f, .52f, .60f), armor, Quaternion.Euler(0f, -7f, 0f));
            Part("DF_Trench_AmmoCrateB", PrimitiveType.Cube, visualRoot, new Vector3(-1.35f, .31f, -.84f),
                new Vector3(.62f, .40f, .52f), armor, Quaternion.Euler(0f, 9f, 0f));
        }

        private void BuildFieldDetails(Material soil, Material metal, Material recess, Material sandbag, int variant)
        {
            // Duckboards / floor ribs.
            for (int i = 0; i < 7; i++)
            {
                float z = -1.55f + i * .48f;
                Part($"DF_Trench_Duckboard_{i}", PrimitiveType.Cube, visualRoot, new Vector3(0f, .20f, z),
                    new Vector3(3.54f, .07f, .22f), metal, Quaternion.Euler(0f, (i % 2 == 0 ? -1f : 1f) * 1.8f, 0f));
            }

            // Cable / pipe runs on both walls.
            Tube("DF_Trench_CableL", new Vector3(-2.29f, .48f, -1.70f), new Vector3(-2.29f, .48f, 1.64f), .032f, recess);
            Tube("DF_Trench_CableR", new Vector3(2.29f, .58f, -1.45f), new Vector3(2.29f, .58f, 1.40f), .032f, recess);

            // Antenna mast and small support wire.
            Tube("DF_Trench_Antenna", new Vector3(1.68f, 2.02f, -1.86f),
                new Vector3(1.78f + (variant == 0 ? .10f : -.08f), 3.35f, -1.94f), .028f, metal);
            Tube("DF_Trench_AntennaBrace", new Vector3(1.70f, 2.30f, -1.88f),
                new Vector3(2.22f, 1.52f, -2.05f), .018f, metal);

            // Small irregular regolith chunks around the berm base.
            Vector3[] rubble =
            {
                new(-3.70f,.12f,1.82f), new(3.76f,.10f,1.55f), new(-3.62f,.11f,-2.30f),
                new(3.68f,.09f,-2.18f), new(-2.95f,.10f,2.65f), new(2.96f,.11f,2.72f)
            };
            for (int i = 0; i < rubble.Length; i++)
            {
                Vector3 p = rubble[i];
                if (variant == 1) p.z *= -1f;
                Part($"DF_Trench_Rubble_{i}", PrimitiveType.Sphere, visualRoot, p,
                    new Vector3(.28f + (i % 2) * .10f, .16f + (i % 3) * .04f, .24f + (i % 2) * .07f),
                    soil, Quaternion.Euler(i * 13f, i * 41f, i * 9f));
            }

            // A pair of loose field bags break up the interior.
            Sandbag("DF_Trench_LooseBagA", new Vector3(1.46f, .36f, -.60f), false,
                Quaternion.Euler(0f, 16f, 90f), sandbag);
            Sandbag("DF_Trench_LooseBagB", new Vector3(1.72f, .34f, -.18f), false,
                Quaternion.Euler(0f, -12f, 90f), sandbag);
        }

        private void BuildFactionDetails(Material metal, Material teamGlow)
        {
            // No visible damage-radius ring. The trench remains a close-range hazard, but its
            // presentation stays environmental rather than drawing a bright gameplay circle.
            Vector3[] pylons =
            {
                new(-3.70f,.58f, 2.45f), new(3.70f,.58f,2.45f),
                new(-3.70f,.58f,-2.45f), new(3.70f,.58f,-2.45f)
            };

            for (int i = 0; i < pylons.Length; i++)
            {
                Vector3 p = pylons[i];
                Part($"DF_Trench_FieldPost_{i}", PrimitiveType.Cylinder, visualRoot, p,
                    new Vector3(.11f, .58f, .11f), metal);
                Part($"DF_Trench_TeamLamp_{i}", PrimitiveType.Sphere, visualRoot, p + Vector3.up * .67f,
                    new Vector3(.15f, .15f, .15f), teamGlow, Quaternion.identity, false);
            }

            Part("DF_Trench_FactionMarkL", PrimitiveType.Cube, visualRoot, new Vector3(-2.83f, 1.00f, -.12f),
                new Vector3(.035f, .14f, 1.20f), teamGlow, Quaternion.identity, false);
            Part("DF_Trench_FactionMarkR", PrimitiveType.Cube, visualRoot, new Vector3(2.83f, 1.00f, -.12f),
                new Vector3(.035f, .14f, 1.20f), teamGlow, Quaternion.identity, false);
        }

        private void Sandbag(string name, Vector3 position, bool sideRun, Quaternion rotation, Material material)
        {
            GameObject bag = Part(name, PrimitiveType.Capsule, visualRoot, position,
                sideRun ? new Vector3(.38f, .58f, .38f) : new Vector3(.38f, .58f, .38f),
                material, rotation);
            // Slight squash keeps the primitive from reading as a pill from the top-down camera.
            bag.transform.localScale = sideRun ? new Vector3(.42f, .58f, .34f) : new Vector3(.36f, .58f, .42f);
        }

        private void Tube(string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            Mesh mesh = ProductionMeshFactory.Tube(from, to, radius, 8);
            if (mesh == null) return;
            meshes.Add(mesh);
            GameObject part = ProductionMeshFactory.MeshObject(name, visualRoot, mesh, material);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 localPosition,
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

            return part;
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
                material.SetColor("_EmissionColor", color * 1.75f);
            }

            return material;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.96f, .26f, .055f, 1f);
            Gizmos.DrawWireSphere(transform.position, damageRadius);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) Destroy(materials[i]);

            for (int i = 0; i < meshes.Count; i++)
                if (meshes[i] != null) Destroy(meshes[i]);
        }
    }
}
