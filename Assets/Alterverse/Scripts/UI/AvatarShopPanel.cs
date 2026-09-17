using Alterverse.Profile;using Alterverse.Shop;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.UI
{
public static class AvatarShopPanel
{
public static void Draw(Rect area,Texture2D pixel,GUIStyle heading,GUIStyle text,GUIStyle button){GUI.Label(new Rect(area.x,area.y,area.width,28),$"AVATAR COLLECTION     ◆ {PlayerWallet.Balance} {AvatarShopCatalog.CurrencyName}",heading);int cols=3;float gap=8f,cardW=(area.width-gap*(cols-1))/cols,cardH=92f;for(int i=0;i<AvatarShopCatalog.Items.Length;i++){AvatarShopItem item=AvatarShopCatalog.Items[i];int row=i/cols,col=i%cols;Rect card=new Rect(area.x+col*(cardW+gap),area.y+36+row*(cardH+gap),cardW,cardH);Color accent=item.Theme.Contains("ZERO")?StylizedArtDirection.ZeroEnergy:item.Theme.Contains("LUMEN")?StylizedArtDirection.LumenEnergy:StylizedArtDirection.WarmAccent;DimensionUiMotif.DrawCornerCuts(card,pixel,accent);GUI.Box(card,GUIContent.none);GUI.Label(new Rect(card.x+5,card.y+6,card.width-10,20),item.Name,text);GUI.Label(new Rect(card.x+5,card.y+27,card.width-10,18),item.Theme,text);bool owned=AvatarInventory.IsOwned(item.AvatarId);string action=owned?(PlayerProfile.Avatar==item.AvatarId?"EQUIPPED":"EQUIP"):$"{item.Price} SHARDS";GUI.enabled=!owned||PlayerProfile.Avatar!=item.AvatarId;if(GUI.Button(new Rect(card.x+12,card.y+52,card.width-24,30),action,button)){if(owned)PlayerProfile.Avatar=item.AvatarId;else if(PlayerWallet.TrySpend(item.Price)){AvatarInventory.Unlock(item.AvatarId);PlayerProfile.Avatar=item.AvatarId;}}GUI.enabled=true;}}
}
}
