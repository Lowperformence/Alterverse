using System;
namespace Alterverse.Shop
{
public enum StorePurchaseState{Unavailable,Ready,Processing,Succeeded,Cancelled,Failed}
public readonly struct StorePurchaseResult{public readonly StorePurchaseState State;public readonly string ProductId,TransactionId,Message;public StorePurchaseResult(StorePurchaseState state,string productId,string transactionId,string message){State=state;ProductId=productId;TransactionId=transactionId;Message=message;}public bool Succeeded=>State==StorePurchaseState.Succeeded;}
public interface IStorePurchaseProvider{bool IsReady{get;}string LocalizedPrice(string productId,string fallback);void Purchase(string productId,Action<StorePurchaseResult> completed);void Restore(Action<bool,string> completed);}
public static class StorePurchaseGateway
{
public static IStorePurchaseProvider Provider{get;private set;}public static bool IsReady=>Provider!=null&&Provider.IsReady;public static void Install(IStorePurchaseProvider provider){Provider=provider;}public static string Price(string productId,string fallback)=>IsReady?Provider.LocalizedPrice(productId,fallback):fallback;public static void Purchase(string productId,Action<StorePurchaseResult> completed){if(!IsReady){completed?.Invoke(new StorePurchaseResult(StorePurchaseState.Unavailable,productId,"","STORE BILLING NOT CONNECTED"));return;}Provider.Purchase(productId,completed);}public static void Restore(Action<bool,string> completed){if(!IsReady){completed?.Invoke(false,"STORE BILLING NOT CONNECTED");return;}Provider.Restore(completed);}
}
}
