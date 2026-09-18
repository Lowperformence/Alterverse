using Alterverse.UI;
using UnityEngine;
namespace Alterverse.Onboarding
{
    public sealed class OnboardingHud:MonoBehaviour
    {
        private FrontEndTheme theme;
        private GUIStyle title,body;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate(){if(FindAnyObjectByType<OnboardingHud>()==null)new GameObject("DF_Onboarding").AddComponent<OnboardingHud>();}
        private void OnGUI()
        {
            if(OnboardingProgress.Complete||Time.timeScale<=0||MainMenuController.IsVisible)return;
            CombatHudLayout layout=new CombatHudLayout(Screen.safeArea);
            theme??=new FrontEndTheme();title??=new GUIStyle(theme.Small){fontStyle=FontStyle.Bold,fontSize=13};body??=new GUIStyle(theme.Small){fontSize=14};
            Matrix4x4 before=GUI.matrix;
            try
            {
                GUI.matrix=layout.Matrix;
                // Leave both thumb zones and the center-top match/objective header clear.
                Rect r=new Rect((layout.Width-300)*.5f,layout.Height-124,300,104);theme.Panel(r);
                OnboardingStep step=OnboardingProgress.Current;
                theme.Label(new Rect(r.x+12,r.y+8,204,32),OnboardingCopy.Title(step),title);
                if(theme.Action(new Rect(r.xMax-76,r.y+4,72,44),"SKIP"))OnboardingProgress.Skip();
                theme.Label(new Rect(r.x+12,r.y+44,276,56),OnboardingCopy.Body(step),body,true);
            }
            finally{GUI.matrix=before;}
        }
    }
}
