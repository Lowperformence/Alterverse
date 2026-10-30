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
            theme.FitLabel(new Rect(x + 24, 238, width - 48, 40), "GOOGLE PLAY", theme.Heading);
            var service = AccountServerBootstrap.Instance;
            bool verified = service?.GooglePlayAccountReady == true;
            theme.Label(new Rect(x + 24, 292, width - 48, 44),
                verified ? "Loading your saved progress..." : service?.Busy == false ?
                    "Sign in to load your Google Play profile." : "Loading your Google Play profile...",
                theme.Body, true);
            theme.Label(new Rect(x + 24, 344, width - 48, 72),
                verified ? PaidShardCommerce.Instance?.Status ?? service.Status :
                    service?.GoogleStatus ?? "Connecting to Google Play Games...", theme.Small, true);
            if (theme.Action(new Rect(x + 24, 428, width - 48, 44), verified ? "TRY AGAIN" : "CONNECT GOOGLE", true,
                enabled: service != null && !service.Busy))
            {
                if (verified) PaidShardCommerce.Instance?.Refresh();
                else _ = service.ConnectGoogleAsync();
            }
        }
    }
}
