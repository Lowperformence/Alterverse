using System.Collections.Generic;
using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.Visuals
{
    public sealed class HealingZoneMarker : MonoBehaviour
    {
        private const int MoteCount = 12;

        private Transform root, plus, auraRing, auraDisc;
        private readonly Transform[] motes = new Transform[MoteCount];
        private readonly List<Mesh> ownedMeshes = new();
        private Material energy, metal, trim, deck, auraMat, moteMat;
        private ParticleSystem healParticles;
        private SpawnHealingZone zone;
        private float occupiedBlend;

        private void Start()
        {
            zone = GetComponent<SpawnHealingZone>() ?? GetComponentInParent<SpawnHealingZone>();
            Build();
        }

        private void OnDestroy()
        {
            if (energy != null) Destroy(energy);
            if (metal != null) Destroy(metal);
            if (trim != null) Destroy(trim);
            if (deck != null) Destroy(deck);
            if (auraMat != null) Destroy(auraMat);
            if (moteMat != null) Destroy(moteMat);
            foreach (Mesh mesh in ownedMeshes)
                if (mesh != null) Destroy(mesh);
            ownedMeshes.Clear();
        }

        private void Build()
        {
            root = new GameObject("DF_SpawnHealStationProduction").transform;
            root.SetParent(transform, false);
            root.localScale = new Vector3(ArenaLayout.HealingPlateScale, 1f, ArenaLayout.HealingPlateScale);

            energy = HealingVisualPalette.CreateGreen(HealingVisualPalette.Energy);
            metal = Mat(new Color(.055f, .07f, .055f), .78f, .30f);
            trim = Mat(new Color(.16f, .21f, .15f), .62f, .46f);
            deck = HealingVisualPalette.CreateGreen(HealingVisualPalette.Deck);
            auraMat = HealingVisualPalette.CreateGreen(HealingVisualPalette.Aura);
            moteMat = HealingVisualPalette.CreateGreen(HealingVisualPalette.Mote);

            StructureSurfaceMaterials.Apply(metal, StructureFinish.Deck);
            StructureSurfaceMaterials.Apply(trim, StructureFinish.Metal);

            Disc("HealBase", 2.15f, .025f, .025f, metal);
            Disc("HealDeck", 1.78f, .065f, .015f, deck);

            GameObject halo = ProductionMeshFactory.MeshObject("DF_HealEnergyRing", root, Own(ProductionMeshFactory.Ring(1.42f, .050f, 32, 8)), energy);
            halo.transform.localPosition = new Vector3(0, .185f, 0);

            // Active occupied outer ring and ground glow disc
            GameObject activeRingObj = ProductionMeshFactory.MeshObject("DF_HealOccupiedRing", root, Own(ProductionMeshFactory.Ring(1.74f, .042f, 32, 8)), auraMat);
            auraRing = activeRingObj.transform;
            auraRing.localPosition = new Vector3(0, .195f, 0);
            auraRing.localScale = Vector3.zero;

            GameObject activeDiscObj = ProductionMeshFactory.MeshObject("DF_HealOccupiedAuraDisc", root, Own(ProductionMeshFactory.Disc(1.58f, .006f, 32)), auraMat);
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

            Mesh plusMesh = CrossMesh(new Vector3(.16f, .44f, .10f), new Vector3(.44f, .16f, .10f));
            plus = Cross("DF_SpawnHealingPlus", plusMesh, energy);
            plus.localPosition = new Vector3(0, .92f, 0);

            // Rising 3D luminous green healing motes/crosses
            Mesh moteMesh = CrossMesh(new Vector3(.045f, .14f, .045f), new Vector3(.14f, .045f, .045f));
            for (int i = 0; i < MoteCount; i++)
            {
                Transform mote = Cross("DF_HealRisingMote_" + i, moteMesh, moteMat);
                mote.localScale = Vector3.zero;
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
            main.startColor = HealingVisualPalette.Mote;
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
                Color baseC = Color.Lerp(HealingVisualPalette.Energy, HealingVisualPalette.Active, occupiedBlend);
                float glow = .04f + occupiedBlend * (.02f + Mathf.Sin(t * 6.2f) * .015f);
                SetEmission(energy, baseC, new Color(.005f, glow, 0f));
            }

            if (auraRing != null && auraDisc != null)
            {
                if (occupiedBlend > .001f)
                {
                    float ringPulse = 1f + Mathf.Sin(t * 5.2f) * .05f;
                    auraRing.localScale = new Vector3(ringPulse, 1f, ringPulse) * occupiedBlend;
                    float discScale = (.82f + Mathf.Sin(t * 4.4f) * .06f) * occupiedBlend;
                    auraDisc.localScale = new Vector3(discScale, 1f, discScale);
                    SetEmission(auraMat, HealingVisualPalette.Aura, new Color(.005f, .12f * occupiedBlend, 0f));
                }
                else
                {
                    auraRing.localScale = Vector3.zero;
                    auraDisc.localScale = Vector3.zero;
                }
            }

            if (moteMat != null && occupiedBlend > .001f)
            {
                SetEmission(moteMat, HealingVisualPalette.Mote, new Color(.005f, .06f * occupiedBlend, 0f));
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
            GameObject g = ProductionMeshFactory.MeshObject("DF_" + n, root, Own(ProductionMeshFactory.Disc(radius, halfHeight, 32)), mat);
            g.transform.localPosition = new Vector3(0, y, 0);
        }

        private void Tube(string n, Vector3 a, Vector3 b, float radius, Material mat)
        {
            ProductionMeshFactory.MeshObject("DF_" + n, root, Own(ProductionMeshFactory.Tube(a, b, radius, 8)), mat);
        }

        private Mesh Own(Mesh mesh)
        {
            if (mesh != null) ownedMeshes.Add(mesh);
            return mesh;
        }

        private Mesh CrossMesh(Vector3 vertical, Vector3 horizontal)
        {
            Mesh box = ProductionMeshFactory.RoundedBox();
            var parts = new[]
            {
                new CombineInstance { mesh = box, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, vertical) },
                new CombineInstance { mesh = box, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, horizontal) }
            };
            Mesh mesh = Own(new Mesh { name = "DF_HealingCross" });
            mesh.CombineMeshes(parts, true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private Transform Cross(string name, Mesh mesh, Material mat)
        {
            GameObject g = ProductionMeshFactory.MeshObject(name, root, mesh, mat);
            var r = g.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g.transform;
        }

        private Material Mat(Color c, float metallic, float smooth)
        {
            return RuntimeShaderLibrary.Create(c, false, Color.black, metallic, smooth);
        }
    }

    // Unlit green keeps healing readable under every planet's coloured lighting.
    public static class HealingVisualPalette
    {
        public static readonly Color Deck = new(.025f, .46f, .005f, 1f);
        public static readonly Color Energy = new(.09f, .84f, .01f, 1f);
        public static readonly Color Active = new(.15f, .92f, .015f, 1f);
        public static readonly Color Aura = new(.04f, .60f, .005f, 1f);
        public static readonly Color Mote = new(.15f, .90f, .01f, 1f);

        public static Material CreateGreen(Color color)
        {
            Material material = RuntimeShaderLibrary.Create(color, true, Color.black, 0f, 0f);
            if (material == null) Debug.LogError("DIMENSIONS FRONT: healing material is unavailable.");
            return material;
        }
    }
}
