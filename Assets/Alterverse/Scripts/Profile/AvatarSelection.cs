using System.Collections.Generic;using Alterverse.Shop;
namespace Alterverse.Profile
{
public readonly struct AvatarChoice{public readonly int Id;public readonly string Label,Theme;public AvatarChoice(int id,string label,string theme){Id=id;Label=label;Theme=theme;}}
public static class AvatarSelection
{
private static readonly List<AvatarChoice> result=new();public static List<AvatarChoice> Owned(){result.Clear();for(int i=0;i<AvatarInventory.BuiltInAvatarCount;i++)result.Add(new AvatarChoice(i,"FRONT "+(i+1),"CORE"));if(AvatarInventory.IsOwned(AvatarInventory.FounderAvatarId))result.Add(new AvatarChoice(AvatarInventory.FounderAvatarId,"FIRST FRACTURE","FOUNDER"));foreach(AvatarShopItem item in AvatarShopCatalog.Items)if(AvatarInventory.IsOwned(item.AvatarId))result.Add(new AvatarChoice(item.AvatarId,item.Name,item.Theme));if(AvatarInventory.IsOwned(BattlePassProgress.BetaCrownAvatarId))result.Add(new AvatarChoice(BattlePassProgress.BetaCrownAvatarId,"BETA CROWN",BattlePassProgress.SeasonName));return result;}
}
}
