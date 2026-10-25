using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        void OpenShardStore()=>OpenStoreSection(true);
        void OpenAvatarStore()=>OpenStoreSection(false);
        void OpenStoreSection(bool shards)
        {
            skinShop=itemShop=weaponShop=mapShop=battlePassShop=heroShop=false;
            shardShop=shards;
            shopMessage="";
            SetPage(Page.Shop);
        }
        bool ShardPurchaseAction(Rect rect,string label,int price,bool primary=false,bool enabled=true,bool locked=false)
        {
            if(!theme.Action(rect,label,primary,enabled:enabled,locked:locked))return false;
            if(PlayerWallet.Balance<price){OpenShardStore();return false;}
            return true;
        }
    }
}
