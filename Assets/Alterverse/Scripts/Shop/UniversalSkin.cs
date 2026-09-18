using Alterverse.Profile;
using UnityEngine;
namespace Alterverse.Shop
{
    public static class UniversalSkin
    {
        public const string Name="VOIDFALL",OwnedKey="DF_SkinOwned_Voidfall",EquippedKey="DF_Skin_Voidfall";
        public const int Price=1200;
        public static bool Owned=>PlayerPrefs.GetInt(OwnedKey,0)==1;
        public static bool Equipped=>Owned&&PlayerPrefs.GetInt(EquippedKey,0)==1;
        public static AvatarPurchaseResult Purchase()
        {
            ProfileTransaction.Recover();
            if(Owned)return AvatarPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(Price))return AvatarPurchaseResult.InsufficientShards;
            ProfileTransaction.Commit(new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-Price),new ProfileTransaction.Write(OwnedKey,1),new ProfileTransaction.Write(EquippedKey,1));
            return AvatarPurchaseResult.Purchased;
        }
        public static bool Equip(bool enabled)
        {
            ProfileTransaction.Recover();if(enabled&&!Owned)return false;
            ProfileTransaction.Commit(new ProfileTransaction.Write(EquippedKey,enabled?1:0));return true;
        }
    }
}
