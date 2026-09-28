using System.Reflection;
using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Uses the approved BombJetFlyby clip for every space-bomber pass without touching
    /// the bomber visual, route, bomb drops or gameplay logic.
    /// </summary>
    public sealed class SpaceBomberReferenceAudioOverride : MonoBehaviour
    {
        private const string ResourcePath = "Audio/BombJetFlyby";
        private const string ClipName = "BombJetFlyby";

        private static readonly FieldInfo EngineLoopField = typeof(SpaceBomberDirector).GetField(
            "engineLoop", BindingFlags.Instance | BindingFlags.NonPublic);

        private AudioClip referenceClip;
        private SpaceBomberDirector patchedDirector;
        private SpaceBomberFlyover patchedFlyover;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<SpaceBomberReferenceAudioOverride>() != null) return;

            GameObject root = new("DF_BomberReferenceAudio");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<SpaceBomberReferenceAudioOverride>();
        }

        private void Awake()
        {
            referenceClip = Resources.Load<AudioClip>(ResourcePath);

            // Fallback for Unity/Android import edge cases: search the Audio Resources folder
            // by clip name if the direct Resources path did not resolve.
            if (referenceClip == null)
            {
                AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio");
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null && clips[i].name == ClipName)
                    {
                        referenceClip = clips[i];
                        break;
                    }
                }
            }

            if (referenceClip != null) return;

            Debug.LogWarning(
                "DIMENSIONS FRONT: BombJetFlyby audio is not available in Resources/Audio. " +
                "The bomber will remain silent instead of using the removed procedural jet sound.");
            enabled = false;
        }

        private void Update()
        {
            if (referenceClip == null) return;

            PatchDirectorBeforeSpawn();
            PatchActiveFlyover();
        }

        private void PatchDirectorBeforeSpawn()
        {
            SpaceBomberDirector director = Object.FindAnyObjectByType<SpaceBomberDirector>();
            if (director == null)
            {
                patchedDirector = null;
                return;
            }

            if (director == patchedDirector) return;

            if (EngineLoopField == null)
            {
                Debug.LogError("DIMENSIONS FRONT: Could not access the bomber engine clip field.");
                return;
            }

            // Replace the generated legacy jet clip before the first bomber can spawn. The generated
            // clip is destroyed immediately and can therefore never be heard or overlap BombJetFlyby.
            AudioClip legacyClip = EngineLoopField.GetValue(director) as AudioClip;
            EngineLoopField.SetValue(director, referenceClip);
            if (legacyClip != null && legacyClip != referenceClip)
                Object.Destroy(legacyClip);

            patchedDirector = director;
        }

        private void PatchActiveFlyover()
        {
            SpaceBomberFlyover flyover = Object.FindAnyObjectByType<SpaceBomberFlyover>();
            if (flyover == null)
            {
                patchedFlyover = null;
                return;
            }

            if (flyover == patchedFlyover) return;

            AudioSource source = flyover.GetComponent<AudioSource>();
            if (source == null) return;

            // Normally the flyover already receives BombJetFlyby directly from the director. Keep this
            // as a safety net for scene-order edge cases, but never restart the clip unnecessarily.
            bool clipAlreadyCorrect = source.clip == referenceClip;
            if (!clipAlreadyCorrect)
            {
                source.Stop();
                source.clip = referenceClip;
            }

            source.loop = true;
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.pitch = 1.02f;

            // The reference recording already contains its own jet character, so do not run
            // the old procedural-engine EQ over it.
            AudioHighPassFilter high = flyover.GetComponent<AudioHighPassFilter>();
            if (high != null) high.enabled = false;

            AudioLowPassFilter low = flyover.GetComponent<AudioLowPassFilter>();
            if (low != null) low.enabled = false;

            if (!source.isPlaying)
                source.Play();
            if (Time.timeScale <= 0f)
                source.Pause();

            patchedFlyover = flyover;
        }
    }
}
