using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { AfterSceneLoad, SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute
    {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
    public class Object
    {public static void Destroy(Object value){} public static void DontDestroyOnLoad(Object value){}}
    public class MonoBehaviour:Object
    {
        public GameObject gameObject;
        public static T FindAnyObjectByType<T>() where T:class=>null;
    }
    public class GameObject:Object
    {
        public GameObject(string name=""){}
        public T AddComponent<T>() where T:MonoBehaviour,new()
        {
            var component=new T{gameObject=this};
            typeof(T).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(component,null);
            return component;
        }
    }
    public static class Application {public static void OpenURL(string url){}}
    public static class Time {public static float unscaledTime;}
    public static class Debug {public static void LogWarning(string message){}}
    public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}
    public sealed class PlayGamesSettings {public string mAppId="1029475752167",mWebClientId="";}
    public static class Resources
    {
        public static PlayGamesSettings Settings=new();
        public static object Load(string path)=>Settings;
    }
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
        public static string ToJson(object value)=>JsonSerializer.Serialize(value,value.GetType(),Options);
        public static T FromJson<T>(string value)=>JsonSerializer.Deserialize<T>(value,Options);
    }
}

namespace UnityEngine.Purchasing
{
    public enum ProductType { Consumable,Subscription }
    public sealed class ProductDefinition
    {public string id;public ProductDefinition(string id,ProductType type){this.id=id;}}
    public class ProductMetadata {public string localizedPriceString="4.99 EUR",isoCurrencyCode="EUR";public decimal localizedPrice=4.99m;}
    public class GoogleProductMetadata:ProductMetadata {public string subscriptionPeriod="P1M",freeTrialPeriod="";public int introductoryPriceCycles;}
    public sealed class Product
    {public bool availableToPurchase=true;public ProductDefinition definition=new("dimensions_shards_500",ProductType.Consumable);public ProductMetadata metadata=new();}
    public sealed class ProductFetchFailed{}
    public sealed class PurchasesFetchFailureDescription{}
    public sealed class StoreConnectionFailureDescription{}
    public sealed class GoogleOrderInfo {public string PurchaseToken;}
    public sealed class OrderInfo {public GoogleOrderInfo Google=new();}
    public sealed class CartItem {public Product Product=new();}
    public sealed class Cart {public List<CartItem> Contents=new(){new()};public List<CartItem> Items()=>Contents;}
    public class Order {public OrderInfo Info=new();public Cart CartOrdered=new();}
    public sealed class PendingOrder:Order{}
    public sealed class DeferredOrder:Order{}
    public enum PurchaseFailureReason { UserCancelled,Unknown }
    public sealed class FailedOrder:Order {public PurchaseFailureReason FailureReason;}
    public sealed class Orders {public List<PendingOrder> PendingOrders=new();public List<Order> ConfirmedOrders=new();}
    public sealed class GoogleExtendedService
    {public string Account;public void SetObfuscatedAccountId(string account){Account=account;}}
    public sealed class StoreController
    {
        public static readonly List<StoreController> Created=new();
        public static bool ConnectImmediately,AutoProducts,AutoPurchases;
        public readonly TaskCompletionSource<bool> Connection=new();
        public GoogleExtendedService GooglePlayStoreExtendedService=new();
        public int ProductFetches,PurchaseFetches,Checkouts,Confirmations;public List<ProductDefinition> Definitions;
        public event Action<List<Product>> OnProductsFetched;
        public event Action<ProductFetchFailed> OnProductsFetchFailed;
        public event Action<PendingOrder> OnPurchasePending;
        public event Action<Order> OnPurchaseConfirmed;
        public event Action<FailedOrder> OnPurchaseFailed;
        public event Action<DeferredOrder> OnPurchaseDeferred;
        public event Action<Orders> OnPurchasesFetched;
        public event Action<PurchasesFetchFailureDescription> OnPurchasesFetchFailed;
        public event Action<StoreConnectionFailureDescription> OnStoreDisconnected;
        public StoreController(string name){Created.Add(this);}
        public void ProcessPendingOrdersOnPurchasesFetched(bool value){}
        public Task Connect(){if(ConnectImmediately)Connection.TrySetResult(true);return Connection.Task;}
        public void FetchProducts(List<ProductDefinition> products)
        {Definitions=products;ProductFetches++;if(AutoProducts)Products();}
        public void FetchPurchases(){PurchaseFetches++;if(AutoPurchases)Purchases();}
        public void PurchaseProduct(string id){Checkouts++;}
        public void ConfirmPurchase(PendingOrder order){Confirmations++;OnPurchaseConfirmed?.Invoke(order);}
        public void Products()=>OnProductsFetched?.Invoke(new(){new()});
        public void Purchases(params PendingOrder[] pending)=>OnPurchasesFetched?.Invoke(new(){PendingOrders=new(pending)});
        public void Drop()=>OnStoreDisconnected?.Invoke(new());
        public void Pending(string token)=>OnPurchasePending?.Invoke(new(){Info=new(){Google=new(){PurchaseToken=token}}});
        public Action<List<Product>> CapturedProducts=>OnProductsFetched;
        public void Restore(Order order)=>OnPurchasesFetched?.Invoke(new(){ConfirmedOrders=new(){order}});
        public void Pending(PendingOrder order)=>OnPurchasePending?.Invoke(order);
    }
}

