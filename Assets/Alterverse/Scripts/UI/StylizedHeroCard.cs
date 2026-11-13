using Alterverse.Characters;
using UnityEngine;
namespace Alterverse.UI
{
    public static class StylizedHeroCard
    {
        public static void PrepareRoster()=>Alterverse.Visuals.StylizedHeroCard.PrepareRoster();
        public static bool RosterReady=>Alterverse.Visuals.StylizedHeroCard.RosterReady;
        public static void Draw(Rect area,HeroId hero,Texture2D pixel,float time,bool showPortrait=true)
        {
            Alterverse.Visuals.StylizedHeroCard.Draw(area,hero,pixel,time,showPortrait);
        }
    }
}
