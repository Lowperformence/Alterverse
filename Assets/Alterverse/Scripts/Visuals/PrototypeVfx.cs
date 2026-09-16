using UnityEngine;

namespace Alterverse.Visuals
{
    public static class PrototypeVfx
    {
        public static void Burst(Vector3 position, Color color, float size = 1f, int pieces = 7)
        {
            for (int i = 0; i < pieces; i++)
            {
                float angle = (360f / pieces) * i;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "AV_VFX_BurstShard";
                shard.transform.position = position + Vector3.up * 0.8f + direction * 0.2f;
                shard.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                DisableCollider(shard);
                SetColor(shard, color);

                Vector3 from = new Vector3(0.10f, 0.10f, 0.35f) * size;
                Vector3 to = new Vector3(0.02f, 0.02f, 1.65f) * size;
                shard.AddComponent<TransientScaleVfx>().Configure(from, to, 0.24f, 220f);
            }
        }

        public static void Ring(Vector3 position, Color color, float radius = 1.5f, float duration = 0.35f)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "AV_VFX_Ring";
            ring.transform.position = position + Vector3.up * 0.06f;
            DisableCollider(ring);
            SetColor(ring, color);
            Vector3 from = new Vector3(0.25f, 0.025f, 0.25f);
            Vector3 to = new Vector3(radius, 0.015f, radius);
            ring.AddComponent<TransientScaleVfx>().Configure(from, to, duration, 90f);
        }

        public static void Slash(Transform source, Color color, float length = 1.7f)
        {
            GameObject slash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slash.name = "AV_VFX_Slash";
            slash.transform.position = source.position + Vector3.up * 0.75f + source.forward * 1.0f;
            slash.transform.rotation = source.rotation * Quaternion.Euler(0f, 25f, -25f);
            DisableCollider(slash);
            SetColor(slash, color);
            Vector3 from = new Vector3(0.12f, 0.7f, length);
            Vector3 to = new Vector3(0.025f, 0.08f, length * 1.2f);
            slash.AddComponent<TransientScaleVfx>().Configure(from, to, 0.18f, 320f);
        }

        public static void Mark(Vector3 position, Color color)
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mark.name = "AV_VFX_FutureMark";
            mark.transform.position = position + Vector3.up * 2.2f;
            mark.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            DisableCollider(mark);
            SetColor(mark, color);
            mark.AddComponent<TransientScaleVfx>().Configure(
                new Vector3(0.2f, 0.02f, 0.2f),
                new Vector3(0.9f, 0.02f, 0.9f),
                1.1f,
                -180f);
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        private static void SetColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }
    }
}
