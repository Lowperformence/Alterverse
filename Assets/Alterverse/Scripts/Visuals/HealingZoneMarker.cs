using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.Visuals
{
    public sealed class HealingZoneMarker : MonoBehaviour
    {
        private const int MoteCount = 12;

        private Transform root, plus, auraRing, auraDisc;
        private readonly Transform[] motes = new Transform[MoteCount];
        private Material energy, metal, trim, auraMat, moteMat;
        private ParticleSystem healParticles;
        private SpawnHealingZone zone;
        private float occupiedBlend;

        private void Start()
        {
            zone = GetComponent<SpawnHealingZone>() ?? GetComponentInParent<SpawnHealingZone>();
            Build();
        }

        private void Build()
        {
            root = new GameObject("DF_SpawnHealStationProduction").transform;
            root.SetParent(transform, false);
            root.localScale = new Vector3(ArenaLayout.HealingPlateScale, 1f, ArenaLayout.HealingPlateScale);

            Color baseGreen = new Color(.02f, 1f, .16f);
            energy = Mat(baseGreen, .08f, .68f, true);
            metal = Mat(new Color(.055f, .075f, .10f), .78f, .30f);
            trim = Mat(new Color(.19f, .24f, .29f), .62f, .46f);
            auraMat = Mat(new Color(.12f, 1f, .32f), .04f, .82f, true);
            moteMat = Mat(new Color(.28f, 1f, .44f), .02f, .90f, true);

            StructureSurfaceMaterials.Apply(metal, StructureFinish.Deck);
            StructureSurfaceMaterials.Apply(trim, StructureFinish.Metal);

            Disc("HealBase", 2.15f, .025f, .025f, metal);
            Disc("HealDeck", 1.78f, .065f, .015f, trim);

            GameObject halo = ProductionMeshFactory.MeshObject("DF_HealEnergyRing", root, ProductionMeshFactory.Ring(1.42f, .050f, 32, 8), energy);
            halo.transform.localPosition = new Vector3(0, .185f, 0);

            // Active occupied outer ring and ground glow disc
            GameObject activeRingObj = ProductionMeshFactory.MeshObject("DF_HealOccupiedRing", root, ProductionMeshFactory.Ring(1.74f, .042f, 32, 8), auraMat);
            auraRing = activeRingObj.transform;
            auraRing.localPosition = new Vector3(0, .195f, 0);
            auraRing.localScale = Vector3.zero;

            GameObject activeDiscObj = ProductionMeshFactory.MeshObject("DF_HealOccupiedAuraDisc", root, ProductionMeshFactory.Disc(1.58f, .006f, 32), auraMat);
            auraDisc = activeDiscObj.transform;
            auraDisc.localPosition = new Vector3(0, .092f, 0);
            auraDisc.localScale = Vector3.zero;

            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 outer = new(Mathf.Cos(a) * 1.82f, .145f, Mathf.Sin(a) * 1.82f),
                        inner = new(Mathf.Cos(a) * 1.02f, .205f, Mathf.Sin(a) * 1.02f);
                Tube("HealFeed", outer, inner, .026f, i % 2 == 0 ? energy : trim);
            }

            plus = new GameObject("DF_SpawnHealingPlus").transform;
            plus.SetParent(root, false);
            plus.localPosition = new Vector3(0, .92f, 0);
            BoxOn(plus, "PlusVertical", new Vector3(.16f, .44f, .10f), energy);
            BoxOn(plus, "PlusHorizontal", new Vector3(.44f, .16f, .10f), energy);

            // Rising 3D luminous green healing motes/crosses
            for (int i = 0; i < MoteCount; i++)
            {
                Transform mote = new GameObject("DF_HealRisingMote_" + i).transform;
                mote.SetParent(root, false);
                mote.localScale = Vector3.zero;
                BoxOn(mote, "MoteV", new Vector3(.045f, .14f, .045f), moteMat);
                BoxOn(mote, "MoteH", new Vector3(.14f, .045f, .045f), moteMat);
                motes[i] = mote;
            }

            BuildRisingParticleStream();
        }

        private void BuildRisingParticleStream()
        {
            GameObject psObj = new GameObject("DF_HealRisingParticles");
            psObj.transform.SetParent(root, false);
            psObj.transform.localPosition = new Vector3(0f, .16f, 0f);
            psObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            healParticles = psObj.AddComponent<ParticleSystem>();
            var main = healParticles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.85f, 1.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.15f, 2.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(.06f, .13f);
            main.startColor = new Color(.25f, 1f, .42f, .92f);
            main.maxParticles = 36;

            var emission = healParticles.emission;
            emission.rateOverTime = 0f;

            var shape = healParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.48f;

            var sizeOverLifetime = healParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, .25f),
                new Keyframe(.35f, 1f),
                new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.sharedMaterial = moteMat;
                psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                psRenderer.receiveShadows = false;
            }
        }

        private bool CheckSomeoneStandingOnPad()
        {
            if (zone == null)
                zone = GetComponent<SpawnHealingZone>() ?? GetComponentInParent<SpawnHealingZone>();
            if (zone != null && zone.IsOccupied)
                return true;

            var active = Health.Active;
            Vector3 center = transform.position;
            for (int i = 0; i < active.Count; i++)
            {
                Health h = active[i];
                if (h == null || h.IsDead) continue;
                if (zone != null)
                {
                    TeamMember tm = h.GetComponent<TeamMember>();
                    if (tm != null && tm.Team != zone.Team) continue;
                }
                if (ArenaLayout.IsInsideHealingArea(h.transform.position, center))
                    return true;
            }
            return false;
        }

        private void Update()
        {
            if (plus == null) return;
            float t = Time.time;
            float dt = Time.deltaTime;
            bool occupied = CheckSomeoneStandingOnPad();
            occupiedBlend = Mathf.MoveTowards(occupiedBlend, occupied ? 1f : 0f, dt * 4.8f);

            float p = 1f + Mathf.Sin(t * (2.7f + occupiedBlend * 2.8f)) * (.040f + occupiedBlend * .045f);
            plus.localScale = Vector3.one * ((3f + occupiedBlend * .42f) * p);
            plus.localPosition = new Vector3(0, .92f + occupiedBlend * .14f + Mathf.Sin(t * (1.8f + occupiedBlend * 2.2f)) * (.012f + occupiedBlend * .035f), 0);
            plus.Rotate(Vector3.up, (9f + occupiedBlend * 48f) * dt, Space.Self);

            if (energy != null)
            {
                Color baseC = Color.Lerp(new Color(.02f, 1f, .16f), new Color(.24f, 1f, .42f), occupiedBlend);
                float pulse = 1.8f + occupiedBlend * (2.45f + Mathf.Sin(t * 6.2f) * .65f);
                SetEmission(energy, baseC, baseC * pulse);
            }

            if (auraRing != null && auraDisc != null)
            {
                if (occupiedBlend > .001f)
                {
                    float ringPulse = 1f + Mathf.Sin(t * 5.2f) * .05f;
                    auraRing.localScale = new Vector3(ringPulse, 1f, ringPulse) * occupiedBlend;
                    float discScale = (.82f + Mathf.Sin(t * 4.4f) * .06f) * occupiedBlend;
                    auraDisc.localScale = new Vector3(discScale, 1f, discScale);
                    Color auraColor = new Color(.18f, 1f, .38f);
                    SetEmission(auraMat, auraColor, auraColor * (3.2f * occupiedBlend));
                }
                else
                {
                    auraRing.localScale = Vector3.zero;
                    auraDisc.localScale = Vector3.zero;
                }
            }

            if (moteMat != null && occupiedBlend > .001f)
            {
                Color moteColor = new Color(.35f, 1f, .48f);
                SetEmission(moteMat, moteColor, moteColor * (3.6f * occupiedBlend));
            }

            // Animate 12 rising green healing motes around the pad when occupied
            for (int i = 0; i < MoteCount; i++)
            {
                Transform mote = motes[i];
                if (mote == null) continue;
                if (occupiedBlend <= .005f)
                {
                    mote.localScale = Vector3.zero;
                    continue;
                }
                float phase = Mathf.Repeat(t * (.68f + (i % 3) * .14f) + i * (1f / MoteCount), 1f);
                float angle = i * (Mathf.PI * 2f / MoteCount) + t * .85f + phase * .65f;
                float radius = Mathf.Lerp(.48f, 1.48f, (i % 4) / 3f) * (1f - phase * .18f);
                float y = Mathf.Lerp(.16f, 2.05f, phase);
                mote.localPosition = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                mote.localRotation = Quaternion.Euler(0f, (t * 95f + i * 30f) % 360f, 0f);
                float envelope = Mathf.Sin(phase * Mathf.PI);
                mote.localScale = Vector3.one * (envelope * occupiedBlend * .95f);
            }

            if (healParticles != null)
            {
                var emission = healParticles.emission;
                emission.rateOverTime = occupiedBlend * 22f;
                if (occupied && !healParticles.isPlaying) healParticles.Play();
                else if (!occupied && occupiedBlend <= .01f && healParticles.isPlaying) healParticles.Stop();
            }
        }

        private static void SetEmission(Material m, Color baseCol, Color hdrEmission)
        {
            if (m == null) return;
            m.color = baseCol;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", hdrEmission);
        }

        private void Disc(string n, float radius, float y, float halfHeight, Material mat)
        {
            GameObject g = ProductionMeshFactory.MeshObject("DF_" + n, root, ProductionMeshFactory.Disc(radius, halfHeight, 32), mat);
            g.transform.localPosition = new Vector3(0, y, 0);
        }

        private void Tube(string n, Vector3 a, Vector3 b, float radius, Material mat)
        {
            ProductionMeshFactory.MeshObject("DF_" + n, root, ProductionMeshFactory.Tube(a, b, radius, 8), mat);
        }

        private void BoxOn(Transform parent, string n, Vector3 scale, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "DF_" + n;
            g.transform.SetParent(parent, false);
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Collider c = g.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private Material Mat(Color c, float metallic, float smooth, bool emissive = false)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new(sh);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.8f);
            }
            return m;
        }
    }
}
