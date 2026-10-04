using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Keeps Auren/Kaelon's hammer in the upright menu/guard silhouette requested by art direction.
    /// The hammer rotates around its authored right-hand grip only; the arm pose is untouched.
    /// Attacks, abilities, hit reactions and dashes retain their authored weapon motion.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(970)]
    public sealed class KaelonHammerGuardPose : MonoBehaviour
    {
        private HeroIdentity identity;
        private HeroVisualMotion motion;
        private Transform model, grip, hammerGrip, hammerTop, hammerFront;

        private void Awake()
        {
            identity = GetComponent<HeroIdentity>();
            motion = GetComponent<HeroVisualMotion>();
            Resolve();
        }

        private void Resolve()
        {
            Transform visualRoot = transform.Find("DF_VisualRoot");
            if (visualRoot == null) return;
            model = visualRoot.Find("DF_AuthoredModel");
            if (model == null) return;
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            foreach (Transform part in all)
            {
                if (part.name == "WeaponGripR") grip = part;
                else if (part.name == "DF_TankHammerGrip") hammerGrip = part;
                else if (part.name == "DF_TankHammerTop") hammerTop = part;
                else if (part.name == "DF_TankHammerFront") hammerFront = part;
            }
        }

        private void LateUpdate()
        {
            if (identity == null) identity = GetComponent<HeroIdentity>();
            if (identity == null || identity.Hero != HeroId.Kaelon) return;
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            if (motion != null && motion.ArmActionActive) return;
            if (model == null || grip == null || hammerGrip == null || hammerTop == null || hammerFront == null)
            {
                Resolve();
                if (model == null || grip == null || hammerGrip == null || hammerTop == null || hammerFront == null) return;
            }

            Vector3 shaft = hammerTop.position - hammerGrip.position;
            Vector3 face = hammerFront.position - hammerTop.position;
            if (shaft.sqrMagnitude < .000001f || face.sqrMagnitude < .000001f) return;

            // The user's red guide is almost vertical, leaning roughly 16 degrees toward
            // Kaelon's weapon side. Keep the hammer head facing forward while matching that line.
            Vector3 desiredShaft = (model.up + model.right * .29f).normalized;
            Quaternion currentFrame = Quaternion.LookRotation(face.normalized, shaft.normalized);
            Quaternion desiredFrame = Quaternion.LookRotation(model.forward, desiredShaft);
            grip.rotation = desiredFrame * Quaternion.Inverse(currentFrame) * grip.rotation;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("DF_KaelonHammerGuardBootstrap");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Installer>();
        }

        [DefaultExecutionOrder(965)]
        private sealed class Installer : MonoBehaviour
        {
            private float nextScan;
            private void Update()
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + .5f;
                foreach (HeroIdentity hero in FindObjectsByType<HeroIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (hero != null && hero.Hero == HeroId.Kaelon && hero.GetComponent<KaelonHammerGuardPose>() == null)
                        hero.gameObject.AddComponent<KaelonHammerGuardPose>();
            }
        }
    }
}
