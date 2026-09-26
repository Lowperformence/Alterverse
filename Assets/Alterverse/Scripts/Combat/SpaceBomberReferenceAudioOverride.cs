using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Replaces the procedural space-bomber engine tone with the approved reference fly-by
    /// recording while preserving the existing in-game distance/volume behaviour.
    /// </summary>
    public sealed class SpaceBomberReferenceAudioOverride : MonoBehaviour
    {
        private const string ResourcePath="Audio/BombJetFlyby";

        private AudioClip referenceClip;
        private SpaceBomberFlyover patchedFlyover;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(Object.FindAnyObjectByType<SpaceBomberReferenceAudioOverride>()!=null)return;

            GameObject root=new("DF_BomberReferenceAudio");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<SpaceBomberReferenceAudioOverride>();
        }

        private void Awake()
        {
            referenceClip=Resources.Load<AudioClip>(ResourcePath);
            if(referenceClip!=null)return;

            Debug.LogError("DIMENSIONS FRONT: Bomb jet reference audio could not be loaded from Resources/Audio/BombJetFlyby.");
            enabled=false;
        }

        private void Update()
        {
            if(referenceClip==null)return;

            SpaceBomberFlyover flyover=Object.FindAnyObjectByType<SpaceBomberFlyover>();
            if(flyover==null)
            {
                patchedFlyover=null;
                return;
            }

            if(flyover==patchedFlyover)return;

            AudioSource source=flyover.GetComponent<AudioSource>();
            if(source==null)return;

            source.Stop();
            source.clip=referenceClip;
            source.pitch=1f;
            source.dopplerLevel=0f;

            AudioHighPassFilter high=flyover.GetComponent<AudioHighPassFilter>();
            if(high!=null)high.enabled=false;

            AudioLowPassFilter low=flyover.GetComponent<AudioLowPassFilter>();
            if(low!=null)low.enabled=false;

            source.Play();
            if(Time.timeScale<=0f)source.Pause();

            patchedFlyover=flyover;
        }
    }
}
