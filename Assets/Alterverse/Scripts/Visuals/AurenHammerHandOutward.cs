using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Rotates only Auren/Kaelon's right hand around the forearm axis in idle/guard so the
    /// hammer is rolled from the inside toward the outside and its shaft stands upright.
    /// Arm position, forearm length/thickness and combat animation are left untouched.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(980)]
    public sealed class AurenHammerHandOutward : MonoBehaviour
    {
        private HeroIdentity identity;
        private HeroVisualMotion motion;
        private Transform model, forearm, hand, hammerGrip, hammerTop;

        private void Awake()
        {
            identity = GetComponent<HeroIdentity>();
            motion = GetComponent<HeroVisualMotion>();
            Resolve();
        }

        private void Resolve()
        {
            Transform root = transform.Find("DF_VisualRoot");
            model = root != null ? root.Find("DF_AuthoredModel") : null;
            if (model == null) return;

            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "ForearmR") forearm = part;
                else if (part.name == "HandR") hand = part;
                else if (part.name == "DF_TankHammerGrip") hammerGrip = part;
                else if (part.name == "DF_TankHammerTop") hammerTop = part;
            }
        }

        private void LateUpdate()
        {
            if (identity == null) identity = GetComponent<HeroIdentity>();
            if (identity == null || identity.Hero != HeroId.Kaelon) return;
            if (motion == null) motion = GetComponent<HeroVisualMotion>();

            // Do not alter Auren's hand while an attack, ability, hit reaction or dash owns it.
            if (motion != null && motion.ArmActionActive) return;

            if (model == null || forearm == null || hand == null || hammerGrip == null || hammerTop == null)
            {
                Resolve();
                if (model == null || forearm == null || hand == null || hammerGrip == null || hammerTop == null) return;
            }

            // This is a pure pronation/supination correction: rotate around the forearm axis,
            // not around the wrist bend axes. The arm and hand positions therefore stay fixed.
            Vector3 axis = hand.position - forearm.position;
            Vector3 shaft = hammerTop.position - hammerGrip.position;
            if (axis.sqrMagnitude < .000001f || shaft.sqrMagnitude < .000001f) return;
            axis.Normalize();

            // Match the marked reference: the hammer head should sit above the hand and the
            // handle should read nearly vertical. Because we are only allowed to roll the hand
            // around the forearm, align the shaft and world-up projections in that roll plane.
            Vector3 from = Vector3.ProjectOnPlane(shaft, axis);
            Vector3 to = Vector3.ProjectOnPlane(model.up, axis);
            if (from.sqrMagnitude < .000001f || to.sqrMagnitude < .000001f) return;

            float angle = Vector3.SignedAngle(from, to, axis);
            // Avoid an accidental imported-marker flip while still allowing the large outward
            // roll visible in the user's red guide.
            angle = Mathf.Clamp(angle, -150f, 150f);
            hand.rotation = Quaternion.AngleAxis(angle, axis) * hand.rotation;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("DF_AurenHammerHandOutwardBootstrap");
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
                    if (hero.GetComponent<AurenHammerHandOutward>() == null)
                        hero.gameObject.AddComponent<AurenHammerHandOutward>();
                }
            }
        }
    }
}
