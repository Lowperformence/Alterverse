using Alterverse.Characters;
using UnityEngine;
namespace Alterverse.UI
{
    public static class StylizedHeroCard
    {
        public static void Draw(Rect area,HeroId hero,Texture2D pixel,float time)
        {
            Alterverse.Visuals.StylizedHeroCard.Draw(area,hero,pixel,time);
        }
    }
}
