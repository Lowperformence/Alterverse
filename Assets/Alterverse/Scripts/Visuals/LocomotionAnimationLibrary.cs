using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Free CC0 source clips sampled into model-independent anatomical frames.</summary>
    public static class LocomotionAnimationLibrary
    {
        [Serializable]
        public sealed class Clip
        {
            public string name;
            public int frames;
            public float fps;
            public float stride;
            public float sourceLegLength, pelvisMean;
            public float[] rotations, positions, floor, heights, pelvisHeights;
            public int[] contacts;
            public Quaternion Rotation(int bone, float phase)
            {
                float frame = Mathf.Repeat(phase, 1f) * (frames - 1);
                int first = Mathf.FloorToInt(frame), next = Mathf.Min(first + 1, frames - 1);
                int a = (first * 6 + bone) * 4, b = (next * 6 + bone) * 4;
                return Quaternion.Slerp(new Quaternion(rotations[a], rotations[a+1], rotations[a+2], rotations[a+3]),
                    new Quaternion(rotations[b], rotations[b+1], rotations[b+2], rotations[b+3]), frame - first);
            }
            public Vector3 Position(int bone, float phase)
            {
                float frame = Mathf.Repeat(phase, 1f) * (frames - 1);
                int first = Mathf.FloorToInt(frame), next = Mathf.Min(first + 1, frames - 1);
                int a = (first * 6 + bone) * 3, b = (next * 6 + bone) * 3;
                return Vector3.Lerp(new Vector3(positions[a], positions[a+1], positions[a+2]),
                    new Vector3(positions[b], positions[b+1], positions[b+2]), frame - first);
            }
            public float Contact(int side, float phase)
            {
                float frame = Mathf.Repeat(phase, 1f) * (frames - 1);
                int first = Mathf.FloorToInt(frame), next = Mathf.Min(first + 1, frames - 1);
                return Mathf.Lerp(contacts[first * 2 + side], contacts[next * 2 + side], frame - first);
            }
            public float Height(int side, float phase)
            {
                float frame = Mathf.Repeat(phase, 1f) * (frames - 1);
                int first = Mathf.FloorToInt(frame), next = Mathf.Min(first + 1, frames - 1);
                return Mathf.Lerp(heights[first * 2 + side], heights[next * 2 + side], frame - first);
            }
            public float PelvisHeight(float phase)
            {
                float frame = Mathf.Repeat(phase, 1f) * (frames - 1);
                int first = Mathf.FloorToInt(frame), next = Mathf.Min(first + 1, frames - 1);
                return Mathf.Lerp(pelvisHeights[first], pelvisHeights[next], frame - first);
            }
        }
        [Serializable]
        private sealed class Bank
        {
            public int version;
            public string[] bones;
            public float sourceLegLength;
            public float[] restPositions;
            public Clip[] clips;
        }
        public static readonly string[] Bones = { "ThighL", "ShinL", "FootL", "ThighR", "ShinR", "FootR" };
        private static readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        private static Bank bank;
        private static bool attempted;
        public static bool Ready { get { Load(); return bank != null; } }
        public static float SourceLegLength { get { Load(); return bank != null ? bank.sourceLegLength : 1f; } }
        public static Vector3 Rest(int bone)
        {
            Load();
            if (bank == null) return Vector3.zero;
            int at = bone * 3;
            return new Vector3(bank.restPositions[at], bank.restPositions[at+1], bank.restPositions[at+2]);
        }
        public static Clip Find(string name) { Load(); return clips.TryGetValue(name, out Clip clip) ? clip : null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { attempted = false; bank = null; clips.Clear(); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Load()
        {
            if (attempted) return;
            attempted = true;
            TextAsset asset = Resources.Load<TextAsset>("LocomotionAnimation/Library");
            if (asset == null) { Debug.LogError("DF: locomotion library v2 is missing; reimport LocomotionAnimation/Library.bytes."); return; }
            try
            {
                Bank loaded;
                using (var input = new MemoryStream(asset.bytes))
                using (var zip = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(zip)) loaded = JsonUtility.FromJson<Bank>(reader.ReadToEnd());
                if (loaded == null || loaded.version != 2 || loaded.bones == null || loaded.bones.Length != 6 ||
                    loaded.restPositions == null || loaded.restPositions.Length != 18 || loaded.clips == null ||
                    !Finite(loaded.sourceLegLength) || loaded.sourceLegLength < .05f)
                    throw new InvalidDataException("Invalid locomotion schema");
                for (int i = 0; i < 6; i++) if (loaded.bones[i] != Bones[i]) throw new InvalidDataException("Locomotion bone order mismatch");
                foreach (float value in loaded.restPositions) if (!Finite(value)) throw new InvalidDataException("Non-finite rest position");
                foreach (Clip clip in loaded.clips)
                {
                    if (clip == null || string.IsNullOrEmpty(clip.name) || clip.frames < 3 || !Finite(clip.fps) || clip.fps <= 0f ||
                        !Finite(clip.stride) || clip.stride < .3f || clip.stride > 5f ||
                        !Finite(clip.sourceLegLength) || clip.sourceLegLength < .05f || !Finite(clip.pelvisMean) ||
                        clip.rotations == null || clip.rotations.Length != clip.frames * 24 ||
                        clip.positions == null || clip.positions.Length != clip.frames * 18 ||
                        clip.contacts == null || clip.contacts.Length != clip.frames * 2 || clip.floor == null || clip.floor.Length != 2 ||
                        clip.heights == null || clip.heights.Length != clip.frames * 2 ||
                        clip.pelvisHeights == null || clip.pelvisHeights.Length != clip.frames)
                        throw new InvalidDataException("Invalid locomotion clip");
                    for (int at = 0; at < clip.rotations.Length; at += 4)
                    {
                        float norm = 0f;
                        for (int j = 0; j < 4; j++) { float value = clip.rotations[at+j]; if (!Finite(value)) throw new InvalidDataException("Non-finite rotation"); norm += value * value; }
                        if (Mathf.Abs(norm - 1f) > .005f) throw new InvalidDataException("Non-unit locomotion rotation");
                    }
                    foreach (float value in clip.positions) if (!Finite(value)) throw new InvalidDataException("Non-finite trajectory");
                    foreach (float value in clip.floor) if (!Finite(value)) throw new InvalidDataException("Non-finite foot floor");
                    foreach (float value in clip.heights) if (!Finite(value) || value < -.0001f) throw new InvalidDataException("Invalid foot lift");
                    foreach (float value in clip.pelvisHeights) if (!Finite(value) || Mathf.Abs(value) > clip.sourceLegLength)
                        throw new InvalidDataException("Invalid pelvis motion");
                    foreach (int value in clip.contacts) if (value != 0 && value != 1) throw new InvalidDataException("Invalid foot contact");
                    clips.Add(clip.name, clip);
                }
                foreach (string required in new[] { "walk", "run", "left", "right", "back" })
                    if (!clips.ContainsKey(required)) throw new InvalidDataException("Missing directional locomotion clip: " + required);
                bank = loaded;
            }
            catch (Exception error) { bank = null; clips.Clear(); Debug.LogError("DF: locomotion library v2 failed; retaining existing pose fallback: " + error.Message); }
        }
    }
}
