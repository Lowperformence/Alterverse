using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Final visual pass for Zahev Null (Veyr): moves both crescent blades so the
    /// black/gold handle section sits inside the palm instead of outside the hand.
    /// AuthoredHeroPresentation restores WeaponGripL/R every LateUpdate, therefore
    /// this correction intentionally runs afterwards and only changes the grip position.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    internal sealed class VeyrBladeGripCorrection : MonoBehaviour
    {
        private const float InwardOffset = 0.10f;
        private const float UpOffset = 0.018f;
        private const float RefreshInterval = 0.5f;
        private const string HostName = "DF_VeyrBladeGripCorrection";

        private sealed class GripRig
        {
            public HeroIdentity Identity;
            public Transform Model;
            public Transform LeftGrip;
            public Transform RightGrip;
            public Vector3 LeftRest;
            public Vector3 RightRest;
        }

        private readonly Dictionary<int, GripRig> rigs = new Dictionary<int, GripRig>();
        private readonly List<int> stale = new List<int>();
        private float nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject existing = GameObject.Find(HostName);
            if (existing != null && existing.GetComponent<VeyrBladeGripCorrection>() != null) return;

            GameObject host = new GameObject(HostName);
            Object.DontDestroyOnLoad(host);
            host.AddComponent<VeyrBladeGripCorrection>();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshInterval;
                RefreshRigs();
            }

            foreach (GripRig rig in rigs.Values)
            {
                if (rig == null || rig.Identity == null || rig.Model == null) continue;

                // Veyr's left side is negative model-X, so +X moves that weapon inward.
                // The right side is mirrored. A slight upward correction centres the
                // gold-wrapped handle vertically in the closed fist shown in the reference.
                ApplyOffset(rig.LeftGrip, rig.LeftRest, rig.Model,
                    new Vector3(InwardOffset, UpOffset, 0f));
                ApplyOffset(rig.RightGrip, rig.RightRest, rig.Model,
                    new Vector3(-InwardOffset, UpOffset, 0f));
            }
        }

        private void RefreshRigs()
        {
            stale.Clear();
            foreach (KeyValuePair<int, GripRig> entry in rigs)
                if (entry.Value == null || entry.Value.Identity == null || entry.Value.Model == null)
                    stale.Add(entry.Key);
            foreach (int key in stale) rigs.Remove(key);

            foreach (HeroIdentity identity in Object.FindObjectsOfType<HeroIdentity>(true))
            {
                if (identity == null || identity.Hero != HeroId.Veyr) continue;
                int key = identity.GetInstanceID();
                if (rigs.ContainsKey(key)) continue;

                Transform model = FindDeep(identity.transform, "DF_AuthoredModel");
                if (model == null) continue;
                Transform left = FindDeep(model, "WeaponGripL");
                Transform right = FindDeep(model, "WeaponGripR");
                if (left == null || right == null) continue;

                rigs.Add(key, new GripRig
                {
                    Identity = identity,
                    Model = model,
                    LeftGrip = left,
                    RightGrip = right,
                    LeftRest = left.localPosition,
                    RightRest = right.localPosition
                });
            }
        }

        private static void ApplyOffset(Transform grip, Vector3 restPosition,
            Transform model, Vector3 modelLocalOffset)
        {
            if (grip == null || grip.parent == null || model == null) return;
            Vector3 worldOffset = model.TransformVector(modelLocalOffset);
            grip.localPosition = restPosition + grip.parent.InverseTransformVector(worldOffset);
        }

        private static Transform FindDeep(Transform root, string targetName)
        {
            if (root == null) return null;
            if (root.name == targetName) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
