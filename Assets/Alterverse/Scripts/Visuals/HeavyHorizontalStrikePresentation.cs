using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only override for Kaelon and Nerok basic attacks.
    /// HeroVisualMotion still owns combat timing and the normal guard; this component runs
    /// immediately afterwards so the heavy weapon travels beside the body and then sweeps
    /// horizontally forward instead of pitching from below the character upward.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(935)]
    public sealed class HeavyHorizontalStrikePresentation : MonoBehaviour
    {
        private HeroIdentity identity;
        private AuthoredHeroPresentation authored;
        private Transform visualRoot;
        private Transform armR;
        private Transform armL;
        private Transform weaponR;
        private Quaternion armRestR;
        private Quaternion armRestL;
        private Quaternion weaponRestR;
        private bool captured;

        private bool active;
        private float attackStarted;
        private float windupDuration;
        private float recoveryDuration;

        public static HeavyHorizontalStrikePresentation For(GameObject actor)
        {
            if (actor == null) return null;
            HeroIdentity heroIdentity = actor.GetComponent<HeroIdentity>();
            if (heroIdentity == null || (heroIdentity.Hero != HeroId.Kaelon && heroIdentity.Hero != HeroId.Nerok))
                return null;
            return actor.GetComponent<HeavyHorizontalStrikePresentation>() ?? actor.AddComponent<HeavyHorizontalStrikePresentation>();
        }

        private void Awake()
        {
            identity = GetComponent<HeroIdentity>();
        }

        private void Start()
        {
            CaptureRig();
        }

        public void PulseBasic(float windup)
        {
            if (identity == null) identity = GetComponent<HeroIdentity>();
            if (identity == null || (identity.Hero != HeroId.Kaelon && identity.Hero != HeroId.Nerok)) return;
            if (!captured) CaptureRig();
            if (!captured) return;

            attackStarted = Time.time;
            windupDuration = Mathf.Max(.001f, windup);
            recoveryDuration = identity.Hero == HeroId.Kaelon ? .40f : .22f;
            active = true;
        }

        private void CaptureRig()
        {
            if (captured) return;
            if (identity == null) identity = GetComponent<HeroIdentity>();
            if (identity == null || (identity.Hero != HeroId.Kaelon && identity.Hero != HeroId.Nerok)) return;

            authored = GetComponent<AuthoredHeroPresentation>();
            visualRoot = transform.Find("DF_VisualRoot");
            armR = authored != null ? authored.Control("ArmR") : FindPart("DF_ArmR");
            armL = authored != null ? authored.Control("ArmL") : FindPart("DF_ArmL");
            weaponR = authored != null ? authored.Control("WeaponR") : FindPart("DF_WeaponR");

            if (armR == null || weaponR == null) return;
            armRestR = armR.localRotation;
            armRestL = armL != null ? armL.localRotation : Quaternion.identity;
            weaponRestR = weaponR.localRotation;
            captured = true;
        }

        private Transform FindPart(string partName)
        {
            if (visualRoot == null) return null;
            foreach (Transform child in visualRoot.GetComponentsInChildren<Transform>(true))
                if (child.name == partName) return child;
            return null;
        }

        private static float Ease(float value)
            => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

        private void LateUpdate()
        {
            if (!active || Time.timeScale <= 0f) return;
            if (!captured)
            {
                CaptureRig();
                if (!captured) return;
            }

            float age = Time.time - attackStarted;
            float end = windupDuration + recoveryDuration;
            if (age >= end)
            {
                active = false;
                return;
            }

            // Keep the shoulder at almost one height throughout the cut. The 90-degree
            // travel is around local Y (horizontal yaw), never a large local-X underhand arc.
            Vector3 neutralArm = identity.Hero == HeroId.Kaelon
                ? new Vector3(-28f, 0f, -12f)
                : new Vector3(-30f, 0f, -12f);
            Vector3 sideArm = identity.Hero == HeroId.Kaelon
                ? new Vector3(-32f, -72f, -20f)
                : new Vector3(-30f, -72f, -18f);
            Vector3 contactArm = identity.Hero == HeroId.Kaelon
                ? new Vector3(-36f, 18f, -5f)
                : new Vector3(-34f, 18f, -4f);
            Vector3 throughArm = identity.Hero == HeroId.Kaelon
                ? new Vector3(-35f, 32f, 2f)
                : new Vector3(-33f, 32f, 3f);

            Quaternion armPose;
            Quaternion weaponPose;
            if (age < windupDuration)
            {
                float w = Mathf.Clamp01(age / windupDuration);
                if (w < .45f)
                {
                    // First bring the weapon beside the body.
                    float u = Ease(w / .45f);
                    armPose = Quaternion.Slerp(Quaternion.Euler(neutralArm), Quaternion.Euler(sideArm), u);
                    weaponPose = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0f, -6f, -5f), u);
                }
                else
                {
                    // Then sweep exactly 90 degrees in yaw: -72 -> +18.
                    float u = Ease((w - .45f) / .55f);
                    armPose = Quaternion.Slerp(Quaternion.Euler(sideArm), Quaternion.Euler(contactArm), u);
                    weaponPose = Quaternion.Slerp(Quaternion.Euler(0f, -6f, -5f), Quaternion.Euler(0f, 4f, 0f), u);
                }
            }
            else
            {
                float r = Mathf.Clamp01((age - windupDuration) / recoveryDuration);
                if (r < .24f)
                {
                    float u = Ease(r / .24f);
                    armPose = Quaternion.Slerp(Quaternion.Euler(contactArm), Quaternion.Euler(throughArm), u);
                    weaponPose = Quaternion.Slerp(Quaternion.Euler(0f, 4f, 0f), Quaternion.Euler(0f, 8f, 3f), u);
                }
                else
                {
                    float u = Ease((r - .24f) / .76f);
                    armPose = Quaternion.Slerp(Quaternion.Euler(throughArm), Quaternion.Euler(neutralArm), u);
                    weaponPose = Quaternion.Slerp(Quaternion.Euler(0f, 8f, 3f), Quaternion.identity, u);
                }
            }

            armR.localRotation = armRestR * armPose;
            weaponR.localRotation = weaponRestR * weaponPose;

            // The support arm stays in a compact guard instead of following the old vertical slam.
            if (armL != null)
            {
                Vector3 support = identity.Hero == HeroId.Kaelon
                    ? new Vector3(-25f, -4f, 10f)
                    : new Vector3(-22f, -3f, 11f);
                armL.localRotation = armRestL * Quaternion.Euler(support);
            }
        }
    }
}
