using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Removes the visible kink at Auren/Kaelon's right wrist while she is in her idle/guard pose.
    /// The correction is anatomical: the hand/grip axis is turned toward the forearm axis instead
    /// of imposing an arbitrary hammer angle. Combat actions keep their authored animation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(975)]
    public sealed class KaelonWristStraightener : MonoBehaviour
    {
        private HeroIdentity identity;
        private HeroVisualMotion motion;
        private Transform model, forearm, hand, hammerGrip;

        private void Awake()
        {
            identity = GetComponent<HeroIdentity>();
            motion = GetComponent<HeroVisualMotion>();
            Resolve();
        }

        private void Resolve()
        {
            Transform root = transform.Find("DF_VisualRoot");
            if (root == null) return;
            model = root.Find("DF_AuthoredModel");
            if (model == null) return;

            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "ForearmR") forearm = part;
                else if (part.name == "HandR") hand = part;
                else if (part.name == "DF_TankHammerGrip") hammerGrip = part;
            }
        }

        private void LateUpdate()
        {
            if (identity == null) identity = GetComponent<HeroIdentity>();
            if (identity == null || identity.Hero != HeroId.Kaelon) return;
            if (motion == null) motion = GetComponent<HeroVisualMotion>();

            // Do not fight attack, ability, hit or dash animation.
            if (motion != null && motion.ArmActionActive) return;

            if (model == null || forearm == null || hand == null || hammerGrip == null)
            {
                Resolve();
                if (model == null || forearm == null || hand == null || hammerGrip == null) return;
            }

            Vector3 forearmAxis = hand.position - forearm.position;
            Vector3 handAxis = hammerGrip.position - hand.position;
            if (forearmAxis.sqrMagnitude < .000001f || handAxis.sqrMagnitude < .000001f) return;

            forearmAxis.Normalize();
            handAxis.Normalize();
            // Some imported rigs place the grip marker on the palm side of the wrist.
            // Use the equivalent axis that requires the smaller anatomical correction.
            if (Vector3.Dot(handAxis, forearmAxis) < 0f) handAxis = -handAxis;

            Quaternion wanted = Quaternion.FromToRotation(handAxis, forearmAxis);
            // Cap a single-frame correction to avoid an accidental 180-degree flip if an
            // imported marker is malformed. The normal Kaelon wrist bend is well inside this.
            Quaternion correction = Quaternion.RotateTowards(Quaternion.identity, wanted, 55f);
            hand.rotation = correction * hand.rotation;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("DF_KaelonWristStraightenerBootstrap");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Installer>();
        }

        [DefaultExecutionOrder(970)]
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
                    if (hero.GetComponent<KaelonWristStraightener>() == null)
                        hero.gameObject.AddComponent<KaelonWristStraightener>();
                }
            }
        }
    }
}
