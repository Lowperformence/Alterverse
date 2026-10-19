using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        // 0 = no completed first match, 1 = waiting for menu, 2 = already offered.
        private const string ReviewPromptKey = "DF_FirstMatchReviewPrompt_v1";
        private bool reviewPromptOpen;
        private string reviewPromptAccount;
        public static bool ReviewPromptVisible => IsVisible && instance.reviewPromptOpen &&
            instance.reviewPromptAccount == AccountProfilePrefs.Account;

        private void UpdateReviewPrompt()
        {
            if (reviewPromptOpen && reviewPromptAccount != AccountProfilePrefs.Account)
                reviewPromptOpen = false;
            if (reviewPromptOpen || launching || choosingMatchMode || MatchExit.Leaving ||
                CoopAuthority.Active || RankedPanel.Visible || !AccountServerBootstrap.CloudProfileReady ||
                Time.unscaledTime - pageChangedAt < .5f ||
                Screen.safeArea.width <= 0 || Screen.safeArea.height <= 0)
                return;
            if (AccountProfilePrefs.GetInt(ReviewPromptKey, 0) != 1 ||
                Alterverse.Profile.MatchRewardService.MatchesCompleted < 1)
                return;

            reviewPromptAccount = AccountProfilePrefs.Account;
            reviewPromptOpen = true;
            // Save on display, including dismissal: this is a single invitation, not a nag.
            AccountProfilePrefs.SetInt(ReviewPromptKey, 2);
            AccountProfilePrefs.Save();
            GUIUtility.hotControl = 0;
        }

        private void DrawReviewPrompt()
        {
            GUI.color = Color.white;
            GUI.enabled = true;
            FrontEndTheme.Fill(new Rect(0, 0, menuWidth, MenuHeight), new Color(0, 0, 0, .72f));
            float width = Mathf.Min(760, menuWidth - 64), x = (menuWidth - width) * .5f;
            theme.Panel(new Rect(x, 164, width, 392));
            theme.Label(new Rect(x + 32, 190, width - 64, 66),
                "GALACTIC SUPPORT", theme.Title);
            theme.Label(new Rect(x + 32, 270, width - 64, 116),
                "Thank you for playing! Give us some galactic support by rating DIMENSIONS FRONT 5 stars on Google Play. Your support helps our galaxy grow!",
                theme.Body);
            theme.Label(new Rect(x + 32, 394, width - 64, 28),
                "Thank you for your support!",
                theme.Small, true);
            float buttonWidth = (width - 80) * .5f;
            if (theme.Action(new Rect(x + 32, 458, buttonWidth, 58),
                "RATE NOW", true))
            {
                reviewPromptOpen = false;
                Application.OpenURL("https://play.google.com/store/apps/details?id=" +
                    System.Uri.EscapeDataString(Application.identifier));
            }
            if (theme.Action(new Rect(x + 48 + buttonWidth, 458, buttonWidth, 58),
                "NOT NOW"))
                reviewPromptOpen = false;
        }
    }
}
