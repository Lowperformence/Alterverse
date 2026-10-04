#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class AuthoredCombatRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/All authored hero combat clips")]
        public static void Run()
        {
            string[] names = CombatAnimationLibrary.Bones;
            Require(names != null && names.Length == 16, "Missing/invalid Blender combat library");
            string[] clips = { "idle_relaxed", "mixamo_standing_idle", "jab", "cross", "slash_a", "heavy_sweep", "spell" };
            foreach (string name in clips)
            {
                var clip = CombatAnimationLibrary.Find(name);
                Require(clip != null, "Missing clip " + name);
                foreach (float contact in new[] { .10f, .12f, .14f, .16f, .18f, .20f, .22f })
                {
                    Require(Mathf.Abs(CombatAnimationLibrary.SourceFrame(clip, contact, contact, contact+.46f)-clip.contact)<.0001f,
                        name + " gameplay/contact frame drift");
                    Require(CombatAnimationLibrary.SourceFrame(clip, 0, contact, contact+.46f)==0, "Invalid first frame");
                    Require(CombatAnimationLibrary.SourceFrame(clip, contact+.46f, contact, contact+.46f)==clip.frames-1,
                        "Invalid last frame");
                }
                for (int f = 0; f < clip.frames; f++) for (int b = 0; b < names.Length; b++)
                {
                    Quaternion q=clip.Sample(b, f, names.Length);
                    Require(Mathf.Abs(Quaternion.Dot(q,q)-1)<.001f, "Non-unit sampled rotation");
                    Require(Quaternion.Angle(q, CombatAnimationLibrary.Mirror(CombatAnimationLibrary.Mirror(q)))<.1f,
                        "Mirror does not preserve the pose");
                    Require(CombatAnimationLibrary.MirrorBone(CombatAnimationLibrary.MirrorBone(b))==b, "Invalid mirror bone");
                }
            }
            foreach (HeroId hero in Enum.GetValues(typeof(HeroId)))
            {
                for (int bone = 0; bone < names.Length; bone++)
                    Require(MeleeBasicAttackAnimation.UsesStandingPose(hero, bone) ==
                        (hero != HeroId.Nyra && !(hero == HeroId.Kaelon && bone >= 4 && bone <= 6)),
                        "Standing pose must exclude Sivra and Auren's entire shield arm");
                var model = Resources.Load<GameObject>("AuthoredHeroes/"+(hero==HeroId.Nerok?HeroId.Rook:hero));
                Require(model != null, "Missing authored model for "+hero);
                foreach (string name in names)
                {
                    bool found=false;
                    foreach(Transform t in model.GetComponentsInChildren<Transform>(true)) if(t.name==name){found=true;break;}
                    Require(found, hero+" missing retarget bone "+name);
                }
                Require(HeroAttackDelivery.Windup(hero)>0, "Invalid gameplay windup");
            }
            CombatMotionRegression.Run();
            Require(CombatAnimationLibrary.Find("idle_relaxed").loop, "Relaxed idle must loop");
            var standing = CombatAnimationLibrary.Find("mixamo_standing_idle");
            for (int bone = 0; bone < names.Length; bone++)
                Require(Quaternion.Angle(standing.Sample(bone, 0, names.Length), standing.Sample(bone, 1, names.Length)) < .1f,
                    "Starting pose must remain stationary");
            Debug.Log("DF: eight hero rigs, seven pose/clip assets, contact clocks, mirror mapping and IK regression passed. " +
                "Still inspect live weapon/shield clearance and network playback in Play Mode.");
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
