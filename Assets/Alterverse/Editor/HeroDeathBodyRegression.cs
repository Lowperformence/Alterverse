#if UNITY_EDITOR
using System;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alterverse.EditorChecks
{
    public static class HeroDeathBodyRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Hero death bodies (Play Mode)")]
        public static void Run()
        {
            Require(Application.isPlaying, "Start Play Mode first.");
            foreach (string path in new[] { "HeroArt/HeroSurface", "HeroArt/HeroUndersuitFabric",
                "HeroArt/VoidArmorEnergy", "HeroArt/VoidArmorHalo", "HeroArt/WeaponEnergyOverlay",
                "CombatVfx/HeroEnergy", "CyberShark/SharkSkin", "CyberShark/JacketFabric",
                "CyberShark/AnchorMetal", "DF_RuntimeLit", "DF_RuntimeUnlit" })
            {
                Shader shader = Resources.Load<Shader>(path);
                Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader import failed: " + path);
            }
            foreach (HeroId hero in Enum.GetValues(typeof(HeroId))) Check(hero);
            Debug.Log("PASS: death bodies for all heroes; isolated passive physics, independent limbs, gravity collapse, stable actor and renderer-local fade. Check collapse visually on Android and two online clients.");
        }

        private static void Check(HeroId hero)
        {
            GameObject actor = new GameObject("DF_DeathRegression_" + hero);
            HeroDeathBody corpse = null;
            try
            {
                actor.transform.position = Vector3.up * 4f;
                actor.AddComponent<HeroIdentity>().Configure(hero);
                actor.AddComponent<StylizedHeroVisual>().Build();
                Transform root = actor.transform.Find("DF_VisualRoot");
                Require(root != null, "Missing hero visual: " + hero);
                var existing = new System.Collections.Generic.HashSet<HeroDeathBody>(UnityEngine.Object.FindObjectsByType<HeroDeathBody>(FindObjectsSortMode.None));
                Vector3 originalPosition = actor.transform.position;
                HeroDeathBody.Spawn(actor);
                foreach (HeroDeathBody body in UnityEngine.Object.FindObjectsByType<HeroDeathBody>(FindObjectsSortMode.None))
                    if (!existing.Contains(body)) corpse = body;
                Require(corpse != null, "No death body: " + hero);
                Require(corpse.gameObject.scene.GetPhysicsScene() != Physics.defaultPhysicsScene,
                    "Ragdoll leaked into gameplay physics.");
                Rigidbody[] limbs = corpse.GetComponentsInChildren<Rigidbody>(true);
                Require(limbs.Length == 16, "Missing articulated ragdoll bodies: " + hero);
                Require(corpse.GetComponentsInChildren<CharacterJoint>(true).Length == 15, "Missing ragdoll joints.");
                Rigidbody hips = Array.Find(limbs, limb => limb.name == "DF_Ragdoll_Hips" || limb.name == "DF_Ragdoll_DF_PelvisJoint");
                Rigidbody arm = Array.Find(limbs, limb => limb.name == "DF_Ragdoll_ForearmL" || limb.name == "DF_Ragdoll_DF_ElbowJointL");
                Require(hips != null && arm != null, "Missing pelvis/arm bodies.");
                float initialHipY = hips.position.y;
                Quaternion initialRelativeArm = Quaternion.Inverse(hips.rotation) * arm.rotation;
                for (int step = 0; step < 90; step++) corpse.SendMessage("FixedUpdate");
                corpse.SendMessage("LateUpdate");
                Require(hips.position.y < initialHipY - .15f, "Pelvis did not fall under gravity: " + hero);
                Require(Quaternion.Angle(initialRelativeArm, Quaternion.Inverse(hips.rotation) * arm.rotation) > 5f,
                    "Limbs remained rigid relative to the torso: " + hero);
                foreach (Rigidbody limb in limbs)
                    Require(!limb.isKinematic && limb.useGravity && limb.constraints == RigidbodyConstraints.None,
                        "Ragdoll has a locked or driven body.");
                Require(corpse.GetComponentsInChildren<Animator>(true).Length == 0, "Corpse copied an animator.");
                Require(corpse.GetComponentsInChildren<MonoBehaviour>(true).Length == 1, "Corpse copied gameplay scripts.");
                Renderer[] copies = corpse.GetComponentsInChildren<Renderer>(true);
                Require(copies.Length > 0, "Empty corpse.");
                foreach (SkinnedMeshRenderer skin in corpse.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach (Transform bone in skin.bones)
                        Require(bone == null || bone.IsChildOf(corpse.transform), "Corpse references living skeleton.");

                var elapsed = typeof(HeroDeathBody).GetField("elapsed", BindingFlags.NonPublic | BindingFlags.Instance);
                elapsed.SetValue(corpse, HeroDeathBody.CollapseSeconds + HeroDeathBody.HoldSeconds + HeroDeathBody.FadeSeconds * .5f);
                corpse.SendMessage("LateUpdate");
                var block = new MaterialPropertyBlock();
                foreach (Renderer renderer in copies)
                {
                    renderer.GetPropertyBlock(block);
                    Require(block.GetFloat("_DFDeathFade") > 0f, "Corpse did not fade.");
                }
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.GetPropertyBlock(block);
                    Require(block.GetFloat("_DFDeathFade") == 0f, "Fade leaked to living hero.");
                }
                Require(actor.transform.position == originalPosition, "Death body moved gameplay actor.");
            }
            finally
            {
                if (corpse != null) UnityEngine.Object.DestroyImmediate(corpse.gameObject);
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
    }
}
#endif
