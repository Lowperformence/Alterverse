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
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Page { Home, Play, Heroes, Shop, Inventory, Profile, Quests, Leaderboard, Settings, Coop, BattlePass }
        private static MainMenuController instance;
        public static bool IsVisible => instance != null && instance.visible;
        private static readonly string[] NavigationLabels = { "PLAY", "HEROES", "STORE", "INVENTORY", "BATTLE PASS", "PROFILE", "DAILY", "RANKING", "SETTINGS" };
        private static readonly Page[] NavigationPages = { Page.Play, Page.Heroes, Page.Shop, Page.Inventory, Page.BattlePass, Page.Profile, Page.Quests, Page.Leaderboard, Page.Settings };
        private static readonly int[] HomeNavigation={0,1,2,3,5};
        private static readonly string[] Roles = { "ASSASSIN", "TANK", "MARKSMAN", "MAGE", "SUPPORT", "BRUISER", "TEMPORAL SKIRMISHER", "TIDAL BRUISER" };
        private static readonly string[] RoleDescriptions = {
            "Close the distance. Isolate a vulnerable target, strike, then disengage.",
            "Hold the front. Shield your team and interrupt enemies at close range.",
            "Keep your distance. Deliver sustained fire while allies protect your position.",
            "Control the fight. Place powerful area attacks where enemies want to move.",
            "Keep allies fighting. Heal, shield and slow enemies from the backline.",
            "Win the long fight. Trade at close range with heavy strikes and sustain.",
            "Slip between seconds. Reposition through rifts, control space and strike with chronolight.",
            "Hook an enemy with your anchor. Shield yourself and control fights with a water vortex." };
        private static readonly string[] Strengths = { "BURST / MOBILITY", "PROTECTION / CONTROL", "RANGE / SUSTAINED DAMAGE", "AREA DAMAGE / CONTROL", "HEALING / PROTECTION", "MELEE / ENDURANCE", "MOBILITY / CONTROL", "PULL / AREA CONTROL" };
        private Page page = Page.Home;
        private bool visible = true, launching, coopLaunching, editingName, suspended;
        private bool choosingMatchMode;
        private int selectedHero, shopAvatarIndex, battlePassPageStart=1, battlePassPreviewLevel=1, weaponSkinVariant;
        private int playArenaBrowseIndex=-1,mapShopPageStart;
        private bool battlePassPreviewPremium;
        private bool skinShop=true,shardShop,itemShop,weaponShop,mapShop,battlePassShop,heroShop,cosmeticBusy;
        private float pageChangedAt, launchAt, menuWidth = 1280;
        private bool shopPurchasePending;
        private string profileName, profileMessage = "", shopMessage = "", launchMessage = "", joinCodeInput = "";
        private FrontEndTheme theme;
        private GUIStyle homeLogo,homeSubtitle;
        private MenuCosmicBackdrop backdrop;
        private MenuHeroShowcase showcase;
        private CosmeticCollectionPanel collection;
        private System.Action<int> changeCollectionHero;
        private TouchScreenKeyboard nameKeyboard;
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
            profileName = PlayerProfile.PlayerName;
            master = PlayerProfile.MasterVolume; music = PlayerProfile.MusicVolume; sfx = PlayerProfile.SfxVolume;
            showcase = gameObject.AddComponent<MenuHeroShowcase>();
            collection=new CosmeticCollectionPanel();
            changeCollectionHero=ChangeHero;
            UniversalCosmeticSelection.EnsureMigrated((HeroId)selectedHero);
            if(GooglePlayLevelLeaderboard.IsConfigured)GooglePlayLevelLeaderboard.AuthenticateAndSync();
            MatchClock.Set(PauseReason.Menu,true);
        }
        public static void ShowHome()
        {
            if (instance == null) { new GameObject("DF_MainMenu").AddComponent<MainMenuController>(); return; }
            instance.visible = true; instance.launching = false; instance.coopLaunching=false; instance.page = Page.Home;
            instance.editingName = false; instance.profileName = PlayerProfile.PlayerName;
            instance.profileMessage = ""; instance.shopMessage = ""; instance.pageChangedAt = Time.unscaledTime;
            instance.collection?.Invalidate();
            instance.showcase?.InvalidateLoadout();
            instance.gameObject.SetActive(true);
            if(GooglePlayLevelLeaderboard.IsConfigured)GooglePlayLevelLeaderboard.AuthenticateAndSync();
            MatchClock.Set(PauseReason.Menu,true);
        }

        public static void ShowCoopAfterMatch(string message="")
        {
            if(instance==null)new GameObject("DF_MainMenu").AddComponent<MainMenuController>();
            if(instance==null)return;
            instance.visible=true;instance.launching=false;instance.coopLaunching=false;instance.page=Page.Coop;
            instance.editingName=false;instance.profileName=PlayerProfile.PlayerName;
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
                    page=coopLaunching?Page.Coop:Page.Play;
                    coopLaunching=false;
                }
            }
            if (visible) MatchClock.Set(PauseReason.Menu,true);
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
        private void OnDisable() { FlushAudio(); CloseKeyboard(); }
        private void OnDestroy() { collection?.Dispose();backdrop?.Dispose(); if (instance == this) instance = null; }
        private void Update()
        {
            if (!visible || suspended) return;
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
            if (nameKeyboard != null)
            {
                profileName = nameKeyboard.text;
                if (nameKeyboard.status == TouchScreenKeyboard.Status.Done) { SaveName(); CloseKeyboard(); }
                else if (nameKeyboard.status == TouchScreenKeyboard.Status.Canceled || nameKeyboard.status == TouchScreenKeyboard.Status.LostFocus)
                { CancelName(); }
            }
            if (!OnlinePartyOverlay.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (editingName) CancelName();
                else if (launching) launching = false;
                else SetPage(Page.Home);
            }
            if (launching && Time.unscaledTime-launchAt >= .55f) EnterMatch();
        }
        private void SetPage(Page next)
        {
            if (page == next || launching) return;
            FlushAudio(); CancelName(); collection?.Invalidate();showcase?.InvalidateLoadout(); page = next; pageChangedAt = Time.unscaledTime;
            profileMessage = ""; shopMessage = ""; GUI.FocusControl(null);
            if(next==Page.Leaderboard)GooglePlayLevelLeaderboard.AuthenticateAndSync();
        }
        private void OnGUI()
        {
            if (!visible || OnlinePartyOverlay.IsOpen) return;
            Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color; bool oldEnabled = GUI.enabled;
            try
            {
                GUI.color = Color.white; GUI.enabled = !launching;
                backdrop ??= new MenuCosmicBackdrop();
                backdrop.Draw(new Rect(0,0,Screen.width,Screen.height));
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) return;
                float scale = Mathf.Min(safe.height/MenuHeight,safe.width/1500f);
                menuWidth = safe.width/scale;
                float y = Screen.height-safe.yMax+(safe.height-MenuHeight*scale)*.5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x,y,0),Quaternion.identity,Vector3.one*scale);
                theme ??= new FrontEndTheme();
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
                if(theme.Navigation(new Rect(rail,178+i*51,202,46),NavigationLabels[i],NavigationIcon(NavigationPages[i]),
                    primary:i==0,selected:page==NavigationPages[i]))SetPage(NavigationPages[i]);
            theme.FitLabel(new Rect(Left,30,ContentWidth*.62f,34),"COMMAND DECK  /  "+page.ToString().ToUpperInvariant(),theme.Heading);
            theme.FitLabel(new Rect(Right-300,32,300,30),$"LEVEL {PlayerExperience.Level}  /  {PlayerWallet.Balance} SHARDS",theme.Body,true);
            FrontEndTheme.Fill(new Rect(Left,85,ContentWidth,1),new Color(.75f,.65f,.47f,.50f));
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
            theme.Label(new Rect(profile.x+68,profile.y+32,196,23),$"LEVEL {PlayerExperience.Level}  /  {PlayerWallet.Balance} SHARDS",theme.Small,true);
            theme.Progress(new Rect(profile.x+68,profile.y+60,194,3),PlayerExperience.LevelProgress);
            if(GUI.Button(profile,GUIContent.none,GUIStyle.none))SetPage(Page.Profile);

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
            if(theme.Action(new Rect(Left,680,112,28),"RANKING"))SetPage(Page.Leaderboard);
            if(theme.Action(new Rect(Left+124,680,112,28),"SETTINGS"))SetPage(Page.Settings);
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
            theme.Label(new Rect(Left+28,228,w-56,22),"AVAILABLE NOW",theme.Small,true);
            theme.Label(new Rect(Left+28,252,w-56,42),"MATCH 5v5",theme.Title);
            theme.Label(new Rect(Left+28,300,w-56,48),"Five versus five.\nTwo lanes. Dimension planets. One Reality Core.",theme.Body,true);
            if(theme.Action(new Rect(Left+28,354,w-56,32),"CO-OP  •  PRIVATE LOBBY",true))
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
            float matchButtonWidth = (w - matchButtonGap) * .5f;
            bool matchButtonsEnabled = !choosingMatchMode && !OnlinePlanHandshake.Preparing &&
                (EdgegapOnlineFlow.Instance == null || !EdgegapOnlineFlow.Instance.Busy) &&
                (OnlinePartyService.Instance == null || !OnlinePartyService.Instance.Busy) &&
                (CoopLobbyService.Instance == null || !CoopLobbyService.Instance.Busy);
            if (theme.Action(new Rect(Left,620,matchButtonWidth,58),"Play Online",true,
                enabled:matchButtonsEnabled)) _ = BeginOnlineMatchmakingAsync();
            if (theme.Action(new Rect(Left+matchButtonWidth+matchButtonGap,620,matchButtonWidth,58),"Play Offline",true,
                enabled:matchButtonsEnabled)) _ = BeginOfflineMatchAsync();
            theme.Label(new Rect(Left,684,w,22),launchMessage.Length>0?launchMessage:
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
            Heading("Private Co-op","Create a private lobby for 2–5 players and fight together.");

            CoopLobbyService coop=CoopLobbyService.Instance;
            if(coop==null)
            {
                theme.Panel(new Rect(Left,226,ContentWidth,360));
                theme.Center(new Rect(Left+32,360,ContentWidth-64,42),"INITIALIZING CO-OP SERVICES...");
                return;
            }

            if(!coop.InLobby)
            {
                float gap=24f;
                float panelW=(ContentWidth-gap)*.5f;

                theme.Panel(new Rect(Left,226,panelW,360));
                theme.Label(new Rect(Left+28,250,panelW-56,32),"CREATE LOBBY",theme.Heading);
                theme.Label(new Rect(Left+28,292,panelW-56,74),"Host a private room for up to four friends.\nNo matchmaking. No public player list.",theme.Body,true);
                theme.Label(new Rect(Left+28,386,panelW-56,42),"2–5 PLAYERS  •  EMPTY ALLY SLOTS FILL AUTOMATICALLY",theme.Small,true);
                if(theme.Action(new Rect(Left+28,454,panelW-56,58),coop.Busy?"CONNECTING...":"CREATE PRIVATE LOBBY",true,enabled:!coop.Busy))
                    _=coop.CreateLobbyAsync();

                float right=Left+panelW+gap;
                theme.Panel(new Rect(right,226,panelW,360));
                theme.Label(new Rect(right+28,250,panelW-56,32),"JOIN LOBBY",theme.Heading);
                theme.Label(new Rect(right+28,292,panelW-56,52),"Enter the join code your friend sent you.",theme.Body,true);
                GUI.SetNextControlName("DF_CoopJoinCode");
                joinCodeInput=CoopLobbyService.NormalizeJoinCode(
                    GUI.TextField(new Rect(right+28,366,panelW-56,54),joinCodeInput,12,theme.Field));
                theme.Center(new Rect(right+28,426,panelW-56,24),string.IsNullOrEmpty(joinCodeInput)?"ENTER CODE":joinCodeInput);
                if(theme.Action(new Rect(right+28,454,panelW-56,58),coop.Busy?"CONNECTING...":"JOIN PRIVATE LOBBY",true,
                    enabled:!coop.Busy&&joinCodeInput.Length>=4))
                    _=coop.JoinLobbyAsync(joinCodeInput);

                string footer=string.IsNullOrEmpty(coop.LastError)?coop.Status:coop.Status+"  •  "+coop.LastError;
                theme.Label(new Rect(Left,610,ContentWidth,52),footer,theme.Small,true);
                theme.Label(new Rect(Left,670,ContentWidth,24),"PRIVATE CO-OP • 2–5 PLAYERS • HOST + RELAY",theme.Small,true);
                return;
            }

            ArenaVariant lobbyArena=coop.LobbyArena;
            ArenaShopItem arenaItem=ArenaShopCatalog.For(lobbyArena);

            theme.Panel(new Rect(Left,222,ContentWidth,94));
            theme.Label(new Rect(Left+24,238,220,22),coop.IsHost?"HOST LOBBY":"FRIEND LOBBY",theme.Small,true);
            theme.Label(new Rect(Left+24,262,310,36),$"CODE  {coop.JoinCode}",theme.Heading);
            if(theme.Action(new Rect(Left+350,244,126,44),"COPY CODE",enabled:!string.IsNullOrEmpty(coop.JoinCode)))
            {
                GUIUtility.systemCopyBuffer=coop.JoinCode;
                joinCodeInput=coop.JoinCode;
            }
            theme.Label(new Rect(Left+500,246,150,22),$"PLAYERS  {coop.PlayerCount} / {coop.MaxPlayers}",theme.Small,true);

            CoopVoiceChatService voice=CoopVoiceChatService.Instance;
            string micLabel=voice==null?"VOICE...":
                voice.PermissionDenied||!voice.HasMicrophonePermission?"ENABLE MIC":
                voice.IsMuted?"MIC OFF":"MIC ON";
            bool micSelected=voice!=null&&voice.IsConnected&&!voice.IsMuted;
            if(theme.Action(new Rect(Right-322,244,142,44),micLabel,selected:micSelected,
                enabled:voice!=null&&!voice.Busy))
            {
                if(voice.PermissionDenied||!voice.HasMicrophonePermission||!voice.IsConnected)
                    voice.RequestMicrophoneAndJoin();
                else voice.ToggleMute();
            }

            if(theme.Action(new Rect(Right-170,244,170,44),coop.Busy?"PLEASE WAIT":"LEAVE LOBBY",enabled:!coop.Busy))
                _=coop.LeaveLobbyAsync();

            const int visibleSlots=CoopLobbyService.MaxHumanPlayers;
            const float playerGap=8f;
            float playerW=(ContentWidth-playerGap*(visibleSlots-1))/visibleSlots;
            IReadOnlyList<CoopLobbyService.MemberView> members=coop.Members;
            for(int slot=0;slot<visibleSlots;slot++)
            {
                float x=Left+slot*(playerW+playerGap);
                theme.Panel(new Rect(x,332,playerW,178));

                if(slot>=members.Count)
                {
                    theme.Label(new Rect(x+10,344,playerW-20,20),slot==0?"HOST":$"SLOT {slot+1}",theme.Small,true);
                    theme.Center(new Rect(x+10,386,playerW-20,36),"OPEN");
                    theme.Label(new Rect(x+10,454,playerW-20,20),"WAITING...",theme.Small,true);
                    continue;
                }

                CoopLobbyService.MemberView member=members[slot];
                string role=member.IsHost?"HOST":$"PLAYER {slot+1}";
                if(member.IsLocal)role+=" • YOU";
                theme.Label(new Rect(x+10,344,playerW-20,20),role,theme.Small,true);
                theme.FitLabel(new Rect(x+10,368,playerW-20,26),member.DisplayName,theme.Heading);
                theme.FitLabel(new Rect(x+10,400,playerW-20,22),HeroIdentity.NameFor(member.Hero).ToUpperInvariant(),theme.Small,true);
                theme.Center(new Rect(x+10,430,playerW-20,20),member.Ready?"READY":"NOT READY");

                if(member.IsLocal)
                {
                    bool lobbyEditable=!coop.Busy&&coop.IsLobby;
                    if(theme.Action(new Rect(x+10,456,34,34),"<",enabled:lobbyEditable))
                        _=coop.CycleLocalHeroAsync(-1);
                    if(theme.Action(new Rect(x+playerW-44,456,34,34),">",enabled:lobbyEditable))
                        _=coop.CycleLocalHeroAsync(1);
                    float readyX=x+50f,readyW=Mathf.Max(42f,playerW-100f);
                    string readyText=coop.LocalReady?"UNREADY":"READY";
                    if(theme.Action(new Rect(readyX,456,readyW,34),readyText,true,enabled:lobbyEditable&&(coop.LocalReady||!coop.HasHeroConflict)))
                        _=coop.SetLocalReadyAsync(!coop.LocalReady);
                }
            }

            theme.Panel(new Rect(Left,526,ContentWidth,100));
            theme.Label(new Rect(Left+22,538,110,20),"MATCH",theme.Small,true);

            float mapX=Left+122;
            bool hostEditable=coop.IsHost&&!coop.Busy&&coop.IsLobby;
            bool lobbyShuffle=coop.LobbyArenaShuffle;
            if(coop.IsHost&&theme.Action(new Rect(mapX,538,36,34),"<",enabled:hostEditable))
                _=coop.CycleHostArenaAsync(-1);
            theme.Center(new Rect(mapX+42,538,146,34),lobbyShuffle?"RANDOM":arenaItem.Name);
            if(coop.IsHost&&theme.Action(new Rect(mapX+194,538,36,34),">",enabled:hostEditable))
                _=coop.CycleHostArenaAsync(1);
            if(theme.Action(new Rect(mapX+238,538,112,34),lobbyShuffle?"SHUFFLE ON":"SHUFFLE",
                selected:lobbyShuffle,enabled:hostEditable))
                _=coop.SetHostArenaShuffleAsync(!lobbyShuffle);

            if(coop.IsStaging)
            {
                string staged=coop.TryBuildMatchPlan(out CoopMatchPlan plan)&&plan?.Session!=null
                    ?$"STAGED  •  {plan.HumanCount} PLAYERS  •  TEAM FILLED AUTOMATICALLY"
                    :"STAGED  •  VALIDATING ROSTER";
                theme.Center(new Rect(Right-382,530,382,22),staged);
                string sync=CoopPlanHandshake.PlanHandshakeReady?"START CO-OP MATCH":"SYNCING MATCH PLAN...";
                if(coop.IsHost&&theme.Action(new Rect(Right-330,554,330,38),sync,true,
                    enabled:!coop.Busy&&CoopPlanHandshake.PlanHandshakeReady))
                    _=coop.LaunchPreparedMatchAsync();
                if(coop.IsHost&&theme.Action(new Rect(Right-330,596,330,26),"CANCEL MATCH PREP",enabled:!coop.Busy))
                    _=coop.CancelMatchStagingAsync();
            }
            else
            {
                string startLabel=coop.IsPostMatch?(coop.IsHost?"RETURNING TO LOBBY":"WAITING FOR HOST"):
                    coop.IsAborted?"MATCH ABORTED":
                    coop.CanStageMatch?"PREPARE CO-OP MATCH":
                    coop.CanStartLobby?"WAITING FOR RELAY LINK":
                    coop.PlayerCount<CoopLobbyService.MinHumanPlayers?"WAITING FOR FRIEND":"WAITING FOR ALL READY";
                if(theme.Action(new Rect(Right-330,538,330,54),startLabel,true,
                    enabled:coop.IsHost&&coop.CanStageMatch&&!coop.Busy))
                    _=coop.BeginMatchStagingAsync();
            }

            string state=!string.IsNullOrEmpty(launchMessage)?launchMessage:
                coop.HasHeroConflict?"CHOOSE DIFFERENT HEROES":
                !string.IsNullOrEmpty(coop.LastError)?coop.LastError:coop.Status;
            string voiceState=CoopVoiceChatService.Instance!=null
                ?CoopVoiceChatService.Instance.Status
                :"VOICE INITIALIZING";
            theme.Label(new Rect(Left,646,ContentWidth,28),state+"  •  "+coop.NetworkLinkState,theme.Small,true);
            theme.Label(new Rect(Left,678,ContentWidth,20),
                (coop.IsStaging
                    ?$"PLAN SYNC  /  {CoopPlanHandshake.HandshakeStatus}"
                    :"PRIVATE SESSION + RELAY ACTIVE")+"  •  "+voiceState,theme.Small,true);
        }

        private void DrawHeroes()
        {
            Heading("Heroes",$"{HeroIdentity.HeroCount} heroes. Choose how you fight.");
            const float rosterWidth=244f;
            for(int i=0;i<HeroIdentity.HeroCount;i++)
            {
                Rect row=new Rect(Left,216+i*55,rosterWidth,50);
                HeroId rowHero=(HeroId)i;
                bool chosen=i==selectedHero;
                bool owned=HeroUnlockInventory.Owned(rowHero);
                if(theme.Action(row,"",selected:chosen))ChangeHero(i);
                FrontEndTheme.Fill(new Rect(row.x+5,row.y+10,3,row.height-20),Alterverse.Visuals.StylizedArtDirection.HeroAccent(rowHero));
                theme.Label(new Rect(row.x+18,row.y+5,row.width-32,24),HeroIdentity.NameFor(rowHero),theme.Body);
                string subtitle=owned?Roles[i]:$"LOCKED  •  {HeroUnlockCatalog.Price(rowHero):N0} SHARDS";
                theme.Label(new Rect(row.x+18,row.y+31,row.width-32,18),subtitle,theme.Small,true);
            }

            HeroId heroId=(HeroId)selectedHero;
            bool heroOwned=HeroUnlockInventory.Owned(heroId);
            Color accent=Alterverse.Visuals.StylizedArtDirection.HeroAccent(heroId);
            float portraitX=Left+rosterWidth+16;
            float portraitWidth=Mathf.Clamp(ContentWidth*.275f,292f,356f);
            float attacksX=portraitX+portraitWidth+18;
            float attacksWidth=Right-attacksX;

            theme.Label(new Rect(portraitX,212,portraitWidth,44),HeroName,theme.Heading);
            theme.FitLabel(new Rect(portraitX,250,portraitWidth,22),Roles[selectedHero]+"  •  "+Strengths[selectedHero],theme.Small,true);
            DrawPortrait(new Rect(portraitX,282,portraitWidth,272));
            theme.Label(new Rect(portraitX,566,portraitWidth,46),RoleDescriptions[selectedHero],theme.Small,true);
            string weaponStatus=WeaponSkinInventory.Equipped(heroId)?WeaponSkinCatalog.For(heroId).Name:
                heroId==HeroId.Veyr?"SHADOW STEEL":heroId==HeroId.Traveler?"CHRONOLUX FOCUS":heroId==HeroId.Nerok?"ABYSSAL ANCHOR":"ORIGINAL WEAPON";
            theme.FitLabel(new Rect(portraitX,616,portraitWidth,24),
                heroOwned?"EQUIPMENT  /  "+weaponStatus:$"PREMIUM HERO  /  {HeroUnlockCatalog.Price(heroId):N0} SHARDS",theme.Small,true);

            string heroAction=heroOwned?"DEPLOY":"VIEW IN STORE";
            if(theme.Action(new Rect(portraitX,646,portraitWidth,38),heroAction,true))
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
            if(theme.Action(new Rect(Left+(tabW+tabGap)*2,204,tabW,52),"SHARDS",selected:shardShop))
            {skinShop=itemShop=weaponShop=mapShop=battlePassShop=heroShop=false;shardShop=true;shopMessage="";}
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
            if (theme.Action(new Rect(Left+292,542,galleryWidth-320,60),action,true,enabled:!cosmeticBusy&&!equipped&&(owned||affordable)))
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
            int price=HeroUnlockCatalog.Price(hero);
            showcase.Present(hero,true,false,false,null);

            float w=ContentWidth*.54f,x=Left+w+32,rw=ContentWidth-w-32;
            theme.Panel(new Rect(Left,270,w,394));
            FrontEndTheme.Fill(new Rect(Left,270,5,394),Alterverse.Visuals.StylizedArtDirection.HeroAccent(hero));
            float tabWidth=(w-52f)*.5f;
            if(theme.Action(new Rect(Left+22,282,tabWidth,32),"THE TRAVELER",selected:hero==HeroId.Traveler))
                selectedHero=(int)HeroId.Traveler;
            if(theme.Action(new Rect(Left+30+tabWidth,282,tabWidth,32),"NEROK ABYSS",selected:hero==HeroId.Nerok))
                selectedHero=(int)HeroId.Nerok;
            DrawPortrait(new Rect(Left+22,320,w-44,280));
            theme.Center(new Rect(Left+22,614,w-44,34),hero==HeroId.Nerok?
                "PREMIUM HERO  •  TIDAL BRUISER":"PREMIUM HERO  •  TEMPORAL SKIRMISHER");

            theme.Label(new Rect(x,276,rw,58),HeroIdentity.NameFor(hero).ToUpperInvariant(),theme.Title);
            theme.Label(new Rect(x,342,rw,28),hero==HeroId.Nerok?
                "ANCHOR / PULL / AREA CONTROL":"TIME / LIGHT / MOBILITY  •  NO COMBAT POWER ADVANTAGE",theme.Small,true);
            theme.Label(new Rect(x,384,rw,92),
                hero==HeroId.Nerok?
                "A shark bruiser with a heavy anchor. Hook enemies, protect yourself and draw opponents into an abyssal vortex.":
                "A shadow moving between seconds. Bend chronolight, open paired rifts and control space with temporal fields. Premium presentation; balanced by the same combat budget as every hero.",
                theme.Body,true);
            theme.Label(new Rect(x,494,rw,34),owned?"OWNED":$"{price:N0} SHARDS",theme.Heading);
            FrontEndTheme.Fill(new Rect(x,536,rw,3),Alterverse.Visuals.StylizedArtDirection.HeroAccent(hero));

            string action=owned?"SELECT "+HeroIdentity.NameFor(hero).ToUpperInvariant():
                PlayerWallet.CanAfford(price)?$"UNLOCK {price:N0} SHARDS":$"NEED {price:N0} SHARDS";
            bool enabled=owned||PlayerWallet.CanAfford(price);
            if(theme.Action(new Rect(x,558,rw,62),action,true,enabled:enabled))
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
                $"SHARDS {PlayerWallet.Balance:N0}  •  PRICE {price:N0}",theme.Small,true);
        }

        private void DrawShardShop()
        {
            theme.Label(new Rect(Left,276,ContentWidth,36),"Shard Packs",theme.Heading);
            theme.Label(new Rect(Left,316,ContentWidth,32),$"BALANCE: {PlayerWallet.Balance} SHARDS  •  Earned and purchased shards are identical.",theme.Small,true);
            var store=GooglePlayShardStore.Instance;
            int packCount=ShardPacks.All.Length;
            float w=packCount>0?(ContentWidth-16f*(packCount-1))/packCount:0f;
            for(int i=0;i<ShardPacks.All.Length;i++)
            {
                var pack=ShardPacks.All[i];float x=Left+i*(w+16);theme.Panel(new Rect(x,368,w,220));
                theme.Label(new Rect(x+18,384,w-36,28),pack.Name,theme.Small,true);
                theme.Label(new Rect(x+18,424,w-36,48),pack.Amount+" SHARDS",theme.Heading);
                ShardShopVisualOverlay.DrawPackCard(x,w,pack,i==packCount-1?3:i);
                if(theme.Action(new Rect(x+18,508,w-36,60),store!=null?store.Price(pack.Id):"Unavailable",true,enabled:store!=null&&store.CanBuy(pack.Id)))store.Buy(pack.Id);
                ShardShopVisualOverlay.DrawPurchaseIcon(x,w,i==packCount-1?3:i);
            }
            theme.Label(new Rect(Left,608,ContentWidth-190,64),store!=null?store.Status:"Store unavailable.",theme.Body,true);
            if(theme.Action(new Rect(Right-170,614,170,54),"RECONNECT",enabled:store!=null&&!store.Busy))store.Reconnect();
        }
        private void DrawSkinShop()
        {
            collection.Draw(theme,new Rect(Left,270,ContentWidth,410),(HeroId)selectedHero,false,showcase,changeCollectionHero);
        }

        private void DrawWeaponSkinShop()
        {
            HeroId hero=(HeroId)selectedHero;
            WeaponSkinItem item=WeaponSkinCatalog.For(hero,weaponSkinVariant);
            bool heroAvailable=HeroUnlockInventory.Owned(hero);
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
            string action=!heroAvailable?"UNLOCK HERO FIRST":equipped?$"USE {originalName}":owned?"EQUIP":
                PlayerWallet.CanAfford(item.Price)?"BUY & EQUIP":$"NEED {item.Price} SHARDS";
            bool enabled=heroAvailable&&(equipped||owned||PlayerWallet.CanAfford(item.Price));
            if(theme.Action(new Rect(x,588,rw,54),action,true,enabled:enabled))
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
                float x=Left+slot*(cardW+gap);

                theme.Panel(new Rect(x,356,cardW,274));
                FrontEndTheme.Fill(new Rect(x,356,cardW,5),item.Accent);
                FrontEndTheme.Fill(new Rect(x+18,378,cardW-36,72),new Color(item.Accent.r*.18f,item.Accent.g*.18f,item.Accent.b*.18f,.72f));
                FrontEndTheme.Fill(new Rect(x+36,416,cardW-72,3),new Color(item.Accent.r,item.Accent.g,item.Accent.b,.72f));

                theme.Label(new Rect(x+18,466,cardW-36,32),item.Name,theme.Heading);
                theme.Label(new Rect(x+18,500,cardW-36,22),item.Subtitle,theme.Small,true);
                string status=owned
                    ?(item.Price>0?"OWNED":"FREE")
                    :item.Price+" SHARDS";
                theme.Label(new Rect(x+18,530,cardW-36,28),status,theme.Body);

                string action;
                bool enabled=true;
                if(owned){action=item.Price>0?"OWNED":"FREE";enabled=false;}
                else if(PlayerWallet.CanAfford(item.Price))action=$"UNLOCK {item.Price}";
                else {action=$"NEED {item.Price} SHARDS";enabled=false;}

                if(theme.Action(new Rect(x+18,572,cardW-36,44),action,enabled:enabled))
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
            AvatarBadgeRenderer.Draw(new Rect(px+14,284,64,64),BattlePassProgress.BetaCrownAvatarId,Texture2D.whiteTexture,Time.unscaledTime);
            theme.FitLabel(new Rect(px+92,283,previewW-108,20),"THE FINAL REWARD",theme.Small,true);
            theme.FitLabel(new Rect(px+92,307,previewW-108,28),"BETA CROWN",theme.Heading);
            theme.FitLabel(new Rect(px+92,339,previewW-108,18),"LEVEL 50  •  PREMIUM AVATAR",theme.Small,true);

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
                if(theme.Action(new Rect(px,632,previewW,44),canBuy?$"UNLOCK PREMIUM • {BattlePassProgress.PremiumPrice:N0} SHARDS":$"PREMIUM • NEED {BattlePassProgress.PremiumPrice-PlayerWallet.Balance:N0} SHARDS",true,enabled:canBuy))
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
                Rect icon=new Rect(rect.center.x-iconSize*.5f,rect.y+42,iconSize,iconSize);
                DrawBattlePassRewardIcon(icon,reward);
                string title=reward.AvatarId>=0?"BETA CROWN":reward.Shards>0?$"{reward.Shards} SHARDS + CORE":$"{reward.ItemAmount}× {BattleItemInventory.Name(reward.Item)}";
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
            DrawBattlePassRewardIcon(new Rect(rect.x+18,rect.y+45,82,82),reward);
            theme.FitLabel(new Rect(rect.x+114,rect.y+46,rect.width-130,28),reward.AvatarId>=0?"BETA CROWN":reward.Shards>0?$"{reward.Shards} SHARDS":reward.IsEmpty?"KEEP GOING":$"{reward.ItemAmount}× CORE",theme.Heading);
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
            if(reward.AvatarId==BattlePassProgress.BetaCrownAvatarId)return "Exclusive Beta Crown avatar.";
            if(reward.Item!=BattleItemType.None)return BattleItemInventory.ShortDescription(reward.Item);
            if(reward.Shards>0)return "Shard currency reward.";
            return "No reward on this track at this level.";
        }

        private void DrawBattleItemShop()
        {
            theme.Label(new Rect(Left,276,ContentWidth,36),"Battle Supply",theme.Heading);
            theme.Label(new Rect(Left,316,ContentWidth,28),"Only the Aegis Core can be purchased. Other battle items are victory drops.",theme.Small,true);
            float cardW=Mathf.Min(680,ContentWidth*.62f);
            theme.Panel(new Rect(Left,354,cardW,270));
            BattleItemIconRenderer.Draw(new Rect(Left+24,374,132,132),BattleItemType.Shield);
            theme.Label(new Rect(Left+180,370,cardW-204,38),BattleItemInventory.Name(BattleItemType.Shield),theme.Heading);
            theme.Label(new Rect(Left+180,410,cardW-204,62),BattleItemInventory.ShortDescription(BattleItemType.Shield)+"\nParachute supply drop after 2 minutes. Only your team can collect it.",theme.Body,true);
            theme.Label(new Rect(Left+180,478,cardW-204,24),$"OWNED  {BattleItemInventory.Count(BattleItemType.Shield)}",theme.Small,true);
            theme.Label(new Rect(Left+180,508,cardW-204,28),$"{BattleItemInventory.ShieldShardPrice} SHARDS",theme.Heading);
            bool canBuy=PlayerWallet.CanAfford(BattleItemInventory.ShieldShardPrice);
            if(theme.Action(new Rect(Left+180,548,cardW-204,54),canBuy?"BUY 1 AEGIS CORE":"NEED 250 SHARDS",true,enabled:canBuy))
                shopMessage=BattleItemInventory.TryBuyShield()?"Aegis Core added to inventory.":PaidShardCommerce.Spending?"Saving purchase…":"Purchase failed.";
            if(PaidShardCommerce.Spending)shopPurchasePending=true;
            theme.Label(new Rect(Left,634,cardW,32),shopMessage.Length>0?shopMessage:"Any shards in your balance can buy the Aegis Core.",theme.Small,true);

            float x=Left+cardW+34,w=ContentWidth-cardW-34;
            theme.Label(new Rect(x,364,w,38),"Victory drops",theme.Heading);
            theme.Label(new Rect(x,410,w,110),"Every victory has a 30% chance to award one random battle item. Regen, Aegis, Velocity and Meteor Cores are equally likely.",theme.Body,true);
            theme.Label(new Rect(x,530,w,68),"One item per match. Consumed when the 2-minute supply drop starts. The crate stays until your team collects it.",theme.Body,true);
            if(theme.Action(new Rect(x,610,w,56),"OPEN INVENTORY"))SetPage(Page.Inventory);
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
            theme.Label(new Rect(Left+120,244,identityWidth-148,34),PlayerProfile.PlayerName,theme.Heading);
            theme.Label(new Rect(Left+120,286,identityWidth-148,26),$"{PlayerProfile.DisplayTitle}  /  LEVEL {PlayerExperience.Level}",theme.Small,true);
            theme.Progress(new Rect(Left+24,348,identityWidth-48,6),PlayerExperience.LevelProgress);
            theme.Label(new Rect(Left+24,364,identityWidth-48,28),XpLabel(),theme.Small,true);
            theme.Label(new Rect(Left+24,412,identityWidth-48,24),"DISPLAY NAME",theme.Small,true);
            if (editingName && nameKeyboard==null)
                profileName=GUI.TextField(new Rect(Left+24,446,identityWidth-224,54),profileName,PlayerProfile.MaxNameLength,theme.Field);
            else theme.Label(new Rect(Left+24,446,identityWidth-224,54),editingName?profileName:PlayerProfile.PlayerName,theme.Body);
            if (!editingName)
            {
                if (theme.Action(new Rect(Left+identityWidth-176,446,152,54),"EDIT NAME")) BeginNameEdit();
            }
            else
            {
                if (theme.Action(new Rect(Left+identityWidth-176,446,152,54),"SAVE",true)) SaveName();
                if (theme.Action(new Rect(Left+identityWidth-176,510,152,48),"CANCEL")) CancelName();
            }
            theme.Label(new Rect(Left+24,510,identityWidth-224,58),profileMessage.Length>0?profileMessage:"3–16 characters: letters, numbers, spaces, _ or -.",theme.Small,true);
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
            Stat(x,458,w,"ASSISTS",ProgressionSummary.Assists.ToString());
            Stat(x,514,w,"OBJECTIVES",ProgressionSummary.Objectives.ToString());
            Stat(x,570,w,"SHARDS",PlayerWallet.Balance.ToString());
            AccountLoginPanel.Draw(theme,new Rect(x,626,w,48));
        }
        private void DrawQuests()
        {
            Heading("Daily missions","Daily progress resets at 00:00 Europe/Berlin. Claim completed missions here.");
            float width=Mathf.Min(780,ContentWidth);
            float x=Left+(ContentWidth-width)*.5f;
            for(int i=0;i<PlayerChallengeContent.Daily.Length;i++)
            {
                ChallengeDef quest=PlayerChallengeContent.Daily[i];
                float y=220+i*142;
                theme.Panel(new Rect(x,y,width,126));
                theme.Label(new Rect(x+22,y+13,width-235,30),quest.Name,theme.Heading);
                theme.Label(new Rect(x+22,y+48,width-235,30),quest.Description,theme.Body,true);
                int progress=DailyQuestProgress.Progress(i),target=DailyQuestProgress.Target(i);
                theme.Progress(new Rect(x+22,y+91,width-260,8),progress/(float)target);
                theme.Label(new Rect(x+width-213,y+18,190,24),$"{progress} / {target}  •  {quest.Shards} SHARDS",theme.Small,true);
                bool claimed=DailyQuestProgress.Claimed(i);
                if(theme.Action(new Rect(x+width-205,y+58,180,50),claimed?"CLAIMED":"CLAIM",true,enabled:!claimed&&progress>=target))
                    DailyQuestProgress.Claim(i);
            }
        }

        private void Stat(float x,float y,float w,string label,string value)
        {
            theme.Label(new Rect(x,y,w*.57f,40),label,theme.Small,true);
            theme.Label(new Rect(x+w*.6f,y,w*.4f,40),value,theme.Heading);
            FrontEndTheme.Fill(new Rect(x,y+47,w,1),FrontEndTheme.Raised);
        }
        private void BeginNameEdit()
        {
            editingName=true; profileMessage=""; profileName=PlayerProfile.PlayerName;
            if (TouchScreenKeyboard.isSupported)
                nameKeyboard=TouchScreenKeyboard.Open(profileName,TouchScreenKeyboardType.Default,false,false,false,false,"Display name",PlayerProfile.MaxNameLength);
        }
        private void SaveName()
        {
            if (nameKeyboard!=null) profileName=nameKeyboard.text;
            if (!PlayerProfile.IsValidName(profileName)) { profileMessage="Use 3–16 valid characters."; return; }
            PlayerProfile.PlayerName=profileName; profileName=PlayerProfile.PlayerName;
            editingName=false; profileMessage="Name saved."; CloseKeyboard();
        }
        private void CancelName() { CloseKeyboard(); editingName=false; profileName=PlayerProfile.PlayerName; }
        private void CloseKeyboard() { if (nameKeyboard!=null) { nameKeyboard.active=false; nameKeyboard=null; } }
        private void DrawLeaderboard()
        {
            Heading("Most Wins", "Worldwide lifetime win rankings powered by Google Play Games.");

            float leftWidth = ContentWidth * .58f;
            theme.Panel(new Rect(Left, 220, leftWidth, 430));
            theme.Label(new Rect(Left + 28, 244, leftWidth - 56, 24), "MOST WINS", theme.Small, true);
            int wins = Alterverse.Profile.MatchRewardService.MatchesWon;
            theme.Label(new Rect(Left + 28, 278, leftWidth - 56, 54), $"WINS {wins:N0}", theme.Title);
            theme.Label(new Rect(Left + 28, 344, leftWidth - 56, 40),
                "Lifetime match victories place you on the global leaderboard.", theme.Body, true);

            theme.Label(new Rect(Left + 28, 410, leftWidth - 56, 22), "GOOGLE PLAY GAMES ACCOUNT", theme.Small, true);
            theme.Label(new Rect(Left + 28, 440, leftWidth - 56, 32), GooglePlayLevelLeaderboard.PlayerName, theme.Heading);
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
            // Voice-chat controls occupy the right-column slot at y=306.
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
            if (launching || choosingMatchMode || !PrepareSelectedMatch()) return;
            var online=OnlinePartyService.Instance;
            if(online==null){launchMessage="Online service is initializing. Please try again.";return;}
            var flow=EdgegapOnlineFlow.Instance;
            if(flow==null){launchMessage="Online matchmaking is initializing. Please try again.";return;}
            if(online.Busy || flow.Busy || OnlinePlanHandshake.Preparing)return;
            if(CoopLobbyService.Instance!=null && CoopLobbyService.Instance.InLobby)
            {launchMessage="Leave the private co-op lobby before online matchmaking.";return;}
            choosingMatchMode=true;
            try
            {
                OnlinePartyOverlay.Show();
                if(flow.IsSearching){launchMessage=flow.Status;return;}
                if(online.InParty)
                {
                    if(!online.IsHost){launchMessage="Waiting for the party leader to start matchmaking.";return;}
                    if(online.HasHeroConflict || !online.AllPresentPlayersReady)
                    {launchMessage="Choose different heroes and ready up in the online party.";return;}
                    await flow.BeginSearchAsync();
                    launchMessage=flow.Status;
                    return;
                }
                launchMessage="SEARCHING FOR ONLINE MATCH...";
                await flow.QuickSearchAsync();
                launchMessage=flow.Status;
            }
            catch(System.Exception ex)
            {
                launchMessage="Online matchmaking could not connect.";
                Debug.LogError("DIMENSIONS FRONT: online matchmaking failed: "+ex.Message);
            }
            finally { choosingMatchMode=false; }
        }

        private async System.Threading.Tasks.Task BeginOfflineMatchAsync()
        {
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
            theme.Label(new Rect(Left,230,ContentWidth,60),"Entering the Front",theme.Title);
            theme.Label(new Rect(Left,316,ContentWidth,40),HeroName+"  /  "+Roles[selectedHero],theme.Heading);
            string mode=coopLaunching&&CoopAuthority.ActivePlan!=null
                ?$"PRIVATE CO-OP  •  {CoopAuthority.ActivePlan.HumanCount} PLAYERS  /  {ArenaShopCatalog.For(CoopAuthority.ActivePlan.Arena).Name}  /  ZERO TEAM"
                :ArenaVariantRuntime.ShuffleEnabled
                    ?"MATCH 5v5  /  RANDOM OWNED ARENA  /  ZERO TEAM"
                    : "MATCH 5v5  /  "+ArenaShopCatalog.For(ArenaVariantRuntime.Selected).Name+"  /  ZERO TEAM";
            theme.Label(new Rect(Left,372,ContentWidth,32),mode,theme.Body,true);
            theme.Progress(new Rect(Left,452,ContentWidth,5),Mathf.Clamp01((Time.unscaledTime-launchAt)/.55f));
            theme.Label(new Rect(Left,480,ContentWidth,30),coopLaunching?"Synchronizing co-op battlefield":"Preparing your local match",theme.Small,true);
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
                launching=false;coopLaunching=false;page=Page.Coop;launchMessage="Co-op session was lost before launch.";return;
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
            Alterverse.Prototype.PrototypeBootstrap.PrepareTransitionFrame();
            launching=false;visible=false;MatchClock.Set(PauseReason.Result,false);MatchClock.Set(PauseReason.Menu,false);
            if(restarting)bootstrap.StartCoroutine(bootstrap.RestartPrototype());
            else bootstrap.BuildPrototype();
            gameObject.SetActive(false);
        }
    }
}
