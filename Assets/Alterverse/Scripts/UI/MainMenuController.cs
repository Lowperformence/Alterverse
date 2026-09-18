using UnityEngine;using Alterverse.Characters;using Alterverse.Profile;using Alterverse.Visuals;using Alterverse.Shop;using Alterverse.Multiplayer;
namespace Alterverse.UI
{
public sealed class MainMenuController:MonoBehaviour
{
public static bool IsVisible=>instance!=null&&instance.visible;private enum Page{Home,Play,Heroes,Shop,Profile,Settings}private static MainMenuController instance;private Page page;private bool visible=true,launching;private GUIStyle title,subtitle,button,primary,small,panel,heroName,sideLabel;private Texture2D dimTexture,zeroTexture,lumenTexture,whiteTexture;private int selectedHero,shopAvatarIndex;private float openedAt,launchAt;private Page lastPage;private float pageChangedAt;private string profileName,profileMessage="",shopMessage="";private bool editingName;private readonly string[] roles={"ASSASSIN","TANK","MARKSMAN","MAGE","SUPPORT","BRUISER"};
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<MainMenuController>()==null)new GameObject("DF_MainMenu").AddComponent<MainMenuController>();}public static void ShowHome(){if(instance==null){new GameObject("DF_MainMenu").AddComponent<MainMenuController>();return;}instance.gameObject.SetActive(true);instance.page=Page.Home;instance.visible=true;instance.launching=false;instance.openedAt=Time.unscaledTime;Time.timeScale=0f;}private void Awake(){if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;selectedHero=(int)PlayerHeroSelection.Selected;profileName=PlayerProfile.PlayerName;openedAt=Time.unscaledTime;Time.timeScale=0;dimTexture=MakeTexture(StylizedArtDirection.Ink);zeroTexture=MakeTexture(StylizedArtDirection.ZeroDark);lumenTexture=MakeTexture(StylizedArtDirection.LumenDark);whiteTexture=MakeTexture(Color.white);}private void OnEnable(){if(visible)Time.timeScale=0;}private void SetPage(Page next){if(page==next)return;lastPage=page;page=next;pageChangedAt=Time.unscaledTime;}private void Update(){if(launching&&Time.unscaledTime-launchAt>.62f)EnterMatch();}
private Texture2D MakeTexture(Color c){Texture2D t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();return t;}private void OnDestroy(){if(instance==this)instance=null;DestroyTexture(dimTexture);DestroyTexture(zeroTexture);DestroyTexture(lumenTexture);DestroyTexture(whiteTexture);}private static void DestroyTexture(Texture2D t){if(t!=null)Destroy(t);}private const int MenuWidth=960,MenuHeight=720;
private void OnGUI()
{
if(!visible)return;
Matrix4x4 previousMatrix=GUI.matrix;Color previousColor=GUI.color;bool previousEnabled=GUI.enabled;
Rect safe=Screen.safeArea;
float scale=Mathf.Min(safe.width/MenuWidth,safe.height/MenuHeight);
float offsetX=safe.x+(safe.width-MenuWidth*scale)*.5f;
float offsetY=Screen.height-safe.yMax+(safe.height-MenuHeight*scale)*.5f;
GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),dimTexture);
GUI.matrix=Matrix4x4.TRS(new Vector3(offsetX,offsetY,0),Quaternion.identity,Vector3.one*scale);
try{EnsureStyles();DrawBackground();if(launching){DrawLaunch();return;}float reveal=pageChangedAt<=0?1f:Mathf.Clamp01((Time.unscaledTime-pageChangedAt)/.18f);GUI.color=new Color(1,1,1,.35f+.65f*reveal);switch(page){case Page.Play:DrawPlay();break;case Page.Heroes:DrawHeroes();break;case Page.Shop:DrawShop();break;case Page.Profile:DrawProfile();break;case Page.Settings:DrawSettings();break;default:DrawHome();break;}GUI.color=Color.white;}
finally{GUI.matrix=previousMatrix;GUI.color=previousColor;GUI.enabled=previousEnabled;}
}
private string HeroName(int index){return HeroIdentity.NameFor((HeroId)index).ToUpperInvariant();}
private void DrawBackground(){GUI.DrawTexture(new Rect(0,0,MenuWidth,MenuHeight),dimTexture);float t=Time.unscaledTime-openedAt,pulse=.5f+.5f*Mathf.Sin(t*1.35f),collision=Mathf.Sin(t*.7f)*MenuWidth*.006f;GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,MenuWidth*.505f+collision,MenuHeight),zeroTexture);GUI.DrawTexture(new Rect(MenuWidth*.495f+collision,0,MenuWidth*.505f,MenuHeight),lumenTexture);float center=MenuWidth*.5f+collision;for(int i=0;i<12;i++){float phase=t*(.55f+i*.025f)+i*1.37f,y=(i+.4f)*MenuHeight/12f+Mathf.Sin(phase*1.7f)*15,x=center+Mathf.Sin(phase)*14;GUI.color=i%2==0?new Color(StylizedArtDirection.ZeroEnergy.r,StylizedArtDirection.ZeroEnergy.g,StylizedArtDirection.ZeroEnergy.b,.25f+.2f*pulse):new Color(StylizedArtDirection.LumenEnergy.r,StylizedArtDirection.LumenEnergy.g,StylizedArtDirection.LumenEnergy.b,.25f+.2f*pulse);GUI.DrawTexture(new Rect(x-2,y-18,4,36),whiteTexture);}GUI.color=Color.white;GUI.DrawTexture(new Rect(center-(3+5*pulse)*.5f,0,3+5*pulse,MenuHeight),whiteTexture);DrawComicCuts(center,t);GUI.Label(new Rect(18,MenuHeight*.48f,MenuWidth*.22f,32),"DIMENSION ZERO",sideLabel);GUI.Label(new Rect(MenuWidth*.78f-18,MenuHeight*.48f,MenuWidth*.22f,32),"LUMEN",sideLabel);}
private void DrawComicCuts(float center,float t){for(int i=0;i<5;i++){float y=MenuHeight*(.13f+i*.18f),drift=Mathf.Sin(t*.45f+i)*8;GUI.color=new Color(1,1,1,.07f);GUI.DrawTexture(new Rect(0,y+drift,center*.7f,2),whiteTexture);GUI.DrawTexture(new Rect(center+center*.3f,y-drift,MenuWidth-(center+center*.3f),2),whiteTexture);}GUI.color=Color.white;}
private void DrawHome()
{
float t=Time.unscaledTime-openedAt;
GUI.Label(new Rect(42,28,560,62),"DIMENSIONS FRONT",title);
GUI.Label(new Rect(48,84,500,24),"TWO REALITIES. ONE FRONT.",subtitle);DimensionUiMotif.DrawHeaderLine(new Rect(42,112,878,2),whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
Rect profile=new Rect(690,28,230,72);DimensionUiMotif.DrawSplitPanel(profile,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(704,37,202,23),PlayerProfile.PlayerName.ToUpperInvariant(),subtitle);GUI.Label(new Rect(704,62,202,20),$"LEVEL {PlayerExperience.Level}   •   ◆ {PlayerWallet.Balance}",small);

Rect heroStage=new Rect(355,126,565,430);DimensionUiMotif.DrawSplitPanel(heroStage,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(382,146,510,22),"ACTIVE HERO",small);
float pulse=.5f+.5f*Mathf.Sin(t*1.25f);GUI.color=new Color(1,1,1,.05f+.04f*pulse);GUI.DrawTexture(new Rect(470,184,330,330),whiteTexture);GUI.color=Color.white;
StylizedHeroCard.Draw(new Rect(520,174,230,292),(HeroId)selectedHero,whiteTexture,Time.unscaledTime);
GUI.Label(new Rect(402,468,470,46),HeroName(selectedHero),heroName);GUI.Label(new Rect(402,511,470,26),roles[selectedHero],subtitle);
if(GUI.Button(new Rect(377,239,54,54),"‹",button))ChangeHero(-1);if(GUI.Button(new Rect(844,239,54,54),"›",button))ChangeHero(1);

Rect nav=new Rect(42,126,282,430);DimensionUiMotif.DrawSplitPanel(nav,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(64,145,238,24),"FRONT COMMAND",subtitle);
if(GUI.Button(new Rect(64,190,238,76),"PLAY",primary))SetPage(Page.Play);
if(GUI.Button(new Rect(64,278,238,48),"HEROES",button))SetPage(Page.Heroes);
if(GUI.Button(new Rect(64,336,238,48),"COSMETICS",button))SetPage(Page.Shop);
if(GUI.Button(new Rect(64,394,238,48),"PROFILE",button))SetPage(Page.Profile);
if(GUI.Button(new Rect(64,452,238,48),"SETTINGS",button))SetPage(Page.Settings);

Rect status=new Rect(42,578,878,96);DimensionUiMotif.DrawSplitPanel(status,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(66,590,190,22),"DIMENSIONS FRONT",small);GUI.Label(new Rect(66,614,190,30),"AI 5v5 READY",subtitle);
GUI.Label(new Rect(300,590,250,22),"SELECTED",small);GUI.Label(new Rect(300,614,250,30),$"{HeroName(selectedHero)}  •  {roles[selectedHero]}",subtitle);
GUI.Label(new Rect(594,590,300,22),FounderEntitlement.Owned?"FOUNDER STATUS":"ACCOUNT PROGRESSION",small);
GUI.Label(new Rect(594,614,300,30),FounderEntitlement.Owned?"◆ FIRST TRAVELER ◆":$"LEVEL {PlayerExperience.Level}  •  EXPERIENCE ONLY",subtitle);
}
private void DrawPlay()
{
GUI.Label(new Rect(42,28,620,62),"ENTER THE FRONT",title);GUI.Label(new Rect(48,84,500,24),"DEPLOYMENT",subtitle);
DimensionUiMotif.DrawHeaderLine(new Rect(42,112,878,2),whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
Rect mode=new Rect(42,142,560,466);DimensionUiMotif.DrawSplitPanel(mode,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(68,164,508,30),"SELECT MATCH TYPE",subtitle);
GUI.Label(new Rect(72,210,210,22),"AVAILABLE",small);
if(GUI.Button(new Rect(72,238,500,102),"AI MATCH   5v5\nFULL TEAM BATTLE • LOCAL",primary))BeginLaunch();
GUI.Label(new Rect(88,350,468,42),"Two lanes • Reality Bastions • Dimension planets • Reality Core",small);
GUI.Label(new Rect(72,408,210,22),"NETWORK",small);GUI.enabled=false;GUI.Button(new Rect(72,436,500,82),"MULTIPLAYER   5v5\nCOMING SOON",button);GUI.enabled=true;
GUI.Label(new Rect(72,530,500,38),"NO PAY-TO-WIN • ACCOUNT LEVEL IS EXPERIENCE ONLY",small);
Rect loadout=new Rect(626,142,294,466);DimensionUiMotif.DrawSplitPanel(loadout,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(648,164,250,24),"YOUR DEPLOYMENT",subtitle);StylizedHeroCard.Draw(new Rect(680,205,186,236),(HeroId)selectedHero,whiteTexture,Time.unscaledTime);
GUI.Label(new Rect(646,451,254,38),HeroName(selectedHero),heroName);GUI.Label(new Rect(646,489,254,24),roles[selectedHero],subtitle);
if(GUI.Button(new Rect(654,535,238,46),"CHANGE HERO",button))SetPage(Page.Heroes);
if(GUI.Button(new Rect(42,632,180,46),"BACK",button))SetPage(Page.Home);
}
private void DrawHeroes()
{
GUI.Label(new Rect(42,28,620,62),"HEROES",title);GUI.Label(new Rect(48,84,500,24),"CHOOSE YOUR FIGHTER",subtitle);
DimensionUiMotif.DrawHeaderLine(new Rect(42,112,878,2),whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
Rect roster=new Rect(42,142,286,470);DimensionUiMotif.DrawSplitPanel(roster,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(62,162,246,24),"ROSTER  •  6 HEROES",subtitle);
for(int i=0;i<6;i++){float y=202+i*61;bool chosen=i==selectedHero;GUI.enabled=!chosen;if(GUI.Button(new Rect(62,y,246,48),$"{HeroName(i)}   •   {roles[i]}",chosen?primary:button))ChangeHero(i-selectedHero);GUI.enabled=true;}
Rect showcase=new Rect(350,142,570,470);DimensionUiMotif.DrawSplitPanel(showcase,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(374,162,522,24),"SELECTED HERO",small);StylizedHeroCard.Draw(new Rect(520,190,230,292),(HeroId)selectedHero,whiteTexture,Time.unscaledTime);
GUI.Label(new Rect(390,486,490,46),HeroName(selectedHero),heroName);GUI.Label(new Rect(390,530,490,26),roles[selectedHero],subtitle);
GUI.Label(new Rect(390,561,490,24),$"HERO {selectedHero+1} / 6   •   READY FOR DEPLOYMENT",small);
if(GUI.Button(new Rect(42,632,180,46),"BACK",button))SetPage(Page.Home);if(GUI.Button(new Rect(710,632,210,46),"DEPLOY",primary))SetPage(Page.Play);
}
private void DrawShop()
{
GUI.Label(new Rect(42,28,620,62),"COSMETICS",title);GUI.Label(new Rect(48,84,500,24),"CUSTOMIZE YOUR IDENTITY",subtitle);DimensionUiMotif.DrawHeaderLine(new Rect(42,112,878,2),whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);
GUI.Label(new Rect(690,52,230,30),$"{AvatarShopCatalog.CurrencyName}   ◆ {PlayerWallet.Balance}",subtitle);
AvatarShopItem item=AvatarShopCatalog.Items[Mathf.Clamp(shopAvatarIndex,0,AvatarShopCatalog.Items.Length-1)];bool owned=AvatarInventory.IsOwned(item.AvatarId),equipped=PlayerProfile.Avatar==item.AvatarId;
Rect collection=new Rect(42,142,520,466);DimensionUiMotif.DrawSplitPanel(collection,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);GUI.Label(new Rect(64,160,476,26),"AVATAR COLLECTION",subtitle);
ShopAvatarPreview.Draw(new Rect(92,205,230,230),item,whiteTexture,Time.unscaledTime);GUI.Label(new Rect(338,222,200,38),item.Name,heroName);GUI.Label(new Rect(338,263,200,24),item.Theme,small);GUI.Label(new Rect(338,300,200,30),owned?"OWNED":$"◆ {item.Price}",subtitle);
GUI.enabled=!equipped&&(owned||PlayerWallet.CanAfford(item.Price));string action=equipped?"EQUIPPED":owned?"EQUIP":"UNLOCK";if(GUI.Button(new Rect(338,350,190,54),action,primary)){if(owned){PlayerProfile.Avatar=item.AvatarId;shopMessage="AVATAR EQUIPPED";}else{AvatarPurchaseResult result=AvatarShopService.TryPurchase(item);shopMessage=result==AvatarPurchaseResult.Purchased?"UNLOCKED AND EQUIPPED":result==AvatarPurchaseResult.InsufficientShards?"NOT ENOUGH SHARDS":result.ToString().ToUpperInvariant();}}GUI.enabled=true;
if(GUI.Button(new Rect(92,470,54,42),"‹",button))shopAvatarIndex=(shopAvatarIndex-1+AvatarShopCatalog.Items.Length)%AvatarShopCatalog.Items.Length;if(GUI.Button(new Rect(268,470,54,42),"›",button))shopAvatarIndex=(shopAvatarIndex+1)%AvatarShopCatalog.Items.Length;GUI.Label(new Rect(150,478,114,24),$"{shopAvatarIndex+1} / {AvatarShopCatalog.Items.Length}",small);
if(!string.IsNullOrEmpty(shopMessage))GUI.Label(new Rect(72,540,460,30),shopMessage,subtitle);
Rect premium=new Rect(584,142,336,466);DimensionUiMotif.DrawSplitPanel(premium,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);GUI.Label(new Rect(606,160,292,26),"FEATURED",subtitle);GUI.Label(new Rect(606,206,292,34),RecallCosmeticCatalog.DisplayName,subtitle);GUI.Label(new Rect(606,244,292,22),RecallCosmeticCatalog.Category,small);GUI.Label(new Rect(616,282,272,68),RecallCosmeticCatalog.Description,small);GUI.Label(new Rect(606,360,292,38),RecallCosmeticCatalog.TargetPrice,heroName);GUI.enabled=false;GUI.Button(new Rect(620,414,264,52),RecallCosmeticEntitlement.Owned?"OWNED":"STORE COMING SOON",primary);GUI.enabled=true;GUI.Label(new Rect(606,500,292,24),FounderEntitlement.Owned?"◆ FIRST TRAVELER ◆":"FIRST FRACTURE",subtitle);GUI.Label(new Rect(606,532,292,42),"COSMETIC ONLY\nNO GAMEPLAY ADVANTAGE",small);
if(GUI.Button(new Rect(42,632,180,46),"BACK",button)){shopMessage="";SetPage(Page.Home);}
}
private void ChangeHero(int direction){selectedHero=(selectedHero+direction+6)%6;PlayerHeroSelection.Select((HeroId)selectedHero);}private void DrawProfile(){float w=820,x=70,y=150;GUI.Label(new Rect(42,28,620,62),"PLAYER PROFILE",title);DimensionUiMotif.DrawHeaderLine(new Rect(42,112,878,2),whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);Rect profilePanel=new Rect(x,y,w,390);DimensionUiMotif.DrawSplitPanel(profilePanel,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);GUI.Label(new Rect(x+30,y+18,w-60,26),PlayerProfile.HasFounderFrame?"◆ FIRST FRACTURE PROFILE ◆":"DIMENSIONS FRONT PROFILE",subtitle);GUI.Label(new Rect(x+30,y+48,w-60,24),$"{PlayerProfile.DisplayTitle}  •  LEVEL {PlayerExperience.Level}",small);float xpProgress=PlayerExperience.LevelProgress;GUI.Label(new Rect(x+55,y+72,w-110,16),PlayerExperience.Level>=PlayerExperience.MaxLevel?"MAX EXPERIENCE":$"{PlayerExperience.XpIntoLevel} / {PlayerExperience.XpIntoLevel+PlayerExperience.XpToNextLevel} XP",small);GUI.Box(new Rect(x+55,y+91,w-110,8),"");if(xpProgress>0f)GUI.Box(new Rect(x+55,y+91,(w-110)*xpProgress,8),"");GUI.Label(new Rect(x+30,y+106,w-60,28),"DISPLAY NAME",subtitle);GUI.enabled=editingName;profileName=GUI.TextField(new Rect(x+55,y+140,w-285,42),profileName,PlayerProfile.MaxNameLength);GUI.enabled=true;GUI.Label(new Rect(x+55,y+181,w-285,18),$"{profileName.Length}/{PlayerProfile.MaxNameLength}  •  3–16 CHARACTERS",small);if(!editingName){if(GUI.Button(new Rect(x+w-210,y+140,155,42),"EDIT NAME",button)){editingName=true;profileMessage="";}}else{if(GUI.Button(new Rect(x+w-210,y+140,74,42),"SAVE",primary)){if(PlayerProfile.IsValidName(profileName)){PlayerProfile.PlayerName=profileName;profileName=PlayerProfile.PlayerName;profileMessage="NAME SAVED";editingName=false;}else profileMessage="3–16: LETTERS, NUMBERS, SPACE, _ OR -";}if(GUI.Button(new Rect(x+w-128,y+140,74,42),"CANCEL",button)){profileName=PlayerProfile.PlayerName;profileMessage="";editingName=false;}}GUI.Label(new Rect(x+30,y+181,w-60,22),profileMessage,small);GUI.Label(new Rect(x+30,y+204,w-60,28),"AVATAR",subtitle);int avatarCount=FounderEntitlement.Owned?7:6,totalWidth=avatarCount*52;float ax=x+(w-totalWidth)*.5f;for(int i=0;i<avatarCount;i++){bool chosen=PlayerProfile.Avatar==i;string label=i==6?"◆":(i+1).ToString();GUI.enabled=!chosen;if(GUI.Button(new Rect(ax+i*52,y+238,44,44),label,chosen?primary:button))PlayerProfile.Avatar=i;GUI.enabled=true;}GUI.Label(new Rect(x+30,y+292,w-60,25),FounderEntitlement.Owned?"Founder avatar unlocked • permanent account cosmetic":"Built-in Dimensions Front avatars",small);ProfileStats stats=ProfileStatsSummary.Current;GUI.Label(new Rect(x+30,y+316,w-60,22),$"MATCHES {stats.Matches}   •   WINS {stats.Wins}   •   WIN RATE {stats.WinRate:0}%",small);GUI.Label(new Rect(x+30,y+336,w-60,22),$"KILLS {ProgressionSummary.Kills}   •   ASSISTS {ProgressionSummary.Assists}   •   OBJECTIVES {ProgressionSummary.Objectives}",small);if(GUI.Button(new Rect(42,632,180,46),"BACK",button)){profileName=PlayerProfile.PlayerName;profileMessage="";editingName=false;SetPage(Page.Home);}}
private void DrawSettings(){float w=820,x=70,y=150;GUI.Label(new Rect(42,28,620,62),"SETTINGS",title);GUI.Label(new Rect(48,84,500,24),"SYSTEM & AUDIO",subtitle);DimensionUiMotif.DrawHeaderLine(new Rect(42,112,878,2),whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);Rect settingsPanel=new Rect(x,y,w,390);DimensionUiMotif.DrawSplitPanel(settingsPanel,whiteTexture,StylizedArtDirection.ZeroEnergy,StylizedArtDirection.LumenEnergy,Time.unscaledTime);DrawVolume(x,y+35,w,"MASTER VOLUME",PlayerProfile.MasterVolume,v=>PlayerProfile.MasterVolume=v);DrawVolume(x,y+105,w,"MUSIC",PlayerProfile.MusicVolume,v=>PlayerProfile.MusicVolume=v);DrawVolume(x,y+175,w,"SFX",PlayerProfile.SfxVolume,v=>PlayerProfile.SfxVolume=v);GUI.Label(new Rect(x+30,y+245,w-60,28),"LANGUAGE  •  ENGLISH",subtitle);GUI.Label(new Rect(x+30,y+278,w-60,22),"More languages will be added later.",small);if(GUI.Button(new Rect(42,632,180,46),"BACK",button))SetPage(Page.Home);}private void DrawVolume(float x,float y,float w,string name,float value,System.Action<float> set){GUI.Label(new Rect(x+30,y,w-60,24),$"{name}  {Mathf.RoundToInt(value*100)}%",small);float next=GUI.HorizontalSlider(new Rect(x+75,y+31,w-150,22),value,0,1);if(!Mathf.Approximately(next,value))set(next);}
private void BeginLaunch(){PlayerHeroSelection.Select((HeroId)selectedHero);launching=true;launchAt=Time.unscaledTime;}private void DrawLaunch(){float p=Mathf.Clamp01((Time.unscaledTime-launchAt)/.62f),ease=p*p*(3-2*p),w=Mathf.Lerp(MenuWidth*.02f,MenuWidth*.72f,ease);GUI.color=new Color(1,1,1,.12f+.65f*ease);GUI.DrawTexture(new Rect(MenuWidth*.5f-w*.5f,0,w,MenuHeight),whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(0,MenuHeight*.43f,MenuWidth,70),p>.48f?"ENTER THE FRONT":"REALITY COLLISION",heroName);}private void EnterMatch(){launching=false;visible=false;Time.timeScale=1;
if(GameObject.Find("DF_MatchWorld")!=null){Alterverse.Prototype.PrototypeBootstrap bootstrap=FindAnyObjectByType<Alterverse.Prototype.PrototypeBootstrap>();if(bootstrap!=null)bootstrap.StartCoroutine(bootstrap.RestartPrototype());}
gameObject.SetActive(false);}
private void EnsureStyles(){if(title!=null)return;title=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(MenuWidth/18,34,66),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};subtitle=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(MenuWidth/48,15,23),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};button=new GUIStyle(GUI.skin.button){fontSize=Mathf.Clamp(MenuWidth/48,15,22),fontStyle=FontStyle.Bold};primary=new GUIStyle(button){fontSize=Mathf.Clamp(MenuWidth/38,20,29)};small=new GUIStyle(GUI.skin.label){fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true};panel=new GUIStyle(GUI.skin.box);heroName=new GUIStyle(title){fontSize=Mathf.Clamp(MenuWidth/24,27,46)};sideLabel=new GUIStyle(small){fontSize=12,alignment=TextAnchor.MiddleCenter};}
}
}
