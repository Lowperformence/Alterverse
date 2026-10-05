using System.Reflection;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Settings-only exit action for desktop/mobile builds with a deliberate second-tap confirmation.</summary>
    [DefaultExecutionOrder(950)]
    public sealed class MainMenuQuitButton : MonoBehaviour
    {
        private static MainMenuQuitButton instance;
        private static FieldInfo pageField;

        private MainMenuController menu;
        private FrontEndTheme theme;
        private bool confirming;
        private float confirmUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (instance == null && FindAnyObjectByType<MainMenuQuitButton>() == null)
                new GameObject("DF_MainMenuQuitButton").AddComponent<MainMenuQuitButton>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private bool SettingsPageVisible()
        {
            if (!MainMenuController.IsVisible) return false;
            menu ??= FindAnyObjectByType<MainMenuController>();
            if (menu == null) return false;

            pageField ??= typeof(MainMenuController).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic);
            object value = pageField?.GetValue(menu);
            return value != null && value.ToString() == "Settings";
        }

        private void OnGUI()
        {
            if (OnlinePartyOverlay.IsOpen || !SettingsPageVisible())
            {
                confirming = false;
                return;
            }

            if (confirming && Time.unscaledTime > confirmUntil)
                confirming = false;

            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) return;

            const float menuHeight = 720f;
            float scale = Mathf.Min(safe.height / menuHeight, safe.width / 1500f);
            if (scale <= .001f) return;

            float menuWidth = safe.width / scale;
            float contentWidth = Mathf.Min(1360f, menuWidth - 288f);
            float left = (menuWidth - contentWidth + 240f) * .5f;
            float settingsWidth = contentWidth * .64f;
            float x = left + settingsWidth + 32f;
            float width = contentWidth - settingsWidth - 32f;
            float y = Screen.height - safe.yMax + (safe.height - menuHeight * scale) * .5f;

            theme ??= new FrontEndTheme();
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            try
            {
                GUI.depth = -4500;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0f), Quaternion.identity, Vector3.one * scale);

                theme.Panel(new Rect(x - 14f, 544f, width + 28f, 120f));
                theme.Label(new Rect(x, 558f, width, 22f), "APPLICATION", theme.Small, true);
                string label = confirming ? "CONFIRM CLOSE GAME" : "CLOSE GAME";
                if (theme.Action(new Rect(x, 586f, width, 54f), label, primary: confirming))
                {
                    if (!confirming)
                    {
                        confirming = true;
                        confirmUntil = Time.unscaledTime + 4f;
                    }
                    else
                    {
                        Alterverse.Profile.AccountProfilePrefs.Save();
                        Application.Quit();
                    }
                }

                if (confirming)
                    theme.Label(new Rect(x, 644f, width, 24f), "Tap again to exit DIMENSIONS FRONT.", theme.Small, true);
            }
            finally
            {
                GUI.depth = oldDepth;
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
            }
        }
    }
}


