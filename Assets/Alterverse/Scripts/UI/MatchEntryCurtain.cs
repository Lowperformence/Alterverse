using Alterverse.Match;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Survives menu deactivation; releases only after an initialized arena frame.</summary>
    public sealed class MatchEntryCurtain : MonoBehaviour
    {
        private static MatchEntryCurtain instance;
        private bool waiting;
        private int revealedFrame;
        private float fade;
        private FrontEndTheme theme;
        public static bool AwaitingArena => instance != null && instance.waiting;
        public static bool IsVisible => instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { instance = null; }

        public static void Begin()
        {
            if (instance == null)
                instance = new GameObject("DF_MatchEntryCurtain").AddComponent<MatchEntryCurtain>();
            instance.waiting = true;
            instance.fade = 1f;
        }

        private void Awake() { DontDestroyOnLoad(gameObject); }

        public static void Reveal()
        {
            if (instance == null || !instance.waiting) return;
            instance.waiting = false;
            instance.revealedFrame = Time.frameCount;
        }

        public static void Cancel()
        {
            if (instance == null) return;
            var old = instance;
            instance = null;
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }

        private void Update()
        {
            if (MatchExit.Leaving || MainMenuController.IsVisible) { Cancel(); return; }
            // Keep the first fully rendered arena frame covered; then dissolve into the arrival.
            if (!waiting && Time.frameCount > revealedFrame + 1 && !MatchClock.InBackground)
            {
                fade = Mathf.Max(0f, fade - Time.unscaledDeltaTime / .35f);
                if (fade <= 0f) Cancel();
            }
        }

        private void OnGUI()
        {
            if (instance != this) return;
            theme ??= new FrontEndTheme();
            MatchLoadingPresentation.Draw(theme, "PREPARING BATTLEFIELD",
                "Preparing your squad and transport", opacity: fade);
        }

        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
