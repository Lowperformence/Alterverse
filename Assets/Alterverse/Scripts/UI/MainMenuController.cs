using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Page { Home, Play, Heroes, Shop, Inventory, Profile, Settings }
        private static MainMenuController instance;
        public static bool IsVisible => instance != null && instance.visible;
        private static readonly string[] NavigationLabels = { "HOME", "PLAY", "HEROES", "STORE", "INVENTORY", "PROFILE", "SETTINGS" };
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
        private int selectedHero, shopAvatarIndex;
        private bool skinShop=true,shardShop,itemShop,weaponShop,cosmeticBusy;
        private float pageChangedAt, launchAt, menuWidth = 1280;
        private string profileName, profileMessage = "", shopMessage = "", inventoryMessage = "", launchMessage = "";
        private FrontEndTheme theme;
        private MenuCosmicBackdrop backdrop;
        private MenuHeroShowcase showcase;
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
            profileName = PlayerProfile.PlayerName;
            master = PlayerProfile.MasterVolume; music = PlayerProfile.MusicVolume; sfx = PlayerProfile.SfxVolume;
            showcase = gameObject.AddComponent<MenuHeroShowcase>();
            MatchClock.Set(PauseReason.Menu,true);
        }
        public static void ShowHome()
        {
            if (instance == null) { new GameObject("DF_MainMenu").AddComponent<MainMenuController>(); return; }
            instance.visible = true; instance.launching = false; instance.page = Page.Home;
            instance.editingName = false; instance.profileName = PlayerProfile.PlayerName;
            instance.profileMessage = ""; instance.shopMessage = ""; instance.pageChangedAt = Time.unscaledTime;
            instance.gameObject.SetActive(true); MatchClock.Set(PauseReason.Menu,true);
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
            if (focused && visible) MatchClock.Set(PauseReason.Menu,true);
        }
        private void OnDisable() { FlushAudio(); CloseKeyboard(); }
        private void OnDestroy() { backdrop?.Dispose(); if (instance == this) instance = null; }
        private void Update()
        {
            if (!visible || suspended) return;
            bool previewSkin=page==Page.Shop&&skinShop&&!shardShop&&!itemShop&&!weaponShop;
            bool previewWeapon=page==Page.Shop&&weaponShop;
            showcase.Present((HeroId)selectedHero,
                !launching&&(page==Page.Home||page==Page.Play||page==Page.Heroes||previewSkin||previewWeapon),
                previewSkin?(bool?)true:previewWeapon?(bool?)false:null,
                previewWeapon?(bool?)true:null);
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
            FlushAudio(); CancelName(); page = next; pageChangedAt = Time.unscaledTime;
            profileMessage = ""; shopMessage = ""; inventoryMessage = ""; GUI.FocusControl(null);
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
                    case Page.Settings: DrawSettings(); break;
                    default: DrawHome(); break;
                }
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; }
        }
        private void DrawNavigation()
        {
            theme.Label(new Rect(Left,20,300,48),"DIMENSIONS FRONT",theme.Heading);

            // INVENTORY needs more horizontal room than the short navigation labels.
            // Use authored widths instead of equal buttons so every label stays on one line.
            float[] widths={90f,90f,100f,90f,136f,100f,112f};
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
            Heading("Deployment","Choose your hero. Enter a local team battle.");
            float w = ContentWidth*.52f;
            theme.Panel(new Rect(Left,222,w,306));
            theme.Label(new Rect(Left+28,244,w-56,26),"AVAILABLE NOW",theme.Small,true);
            theme.Label(new Rect(Left+28,286,w-56,50),"AI MATCH 5v5",theme.Title);
            theme.Label(new Rect(Left+28,350,w-56,62),"You + 4 allied bots against 5 enemy bots.\nTwo lanes. Dimension planets. One Reality Core.",theme.Body,true);
            theme.Label(new Rect(Left+28,438,w-56,30),"ZERO TEAM / "+Alterverse.AI.BotDifficultySettings.Label(Alterverse.AI.BotDifficultySettings.Selected)+" BOTS",theme.Body);
            theme.Label(new Rect(Left+28,479,w-56,24),"Online multiplayer is not available yet.",theme.Small,true);
            BattleItemType equippedItem=BattleItemInventory.Equipped;
            theme.Label(new Rect(Left+28,505,w-56,22),equippedItem==BattleItemType.None?"BATTLE ITEM  /  NONE":$"BATTLE ITEM  /  {BattleItemInventory.Name(equippedItem)}  ×{BattleItemInventory.Count(equippedItem)}",theme.Small,true);
            HeroId deploymentHero=(HeroId)selectedHero;
            theme.Label(new Rect(Left+28,527,w-56,22),WeaponSkinInventory.Equipped(deploymentHero)?$"WEAPON  /  {WeaponSkinCatalog.For(deploymentHero).Name}":"WEAPON  /  ORIGINAL",theme.Small,true);
            if (theme.Action(new Rect(Left,554,w,76),"START AI MATCH",true)) BeginLaunch();
            theme.Label(new Rect(Left,642,w,28),launchMessage.Length>0?launchMessage:"Your selected hero is confirmed when the match starts.",theme.Small,true);
            float x = Left+w+32, rw = ContentWidth-w-32;
            DrawPortrait(new Rect(x,198,rw,330));
            theme.Label(new Rect(x+24,538,rw-48,40),HeroName,theme.Heading);
            theme.Label(new Rect(x+24,578,rw-48,24),Roles[selectedHero],theme.Small,true);
            if (theme.Action(new Rect(x+24,620,rw-48,54),"CHANGE HERO")) SetPage(Page.Heroes);
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
            theme.Label(new Rect(attacksX,240,attacksWidth,20),"Base gameplay values • damage / range / cooldown / effects",theme.Small,true);
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
            Rect iconRect=new Rect(rect.x+12,rect.y+9,58,58);
            FrontEndTheme.Fill(iconRect,FrontEndTheme.Raised);
            if(ultimate)
            {
                CombatGlyphs.Ring(iconRect.center,25,2,new Color(accent.r,accent.g,accent.b,.72f),20);
                CombatGlyphs.Ring(iconRect.center,29,1,new Color(accent.r,accent.g,accent.b,.34f),20);
            }
            CombatGlyphs.Draw(MenuGlyph((HeroId)selectedHero,attack.Key),iconRect.center,ultimate?34:31,ultimate?accent:FrontEndTheme.Text);
            Rect keyRect=new Rect(rect.x+78,rect.y+7,52,22);
            FrontEndTheme.Fill(keyRect,ultimate?accent:FrontEndTheme.Raised);
            theme.Label(keyRect,attack.Key,theme.Small);
            theme.Label(new Rect(rect.x+138,rect.y+5,rect.width-150,26),attack.Name,theme.Body);
            theme.Label(new Rect(rect.x+78,rect.y+34,rect.width-90,18),attack.Stats,theme.Small,true);
            theme.Label(new Rect(rect.x+78,rect.y+54,rect.width-90,22),attack.Effect,theme.Small,true);
        }
        private static CombatGlyph MenuGlyph(HeroId h,string key)
        {
            if(key=="BASIC")return h==HeroId.Veyr?CombatGlyph.Blade:h==HeroId.Kaelon?CombatGlyph.Heavy:h==HeroId.Nyra?CombatGlyph.Rifle:h==HeroId.Elyra?CombatGlyph.Orb:h==HeroId.Mira?CombatGlyph.SupportShot:CombatGlyph.Gauntlet;
            int slot=key=="Q"?0:key=="W"?1:key=="E"?2:3;
            if(h==HeroId.Veyr)return slot==2?CombatGlyph.Dash:slot==3?CombatGlyph.Rift:slot==1?CombatGlyph.Rift:CombatGlyph.Blade;
            if(h==HeroId.Kaelon)return slot==0?CombatGlyph.Stun:slot==1?CombatGlyph.Shield:slot==2?CombatGlyph.Area:CombatGlyph.Shield;
            if(h==HeroId.Nyra)return slot==0?CombatGlyph.Rifle:slot==1?CombatGlyph.Dash:slot==2?CombatGlyph.Stun:CombatGlyph.Volley;
            if(h==HeroId.Elyra)return slot==0?CombatGlyph.Area:slot==1?CombatGlyph.Rift:slot==2?CombatGlyph.Dash:CombatGlyph.Burst;
            if(h==HeroId.Mira)return slot==0?CombatGlyph.SupportShot:slot==1?CombatGlyph.Heal:slot==2?CombatGlyph.Shield:CombatGlyph.Heal;
            return slot==0?CombatGlyph.Gauntlet:slot==1?CombatGlyph.Shield:slot==2?CombatGlyph.Slam:CombatGlyph.Burst;
        }
        private void ChangeHero(int index)
        {
            if (index==selectedHero) return;
            selectedHero=Mathf.Clamp(index,0,5);
            PlayerHeroSelection.Select((HeroId)selectedHero);
            bool previewSkin=page==Page.Shop&&skinShop&&!shardShop&&!itemShop&&!weaponShop;
            bool previewWeapon=page==Page.Shop&&weaponShop;
            showcase.Present((HeroId)selectedHero,true,previewSkin?(bool?)true:previewWeapon?(bool?)false:null,previewWeapon?(bool?)true:null);
        }
        private void DrawShop()
        {
            Heading("Store","Cosmetics, shards and a limited battle supply.");
            float tabW=136f,tabGap=10f;
            if(theme.Action(new Rect(Left,204,tabW,52),"SKINS",selected:skinShop&&!shardShop&&!itemShop&&!weaponShop)){skinShop=true;shardShop=false;itemShop=false;weaponShop=false;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap),204,tabW,52),"AVATARS",selected:!skinShop&&!shardShop&&!itemShop&&!weaponShop)){skinShop=false;shardShop=false;itemShop=false;weaponShop=false;shopMessage="";}
            theme.Label(new Rect(Right-264,116,264,42),$"SHARDS  {PlayerWallet.Balance}",theme.Small);
            if(theme.Action(new Rect(Left+(tabW+tabGap)*2,204,tabW,52),"SHARDS",selected:shardShop)){skinShop=false;shardShop=true;itemShop=false;weaponShop=false;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap)*3,204,tabW,52),"WEAPONS",selected:weaponShop)){skinShop=false;shardShop=false;itemShop=false;weaponShop=true;shopMessage="";}
            if(theme.Action(new Rect(Left+(tabW+tabGap)*4,204,170,52),"BATTLE ITEM",selected:itemShop)){skinShop=false;shardShop=false;itemShop=true;weaponShop=false;shopMessage="";}
            if(itemShop){DrawBattleItemShop();return;}
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
            float w=ContentWidth*.53f,x=Left+w+32,rw=ContentWidth-w-32;
            DrawPortrait(new Rect(Left,270,w,340));
            if(theme.Action(new Rect(Left,614,64,54),"<"))ChangeHero((selectedHero+5)%6);
            theme.Center(new Rect(Left+72,614,w-144,54),HeroName);
            if(theme.Action(new Rect(Left+w-64,614,64,54),">"))ChangeHero((selectedHero+1)%6);
            theme.Label(new Rect(x,282,rw,52),UniversalSkin.Name,theme.Title);
            theme.Label(new Rect(x,346,rw,60),"UNIVERSAL ARMOR FINISH / ALL SIX HEROES",theme.Small,true);
            theme.Label(new Rect(x,410,rw,90),"Fractured violet wings. Obsidian armor.\nYour hero's energy and team identity remain distinct.",theme.Body,true);
            theme.Label(new Rect(x,510,rw,36),UniversalSkin.Owned?"OWNED":UniversalSkin.Price+" SHARDS",theme.Heading);
            bool owned=UniversalSkin.Owned,equipped=UniversalSkin.Equipped,affordable=PlayerWallet.CanAfford(UniversalSkin.Price);
            string label=equipped?"USE ORIGINAL":owned?"EQUIP VOIDFALL":affordable?"BUY & EQUIP":"NEED MORE SHARDS";
            if(theme.Action(new Rect(x,562,rw,64),label,true,enabled:!cosmeticBusy&&(owned||affordable)))
            {
                if(owned){UniversalSkin.Equip(!equipped);shopMessage=equipped?"Original finish equipped.":"VOIDFALL equipped for your next match.";}
                else shopMessage=UniversalSkin.Purchase()==AvatarPurchaseResult.Purchased?"Purchased and equipped for all six heroes.":"Purchase could not be completed.";
            }
            theme.Label(new Rect(x,638,rw,48),shopMessage.Length>0?shopMessage:"Preview: VOIDFALL • cosmetic only",theme.Small,true);
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
            theme.Label(new Rect(Left+42,282,w-84,28),"WEAPON OVERDRIVE PREVIEW  •  +50% VISUAL SIZE",theme.Small,true);
            if(theme.Action(new Rect(Left,614,64,54),"<")){ChangeHero((selectedHero+5)%6);shopMessage="";}
            theme.Center(new Rect(Left+72,614,w-144,54),$"{HeroName}  /  {item.WeaponName}");
            if(theme.Action(new Rect(Left+w-64,614,64,54),">")){ChangeHero((selectedHero+1)%6);shopMessage="";}

            theme.Label(new Rect(x,282,rw,62),item.Name,theme.Title);
            theme.Label(new Rect(x,350,rw,34),item.WeaponName.ToUpperInvariant()+"  /  COSMETIC WEAPON SKIN",theme.Small,true);
            theme.Label(new Rect(x,400,rw,90),item.Description+"\nVisual only — no damage, range or hitbox advantage.",theme.Body,true);
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
            Heading("Battle Inventory","Equip one consumable for your next match. It activates on your first successful Ultimate.");
            BattleItemType equipped=BattleItemInventory.Equipped;
            float gap=18f,cardW=(ContentWidth-gap)/2f,cardH=202f;
            for(int i=0;i<BattleItemInventory.All.Length;i++)
            {
                BattleItemType item=BattleItemInventory.All[i];
                int row=i/2,col=i%2;
                float x=Left+col*(cardW+gap),y=218+row*(cardH+18);
                bool selected=equipped==item;
                theme.Panel(new Rect(x,y,cardW,cardH));
                BattleItemIconRenderer.Draw(new Rect(x+18,y+22,116,116),item,selected);
                theme.Label(new Rect(x+154,y+18,cardW-172,32),BattleItemInventory.Name(item),theme.Heading);
                theme.Label(new Rect(x+154,y+54,cardW-172,64),BattleItemInventory.ShortDescription(item),theme.Small,true);
                int count=BattleItemInventory.Count(item);
                theme.Label(new Rect(x+154,y+122,cardW-172,24),$"OWNED  {count}",theme.Small,true);
                string action=selected?"EQUIPPED":count>0?"EQUIP":"NOT OWNED";
                if(theme.Action(new Rect(x+154,y+154,cardW-172,38),action,selected,selected,enabled:count>0&&!selected))
                {
                    if(BattleItemInventory.Equip(item)){inventoryMessage=BattleItemInventory.Name(item)+" equipped for the next match.";equipped=item;}
                }
            }
            string status=equipped==BattleItemType.None?"NO ITEM EQUIPPED":"EQUIPPED  ◆  "+BattleItemInventory.Name(equipped);
            theme.Label(new Rect(Left,650,ContentWidth*.62f,28),inventoryMessage.Length>0?inventoryMessage:status,theme.Small,true);
            if(equipped!=BattleItemType.None&&theme.Action(new Rect(Right-220,642,220,44),"UNEQUIP"))
            {BattleItemInventory.Equip(BattleItemType.None);inventoryMessage="Battle item unequipped.";}
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
            if (theme.Action(new Rect(Left+identityWidth-112,592,88,54),"MORE")){skinShop=false;shardShop=false;itemShop=false;weaponShop=false;SetPage(Page.Shop);}
            float x=Left+identityWidth+32,w=ContentWidth-identityWidth-32;
            ProfileStats stats=ProfileStatsSummary.Current;
            theme.Label(new Rect(x,230,w,36),"Lifetime record",theme.Heading);
            Stat(x,290,w,"MATCHES",stats.Matches.ToString());
            Stat(x,346,w,"WINS / WIN RATE",$"{stats.Wins} / {stats.WinRate:0}%");
            Stat(x,402,w,"KILLS",ProgressionSummary.Kills.ToString());
            Stat(x,458,w,"ASSISTS",ProgressionSummary.Assists.ToString());
            Stat(x,514,w,"OBJECTIVES",ProgressionSummary.Objectives.ToString());
            Stat(x,570,w,"SHARDS",PlayerWallet.Balance.ToString());
            theme.Label(new Rect(x,626,w,48),"LOCAL PROFILE / UNRANKED\nAccount level grants no combat power.",theme.Small,true);
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
        private void DrawSettings()
        {
            Heading("Settings","Choose your challenge. Make the sound your own.");
            float w=ContentWidth*.64f;
            theme.Panel(new Rect(Left,222,w,438));
            float previousMaster=master, previousMusic=music, previousSfx=sfx;
            DrawVolume(new Rect(Left+28,250,w-56,100),"Master volume",ref master);
            DrawVolume(new Rect(Left+28,380,w-56,100),"Music",ref music);
            DrawVolume(new Rect(Left+28,510,w-56,100),"Sound effects",ref sfx);
            if(previousMaster!=master||previousMusic!=music||previousSfx!=sfx)
                PlayerProfile.SetAudioVolumes(master,music,sfx,false);
            float x=Left+w+32,rw=ContentWidth-w-32;
            theme.Label(new Rect(x,238,rw,38),"BOT DIFFICULTY",theme.Heading);
            var selected=Alterverse.AI.BotDifficultySettings.Selected;
            for(int i=0;i<3;i++)
            {
                var level=(Alterverse.AI.BotDifficulty)i;
                if(theme.Action(new Rect(x,288+i*62,rw,54),Alterverse.AI.BotDifficultySettings.Label(level),selected:selected==level))
                    Alterverse.AI.BotDifficultySettings.Selected=level;
            }
            theme.Label(new Rect(x,482,rw,78),"Applies to both bot teams next match. Smarter decisions; same combat stats.",theme.Body,true);
            theme.Label(new Rect(x,572,rw,28),"Language: "+PlayerProfile.Language,theme.Small,true);
            theme.Label(new Rect(x,612,rw,48),"Progress and settings are saved on this device.",theme.Small,true);
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
            PlayerHeroSelection.Select((HeroId)selectedHero); FlushAudio();
            launchMessage=""; launching=true; launchAt=Time.unscaledTime; GUI.FocusControl(null);
            showcase.Present((HeroId)selectedHero,false);
        }
        private void DrawLaunch()
        {
            theme.Label(new Rect(Left,230,ContentWidth,60),"Entering the Front",theme.Title);
            theme.Label(new Rect(Left,316,ContentWidth,40),HeroName+"  /  "+Roles[selectedHero],theme.Heading);
            theme.Label(new Rect(Left,372,ContentWidth,32),"AI MATCH 5v5  /  ZERO TEAM",theme.Body,true);
            theme.Progress(new Rect(Left,452,ContentWidth,5),Mathf.Clamp01((Time.unscaledTime-launchAt)/.55f));
            theme.Label(new Rect(Left,480,ContentWidth,30),"Preparing your local match",theme.Small,true);
        }
        private void EnterMatch()
        {
            var bootstrap=FindAnyObjectByType<Alterverse.Prototype.PrototypeBootstrap>();
            if (bootstrap==null) { launching=false; page=Page.Play; launchMessage="Match could not start. Please try again."; Debug.LogError("DIMENSIONS FRONT: match bootstrap unavailable."); return; }
            launching=false; visible=false; MatchClock.Set(PauseReason.Result,false); MatchClock.Set(PauseReason.Menu,false);
            if (GameObject.Find("DF_MatchWorld")!=null) bootstrap.StartCoroutine(bootstrap.RestartPrototype());
            gameObject.SetActive(false);
        }
    }
}
