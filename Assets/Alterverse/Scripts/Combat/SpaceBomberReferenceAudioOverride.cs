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

        private AudioClip referenceClip;
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
                "The bomber keeps its built-in fallback engine sound.");
            enabled = false;
        }

        private void Update()
        {
            if (referenceClip == null) return;

            SpaceBomberFlyover flyover = Object.FindAnyObjectByType<SpaceBomberFlyover>();
            if (flyover == null)
            {
                patchedFlyover = null;
                return;
            }

            if (flyover == patchedFlyover) return;

            AudioSource source = flyover.GetComponent<AudioSource>();
            if (source == null) return;

            // Change only the audio clip. The actual bomber object and its gameplay stay intact.
            source.Stop();
            source.clip = referenceClip;
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

            source.Play();
            if (Time.timeScale <= 0f) source.Pause();

            patchedFlyover = flyover;
        }
    }
}
