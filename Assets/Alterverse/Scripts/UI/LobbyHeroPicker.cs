using System;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    internal static class LobbyHeroPicker
    {
        public static void Draw(FrontEndTheme theme,Rect area,HeroId selected,
            bool editable,Func<HeroId,bool> available,Action<HeroId> choose,bool human=true,bool showPortraits=true)
        {
            var heroes=(HeroId[])Enum.GetValues(typeof(HeroId));
            float gap=6f,width=(area.width-gap*(heroes.Length-1))/heroes.Length;
            for(int i=0;i<heroes.Length;i++)
            {
                HeroId hero=heroes[i];Rect card=new(area.x+i*(width+gap),area.y,width,area.height);
                bool owned=!human||HeroUnlockInventory.Owned(hero);
                bool free=available(hero),active=hero==selected;
                theme.Panel(card);
                StylizedHeroCard.Draw(new Rect(card.x+3,card.y+3,card.width-6,card.height-27),hero,FrontEndTheme.Pixel,0,showPortraits);
                theme.FitLabel(new Rect(card.x+3,card.yMax-24,card.width-6,20),
                    !owned?"LOCKED":!free&&!active?"TAKEN":HeroIdentity.NameFor(hero),theme.Small,true);
                if(active)theme.Frame(card,true);
                bool before=GUI.enabled;GUI.enabled=before&&editable&&owned&&free;
                if(GUI.Button(card,GUIContent.none,GUIStyle.none))
                {Alterverse.Audio.UiSoundRouter.Confirm();choose(hero);}
                GUI.enabled=before;
            }
        }
    }
}
