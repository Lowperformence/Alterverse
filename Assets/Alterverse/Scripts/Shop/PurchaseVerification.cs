namespace Alterverse.Shop
{
public readonly struct PurchaseReceipt{public readonly string ProductId,TransactionId,Receipt;public PurchaseReceipt(string productId,string transactionId,string receipt){ProductId=productId;TransactionId=transactionId;Receipt=receipt;}}
public interface IPurchaseVerifier{void Verify(PurchaseReceipt receipt,System.Action<bool,string> completed);}
public static class PurchaseVerification
{
public static IPurchaseVerifier Verifier{get;private set;}public static bool IsConfigured=>Verifier!=null;public static void Install(IPurchaseVerifier verifier){Verifier=verifier;}public static void Verify(PurchaseReceipt receipt,System.Action<bool,string> completed){if(Verifier==null){completed?.Invoke(false,"PURCHASE VERIFIER NOT CONNECTED");return;}Verifier.Verify(receipt,completed);}
}
}
