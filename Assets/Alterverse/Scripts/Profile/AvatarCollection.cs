using System.Collections.Generic;using Alterverse.Shop;
namespace Alterverse.Profile
{
public static class AvatarCollection
{
public static List<int> OwnedIds(){List<int> ids=new();for(int i=0;i<AvatarInventory.BuiltInAvatarCount;i++)ids.Add(i);if(FounderEntitlement.Owned)ids.Add(AvatarInventory.FounderAvatarId);for(int i=0;i<AvatarShopCatalog.Items.Length;i++){int id=AvatarShopCatalog.Items[i].AvatarId;if(AvatarInventory.IsOwned(id))ids.Add(id);}return ids;}public static int Count=>OwnedIds().Count;public static int NextOwned(int current,int direction){List<int> ids=OwnedIds();if(ids.Count==0)return 0;int index=ids.IndexOf(current);if(index<0)index=0;index=(index+(direction>=0?1:-1)+ids.Count)%ids.Count;return ids[index];}
}
}
