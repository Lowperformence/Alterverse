using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Alterverse.Online;
using Alterverse.Profile;
using Alterverse.Shop;
using GooglePlayGames;
using UnityEngine;
using UnityEngine.Purchasing;

static class Regression
{
    static int checks;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Tick(GooglePlayShardStore shop,float seconds=0)
    {Time.unscaledTime+=seconds;typeof(GooglePlayShardStore).GetMethod("Update",Private).Invoke(shop,null);}
    static void Destroy(GooglePlayShardStore shop)=>typeof(GooglePlayShardStore).GetMethod("OnDestroy",Private).Invoke(shop,null);
    static GooglePlayShardStore New(bool connection=false,bool products=false,bool purchases=false)
    {
        StoreController.Created.Clear();StoreController.ConnectImmediately=connection;
        StoreController.AutoProducts=products;StoreController.AutoPurchases=purchases;Time.unscaledTime=0;
        AccountServerBootstrap.Instance=new(){GooglePlayAccountReady=true};
        PaidShardCommerce.Instance=new(){Ready=true,PurchasesReady=true};
        return new GameObject().AddComponent<GooglePlayShardStore>();
    }
    static async Task Main()
    {
        var shop=New();Tick(shop);var first=StoreController.Created[0];
        Check(shop.Status.Contains("Connecting")&&shop.PendingRecovery,"Real adapter starts connection and blocks checkout during recovery");
        Tick(shop,31);
        Check(shop.Status.Contains("timed out")&&!shop.Ready,"Missing native Connect callback times out instead of freezing");
        StoreController.ConnectImmediately=StoreController.AutoProducts=StoreController.AutoPurchases=true;
        Tick(shop,15);var replacement=StoreController.Created[1];
        Check(shop.Ready&&!shop.PendingRecovery&&shop.CanBuy("dimensions_shards_500"),"Automatic retry fully recovers prices and purchasing");
        first.Connection.SetResult(true);await Task.Delay(10);
        Check(first.ProductFetches==0&&replacement.ProductFetches==1,"Late abandoned connection never fetches products or clobbers the replacement");
        Destroy(shop);

        shop=New();Tick(shop);first=StoreController.Created[0];shop.Reconnect();
        Tick(shop);Check(StoreController.Created.Count==2,"RECONNECT can cancel a hung startup immediately");
        first.Connection.SetException(new Exception("late native failure"));await Task.Delay(10);
        Check(!shop.Ready,"Late failure from cancelled connection cannot mark the new store ready");Destroy(shop);

        shop=New(true);Tick(shop);first=StoreController.Created[0];
        var oldProducts=first.CapturedProducts;
        Check(shop.Status.Contains("prices"),"UI distinguishes product lookup from native connection");
        Tick(shop,31);Check(shop.Status.Contains("product loading timed out"),"Lost product callback is bounded");
        StoreController.AutoProducts=StoreController.AutoPurchases=true;Tick(shop,15);
        string healthy=shop.Status;oldProducts(new List<Product>());
        Check(shop.Ready&&shop.Status==healthy,"A previously queued callback cannot alter the new connection");Destroy(shop);

        shop=New(true,true);Tick(shop);
        Check(shop.Ready&&shop.PendingRecovery&&!shop.CanBuy("dimensions_shards_500"),"Prices alone cannot enable checkout before receipt recovery");
        Tick(shop,31);Check(shop.Status.Contains("purchase recovery timed out"),"Lost receipt-recovery callback is bounded");Destroy(shop);

        shop=New();Tick(shop);first=StoreController.Created[0];first.Drop();await Task.Delay(10);
        StoreController.ConnectImmediately=StoreController.AutoProducts=StoreController.AutoPurchases=true;
        Tick(shop,16);Check(shop.Ready,"Disconnect callback releases an incomplete Connect task so retry can proceed");Destroy(shop);

        shop=New(true,true,true);Tick(shop);var current=StoreController.Created[0];
#if !DF_GOOGLE_PLAY_SHARD_TEST
        AccountServerBootstrap.Instance.GooglePlayAccountReady=false;
        Check(!shop.CanBuy("dimensions_shards_500"),"Unlinked identity cannot purchase");
        AccountServerBootstrap.Instance.GooglePlayAccountReady=true;PaidShardCommerce.Instance.PurchasesReady=false;
        Check(!shop.CanBuy("dimensions_shards_500"),"Billing-disabled backend cannot purchase");
        PaidShardCommerce.Instance.PurchasesReady=true;
        PaidShardCommerce.Instance.Fulfillment=new();current.Pending("one-token");current.Pending("one-token");
        Check(shop.Busy&&PaidShardCommerce.Instance.Fulfillments==1,"Duplicate native callbacks start one server verification");
        int created=StoreController.Created.Count;shop.Reconnect();Tick(shop,31);
        Check(StoreController.Created.Count==created&&current.Confirmations==0,"Reconnect/watchdog cannot abandon an active verification or acknowledge early");
        PaidShardCommerce.Instance.Fulfillment.SetResult(true);await Task.Delay(10);
        Check(current.Confirmations==1&&!shop.Busy&&!shop.PendingRecovery,"Purchase acknowledges exactly once after durable server success");
        PaidShardCommerce.Instance.SessionVersion++;Tick(shop);
        Check(StoreController.Created.Count==2,"Changing authenticated session binds a fresh store controller");
#else
        current.Pending("test-token");await Task.Delay(10);
        Check(PlayerWallet.TestGrants==1&&current.Confirmations==1,"Explicit internal-test build keeps its isolated local fulfillment path");
#endif
        Destroy(shop);

        shop=New(true,true,true);Tick(shop);current=StoreController.Created[0];
        Check(current.Definitions.Exists(p=>p.id==ShardPacks.Subscription.Id),"Monthly subscription is requested from Google alongside packs");
        var monthly=new Product{definition=new(ShardPacks.Subscription.Id,ProductType.Subscription),
            metadata=new GoogleProductMetadata{localizedPrice=7.49m,localizedPriceString="7.49 EUR"}};
        var once=new Product{metadata=new ProductMetadata{localizedPrice=5m}};
        current.CapturedProducts(new(){once,monthly});
        Check(shop.SubscriptionDiscount==37,"Discount rounds down against the actual best same-currency pack price");
        Check(!shop.CanBuy(ShardPacks.Subscription.Id),"Missing renewal backend keeps subscription checkout disabled");
#if !DF_GOOGLE_PLAY_SHARD_TEST
        PaidShardCommerce.Instance.SubscriptionsReady=true;
        Check(shop.CanBuy(ShardPacks.Subscription.Id),"Configured server and valid monthly product enable subscription checkout");
        PaidShardCommerce.Instance.SubscriptionActive=true;
        Check(!shop.CanBuy(ShardPacks.Subscription.Id),"Active subscription cannot be purchased twice");
        PaidShardCommerce.Instance.SubscriptionActive=false;
        var restored=new Order{Info=new(){Google=new(){PurchaseToken="renewal-token"}},
            CartOrdered=new(){Contents=new(){new(){Product=monthly}}}};
        PaidShardCommerce.Instance.Fulfillment=new();
        current.Restore(restored);current.Restore(restored);
        Check(shop.PendingRecovery&&shop.Busy&&PaidShardCommerce.Instance.Fulfillments==1,"Confirmed subscription receipt is restored and deduplicated");
        PaidShardCommerce.Instance.Fulfillment.SetResult(true);await Task.Delay(10);
        Check(!shop.PendingRecovery&&!shop.Busy&&current.Confirmations==0,"Already confirmed subscription syncs without consuming or acknowledging twice");
#else
        PaidShardCommerce.Instance.SubscriptionsReady=true;
        Check(!shop.CanBuy(ShardPacks.Subscription.Id),"Local grant test mode cannot sell subscriptions");
#endif
        monthly.metadata.isoCurrencyCode="USD";current.CapturedProducts(new(){once,monthly});
        Check(shop.SubscriptionDiscount==0,"Different currencies never produce a discount claim");
        monthly.metadata.isoCurrencyCode="EUR";((GoogleProductMetadata)monthly.metadata).subscriptionPeriod="P1Y";
        current.CapturedProducts(new(){once,monthly});
        Check(shop.Price(ShardPacks.Subscription.Id)=="Unavailable"&&!shop.CanBuy(ShardPacks.Subscription.Id),"Annual or mismatched metadata cannot be sold as monthly");
        ((GoogleProductMetadata)monthly.metadata).subscriptionPeriod="P1M";
        ((GoogleProductMetadata)monthly.metadata).freeTrialPeriod="P7D";
        current.CapturedProducts(new(){once,monthly});
        Check(shop.Price(ShardPacks.Subscription.Id)=="Unavailable","Unsupported trial cannot silently replace the advertised recurring offer");
        Destroy(shop);

        Resources.Settings=new(){mWebClientId=""};PlayGamesPlatform.Instance=new();
        Check(GooglePlayAccountSession.NativeConfigurationError==""&&GooglePlayAccountSession.ConfigurationError.Contains("Web Client"),"Native sign-in and server authorization have distinct setup requirements");
        AccountServerBootstrap.Instance.GooglePlayAccountReady=false;
        AccountProfilePrefs.Attached=false;
        Check(GooglePlayLevelLeaderboard.IsConfigured,"A valid published game/leaderboard can be browsed without a Web Client ID");
        GooglePlayLevelLeaderboard.ShowLeaderboard();await Task.Delay(10);
        Check(PlayGamesPlatform.Instance.SignIns==1&&PlayGamesPlatform.Instance.Views==1,"Ranking button authenticates natively and opens Google ranking UI");
        Check(PlayGamesPlatform.Instance.Reports==0,"Viewing ranking never uploads unlinked/offline wins");
        Check(PlayGamesPlatform.Instance.ViewedId=="CgkI57GljPsdEAIQAA","Ranking uses the exact published Most Wins resource ID");
        Check(GooglePlayLevelLeaderboard.Status.Contains("Web Client"),"Unavailable score sync explains the exact missing setup");
        Resources.Settings.mWebClientId="1029475752167-example.apps.googleusercontent.com";
        AccountServerBootstrap.Instance.GooglePlayAccountReady=true;
        AccountProfilePrefs.Attached=true;AccountProfilePrefs.Account="different-account";
        GooglePlayLevelLeaderboard.SubmitCurrentScore();
        Check(PlayGamesPlatform.Instance.Reports==0,"Wrong account's cloud profile cannot publish wins");
        AccountProfilePrefs.Account=PaidShardCommerce.Instance.AccountHash;
        GooglePlayLevelLeaderboard.SubmitCurrentScore();GooglePlayLevelLeaderboard.SubmitCurrentScore();
        Check(PlayGamesPlatform.Instance.Reports==1&&PlayGamesPlatform.Instance.Score==7,"Verified cloud wins upload once after correct account linkage");
        Resources.Settings.mAppId="";
        Check(GooglePlayAccountSession.NativeConfigurationError.Contains("App ID"),"Missing native App ID remains a hard configuration error");
        Console.WriteLine("PASS "+checks+" Google Play connection/ranking checks; native SDK/network/device are fixtures.");
    }
}
