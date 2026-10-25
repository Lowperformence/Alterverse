using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Content;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Online;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController : MonoBehaviour
    {
        private enum Page { Home, Play, Heroes, Shop, Inventory, Profile, Quests, Leaderboard, Settings, Coop, BattlePass, Clans }
        private static MainMenuController instance;
        public static bool IsVisible => instance != null && instance.visible && instance.isActiveAndEnabled;
        private static readonly string[] NavigationLabels = { "PLAY", "HEROES", "STORE", "INVENTORY", "BATTLE PASS", "PROFILE", "CLANS", "DAILY", "RANKING", "SETTINGS" };
        private static readonly Page[] NavigationPages = { Page.Play, Page.Heroes, Page.Shop, Page.Inventory, Page.BattlePass, Page.Profile, Page.Clans, Page.Quests, Page.Leaderboard, Page.Settings };
        private static readonly int[] HomeNavigation={0,1,2,3,5};
        private static readonly string[] Roles = { "ASSASSIN", "TANK", "MARKSMAN", "MAGE", "SUPPORT", "BRUISER", "TEMPORAL SKIRMISHER", "TIDAL BRUISER", "FELINE ASSASSIN", "VOID DUELIST", "MECH / SHIELD BREAKER" };
        private static readonly string[] RoleDescriptions = {
            "Close the distance. Isolate a vulnerable target, strike, then disengage.",
            "Hold the front. Shield your team and interrupt enemies at close range.",
            "Keep your distance. Deliver sustained fire while allies protect your position.",
            "Control the fight. Place powerful area attacks where enemies want to move.",
            "Keep allies fighting. Heal, shield and slow enemies from the backline.",
            "Win the long fight. Trade at close range with heavy strikes and sustain.",
            "Slip between seconds. Reposition through rifts, control space and strike with chronolight.",
            "Hook an enemy with your anchor. Shield yourself and control fights with a water vortex.",
            "A small cat astronaut. Jet into range, scratch one enemy, then slip away.", "A two-handed greatsword duelist from an unknown galaxy. Longer melee reach, measured damage and timely frontal parries.", "Compact mech: rapid Gatling fire, short grenade arcs and a shield-breaking electrical ultimate." };
        private static readonly string[] Strengths = { "BURST / MOBILITY", "PROTECTION / CONTROL", "RANGE / SUSTAINED DAMAGE", "AREA DAMAGE / CONTROL", "HEALING / PROTECTION", "MELEE / ENDURANCE", "MOBILITY / CONTROL", "PULL / AREA CONTROL", "MOBILITY / SINGLE TARGET", "MELEE REACH / PARRY", "GATLING / GRENADES / SHIELD BREAK" };
        private Page page = Page.Home;
        private bool visible = true, launching, coopLaunching, nameRerollBusy, suspended;
        private bool choosingMatchMode;
        private Vector2 heroRosterScroll;
        private int selectedHero, shopAvatarIndex, battlePassPageStart=1, battlePassPreviewLevel=1, weaponSkinVariant;
        private int playArenaBrowseIndex=-1,mapShopPageStart;
        private bool battlePassPreviewPremium;
        private bool skinShop=true,shardShop,itemShop,weaponShop,mapShop,battlePassShop,heroShop,cosmeticBusy;
        private float pageChangedAt, launchAt, menuWidth = 1280;
        private bool shopPurchasePending;
        private string profileMessage = "", shopMessage = "", launchMessage = "";
        private string coopInviteCode = "";
        private FrontEndTheme theme;
        private GUIStyle homeLogo,homeSubtitle;
        private MenuCosmicBackdrop backdrop;
        private MenuBackgroundVideo backgroundVideo;
        private MenuHeroShowcase showcase;
        private CosmeticCollectionPanel collection;
        private System.Action<int> changeCollectionHero;
        private float master, music, sfx;
        private bool audioDirty;
        private const float MenuHeight = 720;
        private float Left => (menuWidth - ContentWidth + (page==Page.Home?0:240))*.5f;
        private float ContentWidth => Mathf.Min(1360,menuWidth-(page==Page.Home?64:288));
        private float Right => Left+ContentWidth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<MainMenuController>() == null) new GameObject("DF_MainMenu").AddComponent<MainMenuController>();
        }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            selectedHero = Mathf.Clamp((int)PlayerHeroSelection.Selected,0,HeroIdentity.HeroCount-1);
            playArenaBrowseIndex=ArenaShopCatalog.IndexOf(ArenaVariantRuntime.Selected);
            mapShopPageStart=Mathf.Max(0,playArenaBrowseIndex-1);
            master = PlayerProfile.MasterVolume; music = PlayerProfile.MusicVolume; sfx = PlayerProfile.SfxVolume;
            showcase = gameObject.AddComponent<MenuHeroShowcase>();
            backgroundVideo = gameObject.AddComponent<MenuBackgroundVideo>();
            collection=new CosmeticCollectionPanel();
            changeCollectionHero=ChangeHero;
            UniversalCosmeticSelection.EnsureMigrated((HeroId)selectedHero);
            if(GooglePlayLevelLeaderboard.IsConfigured)GooglePlayLevelLeaderboard.AuthenticateAndSync();
            MatchClock.Set(PauseReason.Menu,true);
        }
        public static void ShowHome()
        {
            // Hide any previous arena camera before exposing the menu's first rendered frame.
            Alterverse.Prototype.PrototypeBootstrap.PrepareTransitionFrame();
            if (instance == null) { new GameObject("DF_MainMenu").AddComponent<MainMenuController>(); return; }
            instance.visible = true; instance.launching = false; instance.coopLaunching=false; instance.page = Page.Home;
            instance.profileMessage = ""; instance.shopMessage = ""; instance.pageChangedAt = Time.unscaledTime;
            instance.collection?.Invalidate();
            instance.showcase?.InvalidateLoadout();
            instance.gameObject.SetActive(true);
            if(GooglePlayLevelLeaderboard.IsConfigured)GooglePlayLevelLeaderboard.AuthenticateAndSync();
            MatchClock.Set(PauseReason.Menu,true);
        }

        public static void ShowLaunchFailure(string message)
        {
            ShowHome();
            if(instance==null)return;
            instance.page=Page.Play;
            instance.launchMessage=message;
        }

        public static void ShowCoopAfterMatch(string message="")
        {
            Alterverse.Prototype.PrototypeBootstrap.PrepareTransitionFrame();
            if(instance==null)new GameObject("DF_MainMenu").AddComponent<MainMenuController>();
            if(instance==null)return;
            instance.visible=true;instance.launching=false;instance.coopLaunching=false;instance.page=Page.Play;
            instance.nameRerollBusy=false;
            instance.profileMessage="";instance.shopMessage="";instance.launchMessage=message??"";
            instance.pageChangedAt=Time.unscaledTime;
            instance.collection?.Invalidate();instance.showcase?.InvalidateLoadout();
            instance.gameObject.SetActive(true);
            MatchClock.Set(PauseReason.Result,false);
            MatchClock.Set(PauseReason.Menu,true);
        }

        private void OnEnable() { if (visible) MatchClock.Set(PauseReason.Menu,true); }
        private void OnApplicationPause(bool paused)
        {
            suspended = paused;
            if (paused)
            {
                FlushAudio();
                if(launching)
                {
                    launching=false;
                    page=Page.Play;
                    coopLaunching=false;
                }
            }
            if (visible && !CoopAuthority.Active) MatchClock.Set(PauseReason.Menu,true);
        }
        private void OnApplicationFocus(bool focused)
        {
            if(!focused&&launching)
            {
                launching=false;
                page=coopLaunching?Page.Coop:Page.Play;
                coopLaunching=false;
            }
            if (focused && visible) { MatchClock.Set(PauseReason.Menu,true);collection?.Invalidate();showcase?.InvalidateLoadout(); }
        }
        private void OnDisable() { FlushAudio(); }
        private void OnDestroy() { collection?.Dispose();backdrop?.Dispose(); if (instance == this) instance = null; }
        private void Update()
        {
            if (!visible || suspended || OnlineMatchLoadingScreen.IsVisible) return;
            UpdateReviewPrompt();
            if (ReviewPromptVisible)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    reviewPromptOpen = false;
                return;
            }
            EdgegapOnlineFlow onlineFlow = EdgegapOnlineFlow.Instance;
            if (page == Page.Play && onlineFlow != null &&
                (onlineFlow.Busy || onlineFlow.IsSearching || !string.IsNullOrEmpty(onlineFlow.LastError)))
                launchMessage = onlineFlow.Status;
            CoopLobbyService coop=CoopLobbyService.Instance;
            if(!launching&&page==Page.Coop&&coop!=null&&coop.InLobby&&coop.IsLaunching)
                BeginCoopLaunch(coop);
            bool previewSkin=page==Page.Shop&&skinShop&&!shardShop&&!itemShop&&!weaponShop&&!mapShop&&!battlePassShop&&!heroShop;
            bool previewInventory=page==Page.Inventory&&collection.InventoryPreview;
            bool previewWeapon=page==Page.Shop&&weaponShop;
            bool previewPremiumHero=page==Page.Shop&&heroShop;
            HeroId previewHero=previewPremiumHero?PremiumShopHero:(HeroId)selectedHero;
            showcase.Present(previewHero,
                !launching&&(page==Page.Home||page==Page.Play||page==Page.Heroes||previewSkin||previewWeapon||previewInventory||previewPremiumHero),
                previewSkin?null:previewWeapon?(bool?)false:null,
                previewWeapon?(bool?)true:null,
                previewSkin?(UniversalHeroCosmetic?)collection.Preview(false):previewInventory?(UniversalHeroCosmetic?)collection.Preview(true):null,
                previewWeapon?weaponSkinVariant:(int?)null);
            if (!OnlinePartyOverlay.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (launching) launching = false;
                else SetPage(Page.Home);
            }
            if (launching && Time.unscaledTime-launchAt >= .55f) EnterMatch();
        }
        private void SetPage(Page next)
        {
            if (page == next || launching) return;
            FlushAudio(); collection?.Invalidate();showcase?.InvalidateLoadout(); page = next; pageChangedAt = Time.unscaledTime;
            profileMessage = ""; shopMessage = ""; GUI.FocusControl(null);
            confirmRoute=confirmPlayer=confirmLabel=null;
            if(next==Page.Leaderboard)GooglePlayLevelLeaderboard.AuthenticateAndSync();
            if(next==Page.Clans)SocialService.Instance?.Load("/social/state");
        }
        private void OnGUI()
        {
            if (!visible || (!ReviewPromptVisible && OnlinePartyOverlay.IsOpen) || RankedPanel.Visible || OnlineMatchLoadingScreen.IsVisible) return;
            Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color; bool oldEnabled = GUI.enabled;
            try
            {
                GUI.color = Color.white; GUI.enabled = !launching;
                backdrop ??= new MenuCosmicBackdrop();
                backdrop.Draw(new Rect(0,0,Screen.width,Screen.height),backgroundVideo?.Image);
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) return;
                float scale = Mathf.Min(safe.height/MenuHeight,safe.width/1500f);
                menuWidth = safe.width/scale;
                float y = Screen.height-safe.yMax+(safe.height-MenuHeight*scale)*.5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x,y,0),Quaternion.identity,Vector3.one*scale);
                theme ??= new FrontEndTheme();
                if (ReviewPromptVisible) { DrawReviewPrompt(); return; }
                if (launching) { DrawLaunch(); return; }
                if(page != Page.Home)
                {
                    FrontEndTheme.Fill(new Rect(0,0,menuWidth,MenuHeight),new Color(.008f,.014f,.025f,.52f));
                    DrawNavigation();
                }
                float reveal = Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-pageChangedAt)/.16f));
                GUI.color = new Color(1,1,1,.65f+.35f*reveal);
                if(shopPurchasePending)
                {shopMessage=PaidShardCommerce.Instance?.Status??shopMessage;if(!PaidShardCommerce.Spending)shopPurchasePending=false;}
                switch (page)
                {
                    case Page.Home: DrawHome(); break;
                    case Page.Play: DrawPlay(); break;
                    case Page.Heroes: DrawHeroes(); break;
                    case Page.Shop: DrawShop(); break;
                    case Page.BattlePass: Heading("Battle Pass","Play matches. Unlock seasonal rewards. Claim your progress."); DrawBattlePassShop(); break;
                    case Page.Inventory: DrawInventory(); break;
                    case Page.Profile: DrawProfile(); break;
                    case Page.Quests: DrawQuests(); break;
                    case Page.Leaderboard: DrawLeaderboard(); break;
                    case Page.Settings: DrawSettings(); break;
                    case Page.Coop: DrawCoop(); break;
                    case Page.Clans: DrawClans(); break;
                    default: DrawPlay(); break;
                }
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; }
        }
        private void DrawNavigation()
        {
            float rail=Left-232;
            theme.FitLabel(new Rect(rail,26,208,35),"DIMENSIONS",theme.Heading);
            theme.Label(new Rect(rail,61,208,27),"F  R  O  N  T",theme.Small,true);
            FrontEndTheme.Fill(new Rect(rail+218,26,1,660),new Color(.53f,.64f,.75f,.22f));
            if(theme.Navigation(new Rect(rail,116,202,46),"HOME",0,selected:page==Page.Home))SetPage(Page.Home);
            for(int i=0;i<NavigationLabels.Length;i++)
                if(theme.Navigation(new Rect(rail,178+i*44,202,40),NavigationLabels[i],NavigationIcon(NavigationPages[i]),
                    primary:i==0,selected:page==NavigationPages[i]))SetPage(NavigationPages[i]);
            theme.FitLabel(new Rect(Left,25,ContentWidth-300,30),"COMMAND DECK  /  "+page.ToString().ToUpperInvariant(),theme.Heading);
            theme.Label(new Rect(Left,57,ContentWidth-300,22),$"LEVEL {PlayerExperience.Level}",theme.Small,true);
            DrawShardWallet(new Rect(Right-280,20,280,56));
            FrontEndTheme.Fill(new Rect(Left,85,ContentWidth,1),new Color(.75f,.65f,.47f,.50f));
        }
        private void DrawShardWallet(Rect rect)
        {
            theme.Panel(rect);
            ShardShopVisualOverlay.DrawWallet(rect,PlayerWallet.Balance);
        }
        private static int NavigationIcon(Page target) => target switch
        {
            Page.Play=>1, Page.Heroes=>2, Page.Shop=>3, Page.Inventory=>4,
            Page.Profile=>5, Page.Quests=>6, Page.Leaderboard=>7, Page.Settings=>8,
            Page.BattlePass=>9, _=>0
        };

        private void Heading(string title, string description)
        {
            theme.Label(new Rect(Left,110,ContentWidth,52),title,theme.Title);
            theme.Label(new Rect(Left,166,ContentWidth,30),description,theme.Body,true);
        }
        private string HeroName => HeroIdentity.NameFor((HeroId)selectedHero);

        private string ArenaBrowseStatus(ArenaVariant arena,bool owned,bool selected)
        {
            if(selected)return "SELECTED FOR NEXT MATCH";
            if(owned)return "FREE  •  TAP ARENA TO SELECT";
            ArenaShopItem item=ArenaShopCatalog.For(arena);
            return item.Price>0?$"LOCKED  •  {item.Price} SHARDS IN STORE":"FREE  •  TAP ARENA TO SELECT";
        }

        private void DrawPortrait(Rect rect)
        {
            Color accent=Alterverse.Visuals.StylizedArtDirection.HeroAccent((HeroId)selectedHero);
            showcase?.DrawPreview(rect);
            DimensionUiMotif.DrawCornerCuts(rect,Texture2D.whiteTexture,accent);
            FrontEndTheme.Fill(new Rect(rect.x+rect.width*.24f,rect.yMax-10,rect.width*.52f,2),new Color(accent.r,accent.g,accent.b,.50f));
        }
        private void DrawHome()
        {
            float navWidth=252f;
            float heroX=Left+ContentWidth*.62f;
            // Render the actual selected loadout over the environment, without an opaque card.
            if(showcase?.Image!=null)
                GUI.DrawTexture(new Rect(heroX,152,Right-heroX,450),showcase.Image,ScaleMode.ScaleToFit,true);

            homeLogo??=new GUIStyle(theme.Title){fontSize=64,fontStyle=FontStyle.Normal};
            theme.FitLabel(new Rect(Left,58,ContentWidth*.67f,80),"DIMENSIONS",homeLogo);
            if(homeSubtitle==null){homeSubtitle=new GUIStyle(theme.Heading);homeSubtitle.normal.textColor=FrontEndTheme.Accent;}
            theme.Label(new Rect(Left+4,131,530,36),"F  R  O  N  T",homeSubtitle);
            theme.Label(new Rect(Left+4,174,600,26),"BEYOND BORDERS. HIGHER HORIZONS.",theme.Small,true);

            Rect profile=new Rect(Right-280,24,280,70);
            theme.Panel(profile);
            AvatarBadgeRenderer.Draw(new Rect(profile.x+12,profile.y+12,44,44),PlayerProfile.Avatar,Texture2D.whiteTexture,Time.unscaledTime);
            theme.FitLabel(new Rect(profile.x+68,profile.y+8,196,25),PlayerProfile.PlayerName,theme.Body);
            theme.Label(new Rect(profile.x+68,profile.y+32,196,23),$"LEVEL {PlayerExperience.Level}",theme.Small,true);
            theme.Progress(new Rect(profile.x+68,profile.y+60,194,3),PlayerExperience.LevelProgress);
            if(GUI.Button(profile,GUIContent.none,GUIStyle.none))SetPage(Page.Profile);
            DrawShardWallet(new Rect(profile.x-292,profile.y,280,profile.height));

            FrontEndTheme.Fill(new Rect(Left,246,1,276),new Color(.66f,.75f,.84f,.45f));
            int[] homeNavigation=HomeNavigation;
            for(int row=0;row<homeNavigation.Length;row++)
            {
                int i=homeNavigation[row];
                if(theme.Navigation(new Rect(Left+10,246+row*56,navWidth-10,48),NavigationLabels[i],NavigationIcon(NavigationPages[i]),primary:row==0))
                    SetPage(NavigationPages[i]);
            }
            float textX=Left+navWidth+38;
            float textWidth=Mathf.Max(230,heroX-textX+30);
            theme.FitLabel(new Rect(textX,267,textWidth,28),"CHOOSE. DEPLOY. CONQUER.",homeSubtitle);
            theme.Label(new Rect(textX,309,textWidth,94),"TWO REALITIES.\nONE FRONT.",theme.Title);
            theme.Label(new Rect(textX,416,textWidth-12,76),"Choose your hero. Fight alongside your team. Claim the Hotzone and break the enemy core.",theme.Body,true);
            FrontEndTheme.Fill(new Rect(textX,510,56,2),FrontEndTheme.Accent);
            theme.FitLabel(new Rect(heroX+24,616,Right-heroX-48,35),HeroName,theme.Heading);
            theme.FitLabel(new Rect(heroX+24,654,Right-heroX-48,22),Roles[selectedHero],theme.Small,true);

            float cardWidth=Mathf.Min(232,(ContentWidth*.66f-24)/3f);
            DrawHomeCard(new Rect(Left,542,cardWidth,126),"01  /  DEPLOYMENT","ENTER THE FRONT",Page.Play);
            DrawHomeCard(new Rect(Left+cardWidth+12,542,cardWidth,126),"02  /  SEASON REWARDS","BATTLE PASS",Page.BattlePass);
            DrawHomeCard(new Rect(Left+(cardWidth+12)*2,542,cardWidth,126),"03  /  DAILY OBJECTIVES","MISSIONS",Page.Quests);
            if(theme.Action(new Rect(Left,672,cardWidth,44),"RANKING"))SetPage(Page.Leaderboard);
            if(theme.Action(new Rect(Left+cardWidth+12,672,cardWidth,44),"SETTINGS"))SetPage(Page.Settings);
            if(theme.Action(new Rect(Left+(cardWidth+12)*2,672,cardWidth,44),"CLANS"))SetPage(Page.Clans);
            theme.FitLabel(new Rect(Right-350,681,350,26),"SAME STARS. NEW POSSIBILITIES.",theme.Small,true);
        }
        private void DrawHomeCard(Rect rect,string eyebrow,string title,Page target)
        {
            theme.Panel(rect);
            backdrop.DrawCard(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),target==Page.Play?0:target==Page.BattlePass?1:2);
            FrontEndTheme.Fill(new Rect(rect.x+1,rect.yMax-51,rect.width-2,50),new Color(.018f,.028f,.043f,.94f));
            theme.Frame(rect,rect.Contains(Event.current.mousePosition));
            FrontEndTheme.Fill(new Rect(rect.x+14,rect.yMax-53,26,2),FrontEndTheme.Accent);
            theme.FitLabel(new Rect(rect.x+14,rect.yMax-48,rect.width-28,20),eyebrow,theme.Small,true);
            theme.FitLabel(new Rect(rect.x+14,rect.yMax-28,rect.width-28,24),title,theme.Heading);
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none))
            {
                Alterverse.Audio.UiSoundRouter.Confirm();
                SetPage(target);
            }
        }
        private void DrawPlay()
        {
            Heading("Deployment","Choose your hero and arena before entering the Front.");
            float w = ContentWidth*.52f;
            theme.Panel(new Rect(Left,214,w,394));
            theme.StatusBadge(new Rect(Left+28,224,154,28),"READY TO PLAY",FrontEndTheme.Positive);
            theme.Label(new Rect(Left+28,252,w-56,42),"MATCH 4v4",theme.Title);
            theme.Label(new Rect(Left+28,300,w-56,48),"Four versus four.\nTwo lanes. Dimension planets. One Reality Core.",theme.Body,true);
            float lobbyButtonWidth=(w-64)/2;
            if(theme.Action(new Rect(Left+28,354,lobbyButtonWidth,32),"INVITE FRIENDS",true))
                _=BeginOnlineMatchmakingAsync();
            if(theme.Action(new Rect(Left+36+lobbyButtonWidth,354,lobbyButtonWidth,32),"PRIVATE CO-OP"))
                SetPage(Page.Coop);
            BattleItemType equippedItem=BattleItemInventory.Equipped;
            theme.Label(new Rect(Left+28,396,w-56,18),equippedItem==BattleItemType.None?"BATTLE ITEM  /  NONE":$"BATTLE ITEM  /  {BattleItemInventory.Name(equippedItem)}  ×{BattleItemInventory.Count(equippedItem)}",theme.Small,true);
            HeroId deploymentHero=(HeroId)selectedHero;
            theme.Label(new Rect(Left+28,418,w-56,18),WeaponSkinInventory.Equipped(deploymentHero)?$"WEAPON  /  {WeaponSkinCatalog.For(deploymentHero,WeaponSkinInventory.EquippedVariant(deploymentHero)).Name}":deploymentHero==HeroId.Veyr?"WEAPON  /  SHADOW STEEL":deploymentHero==HeroId.Traveler?"WEAPON  /  PLANET FLUX":"WEAPON  /  ORIGINAL",theme.Small,true);

            theme.Label(new Rect(Left+28,448,w-56,20),"READY FOR DEPLOYMENT",theme.Small,true);
            theme.Progress(new Rect(Left+28,482,w-56,3),1);
            theme.Label(new Rect(Left+28,520,w-230,20),"ARENA",theme.Small,true);
            bool arenaShuffle=ArenaVariantRuntime.ShuffleEnabled;
            if(theme.Action(new Rect(Left+w-194,514,166,28),arenaShuffle?"SHUFFLE  ON":"SHUFFLE",selected:arenaShuffle))
            {
                ArenaVariantRuntime.SetShuffleEnabled(!arenaShuffle);
                arenaShuffle=!arenaShuffle;
                launchMessage=arenaShuffle?"Arena shuffle enabled.":"Arena shuffle disabled.";
            }
            ArenaVariant selectedArena=ArenaVariantRuntime.Selected;
            int arenaCount=ArenaShopCatalog.Items.Length;
            if(arenaCount>0)
            {
                if(playArenaBrowseIndex<0||playArenaBrowseIndex>=arenaCount)
                    playArenaBrowseIndex=ArenaShopCatalog.IndexOf(selectedArena);

                float arrowW=46f,selectorGap=8f;
                float selectorX=Left+28f,selectorW=w-56f;
                if(theme.Action(new Rect(selectorX,544,arrowW,44),"<",enabled:arenaCount>1))
                {
                    playArenaBrowseIndex=(playArenaBrowseIndex-1+arenaCount)%arenaCount;
                    ArenaShopItem target=ArenaShopCatalog.Items[playArenaBrowseIndex];
                    if(!arenaShuffle&&ArenaUnlockInventory.Owned(target.Variant))
                    {
                        ArenaVariantRuntime.Select(target.Variant);
                        selectedArena=target.Variant;
                    }
                    launchMessage="";
                }

                ArenaShopItem browsed=ArenaShopCatalog.Items[playArenaBrowseIndex];
                bool owned=ArenaUnlockInventory.Owned(browsed.Variant);
                bool selectedNow=selectedArena==browsed.Variant;
                float centerX=selectorX+arrowW+selectorGap;
                float centerW=selectorW-arrowW*2-selectorGap*2;
                string arenaLabel=$"{playArenaBrowseIndex+1} / {arenaCount}   •   {browsed.Name}";
                if(theme.Action(new Rect(centerX,544,centerW,44),arenaLabel,selected:selectedNow,enabled:owned))
                {
                    ArenaVariantRuntime.Select(browsed.Variant);
                    selectedArena=browsed.Variant;
                    launchMessage="";
                }
                FrontEndTheme.Fill(new Rect(centerX+16,584,centerW-32,3),new Color(browsed.Accent.r,browsed.Accent.g,browsed.Accent.b,.85f));

                if(theme.Action(new Rect(selectorX+selectorW-arrowW,544,arrowW,44),">",enabled:arenaCount>1))
                {
                    playArenaBrowseIndex=(playArenaBrowseIndex+1)%arenaCount;
                    ArenaShopItem target=ArenaShopCatalog.Items[playArenaBrowseIndex];
                    if(!arenaShuffle&&ArenaUnlockInventory.Owned(target.Variant))
                    {
                        ArenaVariantRuntime.Select(target.Variant);
                        selectedArena=target.Variant;
                    }
                    launchMessage="";
                }

                browsed=ArenaShopCatalog.Items[playArenaBrowseIndex];
                owned=ArenaUnlockInventory.Owned(browsed.Variant);
                selectedNow=selectedArena==browsed.Variant;
                theme.Center(new Rect(selectorX,590,selectorW,18),arenaShuffle
                    ?"SHUFFLE ON  •  RANDOM OWNED ARENA EACH MATCH"
                    :ArenaBrowseStatus(browsed.Variant,owned,selectedNow));
            }

            const float matchButtonGap = 8f;
            float matchButtonWidth = (w - matchButtonGap * 2) / 3f;
            bool heroReleaseLocked = !HeroUnlockCatalog.Released(deploymentHero);
            bool matchButtonsEnabled = AccountServerBootstrap.CloudProfileReady &&
                !choosingMatchMode && !OnlinePlanHandshake.Preparing &&
                !RankedPanel.Visible && RankedService.Instance?.Searching != true && RankedService.Instance?.Busy != true &&
                (EdgegapOnlineFlow.Instance == null || !EdgegapOnlineFlow.Instance.Busy) &&
                (OnlinePartyService.Instance == null || !OnlinePartyService.Instance.Busy) &&
                (CoopLobbyService.Instance == null || !CoopLobbyService.Instance.Busy);
            if (theme.Action(new Rect(Left,620,matchButtonWidth,58),"Play Online",true,
                enabled:matchButtonsEnabled,locked:heroReleaseLocked)) _ = BeginOnlineMatchmakingAsync();
            if (theme.Action(new Rect(Left+matchButtonWidth+matchButtonGap,620,matchButtonWidth,58),"Ranked",true,
                enabled:matchButtonsEnabled && HeroUnlockInventory.Owned((HeroId)selectedHero),locked:heroReleaseLocked || !RankedAvailability.Enabled))
            { PlayerHeroSelection.Select((HeroId)selectedHero); RankedPanel.Show(); }
            if (theme.Action(new Rect(Left+2*(matchButtonWidth+matchButtonGap),620,matchButtonWidth,58),"Play Offline",primary:true,
                enabled:matchButtonsEnabled,locked:heroReleaseLocked)) _ = BeginOfflineMatchAsync();
            theme.Label(new Rect(Left,684,w,22),launchMessage.Length>0?launchMessage:
                !AccountServerBootstrap.CloudProfileReady?"WAIT FOR CLOUD SYNC  /  PROFILE > GOOGLE ACCOUNT":
                arenaShuffle?"ARENA SHUFFLE  /  RANDOM OWNED MAP":
                $"SELECTED ARENA  /  {ArenaShopCatalog.For(selectedArena).Name}",theme.Small,true);

            float x = Left+w+32, rw = ContentWidth-w-32;
            DrawPortrait(new Rect(x,198,rw,330));
            theme.Label(new Rect(x+24,538,rw-48,40),HeroName,theme.Heading);
            theme.Label(new Rect(x+24,578,rw-48,24),Roles[selectedHero],theme.Small,true);
            if (theme.Action(new Rect(x+24,620,rw-48,54),"CHANGE HERO")) SetPage(Page.Heroes);
        }
        private void DrawCoop()
        {
            Heading("PRIVATE CO-OP", "Invite friends with a room code. Play together with up to four players.");
            CoopLobbyService coop = CoopLobbyService.Instance;
            theme.Panel(new Rect(Left, 214, ContentWidth, 456));
            if (coop == null)
            {
                theme.Label(new Rect(Left + 28, 264, ContentWidth - 56, 45),
                    "Co-op is initializing. Please try again.", theme.Body, true);
                return;
            }

            bool busy = coop.Busy;
            if (!coop.InLobby)
            {
                theme.Label(new Rect(Left + 30, 242, ContentWidth - 60, 36),
                    "CREATE A PRIVATE ROOM", theme.Heading);
                theme.Label(new Rect(Left + 30, 292, ContentWidth - 60, 48),
                    "Create a room and share its code with friends, or join a friend's room below.",
                    theme.Body, true);
                if (theme.Action(new Rect(Left + 30, 353, ContentWidth - 60, 55),
                    busy ? "PLEASE WAIT" : "CREATE ROOM", true, enabled: !busy))
                    _ = coop.CreateLobbyAsync();

                theme.Label(new Rect(Left + 30, 438, ContentWidth - 60, 26),
                    "ENTER FRIEND'S ROOM CODE", theme.Small, true);
                coopInviteCode = GUI.TextField(new Rect(Left + 30, 474, ContentWidth - 260, 52),
                    coopInviteCode ?? "", 16, theme.Field);
                if (theme.Action(new Rect(Right - 216, 474, 186, 52), "JOIN ROOM", true,
                    enabled: !busy && CoopLobbyService.NormalizeJoinCode(coopInviteCode).Length >= 4))
                    _ = coop.JoinLobbyAsync(CoopLobbyService.NormalizeJoinCode(coopInviteCode));
                theme.FitLabel(new Rect(Left + 30, 595, ContentWidth - 60, 26),
                    !string.IsNullOrEmpty(coop.LastError) ? coop.LastError : coop.Status, theme.Small, true);
                return;
            }

            theme.Label(new Rect(Left + 28, 233, 270, 30), "FRIEND INVITE CODE", theme.Small, true);
            theme.FitLabel(new Rect(Left + 28, 259, ContentWidth - 250, 31),
                coop.JoinCode, theme.Heading);
            if (theme.Action(new Rect(Right - 206, 240, 176, 48), "COPY CODE",
                enabled: !string.IsNullOrEmpty(coop.JoinCode)))
                GUIUtility.systemCopyBuffer = coop.JoinCode;

            const int maxSlots = CoopLobbyService.MaxHumanPlayers;
            float gap = 9f;
            float slotWidth = (ContentWidth - 56 - (maxSlots - 1) * gap) / maxSlots;
            IReadOnlyList<CoopLobbyService.MemberView> members = coop.Members;
            for (int i = 0; i < maxSlots; i++)
            {
                float x = Left + 28 + i * (slotWidth + gap);
                theme.Panel(new Rect(x, 310, slotWidth, 117));
                if (i >= members.Count)
                {
                    theme.Center(new Rect(x + 8, 342, slotWidth - 16, 35), "OPEN SLOT");
                    continue;
                }
                CoopLobbyService.MemberView member = members[i];
                theme.FitLabel(new Rect(x + 10, 320, slotWidth - 20, 23),
                    member.DisplayName + (member.IsLocal ? " (YOU)" : ""), theme.Small);
                theme.FitLabel(new Rect(x + 10, 350, slotWidth - 20, 26),
                    HeroIdentity.NameFor(member.Hero), theme.Body);
                theme.StatusBadge(new Rect(x + 10, 390, slotWidth - 20, 25),
                    member.Ready ? "READY" : "NOT READY",
                    member.Ready ? FrontEndTheme.Positive : FrontEndTheme.Warning);
            }

            bool editable = !busy && coop.IsLobby;
            theme.Label(new Rect(Left + 30, 439, 180, 23), "YOUR HERO", theme.Small, true);
            if (theme.Action(new Rect(Left + 28, 465, 46, 44), "<", enabled: editable))
                _ = coop.CycleLocalHeroAsync(-1);
            theme.FitLabel(new Rect(Left + 85, 472, 174, 30),
                HeroIdentity.NameFor(coop.LocalHero), theme.Body);
            if (theme.Action(new Rect(Left + 258, 465, 46, 44), ">", enabled: editable))
                _ = coop.CycleLocalHeroAsync(1);
            if (theme.Action(new Rect(Left + 332, 465, 191, 44),
                coop.LocalReady ? "UNREADY" : "READY", true,
                enabled: editable && (coop.LocalReady || !coop.HasHeroConflict)))
                _ = coop.SetLocalReadyAsync(!coop.LocalReady);

            if (coop.IsHost)
            {
                if (theme.Action(new Rect(Left + 28, 532, 40, 40), "<",
                    enabled: editable && coop.IsHost))
                    _ = coop.CycleHostArenaAsync(-1);
                theme.FitLabel(new Rect(Left + 74, 532, 192, 40),
                    ArenaShopCatalog.For(coop.LobbyArena).Name, theme.Small);
                if (theme.Action(new Rect(Left + 271, 532, 40, 40), ">",
                    enabled: editable && coop.IsHost))
                    _ = coop.CycleHostArenaAsync(1);
                if (theme.Action(new Rect(Left + 330, 532, 135, 40),
                    coop.LobbyArenaShuffle ? "SHUFFLE ON" : "SHUFFLE",
                    enabled: editable))
                    _ = coop.SetHostArenaShuffleAsync(!coop.LobbyArenaShuffle);
                if (theme.Action(new Rect(Left + 480, 532, 36, 40), "<", enabled: editable))
                    _ = coop.CycleHostDifficultyAsync(-1);
                theme.FitLabel(new Rect(Left + 521, 532, 135, 40),
                    BotDifficultySettings.Label(coop.LobbyDifficulty), theme.Small);
                if (theme.Action(new Rect(Left + 658, 532, 36, 40), ">", enabled: editable))
                    _ = coop.CycleHostDifficultyAsync(1);
            }
            else
                theme.Label(new Rect(Left + 28, 541, ContentWidth - 56, 25),
                    "Waiting for the party host to configure the match.", theme.Small, true);

            if (coop.IsStaging && coop.IsHost)
            {
                if (theme.Action(new Rect(Left + 28, 592, 230, 50), "CANCEL PREP",
                    enabled: !busy))
                    _ = coop.CancelMatchStagingAsync();
                if (theme.Action(new Rect(Left + 269, 592, 238, 50),
                    CoopPlanHandshake.PlanHandshakeReady ? "START MATCH" : "SYNCING PLAYERS",
                    true, enabled: !busy && CoopPlanHandshake.PlanHandshakeReady))
                    _ = coop.LaunchPreparedMatchAsync();
            }
            else if (coop.IsHost && coop.IsLobby)
            {
                if (theme.Action(new Rect(Left + 28, 592, 265, 50),
                    "PREPARE MATCH", true, enabled: !busy && coop.CanStageMatch))
                    _ = coop.BeginMatchStagingAsync();
            }
            else
                theme.Label(new Rect(Left + 28, 602, 420, 26),
                    coop.IsStaging ? CoopPlanHandshake.HandshakeStatus : "Waiting for host to start.",
                    theme.Small, true);

            if (theme.Action(new Rect(Right - 200, 592, 170, 50),
                busy ? "PLEASE WAIT" : "LEAVE ROOM", enabled: !busy && !coop.IsLaunching))
                _ = coop.LeaveLobbyAsync();
            theme.FitLabel(new Rect(Left + 28, 645, ContentWidth - 56, 22),
                !string.IsNullOrEmpty(coop.LastError) ? coop.LastError :
                coop.IsStaging ? CoopPlanHandshake.HandshakeStatus : coop.Status,
                theme.Small, true);
        }
        private void DrawHeroes()
        {
            const float rosterWidth=244f,rowStep=48f;
            float portraitX=Left+rosterWidth+16;
            float portraitWidth=Mathf.Clamp(ContentWidth*.275f,292f,356f);
            float attacksX=portraitX+portraitWidth+18;
            float attacksWidth=Right-attacksX;
            // The heading sits above the hero card so the roster can use the full column.
            theme.Label(new Rect(portraitX,110,Right-portraitX,52),"Heroes",theme.Title);
            theme.Label(new Rect(portraitX,166,Right-portraitX,30),$"{HeroIdentity.HeroCount} heroes. Choose how you fight.",theme.Body,true);
            Rect roster=new Rect(Left,112,rosterWidth,572);
            float listHeight=Mathf.Max(roster.height,HeroIdentity.HeroCount*rowStep-5f);
            float rowWidth=rosterWidth-(listHeight>roster.height?20f:0f);
            heroRosterScroll=GUI.BeginScrollView(roster,heroRosterScroll,new Rect(0,0,rowWidth,listHeight),false,false);
            try
            {
                for(int i=0;i<HeroIdentity.HeroCount;i++)
                {
                    Rect row=new Rect(0,i*rowStep,rowWidth,rowStep-5f);
                    HeroId rowHero=(HeroId)i;
                    bool chosen=i==selectedHero;
                    bool owned=HeroUnlockInventory.Owned(rowHero);
                    if(theme.Action(row,"",selected:chosen))ChangeHero(i);
                    FrontEndTheme.Fill(new Rect(row.x+5,row.y+10,3,row.height-20),Alterverse.Visuals.StylizedArtDirection.HeroAccent(rowHero));
                    var icon=rowHero==HeroId.Bolt7?Alterverse.Visuals.Bolt7Visual.Portrait:null;
                    float labelX=icon!=null?48f:18f;
                    if(icon!=null)
                    {
                        Color beforeIcon=GUI.color;GUI.color=Color.white;
                        GUI.DrawTexture(new Rect(row.x+10,row.y+7,28,28),icon,ScaleMode.ScaleToFit,true);
                        GUI.color=beforeIcon;
                    }
                    theme.Label(new Rect(row.x+labelX,row.y+3f,row.width-labelX-14,22f),HeroIdentity.NameFor(rowHero),theme.Body);
                    string subtitle=!HeroUnlockCatalog.Released(rowHero)?"COMING SOON":owned?Roles[i]:$"LOCKED  •  {HeroUnlockCatalog.Price(rowHero):N0} SHARDS";
                    theme.Label(new Rect(row.x+labelX,row.y+24f,row.width-labelX-14,16f),subtitle,theme.Small,true);
                }
            }
            finally{GUI.EndScrollView();}

            HeroId heroId=(HeroId)selectedHero;
            bool heroOwned=HeroUnlockInventory.Owned(heroId);
            bool heroReleaseLocked=!HeroUnlockCatalog.Released(heroId);
            Color accent=Alterverse.Visuals.StylizedArtDirection.HeroAccent(heroId);
            theme.Label(new Rect(portraitX,212,portraitWidth,44),HeroName,theme.Heading);
            theme.FitLabel(new Rect(portraitX,250,portraitWidth,22),Roles[selectedHero]+"  •  "+Strengths[selectedHero],theme.Small,true);
            DrawPortrait(new Rect(portraitX,282,portraitWidth,272));
            theme.Label(new Rect(portraitX,566,portraitWidth,46),RoleDescriptions[selectedHero],theme.Small,true);
            string weaponStatus=WeaponSkinInventory.Equipped(heroId)?WeaponSkinCatalog.For(heroId).Name:
                heroId==HeroId.VoidKnight?"RIFT EDGE GREATSWORD":heroId==HeroId.Veyr?"SHADOW STEEL":heroId==HeroId.Traveler?"CHRONOLUX FOCUS":heroId==HeroId.Nerok?"ABYSSAL ANCHOR":heroId==HeroId.Timmy?"SUIT CLAWS":"ORIGINAL WEAPON";
            theme.FitLabel(new Rect(portraitX,616,portraitWidth,24),
                heroOwned?"EQUIPMENT  /  "+weaponStatus:$"PREMIUM HERO  /  {HeroUnlockCatalog.Price(heroId):N0} SHARDS",theme.Small,true);

            string heroAction=heroReleaseLocked||heroOwned?"DEPLOY":"VIEW IN STORE";
            if(theme.Action(new Rect(portraitX,646,portraitWidth,38),heroAction,true,locked:heroReleaseLocked))
            {
                if(heroOwned)SetPage(Page.Play);
                else
                {
                    heroShop=true;skinShop=shardShop=itemShop=weaponShop=mapShop=battlePassShop=false;
                    SetPage(Page.Shop);
                }
            }

            theme.Label(new Rect(attacksX,212,attacksWidth,28),"COMBAT LOADOUT",theme.Heading);
            theme.FitLabel(new Rect(attacksX,240,attacksWidth,20),
                heroId==HeroId.Traveler?"Premium presentation • same fair combat budget":"Mobile combat loadout • damage / range / cooldown / effects",
                theme.Small,true);
            HeroAttackCard[] attacks=HeroAttackCatalog.For(heroId);
            const float cardHeight=82f,gap=5f;
            for(int i=0;i<attacks.Length;i++)
            {
                Rect card=new Rect(attacksX,266+i*(cardHeight+gap),attacksWidth,cardHeight);
                DrawHeroAttackCard(card,attacks[i],accent,i==attacks.Length-1);
            }
        }

        private void DrawHeroAttackCard(Rect rect,HeroAttackCard attack,Color accent,bool ultimate)
        {
            theme.Panel(rect);
            FrontEndTheme.Fill(new Rect(rect.x,rect.y,4,rect.height),accent);
            Rect keyRect=new Rect(rect.x+14,rect.y+10,76,24);
            FrontEndTheme.Fill(keyRect,ultimate?accent:FrontEndTheme.Raised);
            theme.Label(keyRect,attack.Key,theme.Small);
            theme.FitLabel(new Rect(rect.x+102,rect.y+7,rect.width-116,26),attack.Name,theme.Body);
            theme.FitLabel(new Rect(rect.x+14,rect.y+36,rect.width-28,18),attack.Stats,theme.Small,true);
            theme.Label(new Rect(rect.x+14,rect.y+56,rect.width-28,22),attack.Effect,theme.Small,true);
        }
        private void ChangeHero(int index)
        {
            if (index==selectedHero) return;
            selectedHero=Mathf.Clamp(index,0,HeroIdentity.HeroCount-1);
            weaponSkinVariant=0;
            HeroId hero=(HeroId)selectedHero;
            if(HeroUnlockInventory.Owned(hero))PlayerHeroSelection.Select(hero);
            bool previewSkin=page==Page.Shop&&skinShop&&!shardShop&&!itemShop&&!weaponShop&&!mapShop&&!battlePassShop&&!heroShop;
            bool previewInventory=page==Page.Inventory&&collection.InventoryPreview;
            bool previewWeapon=page==Page.Shop&&weaponShop;
            showcase.Present(hero,true,
                previewSkin?null:previewWeapon?(bool?)false:null,
                previewWeapon?(bool?)true:null,
                previewSkin?(UniversalHeroCosmetic?)collection.Preview(false):previewInventory?(UniversalHeroCosmetic?)collection.Preview(true):null);
        }
        private void DrawShop()
        {
            if(shopMessage=="Saving purchase…"||PaidShardCommerce.Spending)shopMessage=PaidShardCommerce.Instance?.Status??shopMessage;
            Heading("Store","Cosmetics, premium heroes, arenas, shards, battle supply and seasonal progression.");
            float tabW=104f,tabGap=7f;
            bool avatarShop=!skinShop&&!shardShop&&!itemShop&&!weaponShop&&!mapShop&&!battlePassShop&&!heroShop;
            if(theme.Action(new Rect(Left,204,tabW,52),"COSMETICS",selected:skinShop))
            {skinShop=true;shardShop=itemShop=weaponShop=mapShop=battlePassShop=heroShop=false;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap),204,tabW,52),"AVATARS",selected:avatarShop))
            {skinShop=shardShop=itemShop=weaponShop=mapShop=battlePassShop=heroShop=false;shopMessage="";}
            theme.Label(new Rect(Right-264,116,264,42),collection.BalanceText((HeroId)selectedHero),theme.Small);
            Rect shardTab=new Rect(Left+(tabW+tabGap)*2,204,tabW,52);
            if(theme.Action(shardTab,"SHARDS",selected:shardShop,accent:ShardShopVisualOverlay.TabAccent))
            {skinShop=itemShop=weaponShop=mapShop=battlePassShop=heroShop=false;shardShop=true;shopMessage="";}
            ShardShopVisualOverlay.DrawTabSparkles(shardTab,shardShop);
            if(theme.Action(new Rect(Left+(tabW+tabGap)*3,204,tabW,52),"WEAPONS",selected:weaponShop))
            {skinShop=shardShop=itemShop=mapShop=battlePassShop=heroShop=false;weaponShop=true;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap)*4,204,tabW,52),"HERO",selected:heroShop))
            {skinShop=shardShop=itemShop=weaponShop=mapShop=battlePassShop=false;heroShop=true;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap)*5,204,tabW,52),"MAPS",selected:mapShop))
            {skinShop=shardShop=itemShop=weaponShop=battlePassShop=heroShop=false;mapShop=true;shopMessage="";}
            float wideX=Left+(tabW+tabGap)*6;
            if(theme.Action(new Rect(wideX,204,138,52),"BATTLE ITEM",selected:itemShop))
            {skinShop=shardShop=weaponShop=mapShop=battlePassShop=heroShop=false;itemShop=true;shopMessage="";}
            if(theme.Action(new Rect(wideX+145,204,138,52),"BATTLE PASS",selected:battlePassShop))
            {skinShop=shardShop=itemShop=weaponShop=mapShop=heroShop=false;battlePassShop=true;shopMessage="";}

            if(heroShop){DrawPremiumHeroShop();return;}
            if(battlePassShop){DrawBattlePassShop();return;}
            if(itemShop){DrawBattleItemShop();return;}
            if(mapShop){DrawMapShop();return;}
            if(weaponShop){DrawWeaponSkinShop();return;}
            if(shardShop){DrawShardShop();return;}
            if(skinShop){DrawSkinShop();return;}
            AvatarShopItem item=AvatarShopCatalog.Items[Mathf.Clamp(shopAvatarIndex,0,AvatarShopCatalog.Items.Length-1)];
            bool owned=AvatarInventory.IsOwned(item.AvatarId), equipped=PlayerProfile.Avatar==item.AvatarId;
            float galleryWidth=ContentWidth*.56f;
            theme.Panel(new Rect(Left,270,galleryWidth,390));
            ShopAvatarPreview.Draw(new Rect(Left+32,258,230,230),item,Texture2D.whiteTexture,Time.unscaledTime);
            theme.Label(new Rect(Left+292,264,galleryWidth-320,80),item.Name,theme.Heading);
            theme.Label(new Rect(Left+292,354,galleryWidth-320,52),item.Theme,theme.Body,true);
            theme.Label(new Rect(Left+292,428,galleryWidth-320,36),owned?"OWNED":$"{item.Price} SHARDS",theme.Body);
            if (theme.Action(new Rect(Left+32,542,64,60),"<")) { shopAvatarIndex=(shopAvatarIndex-1+AvatarShopCatalog.Items.Length)%AvatarShopCatalog.Items.Length; shopMessage=""; }
            theme.Center(new Rect(Left+102,542,90,60),$"{shopAvatarIndex+1} / {AvatarShopCatalog.Items.Length}");
            if (theme.Action(new Rect(Left+198,542,64,60),">")) { shopAvatarIndex=(shopAvatarIndex+1)%AvatarShopCatalog.Items.Length; shopMessage=""; }
            bool affordable=PlayerWallet.CanAfford(item.Price);
            string action=equipped?"EQUIPPED":owned?"EQUIP":affordable?"UNLOCK":"NEED MORE SHARDS";
            if (ShardPurchaseAction(new Rect(Left+292,542,galleryWidth-320,60),action,owned?0:item.Price,true,enabled:!cosmeticBusy&&!equipped&&(owned||affordable||PlayerWallet.Balance<item.Price)))
            {
                if (owned) { PlayerProfile.Avatar=item.AvatarId; shopMessage="Avatar equipped."; }
                else { AvatarPurchaseResult result=AvatarShopService.TryPurchase(item); shopPurchasePending=result==AvatarPurchaseResult.Pending; shopMessage=result==AvatarPurchaseResult.Pending?"Saving purchase…":result==AvatarPurchaseResult.Purchased?"Unlocked and equipped.":result==AvatarPurchaseResult.InsufficientShards?"Not enough shards.":"Purchase could not be completed."; }
            }
            theme.Label(new Rect(Left+32,618,galleryWidth-64,26),shopMessage,theme.Small,true);
            float x=Left+galleryWidth+32, w=ContentWidth-galleryWidth-32;
            theme.Label(new Rect(x,244,w,36),"One shard balance",theme.Heading);
            theme.Label(new Rect(x,296,w,100),"Earn shards in matches or buy shard packs. They all enter the same balance and can be spent on any shard-priced item.",theme.Body,true);
            FrontEndTheme.Fill(new Rect(x,436,w,1),FrontEndTheme.Raised);
            theme.Label(new Rect(x,462,w,40),"Premium cosmetics",theme.Heading);
            theme.Label(new Rect(x,518,w,62),FounderEntitlement.Owned?"First Traveler status active.\nYour owned cosmetics remain available.":"The real-money store is not available yet.",theme.Body,true);
            if(RecallCosmeticEntitlement.Owned) theme.Label(new Rect(x,600,w,48),RecallCosmeticCatalog.DisplayName+" / OWNED",theme.Small,true);
        }
        private HeroId PremiumShopHero=>HeroUnlockCatalog.Purchasable((HeroId)selectedHero)?
            (HeroId)selectedHero:HeroId.Traveler;

        private void DrawPremiumHeroShop()
        {
            HeroId hero=PremiumShopHero;
            bool owned=HeroUnlockInventory.Owned(hero);
            bool releaseLocked=!HeroUnlockCatalog.Released(hero);
            int price=HeroUnlockCatalog.Price(hero);
            showcase.Present(hero,true,false,false,null);

            float w=ContentWidth*.54f,x=Left+w+32,rw=ContentWidth-w-32;
            theme.Panel(new Rect(Left,270,w,394));
            FrontEndTheme.Fill(new Rect(Left,270,5,394),Alterverse.Visuals.StylizedArtDirection.HeroAccent(hero));
            float tabWidth=(w-68f)/4f;
            if(theme.Action(new Rect(Left+22,282,tabWidth,32),"TRAVELER",selected:hero==HeroId.Traveler))
                selectedHero=(int)HeroId.Traveler;
            if(theme.Action(new Rect(Left+30+tabWidth,282,tabWidth,32),"NEROK ABYSS",selected:hero==HeroId.Nerok))
                selectedHero=(int)HeroId.Nerok;
            if(theme.Action(new Rect(Left+38+tabWidth*2,282,tabWidth,32),"TIMMY",selected:hero==HeroId.Timmy))
                selectedHero=(int)HeroId.Timmy;
            if(theme.Action(new Rect(Left+46+tabWidth*3,282,tabWidth,32),"VOID KNIGHT",selected:hero==HeroId.VoidKnight))
                selectedHero=(int)HeroId.VoidKnight;
            DrawPortrait(new Rect(Left+22,320,w-44,280));
            theme.Center(new Rect(Left+22,614,w-44,34),hero==HeroId.VoidKnight?"PREMIUM HERO  •  ARMORED SKIRMISHER":hero==HeroId.Timmy?"PREMIUM HERO  •  FELINE ASSASSIN":hero==HeroId.Nerok?
                "PREMIUM HERO  •  TIDAL BRUISER":"PREMIUM HERO  •  TEMPORAL SKIRMISHER");

            theme.Label(new Rect(x,276,rw,58),HeroIdentity.NameFor(hero).ToUpperInvariant(),theme.Title);
            theme.Label(new Rect(x,342,rw,28),hero==HeroId.VoidKnight?"GREATSWORD / PARRY / LIFE DRAIN":hero==HeroId.Timmy?"JETPACK / CLAWS / SINGLE TARGET":hero==HeroId.Nerok?
                "ANCHOR / PULL / AREA CONTROL":"TIME / LIGHT / MOBILITY  •  NO COMBAT POWER ADVANTAGE",theme.Small,true);
            theme.Label(new Rect(x,384,rw,92),
                hero==HeroId.VoidKnight?
                "An armored swordsman from an unknown galaxy. Control distance with a two-handed greatsword, parry incoming strikes and drain enemy health with Eventide Cleave. Coming soon.":hero==HeroId.Timmy?
                "A small cat astronaut with suit claws and a twin-nozzle jetpack. Pounce into range, scratch one enemy and slip away. Coming soon.":hero==HeroId.Nerok?
                "A shark bruiser with a heavy anchor. Hook enemies, protect yourself and draw opponents into an abyssal vortex.":
                "A shadow moving between seconds. Bend chronolight, open paired rifts and control space with temporal fields. Premium presentation; balanced by the same combat budget as every hero.",
                theme.Body,true);
            theme.Label(new Rect(x,494,rw,34),owned?"OWNED":$"{price:N0} SHARDS",theme.Heading);
            FrontEndTheme.Fill(new Rect(x,536,rw,3),Alterverse.Visuals.StylizedArtDirection.HeroAccent(hero));

            string action=releaseLocked?$"UNLOCK {price:N0} SHARDS":owned?"SELECT "+HeroIdentity.NameFor(hero).ToUpperInvariant():
                PlayerWallet.CanAfford(price)?$"UNLOCK {price:N0} SHARDS":$"NEED {price:N0} SHARDS";
            bool enabled=owned||PlayerWallet.CanAfford(price)||!PaidShardCommerce.Spending&&PlayerWallet.Balance<price;
            if(ShardPurchaseAction(new Rect(x,558,rw,62),action,owned?0:price,true,enabled:enabled,locked:releaseLocked))
            {
                if(owned)
                {
                    selectedHero=(int)hero;
                    PlayerHeroSelection.Select(hero);
                    SetPage(Page.Heroes);
                }
                else
                {
                    HeroUnlockResult result=HeroUnlockInventory.Purchase(hero);
                    shopPurchasePending=result==HeroUnlockResult.Pending;
                    shopMessage=result==HeroUnlockResult.Pending?"Saving purchase…":result==HeroUnlockResult.Purchased?
                        HeroIdentity.NameFor(hero).ToUpperInvariant()+" UNLOCKED":
                        result==HeroUnlockResult.InsufficientShards?"Not enough shards.":"Purchase unavailable. Reconnect your account.";
                    if(result==HeroUnlockResult.Purchased)
                    {
                        selectedHero=(int)hero;
                        PlayerHeroSelection.Select(hero);
                        showcase.InvalidateLoadout();
                    }
                }
            }
            theme.Label(new Rect(x,636,rw,38),shopMessage.Length>0?shopMessage:
                releaseLocked?"COMING SOON  •  PURCHASE AND DEPLOYMENT LOCKED":
                $"SHARDS {PlayerWallet.Balance:N0}  •  PRICE {price:N0}",theme.Small,true);
        }

        private void DrawShardShop()
        {
            theme.Label(new Rect(Left,270,ContentWidth*.5f,32),"SHARD EXCHANGE",theme.Heading);
            var store=GooglePlayShardStore.Instance;
            var sub=ShardPacks.Subscription;
            if(sub!=null)
            {
                Rect club=new Rect(Left,312,ContentWidth,128);
                theme.Panel(club);ShardShopVisualOverlay.DrawSubscriptionArtwork(club);
                theme.Label(new Rect(Left+124,322,ContentWidth-400,27),sub.Name+"  /  MONTHLY SUBSCRIPTION",theme.Small);
                theme.Label(new Rect(Left+124,350,ContentWidth-400,34),$"{sub.Amount:N0} SHARDS EVERY MONTH",theme.Heading);
                int discount=store?.SubscriptionDiscount??0;
                string benefit=discount>0?$"SAVE {discount}% per shard vs. available one-time packs":store?.SubscriptionStatus??"Subscription setup in progress";
                theme.Label(new Rect(Left+124,385,ContentWidth-400,22),benefit,theme.Small,true);
                bool active=store?.SubscriptionActive==true;
                string price=store?.Price(sub.Id)??"Unavailable";
                string action=active?"SUBSCRIPTION ACTIVE":price=="Unavailable"?"COMING SOON":price+" / MONTH";
                if(theme.Action(new Rect(Right-250,328,232,48),action,true,enabled:store!=null&&store.CanBuy(sub.Id)))store.Buy(sub.Id);
                if(theme.Action(new Rect(Right-250,382,232,26),"MANAGE / CANCEL"))store?.ManageSubscription();
                theme.Label(new Rect(Left+16,410,ContentWidth-32,28),"Optional. Automatically bills monthly until cancelled in Google Play. Shards after each successful payment.",theme.Small,true);
            }
            int packCount=ShardPacks.All.Length;
            float w=packCount>0?(ContentWidth-14f*(packCount-1))/packCount:0f;
            for(int i=0;i<packCount;i++)
            {
                var pack=ShardPacks.All[i];float x=Left+i*(w+14);
                Rect card=new Rect(x,446,w,192);theme.Panel(card);
                ShardShopVisualOverlay.DrawPackCard(card,pack,i);
                if(theme.Action(new Rect(x+14,584,w-28,44),store!=null?store.Price(pack.Id):"Unavailable",true,enabled:store!=null&&store.CanBuy(pack.Id)))store.Buy(pack.Id);
            }
            theme.Label(new Rect(Left,648,ContentWidth-190,42),store!=null?store.Status:"Store unavailable.",theme.Small,true);
            if(theme.Action(new Rect(Right-170,648,170,42),"RECONNECT",enabled:store!=null&&!store.Busy))store.Reconnect();
        }

        private void DrawSkinShop()
        {
            collection.Draw(theme,new Rect(Left,270,ContentWidth,410),(HeroId)selectedHero,false,showcase,changeCollectionHero,OpenShardStore);
        }

        private void DrawWeaponSkinShop()
        {
            HeroId hero=(HeroId)selectedHero;
            WeaponSkinItem item=WeaponSkinCatalog.For(hero,weaponSkinVariant);
            bool heroAvailable=HeroUnlockInventory.Owned(hero);
            if(hero==HeroId.Timmy)
            {
                showcase.Present(hero,true);DrawPortrait(new Rect(Left,270,ContentWidth*.54f,340));
                theme.Label(new Rect(Left+ContentWidth*.54f+32,282,ContentWidth*.46f-32,120),"ANDROMEDA TIMMY  /  ORIGINAL SUIT CLAWS",theme.Heading,true);
                theme.Label(new Rect(Left+ContentWidth*.54f+32,412,ContentWidth*.46f-32,100),"Weapon cosmetics are not available yet.",theme.Body,true);
                if(theme.Action(new Rect(Left,614,64,54),"<"))ChangeHero((selectedHero+HeroIdentity.HeroCount-1)%HeroIdentity.HeroCount);
                if(theme.Action(new Rect(Right-64,614,64,54),">"))ChangeHero((selectedHero+1)%HeroIdentity.HeroCount);
                return;
            }
            bool owned=WeaponSkinInventory.Owned(hero,weaponSkinVariant);
            bool equipped=WeaponSkinInventory.EquippedVariant(hero)==weaponSkinVariant;
            float w=ContentWidth*.54f,x=Left+w+32,rw=ContentWidth-w-32;

            showcase.Present(hero,true,false,true,null,weaponSkinVariant);
            DrawPortrait(new Rect(Left,270,w,340));
            FrontEndTheme.Fill(new Rect(Left+24,286,5,290),item.Glow);
            theme.Label(new Rect(Left+42,282,w-84,28),"WEAPON SKIN PREVIEW",theme.Small,true);

            if(theme.Action(new Rect(Left,614,64,54),"<")){ChangeHero((selectedHero+HeroIdentity.HeroCount-1)%HeroIdentity.HeroCount);shopMessage="";}
            theme.Center(new Rect(Left+72,614,w-144,54),$"{HeroName}  /  {item.WeaponName}");
            if(theme.Action(new Rect(Left+w-64,614,64,54),">")){ChangeHero((selectedHero+1)%HeroIdentity.HeroCount);shopMessage="";}

            theme.Label(new Rect(x,282,rw,62),item.Name,theme.Title);
            theme.Label(new Rect(x,350,rw,34),item.WeaponName.ToUpperInvariant()+"  /  COSMETIC WEAPON SKIN",theme.Small,true);
            theme.Label(new Rect(x,396,rw,76),item.Description+"  Visual only — no gameplay advantage.",theme.Body,true);

            bool variantChanged=false;
            if(theme.Action(new Rect(x,480,54,44),"<")){weaponSkinVariant=(weaponSkinVariant+1)%2;variantChanged=true;}
            theme.Center(new Rect(x+62,480,rw-124,44),$"VARIANT {weaponSkinVariant+1} / 2");
            if(theme.Action(new Rect(x+rw-54,480,54,44),">")){weaponSkinVariant=(weaponSkinVariant+1)%2;variantChanged=true;}
            if(variantChanged)
            {
                shopMessage="";
                showcase.Present(hero,true,false,true,null,weaponSkinVariant);
            }

            theme.Label(new Rect(x,532,rw,32),owned?(equipped?"OWNED  •  EQUIPPED":"OWNED"):item.Price+" SHARDS",theme.Heading);
            FrontEndTheme.Fill(new Rect(x,570,rw,3),item.Glow);

            string originalName=hero==HeroId.Veyr?"SHADOW STEEL":"ORIGINAL";
            string action=!HeroUnlockCatalog.Released(hero)?"COMING SOON":!heroAvailable?"UNLOCK HERO FIRST":equipped?$"USE {originalName}":owned?"EQUIP":
                PlayerWallet.CanAfford(item.Price)?"BUY & EQUIP":$"NEED {item.Price} SHARDS";
            bool enabled=heroAvailable&&(equipped||owned||PlayerWallet.CanAfford(item.Price)||!PaidShardCommerce.Spending&&PlayerWallet.Balance<item.Price);
            if(ShardPurchaseAction(new Rect(x,588,rw,54),action,equipped||owned?0:item.Price,true,enabled:enabled))
            {
                if(equipped)
                {
                    WeaponSkinInventory.EquipOriginal(hero);
                    shopMessage=originalName+" equipped.";
                }
                else if(owned)
                {
                    WeaponSkinInventory.Equip(hero,weaponSkinVariant);
                    shopMessage=item.Name+" equipped.";
                }
                else
                {
                    AvatarPurchaseResult result=WeaponSkinInventory.Purchase(hero,weaponSkinVariant);
                    shopPurchasePending=result==AvatarPurchaseResult.Pending;
                    shopMessage=result==AvatarPurchaseResult.Pending?"Saving purchase…":result==AvatarPurchaseResult.Purchased?item.Name+" unlocked and equipped.":
                        result==AvatarPurchaseResult.InsufficientShards?"Not enough shards.":"Purchase could not be completed.";
                }
                showcase.InvalidateLoadout();
                showcase.Present(hero,true,false,true,null,weaponSkinVariant);
            }

            theme.Label(new Rect(x,650,rw,42),shopMessage.Length>0?shopMessage:
                $"SHARDS {PlayerWallet.Balance}  •  TWO DISTINCT SKINS PER HERO",theme.Small,true);
        }

        private void DrawMapShop()
        {
            theme.Label(new Rect(Left,276,ContentWidth,36),"Arena Collection",theme.Heading);
            theme.Label(new Rect(Left,316,ContentWidth,28),"Unlock arenas here. Arena selection and shuffle are handled from PLAY.",theme.Small,true);
            int count=ArenaShopCatalog.PurchasableItems.Length;
            const int visible=3;
            int maxStart=Mathf.Max(0,count-visible);
            mapShopPageStart=Mathf.Clamp(mapShopPageStart,0,maxStart);

            float gap=14f,cardW=(ContentWidth-gap*(visible-1))/visible;
            for(int slot=0;slot<visible;slot++)
            {
                int index=mapShopPageStart+slot;
                if(index>=count)break;

                ArenaShopItem item=ArenaShopCatalog.PurchasableItems[index];
                ArenaVariant arena=item.Variant;
                bool owned=ArenaUnlockInventory.Owned(arena);
                bool releaseLocked=!ArenaShopCatalog.PurchaseAvailable(arena);
                float x=Left+slot*(cardW+gap);

                theme.Panel(new Rect(x,356,cardW,274));
                FrontEndTheme.Fill(new Rect(x,356,cardW,5),item.Accent);
                FrontEndTheme.Fill(new Rect(x+18,378,cardW-36,72),new Color(item.Accent.r*.18f,item.Accent.g*.18f,item.Accent.b*.18f,.72f));
                FrontEndTheme.Fill(new Rect(x+36,416,cardW-72,3),new Color(item.Accent.r,item.Accent.g,item.Accent.b,.72f));

                theme.Label(new Rect(x+18,466,cardW-36,32),item.Name,theme.Heading);
                theme.Label(new Rect(x+18,500,cardW-36,22),releaseLocked?"COMING SOON":item.Subtitle,theme.Small,true);
                string status=owned
                    ?(item.Price>0?"OWNED":"FREE")
                    :item.Price+" SHARDS";
                theme.Label(new Rect(x+18,530,cardW-36,28),status,theme.Body);

                string action;
                bool enabled=true;
                if(releaseLocked)action=$"UNLOCK {item.Price}";
                else if(owned){action=item.Price>0?"OWNED":"FREE";enabled=false;}
                else if(PlayerWallet.CanAfford(item.Price))action=$"UNLOCK {item.Price}";
                else {action=$"NEED {item.Price} SHARDS";enabled=!PaidShardCommerce.Spending&&PlayerWallet.Balance<item.Price;}

                if(ShardPurchaseAction(new Rect(x+18,572,cardW-36,44),action,item.Price,enabled:enabled,locked:releaseLocked))
                {
                    if(!owned)
                    {
                        ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(arena);
                        shopPurchasePending=result==ArenaPurchaseResult.Pending;
                        if(result==ArenaPurchaseResult.Purchased)
                            shopMessage=item.Name+" UNLOCKED. Choose it from PLAY.";
                        else if(result==ArenaPurchaseResult.Pending)shopMessage="Saving purchase…";
                        else if(result==ArenaPurchaseResult.InsufficientShards)shopMessage="Not enough shards.";
                        else shopMessage="Arena purchase could not be completed.";
                    }
                }
            }

            bool canBack=mapShopPageStart>0;
            bool canForward=mapShopPageStart<maxStart;
            if(theme.Action(new Rect(Left,640,48,38),"<",enabled:canBack))
            {
                mapShopPageStart=Mathf.Max(0,mapShopPageStart-visible);
                shopMessage="";
            }
            int first=count==0?0:mapShopPageStart+1;
            int last=Mathf.Min(count,mapShopPageStart+visible);
            theme.Center(new Rect(Left+56,640,150,38),$"{first}–{last} / {count}");
            if(theme.Action(new Rect(Left+214,640,48,38),">",enabled:canForward))
            {
                mapShopPageStart=Mathf.Min(maxStart,mapShopPageStart+visible);
                shopMessage="";
            }

            theme.Label(new Rect(Left,684,ContentWidth,24),shopMessage.Length>0?shopMessage:
                $"MAP SELECTION  /  PLAY MENU    •    SHARDS {PlayerWallet.Balance}",theme.Small,true);
        }

        private void DrawBattlePassShop()
        {
            if(BattlePassProgress.HasUnseenLevel&&string.IsNullOrEmpty(shopMessage))
            {
                shopMessage=$"BATTLE PASS LEVEL UP  •  LEVEL {BattlePassProgress.Level}";
                BattlePassProgress.AcknowledgeCurrentLevel();
            }
            const int visible=4;
            int current=BattlePassProgress.Level;
            battlePassPreviewLevel=Mathf.Clamp(battlePassPreviewLevel,1,BattlePassProgress.MaxLevel);
            battlePassPageStart=Mathf.Clamp(battlePassPageStart,1,BattlePassProgress.MaxLevel-visible+1);
            Color gold=new Color(1f,.76f,.38f);
            float previewW=Mathf.Clamp(ContentWidth*.28f,310f,380f);
            float trackW=ContentWidth-previewW-20f;
            float px=Left+trackW+20f;

            // The season banner pairs immediate progress with a tangible final reward.
            Rect seasonBanner=new Rect(Left,270,trackW,94);
            backdrop.DrawCard(seasonBanner,1);
            FrontEndTheme.Fill(seasonBanner,new Color(.018f,.028f,.043f,.68f));
            theme.Frame(seasonBanner);
            FrontEndTheme.Fill(new Rect(Left+18,288,4,56),FrontEndTheme.Accent);
            theme.FitLabel(new Rect(Left+34,281,trackW-260,22),BattlePassProgress.SeasonName,theme.Small,true);
            theme.FitLabel(new Rect(Left+34,305,trackW-260,38),$"LEVEL {current} / {BattlePassProgress.MaxLevel}",theme.Heading);
            float progressX=Left+trackW-218;
            theme.FitLabel(new Rect(progressX,288,198,24),current>=BattlePassProgress.MaxLevel?"SEASON COMPLETE":$"{BattlePassProgress.XpToNextLevel:N0} XP TO NEXT",theme.Small);
            theme.Progress(new Rect(progressX,322,198,9),BattlePassProgress.LevelProgress);
            theme.FitLabel(new Rect(progressX,338,198,18),$"{BattlePassProgress.Xp:N0} TOTAL BP XP",theme.Small,true);

            if(theme.Action(new Rect(px,270,previewW,94),"",selected:battlePassPreviewLevel==50&&battlePassPreviewPremium))
            {battlePassPreviewLevel=50;battlePassPreviewPremium=true;battlePassPageStart=47;}
            HeadwearIconRenderer.Draw(new Rect(px+8,280,78,72));
            theme.FitLabel(new Rect(px+92,283,previewW-108,20),"THE FINAL REWARD",theme.Small,true);
            theme.FitLabel(new Rect(px+92,307,previewW-108,28),HeadwearSelection.BetaCapName,theme.Heading);
            theme.FitLabel(new Rect(px+92,339,previewW-108,18),"LEVEL 50  •  PREMIUM HEADWEAR",theme.Small,true);

            if(theme.Action(new Rect(Left,378,108,36),"FREE",selected:!battlePassPreviewPremium))battlePassPreviewPremium=false;
            if(theme.Action(new Rect(Left+116,378,132,36),"PREMIUM",selected:battlePassPreviewPremium))battlePassPreviewPremium=true;
            if(theme.Action(new Rect(Left+trackW-150,378,150,36),"MY LEVEL"))
            {battlePassPreviewLevel=current;battlePassPageStart=Mathf.Min(47,1+((current-1)/visible)*visible);}

            float gap=12f,cardW=(trackW-gap*(visible-1))/visible;
            Color trackColor=battlePassPreviewPremium?gold:FrontEndTheme.Accent;
            FrontEndTheme.Fill(new Rect(Left+cardW*.5f,429,trackW-cardW,3),FrontEndTheme.Raised);
            float reached=Mathf.Clamp01((current-battlePassPageStart)/(float)(visible-1));
            FrontEndTheme.Fill(new Rect(Left+cardW*.5f,429,(trackW-cardW)*reached,3),trackColor);
            for(int i=0;i<visible;i++)
            {
                int level=battlePassPageStart+i;
                float x=Left+i*(cardW+gap);
                FrontEndTheme.Fill(new Rect(x+cardW*.5f-5,425,10,10),level<=current?trackColor:FrontEndTheme.Muted);
                DrawBattlePassRewardRow(new Rect(x,444,cardW,174),level,battlePassPreviewPremium);
            }
            DrawBattlePassRewardPreview(new Rect(px,378,previewW,240),battlePassPreviewLevel,battlePassPreviewPremium);

            if(theme.Action(new Rect(Left,632,44,44),"<",enabled:battlePassPageStart>1))battlePassPageStart=Mathf.Max(1,battlePassPageStart-visible);
            theme.Center(new Rect(Left+48,632,92,44),$"{battlePassPageStart}–{battlePassPageStart+visible-1} / 50");
            if(theme.Action(new Rect(Left+144,632,44,44),">",enabled:battlePassPageStart+visible<=BattlePassProgress.MaxLevel))battlePassPageStart=Mathf.Min(47,battlePassPageStart+visible);
            int available=BattlePassProgress.AvailableClaimCount();
            if(theme.Action(new Rect(Left+200,632,trackW-200,44),available>0?$"CLAIM ALL  •  {available} READY":"ALL AVAILABLE REWARDS CLAIMED",available>0,enabled:available>0))
            {int claimed=BattlePassProgress.ClaimAllAvailable();shopMessage=$"{claimed} Battle Pass rewards claimed.";}
            if(!BattlePassProgress.PremiumUnlocked)
            {
                bool canBuy=PlayerWallet.CanAfford(BattlePassProgress.PremiumPrice);
                if(ShardPurchaseAction(new Rect(px,632,previewW,44),canBuy?$"UNLOCK PREMIUM • {BattlePassProgress.PremiumPrice:N0} SHARDS":$"PREMIUM • NEED {BattlePassProgress.PremiumPrice-PlayerWallet.Balance:N0} SHARDS",BattlePassProgress.PremiumPrice,true,enabled:canBuy||!PaidShardCommerce.Spending&&PlayerWallet.Balance<BattlePassProgress.PremiumPrice))
                {
                    BattlePassPurchaseResult result=BattlePassProgress.UnlockPremium();
                    shopPurchasePending=result==BattlePassPurchaseResult.Pending;
                    shopMessage=result==BattlePassPurchaseResult.Pending?"Saving purchase…":result==BattlePassPurchaseResult.Unlocked?"PREMIUM BATTLE PASS UNLOCKED.":"Premium purchase unavailable.";
                }
            }
            else theme.Center(new Rect(px,632,previewW,44),"PREMIUM ACTIVE");
            theme.FitLabel(new Rect(Left,684,ContentWidth,20),shopMessage.Length>0?shopMessage:
                $"PLAY MATCHES. EARN BP XP. CLAIM REWARDS.    •    {BattlePassProgress.PremiumShardReturn} SHARDS AVAILABLE THROUGH PREMIUM REWARDS",theme.Small,true);
        }

        private void DrawBattlePassRewardRow(Rect rect,int level,bool premium)
        {
            BattlePassReward reward=premium?BattlePassProgress.PremiumRewardFor(level):BattlePassProgress.FreeRewardFor(level);
            string status=BattlePassProgress.Status(premium,level);
            bool selected=battlePassPreviewLevel==level&&battlePassPreviewPremium==premium;
            Color accent=premium?new Color(1f,.76f,.38f):FrontEndTheme.Accent;
            if(theme.Action(rect,"",selected:selected))
            {battlePassPreviewLevel=level;battlePassPreviewPremium=premium;}
            FrontEndTheme.Fill(new Rect(rect.x+12,rect.y+4,rect.width-24,3),accent);
            theme.FitLabel(new Rect(rect.x+12,rect.y+13,rect.width-24,22),$"LEVEL {level}",theme.Small);
            if(!reward.IsEmpty)
            {
                float iconSize=62f;
                Rect icon=reward.Headwear==UniversalHeroCosmetic.BetaCap
                    ?new Rect(rect.x+10,rect.y+34,rect.width-20,74)
                    :new Rect(rect.center.x-iconSize*.5f,rect.y+42,iconSize,iconSize);
                DrawBattlePassRewardIcon(icon,reward);
                string title=reward.Headwear==UniversalHeroCosmetic.BetaCap?HeadwearSelection.BetaCapName:reward.AvatarId>=0?"BETA CROWN":reward.Shards>0?$"{reward.Shards} SHARDS + CORE":$"{reward.ItemAmount}× {BattleItemInventory.Name(reward.Item)}";
                theme.FitLabel(new Rect(rect.x+10,rect.y+111,rect.width-20,22),title,theme.Small);
            }
            else theme.Center(new Rect(rect.x+10,rect.y+48,rect.width-20,72),"KEEP GOING");
            bool available=premium?BattlePassProgress.CanClaimPremium(level):BattlePassProgress.CanClaimFree(level);
            FrontEndTheme.Fill(new Rect(rect.x+10,rect.yMax-30,rect.width-20,22),available?new Color(.26f,.20f,.10f):new Color(.035f,.05f,.07f));
            theme.Center(new Rect(rect.x+10,rect.yMax-30,rect.width-20,22),reward.IsEmpty?"PROGRESS TIER":available?"READY TO CLAIM":status);
        }

        private void DrawBattlePassRewardPreview(Rect rect,int level,bool premium)
        {
            BattlePassReward reward=premium?BattlePassProgress.PremiumRewardFor(level):BattlePassProgress.FreeRewardFor(level);
            theme.Panel(rect);
            Color accent=premium?new Color(1f,.76f,.38f):FrontEndTheme.Accent;
            FrontEndTheme.Fill(new Rect(rect.x+16,rect.y+4,rect.width-32,3),accent);
            theme.FitLabel(new Rect(rect.x+16,rect.y+13,rect.width-32,22),$"{(premium?"PREMIUM":"FREE")}  /  LEVEL {level}",theme.Small,true);
            DrawBattlePassRewardIcon(reward.Headwear==UniversalHeroCosmetic.BetaCap
                ?new Rect(rect.x+8,rect.y+40,100,86):new Rect(rect.x+18,rect.y+45,82,82),reward);
            theme.FitLabel(new Rect(rect.x+114,rect.y+46,rect.width-130,28),reward.Headwear==UniversalHeroCosmetic.BetaCap?HeadwearSelection.BetaCapName:reward.AvatarId>=0?"BETA CROWN":reward.Shards>0?$"{reward.Shards} SHARDS":reward.IsEmpty?"KEEP GOING":$"{reward.ItemAmount}× CORE",theme.Heading);
            theme.Label(new Rect(rect.x+114,rect.y+80,rect.width-130,50),BattlePassRewardDescription(reward),theme.Small,true);
            theme.FitLabel(new Rect(rect.x+16,rect.y+141,rect.width-32,22),reward.IsEmpty?"No free reward at this level.":reward.Label,theme.Small);
            bool canClaim=premium?BattlePassProgress.CanClaimPremium(level):BattlePassProgress.CanClaimFree(level);
            string status=BattlePassProgress.Status(premium,level);
            string action=canClaim?"CLAIM REWARD":reward.IsEmpty?"KEEP EARNING XP":status=="CLAIMED"?"CLAIMED":premium&&!BattlePassProgress.PremiumUnlocked?"PREMIUM REQUIRED":$"REACH LEVEL {level}";
            if(theme.Action(new Rect(rect.x+16,rect.yMax-58,rect.width-32,42),action,canClaim,enabled:canClaim))
            {
                bool claimed=premium?BattlePassProgress.ClaimPremium(level):BattlePassProgress.ClaimFree(level);
                if(claimed)shopMessage=(premium?"Premium":"Free")+" reward claimed.";
            }
        }

        private void DrawBattlePassRewardIcon(Rect rect,BattlePassReward reward)
        {
            if(reward.Headwear==UniversalHeroCosmetic.BetaCap){HeadwearIconRenderer.Draw(rect);return;}
            if(reward.AvatarId>=0) { AvatarBadgeRenderer.Draw(rect,reward.AvatarId,Texture2D.whiteTexture,Time.unscaledTime); return; }
            if(reward.Item!=BattleItemType.None) { BattleItemIconRenderer.Draw(rect,reward.Item); return; }
            if(reward.Shards>0) { DrawShardRewardIcon(rect); return; }
            FrontEndTheme.Fill(rect,new Color(.035f,.045f,.062f,.72f));
        }

        private void DrawShardRewardIcon(Rect rect)
        {
            Color cyan=new(.10f,.88f,1f,1f);
            FrontEndTheme.Fill(rect,new Color(.025f,.045f,.065f,.96f));
            float w=rect.width*.34f,h=rect.height*.62f;
            FrontEndTheme.Fill(new Rect(rect.center.x-w*.5f,rect.center.y-h*.5f,w,h),new Color(cyan.r,cyan.g,cyan.b,.72f));
            FrontEndTheme.Fill(new Rect(rect.center.x-w*.18f,rect.y+rect.height*.10f,w*.36f,rect.height*.80f),cyan);
            FrontEndTheme.Fill(new Rect(rect.x+rect.width*.18f,rect.y+rect.height*.62f,rect.width*.18f,rect.height*.20f),new Color(.3f,.95f,1f,.8f));
            FrontEndTheme.Fill(new Rect(rect.x+rect.width*.68f,rect.y+rect.height*.22f,rect.width*.14f,rect.height*.22f),new Color(.3f,.95f,1f,.7f));
        }

        private string BattlePassRewardDescription(BattlePassReward reward)
        {
            if(reward.Headwear==UniversalHeroCosmetic.BetaCap)return "Exclusive BETA cap. Equip per hero in Inventory > Cosmetics > Hats. Works with wings.";
            if(reward.AvatarId==BattlePassProgress.BetaCrownAvatarId)return "Exclusive Beta Crown avatar.";
            if(reward.Item!=BattleItemType.None)return BattleItemInventory.ShortDescription(reward.Item);
            if(reward.Shards>0)return "Shard currency reward.";
            return "No reward on this track at this level.";
        }

        private void DrawBattleItemShop()
        {
            theme.Label(new Rect(Left,276,ContentWidth,36),"Battle Supply",theme.Heading);
            theme.Label(new Rect(Left,316,ContentWidth,28),"All Battle Items cost 100 shards. Buy as many as you want and equip one for the next match.",theme.Small,true);

            BattleItemType[] items=BattleItemInventory.All;
            float gap=14f;
            float cardW=(ContentWidth-gap)*.5f;
            float cardH=156f;
            for(int i=0;i<items.Length;i++)
            {
                BattleItemType item=items[i];
                int row=i/2,col=i%2;
                float x=Left+col*(cardW+gap);
                float y=350+row*(cardH+10);
                theme.Panel(new Rect(x,y,cardW,cardH));
                BattleItemIconRenderer.Draw(new Rect(x+14,y+18,78,78),item);
                theme.FitLabel(new Rect(x+106,y+12,cardW-120,30),BattleItemInventory.Name(item),theme.Heading);
                theme.Label(new Rect(x+106,y+42,cardW-120,42),BattleItemInventory.ShortDescription(item),theme.Small,true);
                theme.Label(new Rect(x+106,y+86,cardW-120,20),$"OWNED  {BattleItemInventory.Count(item)}",theme.Small,true);
                theme.Label(new Rect(x+14,y+116,104,28),$"{BattleItemInventory.ShardPrice} SHARDS",theme.Heading);

                bool canBuy=PlayerWallet.CanAfford(BattleItemInventory.ShardPrice);
                string action=canBuy?"BUY 1":$"NEED {BattleItemInventory.ShardPrice} SHARDS";
                if(ShardPurchaseAction(new Rect(x+128,y+110,cardW-142,38),action,BattleItemInventory.ShardPrice,true,enabled:canBuy||!PaidShardCommerce.Spending&&PlayerWallet.Balance<BattleItemInventory.ShardPrice))
                {
                    bool bought=BattleItemInventory.TryBuy(item);
                    shopMessage=bought
                        ?BattleItemInventory.Name(item)+" added to inventory."
                        :PaidShardCommerce.Spending?"Saving purchase…":"Purchase failed.";
                    if(PaidShardCommerce.Spending)shopPurchasePending=true;
                }
            }

            theme.Label(new Rect(Left,682,ContentWidth-180,30),
                shopMessage.Length>0?shopMessage:"Victory drops still remain available in addition to shop purchases.",theme.Small,true);
            if(theme.Action(new Rect(Left+ContentWidth-166,674,166,42),"OPEN INVENTORY"))SetPage(Page.Inventory);
        }

        private void DrawInventory()
        {
            Heading("Inventory","Your collection. A personal loadout for every hero.");
            collection.DrawInventoryTabs(theme,new Rect(Left,204,ContentWidth,52));
            collection.Draw(theme,new Rect(Left,270,ContentWidth,410),(HeroId)selectedHero,true,showcase,changeCollectionHero);
        }

        private string XpLabel() => PlayerExperience.Level>=PlayerExperience.MaxLevel ? "MAX LEVEL" : $"{PlayerExperience.XpIntoLevel} / {PlayerExperience.XpIntoLevel+PlayerExperience.XpToNextLevel} XP";
        private void DrawProfile()
        {
            Heading("Profile","Your identity and lifetime progress.");
            float identityWidth=ContentWidth*.54f;
            theme.Panel(new Rect(Left,220,identityWidth,450));
            AvatarBadgeRenderer.Draw(new Rect(Left+24,246,74,74),PlayerProfile.Avatar,Texture2D.whiteTexture,Time.unscaledTime);
            theme.FitLabel(new Rect(Left+120,244,identityWidth-148,34),PlayerProfile.PlayerName,theme.Heading);
            theme.Label(new Rect(Left+120,286,identityWidth-148,26),$"{PlayerProfile.DisplayTitle}  /  LEVEL {PlayerExperience.Level}",theme.Small,true);
            theme.Progress(new Rect(Left+24,348,identityWidth-48,6),PlayerExperience.LevelProgress);
            theme.Label(new Rect(Left+24,364,identityWidth-48,28),XpLabel(),theme.Small,true);
            theme.Label(new Rect(Left+24,412,identityWidth-48,24),"DISPLAY NAME",theme.Small,true);
            theme.FitLabel(new Rect(Left+24,446,identityWidth-224,54),PlayerProfile.PlayerName,theme.Body);
            // Player identity is assigned automatically; no editable names or reroll controls.
            theme.Label(new Rect(Left+24,510,identityWidth-48,58),profileMessage.Length>0?profileMessage:"Your Google Play Games name is used after your cloud account is connected.",theme.Small,true);
            int count=FounderEntitlement.Owned?3:2;
            for(int i=0;i<count;i++)
            {
                Rect avatar=new Rect(Left+24+i*60,592,52,54);
                int avatarId=i==2?6:i;
                if(theme.Action(avatar,"",selected:PlayerProfile.Avatar==avatarId)) PlayerProfile.Avatar=avatarId;
                AvatarBadgeRenderer.Draw(new Rect(avatar.x+5,avatar.y+5,42,44),avatarId,Texture2D.whiteTexture,Time.unscaledTime);
            }
            if (theme.Action(new Rect(Left+identityWidth-112,592,88,54),"MORE")){skinShop=shardShop=itemShop=weaponShop=mapShop=battlePassShop=heroShop=false;SetPage(Page.Shop);}
            float x=Left+identityWidth+32,w=ContentWidth-identityWidth-32;
            ProfileStats stats=ProfileStatsSummary.Current;
            theme.Label(new Rect(x,230,w,36),"Lifetime record",theme.Heading);
            Stat(x,290,w,"MATCHES",stats.Matches.ToString());
            Stat(x,346,w,"WINS / WIN RATE",$"{stats.Wins} / {stats.WinRate:0}%");
            Stat(x,402,w,"KILLS",ProgressionSummary.Kills.ToString());
            Stat(x,458,w,"K/D",ProgressionSummary.KillDeathRatioLabel);
            Stat(x,514,w,"SHARDS",PlayerWallet.Balance.ToString());
            AccountLoginPanel.Draw(theme,new Rect(x,626,w,48));
        }
        private void DrawQuests()
        {
            Heading("Daily missions","Claim your daily login reward and completed missions here. Resets at 00:00 Europe/Berlin.");
            float width=Mathf.Min(960,ContentWidth);
            float x=Left+(ContentWidth-width)*.5f;
            theme.Panel(new Rect(x,208,width,100));
            theme.Label(new Rect(x+18,216,width-316,24),"DAILY LOGIN STREAK",theme.Heading);
            int currentDay=LoginStreakProgress.CurrentDay;
            bool received=LoginStreakProgress.ReceivedToday;
            bool canClaim=LoginStreakProgress.CanClaimToday;
            bool clockBehind=LoginStreakProgress.ClockBehind;
            string loginStatus=clockBehind?"CHECK DEVICE DATE":received?$"NEXT: DAY {currentDay%LoginStreakProgress.CycleDays+1} TOMORROW":"CLAIM ONCE PER DAY";
            theme.Label(new Rect(x+width-298,216,280,24),loginStatus,theme.Small,true);
            float cellW=(width-36)/LoginStreakProgress.CycleDays;
            for(int day=1;day<=LoginStreakProgress.CycleDays;day++)
            {
                Rect cell=new Rect(x+18+(day-1)*cellW,246,cellW-6,52);
                bool current=currentDay==day;
                FrontEndTheme.Fill(cell,current?FrontEndTheme.Raised:FrontEndTheme.Surface);
                theme.Center(new Rect(cell.x,cell.y,cell.width,22),$"DAY {day}  •  {LoginStreakProgress.RewardForDay(day)} SHARDS");
                if(current&&!received)
                {
                    if(theme.Action(new Rect(cell.x+4,cell.y+24,cell.width-8,26),"CLAIM",true,enabled:canClaim))
                        LoginStreakProgress.ClaimForToday();
                }
                else
                    theme.Center(new Rect(cell.x,cell.y+25,cell.width,20),current?"RECEIVED":day<currentDay?"DONE":"LOCKED");
            }
            for(int i=0;i<PlayerChallengeContent.Daily.Length;i++)
            {
                ChallengeDef quest=PlayerChallengeContent.Daily[i];
                float y=322+i*92;
                theme.Panel(new Rect(x,y,width,84));
                theme.Label(new Rect(x+18,y+8,width-252,26),quest.Name,theme.Heading);
                theme.Label(new Rect(x+18,y+35,width-252,24),quest.Description,theme.Body,true);
                int progress=DailyQuestProgress.Progress(i),target=DailyQuestProgress.Target(i);
                theme.Progress(new Rect(x+18,y+67,width-252,5),target>0?progress/(float)target:0);
                theme.Label(new Rect(x+width-213,y+8,190,22),$"{progress} / {target}  •  {quest.Shards} SHARDS",theme.Small,true);
                bool claimed=DailyQuestProgress.Claimed(i);
                if(theme.Action(new Rect(x+width-205,y+35,180,36),claimed?"CLAIMED":"CLAIM",true,enabled:!claimed&&target>0&&progress>=target))
                    DailyQuestProgress.Claim(i);
            }
        }

        private void Stat(float x,float y,float w,string label,string value)
        {
            theme.Label(new Rect(x,y,w*.57f,40),label,theme.Small,true);
            theme.Label(new Rect(x+w*.6f,y,w*.4f,40),value,theme.Heading);
            FrontEndTheme.Fill(new Rect(x,y+47,w,1),FrontEndTheme.Raised);
        }
        private async void RerollPlayerName()
        {
            if(nameRerollBusy)return;
            nameRerollBusy=true;profileMessage="Saving your generated name…";
            string account=AccountProfilePrefs.Account;
            try
            {
                PlayerProfile.RerollName();
                var commerce=PaidShardCommerce.Instance;
                bool saved=commerce?.Ready==true&&await commerce.FlushProfileAsync();
                if(account!=AccountProfilePrefs.Account)return;
                profileMessage=saved?"Name saved.":"Name saved on this device. Account synchronization pending.";
                if(saved)SocialService.Instance?.Load("/social/state");
            }
            catch(Exception){if(account==AccountProfilePrefs.Account)profileMessage="Name synchronization pending. Try again.";}
            finally{nameRerollBusy=false;}
        }
        private void DrawLeaderboard()
        {
            if(DrawRankingTabs())return;
            Heading("Most Wins", "Worldwide lifetime win rankings powered by Google Play Games.");

            float leftWidth = ContentWidth * .58f;
            theme.Panel(new Rect(Left, 244, leftWidth, 406));
            theme.Label(new Rect(Left + 28, 260, leftWidth - 56, 24), "MOST WINS", theme.Small, true);
            int wins = Alterverse.Profile.MatchRewardService.MatchesWon;
            theme.Label(new Rect(Left + 28, 292, leftWidth - 56, 44), $"WINS {wins:N0}", theme.Title);
            theme.Label(new Rect(Left + 28, 344, leftWidth - 56, 40),
                "Lifetime match victories place you on the global leaderboard.", theme.Body, true);

            theme.Label(new Rect(Left + 28, 410, leftWidth - 56, 22), "GOOGLE PLAY GAMES ACCOUNT", theme.Small, true);
            theme.FitLabel(new Rect(Left + 28, 440, leftWidth - 56, 32), PlayerProfile.PlayerName, theme.Heading);
            theme.Label(new Rect(Left + 28, 484, leftWidth - 56, 54), GooglePlayLevelLeaderboard.Status, theme.Small, true);

            bool configured = GooglePlayLevelLeaderboard.IsConfigured;
            string syncLabel = GooglePlayLevelLeaderboard.IsAuthenticated ? "SYNC WINS" : "CONNECT & SYNC";
            if (theme.Action(new Rect(Left + 28, 558, (leftWidth - 68) * .5f, 58), syncLabel, true, enabled: configured && !GooglePlayLevelLeaderboard.IsBusy))
                GooglePlayLevelLeaderboard.AuthenticateAndSync(true);
            if (theme.Action(new Rect(Left + 40 + (leftWidth - 68) * .5f, 558, (leftWidth - 68) * .5f, 58),
                "OPEN MOST WINS", true, enabled: configured && !GooglePlayLevelLeaderboard.IsBusy))
                GooglePlayLevelLeaderboard.ShowLeaderboard();

            float x = Left + leftWidth + 32, w = ContentWidth - leftWidth - 32;
            theme.Label(new Rect(x, 230, w, 36), "How ranking works", theme.Heading);
            theme.Label(new Rect(x, 282, w, 112),
                "Your lifetime match wins are submitted to Google Play. A higher win total ranks above lower totals.",
                theme.Body, true);
            Stat(x, 420, w, "YOUR WINS", wins.ToString("N0"));
            Stat(x, 476, w, "LAST SYNCED",
                GooglePlayLevelLeaderboard.LastSubmittedScore >= 0
                    ? GooglePlayLevelLeaderboard.LastSubmittedScore.ToString("N0")
                    : "—");
            Stat(x, 532, w, "SCOPE", "ALL-TIME / GLOBAL");
            theme.Label(new Rect(x, 604, w, 56),
                configured ? "Google Play stores the global Most Wins leaderboard."
                    : "SETUP REQUIRED  /  Import the game App ID and leaderboard resources from Google Play Console.",
                theme.Small, true);
        }

        private void DrawSettings()
        {
            Heading("Settings","Make the sound your own.");
            float w=ContentWidth*.64f;
            theme.Panel(new Rect(Left,222,w,438));
            float previousMaster=master, previousMusic=music, previousSfx=sfx;
            DrawVolume(new Rect(Left+28,250,w-56,100),"Master volume",ref master);
            DrawVolume(new Rect(Left+28,380,w-56,100),"Music",ref music);
            DrawVolume(new Rect(Left+28,510,w-56,100),"Sound effects",ref sfx);
            if(previousMaster!=master||previousMusic!=music||previousSfx!=sfx)
                PlayerProfile.SetAudioVolumes(master,music,sfx,false);
            float x=Left+w+32,rw=ContentWidth-w-32;
            theme.Label(new Rect(x,250,rw,38),"MATCH OPTIONS",theme.Heading);
            // Remaining audio controls keep their existing layout.
            theme.Label(new Rect(x,452,rw,28),"Language: "+PlayerProfile.Language,theme.Small,true);
            theme.Label(new Rect(x,492,rw,48),"Progress and settings are saved on this device.",theme.Small,true);
        }
        private void DrawVolume(Rect r,string label,ref float value)
        {
            theme.Label(new Rect(r.x,r.y,r.width-100,34),label,theme.Heading);
            theme.Label(new Rect(r.xMax-90,r.y,90,34),$"{Mathf.RoundToInt(value*100)}%",theme.Body);
            if (theme.Action(new Rect(r.x,r.y+46,64,54),"−")) { value=Mathf.Clamp01(value-.1f); audioDirty=true; }
            Rect track=new Rect(r.x+82,r.y+46,r.width-164,54);
            theme.Progress(new Rect(track.x,track.center.y-3,track.width,6),value);
            // A 54-unit drag target, independent of the narrow visible track.
            int id=GUIUtility.GetControlID(FocusType.Passive,track); Event e=Event.current;
            if (e.type==EventType.MouseDown && track.Contains(e.mousePosition)) { GUIUtility.hotControl=id; value=Mathf.Clamp01((e.mousePosition.x-track.x)/track.width); audioDirty=true; e.Use(); }
            if (GUIUtility.hotControl==id && (e.type==EventType.MouseDrag || e.type==EventType.MouseDown))
            { value=Mathf.Clamp01((e.mousePosition.x-track.x)/track.width); audioDirty=true; e.Use(); }
            if (GUIUtility.hotControl==id && e.type==EventType.MouseUp)
            { value=Mathf.Clamp01((e.mousePosition.x-track.x)/track.width); audioDirty=true; GUIUtility.hotControl=0; e.Use(); }
            FrontEndTheme.Fill(new Rect(track.x+track.width*value-7,track.center.y-13,14,26),FrontEndTheme.Text);
            if (theme.Action(new Rect(r.xMax-64,r.y+46,64,54),"+")) { value=Mathf.Clamp01(value+.1f); audioDirty=true; }
        }
        private void FlushAudio()
        {
            if (!audioDirty) return;
            PlayerProfile.SetAudioVolumes(master,music,sfx,true); audioDirty=false;
        }
        private bool PrepareSelectedMatch()
        {
            if(!AccountServerBootstrap.CloudProfileReady)
            {
                launchMessage="Sign in via PROFILE > GOOGLE ACCOUNT and wait for cloud synchronization.";
                return false;
            }
            // The arena visible in the PLAY slider is the arena that starts.
            // Previously the slider could browse Kryon/Noxara while an older persisted arena
            // (often Moon) remained selected, so START MATCH launched the wrong battlefield.
            if(!ArenaVariantRuntime.ShuffleEnabled&&
               playArenaBrowseIndex>=0&&playArenaBrowseIndex<ArenaShopCatalog.Items.Length)
            {
                ArenaShopItem browsed=ArenaShopCatalog.Items[playArenaBrowseIndex];
                if(!ArenaUnlockInventory.Owned(browsed.Variant))
                {
                    launchMessage=browsed.Name+" LOCKED  •  "+ArenaUnlockInventory.UnlockRequirement(browsed.Variant);
                    return false;
                }
                ArenaVariantRuntime.Select(browsed.Variant);
            }

            ArenaVariantRuntime.EnsureValidSelection();
            ArenaVariant selectedArena=ArenaVariantRuntime.Selected;
            if(!ArenaUnlockInventory.Owned(selectedArena))
            {
                launchMessage="Selected arena is locked.";
                return false;
            }
            HeroId requestedHero=(HeroId)selectedHero;
            if(!HeroUnlockCatalog.Released(requestedHero))
            {
                launchMessage=HeroIdentity.NameFor(requestedHero).ToUpperInvariant()+" COMING SOON";
                return false;
            }
            if(!HeroUnlockInventory.Owned(requestedHero))
            {
                launchMessage=HeroIdentity.NameFor(requestedHero).ToUpperInvariant()+
                    $" LOCKED  •  UNLOCK IN STORE FOR {HeroUnlockCatalog.Price(requestedHero):N0} SHARDS";
                return false;
            }
            if(!PlayerHeroSelection.Select(requestedHero))
            {
                launchMessage="Selected hero is unavailable.";
                return false;
            }
            return true;
        }

        private async System.Threading.Tasks.Task BeginOnlineMatchmakingAsync()
        {
            if (RankedService.Instance?.Searching == true || RankedService.Instance?.Busy == true) return;
            if (launching || choosingMatchMode || !PrepareSelectedMatch()) return;
            if (CoopLobbyService.Instance?.InLobby == true)
            { launchMessage="Leave the private co-op lobby before opening an online party."; return; }
            OnlinePartyOverlay.Show();
            launchMessage="Create a friend party, enter an invite code or find a match.";
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task BeginOfflineMatchAsync()
        {
            if (RankedService.Instance?.Searching == true || RankedService.Instance?.Busy == true) return;
            if(launching || choosingMatchMode || !PrepareSelectedMatch())return;
            var online=OnlinePartyService.Instance;
            var coop=CoopLobbyService.Instance;
            if(OnlinePlanHandshake.Preparing || online!=null&&online.Busy || coop!=null&&coop.Busy ||
                EdgegapOnlineFlow.Instance!=null&&EdgegapOnlineFlow.Instance.Busy)return;
            choosingMatchMode=true;
            try
            {
                if(EdgegapOnlineFlow.Instance!=null)await EdgegapOnlineFlow.Instance.LeavePartyAsync();
                else if(online!=null&&online.InParty)await online.LeavePartyAsync();
                if(online!=null&&online.InParty)
                {launchMessage="Could not leave the online party. Please try again.";return;}
                if(coop!=null&&coop.InLobby)await coop.LeaveLobbyAsync();
                if(coop!=null&&coop.InLobby)
                {launchMessage="Could not leave the co-op lobby. Please try again.";return;}
                OnlinePartyOverlay.Hide();
                CoopAuthority.EndMatch();
                BeginLaunch();
            }
            catch(System.Exception ex)
            {
                launchMessage="Offline match could not start.";
                Debug.LogError("DIMENSIONS FRONT: offline launch failed: "+ex.Message);
            }
            finally { choosingMatchMode=false; }
        }

        private void BeginLaunch()
        {
            if (launching || !PrepareSelectedMatch()) return;
            FlushAudio();
            launchMessage=""; launching=true; launchAt=Time.unscaledTime; GUI.FocusControl(null);
            showcase.Present((HeroId)selectedHero,false);
        }
        private void BeginCoopLaunch(CoopLobbyService coop)
        {
            if(coop==null||!coop.InLobby||!coop.IsLaunching||launching)return;
            if(!AccountServerBootstrap.CloudProfileReady)
            {
                launchMessage="Wait for your Google Play cloud profile before joining this match.";
                return;
            }
            CoopMatchPlan prepared=CoopAuthority.PreparedPlan;
            if(prepared==null)
            {
                if(!coop.TryBuildMatchPlan(out prepared))
                {
                    launchMessage="Co-op match plan unavailable.";
                    return;
                }
                CoopAuthority.Prepare(prepared);
            }
            if(!CoopAuthority.BeginPreparedMatch(coop.IsHost))
            {
                launchMessage="Co-op match could not initialize.";
                return;
            }

            int localSlot=CoopRosterBuilder.FindLocalHumanSlot(CoopAuthority.ActivePlan);
            if(localSlot<0)
            {
                CoopAuthority.EndMatch();
                launchMessage="Local co-op player slot missing.";
                return;
            }

            selectedHero=(int)CoopAuthority.ActivePlan.Session.Slots[localSlot].Hero;
            PlayerHeroSelection.Select((HeroId)selectedHero);
            FlushAudio();
            launchMessage="";
            coopLaunching=true;
            launching=true;
            launchAt=Time.unscaledTime;
            GUI.FocusControl(null);
            showcase.Present((HeroId)selectedHero,false);
        }

        private void DrawLaunch()
        {
            string mode=coopLaunching&&CoopAuthority.ActivePlan!=null
                ?$"PRIVATE CO-OP  •  {CoopAuthority.ActivePlan.HumanCount} PLAYERS  /  {ArenaShopCatalog.For(CoopAuthority.ActivePlan.Arena).Name}  /  ZERO TEAM"
                :ArenaVariantRuntime.ShuffleEnabled
                    ?"MATCH 4v4  /  RANDOM OWNED ARENA  /  ZERO TEAM"
                    : "MATCH 4v4  /  "+ArenaShopCatalog.For(ArenaVariantRuntime.Selected).Name+"  /  ZERO TEAM";
            MatchLoadingPresentation.Draw(theme, coopLaunching
                ? "SYNCHRONIZING BATTLEFIELD" : "PREPARING BATTLEFIELD", mode,
                HeroName+"  /  "+Roles[selectedHero]);
        }
        private void EnterMatch()
        {
            if(!coopLaunching)
            {
                ArenaVariantRuntime.EnsureValidSelection();
                if(!ArenaUnlockInventory.Owned(ArenaVariantRuntime.Selected))
                {
                    ArenaVariantRuntime.Select(ArenaVariant.Moon);
                    launching=false;page=Page.Play;launchMessage="Locked arena blocked. Moon restored.";return;
                }
            }
            else if(!CoopAuthority.Active||CoopAuthority.ActivePlan?.Session==null)
            {
                launching=false;coopLaunching=false;page=Page.Play;launchMessage="Co-op session was lost before launch.";return;
            }

            var bootstrap=FindAnyObjectByType<Alterverse.Prototype.PrototypeBootstrap>();
            if (bootstrap==null)
            {
                launching=false;
                page=coopLaunching?Page.Coop:Page.Play;
                coopLaunching=false;
                launchMessage="Match could not start. Please try again.";
                Debug.LogError("DIMENSIONS FRONT: match bootstrap unavailable.");
                return;
            }

            bool restarting=GameObject.Find("DF_MatchWorld")!=null;
            MatchEntryCurtain.Begin();
            Alterverse.Prototype.PrototypeBootstrap.PrepareTransitionFrame();
            launching=false;visible=false;MatchClock.Set(PauseReason.Result,false);MatchClock.Set(PauseReason.Menu,false);
            // Hide before building: a failed build can synchronously restore this menu.
            // Hiding afterwards would overwrite that recovery and leave a black screen.
            gameObject.SetActive(false);
            if(restarting)bootstrap.StartCoroutine(bootstrap.RestartPrototype());
            else bootstrap.BuildPrototype();
        }
    }
}




