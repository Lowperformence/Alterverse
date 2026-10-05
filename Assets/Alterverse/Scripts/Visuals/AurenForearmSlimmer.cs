using System;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Makes only Auren/Kaelon's forearm body 30% slimmer without changing bone lengths,
    /// hand positions, shield/hammer size, or attack animation. The forearm cross-section
    /// is scaled to 70% around the bind-pose forearm axis while its length is preserved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AurenForearmSlimmer : MonoBehaviour
    {
        private Mesh ownedMesh;
        private bool applied;

        private void Start() => ApplyIfReady();
        private void OnEnable() => ApplyIfReady();

        public static void Apply(GameObject actor)
        {
            if (actor == null) return;
            var slimmer = actor.GetComponent<AurenForearmSlimmer>() ?? actor.AddComponent<AurenForearmSlimmer>();
            slimmer.ApplyIfReady();
        }

        private void ApplyIfReady()
        {
            if (applied) return;
            HeroIdentity identity = GetComponent<HeroIdentity>();
            if (identity == null || identity.Hero != HeroId.Kaelon) return;

            Transform root = transform.Find("DF_VisualRoot");
            Transform model = root != null ? root.Find("DF_AuthoredModel") : null;
            if (model == null) return;

            SkinnedMeshRenderer anatomy = null;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "DF_Anatomy") { anatomy = renderer; break; }
            if (anatomy == null || anatomy.sharedMesh == null || anatomy.bones == null) return;

            int forearmL = BoneIndex(anatomy, "ForearmL");
            int handL = BoneIndex(anatomy, "HandL");
            int forearmR = BoneIndex(anatomy, "ForearmR");
            int handR = BoneIndex(anatomy, "HandR");
            if (forearmL < 0 || handL < 0 || forearmR < 0 || handR < 0) return;

            Mesh source = anatomy.sharedMesh;
            Matrix4x4[] bindposes = source.bindposes;
            if (bindposes == null || bindposes.Length <= Mathf.Max(Mathf.Max(forearmL, handL), Mathf.Max(forearmR, handR))) return;

            Vector3 forearmOriginL = bindposes[forearmL].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 handOriginL = bindposes[handL].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 forearmOriginR = bindposes[forearmR].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 handOriginR = bindposes[handR].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 axisL = handOriginL - forearmOriginL;
            Vector3 axisR = handOriginR - forearmOriginR;
            if (axisL.sqrMagnitude < .000001f || axisR.sqrMagnitude < .000001f) return;
            axisL.Normalize(); axisR.Normalize();

            ownedMesh = Instantiate(source);
            ownedMesh.name = source.name + "_AurenForearms70";
            Vector3[] vertices = ownedMesh.vertices;
            BoneWeight[] weights = ownedMesh.boneWeights;
            if (weights == null || weights.Length != vertices.Length) { Destroy(ownedMesh); ownedMesh = null; return; }

            for (int i = 0; i < vertices.Length; i++)
            {
                // SiegeTankAppearance currently assigns one rigid bone per vertex. Keep a
                // weighted fallback so this remains safe if the skin later becomes blended.
                float leftWeight = WeightFor(weights[i], forearmL);
                float rightWeight = WeightFor(weights[i], forearmR);
                if (leftWeight <= .001f && rightWeight <= .001f) continue;

                Vector3 original = vertices[i];
                Vector3 slimmed = original;
                float total = leftWeight + rightWeight;
                if (leftWeight > .001f)
                    slimmed = Vector3.Lerp(slimmed, SlimAroundAxis(original, forearmOriginL, axisL), leftWeight / total);
                if (rightWeight > .001f)
                    slimmed = Vector3.Lerp(slimmed, SlimAroundAxis(original, forearmOriginR, axisR), rightWeight / total);
                vertices[i] = Vector3.Lerp(original, slimmed, Mathf.Clamp01(total));
            }

            ownedMesh.vertices = vertices;
            ownedMesh.RecalculateBounds();
            anatomy.sharedMesh = ownedMesh;
            applied = true;
            Debug.Log("DF: Auren forearm bodies reduced to 70% thickness; bone length and weapon grips unchanged.", this);
        }

        private static Vector3 SlimAroundAxis(Vector3 point, Vector3 origin, Vector3 axis)
        {
            Vector3 delta = point - origin;
            Vector3 along = axis * Vector3.Dot(delta, axis);
            Vector3 radial = delta - along;
            return origin + along + radial * .70f;
        }

        private static int BoneIndex(SkinnedMeshRenderer renderer, string name)
        {
            for (int i = 0; i < renderer.bones.Length; i++)
                if (renderer.bones[i] != null && renderer.bones[i].name == name) return i;
            return -1;
        }

        private static float WeightFor(BoneWeight weight, int bone)
        {
            float value = 0f;
            if (weight.boneIndex0 == bone) value += weight.weight0;
            if (weight.boneIndex1 == bone) value += weight.weight1;
            if (weight.boneIndex2 == bone) value += weight.weight2;
            if (weight.boneIndex3 == bone) value += weight.weight3;
            return value;
        }

        private void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("DF_AurenForearmSlimmerBootstrap");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Installer>();
        }

        private sealed class Installer : MonoBehaviour
        {
            private float nextScan;
            private void Update()
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + .5f;
                foreach (HeroIdentity hero in FindObjectsByType<HeroIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (hero == null || hero.Hero != HeroId.Kaelon) continue;
                    if (hero.GetComponent<AurenForearmSlimmer>() == null)
                        hero.gameObject.AddComponent<AurenForearmSlimmer>();
                }
            }
        }
    }
}

