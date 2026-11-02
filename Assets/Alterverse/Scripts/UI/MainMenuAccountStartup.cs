using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        string presentedAccount;

        // All profile-backed menu reads wait for the verified owner's cloud save.
        // Loading an old local hero/avatar/wallet first would expose a second account.
        bool PrepareAccountPresentation()
        {
            if (StartupAppUpdateManager.Instance != null && StartupAppUpdateManager.Instance.UpdateRequired)
            {
                presentedAccount = null;
                return false;
            }
            if (!AccountServerBootstrap.CloudProfileReady)
            {
                presentedAccount = null;
                return false;
            }
            string account = AccountProfilePrefs.Account ?? "local";
            if (presentedAccount == account) return true;
            selectedHero = Mathf.Clamp((int)PlayerHeroSelection.Selected, 0, HeroIdentity.HeroCount - 1);
            playArenaBrowseIndex = ArenaShopCatalog.IndexOf(ArenaVariantRuntime.Selected);
            mapShopPageStart = Mathf.Max(0, playArenaBrowseIndex - 1);
            master = PlayerProfile.MasterVolume;
            music = PlayerProfile.MusicVolume;
            sfx = PlayerProfile.SfxVolume;
            audioDirty = false;
            collection?.Dispose();
            collection = new CosmeticCollectionPanel();
            UniversalCosmeticSelection.EnsureMigrated((HeroId)selectedHero);
            showcase?.InvalidateLoadout();
            presentedAccount = account;
            return true;
        }

        void DrawAccountStartup()
        {
            float width = Mathf.Min(640, menuWidth - 64);
            float x = (menuWidth - width) * .5f;
            Rect panel = new Rect(x, 214, width, 276);
            theme.Panel(panel);

            var updater = StartupAppUpdateManager.Instance;
            if (updater != null && updater.UpdateRequired)
            {
                bool de = GameLanguage.Current != null && GameLanguage.Current.Code.StartsWith("de");
                theme.FitLabel(new Rect(x + 24, 238, width - 48, 40),
                    de ? "UPDATE ERFORDERLICH" : "UPDATE REQUIRED", theme.Heading);

                string updateHeadline = de
                    ? "Eine neue Version von DIMENSIONS FRONT ist verfügbar."
                    : "A new version of DIMENSIONS FRONT is available.";
                string updateDetail = !string.IsNullOrEmpty(updater.StatusMessage)
                    ? updater.StatusMessage
                    : (de
                        ? "Bitte aktualisiere das Spiel jetzt über Google Play, um weiterzuspielen (auch wenn automatische Updates deaktiviert sind)."
                        : "Please update the game now via Google Play to continue playing (even if automatic updates are disabled).");

                theme.Label(new Rect(x + 24, 292, width - 48, 44), updateHeadline, theme.Body, true);
                theme.Label(new Rect(x + 24, 344, width - 48, 72), updateDetail, theme.Small, true);

                string btnText = updater.Checking || updater.UpdateFlowActive
                    ? (de ? "PRÜFE UPDATE  •  BITTE WARTEN…" : "CHECKING UPDATE  •  PLEASE WAIT…")
                    : (de ? "JETZT AKTUALISIEREN" : "UPDATE NOW");
                if (theme.Action(new Rect(x + 24, 428, width - 48, 44), btnText, true,
                    enabled: !updater.Checking && !updater.UpdateFlowActive))
                {
                    updater.TriggerUpdateNow();
                }
                return;
            }

            theme.FitLabel(new Rect(x + 24, 238, width - 48, 40), "GOOGLE PLAY", theme.Heading);
            var service = AccountServerBootstrap.Instance;
            var commerce = PaidShardCommerce.Instance;
            bool verified = service?.GooglePlayAccountReady == true;
            string commerceStatus = commerce?.Status ?? string.Empty;
            bool commerceLoading = verified && (commerce == null ||
                commerceStatus.IndexOf("Synchronizing", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                commerceStatus.IndexOf("Connecting", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                commerceStatus.IndexOf("Loading", System.StringComparison.OrdinalIgnoreCase) >= 0);
            bool syncing = service == null || !service.StartupAttemptFinished || service.Busy || commerceLoading;

            string headline = syncing
                ? (verified ? "Synchronizing your saved cloud profile…" : "Signing in with Google Play Games…")
                : (verified ? "Cloud profile sync interrupted." : "Sign in to load your Google Play profile.");
            string detail = verified
                ? (commerce?.Status ?? service?.Status ?? "Loading your saved progress…")
                : (service?.GoogleStatus ?? "Connecting to Google Play Games…");

            theme.Label(new Rect(x + 24, 292, width - 48, 44), headline, theme.Body, true);
            theme.Label(new Rect(x + 24, 344, width - 48, 72), detail, theme.Small, true);

            if (syncing)
            {
                theme.Action(new Rect(x + 24, 428, width - 48, 44), "SIGNING IN  •  PLEASE WAIT…", false, enabled: false);
            }
            else if (theme.Action(new Rect(x + 24, 428, width - 48, 44), verified ? "TRY AGAIN" : "CONNECT GOOGLE", true,
                enabled: service != null && !service.Busy))
            {
                if (verified) commerce?.Refresh();
                else _ = service.ConnectGoogleAsync();
            }
        }
    }
}