namespace GooglePlayGames.BasicApi
{public enum SignInStatus { Success,Failed } public enum LeaderboardTimeSpan { AllTime } public enum UIStatus { Valid,UserClosedUI,Error }}
namespace GooglePlayGames
{
    using GooglePlayGames.BasicApi;
    public sealed class LocalUser {public string id="google-a";}
    public sealed class PlayGamesPlatform
    {
        public static PlayGamesPlatform Instance=new();
        public bool Authenticated;public int SignIns,Views,Reports;public long Score;public string ViewedId;
        public int ManualSignIns;public bool DeferSignIn,ThrowOnSignIn;
        public SignInStatus SignInResult=SignInStatus.Success;public Action<SignInStatus> PendingSignIn;
        public LocalUser localUser=new();
        public static void Activate(){}
        public bool IsAuthenticated()=>Authenticated;
        public string GetUserDisplayName()=>"Player";
        public void Authenticate(Action<SignInStatus> done)
        {
            SignIns++;
            if(ThrowOnSignIn)throw new InvalidOperationException("sensitive-native-message");
            if(DeferSignIn){PendingSignIn=done;return;}
            Authenticated=SignInResult==SignInStatus.Success;done(SignInResult);
        }
        public void ManuallyAuthenticate(Action<SignInStatus> done){ManualSignIns++;Authenticate(done);}
        public void RequestServerSideAccess(bool refresh,Action<string> done)=>done("single-use-code");
        public void ReportScore(long score,string id,Action<bool> done){Reports++;Score=score;done(true);}
        public void ShowLeaderboardUI(string id,LeaderboardTimeSpan span,Action<UIStatus> done){Views++;ViewedId=id;done(UIStatus.UserClosedUI);}
    }
}

namespace Alterverse.Profile
{
    public sealed class AccountServerBootstrap
    {
        public static AccountServerBootstrap Instance;public bool Busy,GooglePlayAccountReady;
        public string GoogleStatus="Connect Google Play Games.";public string Status="Account unavailable.";
        public int GoogleConnections;public bool ReconnectSucceeds=true,ThrowOnReconnect;
        public TaskCompletionSource<bool> GoogleConnection;public Action OnGoogleConnected;
        public async Task<bool> ConnectGoogleAsync(bool restore=false)
        {
            GoogleConnections++;
            if(ThrowOnReconnect)throw new InvalidOperationException("sensitive-native-message");
            bool success=GoogleConnection!=null?await GoogleConnection.Task:ReconnectSucceeds;
            if(success){GooglePlayAccountReady=true;OnGoogleConnected?.Invoke();}
            else GoogleStatus="Google Play sign-in failed (Failed, 1). Tap RECONNECT to retry.";
            return success;
        }
    }
    public static class PlayerWallet {public static int TestGrants;public static bool GrantGooglePlayBillingTest(string id,string token){TestGrants++;return true;}}
    public static class PlayerProfile {public static string PlayerName=>"Player";}
    public static class AccountProfilePrefs {public static bool Attached;public static string Account;}
}
namespace Alterverse.Shop
{
    public sealed class ShardPack {public string Id{get;set;}="dimensions_shards_500";public int Amount{get;set;}=500;}
    public static class ShardPacks {public static ShardPack[] All={new()};public static ShardPack Subscription=new(){Id="dimensions_shards_monthly",Amount=1200};public static bool Contains(string id)=>id=="dimensions_shards_500";public static bool IsSubscription(string id)=>id==Subscription.Id;public static bool IsProduct(string id)=>Contains(id)||IsSubscription(id);}
    public sealed class PaidShardCommerce
    {
        public static PaidShardCommerce Instance;public bool Ready,PurchasesReady,SubscriptionsReady,SubscriptionActive;
        public string AccountHash="account-a",Status="Account unavailable.";public int SessionVersion=1;
        public int Fulfillments,Refreshes;public TaskCompletionSource<bool> Fulfillment;
        public Task<bool> Fulfill(string product,string token){Fulfillments++;return Fulfillment?.Task??Task.FromResult(true);}
        public void Refresh(){Refreshes++;}
    }
}
namespace Alterverse.Audio
{public enum DimensionSound { Purchase } public static class DimensionAudio {public static void Play(DimensionSound sound,float gain){}}}
namespace Alterverse.Online {public static class MatchRewardService {public static int MatchesWon=7;}}
