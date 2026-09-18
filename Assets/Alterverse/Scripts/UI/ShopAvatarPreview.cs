using Alterverse.Shop;using UnityEngine;
namespace Alterverse.UI
{
public static class ShopAvatarPreview
{
    public static void Draw(Rect area,AvatarShopItem item,Texture2D pixel,float time)
    {float size=Mathf.Min(area.width,area.height);AvatarBadgeRenderer.Draw(new Rect(area.center.x-size*.5f,area.center.y-size*.5f,size,size),item.AvatarId,pixel,time);}
}
}
