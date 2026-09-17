using Alterverse.Shop;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.UI
{
public static class ShopAvatarPreview
{
private static GUIStyle priceStyle,themeStyle;public static void Draw(Rect area,AvatarShopItem item,Texture2D pixel,float time){priceStyle??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=17};themeStyle??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=11};AvatarBadgeRenderer.Draw(new Rect(area.center.x-42,area.y+6,84,84),item.AvatarId,pixel,time);GUI.Label(new Rect(area.x,area.y+94,area.width,20),item.Theme,themeStyle);GUI.Label(new Rect(area.x,area.y+116,area.width,24),"◆ "+item.Price+" "+AvatarShopCatalog.CurrencyName,priceStyle);float p=.5f+.5f*Mathf.Sin(time*1.8f+item.AvatarId);Color old=GUI.color;GUI.color=new Color(StylizedArtDirection.LumenEnergy.r,StylizedArtDirection.LumenEnergy.g,StylizedArtDirection.LumenEnergy.b,.08f+.08f*p);GUI.DrawTexture(new Rect(area.x+area.width*.12f,area.y+88,area.width*.76f,2),pixel);GUI.color=old;}
}
}
