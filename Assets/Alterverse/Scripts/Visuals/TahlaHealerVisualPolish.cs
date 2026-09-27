using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Tahla-only presentation polish. It intercepts the procedural hero build before batching so the
    /// side mantle never enters the combined mesh, then adds a more readable feminine face afterwards.
    /// Gameplay, hitboxes, animations and every other hero remain untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TahlaHealerVisualPolish : MonoBehaviour
    {
        private TahlaVisualBuildInterceptor interceptor;
        private Material headMaterial;
        private Material eyeMaterial;
        private Material browMaterial;
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Mesh faceDetailMesh;
        private bool faceAdded;

        private void OnTransformChildrenChanged()
        {
            EnsureInterceptor();
        }

        private void Start()
        {
            EnsureInterceptor();
            AddFeminineFace();
        }

        private void EnsureInterceptor()
        {
            HeroIdentity identity = GetComponent<HeroIdentity>();
            if (identity == null || identity.Hero != HeroId.Mira) return;

            Transform visualRoot = transform.Find("DF_VisualRoot");
            if (visualRoot == null) return;

            interceptor = visualRoot.GetComponent<TahlaVisualBuildInterceptor>();
            if (interceptor == null) interceptor = visualRoot.gameObject.AddComponent<TahlaVisualBuildInterceptor>();
            interceptor.Initialize(this);
        }

        internal void CaptureHead(Transform part)
        {
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null) headMaterial = renderer.sharedMaterial;

            // Softer, more feminine silhouette: keep the forehead/cheeks readable while narrowing
            // the lower jaw progressively toward the chin. The source mesh is unique to this Tahla build.
            MeshFilter filter = part.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return;

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                if (vertex.y < -0.02f)
                {
                    float lowerFace = Mathf.InverseLerp(-0.22f, -0.02f, vertex.y);
                    vertex.x *= Mathf.Lerp(0.78f, 0.92f, lowerFace);
                }
                else if (vertex.y < 0.12f)
                {
                    vertex.x *= 0.96f;
                }
                vertices[i] = vertex;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        internal void CaptureEye(Renderer renderer)
        {
            if (renderer != null && renderer.sharedMaterial != null && eyeMaterial == null)
                eyeMaterial = renderer.sharedMaterial;
            if (renderer != null) renderer.enabled = false;
        }

        internal void CaptureBrow(Renderer renderer)
        {
            if (renderer != null && renderer.sharedMaterial != null && browMaterial == null)
                browMaterial = renderer.sharedMaterial;
            if (renderer != null) renderer.enabled = false;
        }

        internal static void RemoveSideMantle(Renderer renderer)
        {
            // Disabling before StylizedHeroVisual batches static geometry means these panels are truly
            // omitted from the final in-game mesh instead of merely being hidden after construction.
            if (renderer != null) renderer.enabled = false;
        }

        private void AddFeminineFace()
        {
            if (faceAdded) return;

            HeroIdentity identity = GetComponent<HeroIdentity>();
            if (identity == null || identity.Hero != HeroId.Mira) return;

            Transform visualRoot = transform.Find("DF_VisualRoot");
            if (visualRoot == null || headMaterial == null) return;

            faceAdded = true;
            if (faceDetailMesh == null) faceDetailMesh = BuildFaceDetailMesh();

            Material white = CreateTintedMaterial(headMaterial, new Color(0.93f, 0.95f, 0.98f, 1f), "DF_TahlaEyeWhite");
            Material lip = CreateTintedMaterial(headMaterial, new Color(0.55f, 0.27f, 0.36f, 1f), "DF_TahlaLip");
            Material iris = eyeMaterial != null ? eyeMaterial : CreateTintedMaterial(headMaterial, new Color(0.68f, 0.91f, 1f, 1f), "DF_TahlaIris");
            Material dark = browMaterial != null ? browMaterial : CreateTintedMaterial(headMaterial, new Color(0.19f, 0.11f, 0.13f, 1f), "DF_TahlaFaceDark");

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.073f;
                CreateFacePart(visualRoot, "DF_TahlaFeminineEyeWhite" + (side < 0 ? "L" : "R"),
                    new Vector3(x, 0.840f, 0.178f), new Vector3(0.078f, 0.032f, 0.020f), white,
                    new Vector3(0f, 0f, side * -4f));
                CreateFacePart(visualRoot, "DF_TahlaFeminineIris" + (side < 0 ? "L" : "R"),
                    new Vector3(x, 0.840f, 0.190f), new Vector3(0.031f, 0.028f, 0.011f), iris);
                CreateFacePart(visualRoot, "DF_TahlaFemininePupil" + (side < 0 ? "L" : "R"),
                    new Vector3(x, 0.840f, 0.197f), new Vector3(0.011f, 0.017f, 0.006f), dark);

                // Thin, slightly lifted brows and upper lashes make the eyes read clearly at mobile scale.
                CreateFacePart(visualRoot, "DF_TahlaFeminineBrow" + (side < 0 ? "L" : "R"),
                    new Vector3(x, 0.885f, 0.176f), new Vector3(0.080f, 0.010f, 0.010f), dark,
                    new Vector3(0f, 0f, side * -10f));
                CreateFacePart(visualRoot, "DF_TahlaFeminineLash" + (side < 0 ? "L" : "R"),
                    new Vector3(x + side * 0.004f, 0.859f, 0.191f), new Vector3(0.084f, 0.007f, 0.007f), dark,
                    new Vector3(0f, 0f, side * -7f));
            }

            // Small bridge and tip instead of the previous completely flat face.
            CreateFacePart(visualRoot, "DF_TahlaFeminineNoseBridge",
                new Vector3(0f, 0.802f, 0.174f), new Vector3(0.024f, 0.050f, 0.020f), headMaterial);
            CreateFacePart(visualRoot, "DF_TahlaFeminineNoseTip",
                new Vector3(0f, 0.778f, 0.187f), new Vector3(0.034f, 0.025f, 0.029f), headMaterial);

            // Three soft lip forms create a subtle natural mouth rather than a hard rectangular mark.
            CreateFacePart(visualRoot, "DF_TahlaFeminineUpperLipL",
                new Vector3(-0.025f, 0.735f, 0.177f), new Vector3(0.045f, 0.014f, 0.012f), lip,
                new Vector3(0f, 0f, -5f));
            CreateFacePart(visualRoot, "DF_TahlaFeminineUpperLipR",
                new Vector3(0.025f, 0.735f, 0.177f), new Vector3(0.045f, 0.014f, 0.012f), lip,
                new Vector3(0f, 0f, 5f));
            CreateFacePart(visualRoot, "DF_TahlaFeminineLowerLip",
                new Vector3(0f, 0.724f, 0.179f), new Vector3(0.061f, 0.015f, 0.013f), lip);
        }

        private Material CreateTintedMaterial(Material source, Color color, string materialName)
        {
            Material material;
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) return null;
                material = new Material(shader);
            }

            material.name = materialName;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            ownedMaterials.Add(material);
            return material;
        }

        private void CreateFacePart(Transform parent, string partName, Vector3 localPosition,
            Vector3 localScale, Material material, Vector3 localRotation = default)
        {
            GameObject part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localRotation);
            part.AddComponent<MeshFilter>().sharedMesh = faceDetailMesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            part.layer = gameObject.layer;
        }

        private static Mesh BuildFaceDetailMesh()
        {
            const int longitude = 10;
            const int latitude = 6;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int y = 0; y <= latitude; y++)
            {
                float v = y / (float)latitude;
                float phi = Mathf.PI * v;
                float ring = Mathf.Sin(phi) * 0.5f;
                float height = Mathf.Cos(phi) * 0.5f;
                for (int x = 0; x < longitude; x++)
                {
                    float theta = Mathf.PI * 2f * x / longitude;
                    vertices.Add(new Vector3(Mathf.Cos(theta) * ring, height, Mathf.Sin(theta) * ring));
                }
            }

            for (int y = 0; y < latitude; y++)
            {
                for (int x = 0; x < longitude; x++)
                {
                    int next = (x + 1) % longitude;
                    int a = y * longitude + x;
                    int b = y * longitude + next;
                    int c = (y + 1) * longitude + next;
                    int d = (y + 1) * longitude + x;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
            }

            Mesh mesh = new Mesh { name = "DF_TahlaFeminineFaceDetail" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedMaterials.Count; i++)
                if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
            if (faceDetailMesh != null) Destroy(faceDetailMesh);
        }
    }

    /// <summary>
    /// Attached to DF_VisualRoot before Tahla's first generated child. Unity calls
    /// OnTransformChildrenChanged synchronously as the procedural build adds each direct child.
    /// </summary>
    internal sealed class TahlaVisualBuildInterceptor : MonoBehaviour
    {
        private TahlaHealerVisualPolish owner;
        private readonly HashSet<EntityId> handled = new HashSet<EntityId>();

        internal void Initialize(TahlaHealerVisualPolish newOwner)
        {
            owner = newOwner;
            ScanExisting();
        }

        internal void ScanExisting()
        {
            if (owner == null) return;
            for (int i = 0; i < transform.childCount; i++) Handle(transform.GetChild(i));
        }

        private void OnTransformChildrenChanged()
        {
            ScanExisting();
        }

        private void Handle(Transform child)
        {
            if (child == null || !handled.Add(child.GetEntityId())) return;

            string partName = child.name;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();

            if (partName == "DF_Head")
            {
                owner.CaptureHead(child);
                return;
            }

            if (partName.StartsWith("DF_TahlaGoldenVeil") ||
                partName.StartsWith("DF_TahlaVeilEdge") ||
                partName.StartsWith("DF_TahlaRobeSide"))
            {
                TahlaHealerVisualPolish.RemoveSideMantle(renderer);
                return;
            }

            if (partName.StartsWith("DF_TahlaEye"))
            {
                owner.CaptureEye(renderer);
                return;
            }

            if (partName.StartsWith("DF_TahlaBrow"))
            {
                owner.CaptureBrow(renderer);
            }
        }
    }
}
