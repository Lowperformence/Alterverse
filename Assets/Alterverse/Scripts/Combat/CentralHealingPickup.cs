using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    public sealed class CentralHealingPickup : MonoBehaviour
    {
        public static event System.Action<bool, float> NetworkAvailabilityChanged;

        [SerializeField] private float radius = 1.5f, healPercent = .286f, respawnSeconds = 22f / 1.3f;
        private Transform visual, plus;
        private float readyAt;
        private bool available = true;
        private Material greenMat, stoneMat;
        private readonly List<Mesh> ownedMeshes = new();

        private void Start()
        {
            BuildVisual();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedMeshes.Count; i++)
                if (ownedMeshes[i] != null) Destroy(ownedMeshes[i]);
            ownedMeshes.Clear();
            if (greenMat != null) Destroy(greenMat);
            if (stoneMat != null) Destroy(stoneMat);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
            {
                AnimateAvailable();
                return;
            }
            if (!available)
            {
                if (Time.time >= readyAt)
                {
                    available = true;
                    if (plus != null) plus.gameObject.SetActive(true);
                    if (Alterverse.Multiplayer.CoopAuthority.Active && Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority)
                        NetworkAvailabilityChanged?.Invoke(true, 0f);
                }
                return;
            }
            var active = Health.Active;
            float rSqr = radius * radius;
            Vector3 pos = transform.position;
            for (int i = 0; i < active.Count; i++)
            {
                Health h = active[i];
                if (h == null || h.IsDead || h.Normalized >= .995f) continue;
                if (h.GetComponent<TeamMember>() == null) continue;
                Vector3 d = h.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= rSqr)
                {
                    h.Heal(h.Max * healPercent);
                    available = false;
                    readyAt = Time.time + respawnSeconds;
                    if (plus != null) plus.gameObject.SetActive(false);
                    if (Alterverse.Multiplayer.CoopAuthority.Active && Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority)
                        NetworkAvailabilityChanged?.Invoke(false, respawnSeconds);
                    break;
                }
            }
            AnimateAvailable();
        }

        public void ApplyNetworkAvailability(bool value, float readyIn)
        {
            if (Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay) return;
            available = value;
            readyAt = Time.time + Mathf.Max(0, readyIn);
            if (plus != null) plus.gameObject.SetActive(value);
        }

        private void AnimateAvailable()
        {
            if (plus != null && plus.gameObject.activeSelf)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3.6f) * .10f;
                plus.localScale = Vector3.one * pulse;
                plus.localPosition = new Vector3(0, 1.22f + Mathf.Sin(Time.time * 2.4f) * .08f, 0);
            }
        }

        private void BuildVisual()
        {
            visual = new GameObject("DF_HealStation").transform;
            visual.SetParent(transform, false);
            greenMat = HealingVisualPalette.CreateGreen(HealingVisualPalette.Energy);
            stoneMat = RuntimeShaderLibrary.Create(new Color(.18f, .20f, .18f), false, Color.black, .35f, .28f);
            BuildPedestal();
            plus = new GameObject("DF_HealingPlus").transform;
            plus.SetParent(visual, false);
            plus.localPosition = new Vector3(0, 1.22f, 0);
            Part(plus, "Vertical", Vector3.zero, new Vector3(.24f, .88f, .24f), greenMat);
            Part(plus, "Horizontal", Vector3.zero, new Vector3(.88f, .24f, .24f), greenMat);
        }

        private void BuildPedestal()
        {
            MeshPart(visual, "Base", ProductionMeshFactory.Disc(.82f, .10f, 28), new Vector3(0, .10f, 0), Quaternion.identity, stoneMat);
            MeshPart(visual, "EnergyRing", ProductionMeshFactory.Ring(.62f, .045f, 28, 7), new Vector3(0, .23f, 0), Quaternion.identity, greenMat);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 p = new(Mathf.Cos(a) * .72f, .22f, Mathf.Sin(a) * .72f);
                MeshPart(visual, "Node", ProductionMeshFactory.Disc(.13f, .07f, 16), p, Quaternion.identity, i % 2 == 0 ? greenMat : stoneMat);
            }
        }

        private Transform Part(Transform parent, string n, Vector3 p, Vector3 scale, Material mat)
        {
            Vector3 half = scale * .5f;
            Mesh mesh = ProductionMeshFactory.Tube(new Vector3(0, -half.y, 0), new Vector3(0, half.y, 0), Mathf.Max(.04f, Mathf.Min(scale.x, scale.z) * .5f), 10);
            ownedMeshes.Add(mesh);
            GameObject g = ProductionMeshFactory.MeshObject("DF_Heal" + n, parent, mesh, mat);
            g.transform.localPosition = p;
            return g.transform;
        }

        private Transform MeshPart(Transform parent, string n, Mesh mesh, Vector3 p, Quaternion r, Material mat)
        {
            ownedMeshes.Add(mesh);
            GameObject g = ProductionMeshFactory.MeshObject("DF_Heal" + n, parent, mesh, mat);
            g.transform.localPosition = p;
            g.transform.localRotation = r;
            return g.transform;
        }

    }
}
