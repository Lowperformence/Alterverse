using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Shared Christmas snowman meshes; presentation adds no colliders or pickup authority.</summary>
    public sealed class ChristmasSnowmanVisual : MonoBehaviour
    {
        static Mesh sphere, cone;
        static Material snow, coal, carrot, red, wood;
        Transform art;
        float phase;

        public static GameObject Create(Transform parent, Vector3 ground, int id)
        {
            EnsureAssets();
            GameObject root = new("DF_ChristmasSnowman_" + id);
            root.transform.SetParent(parent, false);
            root.transform.position = ground;
            var view = root.AddComponent<ChristmasSnowmanVisual>();
            view.phase = id * .71f;
            view.art = new GameObject("ChristmasSnowman").transform;
            view.art.SetParent(root.transform, false);
            view.Animate(); // Initialize immediately, including during a paused match.
            Transform a = view.art;
            Ball("SnowBase", a, snow, new Vector3(0, .43f, 0), new Vector3(.46f, .43f, .46f));
            Ball("SnowBody", a, snow, new Vector3(0, 1.01f, 0), new Vector3(.34f, .34f, .34f));
            Ball("SnowHead", a, snow, new Vector3(0, 1.52f, 0), new Vector3(.26f, .26f, .26f));
            Ball("LeftCoalEye", a, coal, new Vector3(-.105f, 1.59f, -.233f), new Vector3(.039f, .039f, .028f));
            Ball("RightCoalEye", a, coal, new Vector3(.105f, 1.59f, -.233f), new Vector3(.039f, .039f, .028f));
            Part("CarrotNose", a, cone, carrot, new Vector3(0, 1.51f, -.25f),
                new Vector3(.058f, .23f, .058f), Quaternion.Euler(-90, 0, 0));
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * .052f;
                Ball("SmileCoal" + i, a, coal, new Vector3(x, 1.405f + (i - 2) * (i - 2) * .012f, -.231f),
                    new Vector3(.021f, .021f, .021f));
            }
            Ball("TopButton", a, coal, new Vector3(0, 1.08f, -.335f), new Vector3(.046f, .046f, .026f));
            Ball("BottomButton", a, coal, new Vector3(0, .91f, -.328f), new Vector3(.046f, .046f, .026f));
            Ball("RedScarf", a, red, new Vector3(0, 1.305f, 0), new Vector3(.28f, .085f, .28f));
            Ball("ScarfTail", a, red, new Vector3(.19f, 1.105f, -.282f), new Vector3(.075f, .245f, .045f));
            Ball("LeftTwigArm", a, wood, new Vector3(-.45f, 1.10f, 0), new Vector3(.28f, .035f, .035f), -24);
            Ball("RightTwigArm", a, wood, new Vector3(.45f, 1.10f, 0), new Vector3(.28f, .035f, .035f), 24);
            Ball("SantaHatBrim", a, snow, new Vector3(0, 1.735f, 0), new Vector3(.285f, .065f, .285f));
            Part("SantaHat", a, cone, red, new Vector3(0, 1.75f, 0),
                new Vector3(.26f, .43f, .26f), Quaternion.Euler(0, 0, -12));
            Ball("SantaHatPom", a, snow, new Vector3(.09f, 2.17f, 0), new Vector3(.09f, .09f, .09f));
            return root;
        }

        void Update() => Animate();
        void Animate()
        {
            if (art == null) return;
            art.localPosition = Vector3.up * (.035f + Mathf.Sin(Time.time * 2.2f + phase) * .025f);
            art.localRotation = Quaternion.Euler(0, Time.time * 13f + phase * 25f, 0);
        }
        static void Ball(string name, Transform parent, Material material, Vector3 position, Vector3 scale, float tilt = 0)
            => Part(name, parent, sphere, material, position, scale, Quaternion.Euler(0, 0, tilt));

        static void Part(string name, Transform parent, Mesh mesh, Material material,
            Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Make(Color color, float emission = 0)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material result = new(shader) { color = color };
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", .25f);
            if (emission > 0 && result.HasProperty("_EmissionColor"))
            {
                result.EnableKeyword("_EMISSION");
                result.SetColor("_EmissionColor", color * emission);
            }
            return result;
        }

        static void EnsureAssets()
        {
            if (sphere != null) return;
            snow = Make(new Color(.88f, .96f, 1f), .15f);
            coal = Make(new Color(.025f, .035f, .05f));
            carrot = Make(new Color(1f, .35f, .035f));
            red = Make(new Color(.85f, .035f, .075f));
            wood = Make(new Color(.24f, .12f, .055f));
            const int sides = 24, rings = 12;
            var vertices = new Vector3[(rings + 1) * (sides + 1)];
            var triangles = new int[rings * sides * 6];
            for (int r = 0; r <= rings; r++)
            for (int s = 0; s <= sides; s++)
            {
                float phi = Mathf.PI * r / rings, theta = 2 * Mathf.PI * s / sides;
                vertices[r * (sides + 1) + s] = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
            }
            int t = 0;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                int a = r * (sides + 1) + s, b = a + sides + 1;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
            sphere = new Mesh { name = "DF_SharedChristmasSnowSphere", vertices = vertices, triangles = triangles };
            sphere.RecalculateNormals(); sphere.RecalculateBounds();

            // Separate side triangles keep the cone tip normals stable; the base faces outward too.
            vertices = new Vector3[sides * 6];
            triangles = new int[vertices.Length];
            for (int s = 0; s < sides; s++)
            {
                float angle = 2 * Mathf.PI * s / sides, next = 2 * Mathf.PI * (s + 1) / sides;
                Vector3 a = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 b = new(Mathf.Cos(next), 0, Mathf.Sin(next));
                int i = s * 6;
                vertices[i] = a; vertices[i + 1] = Vector3.up; vertices[i + 2] = b;
                vertices[i + 3] = a; vertices[i + 4] = b; vertices[i + 5] = new Vector3(0, 0, 0);
                for (int k = 0; k < 6; k++) triangles[i + k] = i + k;
            }
            cone = new Mesh { name = "DF_SharedChristmasCone", vertices = vertices, triangles = triangles };
            cone.RecalculateNormals(); cone.RecalculateBounds();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared()
        {
            if (sphere != null) Destroy(sphere);
            if (cone != null) Destroy(cone);
            if (snow != null) Destroy(snow);
            if (coal != null) Destroy(coal);
            if (carrot != null) Destroy(carrot);
            if (red != null) Destroy(red);
            if (wood != null) Destroy(wood);
            sphere = null; cone = null;
            snow = null; coal = null; carrot = null; red = null; wood = null;
        }
    }
}
