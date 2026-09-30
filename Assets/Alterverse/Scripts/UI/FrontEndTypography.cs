using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Bundled fonts work offline and on Android; no operating-system font lookup.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class FrontEndTypography : MonoBehaviour
    {
        private static Font body, display;
        private GUISkin configuredSkin;
        public static Font Body => body != null ? body : body = Resources.Load<Font>("FrontEnd/Rajdhani-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Font Display => display != null ? display : display = Resources.Load<Font>("FrontEnd/Rajdhani-SemiBold") ?? Body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("DF_Typography");
            DontDestroyOnLoad(host);
            host.AddComponent<FrontEndTypography>();
        }

        private void OnGUI()
        {
            // All existing IMGUI HUDs derive their styles from this skin. Configure it before
            // their first OnGUI, including menus instantiated later during a match.
            if (configuredSkin == GUI.skin) return;
            configuredSkin = GUI.skin;
            configuredSkin.font = Body;
            configuredSkin.label.font = Body;
            configuredSkin.box.font = Body;
            configuredSkin.button.font = Display;
            configuredSkin.toggle.font = Body;
            configuredSkin.textField.font = Body;
            configuredSkin.textArea.font = Body;
            foreach (var style in configuredSkin.customStyles)
                if (style != null) style.font = Body;
        }
    }
}
