using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Page { Home, Play, Heroes, Shop, Profile, Settings }
        private static MainMenuController instance;
        public static bool IsVisible => instance != null && instance.visible;
        private static readonly string[] NavigationLabels = { "HOME", "PLAY", "HEROES", "COSMETICS", "PROFILE", "SETTINGS" };
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
        private float pageChangedAt, launchAt, menuWidth = 1280;
        private string profileName, profileMessage = "", shopMessage = "", launchMessage = "";
        private FrontEndTheme theme;
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
            Time.timeScale = 0;
        }
        public static void ShowHome()
        {
            if (instance == null) { new GameObject("DF_MainMenu").AddComponent<MainMenuController>(); return; }
            instance.visible = true; instance.launching = false; instance.page = Page.Home;
            instance.editingName = false; instance.profileName = PlayerProfile.PlayerName;
            instance.profileMessage = ""; instance.shopMessage = ""; instance.pageChangedAt = Time.unscaledTime;
            instance.gameObject.SetActive(true); Time.timeScale = 0;
        }
        private void OnEnable() { if (visible) Time.timeScale = 0; }
        private void OnApplicationPause(bool paused)
        {
            suspended = paused;
            if (paused) { FlushAudio(); if (launching) { launching = false; page = Page.Play; } }
            if (visible) Time.timeScale = 0;
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && launching) { launching = false; page = Page.Play; }
            if (focused && visible) Time.timeScale = 0;
        }
        private void OnDisable() { FlushAudio(); CloseKeyboard(); }
        private void OnDestroy() { if (instance == this) instance = null; }
        private void Update()
        {
            if (!visible || suspended) return;
            showcase.Present((HeroId)selectedHero, !launching && (page == Page.Home || page == Page.Play || page == Page.Heroes));
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
            profileMessage = ""; shopMessage = ""; GUI.FocusControl(null);
        }
        private void OnGUI()
        {
            if (!visible) return;
            Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color; bool oldEnabled = GUI.enabled;
            try
            {
                GUI.color = Color.white; GUI.enabled = !launching;
                FrontEndTheme.Fill(new Rect(0,0,Screen.width,Screen.height),FrontEndTheme.Background);
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
            float start = Right-744;
            for (int i=0;i<NavigationLabels.Length;i++)
                if (theme.Action(new Rect(start+i*124,20,116,48),NavigationLabels[i],selected:(int)page==i)) SetPage((Page)i);
            FrontEndTheme.Fill(new Rect(Left,86,ContentWidth,1),FrontEndTheme.Raised);
        }
        private void Heading(string title, string description)
        {
            theme.Label(new Rect(Left,110,ContentWidth,52),title,theme.Title);
            theme.Label(new Rect(Left,166,ContentWidth,30),description,theme.Body,true);
        }
        private string HeroName => HeroIdentity.NameFor((HeroId)selectedHero);
        private void DrawPortrait(Rect rect)
        {
            if (showcase != null && showcase.Image != null)
                GUI.DrawTexture(rect,showcase.Image,ScaleMode.ScaleToFit,false);
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
            FrontEndTheme.Fill(new Rect(split+48,553,Right-split-96,2),FrontEndTheme.Zero);
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
            theme.Label(new Rect(Left+28,438,w-56,30),"ZERO TEAM  /  LOCAL MATCH",theme.Body);
            theme.Label(new Rect(Left+28,479,w-56,24),"Online multiplayer is not available yet.",theme.Small,true);
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
            float rosterWidth = 288;
            for (int i=0;i<6;i++)
            {
                Rect row = new Rect(Left,216+i*74,rosterWidth,64);
                bool chosen = i==selectedHero;
                if (theme.Action(row,"",selected:chosen)) ChangeHero(i);
                theme.Label(new Rect(row.x+18,row.y+8,row.width-32,26),HeroIdentity.NameFor((HeroId)i),theme.Body);
                theme.Label(new Rect(row.x+18,row.y+35,row.width-32,22),Roles[i],theme.Small,true);
            }
            float infoWidth = 300;
            float portraitX=Left+rosterWidth+16, infoX=Right-infoWidth;
            DrawPortrait(new Rect(portraitX,212,infoX-portraitX-12,456));
            theme.Label(new Rect(infoX,232,infoWidth,72),HeroName,theme.Title);
            theme.Label(new Rect(infoX,320,infoWidth,28),Roles[selectedHero],theme.Heading);
            FrontEndTheme.Fill(new Rect(infoX,366,48,3),FrontEndTheme.Accent);
            theme.Label(new Rect(infoX,390,infoWidth,96),RoleDescriptions[selectedHero],theme.Body,true);
            theme.Label(new Rect(infoX,518,infoWidth,48),Strengths[selectedHero],theme.Small,true);
            if (theme.Action(new Rect(infoX,602,infoWidth,68),"DEPLOY",true)) SetPage(Page.Play);
        }
        private void ChangeHero(int index)
        {
            if (index==selectedHero) return;
            selectedHero=Mathf.Clamp(index,0,5);
            PlayerHeroSelection.Select((HeroId)selectedHero);
            showcase.Present((HeroId)selectedHero,true);
        }
        private void DrawShop()
        {
            Heading("Cosmetics","Personalize your identity. No combat advantages.");
            theme.Label(new Rect(Right-264,116,264,42),$"{PlayerWallet.Balance} SHARDS",theme.Heading);
            AvatarShopItem item=AvatarShopCatalog.Items[Mathf.Clamp(shopAvatarIndex,0,AvatarShopCatalog.Items.Length-1)];
            bool owned=AvatarInventory.IsOwned(item.AvatarId), equipped=PlayerProfile.Avatar==item.AvatarId;
            float galleryWidth=ContentWidth*.56f;
            theme.Panel(new Rect(Left,220,galleryWidth,440));
            ShopAvatarPreview.Draw(new Rect(Left+32,258,230,230),item,Texture2D.whiteTexture,Time.unscaledTime);
            theme.Label(new Rect(Left+292,264,galleryWidth-320,80),item.Name,theme.Heading);
            theme.Label(new Rect(Left+292,354,galleryWidth-320,52),item.Theme,theme.Body,true);
            theme.Label(new Rect(Left+292,428,galleryWidth-320,36),owned?"OWNED":$"{item.Price} SHARDS",theme.Body);
            if (theme.Action(new Rect(Left+32,542,64,60),"<")) { shopAvatarIndex=(shopAvatarIndex-1+AvatarShopCatalog.Items.Length)%AvatarShopCatalog.Items.Length; shopMessage=""; }
            theme.Center(new Rect(Left+102,542,90,60),$"{shopAvatarIndex+1} / {AvatarShopCatalog.Items.Length}");
            if (theme.Action(new Rect(Left+198,542,64,60),">")) { shopAvatarIndex=(shopAvatarIndex+1)%AvatarShopCatalog.Items.Length; shopMessage=""; }
            bool affordable=PlayerWallet.CanAfford(item.Price);
            string action=equipped?"EQUIPPED":owned?"EQUIP":affordable?"UNLOCK":"NEED MORE SHARDS";
            if (theme.Action(new Rect(Left+292,542,galleryWidth-320,60),action,true,enabled:!equipped&&(owned||affordable)))
            {
                if (owned) { PlayerProfile.Avatar=item.AvatarId; shopMessage="Avatar equipped."; }
                else { AvatarPurchaseResult result=AvatarShopService.TryPurchase(item); shopMessage=result==AvatarPurchaseResult.Purchased?"Unlocked and equipped.":result==AvatarPurchaseResult.InsufficientShards?"Not enough shards.":"Purchase could not be completed."; }
            }
            theme.Label(new Rect(Left+32,618,galleryWidth-64,26),shopMessage,theme.Small,true);
            float x=Left+galleryWidth+32, w=ContentWidth-galleryWidth-32;
            theme.Label(new Rect(x,244,w,36),"Earned in battle",theme.Heading);
            theme.Label(new Rect(x,296,w,100),"Complete matches to earn shards. Unlock avatars here and equip them on your profile.",theme.Body,true);
            FrontEndTheme.Fill(new Rect(x,436,w,1),FrontEndTheme.Raised);
            theme.Label(new Rect(x,462,w,40),"Premium cosmetics",theme.Heading);
            theme.Label(new Rect(x,518,w,62),FounderEntitlement.Owned?"First Traveler status active.\nYour owned cosmetics remain available.":"The real-money store is not available yet.",theme.Body,true);
            if(RecallCosmeticEntitlement.Owned) theme.Label(new Rect(x,600,w,48),RecallCosmeticCatalog.DisplayName+" / OWNED",theme.Small,true);
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
            int count=FounderEntitlement.Owned?7:6;
            for(int i=0;i<count;i++)
            {
                Rect avatar=new Rect(Left+24+i*60,592,52,54);
                if(theme.Action(avatar,"",selected:PlayerProfile.Avatar==i)) PlayerProfile.Avatar=i;
                AvatarBadgeRenderer.Draw(new Rect(avatar.x+5,avatar.y+5,42,44),i,Texture2D.whiteTexture,Time.unscaledTime);
            }
            if (theme.Action(new Rect(Left+identityWidth-112,592,88,54),"MORE")) SetPage(Page.Shop);
            float x=Left+identityWidth+32,w=ContentWidth-identityWidth-32;
            ProfileStats stats=ProfileStatsSummary.Current;
            theme.Label(new Rect(x,230,w,36),"Lifetime record",theme.Heading);
            Stat(x,290,w,"MATCHES",stats.Matches.ToString());
            Stat(x,346,w,"WINS / WIN RATE",$"{stats.Wins} / {stats.WinRate:0}%");
            Stat(x,402,w,"KILLS",ProgressionSummary.Kills.ToString());
            Stat(x,458,w,"ASSISTS",ProgressionSummary.Assists.ToString());
            Stat(x,514,w,"OBJECTIVES",ProgressionSummary.Objectives.ToString());
            Stat(x,570,w,"SHARDS",PlayerWallet.Balance.ToString());
            theme.Label(new Rect(x,634,w,32),"Account level grants no combat power.",theme.Small,true);
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
            theme.Label(new Rect(x,248,rw,38),"Language",theme.Heading);
            theme.Label(new Rect(x,296,rw,30),PlayerProfile.Language,theme.Body,true);
            theme.Label(new Rect(x,398,rw,38),"Local profile",theme.Heading);
            theme.Label(new Rect(x,446,rw,96),"Progress and preferences are saved on this device.",theme.Body,true);
            theme.Label(new Rect(x,588,rw,60),"Hear changes immediately. Saved automatically when you leave.",theme.Small,true);
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
            launching=false; visible=false; Time.timeScale=1;
            if (GameObject.Find("DF_MatchWorld")!=null) bootstrap.StartCoroutine(bootstrap.RestartPrototype());
            gameObject.SetActive(false);
        }
    }
}
