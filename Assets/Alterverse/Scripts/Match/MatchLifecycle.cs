using UnityEngine;
namespace Alterverse.Match
{
    [DefaultExecutionOrder(-1000)]
    public sealed class MatchLifecycle : MonoBehaviour
    {
        private bool focused=true,suspended;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {if(FindAnyObjectByType<MatchLifecycle>()==null)new GameObject("DF_MatchLifecycle").AddComponent<MatchLifecycle>();}
        private void Awake(){DontDestroyOnLoad(gameObject);}
        private void OnApplicationFocus(bool value){focused=value;Refresh();}
        private void OnApplicationPause(bool value){suspended=value;Refresh();}
        private void Refresh()
        {
            bool background=!focused||suspended;
            MatchClock.Set(PauseReason.Background,background);
            AudioListener.pause=background;
            if(background)Alterverse.Profile.AccountProfilePrefs.Save();
        }
    }
}

