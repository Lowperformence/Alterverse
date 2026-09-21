using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
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
        private enum Page { Home, Play, Heroes, Shop, Inventory, Profile, Leaderboard, Settings, Coop }
        private static MainMenuController instance;
        public static bool IsVisible => instance != null && instance.visible;
        private static readonly string[] NavigationLabels = { "HOME", "PLAY", "HEROES", "STORE", "INVENTORY", "PROFILE", "RANKING", "SETTINGS" };
        private static readonly string[] Roles = { "ASSASSIN", "TANK", "MARKSMAN", "MAGE", "SUPPORT", "BRUISER" };
        private static readonly string[] RoleDescriptions = {
            "Close the distance. Isolate a vulnerable target, strike, then disengage.",
            "Hold the front. Shield your team and interrupt enemies at close range.",
            "Keep your distance. Deliver sustained fire while allies protect your position.",
            "Control the fight. Place powerful area attacks where enemies want to move.",
            "Keep allies fighting. Heal, shield and slow enemies from the backline.",
            "Win the long fight. Trade at close range with heavy strikes and sustain." };
        private static readonly string[] Strengths = { "BURST / MOBILITY", "PROTECTION / CONTROL", "RANGE / SUSTAINED DAMAGE", "AREA DAMAGE / CONTROL", "HEALING / PROTECTION", "MELEE / ENDURANCE" };
        private Page page;
        private bool visible = true, launching, editingName, suspended;
        private int selectedHero, shopAvatarIndex, battlePassPageStart=1, battlePassPreviewLevel=1;
        private int playArenaBrowseIndex=-1,mapShopPageStart;
        private bool battlePassPreviewPremium;
        private bool skinShop=true,shardShop,itemShop,weaponShop,mapShop,battlePassShop,cosmeticBusy;
        private float pageChangedAt, launchAt, menuWidth = 1280;
        private string profileName, profileMessage = "", shopMessage = "", launchMessage = "", joinCodeInput = "";
        private FrontEndTheme theme;
        private MenuCosmicBackdrop backdrop;
        private MenuHeroShowcase showcase;
        private CosmeticCollectionPanel collection;
        private System.Action<int> changeCollectionHero;
        private TouchScreenKeyboard nameKeyboard;
        private float master, music, sfx;
        private bool audioDirty;
        private const float MenuHeight = 720;
        private float Left => (menuWidth - ContentWidth)*.5f;
        private float ContentWidth => Mathf.Min(1360,menuWidth-64);
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
            selectedHero = Mathf.Clamp((int)PlayerHeroSelection.Selected,0,5);
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
            instance.visible = true; instance.launching = false; instance.page = Page.Home;
            instance.editingName = false; instance.profileName = PlayerProfile.PlayerName;
            instance.profileMessage = ""; instance.shopMessage = ""; instance.pageChangedAt = Time.unscaledTime;
            instance.collection?.Invalidate();
            instance.showcase?.InvalidateLoadout();
            instance.gameObject.SetActive(true);
            if(GooglePlayLevelLeaderboard.IsConfigured)GooglePlayLevelLeaderboard.AuthenticateAndSync();
            MatchClock.Set(PauseReason.Menu,true);
        }
        private void OnEnable() { if (visible) MatchClock.Set(PauseReason.Menu,true); }
        private void OnApplicationPause(bool paused)
        {
            suspended = paused;
            if (paused) { FlushAudio(); if (launching) { launching = false; page = Page.Play; } }
            if (visible) MatchClock.Set(PauseReason.Menu,true);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && launching) { launching = false; page = Page.Play; }
            if (focused && visible) { MatchClock.Set(PauseReason.Menu,true);collection?.Invalidate();showcase?.InvalidateLoadout(); }
        }
        private void OnDisable() { FlushAudio(); CloseKeyboard(); }
        private void OnDestroy() { collection?.Dispose();backdrop?.Dispose(); if (instance == this) instance = null; }
        private void Update()
        {
            if (!visible || suspended) return;
            bool previewSkin=page==Page.Shop&&skinShop&&!shardShop&&!itemShop&&!weaponShop&&!mapShop&&!battlePassShop;
            bool previewInventory=page==Page.Inventory&&collection.InventoryPreview;
            bool previewWeapon=page==Page.Shop&&weaponShop;
            showcase.Present((HeroId)selectedHero,
                !launching&&(page==Page.Home||page==Page.Play||page==Page.Heroes||previewSkin||previewWeapon||previewInventory),
                previewSkin?null:previewWeapon?(bool?)false:null,
                previewWeapon?(bool?)true:null,
                previewSkin?(UniversalHeroCosmetic?)collection.Preview(false):previewInventory?(UniversalHeroCosmetic?)collection.Preview(true):null);
            if (nameKeyboard != null)
            {
                profileName = nameKeyboard.text;
                if (nameKeyboard.status == TouchScreenKeyboard.Status.Done) { SaveName(); CloseKeyboard(); }
                else if (nameKeyboard.status == TouchScreenKeyboard.Status.Canceled || nameKeyboard.status == TouchScreenKeyboard.Status.LostFocus)
                { CancelName(); }
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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
            if (!visible) return;
            Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color; bool oldEnabled = GUI.enabled;
            try
            {
                GUI.color = Color.white; GUI.enabled = !launching;
                backdrop ??= new MenuCosmicBackdrop();
                backdrop.Draw(new Rect(0,0,Screen.width,Screen.height));
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) return;
                float scale = Mathf.Min(safe.height/MenuHeight,safe.width/1200f);
                menuWidth = safe.width/scale;
                float y = Screen.height-safe.yMax+(safe.height-MenuHeight*scale)*.5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x,y,0),Quaternion.identity,Vector3.one*scale);
                theme ??= new FrontEndTheme();
                if (launching) { DrawLaunch(); return; }
                DrawNavigation();
                float reveal = Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-pageChangedAt)/.16f));
                GUI.color = new Color(1,1,1,.65f+.35f*reveal);
                switch (page)
                {
                    case Page.Play: DrawPlay(); break;
                    case Page.Heroes: DrawHeroes(); break;
                    case Page.Shop: DrawShop(); break;
                    case Page.Inventory: DrawInventory(); break;
                    case Page.Profile: DrawProfile(); break;
                    case Page.Leaderboard: DrawLeaderboard(); break;
                    case Page.Settings: DrawSettings(); break;
                    case Page.Coop: DrawCoop(); break;
                    default: DrawHome(); break;
                }
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; }
        }
        private void DrawNavigation()
        {
            theme.FitLabel(new Rect(Left,20,Mathf.Max(120,ContentWidth-970),48),"DIMENSIONS FRONT",theme.Small);

            // INVENTORY needs more horizontal room than the short navigation labels.
            // Use authored widths instead of equal buttons so every label stays on one line.
            float[] widths={82f,82f,94f,82f,126f,92f,108f,126f};
            const float gap=8f;
            float total=gap*(widths.Length-1);
            for(int i=0;i<widths.Length;i++)total+=widths[i];
            float x=Right-total;

            for(int i=0;i<NavigationLabels.Length;i++)
            {
                if(theme.Action(new Rect(x,20,widths[i],48),NavigationLabels[i],selected:(int)page==i))
                    SetPage((Page)i);
                x+=widths[i]+gap;
            }

            DimensionUiMotif.DrawHeaderLine(new Rect(Left,86,ContentWidth,2),Texture2D.whiteTexture,FrontEndTheme.Zero,FrontEndTheme.Accent,0);
        }
        private void Heading(string title, string description)
        {
            theme.Label(new Rect(Left,110,ContentWidth,52),title,theme.Title);
            theme.Label(new Rect(Left,166,ContentWidth,30),description,theme.Body,true);
        }
        private string HeroName => HeroIdentity.NameFor((HeroId)selectedHero);

        private string ArenaBrowseStatus(ArenaVariant arena,bool owned,bool selected)
        {
            if(selected)return "SELECTED FOR NEXT MATCH";
            if(owned)return "OWNED  •  TAP ARENA TO SELECT";
            if(arena==ArenaVariant.Noxara)
                return $"PLAY TO UNLOCK  •  {ArenaUnlockInventory.NoxaraProgress}/{ArenaUnlockInventory.NoxaraMatchesRequired} MATCHES";
            if(arena==ArenaVariant.Kryon)
                return $"PLAY TO UNLOCK  •  {ArenaUnlockInventory.KryonProgress}/{ArenaUnlockInventory.KryonMatchesRequired} MATCHES";
            ArenaShopItem item=ArenaShopCatalog.For(arena);
            return item.Price>0?$"LOCKED  •  {item.Price} SHARDS IN STORE":"LOCKED  •  "+ArenaUnlockInventory.UnlockRequirement(arena);
        }

        private void DrawPortrait(Rect rect)
        {
            Color accent=Alterverse.Visuals.StylizedArtDirection.HeroAccent((HeroId)selectedHero);
            FrontEndTheme.Fill(rect,new Color(.025f,.034f,.05f,.62f));
            DimensionUiMotif.DrawCornerCuts(rect,Texture2D.whiteTexture,accent);
            if (showcase != null && showcase.Image != null)
                GUI.DrawTexture(rect,showcase.Image,ScaleMode.ScaleToFit,true);
            FrontEndTheme.Fill(new Rect(rect.x+rect.width*.24f,rect.yMax-10,rect.width*.52f,2),new Color(accent.r,accent.g,accent.b,.50f));
        }
        private void DrawHome()
        {
            float split = Left+ContentWidth*.43f;
            theme.Label(new Rect(Left,128,460,30),"TWO REALITIES. ONE FRONT.",theme.Small,true);
            theme.Label(new Rect(Left,170,500,112),"Your reality.\nYour fight.",theme.Title);
            theme.Label(new Rect(Left,300,440,66),"Choose your hero. Join your team.\nTake the Dimensions Front.",theme.Body,true);
            AvatarBadgeRenderer.Draw(new Rect(Left,406,64,64),PlayerProfile.Avatar,Texture2D.whiteTexture,Time.unscaledTime);
            theme.Label(new Rect(Left+82,404,340,30),PlayerProfile.PlayerName,theme.Heading);
            theme.Label(new Rect(Left+82,440,340,25),$"LEVEL {PlayerExperience.Level}    /    {PlayerWallet.Balance} SHARDS",theme.Small,true);
            theme.Progress(new Rect(Left,492,420,5),PlayerExperience.LevelProgress);
            theme.Label(new Rect(Left,506,420,28),XpLabel(),theme.Small,true);
            if (theme.Action(new Rect(Left,568,420,72),"PLAY",true)) SetPage(Page.Play);
            theme.Label(new Rect(Left,652,420,26),"AI MATCH 5v5  /  LOCAL PLAY",theme.Small,true);
            DrawPortrait(new Rect(split,108,Right-split,444));
            FrontEndTheme.Fill(new Rect(split+48,553,Right-split-96,2),Alterverse.Visuals.StylizedArtDirection.HeroAccent((HeroId)selectedHero));
            theme.Label(new Rect(split+48,570,Right-split-170,38),HeroName,theme.Heading);
            theme.Label(new Rect(split+48,616,Right-split-180,28),Roles[selectedHero],theme.Small,true);
            if (theme.Action(new Rect(Right-110,580,62,64),">")) SetPage(Page.Heroes);
        }
        private void DrawPlay()
        {
            Heading("Deployment","Choose your hero, AI difficulty and arena before entering the Front.");
            float w = ContentWidth*.52f;
            theme.Panel(new Rect(Left,214,w,394));
            theme.Label(new Rect(Left+28,228,w-56,22),"AVAILABLE NOW",theme.Small,true);
            theme.Label(new Rect(Left+28,252,w-56,42),"AI MATCH 5v5",theme.Title);
            theme.Label(new Rect(Left+28,300,w-56,48),"You + 4 allied bots against 5 enemy bots.\nTwo lanes. Dimension planets. One Reality Core.",theme.Body,true);
            if(theme.Action(new Rect(Left+28,354,w-56,32),"CO-OP VS AI  •  PRIVATE LOBBY",true))
                SetPage(Page.Coop);
            BattleItemType equippedItem=BattleItemInventory.Equipped;
            theme.Label(new Rect(Left+28,396,w-56,18),equippedItem==BattleItemType.None?"BATTLE ITEM  /  NONE":$"BATTLE ITEM  /  {BattleItemInventory.Name(equippedItem)}  ×{BattleItemInventory.Count(equippedItem)}",theme.Small,true);
            HeroId deploymentHero=(HeroId)selectedHero;
            theme.Label(new Rect(Left+28,418,w-56,18),WeaponSkinInventory.Equipped(deploymentHero)?$"WEAPON  /  {WeaponSkinCatalog.For(deploymentHero).Name}":"WEAPON  /  ORIGINAL",theme.Small,true);

            theme.Label(new Rect(Left+28,446,w-56,20),"AI DIFFICULTY",theme.Small,true);
            var difficulty=BotDifficultySettings.Selected;
            float gap=8f,buttonW=(w-56-gap*3)/4f;
            for(int i=0;i<4;i++)
            {
                var level=(BotDifficulty)i;
                if(theme.Action(new Rect(Left+28+i*(buttonW+gap),470,buttonW,42),
                    BotDifficultySettings.Label(level),selected:difficulty==level))
                {
                    BotDifficultySettings.Selected=level;
                    difficulty=level;
                }
            }

            theme.Label(new Rect(Left+28,520,w-56,20),"ARENA",theme.Small,true);
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
                    launchMessage="";
                }

                browsed=ArenaShopCatalog.Items[playArenaBrowseIndex];
                owned=ArenaUnlockInventory.Owned(browsed.Variant);
                selectedNow=selectedArena==browsed.Variant;
                theme.Center(new Rect(selectorX,590,selectorW,18),ArenaBrowseStatus(browsed.Variant,owned,selectedNow));
            }

            if (theme.Action(new Rect(Left,620,w,58),"START AI MATCH",true)) BeginLaunch();
            theme.Label(new Rect(Left,684,w,22),launchMessage.Length>0?launchMessage:
                $"SELECTED ARENA  /  {ArenaShopCatalog.For(selectedArena).Name}",theme.Small,true);

            float x = Left+w+32, rw = ContentWidth-w-32;
            DrawPortrait(new Rect(x,198,rw,330));
            theme.Label(new Rect(x+24,538,rw-48,40),HeroName,theme.Heading);
            theme.Label(new Rect(x+24,578,rw-48,24),Roles[selectedHero],theme.Small,true);
            if (theme.Action(new Rect(x+24,620,rw-48,54),"CHANGE HERO")) SetPage(Page.Heroes);
        }
        private void DrawCoop()
        {
            Heading("Private Co-op","Create a private lobby for 2–5 players and fight together against the AI.");

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
                theme.Label(new Rect(Left+28,386,panelW-56,42),"2–5 PLAYERS  •  EMPTY ALLY SLOTS BECOME BOTS",theme.Small,true);
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
            BotDifficulty lobbyDifficulty=coop.LobbyDifficulty;
            ArenaShopItem arenaItem=ArenaShopCatalog.For(lobbyArena);

            theme.Panel(new Rect(Left,222,ContentWidth,94));
            theme.Label(new Rect(Left+24,238,220,22),coop.IsHost?"HOST LOBBY":"FRIEND LOBBY",theme.Small,true);
            theme.Label(new Rect(Left+24,262,310,36),$"CODE  {coop.JoinCode}",theme.Heading);
            if(theme.Action(new Rect(Left+350,244,126,44),"COPY CODE",enabled:!string.IsNullOrEmpty(coop.JoinCode)))
            {
                GUIUtility.systemCopyBuffer=coop.JoinCode;
                joinCodeInput=coop.JoinCode;
            }
            theme.Label(new Rect(Left+500,246,ContentWidth-690,22),$"PLAYERS  {coop.PlayerCount} / {coop.MaxPlayers}",theme.Small,true);
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
                    bool lobbyEditable=!coop.Busy&&!coop.IsStaging;
                    if(theme.Action(new Rect(x+10,456,34,34),"<",enabled:lobbyEditable))
                        _=coop.CycleLocalHeroAsync(-1);
                    if(theme.Action(new Rect(x+playerW-44,456,34,34),">",enabled:lobbyEditable))
                        _=coop.CycleLocalHeroAsync(1);
                    float readyX=x+50f,readyW=Mathf.Max(42f,playerW-100f);
                    string readyText=coop.LocalReady?"UNREADY":"READY";
                    if(theme.Action(new Rect(readyX,456,readyW,34),readyText,true,enabled:lobbyEditable&&!coop.HasHeroConflict))
                        _=coop.SetLocalReadyAsync(!coop.LocalReady);
                }
            }

            theme.Panel(new Rect(Left,526,ContentWidth,100));
            theme.Label(new Rect(Left+22,538,110,20),"MATCH",theme.Small,true);

            float mapX=Left+132;
            bool hostEditable=coop.IsHost&&!coop.Busy&&!coop.IsStaging;
            if(coop.IsHost&&theme.Action(new Rect(mapX,538,42,34),"<",enabled:hostEditable))
                _=coop.CycleHostArenaAsync(-1);
            theme.Center(new Rect(mapX+48,538,220,34),arenaItem.Name);
            if(coop.IsHost&&theme.Action(new Rect(mapX+274,538,42,34),">",enabled:hostEditable))
                _=coop.CycleHostArenaAsync(1);

            float aiX=Left+480;
            if(coop.IsHost&&theme.Action(new Rect(aiX,538,42,34),"<",enabled:hostEditable))
                _=coop.CycleHostDifficultyAsync(-1);
            theme.Center(new Rect(aiX+48,538,184,34),"AI  •  "+BotDifficultySettings.Label(lobbyDifficulty).ToUpperInvariant());
            if(coop.IsHost&&theme.Action(new Rect(aiX+238,538,42,34),">",enabled:hostEditable))
                _=coop.CycleHostDifficultyAsync(1);

            if(coop.IsStaging)
            {
                string staged=coop.TryBuildMatchPlan(out CoopMatchPlan plan)&&plan?.Session!=null
                    ?$"STAGED  •  {plan.HumanCount} PLAYERS + {5-plan.HumanCount} ALLY BOTS VS 5 BOTS"
                    :"STAGED  •  VALIDATING ROSTER";
                theme.Center(new Rect(Right-382,538,382,28),staged);
                if(coop.IsHost&&theme.Action(new Rect(Right-330,576,330,32),"CANCEL MATCH PREP",enabled:!coop.Busy))
                    _=coop.CancelMatchStagingAsync();
            }
            else
            {
                string startLabel=coop.CanStageMatch?"PREPARE CO-OP MATCH":
                    coop.CanStartLobby?"WAITING FOR RELAY LINK":
                    coop.PlayerCount<CoopLobbyService.MinHumanPlayers?"WAITING FOR FRIEND":"WAITING FOR ALL READY";
                if(theme.Action(new Rect(Right-330,538,330,54),startLabel,true,
                    enabled:coop.IsHost&&coop.CanStageMatch&&!coop.Busy))
                    _=coop.BeginMatchStagingAsync();
            }

            string state=coop.HasHeroConflict?"CHOOSE DIFFERENT HEROES":
                !string.IsNullOrEmpty(coop.LastError)?coop.LastError:coop.Status;
            theme.Label(new Rect(Left,646,ContentWidth,28),state+"  •  "+coop.NetworkLinkState,theme.Small,true);
            theme.Label(new Rect(Left,678,ContentWidth,20),
                coop.IsStaging
                    ?$"PLAN SYNC  /  {CoopPlanHandshake.HandshakeStatus}"
                    :"PRIVATE SESSION + RELAY ACTIVE • HOST CAN START WITH 2–5 READY PLAYERS.",theme.Small,true);
        }

        private void DrawHeroes()
        {
            Heading("Heroes","Six roles. Choose how you fight.");
            const float rosterWidth=244f;
            for(int i=0;i<6;i++)
            {
                Rect row=new Rect(Left,216+i*74,rosterWidth,64);
                bool chosen=i==selectedHero;
                if(theme.Action(row,"",selected:chosen))ChangeHero(i);
                FrontEndTheme.Fill(new Rect(row.x+5,row.y+12,3,row.height-24),Alterverse.Visuals.StylizedArtDirection.HeroAccent((HeroId)i));
                theme.Label(new Rect(row.x+18,row.y+8,row.width-32,26),HeroIdentity.NameFor((HeroId)i),theme.Body);
                theme.Label(new Rect(row.x+18,row.y+35,row.width-32,22),Roles[i],theme.Small,true);
            }

            HeroId heroId=(HeroId)selectedHero;
            Color accent=Alterverse.Visuals.StylizedArtDirection.HeroAccent(heroId);
            float portraitX=Left+rosterWidth+16;
            float portraitWidth=Mathf.Clamp(ContentWidth*.275f,292f,356f);
            float attacksX=portraitX+portraitWidth+18;
            float attacksWidth=Right-attacksX;

            theme.Label(new Rect(portraitX,212,portraitWidth,44),HeroName,theme.Heading);
            theme.Label(new Rect(portraitX,250,portraitWidth,22),Roles[selectedHero]+"  •  "+Strengths[selectedHero],theme.Small,true);
            DrawPortrait(new Rect(portraitX,282,portraitWidth,272));
            theme.Label(new Rect(portraitX,566,portraitWidth,46),RoleDescriptions[selectedHero],theme.Small,true);
            string weaponStatus=WeaponSkinInventory.Equipped(heroId)?WeaponSkinCatalog.For(heroId).Name:"ORIGINAL WEAPON";
            theme.Label(new Rect(portraitX,616,portraitWidth,24),"EQUIPMENT  /  "+weaponStatus,theme.Small,true);
            if(theme.Action(new Rect(portraitX,646,portraitWidth,38),"DEPLOY",true))SetPage(Page.Play);

            theme.Label(new Rect(attacksX,212,attacksWidth,28),"COMBAT LOADOUT",theme.Heading);
            theme.Label(new Rect(attacksX,240,attacksWidth,20),"Mobile combat loadout • damage / range / cooldown / effects",theme.Small,true);
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
            theme.Label(new Rect(rect.x+102,rect.y+7,rect.width-116,26),attack.Name,theme.Body);
            theme.Label(new Rect(rect.x+14,rect.y+36,rect.width-28,18),attack.Stats,theme.Small,true);
            theme.Label(new Rect(rect.x+14,rect.y+56,rect.width-28,22),attack.Effect,theme.Small,true);
        }
        private void ChangeHero(int index)
        {
            if (index==selectedHero) return;
            selectedHero=Mathf.Clamp(index,0,5);
            PlayerHeroSelection.Select((HeroId)selectedHero);
            bool previewSkin=page==Page.Shop&&skinShop&&!shardShop&&!itemShop&&!weaponShop&&!mapShop&&!battlePassShop;
            bool previewInventory=page==Page.Inventory&&collection.InventoryPreview;
            bool previewWeapon=page==Page.Shop&&weaponShop;
            showcase.Present((HeroId)selectedHero,true,
                previewSkin?null:previewWeapon?(bool?)false:null,
                previewWeapon?(bool?)true:null,
                previewSkin?(UniversalHeroCosmetic?)collection.Preview(false):previewInventory?(UniversalHeroCosmetic?)collection.Preview(true):null);
        }
        private void DrawShop()
        {
            Heading("Store","Cosmetics, arenas, shards, battle supply and seasonal progression.");
            float tabW=112f,tabGap=8f;
            if(theme.Action(new Rect(Left,204,tabW,52),"COSMETICS",selected:skinShop&&!shardShop&&!itemShop&&!weaponShop&&!mapShop&&!battlePassShop)){skinShop=true;shardShop=false;itemShop=false;weaponShop=false;mapShop=false;battlePassShop=false;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap),204,tabW,52),"AVATARS",selected:!skinShop&&!shardShop&&!itemShop&&!weaponShop&&!mapShop&&!battlePassShop)){skinShop=false;shardShop=false;itemShop=false;weaponShop=false;mapShop=false;battlePassShop=false;shopMessage="";}
            theme.Label(new Rect(Right-264,116,264,42),collection.BalanceText((HeroId)selectedHero),theme.Small);
            if(theme.Action(new Rect(Left+(tabW+tabGap)*2,204,tabW,52),"SHARDS",selected:shardShop)){skinShop=false;shardShop=true;itemShop=false;weaponShop=false;mapShop=false;battlePassShop=false;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap)*3,204,tabW,52),"WEAPONS",selected:weaponShop)){skinShop=false;shardShop=false;itemShop=false;weaponShop=true;mapShop=false;battlePassShop=false;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap)*4,204,tabW,52),"MAPS",selected:mapShop)){skinShop=false;shardShop=false;itemShop=false;weaponShop=false;mapShop=true;battlePassShop=false;shopMessage="";}
            float wideX=Left+(tabW+tabGap)*5;
            if(theme.Action(new Rect(wideX,204,150,52),"BATTLE ITEM",selected:itemShop)){skinShop=false;shardShop=false;itemShop=true;weaponShop=false;mapShop=false;battlePassShop=false;shopMessage="";}
            if(theme.Action(new Rect(wideX+158,204,150,52),"BATTLE PASS",selected:battlePassShop)){skinShop=false;shardShop=false;itemShop=false;weaponShop=false;mapShop=false;battlePassShop=true;shopMessage="";}
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
                else { AvatarPurchaseResult result=AvatarShopService.TryPurchase(item); shopMessage=result==AvatarPurchaseResult.Purchased?"Unlocked and equipped.":result==AvatarPurchaseResult.InsufficientShards?"Not enough shards.":"Purchase could not be completed."; }
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
        private void DrawShardShop()
        {
            theme.Label(new Rect(Left,276,ContentWidth,36),"Shard Packs",theme.Heading);
            theme.Label(new Rect(Left,316,ContentWidth,32),$"BALANCE: {PlayerWallet.Balance} SHARDS  •  Earned and purchased shards are identical.",theme.Small,true);
            var store=GooglePlayShardStore.Instance;float w=(ContentWidth-48)/4;
            for(int i=0;i<ShardPacks.All.Length;i++)
            {
                var pack=ShardPacks.All[i];float x=Left+i*(w+16);theme.Panel(new Rect(x,368,w,220));
                theme.Label(new Rect(x+18,384,w-36,28),pack.Name,theme.Small,true);
                theme.Label(new Rect(x+18,424,w-36,48),pack.Amount+" SHARDS",theme.Heading);
                if(theme.Action(new Rect(x+18,508,w-36,60),store!=null?store.Price(pack.Id):"Unavailable",true,enabled:store!=null&&store.CanBuy(pack.Id)))store.Buy(pack.Id);
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
            WeaponSkinItem item=WeaponSkinCatalog.For(hero);
            bool owned=WeaponSkinInventory.Owned(hero);
            bool equipped=WeaponSkinInventory.Equipped(hero);
            float w=ContentWidth*.54f,x=Left+w+32,rw=ContentWidth-w-32;

            DrawPortrait(new Rect(Left,270,w,340));
            FrontEndTheme.Fill(new Rect(Left+24,286,5,290),item.Glow);
            theme.Label(new Rect(Left+42,282,w-84,28),"WEAPON OVERDRIVE PREVIEW",theme.Small,true);
            if(theme.Action(new Rect(Left,614,64,54),"<")){ChangeHero((selectedHero+5)%6);shopMessage="";}
            theme.Center(new Rect(Left+72,614,w-144,54),$"{HeroName}  /  {item.WeaponName}");
            if(theme.Action(new Rect(Left+w-64,614,64,54),">")){ChangeHero((selectedHero+1)%6);shopMessage="";}

            theme.Label(new Rect(x,282,rw,62),item.Name,theme.Title);
            theme.Label(new Rect(x,350,rw,34),item.WeaponName.ToUpperInvariant()+"  /  COSMETIC WEAPON SKIN",theme.Small,true);
            theme.Label(new Rect(x,400,rw,90),"Visual only — no damage, range or hitbox advantage.",theme.Body,true);
            theme.Label(new Rect(x,506,rw,34),owned?"OWNED":WeaponSkinCatalog.Price+" SHARDS",theme.Heading);
            FrontEndTheme.Fill(new Rect(x,548,rw,3),item.Glow);

            string action=equipped?"USE ORIGINAL":owned?"EQUIP":PlayerWallet.CanAfford(WeaponSkinCatalog.Price)?"BUY & EQUIP":"NEED 500 SHARDS";
            bool enabled=owned||PlayerWallet.CanAfford(WeaponSkinCatalog.Price);
            if(theme.Action(new Rect(x,570,rw,60),action,true,enabled:enabled))
            {
                if(owned)
                {
                    WeaponSkinInventory.Equip(hero,!equipped);
                    shopMessage=equipped?"Original weapon equipped.":item.Name+" equipped.";
                }
                else
                {
                    AvatarPurchaseResult result=WeaponSkinInventory.Purchase(hero);
                    shopMessage=result==AvatarPurchaseResult.Purchased?item.Name+" unlocked and equipped.":
                        result==AvatarPurchaseResult.InsufficientShards?"Not enough shards.":"Purchase could not be completed.";
                }
                showcase.Present(hero,true,false,true);
            }
            theme.Label(new Rect(x,642,rw,42),shopMessage.Length>0?shopMessage:
                $"SHARDS {PlayerWallet.Balance}  •  ONE SKIN FOR {HeroIdentity.NameFor(hero).ToUpperInvariant()}",theme.Small,true);
        }

        private void DrawMapShop()
        {
            theme.Label(new Rect(Left,276,ContentWidth,36),"Arena Collection",theme.Heading);
            theme.Label(new Rect(Left,316,ContentWidth,28),"Browse every arena here. The carousel expands automatically as new maps are added.",theme.Small,true);

            ArenaVariant selected=ArenaVariantRuntime.Selected;
            int count=ArenaShopCatalog.Items.Length;
            const int visible=3;
            int maxStart=Mathf.Max(0,count-visible);
            mapShopPageStart=Mathf.Clamp(mapShopPageStart,0,maxStart);

            float gap=14f,cardW=(ContentWidth-gap*(visible-1))/visible;
            for(int slot=0;slot<visible;slot++)
            {
                int index=mapShopPageStart+slot;
                if(index>=count)break;

                ArenaShopItem item=ArenaShopCatalog.Items[index];
                ArenaVariant arena=item.Variant;
                bool owned=ArenaUnlockInventory.Owned(arena);
                bool selectedNow=selected==arena;
                bool progressionArena=!owned&&item.Price<=0;
                float x=Left+slot*(cardW+gap);

                theme.Panel(new Rect(x,356,cardW,274));
                FrontEndTheme.Fill(new Rect(x,356,cardW,5),item.Accent);
                FrontEndTheme.Fill(new Rect(x+18,378,cardW-36,72),new Color(item.Accent.r*.18f,item.Accent.g*.18f,item.Accent.b*.18f,.72f));
                FrontEndTheme.Fill(new Rect(x+36,416,cardW-72,3),new Color(item.Accent.r,item.Accent.g,item.Accent.b,.72f));

                theme.Label(new Rect(x+18,466,cardW-36,32),item.Name,theme.Heading);
                theme.Label(new Rect(x+18,500,cardW-36,22),item.Subtitle,theme.Small,true);
                string status=owned?"OWNED":arena==ArenaVariant.Noxara
                    ?$"{ArenaUnlockInventory.NoxaraProgress}/{ArenaUnlockInventory.NoxaraMatchesRequired} MATCHES"
                    :arena==ArenaVariant.Kryon
                        ?$"{ArenaUnlockInventory.KryonProgress}/{ArenaUnlockInventory.KryonMatchesRequired} MATCHES"
                        :item.Price>0?item.Price+" SHARDS":"PLAY TO UNLOCK";
                theme.Label(new Rect(x+18,530,cardW-36,28),status,theme.Body);

                string action;
                bool enabled=true;
                if(selectedNow)action="SELECTED";
                else if(owned)action="SELECT";
                else if(progressionArena){action="PLAY TO UNLOCK";enabled=false;}
                else if(PlayerWallet.CanAfford(item.Price))action=$"UNLOCK {item.Price}";
                else {action=$"NEED {item.Price} SHARDS";enabled=false;}

                if(theme.Action(new Rect(x+18,572,cardW-36,44),action,selected:selectedNow,enabled:enabled&&!selectedNow))
                {
                    if(owned)
                    {
                        ArenaVariantRuntime.Select(arena);
                        selected=arena;
                        playArenaBrowseIndex=ArenaShopCatalog.IndexOf(arena);
                        shopMessage=item.Name+" selected for your next match.";
                    }
                    else if(!progressionArena)
                    {
                        ArenaPurchaseResult result=ArenaUnlockInventory.Purchase(arena);
                        if(result==ArenaPurchaseResult.Purchased)
                        {
                            ArenaVariantRuntime.Select(arena);
                            selected=arena;
                            playArenaBrowseIndex=ArenaShopCatalog.IndexOf(arena);
                            shopMessage=item.Name+" UNLOCKED and selected.";
                        }
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
                $"SELECTED ARENA  /  {ArenaShopCatalog.For(selected).Name}    •    SHARDS {PlayerWallet.Balance}",theme.Small,true);
        }

        private void DrawBattlePassShop()
        {
            if(BattlePassProgress.HasUnseenLevel&&string.IsNullOrEmpty(shopMessage))
            {
                shopMessage=$"BATTLE PASS LEVEL UP  •  LEVEL {BattlePassProgress.Level}  •  NEW REWARD AVAILABLE";
                BattlePassProgress.AcknowledgeCurrentLevel();
            }

            battlePassPreviewLevel=Mathf.Clamp(battlePassPreviewLevel,1,BattlePassProgress.MaxLevel);
            theme.Panel(new Rect(Left,270,ContentWidth,102));

            string seasonHeader=$"BATTLE PASS / {BattlePassProgress.SeasonName}";
            float headerColumn=(ContentWidth-60f)*.5f;
            theme.FitLabel(new Rect(Left+22,282,headerColumn,24),seasonHeader,theme.Small,true);

            float totalProgress=BattlePassProgress.TotalXpRequired<=0
                ?1f
                :(float)BattlePassProgress.Xp/BattlePassProgress.TotalXpRequired;

            theme.Progress(new Rect(Left+22,314,headerColumn,6),totalProgress);

            string xpText=BattlePassProgress.Level>=BattlePassProgress.MaxLevel
                ?"COMPLETE"
                :$"{BattlePassProgress.XpToNextLevel:N0} BP XP TO NEXT LEVEL";

            theme.FitLabel(new Rect(Left+22,330,headerColumn,24),xpText,theme.Small,true);

            theme.FitLabel(new Rect(Right-22-headerColumn,282,headerColumn,24),
                BattlePassProgress.PremiumUnlocked
                    ?"PREMIUM ACTIVE"
                    :"PREMIUM LOCKED / 1000 SHARDS",
                theme.Small,true);

            theme.FitLabel(new Rect(Right-22-headerColumn,330,headerColumn,24),
                $"{BattlePassProgress.PremiumShardReturn} SHARDS RETURNABLE • 50 LEVELS",
                theme.Small,true);

            const int visible=4;
            battlePassPageStart=Mathf.Clamp(battlePassPageStart,1,BattlePassProgress.MaxLevel);
            float previewW=Mathf.Clamp(ContentWidth*.29f,330f,410f);
            float trackW=ContentWidth-previewW-18f;
            float gap=8f,cardW=(trackW-gap*(visible-1))/visible;
            theme.Label(new Rect(Left,382,trackW,22),"REWARD TRACK",theme.Heading);
            for(int i=0;i<visible;i++)
            {
                int level=battlePassPageStart+i;if(level>BattlePassProgress.MaxLevel)break;
                float x=Left+i*(cardW+gap),y=408;
                theme.Panel(new Rect(x,y,cardW,148));
                theme.Label(new Rect(x+8,y+7,cardW-16,20),$"LEVEL {level}",theme.Small,true);
                DrawBattlePassRewardRow(new Rect(x+8,y+32,cardW-16,50),level,false);
                DrawBattlePassRewardRow(new Rect(x+8,y+88,cardW-16,50),level,true);
            }

            float px=Left+trackW+18f;
            DrawBattlePassRewardPreview(new Rect(px,382,previewW,174),battlePassPreviewLevel,battlePassPreviewPremium);

            bool canBack=battlePassPageStart>1,canForward=battlePassPageStart+visible<=BattlePassProgress.MaxLevel;
            if(theme.Action(new Rect(Left,638,46,36),"<",enabled:canBack))battlePassPageStart=Mathf.Max(1,battlePassPageStart-visible);
            int last=Mathf.Min(BattlePassProgress.MaxLevel,battlePassPageStart+visible-1);
            theme.Center(new Rect(Left+52,638,142,36),$"{battlePassPageStart}–{last}");
            if(theme.Action(new Rect(Left+200,638,46,36),">",enabled:canForward))battlePassPageStart=Mathf.Min(BattlePassProgress.MaxLevel,battlePassPageStart+visible);

            int available=BattlePassProgress.AvailableClaimCount();
            if(theme.Action(new Rect(Left+260,638,190,36),available>0?$"CLAIM ALL ({available})":"NO REWARDS READY",true,enabled:available>0))
            { int claimed=BattlePassProgress.ClaimAllAvailable(); shopMessage=$"{claimed} Battle Pass reward{(claimed==1?"":"s")} claimed."; }

            if(!BattlePassProgress.PremiumUnlocked)
            {
                bool canBuy=PlayerWallet.CanAfford(BattlePassProgress.PremiumPrice);
                if(theme.Action(new Rect(Left+464,638,300,36),canBuy?"UNLOCK PREMIUM • 1000 SHARDS":$"NEED {BattlePassProgress.PremiumPrice-PlayerWallet.Balance} SHARDS",true,enabled:canBuy))
                {
                    BattlePassPurchaseResult result=BattlePassProgress.UnlockPremium();
                    shopMessage=result==BattlePassPurchaseResult.Unlocked?"PREMIUM BATTLE PASS UNLOCKED.":"Not enough shards.";
                }
            }
            theme.Label(new Rect(Left,682,ContentWidth,20),shopMessage.Length>0?shopMessage:
                $"FINAL REWARD  •  LEVEL 50 BETA CROWN    /    PREMIUM SHARD RETURN {BattlePassProgress.PremiumShardTotal()}",theme.Small,true);
        }

        private void DrawBattlePassRewardRow(Rect rect,int level,bool premium)
        {
            BattlePassReward reward=premium?BattlePassProgress.PremiumRewardFor(level):BattlePassProgress.FreeRewardFor(level);
            string status=BattlePassProgress.Status(premium,level);
            bool selected=battlePassPreviewLevel==level&&battlePassPreviewPremium==premium;
            if(theme.Action(rect,"",selected:selected))
            { battlePassPreviewLevel=level; battlePassPreviewPremium=premium; return; }
            theme.FitLabel(new Rect(rect.x+4,rect.y+2,rect.width-8,20),(premium?"PREMIUM":"FREE")+"  /  "+status,theme.Small,true);
            if(reward.IsEmpty) { theme.Label(new Rect(rect.x+4,rect.y+23,rect.width-8,18),"—",theme.Small,true); return; }
            Rect icon=new(rect.x+4,rect.y+22,25,25); DrawBattlePassRewardIcon(icon,reward);
            theme.FitLabel(new Rect(rect.x+34,rect.y+23,rect.width-38,24),reward.Label,theme.Small,true);
        }

        private void DrawBattlePassRewardPreview(Rect rect,int level,bool premium)
        {
            BattlePassReward reward=premium?BattlePassProgress.PremiumRewardFor(level):BattlePassProgress.FreeRewardFor(level);
            string status=BattlePassProgress.Status(premium,level);
            theme.Panel(rect);
            FrontEndTheme.Fill(new Rect(rect.x,rect.y,4,rect.height),premium?FrontEndTheme.Zero:FrontEndTheme.Accent);
            theme.Label(new Rect(rect.x+14,rect.y+10,rect.width-28,20),level==50&&premium?"FINAL REWARD":$"LEVEL {level}",theme.Small,true);
            Rect icon=new(rect.x+14,rect.y+36,72,72); DrawBattlePassRewardIcon(icon,reward);
            theme.FitLabel(new Rect(rect.x+98,rect.y+34,rect.width-110,28),reward.IsEmpty?"NO REWARD":reward.Label,theme.Heading);
            theme.FitLabel(new Rect(rect.x+98,rect.y+64,rect.width-110,22),(premium?"PREMIUM REWARD":"FREE REWARD")+"  /  "+status,theme.Small,true);
            string description=BattlePassRewardDescription(reward);
            theme.Label(new Rect(rect.x+98,rect.y+88,rect.width-110,40),description,theme.Small,true);
            if(level==50&&premium)theme.FitLabel(new Rect(rect.x+98,rect.y+130,rect.width-110,24),"BETA SEASON 01  •  EXCLUSIVE PREMIUM AVATAR",theme.Small,true);

            bool canClaim=premium?BattlePassProgress.CanClaimPremium(level):BattlePassProgress.CanClaimFree(level);
            if(canClaim&&theme.Action(new Rect(rect.x+14,rect.yMax-34,72,24),"CLAIM",true))
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
            theme.Label(new Rect(Left+180,410,cardW-204,62),BattleItemInventory.ShortDescription(BattleItemType.Shield)+"\nTriggers from your first successful ultimate next match.",theme.Body,true);
            theme.Label(new Rect(Left+180,478,cardW-204,24),$"OWNED  {BattleItemInventory.Count(BattleItemType.Shield)}",theme.Small,true);
            theme.Label(new Rect(Left+180,508,cardW-204,28),$"{BattleItemInventory.ShieldShardPrice} SHARDS",theme.Heading);
            bool canBuy=PlayerWallet.CanAfford(BattleItemInventory.ShieldShardPrice);
            if(theme.Action(new Rect(Left+180,548,cardW-204,54),canBuy?"BUY 1 AEGIS CORE":"NEED 250 SHARDS",true,enabled:canBuy))
                shopMessage=BattleItemInventory.TryBuyShield()?"Aegis Core added to inventory.":"Purchase failed.";
            theme.Label(new Rect(Left,634,cardW,32),shopMessage.Length>0?shopMessage:"Any shards in your balance can buy the Aegis Core.",theme.Small,true);

            float x=Left+cardW+34,w=ContentWidth-cardW-34;
            theme.Label(new Rect(x,364,w,38),"Victory drops",theme.Heading);
            theme.Label(new Rect(x,410,w,110),"Every victory has a 30% chance to award one random battle item. Regen, Aegis, Velocity and Meteor Cores are equally likely.",theme.Body,true);
            theme.Label(new Rect(x,530,w,68),"Only one item can be equipped. It is consumed when its first valid Ultimate activation happens.",theme.Body,true);
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
            if (theme.Action(new Rect(Left+identityWidth-112,592,88,54),"MORE")){skinShop=false;shardShop=false;itemShop=false;weaponShop=false;mapShop=false;SetPage(Page.Shop);}
            float x=Left+identityWidth+32,w=ContentWidth-identityWidth-32;
            ProfileStats stats=ProfileStatsSummary.Current;
            theme.Label(new Rect(x,230,w,36),"Lifetime record",theme.Heading);
            Stat(x,290,w,"MATCHES",stats.Matches.ToString());
            Stat(x,346,w,"WINS / WIN RATE",$"{stats.Wins} / {stats.WinRate:0}%");
            Stat(x,402,w,"KILLS",ProgressionSummary.Kills.ToString());
            Stat(x,458,w,"ASSISTS",ProgressionSummary.Assists.ToString());
            Stat(x,514,w,"OBJECTIVES",ProgressionSummary.Objectives.ToString());
            Stat(x,570,w,"SHARDS",PlayerWallet.Balance.ToString());
            theme.Label(new Rect(x,626,w,48),"GLOBAL LEVEL RANKING  /  RANKING TAB\nAccount level grants no combat power.",theme.Small,true);
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
            Heading("Global Ranking","Worldwide account-level ranking powered by Google Play Games.");

            float leftWidth=ContentWidth*.58f;
            theme.Panel(new Rect(Left,220,leftWidth,430));
            theme.Label(new Rect(Left+28,244,leftWidth-56,24),"GLOBAL LEVEL",theme.Small,true);
            theme.Label(new Rect(Left+28,278,leftWidth-56,54),$"LEVEL {PlayerExperience.Level}",theme.Title);
            theme.Progress(new Rect(Left+28,344,leftWidth-56,6),PlayerExperience.LevelProgress);
            theme.Label(new Rect(Left+28,360,leftWidth-56,26),XpLabel(),theme.Small,true);

            theme.Label(new Rect(Left+28,410,leftWidth-56,22),"GOOGLE PLAY GAMES ACCOUNT",theme.Small,true);
            theme.Label(new Rect(Left+28,440,leftWidth-56,32),GooglePlayLevelLeaderboard.PlayerName,theme.Heading);
            theme.Label(new Rect(Left+28,484,leftWidth-56,54),GooglePlayLevelLeaderboard.Status,theme.Small,true);

            bool configured=GooglePlayLevelLeaderboard.IsConfigured;
            string syncLabel=GooglePlayLevelLeaderboard.IsAuthenticated?"SYNC LEVEL":"CONNECT & SYNC";
            if(theme.Action(new Rect(Left+28,558,(leftWidth-68)*.5f,58),syncLabel,true,enabled:configured))
                GooglePlayLevelLeaderboard.AuthenticateAndSync();
            if(theme.Action(new Rect(Left+40+(leftWidth-68)*.5f,558,(leftWidth-68)*.5f,58),"OPEN GLOBAL RANKING",true,enabled:configured))
                GooglePlayLevelLeaderboard.ShowLeaderboard();

            float x=Left+leftWidth+32,w=ContentWidth-leftWidth-32;
            theme.Label(new Rect(x,230,w,36),"How ranking works",theme.Heading);
            theme.Label(new Rect(x,282,w,112),
                "Your current account level is submitted as your Google Play leaderboard score. Higher level means a higher global position.",theme.Body,true);
            Stat(x,420,w,"YOUR LEVEL",PlayerExperience.Level.ToString());
            Stat(x,476,w,"LAST SYNCED",GooglePlayLevelLeaderboard.LastSubmittedLevel>0?GooglePlayLevelLeaderboard.LastSubmittedLevel.ToString():"—");
            Stat(x,532,w,"SCOPE","ALL-TIME / GLOBAL");
            theme.Label(new Rect(x,604,w,56),
                configured?"Google Play keeps the global leaderboard. No separate DIMENSIONS FRONT ranking server is required.":
                "SETUP REQUIRED  /  Add the GLOBAL LEVEL leaderboard ID from Google Play Console.",theme.Small,true);
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
            theme.Label(new Rect(x,306,rw,82),"AI difficulty is selected directly before an AI match so you can change the challenge for every deployment.",theme.Body,true);
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
        private void BeginLaunch()
        {
            if (launching) return;
            ArenaVariantRuntime.EnsureValidSelection();
            ArenaVariant selectedArena=ArenaVariantRuntime.Selected;
            if(!ArenaUnlockInventory.Owned(selectedArena))
            {
                ArenaVariantRuntime.Select(ArenaVariant.Moon);
                launchMessage="Selected arena is locked. Moon restored.";
                return;
            }
            PlayerHeroSelection.Select((HeroId)selectedHero); FlushAudio();
            launchMessage=""; launching=true; launchAt=Time.unscaledTime; GUI.FocusControl(null);
            showcase.Present((HeroId)selectedHero,false);
        }
        private void DrawLaunch()
        {
            theme.Label(new Rect(Left,230,ContentWidth,60),"Entering the Front",theme.Title);
            theme.Label(new Rect(Left,316,ContentWidth,40),HeroName+"  /  "+Roles[selectedHero],theme.Heading);
            theme.Label(new Rect(Left,372,ContentWidth,32),"AI MATCH 5v5  /  "+ArenaShopCatalog.For(ArenaVariantRuntime.Selected).Name+"  /  ZERO TEAM",theme.Body,true);
            theme.Progress(new Rect(Left,452,ContentWidth,5),Mathf.Clamp01((Time.unscaledTime-launchAt)/.55f));
            theme.Label(new Rect(Left,480,ContentWidth,30),"Preparing your local match",theme.Small,true);
        }
        private void EnterMatch()
        {
            ArenaVariantRuntime.EnsureValidSelection();
            if(!ArenaUnlockInventory.Owned(ArenaVariantRuntime.Selected))
            {
                ArenaVariantRuntime.Select(ArenaVariant.Moon);
                launching=false;page=Page.Play;launchMessage="Locked arena blocked. Moon restored.";return;
            }
            var bootstrap=FindAnyObjectByType<Alterverse.Prototype.PrototypeBootstrap>();
            if (bootstrap==null) { launching=false; page=Page.Play; launchMessage="Match could not start. Please try again."; Debug.LogError("DIMENSIONS FRONT: match bootstrap unavailable."); return; }
            launching=false; visible=false; MatchClock.Set(PauseReason.Result,false); MatchClock.Set(PauseReason.Menu,false);
            if (GameObject.Find("DF_MatchWorld")!=null) bootstrap.StartCoroutine(bootstrap.RestartPrototype());
            gameObject.SetActive(false);
        }
    }
}


