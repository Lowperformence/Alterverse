using System;using UnityEngine;
namespace Alterverse.Shop
{
public sealed class FounderPackPurchaseService:MonoBehaviour
{
public enum PurchaseState{Unavailable,Ready,Processing,Owned,Failed}public PurchaseState State{get;private set;}public string Status{get;private set;}="STORE NOT CONNECTED";public event Action Changed;
private void Awake(){Refresh();}public void Refresh(){State=FounderEntitlement.Owned?PurchaseState.Owned:PurchaseState.Unavailable;Status=FounderEntitlement.Owned?"OWNED":"STORE BILLING NOT CONNECTED";Changed?.Invoke();}
public bool BeginPurchase(){if(FounderEntitlement.Owned){Refresh();return false;}State=PurchaseState.Unavailable;Status="PURCHASES ENABLE AT STORE INTEGRATION";Changed?.Invoke();return false;}
#if UNITY_EDITOR
public void ApplyVerifiedPurchase(){FounderEntitlement.GrantVerified();Refresh();}
public void GrantEditorPreview(){FounderEntitlement.SetTestOwnership(true);Refresh();}public void ResetEditorPreview(){FounderEntitlement.SetTestOwnership(false);Refresh();}
#endif
}
}
