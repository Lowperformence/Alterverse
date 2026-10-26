using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        void DrawRewardedShardPanel()
        {
            var ads = RewardedShardAds.Ensure();
            ads.Prepare();
            Rect panel = new Rect(Right - 250, 312, 250, 128);
            theme.Panel(panel);
            theme.Label(new Rect(panel.x + 16, 321, 218, 24), "EARN 2 SHARDS", theme.Heading);
            if (theme.Action(new Rect(panel.x + 16, 351, 218, 42), "▶ WATCH AD", true,
                enabled: ads.CanShow, accent: ShardShopVisualOverlay.TabAccent)) ads.Show();
            theme.Label(new Rect(panel.x + 16, 398, 218, 38), ads.Status, theme.Small, true);
            if (ads.PrivacyOptionsRequired &&
                theme.Action(new Rect(Right - 154, 271, 154, 28), "AD PRIVACY")) ads.ShowPrivacyOptions();
        }
    }
}
