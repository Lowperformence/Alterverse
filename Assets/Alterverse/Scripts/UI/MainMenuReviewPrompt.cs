using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        private bool reviewPromptOpen, reviewRequestBusy, reviewStoreFallback;
        private string reviewPromptAccount;
        private string reviewPromptMessage = "Already left a review? Tap ALREADY RATED.";
        public static bool ReviewPromptVisible => IsVisible &&
            (instance.reviewPromptOpen || instance.reviewRequestBusy) &&
            instance.reviewPromptAccount == AccountProfilePrefs.Account;

        private void UpdateReviewPrompt()
        {
            if (reviewPromptOpen && (reviewPromptAccount != AccountProfilePrefs.Account ||
                ReviewPromptProgress.AlreadyRated))
                reviewPromptOpen = false;
            if (reviewPromptOpen || reviewRequestBusy || launching || choosingMatchMode || MatchExit.Leaving ||
                CoopAuthority.Active || RankedPanel.Visible || !AccountServerBootstrap.CloudProfileReady ||
                Time.unscaledTime - pageChangedAt < .5f ||
                Screen.safeArea.width <= 0 || Screen.safeArea.height <= 0)
                return;
            int completedMatches = Alterverse.Profile.MatchRewardService.MatchesCompleted;
            if (!ReviewPromptProgress.ShouldOffer(completedMatches)) return;

            reviewPromptAccount = AccountProfilePrefs.Account;
            reviewPromptOpen = true;
            reviewStoreFallback = false;
            reviewPromptMessage = "Already left a review? Tap ALREADY RATED.";
            // Consume only this invitation. Dismissal does not mean the player rated.
            ReviewPromptProgress.MarkOffered(completedMatches);
            GUIUtility.hotControl = 0;
        }

        private async void RequestStoreReview()
        {
            if (reviewRequestBusy) return;
            string account = reviewPromptAccount;
            reviewRequestBusy = true;
            StoreReviewResult result;
            try { result = await NativeStoreReview.RequestAsync(); }
            finally { reviewRequestBusy = false; }
            if (this == null || account != AccountProfilePrefs.Account ||
                ReviewPromptProgress.AlreadyRated) return;

            // A finished native flow is not proof that the card appeared or a rating
            // was submitted. Offer an explicit store fallback, never an automatic redirect.
            reviewStoreFallback = true;
            reviewPromptMessage = result == StoreReviewResult.FlowFinished
                ? "If you rated, tap ALREADY RATED. Otherwise, use Google Play."
                : "You can also leave your review directly on Google Play.";
            reviewPromptOpen = true;
        }

        private static void DrawReviewStars(float centerX, float centerY)
        {
            // Five gold stars drawn as geometry, independent of font glyph coverage.
            Color gold = new Color(1f, .79f, .27f);
            for (int star = 0; star < 5; star++)
            {
                Vector2 center = new Vector2(centerX + (star - 2) * 62, centerY);
                for (int point = 0; point < 10; point++)
                {
                    float a = point * Mathf.PI / 5 - Mathf.PI * .5f;
                    float b = (point + 1) * Mathf.PI / 5 - Mathf.PI * .5f;
                    float r1 = point % 2 == 0 ? 25 : 11;
                    float r2 = point % 2 == 0 ? 11 : 25;
                    CombatGlyphs.Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r1,
                        center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r2, 4, gold);
                }
            }
        }

        private void DrawReviewPrompt()
        {
            GUI.color = Color.white;
            GUI.enabled = true;
            FrontEndTheme.Fill(new Rect(0, 0, menuWidth, MenuHeight), new Color(0, 0, 0, .72f));
            float width = Mathf.Min(760, menuWidth - 64), x = (menuWidth - width) * .5f;
            theme.Panel(new Rect(x, 82, width, 564));
            theme.Label(new Rect(x + 32, 106, width - 64, 66), "GALACTIC SUPPORT", theme.Title);
            DrawReviewStars(menuWidth * .5f, 207);
            theme.Label(new Rect(x + 32, 249, width - 64, 40), "RATE DIMENSIONS FRONT", theme.Heading);
            theme.Label(new Rect(x + 32, 299, width - 64, 100),
                "Thank you for playing! Give us some galactic support by rating DIMENSIONS FRONT 5 stars on Google Play. Your support helps our galaxy grow!",
                theme.Body);
            theme.Label(new Rect(x + 32, 403, width - 64, 28),
                reviewRequestBusy ? "Opening Google Play rating..." : reviewPromptMessage, theme.Small, true);

            if (theme.Action(new Rect(x + 32, 443, width - 64, 58),
                reviewStoreFallback ? "OPEN GOOGLE PLAY" : "RATE 5 STARS", true,
                enabled: !reviewRequestBusy))
            {
                if (reviewStoreFallback) { reviewPromptOpen = false; NativeStoreReview.OpenStore(); }
                else RequestStoreReview();
            }
            float buttonWidth = (width - 80) * .5f;
            if (theme.Action(new Rect(x + 32, 513, buttonWidth, 50), "NOT NOW",
                enabled: !reviewRequestBusy))
                reviewPromptOpen = false;
            if (theme.Action(new Rect(x + 48 + buttonWidth, 513, buttonWidth, 50), "ALREADY RATED",
                enabled: !reviewRequestBusy))
            {
                ReviewPromptProgress.ConfirmAlreadyRated();
                reviewPromptOpen = false;
            }
            if (theme.Action(new Rect(x + 32, 577, width - 64, 42), "RATE ON GOOGLE PLAY",
                enabled: !reviewRequestBusy))
            {
                reviewPromptOpen = false;
                NativeStoreReview.OpenStore();
            }
        }
    }
}

