using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        public static bool AllowsHomeBanner => IsVisible && instance.page == Page.Home &&
            !instance.launching && !ReviewPromptVisible && !instance.languagePicker.IsOpen &&
            !OnlinePartyOverlay.IsOpen && !RankedPanel.Visible && !OnlineMatchLoadingScreen.IsVisible;
        private MenuBannerLayout HomeAdvertisingLayout => MenuBannerLayout.Calculate(Screen.safeArea,
            Screen.height, MenuBannerAds.DeviceScale);
        private bool HomeAdvertisingActive => HomeAdvertisingLayout.Valid;
        private Rect HomeBannerMenuRect => HomeAdvertisingLayout.InMenuCoordinates(Screen.safeArea, Screen.height);

        void DrawHomeAdvertising()
        {
            if (!AllowsHomeBanner) return;
            float density = MenuBannerAds.DeviceScale;
            var layout = MenuBannerLayout.Calculate(Screen.safeArea, Screen.height, density);
            if (!layout.Valid) return;
            var service = MenuBannerAds.Ensure();
            service.Request(layout.AdPixels, layout.LargeBanner);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                theme.Panel(layout.BoardPixels);
                var label = new GUIStyle(theme.Small) { fontSize = Mathf.Max(12, Mathf.RoundToInt(11 * density)) };
                theme.FitLabel(new Rect(layout.BoardPixels.x + 8 * density, layout.BoardPixels.y + 2 * density,
                    layout.BoardPixels.width - 16 * density, 20 * density), "ADVERTISEMENT", label, true);
                if (!service.Loaded)
                {
#if UNITY_EDITOR
                    const string status = "Banner preview — live ads run on Android.";
#else
                    string status = service.Status;
#endif
                    theme.Label(layout.AdPixels, status, label, true);
                }
            }
            finally { GUI.matrix = previous; }
        }
    }
}
