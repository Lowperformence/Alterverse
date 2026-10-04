using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Blender-baked anatomical frames, independent of imported bone axes and root motion.</summary>
    public static class CombatAnimationLibrary
    {
        [Serializable] public sealed class Clip
        {
            public string name;
            public int frames;
            public float fps;
            public bool loop;
            public int contact;
            public float[] rotations;
            public float Duration => (frames - 1) / fps;
            public Quaternion Sample(int bone, float frame, int boneCount)
            {
                frame = Mathf.Clamp(frame, 0, frames - 1);
                int first = Mathf.FloorToInt(frame), next = Mathf.Min(first + 1, frames - 1);
                int a = (first * boneCount + bone) * 4, b = (next * boneCount + bone) * 4;
                return Quaternion.Slerp(new Quaternion(rotations[a], rotations[a+1], rotations[a+2], rotations[a+3]),
                    new Quaternion(rotations[b], rotations[b+1], rotations[b+2], rotations[b+3]), frame - first);
            }
        }
        [Serializable] private sealed class Bank { public int version; public string[] bones; public Clip[] clips; }
        private static Bank bank;
        private static bool attempted;
        private static readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        private static readonly string[] BoneNames = { "Hips", "Spine", "Chest", "Head", "UpperArmL", "ForearmL", "HandL",
            "UpperArmR", "ForearmR", "HandR", "ThighL", "ShinL", "FootL", "ThighR", "ShinR", "FootR" };
        public static string[] Bones { get { Load(); return BoneNames; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            attempted = false; bank = null; clips.Clear();
        }
        public static Clip Find(string name) { Load(); return clips.TryGetValue(name, out Clip clip) ? clip : null; }
        private static void Load()
        {
            if (attempted) return;
            attempted = true;
            LoadStandingPose();
            TextAsset asset = Resources.Load<TextAsset>("CombatAnimation/Library");
            if (asset == null) { Debug.LogError("DF: missing Blender combat animation library; using legacy pose fallback."); return; }
            try
            {
                using (var input = new MemoryStream(asset.bytes))
                using (var zip = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(zip)) bank = JsonUtility.FromJson<Bank>(reader.ReadToEnd());
                if (bank == null || bank.version != 1 || bank.bones == null || bank.clips == null)
                    throw new InvalidDataException("Invalid combat library version/schema");
                if (bank.bones.Length != BoneNames.Length)
                    throw new InvalidDataException("Invalid combat bone count");
                for (int i = 0; i < BoneNames.Length; i++)
                    if (bank.bones[i] != BoneNames[i]) throw new InvalidDataException("Combat bone order mismatch");
                foreach (Clip clip in bank.clips)
                {
                    if (clip.frames < 2 || clip.fps <= 0 || clip.contact < 0 || clip.contact >= clip.frames ||
                        clip.rotations == null || clip.rotations.Length != clip.frames * bank.bones.Length * 4)
                        throw new InvalidDataException("Invalid combat clip: " + clip.name);
                    foreach (float value in clip.rotations)
                        if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite pose");
                    clips.Add(clip.name, clip);
                }

            }
            catch (Exception error) { bank = null; Debug.LogError("DF: combat library failed; standing pose remains available: " + error.Message); }
        }
        private static void LoadStandingPose()
        {
            TextAsset standing = Resources.Load<TextAsset>("CombatAnimation/StandingIdle");
            if (standing == null)
            {
                Debug.LogError("DF: Mixamo StandingIdle.json is MISSING. Update dev/vertical-slice and reimport Assets/Alterverse/Resources/CombatAnimation. Old guards would otherwise remain visible.");
                return;
            }
            try
            {
                Clip pose = JsonUtility.FromJson<Clip>(standing.text);
                if (pose == null || pose.name != "mixamo_standing_idle" || pose.frames != 2 || pose.fps <= 0 ||
                    pose.rotations == null || pose.rotations.Length != 2 * BoneNames.Length * 4)
                    throw new InvalidDataException("Invalid Mixamo standing pose");
                for (int i = 0; i < pose.rotations.Length; i += 4)
                {
                    float norm = 0f;
                    for (int j = 0; j < 4; j++) norm += pose.rotations[i+j] * pose.rotations[i+j];
                    if (float.IsNaN(norm) || float.IsInfinity(norm) || Mathf.Abs(norm-1f) > .001f)
                        throw new InvalidDataException("Invalid Mixamo standing rotation");
                }
                clips.Add(pose.name, pose);
            }
            catch (Exception error) { Debug.LogError("DF: Mixamo standing pose failed: " + error.Message); }
        }
        public static Quaternion Frame(Vector3 along, Vector3 forward)
        {
            if (along.sqrMagnitude < .000001f) along = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(along.normalized, forward.normalized)) > .98f) forward = Vector3.up;
            return Quaternion.LookRotation(along, forward);
        }
        // Anatomical left/right reflection, not a negative transform scale (which breaks normals/grips).
        public static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
        public static int MirrorBone(int index) => index >= 4 && index < 10 ? (index < 7 ? index + 3 : index - 3) :
            index >= 10 ? (index < 13 ? index + 3 : index - 3) : index;
        public static float SourceFrame(Clip clip, float age, float contactSeconds, float endSeconds)
        {
            if (age <= contactSeconds) return clip.contact * Mathf.Clamp01(age / Mathf.Max(.001f, contactSeconds));
            return Mathf.Lerp(clip.contact, clip.frames - 1,
                Mathf.Clamp01((age - contactSeconds) / Mathf.Max(.001f, endSeconds - contactSeconds)));
        }
    }
}
