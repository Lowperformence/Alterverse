using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        private int offlineEditingSlot;
        public static void SelectLobbyHero(HeroId hero)
        {PlayerHeroSelection.Select(hero);if(instance!=null)instance.selectedHero=(int)hero;}
        private void OpenOfflineLobby()
        {
            OfflineLobbyRoster.Begin((HeroId)selectedHero);
            offlineEditingSlot=0;
            SetPage(Page.Coop); // Retained internal page id; no separate Co-op navigation.
        }
        private void DrawOfflineLobby()
        {
            if(!OfflineLobbyRoster.Prepared)OfflineLobbyRoster.Begin((HeroId)selectedHero);
            Heading("OFFLINE LOBBY","Choose your hero and both teams before starting a local 4v4 match.");
            float gap=12f,w=(ContentWidth-gap*3)/4f;
            for(int team=0;team<2;team++)
            {
                float y=218+team*139;
                theme.Label(new Rect(Left,y,ContentWidth,22),team==0?"YOUR TEAM":"OPPONENT TEAM",theme.Small,true);
                for(int row=0;row<MatchSession.TeamSize;row++)
                {
                    int slot=team*MatchSession.TeamSize+row;HeroId hero=OfflineLobbyRoster.At(slot);
                    Rect card=new(Left+row*(w+gap),y+27,w,98);theme.Panel(card);
                    StylizedHeroCard.Draw(new Rect(card.x+8,card.y+7,64,82),hero,FrontEndTheme.Pixel,0);
                    theme.FitLabel(new Rect(card.x+80,card.y+10,w-90,24),slot==0?"YOU":"AI BOT",theme.Small,true);
                    theme.FitLabel(new Rect(card.x+80,card.y+36,w-90,42),HeroIdentity.NameFor(hero),theme.Body);
                    if(slot==offlineEditingSlot)theme.Frame(card,true);
                    if(GUI.Button(card,GUIContent.none,GUIStyle.none))offlineEditingSlot=slot;
                }
            }
            theme.Label(new Rect(Left,500,ContentWidth,22),offlineEditingSlot==0?"CHOOSE YOUR HERO":"CHOOSE BOT HERO",theme.Small,true);
            LobbyHeroPicker.Draw(theme,new Rect(Left,526,ContentWidth,105),OfflineLobbyRoster.At(offlineEditingSlot),!launching,
                hero=>OfflineLobbyRoster.Available(offlineEditingSlot,hero),hero=>
                {
                    if(!OfflineLobbyRoster.Select(offlineEditingSlot,hero))return;
                    if(offlineEditingSlot==0){selectedHero=(int)hero;PlayerHeroSelection.Select(hero);}
                },offlineEditingSlot==0);
            float third=(ContentWidth-24)/3;
            if(theme.Action(new Rect(Left,646,third,42),"BACK"))SetPage(Page.Play);
            if(theme.Action(new Rect(Left+third+12,646,third,42),"SHUFFLE BOTS"))
                OfflineLobbyRoster.Begin((HeroId)selectedHero);
            if(theme.Action(new Rect(Left+2*(third+12),646,third,42),"START OFFLINE MATCH",true,
                enabled:!choosingMatchMode&&!launching&&HeroUnlockInventory.Owned((HeroId)selectedHero)))
                _=BeginOfflineMatchAsync();
            theme.Label(new Rect(Left,694,ContentWidth,22),launchMessage.Length>0?launchMessage:
                ArenaShopCatalog.For(ArenaVariantRuntime.Selected).Name+"  •  "+BotDifficultySettings.Label(BotDifficultySettings.Selected),theme.Small,true);
        }
    }
}
